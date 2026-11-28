using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// A table directly inside a table cell, laid out as the cell's content the way a browser prints it:
/// the inner table takes the cell's content width (automatic layout inside it), and the outer row
/// grows to fit it. The expected distances and positions were measured against a browser's printed
/// PDF of the same markup (ten point text on 20pt lines, cells aligned to the top).
/// </summary>
[TestFixture]
public sealed class NestedTableTests
{
    private const string Css = "<style>html,body{margin:0;padding:0}body{font-size:10pt;line-height:20pt}" +
        "p,ul,ol,li,table,div,h2{margin:0;padding:0}table{border-collapse:separate;border-spacing:0}td,th{padding:0;vertical-align:top}</style>";

    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 400, Margin = 20 };

    private static List<LayoutPage> Layout(string html, PdfOptions? options = null)
    {
        var settings = options ?? Page;
        return TextLayout.Layout(HtmlDocumentReader.ReadTree(Css + html, settings, default), settings, default);
    }

    private static PlacedText Find(List<LayoutPage> pages, string text, out int page)
    {
        for (var i = 0; i < pages.Count; i++)
        {
            var run = pages[i].Runs.FirstOrDefault(r => r.Text == text);
            if (run is not null) { page = i; return run; }
        }
        throw new AssertionException($"No run '{text}'.");
    }

    private static double Distance(string html, string first = "AAA", string second = "BBB")
    {
        var pages = Layout(html);
        var a = Find(pages, first, out var pageA);
        var b = Find(pages, second, out var pageB);
        Assert.That(pageB, Is.EqualTo(pageA));
        return a.Baseline - b.Baseline;
    }

    // AAA, a table with one cell holding the markup, BBB.
    private static string Around(string cell, string table = "width:100pt")
        => $"<p>AAA</p><table style='{table}'><tr><td>{cell}</td></tr></table><p>BBB</p>";

    private static FactsPdfException Fails(string html, string code, bool strict = true)
    {
        using var output = new MemoryStream();
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(Css + html, output, Page with { Strict = strict }));
        Assert.That(error!.Code, Is.EqualTo(code), error.Message);
        Assert.That(output.Length, Is.Zero);
        return error;
    }

    [Test]
    public void TheInnerRowsAreTheCellContentAndTheOuterRowTakesTheirHeight()
        => Assert.That(Distance(Around("<table><tr><td>T1</td></tr><tr><td>T2</td></tr></table>")), Is.EqualTo(20 + 20 + 20).Within(0.75));

    [Test]
    public void TextBeforeAndAfterTheInnerTableFormsItsOwnLines()
    {
        const string cell = "xx<table><tr><td>T1</td></tr></table>yy";
        Assert.That(Distance(Around(cell)), Is.EqualTo(20 + 20 + 20 + 20).Within(0.75));
        var pages = Layout(Around(cell));
        Assert.That(Find(pages, "xx", out _).Baseline - Find(pages, "T1", out _).Baseline, Is.EqualTo(20).Within(0.75));
        Assert.That(Find(pages, "T1", out _).Baseline - Find(pages, "yy", out _).Baseline, Is.EqualTo(20).Within(0.75));
        // Lines and line breaks around it stay lines of the cell.
        Assert.That(Distance(Around("a<br>b<table><tr><td>T1</td></tr></table>c<br>d")), Is.EqualTo(20 + 5 * 20).Within(0.75));
    }

    [Test]
    public void TwoInnerTablesStackWithTheTextBetweenThem()
        => Assert.That(Distance(Around("<table><tr><td>T1</td></tr></table>mid<table><tr><td>T2</td></tr></table>")),
            Is.EqualTo(20 + 3 * 20).Within(0.75));

    // Over-correction: the cell's own padding stays around the inner table (the inner table is
    // placed in the content box), it is neither dropped nor doubled.
    [Test]
    public void CellPaddingStaysAroundTheInnerTable()
    {
        const string html = "<p>AAA</p><table style='width:100pt'><tr><td style='padding:6pt 4pt'><table><tr><td>T1</td></tr></table></td></tr></table><p>BBB</p>";
        Assert.That(Distance(html), Is.EqualTo(20 + 6 + 20 + 6).Within(0.75));
        Assert.That(Find(Layout(html), "T1", out _).X, Is.EqualTo(20 + 4).Within(0.75));
    }

    [Test]
    public void AnInnerTableOfFullWidthWrapsAtTheCellContentWidth()
    {
        var pages = Layout(Around("<table style='width:100%'><tr><td>aaa bbb ccc ddd eee fff</td></tr></table>"));
        // 100pt holds four of the six words, so the text takes two lines inside the cell.
        Assert.That(Find(pages, "aaa bbb ccc ddd", out _).Baseline - Find(pages, "eee fff", out _).Baseline, Is.EqualTo(20).Within(0.75));
        Assert.That(Distance(Around("<table style='width:100%'><tr><td>aaa bbb ccc ddd eee fff</td></tr></table>")), Is.EqualTo(20 + 40).Within(0.75));
    }

    [Test]
    public void InnerColumnWidthsFollowAutomaticLayoutInsideTheCell()
    {
        var pages = Layout(Around("<table><tr><td style='width:60pt'>k</td><td>v</td></tr></table>", "width:200pt"));
        Assert.That(Find(pages, "v", out _).X, Is.EqualTo(20 + 60).Within(0.75));
    }

    // Over-correction: the inner table's own margins stay inside the cell and lengthen the row.
    [Test]
    public void InnerTableMarginsStayInsideTheCell()
        => Assert.That(Distance(Around("<table style='margin:8pt 0'><tr><td>T1</td></tr></table>")), Is.EqualTo(20 + 8 + 20 + 8).Within(0.75));

    [Test]
    public void AHeaderCellMayHoldATableToo()
        => Assert.That(Distance("<p>AAA</p><table style='width:100pt'><tr><th style='font-weight:normal'><table><tr><td>T1</td></tr></table></th><td>Z</td></tr></table><p>BBB</p>"),
            Is.EqualTo(20 + 20).Within(0.75));

    [Test]
    public void TablesNestThreeLevelsDeep()
    {
        var pages = Layout(Around("L1<table><tr><td>L2<table><tr><td>L3</td></tr></table></td></tr></table>", "width:140pt"));
        Assert.That(Find(pages, "L1", out _).Baseline - Find(pages, "L3", out _).Baseline, Is.EqualTo(40).Within(0.75));
        Assert.That(Find(pages, "L3", out _).Baseline - Find(pages, "BBB", out _).Baseline, Is.EqualTo(20).Within(0.75));
    }

    [Test]
    public void AFourthLevelFailsWithTheNestingLimitCode()
    {
        var error = Fails(Around("a<table><tr><td>b<table><tr><td>c<table><tr><td>d</td></tr></table></td></tr></table></td></tr></table>", "width:200pt"), "FPDF1003");
        Assert.That(error.Message, Does.Contain("3 levels"));
    }

    // The same markup renders identically whatever the mode: nothing about a nested table is dropped.
    [Test]
    public void NonStrictModeRendersTheSameBytes()
    {
        var html = Css + Around("xx<table><tr><td>T1</td></tr></table>yy");
        byte[] Pdf(bool strict)
        {
            using var output = new MemoryStream();
            PdfConverter.Convert(html, output, Page with { Strict = strict });
            return output.ToArray();
        }
        Assert.That(Pdf(false), Is.EqualTo(Pdf(true)));
    }

    // Where the nesting would need layout that is not implemented, it fails instead of guessing.
    [Test]
    public void ATableOutsideACellsDirectContentStillFails()
    {
        Fails("<table><caption><table><tr><td>A</td></tr></table></caption><tr><td>B</td></tr></table>", "FPDF1101");
        Fails(Around("<ul><li><table><tr><td>A</td></tr></table></li></ul>"), "FPDF1101");
        Fails(Around("<p>x<table><tr><td>A</td></tr></table></p>"), "FPDF1101");
    }

    [Test]
    public void ARowTooTallForThePageStillFailsWithTheCellBlockMessage()
    {
        var rows = string.Concat(Enumerable.Range(0, 30).Select(i => $"<tr><td>R{i}</td></tr>"));
        var error = Fails(Around("<table>" + rows + "</table>"), "FPDF1302");
        Assert.That(error.Message, Does.Contain("nested table"));
    }

    // A row with a nested table moves whole to the next page when it fits there (like a row with a
    // list); a browser would split it between the inner rows.
    [Test]
    public void ARowThatDoesNotFitMovesWholeToTheNextPage()
    {
        const string inner = "<table><tr><td>in1</td></tr><tr><td>in2</td></tr><tr><td>in3</td></tr></table>";
        var pages = Layout("<div style='height:320pt'></div><table style='width:100pt'><tr><td>" + inner + "</td></tr></table>");
        Find(pages, "in1", out var first);
        Find(pages, "in3", out var last);
        Assert.That(first, Is.EqualTo(1));
        Assert.That(last, Is.EqualTo(1));
    }

    [Test]
    public void TheOuterHeaderRepeatsAndNestedRowsFollowTheirOuterRow()
    {
        var rows = string.Concat(Enumerable.Range(0, 30).Select(i => $"<tr><td><table><tr><td>k{i}</td><td>v{i}</td></tr></table></td></tr>"));
        var pages = Layout("<table style='width:200pt'><thead><tr><td>H</td></tr></thead><tbody>" + rows + "</tbody></table>");
        Assert.That(pages, Has.Count.EqualTo(2));
        Assert.That(pages.Select(p => p.Runs.Count(r => r.Text == "H")), Is.All.EqualTo(1));
        Assert.That(pages.SelectMany(p => p.Runs).Select(r => r.Text).Where(t => t.StartsWith('k')),
            Is.EqualTo(Enumerable.Range(0, 30).Select(i => $"k{i}")));
        // Every inner pair stays on one page, side by side.
        foreach (var i in Enumerable.Range(0, 30))
        {
            Find(pages, "k" + i, out var left);
            Find(pages, "v" + i, out var right);
            Assert.That(right, Is.EqualTo(left));
        }
    }
}
