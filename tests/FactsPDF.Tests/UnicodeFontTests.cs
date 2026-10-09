using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using FactsPDF.CommandLine;
using NUnit.Framework;

namespace FactsPDF.Tests;

[TestFixture]
public sealed class UnicodeFontTests
{
    private static PdfFont Font(bool format12 = true, bool latinOnly = false, ushort fsType = 0, bool ranged4 = false)
        => PdfFont.LoadTrueType(FontFixture.Create(format12, latinOnly, fsType, ranged4));
    private static (PdfConversionResult Result, byte[] Bytes, string Pdf) Render(string html, PdfOptions? options = null)
    {
        using var stream = new MemoryStream();
        var result = PdfConverter.Convert(html, stream, options ?? new PdfOptions { Fonts = [Font()] });
        return (result, stream.ToArray(), Encoding.Latin1.GetString(stream.ToArray()));
    }
    private static string Text(string pdf)
    {
        // Single-font inspection helper; independent multi-font extraction is exercised in CI.
        var map = new Dictionary<string, string>();
        foreach (Match block in Regex.Matches(pdf, @"\d+ beginbfchar\s+(.*?)\s+endbfchar", RegexOptions.Singleline))
            foreach (Match m in Regex.Matches(block.Groups[1].Value, @"<([0-9A-F]+)>\s+<([0-9A-F]+)>"))
                map[m.Groups[1].Value] = Encoding.BigEndianUnicode.GetString(System.Convert.FromHexString(m.Groups[2].Value));
        var text = new StringBuilder();
        foreach (Match run in Regex.Matches(pdf, @"<([0-9A-F]+)> Tj"))
        { var s = run.Groups[1].Value; for (var i = 0; i < s.Length; i += 4) text.Append(map[s.Substring(i, 4)]); }
        return text.ToString();
    }
    private static FactsPdfException Failure(string html, PdfOptions options)
    {
        using var stream = new MemoryStream();
        var ex = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, stream, options));
        Assert.That(stream.Length, Is.Zero); return ex!;
    }
    [TestCase(false, false)] [TestCase(false, true)] [TestCase(true, false)]
    public void ReadsCmapsAndActualAdvances(bool format12, bool ranged4)
    {
        var f = Font(format12, ranged4: ranged4);
        Assert.That(f.PostScriptName, Is.EqualTo("FactsPDFTest"));
        Assert.That(f.GlyphFor(0x4e2d), Is.EqualTo(6));
        Assert.That(f.Width1000(f.GlyphFor('i')), Is.EqualTo(200));
        Assert.That(f.Width1000(f.GlyphFor('W')), Is.EqualTo(900));
        Assert.That(f.Width1000(11), Is.EqualTo(1000), "The last hMetric advance is reused.");
        Assert.That(f.GlyphFor(0x20000), Is.EqualTo(format12 ? 11 : 0));
    }
    [Test]
    public void SnapshotsTheCallerFontBytes()
    {
        var bytes = FontFixture.Create(); var f = PdfFont.LoadTrueType(bytes); Array.Clear(bytes);
        Assert.That(f.GlyphFor(0x4e2d), Is.EqualTo(6));
        Assert.That(Render("<p>中文</p>", new PdfOptions { Fonts = [f] }).Pdf, Does.Contain("/FontFile2"));
    }
    [Test]
    public void StreamLoadingLeavesCallerStreamOpenAndHonorsCancellation()
    {
        using var stream = new MemoryStream(FontFixture.Create()); var f = PdfFont.LoadTrueType(stream);
        Assert.That(stream.CanRead, Is.True); Assert.That(f.GlyphFor(65), Is.EqualTo(2)); stream.Position = 0;
        Assert.Throws<OperationCanceledException>(() => PdfFont.LoadTrueType(stream, cancellationToken: new CancellationToken(true)));
        Assert.That(stream.Position, Is.Zero);
    }
    [Test]
    public void FontByteLimitsApplyToBytesAndStreams()
    {
        var data = FontFixture.Create();
        Assert.That(Assert.Throws<FactsPdfException>(() => PdfFont.LoadTrueType(data, 16))!.Code, Is.EqualTo("FPDF1503"));
        using var stream = new MemoryStream(data);
        Assert.That(Assert.Throws<FactsPdfException>(() => PdfFont.LoadTrueType(stream, 16))!.Code, Is.EqualTo("FPDF1503"));
    }
    // Preview-only (4) needs read-only document handling, which this increment deliberately does not promise.
    [TestCase(2)] [TestCase(4)] [TestCase(512)]
    public void ProhibitsUnsupportedEmbeddingPermissions(int fsType)
        => Assert.That(Assert.Throws<FactsPdfException>(() => Font(fsType: (ushort)fsType))!.Code, Is.EqualTo("FPDF1505"));
    [TestCase(0)] [TestCase(8)] [TestCase(256)]
    public void AcceptsFullEmbeddingPermissions(int fsType)
        => Assert.That(Font(fsType: (ushort)fsType).GlyphFor(65), Is.EqualTo(2));
    [TestCase("OTTO")] [TestCase("ttcf")] [TestCase("wOFF")] [TestCase("wOF2")]
    public void RejectsUnsupportedFontContainers(string header)
    {
        var data = FontFixture.Create(); Encoding.ASCII.GetBytes(header).CopyTo(data, 0);
        Assert.That(Assert.Throws<FactsPdfException>(() => PdfFont.LoadTrueType(data))!.Code, Is.EqualTo("FPDF1502"));
    }
    [Test]
    public void RejectsTruncatedFont()
        => Assert.That(Assert.Throws<FactsPdfException>(() => PdfFont.LoadTrueType(new byte[5]))!.Code, Is.EqualTo("FPDF1501"));
    [TestCase("head", 18, 0)] [TestCase("hhea", 34, 0)] [TestCase("maxp", 4, 0)]
    public void RejectsInvalidMetrics(string table, int offset, int value)
    {
        var data = FontFixture.Create(); FontFixture.U16(data, FontFixture.Table(data, table) + offset, value);
        Assert.That(Assert.Throws<FactsPdfException>(() => PdfFont.LoadTrueType(data))!.Code, Is.EqualTo("FPDF1501"));
    }
    [Test]
    public void RejectsOutOfRangeTablePointerWithoutOverflow()
    {
        var data = FontFixture.Create(); FontFixture.U32(data, 20, uint.MaxValue);
        Assert.That(Assert.Throws<FactsPdfException>(() => PdfFont.LoadTrueType(data))!.Code, Is.EqualTo("FPDF1501"));
    }
    [Test]
    public void RejectsUnorderedOrOutOfRangeCmap12Groups()
    {
        var data = FontFixture.Create(); var map = FontFixture.Table(data, "cmap") + 12; FontFixture.U32(data, map + 16, 0x110000);
        Assert.That(Assert.Throws<FactsPdfException>(() => PdfFont.LoadTrueType(data))!.Code, Is.EqualTo("FPDF1501"));
    }
    [Test]
    public void RejectsBadLocaRange()
    {
        var data = FontFixture.Create(); FontFixture.U32(data, FontFixture.Table(data, "loca") + 48, uint.MaxValue);
        Assert.That(Assert.Throws<FactsPdfException>(() => PdfFont.LoadTrueType(data))!.Code, Is.EqualTo("FPDF1501"));
    }
    [Test]
    public void ChineseAndSupplementaryScalarsRoundTripThroughToUnicode()
    {
        const string text = "A中文𠀀B"; var result = Render("<p>" + text + "</p>");
        Assert.That(Text(result.Pdf), Is.EqualTo(text)); Assert.That(result.Pdf, Does.Contain("D840DC00"));
        Assert.That(result.Pdf, Does.Contain("/Subtype /Type0")); Assert.That(result.Pdf, Does.Contain("/Subtype /CIDFontType2"));
        Assert.That(result.Pdf, Does.Contain("/Encoding /Identity-H")); Assert.That(result.Pdf, Does.Contain("/CIDToGIDMap"));
        Assert.That(result.Pdf, Does.Contain("/FontFile2"));
    }
    [Test]
    public void SharedGlyphAliasesRemainDifferentUnicodeCharacters()
        => Assert.That(Text(Render("<p>文。</p>").Pdf), Is.EqualTo("文。"));
    [Test]
    public void NumericCharacterReferencesPreserveSupplementaryScalars()
        => Assert.That(Text(Render("<p>&#x4e2d;&#25991;&#x20000;</p>").Pdf), Is.EqualTo("中文𠀀"));
    [Test]
    public void UsesActualWidthsRatherThanCourierApproximation()
    {
        var pdf = Render("<p>iiii<span style='color:red'>W</span></p>").Pdf;
        Assert.That(pdf, Does.Contain("1 0 0 1 45.6 "), "Four 200-unit glyphs at 12pt advance 9.6pt from x=36.");
    }
    [Test]
    public void ChineseWrapsWithoutSpacesAndPaginates()
    {
        var text = string.Concat(Enumerable.Repeat("中文", 60));
        var result = Render("<p>" + text + "</p>", new PdfOptions { Fonts = [Font()], PageWidth = 120, PageHeight = 100, Margin = 12 });
        Assert.That(result.Result.PageCount, Is.GreaterThan(1)); Assert.That(Text(result.Pdf), Is.EqualTo(text));
    }
    [Test]
    public void CommonChinesePunctuationStaysWithItsNeighbor()
    {
        var pdf = Render("<p>中文中（文）中文，中。</p>", new PdfOptions { Fonts = [Font()], PageWidth = 72, Margin = 12 }).Pdf;
        Assert.That(Text(pdf), Is.EqualTo("中文中（文）中文，中。"));
        var map = new Dictionary<string, string>();
        foreach (Match block in Regex.Matches(pdf, @"\d+ beginbfchar\s+(.*?)\s+endbfchar", RegexOptions.Singleline))
            foreach (Match m in Regex.Matches(block.Groups[1].Value, @"<([0-9A-F]+)>\s+<([0-9A-F]+)>")) map[m.Groups[1].Value] = Encoding.BigEndianUnicode.GetString(System.Convert.FromHexString(m.Groups[2].Value));
        foreach (Match run in Regex.Matches(pdf, @"<([0-9A-F]+)> Tj"))
        {
            var s = run.Groups[1].Value; Assert.That(map[s[..4]], Is.Not.AnyOf("）", "，", "。")); Assert.That(map[s[^4..]], Is.Not.EqualTo("（"));
        }
    }
    [Test]
    public void NonBreakingSpacePreservesTextAndCannotBreakTheWord()
    {
        Assert.That(Text(Render("<p>A&nbsp;B</p>").Pdf), Is.EqualTo("A\u00a0B"));
        var options = new PdfOptions { Fonts = [Font()], FontSize = 36, PageWidth = 72, Margin = 12 };
        Assert.That(Failure("<p>A&nbsp;B</p>", options).Code, Is.EqualTo("FPDF1302"));
    }
    [Test]
    public void OrderedFallbackEmbedsOnlyUsedFontsOnce()
    {
        var f = Font(latinOnly: true); var cjk = Font(); var pdf = Render("<p>A中文B</p><p>中文</p>", new PdfOptions { Fonts = [f, cjk, cjk] }).Pdf;
        Assert.That(Regex.Matches(pdf, @"/Subtype /Type0\b").Count, Is.EqualTo(2)); Assert.That(pdf, Does.Contain("/F2 12 Tf"));
    }
    [Test]
    public void MissingGlyphFailsWithoutOutput()
        => Assert.That(Failure("<p>中文</p>", new PdfOptions { Fonts = [Font(latinOnly: true)] }).Code, Is.EqualTo("FPDF1504"));
    // Construct invalid code units at runtime: custom-attribute UTF-8 serialization cannot preserve lone surrogates.
    [TestCase(0xd800)] [TestCase(0xdc00)]
    public void InvalidUtf16FailsWithoutReplacement(int codeUnit)
    {
        var text = new string((char)codeUnit, 1); Assert.That(char.IsSurrogate(text[0]), Is.True);
        Assert.That(Failure("<p>" + text + "</p>", new PdfOptions { Fonts = [Font()] }).Code, Is.EqualTo("FPDF1304"));
    }
    [TestCase("A\u0301")] [TestCase("中\ufe00")] [TestCase("العربية")] [TestCase("\u200d")]
    public void ShapingAndBidiRequirementsFailExplicitly(string text)
        => Assert.That(Failure("<p>" + text + "</p>", new PdfOptions { Fonts = [Font()] }).Code, Is.EqualTo("FPDF1305"));
    [Test]
    public void EnforcesTotalFontAndOutputLimits()
    {
        var font = Font();
        Assert.That(Failure("<p>A</p>", new PdfOptions { Fonts = [font], MaxTotalFontBytes = 16 }).Code, Is.EqualTo("FPDF1503"));
        Assert.That(Failure("<p>A</p>", new PdfOptions { Fonts = [font], MaxOutputBytes = 128 }).Code, Is.EqualTo("FPDF1401"));
    }
    [Test]
    public void EmbeddedFontOutputIsCultureIndependentAndConcurrent()
    {
        var options = new PdfOptions { Fonts = [Font()] }; var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture; var first = Render("<p>A中文</p>", options).Bytes;
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR"); Assert.That(Render("<p>A中文</p>", options).Bytes, Is.EqualTo(first));
            Parallel.For(0, 8, _ => Assert.That(Render("<p>A中文</p>", options).Bytes, Is.EqualTo(first)));
        }
        finally { CultureInfo.CurrentCulture = original; }
    }
    [Test]
    public void BinaryFontStreamsKeepCorrectXrefOffsets()
    {
        var result = Render("<p>中文</p>");
        var xref = Regex.Match(result.Pdf, @"xref\n0 (\d+)\n0000000000 65535 f \n((?:\d{10} 00000 n \n)+)"); Assert.That(xref.Success, Is.True);
        var offsets = Regex.Matches(xref.Groups[2].Value, @"(\d{10}) 00000 n");
        for (var i = 0; i < offsets.Count; i++) Assert.That(result.Pdf[int.Parse(offsets[i].Groups[1].Value, CultureInfo.InvariantCulture)..], Does.StartWith($"{i + 1} 0 obj\n"));
    }
    [Test]
    public void CliLoadsAnExplicitFontAndProtectsItsSource()
    {
        var dir = Path.Combine(Path.GetTempPath(), "factspdf-font-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "test.ttf"); var data = FontFixture.Create(); File.WriteAllBytes(path, data);
            using var input = new MemoryStream(Encoding.UTF8.GetBytes("<p>中文</p>")); using var output = new MemoryStream(); using var error = new StringWriter();
            Assert.That(CliApplication.Run(["-", "-", "--font", path], input, output, error), Is.Zero, error.ToString());
            Assert.That(Text(Encoding.Latin1.GetString(output.ToArray())), Is.EqualTo("中文")); input.Position = 0;
            Assert.That(CliApplication.Run(["-", path, "--font", path, "--overwrite"], input, output, error), Is.EqualTo(2));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(data));
        }
        finally { Directory.Delete(dir, true); }
    }
    [Test]
    public void CliMissingFontArgumentIsAUsageError()
    {
        using var input = new MemoryStream(); using var output = new MemoryStream(); using var error = new StringWriter();
        Assert.That(CliApplication.Run(["-", "-", "--font"], input, output, error), Is.EqualTo(2)); Assert.That(output.Length, Is.Zero);
    }
}
