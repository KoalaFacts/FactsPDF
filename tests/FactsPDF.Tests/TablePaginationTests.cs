using NUnit.Framework;

namespace FactsPDF.Tests;

public sealed class TablePaginationTests
{
    internal static readonly PdfOptions Page = new() { PageWidth = 240, PageHeight = 100, Margin = 10 };
    internal static string Table(int count, bool header = true, string tableStyle = "", string cellStyle = "", string? content = null)
        => "<style>td,th{font-weight:normal;" + cellStyle + "}</style><table style='width:100pt;font-size:10pt;line-height:2;" + tableStyle
        + "'><colgroup><col style='width:100%'></colgroup>"
        + (header ? "<thead><tr><th>HEAD</th></tr></thead>" : "") + "<tbody>"
        + string.Concat(Enumerable.Range(1, count).Select(i => $"<tr><td>{content ?? $"R{i:000}"}</td></tr>")) + "</tbody></table>";
    private static string Before(int lines) => "<p style='font-size:10pt;line-height:2;margin-top:0;margin-bottom:0'>" + string.Join("<br>", Enumerable.Repeat("BEFORE", lines)) + "</p>";

    [TestCase(true, 4)] [TestCase(false, 3)]
    public void LongTableUsesWholeRowsAndRepeatsOnlyHeader(bool header, int pageCount)
    {
        var pages = TableLayoutTests.Layout(Table(10, header), Page);
        Assert.That(pages, Has.Count.EqualTo(pageCount));
        Assert.That(pages.SelectMany(p => p.Runs).Where(r => r.Text != "HEAD").Select(r => r.Text),
            Is.EqualTo(Enumerable.Range(1, 10).Select(i => $"R{i:000}")));
        foreach (var page in pages)
        {
            Assert.That(page.Runs.Count(r => r.Text == "HEAD"), Is.EqualTo(header ? 1 : 0));
            Assert.That(page.Runs[0].Baseline, Is.EqualTo(77).Within(.000001));
            Assert.That(page.Runs[^1].Baseline, Is.GreaterThanOrEqualTo(15));
        }
    }

    [TestCase(1, 2)] [TestCase(2, 1)] [TestCase(3, 0)]
    public void PageTailFitsHeaderWithBodyOrMovesBoth(int precedingLines, int firstPageRows)
    {
        var pages = TableLayoutTests.Layout(Before(precedingLines) + Table(7), Page);
        Assert.That(pages[0].Runs.Count(r => r.Text.StartsWith('R')), Is.EqualTo(firstPageRows));
        Assert.That(pages[0].Runs.Count(r => r.Text == "HEAD"), Is.EqualTo(firstPageRows == 0 ? 0 : 1));
        Assert.That(pages.SelectMany(p => p.Runs).Count(r => r.Text.StartsWith('R')), Is.EqualTo(7));
    }

    [TestCase(1)] [TestCase(3)]
    public void LeadingZeroHeightRowsCannotLeaveAnOrphanHeader(int emptyRows)
    {
        var html = Table(8);
        for (var i = 1; i <= emptyRows; i++) html = html.Replace($"R{i:000}", "");
        var pages = TableLayoutTests.Layout(Before(3) + html, Page);
        Assert.That(pages[0].Runs.Select(r => r.Text), Is.EqualTo(new[] { "BEFORE", "BEFORE", "BEFORE" }));
        Assert.That(pages[1].Runs[0].Text, Is.EqualTo("HEAD"));
        Assert.That(pages.SelectMany(p => p.Runs).Count(r => r.Text.StartsWith('R')), Is.EqualTo(8-emptyRows));
    }

    [Test]
    public void TrailingZeroHeightRowsReserveBottomWithLastOccupiedRow()
    {
        var html = Table(4, tableStyle: "padding-bottom:1pt").Replace("R004", "");
        var pages = TableLayoutTests.Layout(html, Page);
        Assert.That(pages.Select(p => string.Join(',', p.Runs.Select(r => r.Text))),
            Is.EqualTo(new[] { "HEAD,R001,R002", "HEAD,R003" }));
    }

    [TestCase(false, 2)] [TestCase(true, 3)]
    public void FirstAndLastDecorationNeedNotFitOnTheSamePage(bool header, int count)
    {
        var html = Table(5, header, tableStyle: "line-height:1;padding-top:50pt;padding-bottom:50pt;background-color:white");
        var pages = TableLayoutTests.Layout(html, Page);
        Assert.That(pages, Has.Count.EqualTo(count));
        Assert.That(pages.SelectMany(p => p.Runs).Where(r => r.Text != "HEAD").Select(r => r.Text),
            Is.EqualTo(Enumerable.Range(1,5).Select(i => $"R{i:000}")));
        Assert.That(pages.SelectMany(p => p.PaintBoxes).All(b => b.Top >= 10 && b.Top + b.Height <= 90), Is.True);
    }

    [Test]
    public void ShortTableStillMovesWholeEvenWhenOneRowWouldFit()
    {
        var pages = TableLayoutTests.Layout(Before(2) + Table(2), Page);
        Assert.That(pages, Has.Count.EqualTo(2));
        Assert.That(pages[0].Runs.Select(r => r.Text), Is.EqualTo(new[] { "BEFORE", "BEFORE" }));
        Assert.That(pages[1].Runs.Select(r => r.Text), Is.EqualTo(new[] { "HEAD", "R001", "R002" }));
    }

    [Test]
    public void WrappedVariableRowsRemainWhole()
    {
        var html = Table(4).Replace("R002", "A<br>B");
        var pages = TableLayoutTests.Layout(html, Page);
        Assert.That(pages.Select(p => string.Join(',', p.Runs.Select(r => r.Text))),
            Is.EqualTo(new[] { "HEAD,R001,A,B", "HEAD,R003,R004" }));
    }

    [Test]
    public void TableAndAncestorDecorationsAreSlicedWithoutRepeatingTopPadding()
    {
        var html = "<div style='padding-top:4pt;border:1pt solid red'>"
            + Table(6, tableStyle: "padding:2pt;border:1pt solid blue;background-color:white", cellStyle: "background-color:#eeeeee") + "</div>";
        var pages = TableLayoutTests.Layout(html, Page);
        Assert.That(pages, Has.Count.EqualTo(3));
        var fragments = pages.Select(p => p.PaintBoxes.Single(b => b.Style.BackgroundColor == new Rgb(1, 1, 1))).ToArray();
        Assert.That(fragments.Select(b => b.Top), Is.EqualTo(new double[] { 15, 10, 10 }));
        Assert.That(fragments.Select(b => b.IsFirstFragment), Is.EqualTo(new[] { true, false, false }));
        Assert.That(fragments.Select(b => b.IsLastFragment), Is.EqualTo(new[] { false, false, true }));
        Assert.That(pages.Select(p => p.Runs[0].Baseline), Is.EqualTo(new double[] { 69, 77, 77 }));
        Assert.That(pages.SelectMany(p => p.PaintBoxes).All(b => b.Top >= 10 && b.Top + b.Height <= 90), Is.True);
    }

    [TestCase(false)] [TestCase(true)]
    public void OversizeRowOrHeaderCombinationFailsAtomically(bool header)
    {
        var html = Table(2, header, content: "A<br>B<br>C<br>D" + (header ? "" : "<br>E"));
        TableStructureTests.Fails(html, "FPDF1302", Page);
    }

    [Test]
    public void RepeatedHeaderAndFragmentPaintHaveExactCommandBoundary()
    {
        var html = Table(7, tableStyle: "background-color:white", cellStyle: "background-color:red");
        var expected = TableLayoutTests.Pdf(html, Page);
        // 7 body + 3 headers: 10 text + 10 cell backgrounds + 3 table backgrounds.
        Assert.That(TableLayoutTests.Pdf(html, Page with { MaxDisplayCommands = 23 }), Is.EqualTo(expected));
        TableStructureTests.Fails(html, "FPDF1401", Page with { MaxDisplayCommands = 22 });
        FailsLimit(html, "FPDF1303", Page with { MaxPages = 2 });
        FailsLimit(html, "FPDF1401", Page with { MaxOutputBytes = expected.Length - 1 });
    }

    private static void FailsLimit(string html, string code, PdfOptions options)
    {
        using var output = new MemoryStream(); output.Write("sentinel"u8); output.Position = 2;
        Assert.That(Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, options))!.Code, Is.EqualTo(code));
        Assert.That(output.ToArray(), Is.EqualTo("sentinel"u8.ToArray()));
        Assert.That(output.Position, Is.EqualTo(2));
    }

    [Test]
    public void EmptyRowsAndHeaderDoNotCreateGhostPages()
    {
        var html = Table(5).Replace("HEAD", "").Replace("R002", "").Replace("R004", "");
        Assert.That(TableLayoutTests.Layout(html, Page), Has.Count.EqualTo(1));
        var longHtml = Table(10).Replace("HEAD", "").Replace("R004", "").Replace("R005", "");
        var pages = TableLayoutTests.Layout(longHtml, Page);
        Assert.That(pages, Has.Count.EqualTo(2));
        Assert.That(pages.SelectMany(p => p.Runs).Count(), Is.EqualTo(8));
    }
}
