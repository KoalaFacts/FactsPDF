using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>Horizontal panel margins affect actual report PDF boxes, not parser-only acceptance.</summary>
[TestFixture]
public sealed class ReportPanelMarginTests
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
        var settings = page ?? Page;
        return TextLayout.Layout(HtmlDocumentReader.ReadTree(html, settings, default), settings, default);
    }

    private static void Reject(string html, string code)
    {
        using var output = new MemoryStream();
        output.Write("original-pdf"u8);
        output.Position = 3;
        var error = Assert.Throws<FactsPdfException>(() =>
            PdfConverter.Convert(html, output, Page));
        Assert.That(error!.Code, Is.EqualTo(code));
        Assert.That(output.ToArray(), Is.EqualTo("original-pdf"u8.ToArray()));
        Assert.That(output.Position, Is.EqualTo(3));
    }

    [TestCase("div")]
    [TestCase("section")]
    [TestCase("article")]
    public void CentredPaintedPanelEqualsIndependentPointMarginOracle(string name)
    {
        var prefix = "<" + name + " style='width:120pt;padding:6pt;border:1pt solid blue;background:#e5f1f6;";
        var suffix = "'><p style='margin:0'>Client: Example Pty Ltd<br>Invoice SR-2026-01</p></" + name + ">";
        var actual = prefix + "margin:0 auto" + suffix;
        var oracle = prefix + "margin:0 63pt" + suffix;
        Assert.That(Pdf(actual), Is.EqualTo(Pdf(oracle)));
        var page = Layout(actual).Single();
        var painted = page.PaintBoxes.Single(b => b.Style.BackgroundColor.HasValue);
        Assert.That(painted.X, Is.EqualTo(83).Within(0.000001));
        Assert.That(painted.Width, Is.EqualTo(134).Within(0.000001));
        Assert.That(page.Runs[0].X, Is.EqualTo(90).Within(0.000001));
    }

    [Test]
    public void MaxWidthAndBorderBoxCentreActualOuterPanel()
    {
        const string style = "box-sizing:border-box;width:200pt;max-width:150pt;padding:10pt;border:2pt solid red;background:blue;";
        var actual = "<section style='" + style + "margin:0 auto'><p style='margin:0'>APPROVED</p></section>";
        var oracle = "<section style='" + style + "margin:0 55pt'><p style='margin:0'>APPROVED</p></section>";
        Assert.That(Pdf(actual), Is.EqualTo(Pdf(oracle)));
        var painted = Layout(actual).Single().PaintBoxes.Single(b => b.Style.BackgroundColor.HasValue);
        Assert.That(painted.X, Is.EqualTo(75).Within(0.000001));
        Assert.That(painted.Width, Is.EqualTo(150).Within(0.000001));
    }

    [Test]
    public void PanelRelativeSidesUseRootRemAndFinalElementEm()
    {
        const string start = "<html style='font-size:10pt'><body><div style='font-size:16pt;width:120pt;padding:3pt;background:#e5f1f6;";
        const string end = "'><p style='margin:0'>Asset clearance confirmed</p></div></body></html>";
        Assert.That(Pdf(start + "margin:0 1rem 0 2em" + end),
            Is.EqualTo(Pdf(start + "margin:0 10pt 0 32pt" + end)));
        var panel = (BlockNode)((BlockNode)HtmlDocumentReader.ReadTree(
            start + "margin:0 1rem 0 2em" + end, Page, default).Children.Single()).Children.Single();
        Assert.That(panel.BoxStyle.MarginLeft.Value, Is.EqualTo(32));
        Assert.That(panel.BoxStyle.MarginRight.Value, Is.EqualTo(10));
    }

    [Test]
    public void NestedPercentSidesUseParentContentWidth()
    {
        const string start = "<article style='width:200pt'><div style='width:120pt;background:blue;";
        const string end = "'><p style='margin:0'>SERVICE</p></div></article>";
        Assert.That(Pdf(start + "margin:0 5% 0 10%" + end),
            Is.EqualTo(Pdf(start + "margin:0 10pt 0 20pt" + end)));
        var paint = Layout(start + "margin:0 5% 0 10%" + end).Single()
            .PaintBoxes.Single(b => b.Style.BackgroundColor.HasValue);
        Assert.That(paint.X, Is.EqualTo(40).Within(0.000001));
    }

    [Test]
    public void ImportantAndInlineCascadeResolveEachHorizontalSideIndependently()
    {
        const string css = "<style>.report{margin-left:10pt!important;margin-right:5%}" +
            "section.report{margin:0 auto}</style>";
        const string tag = "<section class=report style='width:120pt;margin-left:15pt;margin-right:20pt;background:blue'>A</section>";
        const string oracle = "<section style='width:120pt;margin:0 20pt 0 10pt;background:blue'>A</section>";
        Assert.That(Pdf(css + tag), Is.EqualTo(Pdf(oracle)));
    }

    [Test]
    public void CssWideResetAndExplicitHorizontalInheritanceWorkWithoutBoxInheritanceByDefault()
    {
        const string start = "<div style='width:200pt;margin-left:16pt;margin-right:14pt'>";
        const string actual = "<section style='width:100pt;margin-left:inherit;margin-right:unset'>A</section>";
        const string oracle = "<section style='width:100pt;margin-left:16pt;margin-right:0'>A</section>";
        Assert.That(Pdf(start + actual + "</div>"), Is.EqualTo(Pdf(start + oracle + "</div>")));
        Assert.That(Pdf("<section style='margin:initial'>A</section>"),
            Is.EqualTo(Pdf("<section>A</section>")));
    }

    [Test]
    public void CentredDecoratedReportContinuesWithSameWidthAndXAcrossPages()
    {
        var page = Page with { PageHeight = 150 };
        var rows = string.Join("<br>", Enumerable.Range(1, 18).Select(i => "Inspection " + i.ToString("00")));
        const string prefix = "<section style='width:110pt;padding:5pt;border:2pt solid red;background:#e5f1f6;";
        var suffix = "'><p style='margin:0'>" + rows + "</p></section>";
        var actual = prefix + "margin:0 auto" + suffix;
        var oracle = prefix + "margin:0 68pt" + suffix;
        Assert.That(Pdf(actual, page), Is.EqualTo(Pdf(oracle, page)));
        var pages = Layout(actual, page);
        Assert.That(pages.Count, Is.GreaterThan(1));
        Assert.That(pages.SelectMany(p => p.Runs).Count(), Is.EqualTo(18));
        var panels = pages.SelectMany(p => p.PaintBoxes).Where(b => b.Style.BackgroundColor.HasValue).ToArray();
        Assert.That(panels.Length, Is.EqualTo(pages.Count));
        Assert.That(panels.All(b => Math.Abs(b.X - 88) < 0.000001 && Math.Abs(b.Width - 124) < 0.000001), Is.True);
    }

    [Test]
    public void ZeroPanelMarginDoesNotChangeExistingPdfBytes()
    {
        const string body = "<div style='padding:3pt;background:blue'><p style='margin:0'>Unchanged</p></div>";
        const string explicitZero = "<div style='margin:0;padding:3pt;background:blue'><p style='margin:0'>Unchanged</p></div>";
        Assert.That(Pdf(explicitZero), Is.EqualTo(Pdf(body)));
    }

    [TestCase("<section style='margin:8pt auto'>A</section>", "FPDF1201")]
    [TestCase("<article style='margin:0 auto 2pt'>A</article>", "FPDF1201")]
    [TestCase("<div style='margin-top:inherit'>A</div>", "FPDF1201")]
    [TestCase("<style>.panel{margin-top:4pt;margin-top:0}</style><div class=panel>A</div>", "FPDF1201")]
    [TestCase("<body style='margin:0 auto'>A</body>", "FPDF1201")]
    [TestCase("<ol style='margin-left:5pt'><li>A</li></ol>", "FPDF1201")]
    [TestCase("<span style='margin-left:2pt'>A</span>", "FPDF1201")]
    [TestCase("<div style='margin-left:-2pt'>A</div>", "FPDF1202")]
    [TestCase("<div style='margin-left:calc(2pt + 3pt)'>A</div>", "FPDF1202")]
    [TestCase("<div style='width:240pt;margin-left:30pt'>A</div>", "FPDF1302")]
    public void InvalidOrOutOfScopePanelMarginsPreserveOriginalDestination(string html, string code)
        => Reject(html, code);
}
