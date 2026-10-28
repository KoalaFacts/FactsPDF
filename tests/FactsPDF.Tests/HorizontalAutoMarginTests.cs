using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>Real centered and end-aligned report boxes using horizontal auto margins.</summary>
[TestFixture]
public sealed class HorizontalAutoMarginTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 500, Margin = 20 };

    private static byte[] Render(string html, PdfOptions? options = null)
    {
        using var target = new MemoryStream();
        PdfConverter.Convert(html, target, options ?? Page);
        return target.ToArray();
    }

    private static List<LayoutPage> Layout(string html, PdfOptions? options = null)
    {
        var page = options ?? Page;
        return TextLayout.Layout(HtmlDocumentReader.ReadTree(html, page, default), page, default);
    }

    private static void Reject(string html, string code)
    {
        using var target = new MemoryStream();
        target.Write("original-output"u8);
        target.Position = 4;
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, target, Page));
        Assert.That(error!.Code, Is.EqualTo(code));
        Assert.That(target.ToArray(), Is.EqualTo("original-output"u8.ToArray()));
        Assert.That(target.Position, Is.EqualTo(4));
    }

    [Test]
    public void TwoAutoSidesCenterAnExplicitWidthIncludingPaint()
    {
        const string auto = "<p style='width:100pt;margin:0 auto;background:#d7e6f2'>REPORT</p>";
        const string fixedSides = "<p style='width:100pt;margin:0 80pt;background:#d7e6f2'>REPORT</p>";
        Assert.That(Render(auto), Is.EqualTo(Render(fixedSides)));
        var page = Layout(auto).Single();
        Assert.That(page.Runs.Single().X, Is.EqualTo(100).Within(.00001));
        var paint = page.PaintBoxes.Single(x => x.Style.BackgroundColor.HasValue);
        Assert.That(paint.X, Is.EqualTo(100).Within(.00001));
        Assert.That(paint.Width, Is.EqualTo(100).Within(.00001));
    }

    [TestCase("margin-left:auto;margin-right:20pt", "margin-left:140pt;margin-right:20pt", 160d)]
    [TestCase("margin-left:10pt;margin-right:auto", "margin-left:10pt;margin-right:150pt", 30d)]
    public void AOneSidedAutoMarginTakesOnlyTheRemainingSpace(string auto, string fixedSides, double expectedX)
    {
        var fromAuto = "<p style='width:100pt;" + auto + ";background:blue'>ALIGNED</p>";
        var fromFixed = "<p style='width:100pt;" + fixedSides + ";background:blue'>ALIGNED</p>";
        Assert.That(Render(fromAuto), Is.EqualTo(Render(fromFixed)));
        Assert.That(Layout(fromAuto).Single().PaintBoxes.Single().X, Is.EqualTo(expectedX).Within(.00001));
    }

    [Test]
    public void PercentWidthAndBorderAndPaddingAreIncludedBeforeCentering()
    {
        const string auto = "<p style='width:40%;padding:5pt;border:2pt solid blue;margin:0 auto;background:red'>BUDGET</p>";
        const string fixedSides = "<p style='width:40%;padding:5pt;border:2pt solid blue;margin:0 71pt;background:red'>BUDGET</p>";
        Assert.That(Render(auto), Is.EqualTo(Render(fixedSides)));
        var box = Layout(auto).Single().PaintBoxes.Single();
        Assert.That(box.X, Is.EqualTo(91).Within(.00001));
        Assert.That(box.Width, Is.EqualTo(118).Within(.00001));
    }

    [Test]
    public void AutoSidesOnAnAutoWidthBoxResolveToZeroAndKeepOldPdfBytes()
    {
        Assert.That(Render("<p style='margin:0 auto'>Inspection complete</p>"),
            Is.EqualTo(Render("<p style='margin:0'>Inspection complete</p>")));
    }

    [Test]
    public void ImportantAndInlineCascadeApplyPerMarginSide()
    {
        const string css = "<style>.report{margin-left:auto!important;margin-right:2pt}" +
            "p.report{margin:0 10pt}</style>";
        var auto = css + "<p class=report style='width:100pt;margin:0 3pt'>Report total</p>";
        var explicitSides = "<p style='width:100pt;margin-left:157pt;margin-right:3pt'>Report total</p>";
        Assert.That(Render(auto), Is.EqualTo(Render(explicitSides)));
    }

    [Test]
    public void CssWideInitialClearsPreviouslySelectedAutoMargin()
    {
        const string auto = "<p style='width:100pt;margin:0 auto;margin-left:initial'>CENTERED?</p>";
        const string fixedSides = "<p style='width:100pt;margin-left:0;margin-right:160pt'>CENTERED?</p>";
        Assert.That(Render(auto), Is.EqualTo(Render(fixedSides)));
    }

    [Test]
    public void ExplicitPageBreakAndFollowingSiblingPreserveActualPositions()
    {
        const string html = "<p>First</p><p style='break-before:page;width:100pt;margin:0 auto;background:red'>Second</p>" +
            "<p>Third</p>";
        var pages = Layout(html, Page with { PageHeight = 105 });
        Assert.That(pages, Has.Count.EqualTo(2));
        Assert.That(pages[1].Runs.Select(x => x.Text), Is.EqualTo(new[] { "Second", "Third" }));
        Assert.That(pages[1].Runs[0].X, Is.EqualTo(100).Within(.00001));
        Assert.That(pages[1].Runs[1].X, Is.EqualTo(20).Within(.00001));
        Assert.That(pages[1].PaintBoxes.Single(x => x.Style.BackgroundColor.HasValue).X,
            Is.EqualTo(100).Within(.00001));
    }

    [Test]
    public void HeadingKeepUsesSameCenteredWidthAndPageBreakAsRendering()
    {
        var settings = Page with { PageHeight = 110 };
        const string lead = "<style>p,h2{font-size:10pt;line-height:2;margin-top:0;margin-bottom:0}</style>" +
            "<p>PRELUDE<br>PRELUDE</p>";
        const string auto = "<h2 style='width:100pt;margin:0 auto;break-after:avoid'>INSPECTION</h2>" +
            "<p style='width:100pt;margin:0 auto'>ALPHA<br>BETA<br>GAMMA</p>";
        const string fixedSides = "<h2 style='width:100pt;margin:0 80pt;break-after:avoid'>INSPECTION</h2>" +
            "<p style='width:100pt;margin:0 80pt'>ALPHA<br>BETA<br>GAMMA</p>";
        Assert.That(Render(lead + auto, settings), Is.EqualTo(Render(lead + fixedSides, settings)));
        Assert.That(Layout(lead + auto, settings), Has.Count.GreaterThan(1));
    }

    [TestCase("<p style='margin:auto'>Invalid vertical auto</p>", "FPDF1202")]
    [TestCase("<p style='margin:0 auto 0 -1pt'>Invalid negative</p>", "FPDF1202")]
    [TestCase("<p style='width:260pt;padding:10pt;margin:0 auto'>Too wide</p>", "FPDF1302")]
    [TestCase("<div style='margin-left:auto'>Invalid container</div>", "FPDF1201")]
    [TestCase("<table style='width:100pt'><colgroup><col style='width:100pt'></colgroup><tbody><tr><td><p style='margin:0 auto'>Invalid cell</p></td></tr></tbody></table>", "FPDF1201")]
    public void InvalidOrUnsupportedGeometryPreservesDestination(string html, string code)
        => Reject(html, code);
}
