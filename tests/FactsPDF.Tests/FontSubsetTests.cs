using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using FactsPDF.CommandLine;
using NUnit.Framework;

namespace FactsPDF.Tests;

[TestFixture]
public sealed class FontSubsetTests
{
    private static int U16(byte[] b, int p) => BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(p, 2));
    private static int U32(byte[] b, int p) => checked((int)BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(p, 4)));
    private static int GlyphCount(byte[] b) => U16(b, FontFixture.Table(b, "maxp") + 4);
    private static int GlyphOffset(byte[] b, int id)
    {
        var loca = FontFixture.Table(b, "loca");
        return FontFixture.Table(b, "glyf") + (U16(b, FontFixture.Table(b, "head") + 50) == 1 ? U32(b, loca + id * 4) : U16(b, loca + id * 2) * 2);
    }
    private static FontSubset Subset(byte[] bytes, params int[] scalars)
        => TrueTypeSubsetter.Create(PdfFont.LoadTrueType(bytes), scalars);
    private static byte[] Composite(params int[] children)
    {
        var data = FontFixture.Create(); var at = GlyphOffset(data, 7);
        Array.Clear(data, at, 34); FontFixture.U16(data, at, 65535);
        for (var i = 0; i < children.Length; i++)
        { FontFixture.U16(data, at + 10 + 8 * i, 3 | (i + 1 < children.Length ? 32 : 0)); FontFixture.U16(data, at + 12 + 8 * i, children[i]); }
        return data;
    }
    private static (string Pdf, byte[] Font) Render(byte[] font, bool subset, string text = "中文𠀀")
    {
        using var output = new MemoryStream();
        PdfConverter.Convert("<p>" + text + "</p>", output, new PdfOptions { Fonts = [PdfFont.LoadTrueType(font)], SubsetFonts = subset });
        var data = output.ToArray(); var pdf = Encoding.Latin1.GetString(data);
        var id = Regex.Match(pdf, @"/FontFile2 (\d+) 0 R").Groups[1].Value;
        var m = Regex.Match(pdf, @"\n" + id + @" 0 obj\n<< /Length (\d+) /Filter /FlateDecode /Length1 (\d+) >>\nstream\n");
        Assert.That(m.Success, Is.True);
        using var raw = new MemoryStream(data, m.Index + m.Length, int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture));
        using var z = new ZLibStream(raw, CompressionMode.Decompress); using var restored = new MemoryStream(); z.CopyTo(restored);
        return (pdf, restored.ToArray());
    }

    [TestCase(false)] [TestCase(true)]
    public void DenseSubsetContainsOnlyRequestedGlyphsAndNotdef(bool format12)
    {
        var original = FontFixture.Create(format12); var subset = Subset(original, 65);
        Assert.That(GlyphCount(subset.Bytes), Is.EqualTo(2)); Assert.That(subset.GlyphMap[2], Is.EqualTo(1));
        var loaded = PdfFont.LoadTrueType(subset.Bytes);
        Assert.That(loaded.GlyphFor(65), Is.EqualTo(1)); Assert.That(loaded.GlyphFor(0x4e2d), Is.Zero);
        Assert.That(subset.PostScriptName, Does.Match(@"^[A-Z]{6}\+FactsPDFTest$"));
        Assert.That(loaded.PostScriptName, Is.EqualTo(subset.PostScriptName));
        Assert.That(subset.Bytes, Has.Length.LessThan(original.Length));
    }
    [Test]
    public void PreservesActualAdvancesAndOutlineBytes()
    {
        var original = FontFixture.Create(); var subset = Subset(original, 87);
        Assert.That(PdfFont.LoadTrueType(subset.Bytes).Width1000(subset.GlyphMap[5]), Is.EqualTo(900));
        Assert.That(subset.Bytes.AsSpan(GlyphOffset(subset.Bytes, subset.GlyphMap[5]), 34).ToArray(),
            Is.EqualTo(original.AsSpan(GlyphOffset(original, 5), 34).ToArray()));
        Assert.That(GlyphCount(subset.Bytes), Is.EqualTo(2));
    }
    [Test]
    public void EmptyRequestStillContainsNotdef()
        => Assert.That(GlyphCount(Subset(FontFixture.Create()).Bytes), Is.EqualTo(1));
    [Test]
    public void EmptySpaceGlyphAndSharedAliasesArePreserved()
    {
        var subset = Subset(FontFixture.Create(), 32, 160, 0x6587, 0x3002, 0x20000);
        var f = PdfFont.LoadTrueType(subset.Bytes);
        Assert.That(GlyphCount(subset.Bytes), Is.EqualTo(4));
        Assert.That(f.GlyphFor(32), Is.EqualTo(f.GlyphFor(160)));
        Assert.That(f.GlyphFor(0x6587), Is.EqualTo(f.GlyphFor(0x3002)));
        Assert.That(f.GlyphFor(0x20000), Is.Not.Zero);
        Assert.That(GlyphOffset(subset.Bytes, f.GlyphFor(32)), Is.EqualTo(GlyphOffset(subset.Bytes, f.GlyphFor(32) + 1)));
    }
    [Test]
    public void NestedCompositeClosureRewritesAllComponentReferences()
    {
        var data = Composite(6, 5); var at = GlyphOffset(data, 6);
        Array.Clear(data, at, 34); FontFixture.U16(data, at, 65535); FontFixture.U16(data, at + 10, 3); FontFixture.U16(data, at + 12, 2);
        var s = Subset(data, 0x6587); Assert.That(GlyphCount(s.Bytes), Is.EqualTo(5));
        var parent = GlyphOffset(s.Bytes, s.GlyphMap[7]); var child = GlyphOffset(s.Bytes, s.GlyphMap[6]);
        Assert.That(U16(s.Bytes, parent + 12), Is.EqualTo(s.GlyphMap[6]));
        Assert.That(U16(s.Bytes, parent + 20), Is.EqualTo(s.GlyphMap[5]));
        Assert.That(U16(s.Bytes, child + 12), Is.EqualTo(s.GlyphMap[2]));
        Assert.That(s.GlyphMap[7], Is.EqualTo(4));
    }
    [Test]
    public void RepeatedComponentsAreKeptOnlyOnce()
        => Assert.That(GlyphCount(Subset(Composite(5, 5), 0x6587).Bytes), Is.EqualTo(3));
    [TestCase(7)] [TestCase(65535)]
    public void CyclicOrAbsentComponentFailsExplicitly(int child)
        => Assert.That(Assert.Throws<FactsPdfException>(() => Subset(Composite(child), 0x6587))!.Code, Is.EqualTo("FPDF1506"));
    [Test]
    public void TruncatedCompositeInstructionsFailExplicitly()
    {
        var data = Composite(5); var at = GlyphOffset(data, 7);
        FontFixture.U16(data, at + 10, 0x103); FontFixture.U16(data, at + 18, 500);
        Assert.That(Assert.Throws<FactsPdfException>(() => Subset(data, 0x6587))!.Code, Is.EqualTo("FPDF1506"));
    }
    [Test]
    public void ConflictingCompositeScalesFailExplicitly()
    {
        var data = Composite(5); FontFixture.U16(data, GlyphOffset(data, 7) + 10, 3 | 8 | 64);
        Assert.That(Assert.Throws<FactsPdfException>(() => Subset(data, 0x6587))!.Code, Is.EqualTo("FPDF1506"));
    }
    [Test]
    public void SubsetRespectsCancellation()
        => Assert.Throws<OperationCanceledException>(() => TrueTypeSubsetter.Create(PdfFont.LoadTrueType(FontFixture.Create()), [65], new CancellationToken(true)));
    [Test]
    public void MissingRequestedGlyphIsAnError()
        => Assert.That(Assert.Throws<FactsPdfException>(() => Subset(FontFixture.Create(), 90))!.Code, Is.EqualTo("FPDF1504"));
    [Test]
    public void NoSubsettingPermissionCannotBeBypassed()
    {
        var original = FontFixture.Create(fsType: 256);
        Assert.That(Assert.Throws<FactsPdfException>(() => Subset(original, 65))!.Code, Is.EqualTo("FPDF1505"));
        var result = Render(original, true);
        Assert.That(result.Font, Is.EqualTo(original)); Assert.That(result.Pdf, Does.Not.Match(@"/BaseFont /[A-Z]{6}\+"));
    }
    [Test]
    public void DefaultAndExplicitFullEmbeddingAreUnchanged()
    {
        Assert.That(new PdfOptions().SubsetFonts, Is.False); var original = FontFixture.Create();
        Assert.That(Render(original, false).Font, Is.EqualTo(original));
    }
    [Test]
    public void PdfMapsItsCidsToNewGlyphIdsAndRetainsUnicodeAliases()
    {
        var result = Render(FontFixture.Create(), true, "文。𠀀"); var subset = PdfFont.LoadTrueType(result.Font);
        Assert.That(GlyphCount(result.Font), Is.EqualTo(3));
        Assert.That(result.Pdf, Does.Contain("<0001> <6587>")); Assert.That(result.Pdf, Does.Contain("<0002> <3002>"));
        Assert.That(result.Pdf, Does.Contain("<0003> <D840DC00>"));
        var id = Regex.Match(result.Pdf, @"/CIDToGIDMap (\d+) 0 R").Groups[1].Value;
        var stream = Regex.Match(result.Pdf, @"\n" + id + @" 0 obj\n<< /Length \d+ >>\nstream\n");
        var bytes = Encoding.Latin1.GetBytes(result.Pdf);
        Assert.That(U16(bytes, stream.Index + stream.Length + 2), Is.EqualTo(subset.GlyphFor(0x6587)));
        Assert.That(U16(bytes, stream.Index + stream.Length + 4), Is.EqualTo(subset.GlyphFor(0x3002)));
        Assert.That(U16(bytes, stream.Index + stream.Length + 6), Is.EqualTo(subset.GlyphFor(0x20000)));
    }
    [Test]
    public void SubsetIsDeterministicAndLeavesLoadedFontUntouched()
    {
        var original = FontFixture.Create(); var f = PdfFont.LoadTrueType(original);
        var a = TrueTypeSubsetter.Create(f, [65, 0x4e2d]); var b = TrueTypeSubsetter.Create(f, [0x4e2d, 65, 65]);
        Assert.That(a.Bytes, Is.EqualTo(b.Bytes)); Assert.That(f.Data.ToArray(), Is.EqualTo(original));
        Assert.That(a.PostScriptName, Is.Not.EqualTo(TrueTypeSubsetter.Create(f, [87]).PostScriptName));
    }
    [Test]
    public void WholeFontAndTableChecksumsAreValid()
    {
        var bytes = Subset(FontFixture.Create(), 0x4e2d).Bytes;
        Assert.That(Sum(bytes), Is.EqualTo(0xb1b0afbau));
        for (var i = 0; i < U16(bytes, 4); i++)
        {
            var at = 12 + i * 16; var table = bytes.AsSpan(U32(bytes, at + 8), U32(bytes, at + 12)).ToArray();
            if (Encoding.ASCII.GetString(bytes, at, 4) == "head") Array.Clear(table, 8, 4);
            Assert.That(Sum(table), Is.EqualTo(BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(at + 4))), "Table checksum");
        }
    }
    private static uint Sum(byte[] bytes)
    {
        uint sum = 0;
        for (var i = 0; i < bytes.Length; i += 4)
        { uint word = 0; for (var j = 0; j < 4; j++) word = (word << 8) | (i + j < bytes.Length ? bytes[i + j] : 0u); sum = unchecked(sum + word); }
        return sum;
    }
    [Test]
    public void CliAcceptsSubsetFlagWithoutRequiringFontsForAscii()
    {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes("<p>A</p>")); using var output = new MemoryStream(); using var error = new StringWriter();
        Assert.That(CliApplication.Run(["-", "-", "--subset-fonts"], input, output, error), Is.Zero, error.ToString());
        Assert.That(Encoding.ASCII.GetString(output.ToArray()), Does.StartWith("%PDF-1.7"));
    }
}
