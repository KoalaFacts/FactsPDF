using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>Scoped root-relative paragraph/heading margins must change actual PDF geometry.</summary>
[TestFixture]
public sealed class RootRelativeMarginTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 120, Margin = 20 };

    private static byte[] Render(string html, PdfOptions? settings = null)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, settings ?? Page);
        return output.ToArray();
    }

    private static List<LayoutPage> Pages(string html, PdfOptions? settings = null)
    {
        var options = settings ?? Page;
        return TextLayout.Layout(HtmlDocumentReader.ReadTree(html, options, default), options, default);
    }

    private static void Reject(string html, string code)
    {
        using var output = new MemoryStream();
        output.Write("existing-report"u8);
        output.Position = 4;
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, Page));
        Assert.That(error!.Code, Is.EqualTo(code));
        Assert.That(output.ToArray(), Is.EqualTo("existing-report"u8.ToArray()));
        Assert.That(output.Position, Is.EqualTo(4));
    }

    [TestCase("margin:0.5rem", "margin:7.5pt")]
    [TestCase("margin:0.5rem 1rem", "margin:7.5pt 15pt")]
    [TestCase("margin:0.25rem 0.5rem 0.75rem", "margin:3.75pt 7.5pt 11.25pt")]
    [TestCase("margin:0.25rem 0.5rem 0.75rem 1rem", "margin:3.75pt 7.5pt 11.25pt 15pt")]
    [TestCase("margin:0.5rem auto", "margin:7.5pt auto")]
    [TestCase("margin-left:1rem;margin-right:0.5rem;margin-top:0.25rem;margin-bottom:0.75rem",
        "margin-left:15pt;margin-right:7.5pt;margin-top:3.75pt;margin-bottom:11.25pt")]
    public void AllFourShorthandAndLonghandSidesMatchIndependentlySpecifiedPointLengths(
        string relative, string absolute)
    {
        const string open = "<html style='font-size:15pt'><body style='font-size:9pt'>" +
            "<h2 style='font-size:12pt;width:100pt;background:#f0f6fa;border:1pt solid blue;";
        const string close = "'>SERVICE</h2><p>Following section</p></body></html>";
        Assert.That(Render(open + relative + close), Is.EqualTo(Render(open + absolute + close)));
    }

    [Test]
    public void RootRatherThanBodyControlsMarginGeometryAndTextWrap()
    {
        const string relative = "<html style='font-size:15pt'><body style='font-size:9pt'>" +
            "<p style='width:120pt;margin:1rem 0.5rem 0.25rem 2rem;background:#e0e9f1'>" +
            "Service entry complete</p></body></html>";
        const string points = "<html style='font-size:15pt'><body style='font-size:9pt'>" +
            "<p style='width:120pt;margin:15pt 7.5pt 3.75pt 30pt;background:#e0e9f1'>" +
            "Service entry complete</p></body></html>";
        Assert.That(Render(relative), Is.EqualTo(Render(points)));
        var layout = Pages(relative).Single();
        var painted = layout.PaintBoxes.Single(x => x.Style.BackgroundColor.HasValue);
        Assert.That(painted.X, Is.EqualTo(50).Within(0.00001));
        Assert.That(painted.Width, Is.EqualTo(120).Within(0.00001));
        Assert.That(layout.Runs[0].X, Is.EqualTo(50).Within(0.00001));
    }

    [Test]
    public void AConfiguredInitialRootAndHtmlSelfReferenceResolveMargins()
    {
        const string direct = "<body style='font-size:9pt'><p style='margin:0.5rem 1rem'>Report</p></body>";
        const string points = "<body style='font-size:9pt'><p style='margin:8pt 16pt'>Report</p></body>";
        var options = Page with { FontSize = 16 };
        Assert.That(Render(direct, options), Is.EqualTo(Render(points, options)));
        const string self = "<html style='font-size:2rem'><body style='font-size:9pt'>" +
            "<p style='margin:0.5rem 1rem'>Report</p></body></html>";
        const string selfPoints = "<html style='font-size:24pt'><body style='font-size:9pt'>" +
            "<p style='margin:12pt 24pt'>Report</p></body></html>";
        Assert.That(Render(self), Is.EqualTo(Render(selfPoints)));
    }

    [Test]
    public void IndividualCascadeWinnersAndExplicitResetsAreComputedAfterMatching()
    {
        const string css = "<html style='font-size:15pt'><style>" +
            "p.audit{margin:1rem 0.5rem!important;margin-left:2rem}" +
            "p.audit{margin-top:0.25rem!important}</style><body>";
        const string point = "<html style='font-size:15pt'><style>" +
            "p.audit{margin:15pt 7.5pt!important;margin-left:30pt}" +
            "p.audit{margin-top:3.75pt!important}</style><body>";
        const string suffix = "<p class=audit style='margin-left:initial!important;margin-top:2rem'>A</p>" +
            "<p style='margin:0'>B</p></body></html>";
        Assert.That(Render(css + suffix), Is.EqualTo(Render(point + suffix)));
        var paragraph = Pages(css + suffix).Single().Runs;
        Assert.That(paragraph[0].X, Is.EqualTo(20).Within(0.00001));
        Assert.That(paragraph[1].X, Is.EqualTo(20).Within(0.00001));
    }

    [Test]
    public void RepeatedReportRowsKeepPaintAndMarginsAcrossActualPages()
    {
        var rows = string.Concat(Enumerable.Range(1, 18).Select(i =>
            $"<p style='margin:0.2rem 0.5rem 0.3rem;background:#f0f6fa'>Service entry {i:00}</p>"));
        var pointRows = string.Concat(Enumerable.Range(1, 18).Select(i =>
            $"<p style='margin:3pt 7.5pt 4.5pt;background:#f0f6fa'>Service entry {i:00}</p>"));
        const string before = "<html style='font-size:15pt'><body style='font-size:9pt'>";
        const string after = "</body></html>";
        var actual = before + rows + after;
        Assert.That(Render(actual), Is.EqualTo(Render(before + pointRows + after)));
        var pages = Pages(actual);
        Assert.That(pages.Count, Is.GreaterThan(1));
        Assert.That(pages.SelectMany(p => p.Runs).Select(r => r.Text),
            Is.EqualTo(Enumerable.Range(1, 18).Select(i => $"Service entry {i:00}")));
        Assert.That(pages.SelectMany(p => p.PaintBoxes)
            .Where(p => p.Style.BackgroundColor.HasValue)
            .All(p => Math.Abs(p.X - 27.5) < 0.00001), Is.True);
    }

    [TestCase("<p style='margin:-1rem'>A</p>", "FPDF1202")]
    [TestCase("<p style='margin-top:10000rem'>A</p>", "FPDF1202")]
    [TestCase("<html style='font-size:144pt'><body><p style='margin:101rem'>A</p></body></html>", "FPDF1202")]
    [TestCase("<style>.unused{margin-top:-1rem}</style><p>A</p>", "FPDF1202")]
    [TestCase("<p style='margin-top:1vh'>A</p>", "FPDF1202")]
    [TestCase("<p style='margin-top:10%'>A</p>", "FPDF1202")]
    [TestCase("<p style='margin:1rem auto auto'>A</p>", "FPDF1202")]
    [TestCase("<p style='margin:calc(1rem + 1pt)'>A</p>", "FPDF1202")]
    [TestCase("<div style='margin-left:1rem'>A</div>", "FPDF1201")]
    [TestCase("<table><colgroup><col style='width:100pt'></colgroup><tbody><tr><td><p style='margin-left:1rem'>A</p></td></tr></tbody></table>", "FPDF1201")]
    [TestCase("<table><colgroup><col style='width:100pt'></colgroup><tbody><tr><td><p style='margin-top:1rem'>A</p></td></tr></tbody></table>", "FPDF1201")]
    [TestCase("<p style='width:100pt;margin:0 50rem'>A</p>", "FPDF1302")]
    public void InvalidAndUnrenderableMarginsPreserveOriginalOutput(string html, string code)
        => Reject(html, code);
}
