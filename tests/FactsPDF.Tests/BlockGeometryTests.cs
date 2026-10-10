using System.Globalization;
using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>M6: width/padding and the text geometry of nested block content; no box painting.</summary>
[TestFixture]
public sealed class BlockGeometryTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 500, Margin = 20 };
    private static List<LayoutPage> Layout(string html, PdfOptions? options = null)
    {
        var settings = options ?? Page;
        return TextLayout.Layout(HtmlDocumentReader.ReadTree(html, settings, default), settings, default);
    }
    private static BlockNode Box(BlockChild child) => (BlockNode)child;
    private static FactsPdfException Fails(string html, string code, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        output.Write("keep"u8);
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, options ?? Page));
        Assert.That(error!.Code, Is.EqualTo(code));
        Assert.That(Encoding.ASCII.GetString(output.ToArray()), Is.EqualTo("keep"));
        return error;
    }

    [Test]
    public void WidthAndFourSidedPaddingRetainTheirOwnBoxStyleAndMoveText()
    {
        const string html = "<div style='width:200pt;padding:10pt 20pt 30pt 15pt'><p>A</p></div><p>B</p>";
        var root = HtmlDocumentReader.ReadTree(html, Page, default);
        var div = Box(root.Children[0]);
        Assert.That(div.BoxStyle.Width!.Value.Value, Is.EqualTo(200));
        Assert.That(div.BoxStyle.PaddingTop.Value, Is.EqualTo(10));
        Assert.That(div.BoxStyle.PaddingRight.Value, Is.EqualTo(20));
        Assert.That(div.BoxStyle.PaddingBottom.Value, Is.EqualTo(30));
        Assert.That(div.BoxStyle.PaddingLeft.Value, Is.EqualTo(15));
        var text = Layout(html)[0].Runs;
        Assert.That(text.Single(r => r.Text == "A").X, Is.EqualTo(35).Within(0.00001));
        Assert.That(text.Single(r => r.Text == "B").X, Is.EqualTo(20).Within(0.00001));
        Assert.That(text.Single(r => r.Text == "B").Baseline,
            Is.LessThan(text.Single(r => r.Text == "A").Baseline - 30));
    }

    [Test]
    public void PercentageWidthAndPaddingUseContainingContentWidth()
    {
        const string html = "<section style='width:80%;padding:10pt'><div style='width:50%;padding-left:10%;padding-right:5%'><p style='text-align:right'>AA</p></div></section>";
        var run = Layout(html)[0].Runs.Single();
        // Root C=260; section C=208; child C=104; child x=20+10+20.8.
        // Right aligned Courier AA is 14.4pt wide: x=50.8+104-14.4.
        Assert.That(run.X, Is.EqualTo(140.4).Within(0.001));
    }

    [Test]
    public void AutoWidthSubtractsPaddingBeforeWrappingAnonymousText()
    {
        const string html = "<div style='width:45pt'>alpha beta gamma</div><p>End</p>";
        var runs = Layout(html)[0].Runs;
        Assert.That(runs.Where(r => r.Text is "alpha" or "beta" or "gamma").Select(r => r.Text),
            Is.EqualTo(new[] { "alpha", "beta", "gamma" }));
        Assert.That(runs.Single(r => r.Text == "End").X, Is.EqualTo(20));
    }

    [Test]
    public void VerticalPercentagePaddingUsesContainingWidthNotPageHeight()
    {
        var a = Layout("<div>A</div>")[0].Runs.Single();
        var b = Layout("<div style='padding-top:10%;padding-left:10%'>A</div>")[0].Runs.Single();
        Assert.That(b.X, Is.EqualTo(46).Within(0.001)); // 10% of root's 260pt content width
        Assert.That(a.Baseline - b.Baseline, Is.EqualTo(26).Within(0.001));
    }

    [Test]
    public void BottomPaddingShiftsFollowingSiblingWithoutPainting()
    {
        var runs = Layout("<div style='padding-top:10pt;padding-bottom:30pt'>A</div>B")[0].Runs;
        Assert.That(runs.Select(x => x.Text), Is.EqualTo(new[] { "A", "B" }));
        Assert.That(runs[0].Baseline - runs[1].Baseline, Is.GreaterThan(40));
    }

    [Test]
    public void ShorthandLonghandAndImportantCascadePerSide()
    {
        const string html = "<style>.card{padding:2pt 8pt;padding-left:12pt}.card{padding-top:6pt !important}</style>" +
            "<div class=card style='padding:3pt 4pt 5pt 6pt;padding-right:7pt !important'>X</div>";
        var box = Box(HtmlDocumentReader.ReadTree(html, Page, default).Children.Single());
        Assert.That(box.BoxStyle.PaddingTop.Value, Is.EqualTo(6));
        Assert.That(box.BoxStyle.PaddingRight.Value, Is.EqualTo(7));
        Assert.That(box.BoxStyle.PaddingBottom.Value, Is.EqualTo(5));
        Assert.That(box.BoxStyle.PaddingLeft.Value, Is.EqualTo(6));
        Assert.That(Layout(html)[0].Runs.Single().X, Is.EqualTo(26));
    }

    [Test]
    public void InheritInitialAndUnsetAreComputedSeparatelyFromTextInheritance()
    {
        const string html = "<div style='padding-left:20pt'><div style='padding-left:inherit'>A</div>" +
            "<div style='padding-left:unset'>B</div><div style='padding-left:initial'>C</div></div>";
        var runs = Layout(html)[0].Runs;
        Assert.That(runs.Select(x => x.X), Is.EqualTo(new[] { 60d, 40d, 40d }));
    }

    [Test]
    public void ExplicitOrAutoWidthCannotExceedParentContentRectangle()
    {
        var tooWide = Fails("<div style='width:100%;padding-left:1pt'>A</div>", "FPDF1302");
        Assert.That(tooWide.SourceOffset, Is.EqualTo(0));
        Fails("<div style='padding-left:150pt;padding-right:150pt'>A</div>", "FPDF1302");
        Fails("<div style='width:0'>A</div>", "FPDF1302");
    }

    [TestCase("width:-1pt")]
    [TestCase("padding:1pt 2pt 3pt 4pt 5pt")]
    [TestCase("padding:1pt auto")]
    [TestCase("width:calc(100% - 10pt)")]
    [TestCase("padding-left:-2%")]
    [TestCase("padding:1pt,2pt")]
    public void InvalidBoxDeclarationsAreRejectedBeforeWriting(string css)
        => Fails("<div style='" + css + "'>A</div>", "FPDF1202");

    [Test]
    public void UnsupportedInlineAndHtmlBoxPropertiesFailExplicitly()
    {
        Fails("<div><span style='width:10pt'>A</span></div>", "FPDF1201");
        Fails("<html style='padding:1pt'><body><p>A</p></body></html>", "FPDF1201");
        Fails("<style>span{padding:1pt}</style><span>A</span>", "FPDF1201");
        Fails("<div style='border:1pt solid red'>A</div>", "FPDF1201");
    }

    [Test]
    public void PaddingShorthandExpansionConsumesDeclarationBudget()
        => Fails("<div style='padding:1pt'>A</div>", "FPDF1205",
            Page with { MaxCssDeclarations = 3 });

    [Test]
    public void GeometryIsLocaleIndependentAndStaysWithinPageBounds()
    {
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var run = Layout("<div style='width:120.5pt;padding-left:10.5pt'>A</div>")[0].Runs.Single();
            Assert.That(run.X, Is.EqualTo(30.5).Within(0.00001));
        }
        finally { CultureInfo.CurrentCulture = culture; }
    }


    [Test]
    public void BoxLengthsAcceptCssZeroDecimalsAndPixelUnits()
    {
        var runs = Layout("<div style='width:80px;padding-left:8px;padding-right:0.0'>A</div>")[0].Runs;
        Assert.That(runs.Single().X, Is.EqualTo(26).Within(0.00001));
        var empty = Layout("<div style='width:-0'></div><p>B</p>")[0].Runs;
        Assert.That(empty.Single().Text, Is.EqualTo("B"));
    }

    [Test]
    public void PaddingDoesNotImplicitlyInheritAndExplicitShorthandInheritDoes()
    {
        const string html = "<div style='padding:1pt 20pt'><div>A</div>" +
            "<div style='padding:inherit'>B</div></div>";
        var runs = Layout(html)[0].Runs;
        Assert.That(runs.Select(x => x.X), Is.EqualTo(new[] { 40d, 60d }));
    }

    [Test]
    public void PaddingDoesNotProduceAStandaloneBlankPageAtTheEnd()
    {
        var pages = Layout("<div>A</div><div style='padding-bottom:450pt'></div>");
        Assert.That(pages, Has.Count.EqualTo(1));
        Assert.That(pages[0].Runs.Select(x => x.Text), Is.EqualTo(new[] { "A" }));
    }

    [Test]
    public void TopPaddingMovesWithTheFirstTextLineWhenAPageBreakIsRequired()
    {
        const string html = "<p>A</p><div style='padding-top:240pt'>B</div>";
        var small = Page with { PageHeight = 300 };
        var pages = Layout(html, small);
        Assert.That(pages, Has.Count.EqualTo(2));
        Assert.That(pages[0].Runs.Single().Text, Is.EqualTo("A"));
        Assert.That(pages[1].Runs.Single().Text, Is.EqualTo("B"));
        Assert.That(pages[1].Runs.Single().Baseline, Is.LessThan(60));
    }

    [Test]
    public void InheritedExplicitWidthRetainsParentSpecifiedContentWidth()
    {
        var root = HtmlDocumentReader.ReadTree(
            "<div style='width:120pt'><div style='width:inherit'>A</div></div>", Page, default);
        var child = Box(Box(root.Children.Single()).Children.Single());
        Assert.That(child.BoxStyle.Width!.Value.Value, Is.EqualTo(120));
        Assert.That(Layout("<div style='width:120pt'><div style='width:inherit'>A</div></div>")[0].Runs.Single().X,
            Is.EqualTo(20));
    }

    [Test]
    public void CancelledTreeLayoutAndMaxPagesRetainExistingGuardrails()
    {
        const string html = "<div style='width:180pt;padding:10pt'><p>A</p><p style='break-before:page'>B</p></div>";
        var tree = HtmlDocumentReader.ReadTree(html, Page, default);
        Assert.Throws<OperationCanceledException>(() => TextLayout.Layout(tree, Page, new CancellationToken(true)));
        var error = Assert.Throws<FactsPdfException>(() => TextLayout.Layout(tree, Page with { MaxPages = 1 }, default));
        Assert.That(error!.Code, Is.EqualTo("FPDF1303"));
    }
}
