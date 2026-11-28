using NUnit.Framework;

namespace FactsPDF.Tests;

// Chrome's user-agent th/caption centring, table captions and the collapsing border model.
public sealed class TableCaptionCollapseTests
{
    private const string Columns = "<colgroup><col style='width:50pt'><col style='width:50pt'></colgroup>";
    private static readonly Rgb Red = new(0xcc / 255d, 0, 0);

    private static string Table(string style, string body, string head = "", string caption = "", string tableStyle = "width:100pt;font-size:10pt")
        // html,body{margin:0}: coordinates below are measured from the page margin, not the 8px UA body margin.
        => $"<style>html,body{{margin:0}}{style}</style><table style='{tableStyle}'>{caption}{Columns}{head}<tbody>{body}</tbody></table>";

    private static Paragraph[] Paragraphs(string html)
        => BlockTreeTraversal.Paragraphs(HtmlDocumentReader.ReadTree(html, new(), default), default).ToArray();

    private static string Rows(int count, string attributes = "")
        => string.Concat(Enumerable.Range(0, count).Select(i => $"<tr{attributes}><td>R{i}</td><td>V</td></tr>"));

    // Collapsed border segments are boxes painting only a top (horizontal) or left (vertical) border.
    private static PaintedBox[] Horizontal(LayoutPage page)
        => page.PaintBoxes.Where(box => box.Style.BorderTop.EffectiveWidth > 0 && !box.Style.BorderTop.Transparent
            && box.Style.BackgroundColor is null && box.Style.BorderLeft.EffectiveWidth == 0).ToArray();

    private static PaintedBox[] Vertical(LayoutPage page)
        => page.PaintBoxes.Where(box => box.Style.BorderLeft.EffectiveWidth > 0 && !box.Style.BorderLeft.Transparent
            && box.Style.BackgroundColor is null && box.Style.BorderTop.EffectiveWidth == 0).ToArray();

    [TestCase("", "Center")]
    [TestCase("tr{text-align:start}", "Center")]
    [TestCase("table{text-align:right}", "Right")]
    [TestCase("tr{text-align:left}", "Left")]
    [TestCase("table{text-align:left}", "Left")]
    [TestCase("th{text-align:left}", "Left")]
    [TestCase("tr{text-align:right}th{text-align:inherit}", "Right")]
    public void ThIsCentredUnlessItsParentAlignmentIsSet(string style, string expected)
    {
        var paragraphs = Paragraphs(Table("th{font-weight:normal}" + style, "<tr><th>Ref</th><td>A1</td></tr>"));
        Assert.That(paragraphs[0].Style.Alignment, Is.EqualTo(Enum.Parse<TextAlignment>(expected)));
        Assert.That(paragraphs[1].Style.Alignment, Is.EqualTo(style.Contains("right", StringComparison.Ordinal)
            ? TextAlignment.Right : TextAlignment.Left), "td keeps the inherited alignment");
    }

    [Test]
    public void DefaultThRendersLikeACentredTd()
        => Assert.That(TableLayoutTests.Pdf(Table("th{font-weight:normal}", "<tr><th>Ref</th><td>A1</td></tr>",
                head: "<thead><tr><th>Id</th><th>Value</th></tr></thead>")),
            Is.EqualTo(TableLayoutTests.Pdf(Table("td.c{text-align:center}", "<tr><td class=c>Ref</td><td>A1</td></tr>",
                head: "<thead><tr><td class=c>Id</td><td class=c>Value</td></tr></thead>"))));

    [Test]
    public void CaptionIsCentredAndReadBeforeTheCells()
    {
        var paragraphs = Paragraphs(Table("", Rows(1), caption: "<caption>Totals <b>2026</b></caption>"));
        Assert.That(paragraphs[0].Runs.Select(run => run.Text), Is.EqualTo(new[] { "Totals ", "2026" }));
        Assert.That(paragraphs[0].Style.Alignment, Is.EqualTo(TextAlignment.Center));
        Assert.That(Paragraphs(Table("table{text-align:right}", Rows(1), caption: "<caption>T</caption>"))[0].Style.Alignment,
            Is.EqualTo(TextAlignment.Right));
    }

    [Test]
    public void CaptionIsABlockAboveTheTableAtItsWidth()
    {
        const string style = "caption{padding:2pt 4pt;background-color:#eef3f7;border-top:1pt solid #333}td{background-color:#c00}";
        var captioned = TableLayoutTests.Layout(Table(style, Rows(1), caption: "<caption>Totals</caption>")).Single();
        var without = TableLayoutTests.Layout(Table(style, Rows(1))).Single();
        var captionBox = captioned.PaintBoxes.Single(box => box.Style.BackgroundColor is { G: > 0.9, B: > 0.9 });
        Assert.That(captionBox.X, Is.EqualTo(36));
        Assert.That(captionBox.Top, Is.EqualTo(36));
        Assert.That(captionBox.Width, Is.EqualTo(100).Within(1e-9));
        var cell = captioned.PaintBoxes.First(box => box.Style.BackgroundColor == Red);
        var plainCell = without.PaintBoxes.First(box => box.Style.BackgroundColor == Red);
        Assert.That(cell.Top - plainCell.Top, Is.EqualTo(captionBox.Height).Within(1e-9));
        var text = captioned.Runs.Single(run => run.Text == "Totals");
        Assert.That(text.X, Is.GreaterThan(36 + 4 + 10), "centred over the table, not at the caption's padding");
        Assert.That(text.Baseline, Is.GreaterThan(841.89 - cell.Top), "above the first row");
    }

    [Test]
    public void CaptionStaysWithTheFirstFragmentOfALongTable()
    {
        var pages = TableLayoutTests.Layout(Table("th{font-weight:normal}", Rows(90),
            head: "<thead><tr><th>Id</th><th>Value</th></tr></thead>", caption: "<caption>Ledger</caption>"));
        Assert.That(pages, Has.Count.GreaterThan(1));
        Assert.That(pages[0].Runs.Count(run => run.Text == "Ledger"), Is.EqualTo(1));
        Assert.That(pages.Skip(1).SelectMany(page => page.Runs).Any(run => run.Text == "Ledger"), Is.False);
        Assert.That(pages.All(page => page.Runs.Any(run => run.Text == "Id")), Is.True, "the header still repeats");
    }

    [TestCase("<table>" + Columns + "<caption>A</caption><tbody><tr><td>A</td><td>B</td></tr></tbody></table>", "FPDF1101")]
    [TestCase("<table><caption>A</caption><caption>B</caption><tbody><tr><td>A</td></tr></tbody></table>", "FPDF1101")]
    [TestCase("<table><caption><p>A</p></caption><tbody><tr><td>A</td></tr></tbody></table>", "FPDF1101")]
    [TestCase("<caption>A</caption>", "FPDF1101")]
    [TestCase("<table><caption style='caption-side:bottom'>A</caption><tbody><tr><td>A</td></tr></tbody></table>", "FPDF1202")]
    [TestCase("<table><caption style='margin-bottom:4pt'>A</caption><tbody><tr><td>A</td></tr></tbody></table>", "FPDF1201")]
    [TestCase("<table><caption style='width:40pt'>A</caption><tbody><tr><td>A</td></tr></tbody></table>", "FPDF1201")]
    [TestCase("<table style='width:30pt'><caption>Averyveryverylongunbreakablecaption</caption><tbody><tr><td>A</td></tr></tbody></table>", "FPDF1302")]
    public void UnsupportedCaptionsFail(string html, string code) => TableStructureTests.Fails(html, code);

    [Test]
    public void CaptionDisplayAndZeroMarginAreAccepted()
        => Assert.That(TableLayoutTests.Pdf(Table("caption{display:table-caption;margin:0;caption-side:top}", Rows(1), caption: "<caption>T</caption>")),
            Is.EqualTo(TableLayoutTests.Pdf(Table("", Rows(1), caption: "<caption>T</caption>"))));

    [Test]
    public void CollapsedCellsShareHalfOfEachBorder()
    {
        var page = TableLayoutTests.Layout(Table("table{border-collapse:collapse}td{border:2pt solid #000;background-color:#c00}",
            "<tr><td>A</td><td>B</td></tr>")).Single();
        var cells = page.PaintBoxes.Where(box => box.Style.BackgroundColor == Red).ToArray();
        // The table's layout border is half the outer border (1pt), and its 100pt width includes those
        // halves as in Chrome, so the two 50pt columns share 98pt.
        Assert.That(cells.Select(cell => cell.X), Is.EqualTo(new double[] { 37, 86 }));
        Assert.That(cells.Select(cell => cell.Width), Is.EqualTo(new double[] { 49, 49 }));
        Assert.That(cells[0].Top, Is.EqualTo(37));
        var vertical = Vertical(page);
        Assert.That(vertical.Select(box => box.X), Is.EqualTo(new double[] { 36, 85, 134 }));
        Assert.That(vertical.All(box => box.Width == 2), Is.True, "each shared border is painted once, full width");
        var horizontal = Horizontal(page);
        Assert.That(horizontal, Has.Length.EqualTo(4));
        Assert.That(horizontal.Select(box => box.Top).Distinct(), Is.EqualTo(new double[] { 36, cells[0].Top + cells[0].Height - 1 }));
        Assert.That(horizontal[0].X, Is.EqualTo(36), "a horizontal border reaches over half the vertical one");
        Assert.That(horizontal[0].Width, Is.EqualTo(51));
    }

    [TestCase("table{border:3pt solid #f00}td{border:1pt solid #000}", 3, 1)]
    [TestCase("table{border:1pt solid #f00}td{border:1pt dashed #000}", 1, 1)]
    public void WiderAndSolidBordersWinTheOuterEdge(string style, double outer, double inner)
    {
        var page = TableLayoutTests.Layout(Table("table{border-collapse:collapse}" + style, "<tr><td>A</td><td>B</td></tr>")).Single();
        var vertical = Vertical(page);
        Assert.That(vertical[0].Width, Is.EqualTo(outer));
        Assert.That(vertical[0].Style.BorderLeft.Pattern, Is.EqualTo(BorderPattern.Solid));
        Assert.That(vertical[0].TextColor, Is.EqualTo(new Rgb(1, 0, 0)));
        Assert.That(vertical[1].Width, Is.EqualTo(inner));
    }

    [Test]
    public void EqualBordersGoToTheCellThenTheLeftOrUpperBox()
    {
        var page = TableLayoutTests.Layout(Table("table{border-collapse:collapse}tr{border-top:1pt solid #f00}td{border-top:1pt solid #00f}"
            + "td.a{border-right:1pt solid #0f0}td.b{border-left:1pt solid #f0f}", "<tr><td class=a>A</td><td class=b>B</td></tr>")).Single();
        Assert.That(Horizontal(page).Where(box => box.Top < 40).Select(box => box.TextColor).Distinct(), Is.EqualTo(new[] { new Rgb(0, 0, 1) }));
        Assert.That(Vertical(page).Single().TextColor, Is.EqualTo(new Rgb(0, 1, 0)));
    }

    [Test]
    public void HiddenSuppressesEveryBorderOnItsEdge()
    {
        var page = TableLayoutTests.Layout(Table("table{border-collapse:collapse;border:4pt solid #f00}td{border:1pt solid #000}td.a{border-left:hidden}",
            "<tr><td class=a>A</td><td>B</td></tr>")).Single();
        Assert.That(Vertical(page).Select(box => box.Width), Is.EqualTo(new double[] { 1, 4 }));
    }

    [Test]
    public void RowAndGroupBordersTakePartInCollapsedTables()
    {
        // The zebra-row pattern: a light rule under every body row and a heavier one under the last.
        var page = TableLayoutTests.Layout(Table("table{border-collapse:collapse}tbody tr{border-bottom:1px solid #d8dee9}"
            + "tbody tr.last{border-bottom:2px solid #ae0f1a}thead{border-bottom:2pt solid #000}th{font-weight:normal}",
            Rows(2) + "<tr class=last><td>L</td><td>V</td></tr>", head: "<thead><tr><th>Id</th><th>Value</th></tr></thead>")).Single();
        var lines = Horizontal(page).GroupBy(box => box.Top).Select(group => (group.First().Height, group.First().TextColor)).ToArray();
        Assert.That(lines, Has.Length.EqualTo(4), "header bottom, two light rules and the last row's rule");
        Assert.That(lines[0].Height, Is.EqualTo(2));
        Assert.That(lines[1], Is.EqualTo((0.75, new Rgb(0xd8 / 255d, 0xde / 255d, 0xe9 / 255d))));
        Assert.That(lines[3], Is.EqualTo((1.5, new Rgb(0xae / 255d, 0x0f / 255d, 0x1a / 255d))));
    }

    [Test]
    public void CollapsedTablesIgnoreTablePadding()
        => Assert.That(TableLayoutTests.Pdf(Table("table{border-collapse:collapse;padding:6pt}td{border:1pt solid #000}", Rows(2))),
            Is.EqualTo(TableLayoutTests.Pdf(Table("table{border-collapse:collapse}td{border:1pt solid #000}", Rows(2)))));

    [Test]
    public void FragmentBreaksPaintHalfOfTheBrokenLineOnEachSide()
    {
        var pages = TableLayoutTests.Layout(Table("table{border-collapse:collapse}td{border-top:2pt solid #000}", Rows(120)));
        Assert.That(pages, Has.Count.GreaterThan(1));
        var lastOnFirst = Horizontal(pages[0]).OrderBy(box => box.Top).Last();
        Assert.That(lastOnFirst.Height, Is.EqualTo(1), "the upper half below the fragment's last row");
        var firstOnSecond = Horizontal(pages[1]).OrderBy(box => box.Top).First();
        Assert.That(firstOnSecond.Height, Is.EqualTo(1), "the lower half above the continuation's first row");
        Assert.That(firstOnSecond.Top, Is.EqualTo(36));
    }

    [Test]
    public void ARepeatedHeaderRepeatsTheTableTopBorder()
    {
        var pages = TableLayoutTests.Layout(Table("table{border-collapse:collapse;border-top:4pt solid #00f}th{font-weight:normal}", Rows(120),
            head: "<thead><tr><th>Id</th><th>Value</th></tr></thead>"));
        Assert.That(pages, Has.Count.GreaterThan(1));
        foreach (var page in pages)
        {
            var top = Horizontal(page).OrderBy(box => box.Top).First();
            Assert.That((top.Top, top.Height, top.TextColor), Is.EqualTo((36d, 4d, new Rgb(0, 0, 1))));
        }
    }

    [Test]
    public void RowBordersAreValidatedAndDroppedWithSeparatedBorders()
        // Chrome paints no row border in the separated model, so the declaration changes nothing.
        => Assert.That(TableLayoutTests.Pdf(Table("tr{border-bottom:1pt solid #000}", Rows(1))),
            Is.EqualTo(TableLayoutTests.Pdf(Table("", Rows(1)))));

    [Test]
    public void CollapsedRowThatMustSplitFails()
        => TableStructureTests.Fails(Table("table{border-collapse:collapse}td{border:1pt solid #000}",
            "<tr><td>" + string.Join("<br>", Enumerable.Repeat("line", 120)) + "</td><td>B</td></tr>"), "FPDF1302");

    [Test]
    public void BorderCollapseOutsideTablesChangesNothing()
        => Assert.That(TableLayoutTests.Pdf("<p style='border-collapse:collapse'>A</p>"), Is.EqualTo(TableLayoutTests.Pdf("<p>A</p>")));

    [TestCase("border-collapse:merge")]
    [TestCase("border-collapse:collapse separate")]
    public void OtherBorderCollapseValuesFail(string declaration)
        => TableStructureTests.Fails($"<p style='{declaration}'>A</p>", "FPDF1202");
}
