using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace FactsPDF.Tests;

[TestFixture]
public sealed class FontBoundaryTests
{
    [TestCase(1, 78)] [TestCase(4, 68)]
    public void RejectsOs2TruncationAccordingToDeclaredVersion(int version, int length)
    {
        var bytes = FontFixture.Create(); FontFixture.U16(bytes, FontFixture.Table(bytes, "OS/2"), version);
        // OS/2 is the first directory record in this independently generated fixture.
        FontFixture.U32(bytes, 24, (uint)length);
        Assert.That(Assert.Throws<FactsPdfException>(() => PdfFont.LoadTrueType(bytes))!.Code, Is.EqualTo("FPDF1501"));
    }
    [Test]
    public void RejectsUnsupportedHeadVersion()
    {
        var bytes = FontFixture.Create(); FontFixture.U32(bytes, FontFixture.Table(bytes, "head"), 0);
        Assert.That(Assert.Throws<FactsPdfException>(() => PdfFont.LoadTrueType(bytes))!.Code, Is.EqualTo("FPDF1501"));
    }
    [Test]
    public void BinaryPdfHasABinaryHeaderComment()
    {
        using var output = new MemoryStream();
        PdfConverter.Convert("<p>中文</p>", output, new PdfOptions { Fonts = [PdfFont.LoadTrueType(FontFixture.Create())] });
        Assert.That(output.ToArray().AsSpan(0, 32).ToArray().Count(b => b >= 128), Is.GreaterThanOrEqualTo(4));
    }
    [Test]
    public void FullEmbeddedFontDecompressesToTheSuppliedBytes()
    {
        var bytes = FontFixture.Create(); using var output = new MemoryStream();
        PdfConverter.Convert("<p>中文</p>", output, new PdfOptions { Fonts = [PdfFont.LoadTrueType(bytes)] });
        var pdf = output.ToArray(); var text = Encoding.Latin1.GetString(pdf);
        var reference = Regex.Match(text, @"/FontFile2 (\d+) 0 R"); Assert.That(reference.Success, Is.True);
        var obj = Regex.Match(text, @"\n" + reference.Groups[1].Value + @" 0 obj\n<< /Length (\d+) /Filter /FlateDecode /Length1 (\d+) >>\nstream\n");
        Assert.That(obj.Success, Is.True); Assert.That(int.Parse(obj.Groups[2].Value, CultureInfo.InvariantCulture), Is.EqualTo(bytes.Length));
        using var compressed = new MemoryStream(pdf, obj.Index + obj.Length, int.Parse(obj.Groups[1].Value, CultureInfo.InvariantCulture));
        using var decoder = new ZLibStream(compressed, CompressionMode.Decompress); using var restored = new MemoryStream(); decoder.CopyTo(restored);
        Assert.That(restored.ToArray(), Is.EqualTo(bytes));
    }
    [Test]
    public void Cmap4ZeroGlyphDoesNotAcquireAnIdDelta()
    {
        // This fixture deliberately encodes A as a zero raw glyph with nonzero delta.
        var font = PdfFont.LoadTrueType(FontFixture.Create(format12: false, ranged4: true));
        Assert.That(font.GlyphFor('A'), Is.Zero); Assert.That(font.GlyphFor('W'), Is.EqualTo(5));
    }
    [Test]
    public void ShortLocaOffsetsAreReadInTwoByteUnits()
    {
        var bytes = FontFixture.Create(); var at = FontFixture.Table(bytes, "loca"); var offsets = new uint[13];
        for (var i = 0; i < 13; i++) offsets[i] = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(at + i * 4));
        for (var i = 0; i < 13; i++) FontFixture.U16(bytes, at + i * 2, (int)(offsets[i] / 2));
        FontFixture.U16(bytes, FontFixture.Table(bytes, "head") + 50, 0);
        Assert.That(PdfFont.LoadTrueType(bytes).GlyphFor(0x4e2d), Is.EqualTo(6));
    }
    [Test]
    public void NonSeekableShortReadsHonorByteLimitAndLeaveStreamOpen()
    {
        using var input = new ShortReads(FontFixture.Create());
        Assert.That(Assert.Throws<FactsPdfException>(() => PdfFont.LoadTrueType(input, 16))!.Code, Is.EqualTo("FPDF1503"));
        Assert.That(input.BytesRead, Is.EqualTo(17)); Assert.That(input.CanRead, Is.True);
    }
    [Test]
    public void RejectsNullAndOversizedFontChainsBeforeWriting()
    {
        using var output = new MemoryStream(); var font = PdfFont.LoadTrueType(FontFixture.Create());
        Assert.Throws<ArgumentException>(() => PdfConverter.Convert("A", output, new PdfOptions { Fonts = null! }));
        Assert.Throws<ArgumentException>(() => PdfConverter.Convert("A", output, new PdfOptions { Fonts = [null!] }));
        Assert.Throws<ArgumentException>(() => PdfConverter.Convert("A", output, new PdfOptions { Fonts = Enumerable.Repeat(font, 9).ToArray() }));
        Assert.That(output.Length, Is.Zero);
    }
    [Test]
    public void EmptyDocumentDoesNotEmbedUnusedFonts()
    {
        using var output = new MemoryStream();
        var result = PdfConverter.Convert("", output, new PdfOptions { Fonts = [PdfFont.LoadTrueType(FontFixture.Create())] });
        Assert.That(result.PageCount, Is.EqualTo(1)); Assert.That(Encoding.Latin1.GetString(output.ToArray()), Does.Not.Contain("/FontFile2"));
    }
    private sealed class ShortReads(byte[] bytes) : MemoryStream(bytes)
    {
        public int BytesRead { get; private set; }
        public override bool CanSeek => false;
        public override int Read(byte[] buffer, int offset, int count)
        { var read = base.Read(buffer, offset, Math.Min(count, 3)); BytesRead += read; return read; }
    }
}
