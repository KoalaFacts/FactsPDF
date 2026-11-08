using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>Explicit table tracks and table padding resolve computed font units before PDF layout.</summary>
[TestFixture]
public sealed class TableTrackRelativeWidthTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 240, PageHeight = 132, Margin = 10 };

    private static string Invoice(string tableWidth, string firstWidth, string secondWidth,
        string tablePadding = "padding:2pt", string colgroupFont = "font-size:20pt")
        => "<html style='font-size:16pt'><body><table style='width:" + tableWidth +
           ";font-size:10pt;line-height:1.2;" + tablePadding +
           ";border:1pt solid #315f82;background:#eef3f7'>" +
           "<colgroup style='" + colgroupFont + "'><col style='width:" +
           firstWidth + "'><col style='width:" + secondWidth + "'></colgroup>" +
           "<thead><tr><th style='font-weight:normal;padding:2pt'>CODE</th>" +
           "<th style='font-weight:normal;padding:2pt'>DESCRIPTION</th></tr></thead>" +
           "<tbody><tr><td style='padding:2pt'>A</td><td style='padding:2pt'>B C</td></tr>" +
           "</tbody></table></body></html>";

    private static byte[] Pdf(string html, PdfOptions? options = null)
        => TableLayoutTests.Pdf(html, options ?? Page);

    private static List<LayoutPage> Layout(string html, PdfOptions? options = null)
        => TableLayoutTests.Layout(html, options ?? Page);

    private static void Reject(string html, string code)
    {
        using var destination = new MemoryStream();
        destination.Write("previous invoice PDF"u8);
        var previous = destination.ToArray();
        destination.Position = 5;
        var error = Assert.Throws<FactsPdfException>(() =>
            PdfConverter.Convert(html, destination, Page));
        Assert.That(error!.Code, Is.EqualTo(code));
        Assert.That(destination.ToArray(), Is.EqualTo(previous));
        Assert.That(destination.Position, Is.EqualTo(5));
        Assert.That(Pdf(Invoice("120pt", "40pt", "80pt")), Is.Not.Empty,
            "A rejected track must not poison a subsequent invoice.");
    }

    [Test]
    public void TableOwnEmAndColgroupInheritedEmAndHtmlRemMatchPointOracle()
    {
        var relative = Invoice("12em", "2em", "5rem", "padding:0.25em 0.5rem");
        var physical = Invoice("120pt", "40pt", "80pt", "padding:2.5pt 8pt");
        Assert.That(Pdf(relative), Is.EqualTo(Pdf(physical)));
        Assert.That(Layout(relative).Single().PaintBoxes.Select(b => (b.X, b.Top, b.Width, b.Height)),
            Is.EqualTo(Layout(physical).Single().PaintBoxes.Select(b => (b.X, b.Top, b.Width, b.Height))));
        Assert.That(Layout(relative).Single().Runs.Select(r => (r.Text, r.X, r.Baseline)),
            Is.EqualTo(Layout(physical).Single().Runs.Select(r => (r.Text, r.X, r.Baseline))));
    }

    [TestCase("width:12em", "width:120pt")]
    [TestCase("width:7.5rem", "width:120pt")]
    public void TableWidthUnitsResolveAgainstTheirOwnCorrectFont(string relative, string physical)
    {
        var source = Invoice("120pt", "40pt", "80pt").Replace("width:120pt", relative, StringComparison.Ordinal);
        var oracle = Invoice("120pt", "40pt", "80pt").Replace("width:120pt", physical, StringComparison.Ordinal);
        Assert.That(Pdf(source), Is.EqualTo(Pdf(oracle)));
    }

    [Test]
    public void TableLogicalFontRelativePaddingMatchesPhysicalPointsAcrossRepeatedHeaders()
    {
        const string extraRow = "<tr><td>A</td><td>B C</td></tr>";
        var additionalRows = string.Concat(Enumerable.Repeat(extraRow, 24));
        var relative = Invoice("12em", "2em", "5rem",
            "padding-inline:0.25rem;padding-block:0.2em")
            .Replace("</tbody>", additionalRows + "</tbody>", StringComparison.Ordinal);
        var physical = Invoice("120pt", "40pt", "80pt", "padding:2pt 4pt")
            .Replace("</tbody>", additionalRows + "</tbody>", StringComparison.Ordinal);
        var options = Page with { PageHeight = 125 };
        Assert.That(Pdf(relative, options), Is.EqualTo(Pdf(physical, options)));
        var actualPages = Layout(relative, options);
        var expectedPages = Layout(physical, options);
        Assert.That(actualPages.Count, Is.GreaterThan(1));
        Assert.That(actualPages.All(page => page.Runs.Any(run => run.Text == "CODE")), Is.True);
        Assert.That(actualPages.SelectMany(page => page.Runs).Select(run => (run.Text, run.X, run.Baseline)),
            Is.EqualTo(expectedPages.SelectMany(page => page.Runs).Select(run => (run.Text, run.X, run.Baseline))));
    }

    [Test]
    public void ImportantFontAndWidthWinnersDetermineTracksBeforeMeasurement()
    {
        const string start = "<html style='font-size:16pt'><body>";
        const string rows = "<thead><tr><th style='font-weight:normal'>CODE</th>" +
            "<th style='font-weight:normal'>DETAIL</th></tr></thead>" +
            "<tbody><tr><td>A</td><td>B</td></tr></tbody></table></body></html>";
        var relative = start +
            "<style>table.invoice{font-size:12pt!important;width:10em!important;padding:0.25rem!important}" +
            "col.code{width:2rem!important}</style>" +
            "<table class='invoice' style='width:15rem;font-size:20pt;padding:1em'>" +
            "<colgroup><col class='code' style='width:40pt'><col style='width:88pt'></colgroup>" + rows;
        var oracle = start +
            "<table style='width:120pt;font-size:12pt;padding:4pt'>" +
            "<colgroup><col style='width:32pt'><col style='width:88pt'></colgroup>" + rows;
        Assert.That(Pdf(relative), Is.EqualTo(Pdf(oracle)));
    }

    private static string Example(string name)
        => File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "examples", name));

    [Test]
    public void UnmodifiedMultiPageReportMatchesIndependentPointWidthsAndRepeatedHeaders()
    {
        var source = Example("report-table-relative-tracks.html");
        var oracle = Example("report-table-relative-tracks-oracle.html");
        var options = Page with { PageHeight = 125 };
        Assert.That(Pdf(source, options), Is.EqualTo(Pdf(oracle, options)));
        var pages = Layout(source, options);
        Assert.That(pages.Count, Is.GreaterThan(2));
        Assert.That(pages.All(p => p.Runs.Any(r => r.Text == "CODE")), Is.True);
        var text = pages.SelectMany(p => p.Runs).Select(r => r.Text).ToArray();
        for (var i = 1; i <= 24; i++)
            Assert.That(text.Count(s => s == $"R{i:00}"), Is.EqualTo(1));
    }

    [TestCase("12em", "-1em", "5rem", "FPDF1202")]
    [TestCase("12em", "calc(2em + 1pt)", "5rem", "FPDF1202")]
    [TestCase("12em", "0em", "5rem", "FPDF1302")]
    [TestCase("12em", "3em", "5rem", "FPDF1302")]
    [TestCase("9em", "2em", "5rem", "FPDF1302")]
    [TestCase("1200rem", "2em", "5rem", "FPDF1202")]
    public void UnsupportedAndMismatchedWidthsProtectPreviouslyGeneratedPdf(
        string tableWidth, string first, string second, string error)
        => Reject(Invoice(tableWidth, first, second), error);

    [Test]
    public void InapplicableCellWidthAndColumnDecorationStillFail()
    {
        Reject(Invoice("12em", "2em", "5rem").Replace("<td style='padding:2pt'>",
            "<td style='width:2em;padding:2pt'>", StringComparison.Ordinal), "FPDF1201");
        Reject(Invoice("12em", "2em", "5rem").Replace(
            "<col style='width:2em'>", "<col style='width:2em;padding:1rem'>", StringComparison.Ordinal),
            "FPDF1201");
    }
}
