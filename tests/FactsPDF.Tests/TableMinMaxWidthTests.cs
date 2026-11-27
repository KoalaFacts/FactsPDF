using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// min-width and max-width on a table limit its used width the way a browser prints it: the width
/// the columns would take (width, or the max-content width within the container) is limited by
/// max-width and then raised by min-width, so min-width wins, and the columns' content minimums
/// still floor it. The built-in Courier font advances 6pt per glyph at 10pt, so two-glyph cells
/// are 12pt wide; the page content box is 260pt wide from x=20.
/// </summary>
[TestFixture]
public sealed class TableMinMaxWidthTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 500, Margin = 20 };
    private const string Css = "<style>html,body{margin:0;padding:0}body{font-size:10pt;line-height:20pt}table{border-collapse:separate;border-spacing:0}td{padding:0}</style>";

    private static List<LayoutPage> Layout(string html)
        => TextLayout.Layout(HtmlDocumentReader.ReadTree(Css + html, Page, default), Page, default);

    // The x of the second cell: the first column's width after the table's left edge at 20.
    private static double SecondColumnX(string tableStyle)
    {
        var pages = Layout($"<table style='{tableStyle}'><tr><td>T1</td><td>T2</td></tr></table>");
        return pages[0].Runs.Single(r => r.Text == "T2").X;
    }

    private static FactsPdfException Fails(string html, string code)
    {
        using var output = new MemoryStream();
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(Css + html, output, Page));
        Assert.That(error!.Code, Is.EqualTo(code), error.Message);
        return error;
    }

    [Test]
    public void MinWidthWidensAnAutoWidthTableAndSharesTheExtraAmongItsColumns()
    {
        Assert.That(SecondColumnX(""), Is.EqualTo(20 + 12).Within(1e-9));
        Assert.That(SecondColumnX("min-width:200pt"), Is.EqualTo(20 + 100).Within(1e-9));
        // One CSS pixel is 0.75pt.
        Assert.That(SecondColumnX("min-width:160px"), Is.EqualTo(20 + 60).Within(1e-9));
        Assert.That(SecondColumnX("min-width:50%"), Is.EqualTo(20 + 65).Within(1e-9));
    }

    [Test]
    public void MaxWidthNarrowsAnExplicitWidthButNeverBelowTheColumnMinimums()
    {
        Assert.That(SecondColumnX("width:200pt;max-width:100pt"), Is.EqualTo(20 + 50).Within(1e-9));
        Assert.That(SecondColumnX("width:100%;max-width:25%"), Is.EqualTo(20 + 32.5).Within(1e-9));
        // Below the content minimum the table stays as narrow as its content allows.
        Assert.That(SecondColumnX("max-width:10pt"), Is.EqualTo(20 + 12).Within(1e-9));
    }

    [Test]
    public void MinWidthWinsOverMaxWidthAndOverWidth()
    {
        Assert.That(SecondColumnX("min-width:200pt;max-width:100pt"), Is.EqualTo(20 + 100).Within(1e-9));
        Assert.That(SecondColumnX("width:100pt;min-width:200pt"), Is.EqualTo(20 + 100).Within(1e-9));
    }

    // Over-correction: a constraint that does not bind changes nothing, not even the shrink-to-fit width.
    [Test]
    public void ConstraintsThatDoNotBindLeaveTheNaturalWidth()
    {
        Assert.That(SecondColumnX("min-width:20pt"), Is.EqualTo(20 + 12).Within(1e-9));
        Assert.That(SecondColumnX("max-width:900pt"), Is.EqualTo(20 + 12).Within(1e-9));
        var plain = Layout("<table><tr><td>T1</td><td>T2</td></tr></table>");
        var constrained = Layout("<table style='min-width:1pt;max-width:900pt'><tr><td>T1</td><td>T2</td></tr></table>");
        Assert.That(constrained[0].Runs.Select(r => (r.Text, r.X, r.Baseline)), Is.EqualTo(plain[0].Runs.Select(r => (r.Text, r.X, r.Baseline))));
    }

    [Test]
    public void ExplicitColumnWidthsGrowInProportionToTheirWidths()
    {
        const string columns = "<colgroup><col style='width:60pt'><col style='width:100pt'></colgroup>";
        var wider = Layout($"<table style='min-width:240pt'>{columns}<tr><td>A</td><td>B</td></tr></table>");
        Assert.That(wider[0].Runs.Single(r => r.Text == "B").X, Is.EqualTo(20 + 90).Within(1e-9));
        var inRange = Layout($"<table style='min-width:100pt;max-width:300pt'>{columns}<tr><td>A</td><td>B</td></tr></table>");
        Assert.That(inRange[0].Runs.Single(r => r.Text == "B").X, Is.EqualTo(20 + 60).Within(1e-9));
        var capped = Layout($"<table style='width:300pt;max-width:200pt'>{columns}<tr><td>A</td><td>B</td></tr></table>");
        Assert.That(capped[0].Runs.Single(r => r.Text == "B").X, Is.EqualTo(20 + 75).Within(1e-9));
    }

    [Test]
    public void AMaxWidthBelowTheExplicitColumnWidthsFailsExplicitly()
    {
        var error = Fails("<table style='max-width:100pt'><colgroup><col style='width:60pt'><col style='width:100pt'></colgroup>" +
            "<tr><td>A</td><td>B</td></tr></table>", "FPDF1302");
        Assert.That(error.Message, Does.Contain("max-width"));
    }

    [Test]
    public void AMinWidthWiderThanTheContainerFailsLikeAnyWideTable()
        => Fails("<table style='min-width:300pt'><tr><td>T1</td></tr></table>", "FPDF1302");

    [Test]
    public void CellsRowsAndGroupsStillRejectWidthConstraints()
    {
        Fails("<table><tr><td style='min-width:50pt'>A</td></tr></table>", "FPDF1201");
        Fails("<table><tr style='max-width:50pt'><td>A</td></tr></table>", "FPDF1201");
        Fails("<table><colgroup><col style='min-width:50pt'></colgroup><tr><td>A</td></tr></table>", "FPDF1201");
    }

    [Test]
    public void CollapsedBordersCountInsideTheConstrainedWidth()
    {
        // The collapsed table's width includes the outer halves of its borders, so a 200pt minimum is
        // the whole painted table: its two 2pt half borders leave 196pt, 98pt per column.
        var pages = Layout("<table style='min-width:200pt;border-collapse:collapse'><tr><td style='border:4pt solid #000'>T1</td><td style='border:4pt solid #000'>T2</td></tr></table>");
        Assert.That(pages[0].Runs.Single(r => r.Text == "T2").X - pages[0].Runs.Single(r => r.Text == "T1").X,
            Is.EqualTo(98).Within(1e-6));
    }
}
