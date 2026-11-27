using NUnit.Framework;

namespace FactsPDF.Tests;

// Without supplied fonts every ASCII glyph advances 600/1000 em, so at 10pt each character is
// exactly 6pt wide and every expected column width below is plain arithmetic.
public sealed class TableAutoLayoutTests
{
    private const string Cells = "<style>td,th{background-color:#eef3f7}th{font-weight:normal}</style>";

    private static string Table(string rows, string tableStyle = "font-size:10pt", string head = "")
        => $"{Cells}<table style='{tableStyle}'>{head}<tbody>{rows}</tbody></table>";

    // The painted cell backgrounds of the first row: x and width of each.
    private static (double X, double Width)[] FirstRow(string html, PdfOptions? options = null)
    {
        var page = TableLayoutTests.Layout(html, options).First();
        var top = page.PaintBoxes.Min(box => box.Top);
        return [.. page.PaintBoxes.Where(box => box.Top == top).Select(box => (box.X, box.Width))];
    }

    private static double[] Widths(string html, PdfOptions? options = null) => [.. FirstRow(html, options).Select(cell => cell.Width)];

    [Test]
    public void AutoWidthTableShrinksToItsColumnsMaxContent()
    {
        var cells = FirstRow(Table("<tr><td style='padding:2pt;border:1pt solid red'>AB</td><td>ABCD</td></tr><tr><td>A</td><td>A B C D E</td></tr>"));
        // Column 1: "AB" 12 + padding 4 + borders 2 = 18. Column 2: "A B C D E" 9 glyphs = 54.
        Assert.That(cells, Is.EqualTo(new[] { (36d, 18d), (54d, 54d) }));
    }

    [Test]
    public void WidthBetweenMinimumAndMaximumGrowsColumnsByTheirMaxMinusMinRange()
    {
        // Column 1: min "AAAA" 24, max "AAAA BBBB" 54. Column 2: min 12, max "CC DD EE FF" 66.
        // 100pt shares 64pt over the ranges 30 and 54: 24 + 64 * 30 / 84 and the remainder.
        var widths = Widths(Table("<tr><td>AAAA BBBB</td><td>CC DD EE FF</td></tr>", "font-size:10pt;width:100pt"));
        Assert.That(widths[0], Is.EqualTo(24 + 64d * 30 / 84).Within(1e-9));
        Assert.That(widths[1], Is.EqualTo(100 - (24 + 64d * 30 / 84)).Within(1e-9));
        Assert.That(widths.Sum(), Is.EqualTo(100).Within(1e-9));
    }

    [Test]
    public void AutoWidthTableWiderThanItsContainerWrapsAtTheAvailableWidth()
    {
        // A 120pt container: min 24 + 12, max 54 + 66, so 120 is exactly the sum of the maximums.
        Assert.That(Widths("<div style='width:120pt'>" + Table("<tr><td>AAAA BBBB</td><td>CC DD EE FF</td></tr>") + "</div>"),
            Is.EqualTo(new[] { 54d, 66d }));
        var narrow = Widths("<div style='width:100pt'>" + Table("<tr><td>AAAA BBBB</td><td>CC DD EE FF</td></tr>") + "</div>");
        Assert.That(narrow[0], Is.EqualTo(24 + 64d * 30 / 84).Within(1e-9));
        var page = TableLayoutTests.Layout("<div style='width:100pt'>" + Table("<tr><td>AAAA BBBB</td><td>CC DD EE FF</td></tr>") + "</div>").Single();
        Assert.That(page.Runs.Select(run => run.Text), Is.EqualTo(new[] { "AAAA", "BBBB", "CC DD EE", "FF" }));
    }

    [Test]
    public void WidthAboveTheMaximumGoesToColumnsInProportionToTheirMaximums()
    {
        var widths = Widths(Table("<tr><td>AB</td><td>ABCD</td></tr>", "font-size:10pt;width:200pt"));
        Assert.That(widths[0], Is.EqualTo(12 + 164d * 12 / 36).Within(1e-9));
        Assert.That(widths[1], Is.EqualTo(200 - (12 + 164d * 12 / 36)).Within(1e-9));
        // width:100% of the 523.28pt A4 content width.
        Assert.That(Widths(Table("<tr><td>AB</td><td>ABCD</td></tr>", "font-size:10pt;width:100%")).Sum(), Is.EqualTo(523.28).Within(1e-9));
    }

    [Test]
    public void ExplicitWidthBelowTheMinimumGrowsToTheMinimum()
        => Assert.That(Widths(Table("<tr><td>AB CD</td><td>ABCD</td></tr>", "font-size:10pt;width:10pt")), Is.EqualTo(new[] { 12d, 24d }));

    [Test]
    public void ColumnsThatCannotFitTheContainerFailWithTheirMinimum()
    {
        var html = Table("<tr><td>" + new string('W', 100) + "</td></tr>");
        TableStructureTests.Fails(html, "FPDF1302");
        var error = Assert.Throws<FactsPdfException>(() => TableLayoutTests.Layout(html))!;
        Assert.That(error.Message, Does.Contain("need at least 600pt, but only 523.28pt is available"));
    }

    [Test]
    public void SpanningCellWiderThanItsColumnsGrowsThemInProportionToTheirMaximums()
    {
        // Columns 12 and 24 wide; the 60pt spanning cell adds 24pt: 12 + 24 * 12 / 36 = 20 and 40.
        var html = Table("<tr><td colspan=2>ABCDEFGHIJ</td></tr><tr><td>AB</td><td>ABCD</td></tr>");
        var page = TableLayoutTests.Layout(html).Single();
        Assert.That(page.PaintBoxes.Select(box => (box.X, box.Width)), Is.EqualTo(new[] { (36d, 60d), (36d, 20d), (56d, 40d) }));
    }

    [Test]
    public void SpanningCellMinimumIsSharedByTheColumnsMaxMinusMinRanges()
    {
        // Minimums 12 and 24, maximums 30 and 54: the 42pt unbreakable spanning cell shares its 6pt
        // over the ranges 18 and 30, so the minimums become 14.25 and 27.75.
        var html = Table("<tr><td>AB CD</td><td>ABCD EFGH</td></tr><tr><td colspan=2>ABCDEFG</td></tr>", "font-size:10pt;width:10pt");
        Assert.That(Widths(html), Is.EqualTo(new[] { 14.25, 27.75 }));
        // At auto width the columns take their unchanged maximums.
        Assert.That(Widths(Table("<tr><td>AB CD</td><td>ABCD EFGH</td></tr><tr><td colspan=2>ABCDEFG</td></tr>")), Is.EqualTo(new[] { 30d, 54d }));
    }

    [Test]
    public void SpanningFooterTotalRendersUnderItsColumns()
    {
        var html = Cells + "<table style='font-size:10pt'><tbody><tr><td>A1</td><td>Paper</td><td>18.40</td></tr></tbody>"
            + "<tfoot><tr><td colspan=2>Total</td><td>18.40</td></tr></tfoot></table>";
        var boxes = TableLayoutTests.Layout(html).Single().PaintBoxes;
        Assert.That(boxes.Select(box => (box.X, box.Width)), Is.EqualTo(new[] { (36d, 12d), (48d, 30d), (78d, 30d), (36d, 42d), (78d, 30d) }));
    }

    [Test]
    public void ExplicitWidthWiderThanTheContainerFailsAsATableWidthError()
    {
        var html = Table("<tr><td>A</td></tr>", "font-size:10pt;width:600pt");
        TableStructureTests.Fails(html, "FPDF1302");
        Assert.That(Assert.Throws<FactsPdfException>(() => TableLayoutTests.Layout(html))!.Message, Does.Contain("Table outer width"));
    }

    [Test]
    public void TableOfEmptyUndecoratedColumnsFailsAsZeroWidth()
        => TableStructureTests.Fails(Table("<tr><td></td><td></td></tr>"), "FPDF1302");

    [Test]
    public void HeaderFooterBreaksParagraphsAndNoWrapCountTowardsTheirColumn()
    {
        var html = Cells + "<table style='font-size:10pt'><thead><tr><th>ABC</th><th>A</th></tr></thead><tbody>"
            + "<tr><td>AB<br>ABCDEFG</td><td><p>ABCD</p><p style='white-space:nowrap'>A B C D E F</p></td></tr></tbody>"
            + "<tfoot><tr><td>ABCDEFGH IJ</td><td>A</td></tr></tfoot></table>";
        // Column 1 max: footer "ABCDEFGH IJ" 66; column 2: the nowrap paragraph 66.
        Assert.That(Widths(html), Is.EqualTo(new[] { 66d, 66d }));
        // 120pt: column 1 takes all 6pt above the minimums; column 2 has no max-minus-min range.
        Assert.That(Widths(html.Replace("font-size:10pt'", "font-size:10pt;width:120pt'", StringComparison.Ordinal)),
            Is.EqualTo(new[] { 54d, 66d }));
    }

    [Test]
    public void BreakAllMinimumIsTheWidestGlyph()
        => Assert.That(Widths(Table("<tr><td style='word-break:break-all'>ABCDEFGH</td><td>ABCDEFGH</td></tr>", "font-size:10pt;width:60pt")),
            Is.EqualTo(new[] { 12d, 48d }));

    [Test]
    public void WordBreakBreakWordShrinksTheMinimumButOverflowWrapBreakWordDoesNot()
    {
        // break-word overflow wrapping keeps the 48pt word as the column minimum; word-break:break-word
        // makes its emergency breaks count, so the minimum is one 6pt glyph. 60pt: 6 + 6 * 42 / 42.
        Assert.That(Widths(Table("<tr><td style='overflow-wrap:break-word'>ABCDEFGH</td><td>ABCDEFGH</td></tr>", "font-size:10pt;width:60pt")),
            Is.EqualTo(new[] { 48d, 48d }));
        Assert.That(Widths(Table("<tr><td style='word-break:break-word'>ABCDEFGH</td><td>ABCDEFGH</td></tr>", "font-size:10pt;width:60pt")),
            Is.EqualTo(new[] { 12d, 48d }));
        // overflow-wrap:anywhere wraps like break-word but its breaks count for min-content.
        Assert.That(Widths(Table("<tr><td style='overflow-wrap:anywhere'>ABCDEFGH</td><td>ABCDEFGH</td></tr>", "font-size:10pt;width:60pt")),
            Is.EqualTo(new[] { 12d, 48d }));
        Assert.That(Widths(Table("<tr><td style='word-wrap:anywhere'>ABCDEFGH</td><td>ABCDEFGH</td></tr>", "font-size:10pt;width:60pt")),
            Is.EqualTo(new[] { 12d, 48d }));
        var page = TableLayoutTests.Layout(Table("<tr><td style='word-break:break-word'>ABCDEFGH</td><td>ABCDEFGH</td></tr>", "font-size:10pt;width:60pt")).Single();
        Assert.That(page.Runs.Select(run => run.Text), Is.EqualTo(new[] { "AB", "CD", "EF", "GH", "ABCDEFGH" }));
    }

    [Test]
    public void FirstLineIndentCountsOnTheFirstLineAndItsFirstWord()
        => Assert.That(Widths(Table("<tr><td style='text-indent:30pt'>AB CDE</td><td>A</td></tr>")), Is.EqualTo(new[] { 66d, 6d }));

    [Test]
    public void ImageCellsUseTheImageOuterWidth()
    {
        var html = Table("<tr><td><img src='logo' style='padding:2pt'></td><td>AB</td></tr>");
        Assert.That(Widths(html, TableImageTests.Options()), Is.EqualTo(new[] { 28d, 12d }));
        TableStructureTests.Fails(html.Replace("padding:2pt", "width:50%", StringComparison.Ordinal), "FPDF1302", TableImageTests.Options());
    }

    [Test]
    public void EmptyColumnHasZeroWidth()
    {
        var page = TableLayoutTests.Layout(Table("<tr><td></td><td>AB</td></tr><tr><td><br></td><td>A</td></tr>")).First();
        Assert.That(page.PaintBoxes.Select(box => (box.X, box.Width)), Is.EqualTo(new[] { (36d, 12d), (36d, 12d) }));
    }

    [Test]
    public void ColgroupOfWidthlessColumnsRendersLikeNoColgroup()
    {
        var rows = "<tr><td>AB</td><td>ABCD EF</td></tr>";
        Assert.That(TableLayoutTests.Pdf(Table(rows, head: "<colgroup><col><col style='width:auto'></colgroup>")),
            Is.EqualTo(TableLayoutTests.Pdf(Table(rows))));
    }

    [Test]
    public void LongAutoTableRepeatsItsHeaderWithTheSameColumns()
    {
        var rows = string.Concat(Enumerable.Range(0, 80).Select(i => $"<tr><td>R{i:000}</td><td>Line item {i}</td></tr>"));
        var html = Cells + "<table style='font-size:10pt'><thead><tr><th>Id</th><th>Description of the item</th></tr></thead><tbody>" + rows + "</tbody></table>";
        var pages = TableLayoutTests.Layout(html);
        Assert.That(pages, Has.Count.GreaterThan(1));
        foreach (var page in pages)
        {
            var top = page.PaintBoxes.Min(box => box.Top);
            Assert.That(page.PaintBoxes.Where(box => box.Top == top).Select(box => (box.X, box.Width)),
                Is.EqualTo(new[] { (36d, 24d), (60d, 138d) }));
            Assert.That(page.Runs[0].Text, Is.EqualTo("Id"));
        }
    }

    [Test]
    public void FixedColumnsAreUnchangedByAutomaticLayout()
    {
        var html = Cells + "<table style='width:100pt;font-size:10pt'><colgroup><col style='width:30pt'><col style='width:70pt'></colgroup>"
            + "<tbody><tr><td>A</td><td>ABCDEFGHIJ</td></tr></tbody></table>";
        Assert.That(Widths(html), Is.EqualTo(new[] { 30d, 70d }));
    }

    [TestCase("<colgroup><col style='width:30pt'><col></colgroup><tbody><tr><td>A</td><td>B</td></tr></tbody>", "explicit and automatic")]
    [TestCase("<tbody><tr><td style='padding:5%'>A</td></tr></tbody>", "Percentage cell padding")]
    [TestCase("<tbody><tr><td style='text-indent:10%'>A</td></tr></tbody>", "Percentage text-indent")]
    public void UnsupportedAutomaticLayoutFailsWithAClearMessage(string content, string message)
    {
        var html = $"<table style='font-size:10pt'>{content}</table>";
        TableStructureTests.Fails(html, "FPDF1302");
        Assert.That(Assert.Throws<FactsPdfException>(() => TableLayoutTests.Layout(html))!.Message, Does.Contain(message));
    }

    [TestCase("<tbody><tr><td>A</td><td>B</td></tr><tr><td>A</td></tr></tbody>")]
    [TestCase("<tbody><tr><td>A</td></tr><tr><td>A</td><td>B</td></tr></tbody>")]
    [TestCase("<thead><tr><th>A</th></tr></thead><tbody><tr><td>A</td><td>B</td></tr></tbody>")]
    [TestCase("<tbody><tr></tr></tbody>")]
    [TestCase("<tbody><tr><td colspan=17>A</td></tr></tbody>")]
    public void FirstRowSetsTheColumnCountForEveryRow(string content)
        => TableStructureTests.Fails($"<table>{content}</table>", content.Contains("colspan", StringComparison.Ordinal) ? "FPDF1103" : "FPDF1101");

    [Test]
    public void FirstRowAcceptsSixteenColumnsButNotSeventeen()
    {
        string Row(int cells) => "<table><tbody><tr>" + string.Concat(Enumerable.Repeat("<td>A</td>", cells)) + "</tr></tbody></table>";
        Assert.That(TableLayoutTests.Layout(Row(16)).Single().Runs, Has.Count.EqualTo(16));
        TableStructureTests.Fails(Row(17), "FPDF1101");
    }

    [Test]
    public void IntrinsicWidthsFollowTheWrapperBreakRules()
    {
        static (double, double) Measure(string style, string text)
        {
            var textStyle = new TextStyle(10, 1.2, new Rgb(0, 0, 0), TextAlignment.Left);
            if (style.Contains("nowrap", StringComparison.Ordinal)) textStyle.NoWrap = true;
            if (style.Contains("pre-wrap", StringComparison.Ordinal)) textStyle.PreserveSpaces = true;
            if (style.Contains("break-all", StringComparison.Ordinal)) textStyle.BreakAll = true;
            if (style.Contains("indent", StringComparison.Ordinal)) textStyle.FirstLineIndent = new CssLength(12);
            var paragraph = new Paragraph(textStyle, 0);
            foreach (var part in text.Split('|'))
                paragraph.Runs.Add(part.Length == 0 ? new TextRun("", textStyle, true) : new TextRun(part, textStyle));
            return TextLines.IntrinsicWidths(paragraph, new PdfOptions(), new(), default);
        }
        Assert.Multiple(() =>
        {
            Assert.That(Measure("", "AB  CDE   F"), Is.EqualTo((18d, 48d)));
            Assert.That(Measure("", "AB||CDEF G"), Is.EqualTo((24d, 36d)));
            Assert.That(Measure("nowrap", "AB CD||A"), Is.EqualTo((30d, 30d)));
            Assert.That(Measure("break-all", "ABCD EF"), Is.EqualTo((6d, 42d)));
            Assert.That(Measure("pre-wrap", "AB   C"), Is.EqualTo((12d, 36d)));
            Assert.That(Measure("indent", "AB CDE||ABCDEFGH"), Is.EqualTo((48d, 48d)));
            Assert.That(Measure("indent", "ABCDE"), Is.EqualTo((42d, 42d)));
            Assert.That(Measure("", ""), Is.EqualTo((0d, 0d)));
        });
    }
}
