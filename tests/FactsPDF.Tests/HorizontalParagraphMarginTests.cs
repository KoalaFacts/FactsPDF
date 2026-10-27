using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>Real paragraph/heading inline-margin layout, not just declaration acceptance.</summary>
[TestFixture]
public sealed class HorizontalParagraphMarginTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 500, Margin = 20 };

    private static byte[] Pdf(string html, PdfOptions? page = null)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, page ?? Page);
        return output.ToArray();
    }

    private static List<LayoutPage> Layout(string html, PdfOptions? page = null)
    {
        var options = page ?? Page;
        return TextLayout.Layout(HtmlDocumentReader.ReadTree(html, options, default), options, default);
    }

    private static FactsPdfException Reject(string html, string code)
    {
        using var output = new MemoryStream();
        output.Write("original-pdf"u8);
        output.Position = 3;
        var failure = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, Page));
        Assert.That(failure!.Code, Is.EqualTo(code));
        Assert.That(output.ToArray(), Is.EqualTo("original-pdf"u8.ToArray()));
        Assert.That(output.Position, Is.EqualTo(3));
        return failure;
    }

    [TestCase("margin:5pt", "margin-top:5pt;margin-right:5pt;margin-bottom:5pt;margin-left:5pt")]
    [TestCase("margin:5pt 8pt", "margin-top:5pt;margin-right:8pt;margin-bottom:5pt;margin-left:8pt")]
    [TestCase("margin:5pt 8pt 6pt", "margin-top:5pt;margin-right:8pt;margin-bottom:6pt;margin-left:8pt")]
    [TestCase("margin:5pt 8pt 6pt 10pt", "margin-top:5pt;margin-right:8pt;margin-bottom:6pt;margin-left:10pt")]
    [TestCase("margin:2mm 10% 3mm 4mm", "margin-top:2mm;margin-right:10%;margin-bottom:3mm;margin-left:4mm")]
    public void ShorthandMatchesIndependentlyAuthoredLonghandsInActualPdf(string shorthand, string longhands)
    {
        const string start = "<div style='width:180pt;padding:5pt;background:#f0f6fa'><h2 style='";
        const string end = "'>Equipment inspection</h2><p style='margin:0 4pt 3pt'>Checked all equipment.</p></div>";
        Assert.That(Pdf(start + shorthand + end), Is.EqualTo(Pdf(start + longhands + end)));
    }

    [Test]
    public void MarginsReduceAvailableWidthAndMoveTextAndPaintedBox()
    {
        const string html = "<p style='margin-left:20pt;margin-right:30pt;background:#e0e9f1;text-align:right'>AB</p>" +
            "<p style='margin:0'>C</p>";
        var page = Layout(html).Single();
        Assert.That(page.Runs.Select(x => x.Text), Is.EqualTo(new[] { "AB", "C" }));
        Assert.That(page.Runs[0].X, Is.EqualTo(20 + 20 + (260 - 50) - 14.4).Within(0.001));
        Assert.That(page.Runs[1].X, Is.EqualTo(20).Within(0.001));
        var paint = page.PaintBoxes.Single(x => x.Style.BackgroundColor.HasValue);
        Assert.That(paint.X, Is.EqualTo(40).Within(0.001));
        Assert.That(paint.Width, Is.EqualTo(210).Within(0.001));
    }

    [Test]
    public void PercentSidesUseContainingWidthAndStylesheetCascadeHonoursImportant()
    {
        const string html = "<style>.note{margin-left:10%;margin-right:5%!important}" +
            "p.note{margin:0 12pt 0 8pt}</style><p class=note style='margin-left:9pt;margin-right:14pt'>A</p>";
        var tree = HtmlDocumentReader.ReadTree(html, Page, default);
        var paragraph = (BlockNode)tree.Children.Single();
        Assert.That(paragraph.BoxStyle.MarginLeft.Value, Is.EqualTo(9));
        Assert.That(paragraph.BoxStyle.MarginRight.Value, Is.EqualTo(5));
        Assert.That(paragraph.BoxStyle.MarginRight.IsPercent, Is.True);
        Assert.That(Layout(html).Single().Runs.Single().X, Is.EqualTo(29).Within(0.001));
    }

    [Test]
    public void MarginPercentAndPrintLengthRemainEquivalentToPointGeometry()
    {
        const string length = "<div style='width:200pt'><p style='margin:0 10% 0 0.25in'>REPORT</p></div>";
        const string points = "<div style='width:200pt'><p style='margin-top:0;margin-right:20pt;margin-bottom:0;margin-left:18pt'>REPORT</p></div>";
        Assert.That(Pdf(length), Is.EqualTo(Pdf(points)));
    }

    [Test]
    public void HorizontalMarginsChangeActualWrappingAndRetainEveryWord()
    {
        const string words = "alpha beta gamma delta epsilon zeta eta theta";
        var full = Layout("<div style='width:120pt'><p>" + words + "</p></div>").Single();
        var inset = Layout("<div style='width:120pt'><p style='margin-left:25pt;margin-right:25pt'>" + words + "</p></div>").Single();
        Assert.That(inset.Runs.Count, Is.GreaterThan(full.Runs.Count));
        Assert.That(string.Concat(inset.Runs.Select(x => x.Text)).Replace(" ", ""),
            Is.EqualTo(words.Replace(" ", "")));
        Assert.That(inset.Runs[0].X, Is.EqualTo(45).Within(0.001));
    }

    [Test]
    public void MarginsPersistThroughPageBreaksWithoutMovingSubsequentSiblings()
    {
        const string html = "<p style='margin:0 0 3pt 20pt;background:red'>FIRST</p>" +
            "<p style='break-before:page;margin-left:25pt;margin-right:10pt;background:blue'>SECOND</p>" +
            "<p style='margin:0'>THIRD</p>";
        var pages = Layout(html, Page with { PageHeight = 110 });
        Assert.That(pages, Has.Count.EqualTo(2));
        Assert.That(pages[0].Runs.Single().X, Is.EqualTo(40).Within(0.001));
        Assert.That(pages[1].Runs[0].X, Is.EqualTo(45).Within(0.001));
        Assert.That(pages[1].Runs[1].X, Is.EqualTo(20).Within(0.001));
        Assert.That(pages[1].PaintBoxes.Single(x => x.Style.BackgroundColor == new Rgb(0, 0, 1)).X,
            Is.EqualTo(45).Within(0.001));
    }

    [Test]
    public void HeadingKeepMeasuresInsetWidthBeforeChoosingThePage()
    {
        const string common = "<style>p,h2{font-size:10pt;line-height:2;margin:0;font-weight:normal}</style>";
        var small = Page with { PageWidth = 240, PageHeight = 100, Margin = 10 };
        const string report = "<p>BEFORE<br>BEFORE</p><h2 style='break-after:avoid;margin-left:20pt;margin-right:20pt'>HEADING</h2>" +
            "<p style='margin-left:35pt;margin-right:35pt'>ALPHA BETA GAMMA DELTA</p>";
        var pages = Layout(common + report, small);
        Assert.That(pages.Count, Is.GreaterThanOrEqualTo(2));
        var lines = pages.Select(p => string.Join(",", p.Runs.Select(r => r.Text))).ToArray();
        Assert.That(lines[0], Does.Not.Contain("HEADING"));
        Assert.That(lines[1], Does.Contain("HEADING"));
    }

    [TestCase("margin-left:-1pt", "FPDF1202")]
    [TestCase("margin-right:auto", "FPDF1202")]
    [TestCase("margin:10% 0", "FPDF1202")]
    [TestCase("margin-left:calc(10% - 1pt)", "FPDF1202")]
    [TestCase("margin:1pt 2pt 3pt 4pt 5pt", "FPDF1202")]
    public void UnsupportedValuesFailWithoutChangingAnExistingDestination(string css, string code)
        => Reject("<style>.unused{" + css + "}</style><p style='" + css + "'>A</p>", code);

    [Test]
    public void OverflowAndExplicitWidthCannotEscapeContainingRectangle()
    {
        Reject("<div style='width:100pt'><p style='margin-left:80pt;margin-right:30pt'>A</p></div>", "FPDF1302");
        Reject("<div style='width:100pt'><p style='width:100%;margin-left:5pt'>A</p></div>", "FPDF1302");
    }

    [Test]
    public void NonParagraphMarginsStillFailWithoutFallback()
        => Reject("<section style='margin-left:2pt'>A</section>", "FPDF1201");

    [Test]
    public void OldZeroHorizontalShorthandRetainsItsExactPdfBytes()
    {
        const string old = "<p style='margin:2mm 0 3mm'>Maintenance report</p>";
        const string longhands = "<p style='margin-top:2mm;margin-bottom:3mm'>Maintenance report</p>";
        Assert.That(Pdf(old), Is.EqualTo(Pdf(longhands)));
    }
}
