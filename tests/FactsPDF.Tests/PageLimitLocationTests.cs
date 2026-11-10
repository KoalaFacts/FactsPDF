using NUnit.Framework;

namespace FactsPDF.Tests;

// FPDF1303 points at the element whose content needed the page past MaxPages, so
// SourceOffset and the CLI's "Location: line L, column C." show where the document
// outgrew its page budget instead of leaving -1.
public sealed class PageLimitLocationTests
{
    private const string First = "<p>First page.</p>\n";
    // A first line almost as tall as the usable page, so whatever follows needs page two.
    private const string Filler = "<p style='line-height:760pt'>Filler.</p>\n";
    private static readonly PdfImage Image = PdfImage.FromBytes(JpegResourceTests.Bytes("rgb-420"));

    private static FactsPdfException Fails(string html, int maxPages = 1)
    {
        var writer = Pdf.Write().WithImage("a", Image).WithLimits(l => l.MaxPages(maxPages));
        var error = Assert.Throws<FactsPdfException>(() => writer.ToBytes(html))!;
        Assert.That(error.Code, Is.EqualTo("FPDF1303"));
        Assert.That(error.Message, Is.EqualTo("FPDF1303: Page limit exceeded."));
        return error;
    }

    private static int OffsetOf(string html, string marker) => html.IndexOf(marker, StringComparison.Ordinal);

    [Test]
    public void ForcedBreakBeforeAParagraphPointsAtIt()
    {
        const string html = First + "<p id='next' style='break-before:page'>Second page.</p>";
        Assert.That(Fails(html).SourceOffset, Is.EqualTo(OffsetOf(html, "<p id='next'")));
    }

    [Test]
    public void ParagraphThatNoLongerFitsPointsAtIt()
    {
        const string html = Filler + "<p id='next'>Overflow.</p>";
        Assert.That(Fails(html).SourceOffset, Is.EqualTo(OffsetOf(html, "<p id='next'")));
    }

    [Test]
    public void LaterLineOfALongParagraphPointsAtItsParagraph()
    {
        var html = First + "<p id='long'>" + string.Join("<br>", Enumerable.Repeat("line", 80)) + "</p>";
        Assert.That(Fails(html).SourceOffset, Is.EqualTo(OffsetOf(html, "<p id='long'")));
    }

    [Test]
    public void ListItemPointsAtItsItem()
    {
        const string html = Filler + "<ul><li id='item'>Item.</li></ul>";
        Assert.That(Fails(html).SourceOffset, Is.EqualTo(OffsetOf(html, "<li id='item'")));
    }

    [Test]
    public void ImagePointsAtItsElement()
    {
        const string html = Filler + "<img id='pic' src='a'>";
        Assert.That(Fails(html).SourceOffset, Is.EqualTo(OffsetOf(html, "<img id='pic'")));
    }

    [Test]
    public void TablePointsAtItsElement()
    {
        const string html = Filler + "<table id='grid' style='width:100pt'><colgroup><col style='width:100pt'></colgroup><tbody><tr><td>Cell</td></tr></tbody></table>";
        Assert.That(Fails(html).SourceOffset, Is.EqualTo(OffsetOf(html, "<table id='grid'")));
    }

    [Test]
    public void TableRowsThatOverflowPointAtTheirTable()
    {
        var html = First + "<table id='grid' style='width:100pt'><colgroup><col style='width:100pt'></colgroup><tbody>"
            + string.Concat(Enumerable.Repeat("<tr><td>Row</td></tr>", 80)) + "</tbody></table>";
        Assert.That(Fails(html).SourceOffset, Is.EqualTo(OffsetOf(html, "<table id='grid'")));
    }

    [Test]
    public void PaintedPanelPointsAtItsElement()
    {
        const string html = First + "<div id='panel' style='break-before:page;border:1pt solid black'><p>Inside.</p></div>";
        Assert.That(Fails(html).SourceOffset, Is.EqualTo(OffsetOf(html, "<div id='panel'")));
    }

    [Test]
    public void HeadingKeptWithItsNextBlockPointsAtTheHeading()
    {
        const string html = First + "<h2 id='heading' style='break-before:page'>Heading</h2><p>Body.</p>";
        Assert.That(Fails(html).SourceOffset, Is.EqualTo(OffsetOf(html, "<h2 id='heading'")));
    }

    [Test]
    public void ExactlyMaxPagesStillRenders()
    {
        const string html = First + "<p style='break-before:page'>Second page.</p>";
        Assert.That(Pdf.Write().WithLimits(l => l.MaxPages(2)).ToBytes(html), Is.Not.Empty);
    }
}
