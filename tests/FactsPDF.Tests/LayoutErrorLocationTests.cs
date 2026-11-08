using NUnit.Framework;

namespace FactsPDF.Tests;

// Layout-phase fit failures on body text point at the element whose text could
// not be placed, so SourceOffset and the CLI's "Location: line L, column C."
// name it instead of leaving -1.
public sealed class LayoutErrorLocationTests
{
    private const string Prefix = "<p>Fine text.</p>\n";

    private static FactsPdfException Fails(string html, string message)
    {
        var error = Assert.Throws<FactsPdfException>(() => Pdf.Write().ToBytes(html))!;
        Assert.That(error.Message, Is.EqualTo("FPDF1302: " + message));
        Assert.That(error.Code, Is.EqualTo("FPDF1302"));
        return error;
    }

    private static int OffsetOf(string html, string marker) => html.IndexOf(marker, StringComparison.Ordinal);

    [Test]
    public void FirstLineTallerThanThePagePointsAtItsParagraph()
    {
        const string html = Prefix + "<p id='tall' style='line-height:1000pt'>i</p>";
        var error = Fails(html, "Paragraph margin, box padding and first line cannot fit on a page.");
        Assert.That(error.SourceOffset, Is.EqualTo(OffsetOf(html, "<p id='tall'")));
    }

    [Test]
    public void BoxPaddingThatLeavesNoRoomPointsAtTheParagraphInside()
    {
        const string html = Prefix + "<div style='border:1pt solid black;padding-top:780pt'><p id='inner'>i</p></div>";
        var error = Fails(html, "Paragraph margin, box padding and first line cannot fit on a page.");
        Assert.That(error.SourceOffset, Is.EqualTo(OffsetOf(html, "<p id='inner'>")));
    }

    [Test]
    public void ListItemKeepsItsMarkerOffset()
    {
        const string html = Prefix + "<ul><li id='item' style='line-height:1000pt'>i</li></ul>";
        var error = Fails(html, "Paragraph margin, box padding and first line cannot fit on a page.");
        Assert.That(error.SourceOffset, Is.EqualTo(OffsetOf(html, "<li id='item'")));
    }

    [Test]
    public void PanelTopMarginThatLeavesNoRoomPointsAtTheParagraphInside()
    {
        const string html = Prefix + "<div style='border:1pt solid black;margin-top:780pt'><p id='inner'>i</p></div>";
        var error = Fails(html, "Report panel top margin and first line cannot fit on a fresh page.");
        Assert.That(error.SourceOffset, Is.EqualTo(OffsetOf(html, "<p id='inner'>")));
    }

    [Test]
    public void LaterLineTallerThanThePagePointsAtTheTallSpan()
    {
        const string html = Prefix + "<p>i<br><span style='font-size:20pt'>x</span> <span id='tall' style='line-height:1000pt'>y</span></p>";
        var error = Fails(html, "Line height exceeds the usable page height.");
        Assert.That(error.SourceOffset, Is.EqualTo(OffsetOf(html, "<span id='tall'")));
    }

    [Test]
    public void DisplayCommandLimitReachedBeforeAParagraphPointsAtIt()
    {
        const string html = "<p>one</p>\n<p id='second'>two</p>";
        var error = Assert.Throws<FactsPdfException>(
            () => Pdf.Write().WithLimits(limits => limits.MaxDisplayCommands(1)).ToBytes(html))!;
        Assert.That(error.Code, Is.EqualTo("FPDF1401"), error.Message);
        Assert.That(error.SourceOffset, Is.EqualTo(OffsetOf(html, "<p id='second'>")));
    }
}
