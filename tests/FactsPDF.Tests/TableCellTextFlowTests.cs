using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>Fixed-track cells consume inherited white-space and wrapping in measured PDF layout.</summary>
[TestFixture]
public sealed class TableCellTextFlowTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 260, PageHeight = 180, Margin = 10 };

    private static string Cell(string declarations, string contents, string? stylesheet = null)
        => "<html><body>" + (stylesheet ?? "") +
            "<table style='width:180pt;font-size:10pt'>" +
            "<colgroup><col style='width:60pt'><col style='width:120pt'></colgroup>" +
            "<thead><tr><th style='font-weight:normal;padding:2pt'>KEY</th><th style='font-weight:normal;padding:2pt'>NOTES</th></tr></thead>" +
            "<tbody><tr><td style='padding:2pt'>R01</td>" +
            "<td class='memo' style='padding:2pt;" + declarations + "'>" + contents +
            "</td></tr></tbody></table></body></html>";

    private static List<LayoutPage> Layout(string html, PdfOptions? page = null)
        => TableLayoutTests.Layout(html, page ?? Page);

    private static byte[] Pdf(string html, PdfOptions? page = null)
        => TableLayoutTests.Pdf(html, page ?? Page);

    private static void Reject(string html, string code)
    {
        using var stream = new MemoryStream();
        stream.Write("original-report"u8);
        var previous = stream.ToArray();
        stream.Position = 3;
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, stream, Page));
        Assert.That(error!.Code, Is.EqualTo(code));
        Assert.That(stream.ToArray(), Is.EqualTo(previous));
        Assert.That(stream.Position, Is.EqualTo(3));
        Assert.That(Pdf(Cell("", "<p>Recovered</p>")), Is.Not.Empty);
    }

    [TestCase("normal")]
    [TestCase("nowrap")]
    [TestCase("pre-line")]
    [TestCase("pre-wrap")]
    [TestCase("pre")]
    public void CellWhiteSpaceInheritsIntoFlatParagraphAndMatchesExplicitPdf(string mode)
    {
        const string content = "<p>PAID  OK\nDONE</p>";
        var actual = Cell("white-space:" + mode, content);
        var oracle = Cell("", "<p style='white-space:" + mode + "'>PAID  OK\nDONE</p>");
        Assert.That(Pdf(actual), Is.EqualTo(Pdf(oracle)));
        Assert.That(Layout(actual).SelectMany(p => p.Runs).Select(r => (r.Text,r.X,r.Baseline)),
            Is.EqualTo(Layout(oracle).SelectMany(p => p.Runs).Select(r => (r.Text,r.X,r.Baseline))));
    }

    [Test]
    public void AnonymousCellTextUsesPreservedNewlinesWithoutRewritingHtml()
    {
        var actual = Layout(Cell("white-space:pre-line", "PAID   IN FULL\nREVIEWED"))
            .SelectMany(page => page.Runs).ToArray();
        Assert.That(actual.Select(run => run.Text), Does.Contain("PAID IN FULL"));
        Assert.That(actual.Select(run => run.Text), Does.Contain("REVIEWED"));
        Assert.That(Pdf(Cell("white-space:pre-line", "PAID   IN FULL\nREVIEWED")), Is.Not.Empty);
    }

    [Test]
    public void CellOverflowWrapInheritsAndEmergencyBreaksLongIdentifiers()
    {
        const string code = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        var inherited = Cell("overflow-wrap:break-word", "<p>" + code + "</p>");
        var explicitChild = Cell("", "<p style='overflow-wrap:break-word'>" + code + "</p>");
        Assert.That(Pdf(inherited), Is.EqualTo(Pdf(explicitChild)));
        var lines = Layout(inherited).SelectMany(page => page.Runs).Where(run => run.Text != "KEY" && run.Text != "NOTES" && run.Text != "R01").ToArray();
        Assert.That(lines.Length, Is.GreaterThan(1));
        Assert.That(string.Concat(lines.Select(run => run.Text)), Is.EqualTo(code));
        Reject(Cell("overflow-wrap:normal", "<p>" + code + "</p>"), "FPDF1302");
    }

    [Test]
    public void CellWordBreakAndImportantStylesChangeActualGlyphWrapping()
    {
        const string word = "PAID EXAMPLE";
        const string rules = "<style>td.memo{word-break:break-all!important}</style>";
        var actual = Cell("word-break:normal", "<p>" + word + "</p>", rules);
        var oracle = Cell("", "<p style='word-break:break-all'>" + word + "</p>");
        Assert.That(Pdf(actual), Is.EqualTo(Pdf(oracle)));
        var actualText = Layout(actual).SelectMany(page => page.Runs).Select(r => r.Text).ToArray();
        Assert.That(string.Concat(actualText), Does.Contain(word));
    }

    [Test]
    public void CellInheritedModesCanBeIndependentlyResetOnFlatParagraphs()
    {
        const string text = "A  B\nC";
        var actual = Cell("white-space:pre-wrap;overflow-wrap:break-word;word-break:break-all",
            "<p style='white-space:initial;overflow-wrap:initial;word-break:initial'>" + text + "</p>");
        var oracle = Cell("", "<p style='white-space:normal;overflow-wrap:normal;word-break:normal'>" + text + "</p>");
        Assert.That(Pdf(actual), Is.EqualTo(Pdf(oracle)));
    }

    [Test]
    public void RepeatedHeadersAndMultipageDataMatchIndependentlyStyledParagraphOracle()
    {
        var folder = Path.Combine(TestContext.CurrentContext.TestDirectory, "examples");
        var relative = File.ReadAllText(Path.Combine(folder, "report-cell-text-flow.html"));
        var oracle = File.ReadAllText(Path.Combine(folder, "report-cell-text-flow-oracle.html"));
        Assert.That(Pdf(relative), Is.EqualTo(Pdf(oracle)));
        var pages = Layout(relative);
        var direct = Layout(oracle);
        Assert.That(pages.Count, Is.GreaterThan(2));
        Assert.That(pages.All(page => page.Runs.Any(run => run.Text == "KEY")), Is.True);
        Assert.That(pages.SelectMany(page => page.Runs).Select(run => (run.Text,run.X,run.Baseline)),
            Is.EqualTo(direct.SelectMany(page => page.Runs).Select(run => (run.Text,run.X,run.Baseline))));
        var text = string.Concat(pages.SelectMany(page => page.Runs).Select(run => run.Text));
        for (var i = 1; i <= 24; i++)
            Assert.That(text, Does.Contain($"REC{i:00}"));
    }

    [TestCase("white-space:break-spaces", "FPDF1202")]
    [TestCase("overflow-wrap:anywhere", "FPDF1202")]
    [TestCase("word-break:keep-all", "FPDF1202")]
    [TestCase("white-space:pre", "FPDF1305")]
    public void UnsupportedValuesAndModesProtectExistingPdf(string declaration, string code)
        => Reject(Cell(declaration, declaration == "white-space:pre" ? "ALPHA\tBETA" : "<p>ALPHA</p>"), code);

    [Test]
    public void NoWrapStillRejectsUnfittableCellEvenIfEmergencyBreakIsRequested()
        => Reject(Cell("white-space:nowrap;overflow-wrap:break-word", "<p>ABCDEFGHIJKLMNOPQRSTUVWXYZ</p>"), "FPDF1302");

    [Test]
    public void StructuralAndInlineDeclarationsRemainUnsupported()
    {
        var html = Cell("", "<p><span>Text</span></p>");
        Reject(html.Replace("<table style='width:180pt;font-size:10pt'>",
            "<table style='width:180pt;font-size:10pt;word-break:break-all'>",
            StringComparison.Ordinal), "FPDF1201");
        Reject(html.Replace("<tr><td", "<tr style='overflow-wrap:break-word'><td", StringComparison.Ordinal), "FPDF1201");
        Reject(html.Replace("<col style='width:60pt'>", "<col style='width:60pt;white-space:pre-line'>", StringComparison.Ordinal), "FPDF1201");
        Reject(html.Replace("<span>", "<span style='word-break:break-all'>", StringComparison.Ordinal), "FPDF1201");
    }
}
