using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>Independent M7 follow-up: layout occupancy, empty-child margins and bounded paint work.</summary>
[TestFixture]
public sealed class BoxPaintingIndependentReviewTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 500, Margin = 20 };

    private static List<LayoutPage> Layout(string html, PdfOptions? options = null)
    {
        var config = options ?? Page;
        return TextLayout.Layout(HtmlDocumentReader.ReadTree(html, config, default), config, default);
    }

    private static FactsPdfException Fails(string html, string code, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        output.Write("keep"u8);
        var error = Assert.Throws<FactsPdfException>(() =>
            PdfConverter.Convert(html, output, options ?? Page));
        Assert.That(error!.Code, Is.EqualTo(code));
        Assert.That(Encoding.ASCII.GetString(output.ToArray()), Is.EqualTo("keep"));
        return error;
    }

    [Test]
    public void UnpaintedPaddedChildRetainsOriginalFragmentWhenAncestorOverflows()
    {
        var small = Page with { PageHeight = 100 };
        var pages = Layout("<div style='background-color:red'>" +
            "<div style='padding:25pt 0'></div><p>B</p></div>", small);
        Assert.That(pages, Has.Count.EqualTo(2));
        Assert.That(pages[0].PaintBoxes.Single().IsFirstFragment, Is.True);
        Assert.That(pages[1].PaintBoxes.Single().IsLastFragment, Is.True);
    }

    [Test]
    public void NestedUnpaintedEmptyPaddingKeepsAncestorFirstPageFragment()
    {
        var small = Page with { PageHeight = 100 };
        var pages = Layout("<section style='background-color:blue'><div>" +
            "<div style='padding-top:25pt;padding-bottom:25pt'></div></div>" +
            "<p style='break-before:page'>B</p></section>", small);
        Assert.That(pages, Has.Count.EqualTo(2));
        Assert.That(pages[0].PaintBoxes.Single().IsFirstFragment, Is.True);
        Assert.That(pages[1].PaintBoxes.Single().IsLastFragment, Is.True);
    }

    [Test]
    public void EmptyLastChildMarginIsWithinDecoratedParentBackground()
    {
        const string html = "<div style='background-color:red;padding-bottom:10pt'>" +
            "<p style='margin-bottom:40pt'></p></div>";
        var box = Layout(html)[0].PaintBoxes.Single();
        Assert.That(box.Top, Is.EqualTo(Page.Margin).Within(0.00001));
        Assert.That(box.Height, Is.EqualTo(50).Within(0.00001));
    }

    [Test]
    public void EmptyLastChildMarginDoesNotEnlargeItsOwnPaintedParagraph()
    {
        const string html = "<div style='background-color:blue'>" +
            "<p style='background-color:red;margin-bottom:40pt'></p></div>";
        var boxes = Layout(html)[0].PaintBoxes.OrderBy(x => x.Order).ToArray();
        Assert.That(boxes, Has.Length.EqualTo(2));
        Assert.That(boxes[0].Height, Is.EqualTo(40).Within(0.00001));
        Assert.That(boxes[1].Height, Is.Zero);
    }

    [TestCase("padding:1pt 2pt", "2pt")]
    [TestCase("border-width:1pt 2pt", "2pt")]
    [TestCase("border-color:red blue", "blue")]
    public void DeclarationLimitPinpointsTheTermThatExpandsIntoRejectedSide(string declaration, string term)
    {
        var html = "<div style='" + declaration + "'>A</div>";
        var error = Fails(html, "FPDF1205", Page with { MaxCssDeclarations = 1 });
        Assert.That(error.SourceOffset, Is.EqualTo(html.IndexOf(term, StringComparison.Ordinal)));
    }

    [Test]
    public void SharedPdfPainterStopsAtOutputBudgetWhileAppendingRectangles()
    {
        var style = new BoxStyle { BackgroundColor = new Rgb(1, 0, 0) };
        var one = new PaintedBox(20, 20, 100, 20, style, new Rgb(0, 0, 0), 0);
        var boxes = Enumerable.Repeat(one, 1000).ToArray();
        var content = new StringBuilder();
        var error = Assert.Throws<FactsPdfException>(() =>
            PdfPaintSerializer.Append(content, boxes, Page.PageHeight, 128, default));
        Assert.That(error!.Code, Is.EqualTo("FPDF1401"));
        Assert.That(content.Length, Is.LessThan(256),
            "The painter must never build thousands of commands after the budget has already been exceeded.");
    }
}
