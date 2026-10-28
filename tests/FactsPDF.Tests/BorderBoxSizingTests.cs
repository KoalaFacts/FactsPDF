using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>Actual border-box PDF geometry and strict unchanged-output recovery.</summary>
[TestFixture]
public sealed class BorderBoxSizingTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 500, Margin = 20 };

    private static byte[] Pdf(string html, PdfOptions? options = null)
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

    private static void FailClosed(string html, string code)
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
    public void ExplicitContentBoxDoesNotChangeLegacyPdfBytes()
    {
        const string original = "<section style='width:136pt;padding:10pt;border:2pt solid blue;background:#f0f6fa'>Service reference</section>";
        const string explicitBox = "<section style='box-sizing:content-box;width:136pt;padding:10pt;border:2pt solid blue;background:#f0f6fa'>Service reference</section>";
        Assert.That(Pdf(explicitBox), Is.EqualTo(Pdf(original)));
    }

    [Test]
    public void BorderBoxIncludesPaddingAndBordersInWidthAndActualTextPosition()
    {
        const string border = "<section style='box-sizing:border-box;width:160pt;padding:10pt;border:2pt solid blue;background:#f0f6fa'>Service reference</section>";
        const string content = "<section style='width:136pt;padding:10pt;border:2pt solid blue;background:#f0f6fa'>Service reference</section>";
        Assert.That(Pdf(border), Is.EqualTo(Pdf(content)));
        var page = Layout(border).Single();
        Assert.That(page.Runs.Single().X, Is.EqualTo(32).Within(.00001));
        var paint = page.PaintBoxes.Single(x => x.Style.BackgroundColor.HasValue);
        Assert.That(paint.X, Is.EqualTo(20).Within(.00001));
        Assert.That(paint.Width, Is.EqualTo(160).Within(.00001));
    }

    [Test]
    public void PercentWidthAndPaddingResolveAgainstContainingContentWidth()
    {
        // 260pt containing width: width 50%=130 outer, two 10%=26pt
        // padding sides and two 2pt borders leave exactly 74pt of content.
        const string border = "<div style='box-sizing:border-box;width:50%;padding:0 10%;border:2pt solid red;background:blue'>TOTAL</div>";
        const string content = "<div style='width:74pt;padding:0 10%;border:2pt solid red;background:blue'>TOTAL</div>";
        Assert.That(Pdf(border), Is.EqualTo(Pdf(content)));
        Assert.That(Layout(border).Single().Runs.Single().X, Is.EqualTo(48).Within(.00001));
    }

    [Test]
    public void HorizontalAutoMarginsCenterTheOuterBorderBox()
    {
        const string border = "<p style='box-sizing:border-box;width:160pt;margin:0 auto;padding:10pt;border:2pt solid blue;background:red'>Summary</p>";
        const string content = "<p style='width:136pt;margin:0 50pt;padding:10pt;border:2pt solid blue;background:red'>Summary</p>";
        Assert.That(Pdf(border), Is.EqualTo(Pdf(content)));
        Assert.That(Layout(border).Single().PaintBoxes.Single().X, Is.EqualTo(70).Within(.00001));
    }

    [Test]
    public void CssWideKeywordsAndImportantRespectNonInheritance()
    {
        const string html = "<style>.outer{box-sizing:border-box;width:210pt;padding:5pt}" +
            ".child{box-sizing:content-box!important}</style><section class=outer>" +
            "<div class=child style='box-sizing:border-box;width:70pt'>A</div>" +
            "<div style='box-sizing:inherit;width:70pt'>B</div>" +
            "<div style='box-sizing:unset;width:70pt'>C</div>" +
            "<div style='box-sizing:initial;width:70pt'>D</div>" +
            "<div style='width:70pt'>E</div></section>";
        var root = HtmlDocumentReader.ReadTree(html, Page, default);
        var outer = (BlockNode)root.Children.Single();
        Assert.That(outer.BoxStyle.BorderBoxSizing, Is.True);
        var children = outer.Children.Cast<BlockNode>().ToArray();
        Assert.That(children.Select(x => x.BoxStyle.BorderBoxSizing),
            Is.EqualTo(new[] { false, true, false, false, false }));
    }

    [Test]
    public void ContinuedPaintedBoxAndPageTextMatchIndependentContentWidthProjection()
    {
        // Leave room for the closing 4pt padding and 1pt border on the
        // final page; this still forces multiple real painted page slices.
        var options = Page with { PageHeight = 155 };
        const string body = "<p style='margin:0'>Line 01<br>Line 02<br>Line 03<br>Line 04<br>Line 05<br>Line 06<br>Line 07<br>Line 08<br>Line 09</p>";
        var border = "<section style='box-sizing:border-box;width:160pt;padding:4pt;border:1pt solid blue;background:#f0f6fa'>" + body + "</section>";
        var content = "<section style='width:150pt;padding:4pt;border:1pt solid blue;background:#f0f6fa'>" + body + "</section>";
        Assert.That(Pdf(border, options), Is.EqualTo(Pdf(content, options)));
        var pages = Layout(border, options);
        Assert.That(pages.Count, Is.GreaterThan(1));
        Assert.That(pages.SelectMany(p => p.Runs).Select(r => r.Text),
            Is.EqualTo(Enumerable.Range(1, 9).Select(i => $"Line {i:00}")));
        Assert.That(pages.SelectMany(p => p.PaintBoxes).Where(p => p.Style.BackgroundColor.HasValue)
            .All(p => Math.Abs(p.Width - 160) < .00001), Is.True);
    }

    [TestCase("<div style='box-sizing:padding-box'>Invalid</div>", "FPDF1202")]
    [TestCase("<style>.never{box-sizing:border-box content-box}</style><p>Unmatched invalid</p>", "FPDF1202")]
    [TestCase("<div style='box-sizing:border-box;width:10pt;padding:6pt'>Too narrow</div>", "FPDF1302")]
    [TestCase("<div style='box-sizing:border-box;width:270pt'>Too wide</div>", "FPDF1302")]
    [TestCase("<span style='box-sizing:border-box'>Inline</span>", "FPDF1201")]
    [TestCase("<img src='logo' style='box-sizing:border-box'>", "FPDF1201")]
    [TestCase("<table style='box-sizing:border-box'>", "FPDF1201")]
    public void InvalidOrUnsupportedBoxSizingKeepsExistingOutput(string html, string error)
        => FailClosed(html, error);
}
