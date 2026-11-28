using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// Text decorations propagate to descendant inline boxes: a descendant cannot remove an ancestor's
/// line, adds its own to them, and the inherited lines keep the colour of the element that declared
/// them (not the descendant's text colour).
/// </summary>
[TestFixture]
public sealed class InlineDecorationPropagationTests
{
    private static readonly PdfFont Font = PdfFont.FromBytes(FontFixture.Create());
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 400, Margin = 20, FontSize = 10, Fonts = [Font] };

    private static PlacedText[] Runs(string html)
    {
        var source = "<style>body{margin:0}p{margin:0}</style>" + html;
        var pages = TextLayout.Layout(HtmlDocumentReader.ReadTree(source, Page, default), Page, default);
        return pages[0].Runs.OrderBy(r => r.X).ToArray();
    }

    [Test]
    public void NoneOnADescendantDoesNotRemoveTheAncestorsLine()
    {
        var runs = Runs("<p><u>A<span style='text-decoration:none'>B</span></u></p>");
        Assert.That(runs.Select(r => r.Style.Decoration), Is.All.EqualTo(TextDecoration.Underline));
    }

    [Test]
    public void ADescendantAddsItsLineToTheAncestors()
    {
        var runs = Runs("<p><u>A<s>B</s></u></p>");
        Assert.That(runs.Single(r => r.Text == "A").Style.Decoration, Is.EqualTo(TextDecoration.Underline));
        Assert.That(runs.Single(r => r.Text == "B").Style.Decoration, Is.EqualTo(TextDecoration.Underline | TextDecoration.LineThrough));
    }

    [Test]
    public void InheritedLinesKeepTheDecoratingElementsColour()
    {
        var runs = Runs("<p><u style='color:#f00'>A<span style='color:#00f'>B</span></u></p>");
        var red = new Rgb(1, 0, 0);
        Assert.That(runs.Single(r => r.Text == "B").Style.Color, Is.EqualTo(new Rgb(0, 0, 1)));
        Assert.That(runs.Single(r => r.Text == "B").Style.DecorationColor, Is.EqualTo(red));
        Assert.That(runs.Single(r => r.Text == "A").Style.DecorationColor ?? runs.Single(r => r.Text == "A").Style.Color, Is.EqualTo(red));
    }

    [Test]
    public void ABlocksLinePropagatesToItsChildrenToo()
    {
        var runs = Runs("<div style='text-decoration:underline'><p style='text-decoration:none'>A</p></div>");
        Assert.That(runs.Single().Style.Decoration, Is.EqualTo(TextDecoration.Underline));
    }

    [Test]
    public void AnUndecoratedPageIsUnchanged()
    {
        var runs = Runs("<p>A<span style='text-decoration:none'>B</span></p>");
        Assert.That(runs.Select(r => r.Style.Decoration), Is.All.EqualTo(TextDecoration.None));
    }

    [Test]
    public void NewLinesInADifferentPaintThanTheLinesThatStayFail()
    {
        using var output = new MemoryStream();
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(
            "<p><u>A<s style='color:#f00'>B</s></u></p>", output, Page));
        Assert.That(error!.Code, Is.EqualTo("FPDF1201"));
    }

    [Test]
    public void ARepeatedLineInAnotherColourPaintsOverTheInheritedOne()
    {
        var runs = Runs("<p><u>A<a href='https://example.com/'>B</a></u></p>");
        Assert.That(runs.Single(r => r.Text == "B").Style.Decoration, Is.EqualTo(TextDecoration.Underline));
    }
}
