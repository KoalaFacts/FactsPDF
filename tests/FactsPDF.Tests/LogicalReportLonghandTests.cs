using System.Text;
using FactsPDF.CommandLine;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>Physical rendering witness for LTR logical *side* properties.</summary>
[TestFixture]
public sealed class LogicalReportLonghandTests
{
    private static readonly PdfOptions Page = new()
    {
        PageWidth = 300, PageHeight = 175, Margin = 20
    };

    private static byte[] Render(string html, PdfOptions? options = null)
    {
        using var stream = new MemoryStream();
        PdfConverter.Convert(html, stream, options ?? Page);
        return stream.ToArray();
    }

    private static List<LayoutPage> Layout(string html)
        => TextLayout.Layout(HtmlDocumentReader.ReadTree(html, Page, default), Page, default);

    private static void Reject(string html, string code)
    {
        using var output = new MemoryStream();
        output.Write("ORIGINAL REPORT"u8);
        output.Position = 5;
        var error = Assert.Throws<FactsPdfException>(() =>
            PdfConverter.Convert(html, output, Page));
        Assert.That(error!.Code, Is.EqualTo(code));
        Assert.That(output.ToArray(), Is.EqualTo("ORIGINAL REPORT"u8.ToArray()));
        Assert.That(output.Position, Is.EqualTo(5));
    }

    [Test]
    public void AllEightLogicalSidePropertiesRenderTheirRealPhysicalCounterparts()
    {
        const string logical = "<article style='width:120pt;box-sizing:border-box;" +
            "margin-inline-start:15pt;margin-inline-end:10pt;" +
            "padding-inline-start:5pt;padding-inline-end:11pt;" +
            "padding-block-start:3pt;padding-block-end:3pt;background:#abcdef'>" +
            "<p style='margin-block-start:6pt;margin-block-end:4pt;" +
            "margin-inline-start:2pt;margin-inline-end:3pt'>CHECK</p></article>";
        const string physical = "<article style='width:120pt;box-sizing:border-box;" +
            "margin-left:15pt;margin-right:10pt;" +
            "padding-left:5pt;padding-right:11pt;" +
            "padding-top:3pt;padding-bottom:3pt;background:#abcdef'>" +
            "<p style='margin-top:6pt;margin-bottom:4pt;" +
            "margin-left:2pt;margin-right:3pt'>CHECK</p></article>";
        Assert.That(Render(logical), Is.EqualTo(Render(physical)));
        var layout = Layout(logical).Single();
        Assert.That(layout.PaintBoxes.First(b => b.Style.BackgroundColor.HasValue).X,
            Is.EqualTo(35).Within(0.000001));
        Assert.That(layout.Runs.Single(x => x.Text == "CHECK").X,
            Is.EqualTo(42).Within(0.000001));
    }

    [Test]
    public void IndividualSidesInterleaveWithPhysicalAndLogicalShorthandCascade()
    {
        const string logical = "<style>.card{width:125pt;" +
            "padding-inline:3pt 4pt;padding-inline-start:6pt!important;" +
            "margin-inline:0 auto;margin-inline-end:10pt;" +
            "margin-block:2pt 4pt;margin-block-end:6pt}" +
            "section.card{padding-left:2pt;margin-right:auto}" +
            "</style><section class=card style='padding-inline-start:12pt;" +
            "margin-block-start:5pt;margin-inline-end:auto'>" +
            "<p style='margin:0'>CASCADE</p></section>";
        const string physical = "<style>.card{width:125pt;" +
            "padding-left:3pt;padding-right:4pt;padding-left:6pt!important;" +
            "margin-left:0;margin-right:auto;margin-right:10pt;" +
            "margin-top:2pt;margin-bottom:4pt;margin-bottom:6pt}" +
            "section.card{padding-left:2pt;margin-right:auto}" +
            "</style><section class=card style='padding-left:12pt;" +
            "margin-top:5pt;margin-right:auto'>" +
            "<p style='margin:0'>CASCADE</p></section>";
        Assert.That(Render(logical), Is.EqualTo(Render(physical)));
    }

    [Test]
    public void CssWideKeywordsResetOrInheritIndividualLogicalSides()
    {
        const string logical = "<section style='padding-left:7pt;padding-top:9pt'>" +
            "<blockquote style='margin-inline-start:initial;margin-block-end:unset;" +
            "padding-inline-start:inherit;padding-block-start:inherit;" +
            "padding-inline-end:2pt'><p style='margin:0'>INHERITED</p></blockquote>" +
            "</section>";
        const string physical = "<section style='padding-left:7pt;padding-top:9pt'>" +
            "<blockquote style='margin-left:initial;margin-bottom:unset;" +
            "padding-left:inherit;padding-top:inherit;" +
            "padding-right:2pt'><p style='margin:0'>INHERITED</p></blockquote>" +
            "</section>";
        Assert.That(Render(logical), Is.EqualTo(Render(physical)));
    }

    [Test]
    public void FontRelativeLogicalSidesUseFinalElementAndHtmlRootSizes()
    {
        const string logical = "<html style='font-size:10pt'><body>" +
            "<section style='font-size:20pt;width:150pt;" +
            "padding-inline-start:0.25em;padding-inline-end:0.5rem;" +
            "padding-block-start:0.2em;margin-inline-start:auto;" +
            "margin-inline-end:auto;margin-block-end:0.3em'>" +
            "<p style='margin-block-start:0.5rem;margin-block-end:0.25em'>" +
            "BUDGET</p></section></body></html>";
        const string physical = "<html style='font-size:10pt'><body>" +
            "<section style='font-size:20pt;width:150pt;" +
            "padding-left:5pt;padding-right:5pt;" +
            "padding-top:4pt;margin-left:auto;" +
            "margin-right:auto;margin-bottom:6pt'>" +
            "<p style='margin-top:5pt;margin-bottom:5pt'>" +
            "BUDGET</p></section></body></html>";
        Assert.That(Render(logical), Is.EqualTo(Render(physical)));
    }

    [Test]
    public void PaintedLonghandReportContinuesAcrossPagesAndRetainsAllText()
    {
        var rows = string.Join("", Enumerable.Range(1, 40)
            .Select(i => $"<p style='margin:0'>Service reference {i:00}</p>"));
        var source = "<section style='width:165pt;margin-inline-start:auto;" +
            "margin-inline-end:auto;padding-inline-start:8pt;" +
            "padding-inline-end:12pt;padding-block-start:4pt;" +
            "padding-block-end:6pt;border:2pt solid #23446b;" +
            "background:#edf4f8'>" + rows +
            "</section><p style='margin:0'>COMPLETE</p>";
        var physical = "<section style='width:165pt;margin-left:auto;" +
            "margin-right:auto;padding-left:8pt;" +
            "padding-right:12pt;padding-top:4pt;" +
            "padding-bottom:6pt;border:2pt solid #23446b;" +
            "background:#edf4f8'>" + rows +
            "</section><p style='margin:0'>COMPLETE</p>";
        Assert.That(Render(source), Is.EqualTo(Render(physical)));
        var layout = Layout(source);
        Assert.That(layout.Count, Is.GreaterThan(1));
        Assert.That(layout.SelectMany(x => x.Runs)
            .Count(x => x.Text.StartsWith("Service reference", StringComparison.Ordinal)),
            Is.EqualTo(40));
        Assert.That(layout.SelectMany(x => x.Runs).Last().Text, Is.EqualTo("COMPLETE"));
        Assert.That(layout.SelectMany(x => x.PaintBoxes)
            .Any(x => x.Style.BackgroundColor.HasValue), Is.True);
    }

    [TestCase("margin-inline-start:-0e999", "margin-left:0")]
    [TestCase("margin-inline-end:+0.000", "margin-right:0")]
    [TestCase("margin-block-start:0e-999", "margin-top:0")]
    [TestCase("margin-block-end:-0.00", "margin-bottom:0")]
    [TestCase("padding-inline-start:+0e15", "padding-left:0")]
    [TestCase("padding-inline-end:0.000", "padding-right:0")]
    [TestCase("padding-block-start:-0", "padding-top:0")]
    [TestCase("padding-block-end:0e999", "padding-bottom:0")]
    public void ExactNumericZeroIsEquivalentToPhysicalZero(string logical, string physical)
    {
        Assert.That(Render("<section style='" + logical + "'><p>ZERO</p></section>"),
            Is.EqualTo(Render("<section style='" + physical + "'><p>ZERO</p></section>")));
    }

    [TestCase("margin-inline-start:1e-999", "FPDF1202")]
    [TestCase("margin-inline-end:-1e-999", "FPDF1202")]
    [TestCase("padding-block-end:1e-999", "FPDF1202")]
    [TestCase("padding-inline-start:-1pt", "FPDF1202")]
    [TestCase("padding-inline-end:auto", "FPDF1202")]
    [TestCase("margin-block-start:auto", "FPDF1202")]
    [TestCase("margin-block-end:5%", "FPDF1202")]
    [TestCase("padding-block-start:calc(1pt + 2pt)", "FPDF1202")]
    [TestCase("padding-inline-start:1pt 2pt", "FPDF1202")]
    [TestCase("margin-inline-end:inherit 0", "FPDF1202")]
    [TestCase("margin-inline-start:99999pt", "FPDF1202")]
    [TestCase("padding-inline-start:1e-999pt", "FPDF1202")]
    [TestCase("margin-inline-start:1e-999px", "FPDF1202")]
    [TestCase("margin-left:1e-999pt", "FPDF1202")]
    [TestCase("padding-top:1e-999px", "FPDF1202")]
    public void UnsupportedOrNonzeroUnderflowInputCannotOverwritePriorPdf(string css, string code)
        => Reject("<section style='" + css + "'><p>NO</p></section>", code);

    [Test]
    public void InvalidUnmatchedRuleAndUnsupportedElementsFailBeforeOutput()
    {
        Reject("<style>.absent{padding-inline-start:calc(1pt + 2pt)}</style><p>A</p>", "FPDF1202");
        Reject("<body style='margin-inline-start:4pt'><p>A</p></body>", "FPDF1201");
        Reject("<span style='padding-block-end:2pt'>A</span>", "FPDF1201");
        Reject("<p style='width:250pt;margin-inline-start:40pt;" +
            "margin-inline-end:40pt'>OVERWIDE</p>", "FPDF1302");
    }

    [Test]
    public void SingleLogicalSideChargesOneDeclarationAgainstBudget()
    {
        Assert.DoesNotThrow(() => Render(
            "<p style='padding-inline-start:1pt'>BUDGET</p>",
            Page with { MaxCssDeclarations = 1 }));
        using var output = new MemoryStream();
        output.Write("UNCHANGED"u8);
        var error = Assert.Throws<FactsPdfException>(() =>
            PdfConverter.Convert("<p style='padding-inline:1pt'>BUDGET</p>",
                output, Page with { MaxCssDeclarations = 1 }));
        Assert.That(error!.Code, Is.EqualTo("FPDF1205"));
        Assert.That(output.ToArray(), Is.EqualTo("UNCHANGED"u8.ToArray()));
    }

    [Test]
    public void CliAndLibraryRenderTheSameLogicalLonghandReport()
    {
        const string html = "<style>.card{width:145pt;" +
            "margin-inline-start:auto;margin-inline-end:auto;" +
            "padding-inline-start:5pt;padding-block-end:4pt}</style>" +
            "<section class=card>Service approval</section>";
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(html));
        using var output = new MemoryStream();
        using var diagnostics = new StringWriter();
        Assert.That(CliApplication.Run(["-", "-"], input, output, diagnostics),
            Is.Zero, diagnostics.ToString());
        using var expected = new MemoryStream();
        PdfConverter.Convert(html, expected);
        Assert.That(output.ToArray(), Is.EqualTo(expected.ToArray()));
    }
}
