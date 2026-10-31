using System.Text;
using FactsPDF.CommandLine;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>LTR-only logical report spacing mapped to real physical PDF box geometry.</summary>
[TestFixture]
public sealed class LogicalReportSpacingTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 175, Margin = 20 };

    private static byte[] Render(string html, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, options ?? Page);
        return output.ToArray();
    }

    private static List<LayoutPage> Layout(string html)
        => TextLayout.Layout(HtmlDocumentReader.ReadTree(html, Page, default), Page, default);

    private static void Reject(string html, string code)
    {
        using var output = new MemoryStream();
        output.Write("KEEP ORIGINAL"u8);
        output.Position = 4;
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, Page));
        Assert.That(error!.Code, Is.EqualTo(code));
        Assert.That(output.ToArray(), Is.EqualTo("KEEP ORIGINAL"u8.ToArray()));
        Assert.That(output.Position, Is.EqualTo(4));
    }

    [Test]
    public void PhysicalAndLogicalPaddingAndMarginsPaintExactlyTheSameReport()
    {
        var rows = string.Join("", Enumerable.Range(1, 32)
            .Select(i => $"<p style='margin:0'>Service line {i:00}</p>"));
        var logical = "<style>.card{width:144pt;box-sizing:border-box;" +
            "margin-inline:auto;margin-block:7pt 9pt;" +
            "padding-inline:8pt 12pt;padding-block:4pt 6pt;" +
            "border:2pt solid #23446b;background:#e5eff5}</style>" +
            "<section class=card>" + rows + "</section><p style='margin:0'>TOTAL</p>";
        var physical = "<style>.card{width:144pt;box-sizing:border-box;" +
            "margin-left:auto;margin-right:auto;margin-top:7pt;margin-bottom:9pt;" +
            "padding-left:8pt;padding-right:12pt;padding-top:4pt;padding-bottom:6pt;" +
            "border:2pt solid #23446b;background:#e5eff5}</style>" +
            "<section class=card>" + rows + "</section><p style='margin:0'>TOTAL</p>";
        Assert.That(Render(logical), Is.EqualTo(Render(physical)));
        var pages = Layout(logical);
        Assert.That(pages, Has.Count.GreaterThan(1));
        Assert.That(pages.SelectMany(p => p.Runs).Count(r => r.Text.StartsWith("Service line", StringComparison.Ordinal)), Is.EqualTo(32));
        Assert.That(pages.SelectMany(p => p.Runs).Last().Text, Is.EqualTo("TOTAL"));
        Assert.That(pages.SelectMany(p => p.PaintBoxes).Any(b => b.Style.BackgroundColor.HasValue), Is.True);
    }

    [Test]
    public void SingleAndDoubleLogicalValuesRespectTheirPhysicalSidesAndPdfPositions()
    {
        const string logical = "<article style='width:120pt;margin-inline:15pt 10pt;" +
            "padding-inline:5pt 11pt;padding-block:3pt;background:#abcdef'>" +
            "<p style='margin-block:6pt 4pt;margin-inline:2pt 3pt'>CHECK</p></article>";
        const string physical = "<article style='width:120pt;margin-left:15pt;margin-right:10pt;" +
            "padding-left:5pt;padding-right:11pt;padding-top:3pt;padding-bottom:3pt;background:#abcdef'>" +
            "<p style='margin-top:6pt;margin-bottom:4pt;margin-left:2pt;margin-right:3pt'>CHECK</p></article>";
        Assert.That(Render(logical), Is.EqualTo(Render(physical)));
        var layout = Layout(logical).Single();
        Assert.That(layout.PaintBoxes.First(b => b.Style.BackgroundColor.HasValue).X, Is.EqualTo(35).Within(0.000001));
        Assert.That(layout.Runs.Single(x => x.Text == "CHECK").X, Is.EqualTo(42).Within(0.000001));
    }

    [Test]
    public void ScopedStylesheetCascadeImportantAndWideKeywordsMatchLonghands()
    {
        const string before = "<style>.card{padding-inline:4pt 7pt!important;" +
            "margin-inline:12pt auto;margin-block:6pt 9pt}" +
            "section.card{margin-block:initial;margin-block:8pt 3pt}</style>" +
            "<section class=card style='width:100pt;padding-inline:15pt;" +
            "margin-inline:0 auto'><p style='margin-block:0'>LINE</p></section>";
        const string after = "<style>.card{padding-left:4pt!important;padding-right:7pt!important;" +
            "margin-left:12pt;margin-right:auto;margin-top:6pt;margin-bottom:9pt}" +
            "section.card{margin-top:initial;margin-bottom:initial;margin-top:8pt;margin-bottom:3pt}</style>" +
            "<section class=card style='width:100pt;padding-left:15pt;padding-right:15pt;" +
            "margin-left:0;margin-right:auto'><p style='margin-top:0;margin-bottom:0'>LINE</p></section>";
        Assert.That(Render(before), Is.EqualTo(Render(after)));
        Assert.That(Render("<p style='margin-block:unset;padding-inline:initial'>A</p>"),
            Is.EqualTo(Render("<p style='margin-top:unset;margin-bottom:unset;" +
                "padding-left:initial;padding-right:initial'>A</p>")));
    }

    [Test]
    public void RelativeSpacingUsesComputedFontAndRootAndRetainsStrictBoxLayout()
    {
        const string logical = "<html style='font-size:10pt'><body>" +
            "<div style='font-size:20pt;width:160pt;padding-inline:0.25em 0.5rem;" +
            "padding-block:0.2em;margin-inline:auto;margin-block:5pt'>" +
            "<p style='margin-block:0.25em 0.4rem;margin-inline:0'>BUDGET</p></div></body></html>";
        const string physical = "<html style='font-size:10pt'><body>" +
            "<div style='font-size:20pt;width:160pt;padding-left:5pt;padding-right:5pt;" +
            "padding-top:4pt;padding-bottom:4pt;margin-left:auto;margin-right:auto;margin-top:5pt;margin-bottom:5pt'>" +
            "<p style='margin-top:5pt;margin-bottom:4pt;margin-left:0;margin-right:0'>BUDGET</p></div></body></html>";
        Assert.That(Render(logical), Is.EqualTo(Render(physical)));
    }

    [Test]
    public void SourceCliUsesSameReportConverterForLogicalSpacing()
    {
        const string html = "<style>p{margin-block:3pt 2pt;padding-inline:1pt 2pt}" +
            "div{width:140pt;margin-inline:auto;padding-block:4pt}</style>" +
            "<div><p>Business summary</p></div>";
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(html));
        using var output = new MemoryStream();
        using var diagnostics = new StringWriter();
        Assert.That(CliApplication.Run(["-", "-"], input, output, diagnostics), Is.Zero, diagnostics.ToString());
        using var expected = new MemoryStream();
        PdfConverter.Convert(html, expected);
        Assert.That(output.ToArray(), Is.EqualTo(expected.ToArray()));
    }

    [TestCase("<p style='margin-block:0 auto'>A</p>", "FPDF1202")]
    [TestCase("<p style='margin-inline:-1pt'>A</p>", "FPDF1202")]
    [TestCase("<section style='margin-block:5%'>A</section>", "FPDF1202")]
    [TestCase("<section style='padding-block:5pt -2pt'>A</section>", "FPDF1202")]
    [TestCase("<section style='padding-inline:calc(2pt + 3pt)'>A</section>", "FPDF1202")]
    [TestCase("<p style='margin-inline:inherit 0'>A</p>", "FPDF1202")]
    [TestCase("<p style='padding-block:1pt 2pt 3pt'>A</p>", "FPDF1202")]
    [TestCase("<body style='margin-inline:5pt'>A</body>", "FPDF1201")]
    [TestCase("<span style='padding-inline:5pt'>A</span>", "FPDF1201")]
    [TestCase("<style>.never{margin-inline:-1pt}</style><p>A</p>", "FPDF1202")]
    [TestCase("<p style='width:280pt;margin-inline:20pt 20pt'>A</p>", "FPDF1302")]
    public void InvalidOrUnsupportedValuesPreservePriorOutput(string html, string code) => Reject(html, code);

    [Test]
    public void ExpandedLogicalShorthandsCountEachSideAgainstCssBudget()
    {
        using var output = new MemoryStream();
        output.Write("ORIGINAL"u8);
        var exception = Assert.Throws<FactsPdfException>(() =>
            PdfConverter.Convert("<p style='padding-inline:2pt'>A</p>", output,
                Page with { MaxCssDeclarations = 1 }));
        Assert.That(exception!.Code, Is.EqualTo("FPDF1205"));
        Assert.That(output.ToArray(), Is.EqualTo("ORIGINAL"u8.ToArray()));
    }
}
