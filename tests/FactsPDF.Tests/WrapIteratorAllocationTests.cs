using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

// Wrapping a paragraph reads its glyphs twice (validation, then line breaking). The glyph
// reader is a value inside the line iterator, so a paragraph costs no reader object per pass.
[TestFixture]
[NonParallelizable]
public sealed class WrapIteratorAllocationTests
{
    private static readonly PdfFont Font = PdfFont.FromBytes(FontFixture.Create(listDigits: true));

    private static string Paragraphs(int count)
    {
        var html = new StringBuilder("<body>");
        for (var i = 0; i < count; i++) html.Append("<p>AB Wi AB</p>");
        return html.Append("</body>").ToString();
    }

    private static long LayoutAllocated(string html, PdfOptions options)
    {
        var root = HtmlDocumentReader.ReadTree(html, options, default);
        for (var i = 0; i < 3; i++) TextLayout.Layout(root, options, default);
        var before = GC.GetAllocatedBytesForCurrentThread();
        TextLayout.Layout(root, options, default);
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    // Measured on main 001178a: 1,209 bytes of layout per one-line paragraph with two glyph
    // reader iterator objects; 985 with the reader as a value (eb531d2). 873 once the line
    // iterator no longer holds a List enumerator and placing a line creates no charge delegate.
    [Test]
    public void OneLineParagraphsAllocateNoGlyphReaderObjects()
    {
        var options = new PdfOptions { Fonts = [Font], SubsetFonts = true };
        var small = LayoutAllocated(Paragraphs(200), options);
        var large = LayoutAllocated(Paragraphs(400), options);
        var perParagraph = (large - small) / 200.0;
        TestContext.Out.WriteLine($"layout per paragraph: {perParagraph:F1} bytes");
        Assert.That(perParagraph, Is.LessThan(PerParagraphLimit));
    }

    private const double PerParagraphLimit = 950;
}
