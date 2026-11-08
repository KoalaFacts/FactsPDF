using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>Computed first-line cell indent reaches actual PDF glyph layout and pagination.</summary>
[TestFixture]
public sealed class TableCellTextIndentTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 260, PageHeight = 160, Margin = 10 };

    private static string Cell(string declaration, bool nested = false)
    {
        var text = nested ? "<p style='text-indent:inherit'>FIRST<br>SECOND</p>" : "FIRST<br>SECOND";
        return "<html style='font-size:16pt'><body><table style='width:120pt;font-size:10pt'>" +
            "<colgroup><col style='width:120pt'></colgroup><tbody><tr>" +
            "<td style='font-size:12pt;padding:2pt;" + declaration + "'>" + text +
            "</td></tr></tbody></table></body></html>";
    }

    private static List<LayoutPage> Layout(string html, PdfOptions? options = null)
        => TableLayoutTests.Layout(html, options ?? Page);

    private static byte[] Pdf(string html, PdfOptions? options = null)
        => TableLayoutTests.Pdf(html, options ?? Page);

    private static void Reject(string html, string code)
    {
        using var output = new MemoryStream();
        output.Write("existing invoice"u8);
        var prior = output.ToArray();
        output.Position = 4;
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, Page));
        Assert.That(error!.Code, Is.EqualTo(code));
        Assert.That(output.ToArray(), Is.EqualTo(prior));
        Assert.That(output.Position, Is.EqualTo(4));
        Assert.That(Pdf(Cell("text-indent:3pt")), Is.Not.Empty);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void CellAndFlatParagraphUseOwnComputedEmAndIndentOnlyFirstLine(bool nested)
    {
        var relative = Cell("text-indent:0.25em", nested);
        var absolute = Cell("text-indent:3pt", nested);
        var noIndent = Cell("text-indent:0pt", nested);
        Assert.That(Pdf(relative), Is.EqualTo(Pdf(absolute)));
        var actual = Layout(relative).Single().Runs;
        var expected = Layout(absolute).Single().Runs;
        var plain = Layout(noIndent).Single().Runs;
        Assert.That(actual.Select(r => (r.Text, r.X, r.Baseline)),
            Is.EqualTo(expected.Select(r => (r.Text, r.X, r.Baseline))));
        Assert.That(actual.Single(r => r.Text == "FIRST").X - plain.Single(r => r.Text == "FIRST").X,
            Is.EqualTo(3).Within(0.001));
        Assert.That(actual.Single(r => r.Text == "SECOND").X,
            Is.EqualTo(plain.Single(r => r.Text == "SECOND").X).Within(0.001));
    }

    [Test]
    public void CellPercentageUsesMeasuredCellContentWidth()
    {
        // Track 120pt less the two 2pt horizontal cell insets = 116pt.
        Assert.That(Pdf(Cell("text-indent:25%")), Is.EqualTo(Pdf(Cell("text-indent:29pt"))));
    }

    [Test]
    public void MultipageReportWithImportantCellRulesAndOverriddenParagraphsMatchesPointOracle()
    {
        var directory = Path.Combine(TestContext.CurrentContext.TestDirectory, "examples");
        var source = File.ReadAllText(Path.Combine(directory, "report-cell-text-indent.html"));
        var oracle = File.ReadAllText(Path.Combine(directory, "report-cell-text-indent-oracle.html"));
        Assert.That(Pdf(source), Is.EqualTo(Pdf(oracle)));
        var actual = Layout(source);
        var control = Layout(oracle);
        Assert.That(actual.Count, Is.GreaterThan(2));
        Assert.That(actual.All(page => page.Runs.Any(run => run.Text == "CODE")), Is.True);
        Assert.That(actual.SelectMany(page => page.Runs).Select(r => (r.Text, r.X, r.Baseline)),
            Is.EqualTo(control.SelectMany(page => page.Runs).Select(r => (r.Text, r.X, r.Baseline))));
        var searchable = string.Join(" ", actual.SelectMany(page => page.Runs).Select(r => r.Text));
        for (var i = 1; i <= 24; i++)
            Assert.That(searchable, Does.Contain($"R{i:00}"));
    }

    [TestCase("text-indent:-2pt", "FPDF1202")]
    [TestCase("text-indent:calc(2pt + 1pt)", "FPDF1202")]
    [TestCase("text-indent:150pt", "FPDF1302")]
    [TestCase("text-indent:150%", "FPDF1302")]
    public void UnsupportedOrImpossibleCellIndentsProtectDestination(string declaration, string code)
        => Reject(Cell(declaration), code);

    [Test]
    public void RowColumnAndInlineDeclarationsAreStillInvalid()
    {
        Reject(Cell("text-indent:3pt").Replace("<tr>", "<tr style='text-indent:3pt'>",
            StringComparison.Ordinal), "FPDF1201");
        Reject(Cell("text-indent:3pt").Replace("<col style='width:120pt'>",
            "<col style='width:120pt;text-indent:3pt'>", StringComparison.Ordinal), "FPDF1201");
        Reject(Cell("text-indent:3pt").Replace("FIRST", "<span style='text-indent:3pt'>FIRST</span>",
            StringComparison.Ordinal), "FPDF1201");
    }
}
