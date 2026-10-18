using NUnit.Framework;

namespace FactsPDF.Tests;

internal static class JpegFixture
{
    internal static byte[] Counts(params (int Length, int Count)[] entries)
    {
        var values = new byte[16]; foreach (var (length, count) in entries) values[length - 1] = (byte)count;
        return values;
    }
    internal static byte[] Segment(byte marker, byte[] payload)
        => [255, marker, (byte)((payload.Length + 2) >> 8), (byte)(payload.Length + 2), .. payload];

    // Hand-authored one-component baseline image. A single zero DC followed by
    // EOB is 00, then 1 padding to a byte. No product encoder/parser is used.
    internal static byte[] Gray(byte[] entropy, int width = 8, int height = 8,
        byte[]? dcCounts = null, byte[]? dcValues = null, byte[]? acCounts = null, byte[]? acValues = null,
        int restart = 0, byte[]? metadata = null)
    {
        var data = new List<byte> { 255, 216 };
        data.AddRange(Segment(0xe0, [74, 70, 73, 70, 0, 1, 1, 0, 0, 1, 0, 1, 0, 0]));
        if (metadata is not null) data.AddRange(metadata);
        data.AddRange(Segment(0xdb, [0, .. Enumerable.Repeat((byte)1, 64)]));
        data.AddRange(Segment(0xc0, [8, (byte)(height >> 8), (byte)height, (byte)(width >> 8), (byte)width, 1, 1, 0x11, 0]));
        data.AddRange(Segment(0xc4, [0, .. dcCounts ?? Counts((1, 1)), .. dcValues ?? [0]]));
        data.AddRange(Segment(0xc4, [0x10, .. acCounts ?? Counts((1, 1)), .. acValues ?? [0]]));
        if (restart != 0) data.AddRange(Segment(0xdd, [(byte)(restart >> 8), (byte)restart]));
        data.AddRange(Segment(0xda, [1, 1, 0, 0, 63, 0]));
        data.AddRange(entropy); data.AddRange([255, 217]);
        return data.ToArray();
    }

    internal static byte[] Bits(string bits)
    {
        bits = bits.PadRight((bits.Length + 7) / 8 * 8, '1'); var data = new List<byte>();
        for (var i = 0; i < bits.Length; i += 8)
        { var value = Convert.ToByte(bits.Substring(i, 8), 2); data.Add(value); if (value == 255) data.Add(0); }
        return data.ToArray();
    }
}

public sealed class JpegEntropyTests
{
    private static void Invalid(byte[] bytes) => Assert.That(Assert.Throws<FactsPdfException>(
        () => PdfImage.LoadImage(bytes))!.Code, Is.EqualTo("FPDF1601"));

    [Test]
    public void OneDcZeroAndEobConsumesExactlyOneBlock()
        => Assert.That(PdfImage.LoadImage(JpegFixture.Gray([0x3f])).PixelWidth, Is.EqualTo(8));

    [TestCase(0x3e)] [TestCase(0x7f)] [TestCase(0xff)]
    public void InvalidPaddingCodeOrUnstuffedByteIsRejected(int encoded)
        => Invalid(JpegFixture.Gray([(byte)encoded]));

    [Test]
    public void DcAmplitudeAndAcMagnitudeUpperCategoriesAreSupported()
    {
        Assert.DoesNotThrow(() => PdfImage.LoadImage(JpegFixture.Gray(JpegFixture.Bits("0" + new string('1', 11) + "0"), dcValues: [11])));
        // DC0 (0), AC category10 (0 + 10 amplitude bits), EOB (10).
        Assert.DoesNotThrow(() => PdfImage.LoadImage(JpegFixture.Gray(JpegFixture.Bits("00" + new string('1', 10) + "10"),
            acCounts: JpegFixture.Counts((1, 1), (2, 1)), acValues: [0x0a, 0])));
    }

    [Test]
    public void StuffedFfIsEntropyAndNeverAMarker()
        => Assert.DoesNotThrow(() => PdfImage.LoadImage(JpegFixture.Gray([0, 255, 0, 0xef],
            dcCounts: JpegFixture.Counts((8, 1)), dcValues: [11])));

    [Test]
    public void TruncatedAmplitudeAndEarlyEoiAreRejected()
    {
        Invalid(JpegFixture.Gray([0x7f], dcValues: [8]));
        Invalid(JpegFixture.Gray([0x3f], width: 16));
        Invalid(JpegFixture.Gray([]));
    }

    [Test]
    public void ThreeZeroRunsMayEndButAFourthCannotOverflowTheBlock()
    {
        var counts = JpegFixture.Counts((1, 1), (2, 1)); byte[] values = [0xf0, 0];
        Assert.DoesNotThrow(() => PdfImage.LoadImage(JpegFixture.Gray(JpegFixture.Bits("000010"), acCounts: counts, acValues: values)));
        Invalid(JpegFixture.Gray(JpegFixture.Bits("0000010"), acCounts: counts, acValues: values));
    }

    [Test]
    public void LastCoefficientMayBePresentWithoutEobButCannotRunPastIt()
    {
        var counts = JpegFixture.Counts((1, 1), (2, 1));
        Assert.DoesNotThrow(() => PdfImage.LoadImage(JpegFixture.Gray(JpegFixture.Bits("0000101"), acCounts: counts, acValues: [0xf0, 0xe1])));
        Invalid(JpegFixture.Gray(JpegFixture.Bits("0000101"), acCounts: counts, acValues: [0xf0, 0xf1]));
    }

    [Test]
    public void InvalidCategoriesAndReservedAllOnesCodesAreRejected()
    {
        Invalid(JpegFixture.Gray([0x3f], dcValues: [12]));
        Invalid(JpegFixture.Gray([0x3f], acValues: [0x0b]));
        Invalid(JpegFixture.Gray([0x3f], acValues: [0x10]));
        Invalid(JpegFixture.Gray([0x3f], dcCounts: JpegFixture.Counts((1, 2)), dcValues: [0, 1]));
        Invalid(JpegFixture.Gray([0x3f], dcCounts: JpegFixture.Counts(), dcValues: []));
    }

    [Test]
    public void RestartAtTheExactMcuIntervalResetsTheBitBoundary()
    {
        Assert.DoesNotThrow(() => PdfImage.LoadImage(JpegFixture.Gray([0x3f, 255, 208, 0x3f], width: 16, restart: 1)));
        Assert.DoesNotThrow(() => PdfImage.LoadImage(JpegFixture.Gray([0x3f, 255, 255, 255, 208, 0x3f], width: 16, restart: 1)));
        Invalid(JpegFixture.Gray([0x3f, 255, 209, 0x3f], width: 16, restart: 1));
        Invalid(JpegFixture.Gray([0x0f], width: 16, restart: 1));
        Invalid(JpegFixture.Gray([0x3f, 255, 208], restart: 1));
        Invalid(JpegFixture.Gray([0x3f, 255, 208, 0x3f], width: 16));
    }

    [Test]
    public void LegalMarkerFillIsBoundedAndDistinctFromStuffing()
    {
        var plain = JpegFixture.Gray([0x3f]);
        var longFill = plain[..2].Concat(Enumerable.Repeat((byte)255, 100_000)).Concat(plain[2..]).ToArray();
        Assert.DoesNotThrow(() => PdfImage.LoadImage(longFill));
        Assert.DoesNotThrow(() => PdfImage.LoadImage(JpegFixture.Gray([0x3f, 255, 255])));
        Invalid(JpegFixture.Gray([0x3f, 255, 255, 0]));
        Invalid(plain[..^1]); // Only FF before EOF, no marker code.
        Invalid(JpegFixture.Gray([0x3f, 0])); // Extra entropy after expected blocks.
    }

    [Test]
    public void MetadataAndSegmentCapsHaveIndependentExactBoundaries()
    {
        Assert.DoesNotThrow(() => PdfImage.LoadImage(JpegFixture.Gray([0x3f],
            metadata: JpegFixture.Segment(0xfe, new byte[65_536 - 14]))));
        Assert.That(Assert.Throws<FactsPdfException>(() => PdfImage.LoadImage(JpegFixture.Gray([0x3f],
            metadata: JpegFixture.Segment(0xfe, new byte[65_536 - 13]))))!.Code, Is.EqualTo("FPDF1603"));
        byte[] Comments(int count) => Enumerable.Range(0, count).SelectMany(_ => JpegFixture.Segment(0xfe, [])).ToArray();
        Assert.DoesNotThrow(() => PdfImage.LoadImage(JpegFixture.Gray([0x3f], metadata: Comments(250))));
        Assert.That(Assert.Throws<FactsPdfException>(() => PdfImage.LoadImage(JpegFixture.Gray([0x3f], metadata: Comments(251))))!.Code,
            Is.EqualTo("FPDF1603"));
    }
}
