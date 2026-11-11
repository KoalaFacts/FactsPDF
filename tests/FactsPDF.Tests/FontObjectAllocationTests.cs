using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace FactsPDF.Tests;

// Z2g: the embedded font's objects (Type0 font, CID font with its /W widths, font descriptor and
// the ToUnicode CMap) are appended into the reused builder instead of a width string per character,
// a joined width list, interpolated object strings and, per character in the CMap, a UTF-16 string,
// its byte array, a hex string and a code string, then one CMap string and its byte array.
[TestFixture]
[NonParallelizable]
public sealed class FontObjectAllocationTests
{
    private const int Distinct = 2000;
    private static readonly PdfFont Font = PdfFont.FromBytes(FontFixture.Create(cjkScalars: Distinct));

    // 2,000 distinct characters in one embedded font: 378 (full font) and 514 (subset font) serialize
    // bytes per distinct character on main be6fac8, 220 and 346 after Z2g (the rest is the font's
    // code map and character list, the growing builders and, for the subset, the subsetter). The
    // limits (about 15% above) fail on main in both cases.
    [TestCase(false, 255)]
    [TestCase(true, 400)]
    public void SerializingAllocatesNoStringPerDistinctCharacter(bool subset, int limit)
    {
        var options = new PdfOptions { Fonts = [Font], SubsetFonts = subset };
        var html = new StringBuilder("<p>");
        for (var i = 0; i < Distinct; i++) html.Append((char)(0x4e00 + i));
        html.Append("</p>");
        var pages = TextLayout.Layout(HtmlDocumentReader.ReadTree(html.ToString(), options, default), options, default);
        long Serialize()
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            using (UnicodePdfSerializer.Build(pages, options, default)) { }
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }
        for (var i = 0; i < 10; i++) Serialize(); // warm up JIT tiers and the output buffer pool
        long allocated = 0;
        for (var i = 0; i < 5; i++) allocated += Serialize();
        var perCharacter = allocated / 5 / Distinct;
        TestContext.Out.WriteLine($"serialize bytes per distinct character ({(subset ? "subset" : "full")} font), {pages.Count} pages: {perCharacter}");
        Assert.That(perCharacter, Is.LessThan(limit));
    }

    // The font objects keep their exact bytes (the expected strings are main's output before Z2g),
    // including a supplementary-plane character whose ToUnicode entry is a surrogate pair.
    [TestCase(false)]
    [TestCase(true)]
    public void FontObjectsKeepTheirBytes(bool subset)
    {
        var options = new PdfOptions { PageWidth = 200, PageHeight = 300, Margin = 10, Fonts = [PdfFont.FromBytes(FontFixture.Create())], SubsetFonts = subset };
        using var output = new MemoryStream();
        PdfConverter.Convert("<p>AB Wi 中文。\U00020000</p>", output, options);
        var text = Encoding.Latin1.GetString(output.ToArray());
        var objects = Regex.Matches(text, @"\b[345] 0 obj\n<<[^\n]*>>\nendobj\n").Select(m => m.Value);
        var cmap = Regex.Match(text, @"7 0 obj\n<< /Length \d+ >>\nstream\n.*?endstream\nendobj\n", RegexOptions.Singleline).Value;
        Assert.That(string.Join("", objects) + cmap, Is.EqualTo(subset ? ExpectedSubset : ExpectedFull));
    }

    private const string ExpectedFull =
        "3 0 obj\n"
        + "<< /Type /Font /Subtype /Type0 /BaseFont /FactsPDFTest /Encoding /Identity-H /DescendantFonts [4 0 R] /ToUnicode 7 0 R >>\n"
        + "endobj\n"
        + "4 0 obj\n"
        + "<< /Type /Font /Subtype /CIDFontType2 /BaseFont /FactsPDFTest /CIDSystemInfo << /Registry (Adobe) /Ordering (Identity) /Supplement 0 >> /FontDescriptor 5 0 R /CIDToGIDMap 8 0 R /DW 1000 /W [1 [600 600 300 900 200 1000 1000 1000 1000]] >>\n"
        + "endobj\n"
        + "5 0 obj\n"
        + "<< /Type /FontDescriptor /FontName /FactsPDFTest /Flags 4 /FontBBox [0 0 900 700] /ItalicAngle 0 /Ascent 800 /Descent -200 /CapHeight 700 /StemV 80 /FontFile2 6 0 R >>\n"
        + "endobj\n"
        + "7 0 obj\n"
        + "<< /Length 448 >>\n"
        + "stream\n"
        + "/CIDInit /ProcSet findresource begin\n"
        + "12 dict begin\n"
        + "begincmap\n"
        + "/CIDSystemInfo << /Registry (Adobe) /Ordering (UCS) /Supplement 0 >> def\n"
        + "/CMapName /FactsPDF-UCS def\n"
        + "/CMapType 2 def\n"
        + "1 begincodespacerange\n"
        + "<0000> <FFFF>\n"
        + "endcodespacerange\n"
        + "9 beginbfchar\n"
        + "<0001> <0041>\n"
        + "<0002> <0042>\n"
        + "<0003> <0020>\n"
        + "<0004> <0057>\n"
        + "<0005> <0069>\n"
        + "<0006> <4E2D>\n"
        + "<0007> <6587>\n"
        + "<0008> <3002>\n"
        + "<0009> <D840DC00>\n"
        + "endbfchar\n"
        + "endcmap\n"
        + "CMapName currentdict /CMap defineresource pop\n"
        + "end\n"
        + "end\n"
        + "\n"
        + "endstream\n"
        + "endobj\n";
    private const string ExpectedSubset =
        "3 0 obj\n"
        + "<< /Type /Font /Subtype /Type0 /BaseFont /JIITZO+FactsPDFTest /Encoding /Identity-H /DescendantFonts [4 0 R] /ToUnicode 7 0 R >>\n"
        + "endobj\n"
        + "4 0 obj\n"
        + "<< /Type /Font /Subtype /CIDFontType2 /BaseFont /JIITZO+FactsPDFTest /CIDSystemInfo << /Registry (Adobe) /Ordering (Identity) /Supplement 0 >> /FontDescriptor 5 0 R /CIDToGIDMap 8 0 R /DW 1000 /W [1 [600 600 300 900 200 1000 1000 1000 1000]] >>\n"
        + "endobj\n"
        + "5 0 obj\n"
        + "<< /Type /FontDescriptor /FontName /JIITZO+FactsPDFTest /Flags 4 /FontBBox [0 0 900 700] /ItalicAngle 0 /Ascent 800 /Descent -200 /CapHeight 700 /StemV 80 /FontFile2 6 0 R >>\n"
        + "endobj\n"
        + "7 0 obj\n"
        + "<< /Length 448 >>\n"
        + "stream\n"
        + "/CIDInit /ProcSet findresource begin\n"
        + "12 dict begin\n"
        + "begincmap\n"
        + "/CIDSystemInfo << /Registry (Adobe) /Ordering (UCS) /Supplement 0 >> def\n"
        + "/CMapName /FactsPDF-UCS def\n"
        + "/CMapType 2 def\n"
        + "1 begincodespacerange\n"
        + "<0000> <FFFF>\n"
        + "endcodespacerange\n"
        + "9 beginbfchar\n"
        + "<0001> <0041>\n"
        + "<0002> <0042>\n"
        + "<0003> <0020>\n"
        + "<0004> <0057>\n"
        + "<0005> <0069>\n"
        + "<0006> <4E2D>\n"
        + "<0007> <6587>\n"
        + "<0008> <3002>\n"
        + "<0009> <D840DC00>\n"
        + "endbfchar\n"
        + "endcmap\n"
        + "CMapName currentdict /CMap defineresource pop\n"
        + "end\n"
        + "end\n"
        + "\n"
        + "endstream\n"
        + "endobj\n";
}
