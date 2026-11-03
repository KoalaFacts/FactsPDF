using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// Bounded outside-the-border percentage margins measured from the current
/// containing-block content width, independently of the page height.
/// </summary>
[TestFixture]
public sealed class PercentagePanelMarginTests
{
    private static readonly PdfOptions Page = new()
    {
        PageWidth = 300, PageHeight = 500, Margin = 20
    };

    private static byte[] Render(string html, PdfOptions? options = null)
    {
        using var target = new MemoryStream();
        PdfConverter.Convert(html, target, options ?? Page);
        return target.ToArray();
    }

    private static List<LayoutPage> Layout(string html, PdfOptions? options = null)
    {
        var o = options ?? Page;
        return TextLayout.Layout(HtmlDocumentReader.ReadTree(html, o, default), o, default);
    }

    private static void RejectPreservingOutput(string html, string code, PdfOptions? options = null)
    {
        using var target = new MemoryStream();
        target.Write("previous-pdf"u8);
        target.Position = 3;
        var exception = Assert.Throws<FactsPdfException>(() =>
            PdfConverter.Convert(html, target, options ?? Page));
        Assert.That(exception!.Code, Is.EqualTo(code));
        Assert.That(target.ToArray(), Is.EqualTo("previous-pdf"u8.ToArray()));
        Assert.That(target.Position, Is.EqualTo(3));
    }

    [TestCase("div")]
    [TestCase("section")]
    [TestCase("article")]
    [TestCase("blockquote")]
    public void PercentageMarginsReserveRealOutsideSpaceAndPaintAtMeasuredPosition(string tag)
    {
        var actual = "<" + tag + " style='margin-top:10%;margin-bottom:5%;" +
            "padding:4pt;background:#ddeeff'><p style='margin:0'>MEASURED</p></" + tag + ">" +
            "<p style='margin:0'>AFTER</p>";
        var oracle = actual.Replace("margin-top:10%;margin-bottom:5%",
            "margin-top:26pt;margin-bottom:13pt");
        Assert.That(Render(actual), Is.EqualTo(Render(oracle)));
        var box = Layout(actual).Single().PaintBoxes.Single(x => x.Style.BackgroundColor.HasValue);
        Assert.That(box.Top, Is.EqualTo(46).Within(0.000001));
    }

    [Test]
    public void NestedPercentageMarginsUseTheInnerContainingWidthNotThePageOrParentOuterWidth()
    {
        const string start = "<section style='width:180pt;margin-top:10%;padding:4pt;background:#ddeeff'>" +
            "<article style='margin-top:";
        const string middle = ";margin-bottom:";
        const string end = ";background:#bbddff'><p style='margin:0'>NESTED</p></article>" +
            "</section><p style='margin:0'>AFTER</p>";
        var actual = start + "12.5%" + middle + "5%" + end;
        var oracle = start.Replace("margin-top:10%", "margin-top:26pt") +
            "22.5pt" + middle + "9pt" + end;
        Assert.That(Render(actual), Is.EqualTo(Render(oracle)));
        var boxes = Layout(actual).Single().PaintBoxes;
        Assert.That(boxes.Count, Is.EqualTo(2));
        Assert.That(boxes.Min(x => x.Top), Is.EqualTo(46).Within(0.000001));
        Assert.That(boxes.Max(x => x.Top), Is.EqualTo(72.5).Within(0.000001));
    }

    [Test]
    public void ShorthandLogicalSidesAndImportantUseTheExistingPhysicalCascade()
    {
        const string html = "<style>section{margin-block:2% 1%}" +
            ".report{margin-top:10%!important;margin-bottom:4%}</style>" +
            "<section class='report' style='margin:1% 0 3%'>" +
            "<p style='margin:0'>CASCADE</p></section>";
        const string oracle = "<section style='margin-top:26pt;margin-bottom:7.8pt'>" +
            "<p style='margin:0'>CASCADE</p></section>";
        Assert.That(Render(html), Is.EqualTo(Render(oracle)));

        const string logical = "<article style='margin-block-start:10%;margin-block-end:5%'>" +
            "<p style='margin:0'>LOGICAL</p></article>";
        const string physical = "<article style='margin-top:26pt;margin-bottom:13pt'>" +
            "<p style='margin:0'>LOGICAL</p></article>";
        Assert.That(Render(logical), Is.EqualTo(Render(physical)));
    }

    [Test]
    public void PanelInheritanceRetainsPercentageUntilTheChildKnowsItsOwnWidth()
    {
        const string percent = "<section style='width:180pt;margin-top:10%'>" +
            "<article style='margin-top:inherit;margin-bottom:unset'>" +
            "<p style='margin:0'>INHERIT</p></article></section>";
        const string points = "<section style='width:180pt;margin-top:26pt'>" +
            "<article style='margin-top:18pt;margin-bottom:0'>" +
            "<p style='margin:0'>INHERIT</p></article></section>";
        Assert.That(Render(percent), Is.EqualTo(Render(points)));
    }

    [Test]
    public void StandaloneHorizontalRuleUsesTheSameParentWidth()
    {
        const string percent = "<p style='margin:0'>TOP</p>" +
            "<hr style='margin-top:10%;margin-bottom:5%'>" +
            "<p style='margin:0'>END</p>";
        Assert.That(Render(percent), Is.EqualTo(Render(percent.Replace(
            "margin-top:10%;margin-bottom:5%", "margin-top:26pt;margin-bottom:13pt"))));
    }

    [Test]
    public void PercentMarginOnLongContinuedPanelAppearsOnlyOnFirstAndLastFragment()
    {
        var page = Page with { PageHeight = 150 };
        // Retain a genuine multi-page painted box, while reserving enough
        // final-fragment space for the 13pt closing margin and lower border.
        // A 30-row fixture on a 160pt page is intentionally unplaceable.
        var rows = string.Join("<br>", Enumerable.Range(1, 16).Select(i => $"Row-{i:00}"));
        var actual = "<section style='width:150pt;margin:10% auto 5%;" +
            "padding:4pt;border:1pt solid blue;background:#ddeeff'>" +
            "<p style='margin:0'>" + rows + "</p></section>";
        var oracle = actual.Replace("margin:10% auto 5%", "margin:26pt auto 13pt");
        Assert.That(Render(actual, page), Is.EqualTo(Render(oracle, page)));
        var pages = Layout(actual, page);
        Assert.That(pages.Count, Is.GreaterThan(1));
        Assert.That(pages.SelectMany(p => p.Runs).Count(), Is.EqualTo(16));
        var painted = pages.SelectMany(p => p.PaintBoxes).ToArray();
        Assert.That(painted.Length, Is.EqualTo(pages.Count));
        Assert.That(painted[0].Top, Is.EqualTo(46).Within(0.000001));
        Assert.That(painted.Skip(1).All(p => Math.Abs(p.Top - 20) < 0.000001), Is.True);
    }

    [TestCase("<p style='margin-top:10%'>PARAGRAPH</p>", "FPDF1202")]
    [TestCase("<h2 style='margin-bottom:5%'>HEADING</h2>", "FPDF1202")]
    [TestCase("<div style='margin-top:10%'><p style='margin-top:inherit'>WRONG UNIT</p></div>", "FPDF1201")]
    [TestCase("<section style='margin-bottom:10%'><h2 style='margin-bottom:inherit'>WRONG UNIT</h2></section>", "FPDF1201")]
    [TestCase("<ul><li><section style='margin-top:5%'><p>LIST</p></section></li></ul>", "FPDF1201")]
    [TestCase("<ol><li><article style='margin-block-end:8%'><p>LIST</p></article></li></ol>", "FPDF1201")]
    [TestCase("<section style='margin-top:auto'>INVALID</section>", "FPDF1202")]
    [TestCase("<section style='margin-top:-1%'>INVALID</section>", "FPDF1202")]
    [TestCase("<section style='margin-top:10001%'>INVALID</section>", "FPDF1202")]
    [TestCase("<section style='margin:10% 0 auto'>INVALID</section>", "FPDF1202")]
    [TestCase("<body style='margin-top:10%'>INVALID</body>", "FPDF1201")]
    [TestCase("<ol style='margin-top:10%'><li>INVALID</li></ol>", "FPDF1201")]
    public void UnsupportedMarginsFailExplicitlyWithoutChangingTheExistingPdf(string html, string code)
        => RejectPreservingOutput(html, code);

    [TestCase("<style>p{margin-top:5%}p{margin-top:0!important}</style><p>BAD</p>")]
    [TestCase("<style>h2{margin-bottom:8%}h2{margin-bottom:0!important}</style><h2>BAD</h2>")]
    [TestCase("<p style='margin-top:5%;margin-top:0'>BAD</p>")]
    public void UnsupportedTextPercentMarginsRejectEvenWhenAnotherRuleWins(string html)
        => RejectPreservingOutput(html, "FPDF1202");

    [Test]
    public void FittingEmptyPanelPercentMarginsStillReserveExactPhysicalSpace()
    {
        const string html = "<section style='margin-top:80%;margin-bottom:80%'></section>" +
            "<p style='margin:0'>AFTER EMPTY PANEL</p>";
        Assert.That(Render(html), Is.EqualTo(Render(html.Replace("80%", "208pt"))));
    }

    [TestCase("<section style='margin-top:200%'></section>")]
    [TestCase("<section style='margin-bottom:200%'><p style='margin:0'>TAIL</p></section>")]
    [TestCase("<section style='margin-top:90%;margin-bottom:90%'></section>")]
    [TestCase("<hr style='margin-top:90%;margin-bottom:90%'>")]
    public void UnplaceablePercentageMarginMustNotDisappearAtEmptyPanelOrPageBoundary(string html)
        => RejectPreservingOutput(html, "FPDF1302");

    [Test]
    public void TooLargeButSyntacticallyValidMarginFailsBeforeWriting()
        => RejectPreservingOutput("<section style='margin-top:10000%'>OVERFULL</section>",
            "FPDF1302", Page with { PageHeight = 160, MaxPages = 1 });

    [Test]
    public void PercentMarginFromUnmatchedSelectorStillRejectsInvalidSyntax()
        => RejectPreservingOutput("<style>.unmatched{margin-top:-1%}</style>" +
            "<section style='margin-top:5%'>SAFE</section>", "FPDF1202");
}
