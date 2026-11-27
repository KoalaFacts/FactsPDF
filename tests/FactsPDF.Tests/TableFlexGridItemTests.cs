using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// A table as a flex or grid item: the table itself is the item (a browser wraps it in an anonymous
/// table wrapper box), sized by its width, min-width and max-width, flex-grow and flex-basis, or as
/// a grid item by stretching over its area. The built-in Courier font advances 6pt per glyph at
/// 10pt, so two-glyph cells are 12pt wide; the page content box is 260pt wide from x=20.
/// </summary>
[TestFixture]
public sealed class TableFlexGridItemTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 500, Margin = 20 };
    private const string Css = "<style>html,body{margin:0;padding:0}body{font-size:10pt;line-height:20pt}table{border-collapse:separate;border-spacing:0}td{padding:0;vertical-align:top}p{margin:0}</style>";
    private const string Cells = "<tr><td>T1</td><td>T2</td></tr>";
    private const string Start = "display:flex;align-items:flex-start;";

    private static List<LayoutPage> Layout(string html)
        => TextLayout.Layout(HtmlDocumentReader.ReadTree(Css + html, Page, default), Page, default);

    private static PlacedText Run(List<LayoutPage> pages, string text) => pages[0].Runs.Single(r => r.Text == text);

    [Test]
    public void AnAutoWidthTableTakesItsMaxContentWidthBesideItsSiblings()
    {
        var pages = Layout($"<div style='{Start}'><div style='width:60pt'>L</div><table>{Cells}</table><div>R</div></div>");
        Assert.That(Run(pages, "T1").X, Is.EqualTo(80).Within(1e-9));
        Assert.That(Run(pages, "T2").X, Is.EqualTo(92).Within(1e-9));
        Assert.That(Run(pages, "R").X, Is.EqualTo(104).Within(1e-9));
        // All three sit on the first line.
        Assert.That(Run(pages, "T1").Baseline, Is.EqualTo(Run(pages, "L").Baseline).Within(1e-9));
    }

    [Test]
    public void FlexGrowStretchesTheTablesColumnsOverTheFreeSpace()
    {
        var pages = Layout($"<div style='{Start}'><div style='width:60pt'>L</div><table style='flex-grow:1'>{Cells}</table></div>");
        // 260 - 60 = 200pt for the table: 100pt per column.
        Assert.That(Run(pages, "T2").X, Is.EqualTo(20 + 60 + 100).Within(1e-9));
    }

    [Test]
    public void TableWidthPercentagesResolveAgainstTheFlexContainer()
    {
        var pages = Layout($"<div style='{Start}width:200pt'><div style='width:20pt'>L</div><table style='width:50%'>{Cells}</table></div>");
        // 50% of 200pt = 100pt: 50pt per column.
        Assert.That(Run(pages, "T2").X, Is.EqualTo(20 + 20 + 50).Within(1e-9));
    }

    [Test]
    public void AnExplicitWidthAndAMinWidthSizeTheItem()
    {
        var wide = Layout($"<div style='{Start}'><div style='width:60pt'>L</div><table style='width:120pt'>{Cells}</table></div>");
        Assert.That(Run(wide, "T2").X, Is.EqualTo(20 + 60 + 60).Within(1e-9));
        var minimum = Layout($"<div style='{Start}'><div style='width:60pt'>L</div><table style='min-width:100pt'>{Cells}</table></div>");
        Assert.That(Run(minimum, "T2").X, Is.EqualTo(20 + 60 + 50).Within(1e-9));
    }

    [Test]
    public void ATableOnItsOwnInAFlexContainerLaysOutLikeAnAutoWidthTable()
    {
        var item = Layout($"<div style='display:flex'><table>{Cells}</table></div>");
        var plain = Layout($"<table>{Cells}</table>");
        Assert.That(item[0].Runs.Select(r => (r.Text, r.X, r.Baseline)), Is.EqualTo(plain[0].Runs.Select(r => (r.Text, r.X, r.Baseline))));
    }

    [Test]
    public void ContainerGapJustifyContentAndOrderApplyToTables()
    {
        var gap = Layout($"<div style='{Start}column-gap:15pt'><div style='width:30pt'>L</div><table>{Cells}</table></div>");
        Assert.That(Run(gap, "T1").X, Is.EqualTo(20 + 30 + 15).Within(1e-9));
        var between = Layout($"<div style='{Start}justify-content:space-between'><div style='width:30pt'>L</div><table>{Cells}</table></div>");
        Assert.That(Run(between, "T2").X, Is.EqualTo(280 - 12).Within(1e-9));
        var ordered = Layout($"<div style='{Start}'><div style='width:30pt'>L</div><table style='order:-1'>{Cells}</table></div>");
        Assert.That(Run(ordered, "T1").X, Is.EqualTo(20).Within(1e-9));
        Assert.That(Run(ordered, "L").X, Is.EqualTo(20 + 24).Within(1e-9));
    }

    [Test]
    public void TheTablesVerticalMarginsPlaceItAndEndTheLine()
    {
        var pages = Layout($"<div style='{Start}'><div>L</div><table style='margin:7pt 0 9pt'>{Cells}</table></div><p>BBB</p>");
        Assert.That(Run(pages, "L").Baseline - Run(pages, "T1").Baseline, Is.EqualTo(7).Within(1e-9));
        // The line is 7 + 20 + 9 = 36pt tall.
        Assert.That(Run(pages, "L").Baseline - Run(pages, "BBB").Baseline, Is.EqualTo(36).Within(1e-9));
    }

    // align-items:stretch (the default) grows the table box to its line: its body rows take the extra
    // height in proportion to their own heights, while a header, a footer and a start-aligned table
    // keep theirs. The distances were measured against a browser's printed PDF.
    [Test]
    public void AStretchedTablesBodyRowsShareTheExtraHeightInProportion()
    {
        var equal = Layout($"<div style='display:flex'><div style='height:100pt'>L</div><table>{Cells}{Cells}</table></div>");
        // 100pt for two 20pt rows: 50pt each.
        var rows = equal[0].Runs.Where(r => r.Text == "T1").OrderByDescending(r => r.Baseline).ToList();
        Assert.That(rows[0].Baseline - rows[1].Baseline, Is.EqualTo(50).Within(1e-6));
        var unequal = Layout("<div style='display:flex'><div style='height:200pt'>L</div><table><tr><td>R1</td></tr><tr><td>R2a<br>R2b<br>R2c</td></tr></table></div>");
        // 200pt for 20pt + 60pt rows: 50pt and 150pt.
        Assert.That(Run(unequal, "R1").Baseline - Run(unequal, "R2a").Baseline, Is.EqualTo(50).Within(1e-6));
        var grid = Layout($"<div style='display:grid;grid-template-columns:1fr 1fr'><div style='height:100pt'>L</div><table>{Cells}{Cells}</table></div>");
        var gridRows = grid[0].Runs.Where(r => r.Text == "T1").OrderByDescending(r => r.Baseline).ToList();
        Assert.That(gridRows[0].Baseline - gridRows[1].Baseline, Is.EqualTo(50).Within(1e-6));
    }

    [Test]
    public void AStretchedTableKeepsItsHeaderAndFooterRowsAtTheirOwnHeight()
    {
        var pages = Layout("<div style='display:flex'><div style='height:200pt'>L</div><table>" +
            "<thead><tr><td>H1</td></tr></thead><tbody><tr><td>R1</td></tr><tr><td>R2</td></tr></tbody><tfoot><tr><td>F1</td></tr></tfoot></table></div>");
        // 200 - 20 - 20 = 160pt for the two body rows: 80pt each.
        Assert.That(Run(pages, "H1").Baseline - Run(pages, "R1").Baseline, Is.EqualTo(20).Within(1e-6));
        Assert.That(Run(pages, "R1").Baseline - Run(pages, "R2").Baseline, Is.EqualTo(80).Within(1e-6));
        Assert.That(Run(pages, "R2").Baseline - Run(pages, "F1").Baseline, Is.EqualTo(80).Within(1e-6));
    }

    [Test]
    public void AStartAlignedTableKeepsItsOwnHeightAndATallerTableStretchesItsSiblings()
    {
        var start = Layout($"<div style='display:flex;align-items:flex-start'><div style='height:100pt'>L</div><table>{Cells}{Cells}</table></div>");
        var rows = start[0].Runs.Where(r => r.Text == "T1").OrderByDescending(r => r.Baseline).ToList();
        Assert.That(rows[0].Baseline - rows[1].Baseline, Is.EqualTo(20).Within(1e-9));
        Assert.DoesNotThrow(() => Layout($"<div style='display:flex'><div>L</div><table>{Cells}{Cells}{Cells}</table></div>"));
    }

    [Test]
    public void AStretchedTableAlignsItsCellsInTheGrownRows()
    {
        var pages = Layout("<div style='display:flex'><div style='height:200pt'>L</div><table>" +
            "<tr><td style='vertical-align:bottom'>R1</td></tr><tr><td style='vertical-align:middle'>R2</td></tr></table></div>");
        // Two 100pt rows: the first cell at its row's bottom, the second centred.
        Assert.That(Run(pages, "R1").Baseline - Run(pages, "R2").Baseline, Is.EqualTo(60).Within(1e-6));
    }

    [Test]
    public void AGridTableStretchesOverItsAreaUnlessItIsStartAligned()
    {
        var area = Layout($"<div style='display:grid;grid-template-columns:100pt 1fr;align-items:start'><div>L</div><table>{Cells}</table></div>");
        // The 1fr column is 160pt wide, so the table fills it: 80pt per column.
        Assert.That(Run(area, "T1").X, Is.EqualTo(120).Within(1e-9));
        Assert.That(Run(area, "T2").X, Is.EqualTo(120 + 80).Within(1e-9));
        var start = Layout($"<div style='display:grid;grid-template-columns:100pt 1fr;align-items:start;justify-items:start'><div>L</div><table>{Cells}</table></div>");
        Assert.That(Run(start, "T2").X, Is.EqualTo(120 + 12).Within(1e-9));
        var center = Layout($"<div style='display:grid;grid-template-columns:100pt 1fr;align-items:start;justify-items:center'><div>L</div><table>{Cells}</table></div>");
        Assert.That(Run(center, "T1").X, Is.EqualTo(120 + (160 - 24) / 2).Within(1e-9));
    }

    [Test]
    public void AGridTableInAnAutoColumnSizesTheColumn()
    {
        var pages = Layout($"<div style='display:grid;grid-template-columns:auto 1fr;align-items:start'><table>{Cells}</table><div>R</div></div>");
        Assert.That(Run(pages, "T2").X, Is.EqualTo(20 + 12).Within(1e-9));
        Assert.That(Run(pages, "R").X, Is.EqualTo(20 + 24).Within(1e-9));
    }

    [Test]
    public void ACollapsedBorderTableIsSizedAsOneFlexItem()
    {
        var pages = Layout($"<div style='{Start}'><div style='width:30pt'>L</div><table style='border-collapse:collapse'>" +
            "<tr><td style='border:2pt solid #000'>T1</td><td style='border:2pt solid #000'>T2</td></tr></table></div>");
        // Columns of 12 + the two half borders of 1pt each = 14.
        Assert.That(Run(pages, "T1").X, Is.GreaterThan(50));
        Assert.That(Run(pages, "T2").X - Run(pages, "T1").X, Is.EqualTo(14).Within(1e-9));
    }

    [Test]
    public void AFlexTableRendersTheSameBytesInNonStrictMode()
    {
        var html = Css + $"<div style='{Start}'><div>L</div><table>{Cells}</table></div>";
        using var strict = new MemoryStream();
        using var lenient = new MemoryStream();
        PdfConverter.Convert(html, strict, Page);
        PdfConverter.Convert(html, lenient, Page with { Strict = false });
        Assert.That(lenient.ToArray(), Is.EqualTo(strict.ToArray()));
    }
}
