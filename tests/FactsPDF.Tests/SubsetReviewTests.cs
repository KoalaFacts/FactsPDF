using System.Buffers.Binary;
using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

[TestFixture]
public sealed class SubsetReviewTests
{
    private static int Read32(byte[] data, int at) => checked((int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(at, 4)));

    [Test]
    public void FirstCompositeComponentCannotAttachToNonexistentParentPoints()
    {
        var data = FontFixture.Create(); var offset = FontFixture.Table(data, "glyf") + Read32(data, FontFixture.Table(data, "loca") + 7 * 4);
        Array.Clear(data, offset, 34); FontFixture.U16(data, offset, 65535);
        FontFixture.U16(data, offset + 10, 1); // Word arguments, but not XY: no prior parent points exist.
        FontFixture.U16(data, offset + 12, 5);
        var font = PdfFont.LoadTrueType(data);
        Assert.That(Assert.Throws<FactsPdfException>(() => TrueTypeSubsetter.Create(font, [0x6587]))!.Code, Is.EqualTo("FPDF1506"));
    }

    [Test]
    public void CompositeDepthIsBoundedBeforeStackExhaustion()
    {
        var original = FontFixture.Create(); var tables = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
        var tableCount = BinaryPrimitives.ReadUInt16BigEndian(original.AsSpan(4, 2));
        for (var i = 0; i < tableCount; i++)
        {
            var at = 12 + 16 * i; var tag = Encoding.ASCII.GetString(original, at, 4);
            tables[tag] = original.AsSpan(Read32(original, at + 8), Read32(original, at + 12)).ToArray();
        }
        const int count = 70;
        tables["glyf"] = new byte[count * 20]; tables["loca"] = new byte[(count + 1) * 4]; tables["hmtx"] = new byte[count * 4];
        FontFixture.U16(tables["maxp"], 4, count); FontFixture.U16(tables["hhea"], 34, count); FontFixture.U32(tables["head"], 8, 0);
        for (var i = 0; i <= count; i++) FontFixture.U32(tables["loca"], i * 4, (uint)(i * 20));
        for (var i = 1; i < count - 1; i++)
        {
            FontFixture.U16(tables["glyf"], i * 20, 65535); FontFixture.U16(tables["glyf"], i * 20 + 10, 3);
            FontFixture.U16(tables["glyf"], i * 20 + 12, i + 1);
        }
        var font = PdfFont.LoadTrueType(SubsetSfnt.Build(tables, CancellationToken.None));
        Assert.That(Assert.Throws<FactsPdfException>(() => TrueTypeSubsetter.Create(font, [65]))!.Code, Is.EqualTo("FPDF1506"));
    }

    [TestCase(8188)] [TestCase(8189)] [TestCase(9000)]
    public void LargeAliasCmapStaysValidAcrossFormat4LengthLimit(int count)
    {
        var chars = new SortedDictionary<int, ushort>(); for (var i = 1; i <= count; i++) chars.Add(i, 1);
        var parsed = new UnicodeCmap(SubsetSfnt.Cmap(chars), 2, CancellationToken.None);
        Assert.That(parsed.GlyphFor(count), Is.EqualTo(1)); Assert.That(parsed.GlyphFor(count + 1), Is.Zero);
    }

    [Test]
    public void ReusingFontForParallelSubsetsDoesNotLeakGlyphsBetweenDocuments()
    {
        var font = PdfFont.LoadTrueType(FontFixture.Create());
        var expected = TrueTypeSubsetter.Create(font, [65, 0x4e2d]).Bytes;
        Parallel.For(0, 12, i =>
        {
            var chars = i % 2 == 0 ? new[] { 65, 0x4e2d } : new[] { 87 };
            var result = TrueTypeSubsetter.Create(font, chars);
            if (i % 2 == 0) Assert.That(result.Bytes, Is.EqualTo(expected));
            else Assert.That(PdfFont.LoadTrueType(result.Bytes).GlyphFor(0x4e2d), Is.Zero);
        });
    }

    [Test]
    public void OutputLimitDoesNotTouchAnExistingCallerStream()
    {
        var font = PdfFont.LoadTrueType(FontFixture.Create()); using var output = new MemoryStream(); output.Write("keep"u8);
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert("<p>中文</p>", output,
            new PdfOptions { Fonts = [font], SubsetFonts = true, MaxOutputBytes = 64 }));
        Assert.That(error!.Code, Is.EqualTo("FPDF1401")); Assert.That(Encoding.ASCII.GetString(output.ToArray()), Is.EqualTo("keep"));
    }
}
