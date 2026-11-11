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
    // The limit fails on main.
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
        Assert.That(perPage, Is.LessThan(400));
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
}
