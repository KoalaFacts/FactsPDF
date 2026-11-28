using NUnit.Framework;

namespace FactsPDF.Tests;

// table-layout:fixed sizes the columns from the table width, the col widths and the first row only;
// every expected width below was compared with Chrome 158 (cell offsets within 0.75pt). The page is
// the default A4, so tables of up to 500pt fit.
public sealed class TableFixedLayoutTests
{
    private const string Cells = "<style>td{background-color:#eef3f7;padding:0}</style>";

    private static string Table(string firstRow, string tableStyle, string cols = "", string secondRow = "<td>A</td><td>B</td><td>C</td>")
        => $"{Cells}<table style='table-layout:fixed;border-spacing:0;font-size:10pt;{tableStyle}'>{(cols.Length == 0 ? "" : "<colgroup>" + cols + "</colgroup>")}"
            + $"<tbody><tr>{firstRow}</tr><tr>{secondRow}</tr></tbody></table>";

    private static double[] Widths(string html)
    {
        // The second row, whose cells always cover every column.
        var page = TableLayoutTests.Layout(html).First();
        var top = page.PaintBoxes.Select(box => box.Top).Distinct().Order().ElementAt(1);
        return [.. page.PaintBoxes.Where(box => box.Top == top).OrderBy(box => box.X).Select(box => box.Width)];
    }

    private static void Expect(string html, params double[] widths)
    {
        var actual = Widths(html);
        Assert.That(actual, Has.Length.EqualTo(widths.Length));
        for (var i = 0; i < widths.Length; i++) Assert.That(actual[i], Is.EqualTo(widths[i]).Within(0.01), $"column {i}");
    }

    [Test]
    public void ColumnsWithoutWidthsShareTheTableWidthEqually() =>
        Expect(Table("<td>x</td><td>y</td><td>z</td>", "width:300pt"), 100, 100, 100);

    [Test]
    public void AFirstRowCellWidthSetsItsColumnAndTheRestShareWhatIsLeft() =>
        Expect(Table("<td style='width:50pt'>x</td><td>y</td><td>z</td>", "width:300pt"), 50, 125, 125);

    [Test]
    public void PercentagesAreOfTheTableWidth() =>
        Expect(Table("<td style='width:20%'>x</td><td>y</td><td>z</td>", "width:300pt"), 60, 120, 120);

    [Test]
    public void ColWidthsWinOverFirstRowCells()
    {
        Expect(Table("<td style='width:99pt'>x</td><td>y</td><td>z</td>", "width:300pt", "<col style='width:40pt'><col><col>"), 40, 130, 130);
        Expect(Table("<td>x</td><td>y</td><td>z</td>", "width:300pt", "<col style='width:25%'><col><col>"), 75, 112.5, 112.5);
    }

    [Test]
    public void ASpanningCellDividesItsWidthOverTheColumnsItCovers()
    {
        Expect(Table("<td colspan='2' style='width:100pt'>x</td><td>z</td>", "width:300pt"), 50, 50, 200);
        Expect(Table("<td colspan='2' style='width:40%'>x</td><td>z</td>", "width:300pt"), 60, 60, 180);
    }

    [Test]
    public void ACellWidthIsItsContentBoxUnlessBorderBox()
    {
        Expect(Table("<td style='width:50pt;padding:5pt'>x</td><td>y</td><td>z</td>", "width:300pt"), 60, 120, 120);
        Expect(Table("<td style='width:50pt;padding:5pt;box-sizing:border-box'>x</td><td>y</td><td>z</td>", "width:300pt"), 50, 125, 125);
        // A percentage is of the table width and the cell's padding comes on top of it.
        Expect(Table("<td style='width:20%;padding:5pt'>x</td><td>y</td><td>z</td>", "width:300pt"), 70, 115, 115);
    }

    [Test]
    public void WidthsShortOfTheTableWidthGrowInProportion()
    {
        Expect(Table("<td style='width:50pt'>x</td><td style='width:100pt'>y</td><td style='width:30pt'>z</td>", "width:300pt"),
            50 * 300d / 180, 100 * 300d / 180, 30 * 300d / 180);
        Expect(Table("<td style='width:30%'>x</td><td style='width:30%'>y</td><td style='width:30%'>z</td>", "width:300pt"), 100, 100, 100);
    }

    [Test]
    public void ColumnsWiderThanTheTableWidthGrowTheTable()
    {
        Expect(Table("<td style='width:150pt'>x</td><td style='width:120pt'>y</td>", "width:200pt", "", "<td>A</td><td>B</td>"), 150, 120);
        Expect(Table("<td style='width:150pt'>x</td><td style='width:120pt'>y</td><td style='width:30pt'>z</td>", "width:200pt"), 150, 120, 30);
    }

    [Test]
    public void PercentagesOverOneHundredShareTheTableInProportion() =>
        Expect(Table("<td style='width:60%'>x</td><td style='width:60%'>y</td><td style='width:60%'>z</td>", "width:300pt"), 100, 100, 100);

    [Test]
    public void LaterRowsNeverSetWidths() =>
        Expect(Table("<td>x</td><td>y</td><td>z</td>", "width:300pt", "", "<td style='width:200pt'>A</td><td>B</td><td>C</td>"), 100, 100, 100);

    [Test]
    public void AnAutoTableWidthKeepsAutomaticLayout()
    {
        var fixedAuto = Widths(Table("<td style='width:50pt'>x</td><td>y</td><td>z</td>", ""));
        var automatic = Widths(Table("<td style='width:50pt'>x</td><td>y</td><td>z</td>", "table-layout:auto"));
        Assert.That(fixedAuto, Is.EqualTo(automatic));
    }

    [Test]
    public void TableLayoutAutoIsTheInitialValueAndInheritReadsTheParent()
    {
        var plain = Widths(Cells + "<table style='border-spacing:0;font-size:10pt;width:300pt'><tbody><tr><td style='width:50pt'>x</td><td>y</td></tr><tr><td>A</td><td>B</td></tr></tbody></table>");
        var auto = Widths(Cells + "<table style='table-layout:auto;border-spacing:0;font-size:10pt;width:300pt'><tbody><tr><td style='width:50pt'>x</td><td>y</td></tr><tr><td>A</td><td>B</td></tr></tbody></table>");
        Assert.That(auto, Is.EqualTo(plain));
        var inherited = Widths("<style>td{background-color:#eef3f7;padding:0}</style><div style='table-layout:fixed'><table style='table-layout:inherit;border-spacing:0;font-size:10pt;width:300pt'><tbody><tr><td style='width:50pt'>x</td><td>y</td></tr><tr><td>A</td><td>B</td></tr></tbody></table></div>");
        Assert.That(inherited, Is.EqualTo(new[] { 50d, 250d }).Within(0.01));
    }

    [Test]
    public void MinAndMaxWidthLimitTheTableWidthFirst()
    {
        Expect(Table("<td style='width:50pt'>x</td><td>y</td><td>z</td>", "width:300pt;max-width:200pt"), 50, 75, 75);
        Expect(Table("<td style='width:50pt'>x</td><td>y</td><td>z</td>", "width:100pt;min-width:250pt"), 50, 100, 100);
    }

    [Test]
    public void CollapsedTablesSplitTheWidthLessTheOuterHalfBorders()
    {
        const string collapsed = "<style>td{background-color:#eef3f7;padding:0;border:2px solid #000}</style><table style='table-layout:fixed;border-collapse:collapse;font-size:10pt;width:300pt'><tbody><tr>";
        var widths = Widths(collapsed + "<td style='width:50pt'>x</td><td>y</td><td>z</td></tr><tr><td>A</td><td>B</td><td>C</td></tr></tbody></table>");
        // The first column is 50pt plus its half borders; the other two share the rest of the 300pt less the outer halves.
        Assert.That(widths, Is.EqualTo(new[] { 51.5, 123.5, 123.5 }).Within(0.01));
    }

    [Test]
    public void AZeroWidthColumnWithContentFailsInsteadOfOverflowing()
    {
        // Chrome lets the text overflow a column that has no width left; FactsPDF never paints outside a cell.
        var error = Assert.Throws<FactsPdfException>(() => Widths(
            Table("<td style='width:250pt'>x</td><td style='width:100pt'>y</td><td>z</td>", "width:300pt")));
        Assert.That(error!.Code, Is.EqualTo("FPDF1302"));
    }

    [TestCase("table-layout:stretch")]
    [TestCase("table-layout:fixed auto")]
    public void InvalidValuesFail(string css)
    {
        var error = Assert.Throws<FactsPdfException>(() => Widths(Table("<td>x</td>", "width:300pt;" + css, "", "<td>A</td>")));
        Assert.That(error!.Code, Is.EqualTo("FPDF1202"));
    }

    [Test]
    public void OtherElementsIgnoreTableLayout()
    {
        const string body = "<p>x</p><ul><li>a</li></ul><span>y</span>";
        using var a = new MemoryStream();
        using var b = new MemoryStream();
        PdfConverter.Convert("<style>*{table-layout:fixed}</style>" + body, a, new PdfOptions());
        PdfConverter.Convert(body, b, new PdfOptions());
        Assert.That(a.ToArray(), Is.EqualTo(b.ToArray()));
    }
}
