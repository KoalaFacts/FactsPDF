using System.Buffers.Binary;
using NUnit.Framework;

namespace FactsPDF.Tests;

public sealed class JpegExifTests
{
    private static byte[] Tiff(bool little, params (ushort Tag, ushort Type, uint Count, uint Value)[] entries)
    {
        var data = new byte[8 + 2 + entries.Length * 12 + 4];
        data[0] = data[1] = (byte)(little ? 'I' : 'M');
        void U16(int at, ushort value)
        { if (little) BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(at), value); else BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(at), value); }
        void U32(int at, uint value)
        { if (little) BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(at), value); else BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(at), value); }
        U16(2, 42); U32(4, 8); U16(8, (ushort)entries.Length);
        for (var i = 0; i < entries.Length; i++)
        {
            var e = entries[i]; var at = 10 + i * 12;
            U16(at, e.Tag); U16(at + 2, e.Type); U32(at + 4, e.Count);
            if (e.Type == 3 && e.Count == 1) U16(at + 8, (ushort)e.Value); else U32(at + 8, e.Value);
        }
        return data;
    }
    private static byte[] Jpeg(byte[] tiff) => JpegFixture.Gray([0x3f], metadata: JpegFixture.Segment(0xe1, [69, 120, 105, 102, 0, 0, .. tiff]));
    private static void Error(byte[] tiff, string code) => Assert.That(Assert.Throws<FactsPdfException>(() => PdfImage.LoadImage(Jpeg(tiff)))!.Code, Is.EqualTo(code));

    [TestCase(false, 0)][TestCase(false, 13)][TestCase(false, 65535)]
    [TestCase(true, 0)][TestCase(true, 13)][TestCase(true, 65535)]
    public void UnknownOrientationTypeIsMalformedRatherThanUnsupported(bool little, int type)
        => Error(Tiff(little, (274, (ushort)type, 1, 1)), "FPDF1601");

    [TestCase(false)][TestCase(true)]
    public void BothByteOrdersEnforceOrientationCountValueAndUniqueness(bool little)
    {
        Assert.DoesNotThrow(() => PdfImage.LoadImage(Jpeg(Tiff(little, (274, 3, 1, 1)))));
        Assert.DoesNotThrow(() => PdfImage.LoadImage(Jpeg(Tiff(little))));
        Error(Tiff(little, (274, 3, 1, 1), (274, 3, 1, 1)), "FPDF1601");
        Error(Tiff(little, (274, 4, 1, 1)), "FPDF1601");
        Error(Tiff(little, (274, 3, 0, 1)), "FPDF1601");
        Error(Tiff(little, (274, 3, 2, 1)), "FPDF1601");
        Error(Tiff(little, (274, 3, 1, 0)), "FPDF1601");
        Error(Tiff(little, (274, 3, 1, 9)), "FPDF1601");
        for (uint value = 2; value <= 8; value++) Error(Tiff(little, (274, 3, 1, value)), "FPDF1602");
        Error(Tiff(little, (100, 13, 1, 1)), "FPDF1602");
    }

    [TestCase(false)][TestCase(true)]
    public void IfdEntryCapAcceptsExactly256AndRejects257(bool little)
    {
        var entries = Enumerable.Range(0, 257).Select(i => ((ushort)(1000 + i), (ushort)1, (uint)1, (uint)0)).ToArray();
        Assert.DoesNotThrow(() => PdfImage.LoadImage(Jpeg(Tiff(little, entries[..256]))));
        Error(Tiff(little, entries), "FPDF1603");
    }

    [TestCase(false)][TestCase(true)]
    public void OutOfLineFieldsUseWideCountsAndExactSegmentBounds(bool little)
    {
        var valid = Tiff(little, (100, 7, 8, 26)).Concat(new byte[8]).ToArray();
        Assert.DoesNotThrow(() => PdfImage.LoadImage(Jpeg(valid)));
        Error(valid[..^1], "FPDF1601");
        Error(Tiff(little, (100, 7, 8, uint.MaxValue)), "FPDF1601");
        Error(Tiff(little, (100, 12, uint.MaxValue, 26)), "FPDF1601");
        Error(Tiff(little, (100, 7, 8, 26)), "FPDF1601");
    }

    [Test]
    public void ExifValidatorChecksCancellationWithinItsBoundedEntryLoop()
    {
        var tiff = Tiff(true, (274, 3, 1, 1));
        Assert.Throws<OperationCanceledException>(() => JpegExif.Validate(tiff, new CancellationToken(true)));
    }
}
