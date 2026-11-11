using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace FactsPDF.Tests;

// Z2e: page dictionaries and content-stream headers are written from one reused builder instead
// of an interpolated body, a wrapped object string and two MediaBox number strings per page.
[TestFixture]
[NonParallelizable]
public sealed class PageObjectAllocationTests
{
    private static readonly PdfFont Font = PdfFont.FromBytes(FontFixture.Create(listDigits: true));

    // 600 forced pages, two objects each. On main every page built its dictionary as an interpolated
    // string, wrapped it in an object string and formatted the MediaBox numbers and the stream header
    // as strings: 1,010 (Courier) and 1,024 (subset font) serialize bytes per page, 276 and 339 after Z2e.
    // Z2f also builds the /Kids array in that builder: 300 and 339 on main 0cfc020, 234 and 281 after;
    // limit 400 → 325, which fails on main with the subset font.
    [TestCase(false)]
    [TestCase(true)]
    public void SerializingAllocatesNoStringPerPageObject(bool embedFont)
    {
        var options = embedFont ? new PdfOptions { Fonts = [Font], SubsetFonts = true } : new PdfOptions();
        var html = new StringBuilder();
        for (var i = 0; i < 600; i++) html.Append("<p style=\"break-after:page\">AB</p>");
        var pages = TextLayout.Layout(HtmlDocumentReader.ReadTree(html.ToString(), options, default), options, default);
        long Serialize()
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            using (embedFont ? UnicodePdfSerializer.Build(pages, options, default) : PdfSerializer.Build(pages, options, default)) { }
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }
        for (var i = 0; i < 10; i++) Serialize(); // warm up JIT tiers and the output buffer pool
        long allocated = 0;
        for (var i = 0; i < 5; i++) allocated += Serialize();
        var perPage = allocated / 5 / pages.Count;
        TestContext.Out.WriteLine($"serialize bytes per page ({(embedFont ? "subset font" : "Courier")}), {pages.Count} pages: {perPage}");
        Assert.That(pages, Has.Count.EqualTo(600));
        Assert.That(perPage, Is.LessThan(325));
    }

    // The page dictionary keeps its exact bytes with image resources and link annotations (the
    // expected strings are main's output before Z2e).
    [TestCase(false, "4 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 300] /Resources << /Font << /F1 3 0 R >> /XObject << /Im1 6 0 R /Im3 8 0 R >> >> /Contents 5 0 R /Annots [10 0 R 11 0 R] >>\nendobj\n")]
    [TestCase(true, "9 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 300] /Resources << /Font << /F1 3 0 R >> /XObject << /Im1 11 0 R /Im3 13 0 R >> >> /Contents 10 0 R /Annots [15 0 R 16 0 R] >>\nendobj\n")]
    public void PageDictionaryListsImagesAndLinks(bool embedFont, string expected)
    {
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
        var image = PdfImage.FromBytes(png);
        var html = "<p><a href=\"https://example.com/a\">AB</a> <a href=\"https://example.com/b\">AB</a></p><img src=\"a\" style=\"width:10pt;height:10pt\"><img src=\"b\" style=\"width:10pt;height:10pt\"><img src=\"a\" style=\"width:10pt;height:10pt\">";
        var options = new PdfOptions
        {
            PageWidth = 200, PageHeight = 300, Margin = 10,
            Images = new Dictionary<string, PdfImage> { ["a"] = image, ["b"] = PdfImage.FromBytes(png) },
            Fonts = embedFont ? [Font] : [], SubsetFonts = embedFont,
        };
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, options);
        var text = Encoding.Latin1.GetString(output.ToArray());
        var page = Regex.Match(text, @"\d+ 0 obj\n<< /Type /Page /Parent 2 0 R [^\n]*>>\nendobj\n");
        Assert.That(page.Success, Is.True, text);
        Assert.That(page.Value, Is.EqualTo(expected));
    }

    // Z2f: link annotation objects and the /Kids array are appended into the reused builder instead
    // of four rectangle number strings, a URI byte array, a hex string, a body and a wrapped object
    // string per link, and a string per page in a joined /Kids list. 600 forced pages with three
    // links each: 1,276 (Courier) and 1,593 (subset font) serialize bytes per link on main, 192 and
    // 779 after Z2f (the rest is the annotation list itself and glyph bounds). The limit fails on main.
    [TestCase(false)]
    [TestCase(true)]
    public void SerializingAllocatesNoStringPerLinkAnnotation(bool embedFont)
    {
        var options = embedFont ? new PdfOptions { Fonts = [Font], SubsetFonts = true } : new PdfOptions();
        var html = new StringBuilder();
        for (var i = 0; i < 600; i++)
            html.Append("<p style=\"break-after:page\"><a href=\"https://example.com/a\">AB</a> <a href=\"https://example.com/b\">AB</a> <a href=\"mailto:a@example.com\">AB</a></p>");
        var pages = TextLayout.Layout(HtmlDocumentReader.ReadTree(html.ToString(), options, default), options, default);
        long Serialize()
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            using (embedFont ? UnicodePdfSerializer.Build(pages, options, default) : PdfSerializer.Build(pages, options, default)) { }
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }
        for (var i = 0; i < 10; i++) Serialize(); // warm up JIT tiers and the output buffer pool
        long allocated = 0;
        for (var i = 0; i < 5; i++) allocated += Serialize();
        var perLink = allocated / 5 / (3 * pages.Count);
        TestContext.Out.WriteLine($"serialize bytes per link ({(embedFont ? "subset font" : "Courier")}), {pages.Count} pages: {perLink}");
        Assert.That(pages, Has.Count.EqualTo(600));
        Assert.That(perLink, Is.LessThan(900));
    }

    // The /Pages object and the link annotation objects keep their exact bytes (the expected strings
    // are main's output before Z2f), including fractional rectangle numbers and a mailto URI.
    [TestCase(false, "2 0 obj\n<< /Type /Pages /Count 3 /Kids [4 0 R 6 0 R 8 0 R] >>\nendobj\n")]
    [TestCase(true, "2 0 obj\n<< /Type /Pages /Count 3 /Kids [9 0 R 11 0 R 13 0 R] >>\nendobj\n")]
    public void PagesAndLinkObjectsKeepTheirBytes(bool embedFont, string expectedPages)
    {
        var html = "<p style=\"break-after:page\"><a href=\"https://example.com/a?x=1&amp;y=(2)\">AB</a></p>"
            + "<p style=\"break-after:page;font-size:7.3pt;margin-left:3.1415pt\"><a href=\"mailto:a.b+c@example.com\">AB</a></p><p>AB</p>";
        var options = new PdfOptions { PageWidth = 200, PageHeight = 300, Margin = 10, Fonts = embedFont ? [Font] : [], SubsetFonts = embedFont };
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, options);
        var text = Encoding.Latin1.GetString(output.ToArray());
        Assert.That(text, Does.Contain(expectedPages));
        var links = Regex.Matches(text, @"\d+ 0 obj\n<< /Type /Annot [^\n]*>>\nendobj\n").Select(m => m.Value).ToArray();
        Assert.That(string.Join("", links), Is.EqualTo(embedFont ? ExpectedEmbeddedLinks : ExpectedCourierLinks));
    }

    private const string ExpectedCourierLinks =
        "10 0 obj\n<< /Type /Annot /Subtype /Link /Rect [10 276.56 24.4 289.16] /Border [0 0 0] /A << /S /URI /URI <68747470733A2F2F6578616D706C652E636F6D2F613F783D3126793D283229> >> >>\nendobj\n"
        + "11 0 obj\n<< /Type /Annot /Subtype /Link /Rect [13.1415 281.824 21.9015 289.489] /Border [0 0 0] /A << /S /URI /URI <6D61696C746F3A612E622B63406578616D706C652E636F6D> >> >>\nendobj\n";
    private const string ExpectedEmbeddedLinks =
        "15 0 obj\n<< /Type /Annot /Subtype /Link /Rect [10 276.56 24.4 289.16] /Border [0 0 0] /A << /S /URI /URI <68747470733A2F2F6578616D706C652E636F6D2F613F783D3126793D283229> >> >>\nendobj\n"
        + "16 0 obj\n<< /Type /Annot /Subtype /Link /Rect [13.1415 281.824 21.9015 289.489] /Border [0 0 0] /A << /S /URI /URI <6D61696C746F3A612E622B63406578616D706C652E636F6D> >> >>\nendobj\n";
}
