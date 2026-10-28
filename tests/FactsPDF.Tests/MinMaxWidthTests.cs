using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>Real PDF geometry for bounded content/border-box min/max widths.</summary>
[TestFixture]
public sealed class MinMaxWidthTests
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
    public void MaxWidthShrinksAutoWidthAndHorizontalAutoMarginsCenterItsPaint()
    {
        const string constrained = "<p style='max-width:120pt;margin:0 auto;background:blue'>TOTAL</p>";
        const string projected = "<p style='width:120pt;margin:0 70pt;background:blue'>TOTAL</p>";
        Assert.That(Pdf(constrained), Is.EqualTo(Pdf(projected)));
        var page = Layout(constrained).Single();
        Assert.That(page.Runs.Single().X, Is.EqualTo(90).Within(.00001));
        var box = page.PaintBoxes.Single(x => x.Style.BackgroundColor.HasValue);
        Assert.That(box.X, Is.EqualTo(90).Within(.00001));
        Assert.That(box.Width, Is.EqualTo(120).Within(.00001));
    }

    [Test]
    public void MinWidthExpandsAndMaxWidthShrinksExplicitContentBoxes()
    {
        const string expanded = "<div style='width:60pt;min-width:120pt;padding:5pt;border:1pt solid blue'>A</div>";
        const string capped = "<div style='width:200pt;max-width:120pt;padding:5pt;border:1pt solid blue'>A</div>";
        const string projected = "<div style='width:120pt;padding:5pt;border:1pt solid blue'>A</div>";
        Assert.That(Pdf(expanded), Is.EqualTo(Pdf(projected)));
        Assert.That(Pdf(capped), Is.EqualTo(Pdf(projected)));
        Assert.That(Layout(capped).Single().Runs.Single().X, Is.EqualTo(26).Within(.00001));
    }

    [Test]
    public void BorderBoxConstraintsIncludePaddingAndBorders()
    {
        const string constrained = "<p style='box-sizing:border-box;width:200pt;min-width:120pt;max-width:150pt;padding:10pt;border:2pt solid red;background:blue'>SERVICE</p>";
        const string projected = "<p style='box-sizing:border-box;width:150pt;padding:10pt;border:2pt solid red;background:blue'>SERVICE</p>";
        Assert.That(Pdf(constrained), Is.EqualTo(Pdf(projected)));
        Assert.That(Layout(constrained).Single().PaintBoxes.Single(x => x.Style.BackgroundColor.HasValue).Width,
            Is.EqualTo(150).Within(.00001));
    }

    [Test]
    public void AMinimumAboveMaximumWinsAsInCssUsedWidthSizing()
    {
        const string constrained = "<p style='box-sizing:border-box;width:100pt;min-width:160pt;max-width:130pt;margin:0 auto;background:red'>APPROVED</p>";
        const string projected = "<p style='box-sizing:border-box;width:160pt;margin:0 auto;background:red'>APPROVED</p>";
        Assert.That(Pdf(constrained), Is.EqualTo(Pdf(projected)));
        Assert.That(Layout(constrained).Single().PaintBoxes.Single().X, Is.EqualTo(70).Within(.00001));
    }

    [Test]
    public void PercentageConstraintsUseParentContentWidthNotShrunkenAutoMarginSpace()
    {
        const string constrained = "<section style='width:200pt'><p style='width:170pt;max-width:50%;margin:0 auto;background:red'>BUDGET</p></section>";
        const string projected = "<section style='width:200pt'><p style='width:100pt;margin:0 50pt;background:red'>BUDGET</p></section>";
        Assert.That(Pdf(constrained), Is.EqualTo(Pdf(projected)));
        Assert.That(Layout(constrained).Single().Runs.Single().X, Is.EqualTo(70).Within(.00001));
    }

    [Test]
    public void CssWideKeywordsResetOrExplicitlyInheritNonInheritedConstraints()
    {
        const string html = "<section style='max-width:180pt;min-width:50pt'>" +
            "<p style='max-width:inherit;min-width:inherit'>A</p>" +
            "<p style='max-width:unset;min-width:initial'>B</p>" +
            "<p style='max-width:none;min-width:auto'>C</p></section>";
        var section = (BlockNode)HtmlDocumentReader.ReadTree(html, Page, default).Children.Single();
        var children = section.Children.Cast<BlockNode>().ToArray();
        Assert.That(children[0].BoxStyle.MaxWidth?.Value, Is.EqualTo(180));
        Assert.That(children[0].BoxStyle.MinWidth?.Value, Is.EqualTo(50));
        Assert.That(children[1].BoxStyle.MaxWidth, Is.Null);
        Assert.That(children[1].BoxStyle.MinWidth, Is.Null);
        Assert.That(children[2].BoxStyle.MaxWidth, Is.Null);
        Assert.That(children[2].BoxStyle.MinWidth, Is.Null);
        Assert.That(Pdf("<p style='min-width:auto;max-width:none'>B</p>"), Is.EqualTo(Pdf("<p>B</p>")));
    }

    [Test]
    public void ImportantWinnerControlsRealWidthNotOnlyParsedProperties()
    {
        const string css = "<style>p.summary{max-width:120pt!important;min-width:80pt}" +
            "p.summary{max-width:180pt}</style>";
        var actual = css + "<p class=summary style='width:200pt;max-width:190pt;margin:0 auto;background:blue'>TOTAL</p>";
        const string projected = "<p style='width:120pt;margin:0 70pt;background:blue'>TOTAL</p>";
        Assert.That(Pdf(actual), Is.EqualTo(Pdf(projected)));
    }

    [Test]
    public void WidthConstraintsPaintAndWrapIdenticallyAcrossRealPageFragments()
    {
        var settings = Page with { PageHeight = 155 };
        var lines = string.Join("<br>", Enumerable.Range(1, 9).Select(i => $"Line {i:00}"));
        var constrained = "<section style='width:200pt;max-width:120pt;padding:4pt;border:1pt solid blue;background:#f0f6fa'><p style='margin:0'>" + lines + "</p></section>";
        var projected = "<section style='width:120pt;padding:4pt;border:1pt solid blue;background:#f0f6fa'><p style='margin:0'>" + lines + "</p></section>";
        Assert.That(Pdf(constrained, settings), Is.EqualTo(Pdf(projected, settings)));
        var pages = Layout(constrained, settings);
        Assert.That(pages.Count, Is.GreaterThan(1));
        Assert.That(pages.SelectMany(x => x.Runs).Select(x => x.Text),
            Is.EqualTo(Enumerable.Range(1, 9).Select(i => $"Line {i:00}")));
        Assert.That(pages.SelectMany(x => x.PaintBoxes).Where(x => x.Style.BackgroundColor.HasValue)
            .All(x => Math.Abs(x.Width - 130) < .00001), Is.True);
    }

    [TestCase("<p style='max-width:auto'>Wrong keyword</p>", "FPDF1202")]
    [TestCase("<p style='min-width:none'>Wrong keyword</p>", "FPDF1202")]
    [TestCase("<p style='max-width:-1pt'>Negative</p>", "FPDF1202")]
    [TestCase("<p style='min-width:calc(2pt + 3pt)'>Function</p>", "FPDF1202")]
    [TestCase("<style>.never{max-width:3vw}</style><p>A</p>", "FPDF1202")]
    [TestCase("<p style='min-width:300pt'>Too wide</p>", "FPDF1302")]
    [TestCase("<p style='box-sizing:border-box;max-width:5pt;padding:10pt'>Too small</p>", "FPDF1302")]
    [TestCase("<span style='max-width:50pt'>Inline</span>", "FPDF1201")]
    [TestCase("<img src='logo' style='min-width:50pt'>", "FPDF1201")]
    [TestCase("<table style='max-width:50pt'>", "FPDF1201")]
    [TestCase("<table><colgroup><col style='width:80pt'></colgroup><tbody><tr><td><p style='max-width:50pt'>Bad cell</p></td></tr></tbody></table>", "FPDF1201")]
    public void InvalidOrInapplicableConstraintsPreserveExistingOutput(string html, string code)
        => Reject(html, code);
}
