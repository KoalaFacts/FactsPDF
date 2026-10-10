using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

[TestFixture]
public sealed class BoxFragmentationReviewBoundaryTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 100, Margin = 20 };

    private static List<LayoutPage> Layout(string html)
        => TextLayout.Layout(HtmlDocumentReader.ReadTree(html, Page, default), Page, default);

    [Test]
    public void NewlyMovedChildKeepsPaddingButContinuedParentDoesNotRepeatIt()
    {
        var pages = Layout("<section style='background-color:red;padding-top:20pt'>" +
            "<div style='padding:20pt 0'></div><div style='padding-top:10pt'><p>B</p></div></section>");
        var fresh = Layout("<div style='padding-top:10pt'><p>B</p></div>");
        Assert.That(pages, Has.Count.EqualTo(2));
        Assert.That(pages[1].Runs.Single().Baseline,
            Is.EqualTo(fresh[0].Runs.Single().Baseline).Within(0.00001));
        Assert.That(pages[0].PaintBoxes.Single().IsFirstFragment, Is.True);
        Assert.That(pages[1].PaintBoxes.Single().IsLastFragment, Is.True);
    }

    [Test]
    public void UnpaintedScopeInsideContinuedBackgroundDoesNotRepeatConsumedPadding()
    {
        var pages = Layout("<section style='background-color:red'><div style='padding-top:20pt'>" +
            "<div style='padding:20pt 0'></div><p>B</p></div></section>");
        var fresh = Layout("<p>B</p>");
        Assert.That(pages, Has.Count.EqualTo(2));
        Assert.That(pages[1].Runs.Single().Baseline,
            Is.EqualTo(fresh[0].Runs.Single().Baseline).Within(0.00001));
    }

    [Test]
    public void ConsumedAncestorPaddingIsNotReintroducedOnEmptyFinalPageClosure()
    {
        var pages = Layout("<section style='background-color:red;padding-top:20pt'>" +
            "<div style='padding:20pt 0'></div><p style='break-before:page'></p></section><p>B</p>");
        Assert.That(pages, Has.Count.EqualTo(2));
        Assert.That(pages[1].PaintBoxes.Single().Height, Is.EqualTo(8).Within(0.00001));
        var fresh = Layout("<p style='margin-top:8pt'>B</p>");
        Assert.That(pages[1].Runs.Single().Baseline,
            Is.EqualTo(fresh[0].Runs.Single().Baseline).Within(0.00001));
    }

    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public void ZeroHeightFragmentHasNoBorderOperatorsOrDisplayCharges(bool first, bool last)
    {
        var style = new BoxStyle
        {
            BorderTop = new BorderEdge(10, true, new Rgb(1, 0, 0)),
            BorderBottom = new BorderEdge(10, true, new Rgb(0, 1, 0))
        };
        var fragment = new PaintedBox(20, 20, 100, 0, style, new Rgb(0, 0, 0), 0,
            IsFirstFragment: first, IsLastFragment: last);
        var content = new StringBuilder();
        PdfPaintSerializer.Append(content, [fragment], Page.PageHeight, 1000, default);
        Assert.That(content.Length, Is.Zero);
        Assert.That(fragment.CommandCount, Is.Zero);
    }
}
