using NUnit.Framework;

namespace FactsPDF.Tests;

// Report stylesheets keep table rows together with tr{break-inside:avoid;page-break-inside:avoid}.
// A table row that fits a fresh fragment with the repeated header is never split, and the header
// never splits, so on tr and thead the declaration describes what FactsPDF already renders: the
// page is byte-identical to the page without it. A kept row FactsPDF must split anyway follows
// Chrome 158's printed pages: it first breaks before itself (before the whole table when it is the
// first body row) unless it already starts where a fresh page would put it, then continues at
// line boundaries. A kept last row split only to carry the table's bottom padding and border still
// fails FPDF1302: Chrome keeps it whole and lets that decoration overflow into the page margin.
// tbody, tfoot, td and th still fail FPDF1201.
[TestFixture]
public sealed class TableRowBreakInsideTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 400, Margin = 20 };

    private const string Avoid = ".report tr{break-inside:avoid;page-break-inside:avoid}";

    private static byte[] Pdf(string html)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, Page);
        return output.ToArray();
    }

    // orphans and widows 1 keep the line-by-line breaks a test of row mechanics expects.
    private const string Widows1 = "td{orphans:1;widows:1}";

    private static List<LayoutPage> Layout(string html)
        => TextLayout.Layout(HtmlDocumentReader.ReadTree(html, Page, default), Page, default);

    private static string Lines(string prefix, int count)
        => string.Join("<br>", Enumerable.Range(1, count).Select(i => $"{prefix}{i:00}"));

    // Every cell line is 12pt tall and the usable page is 360pt, so a fresh fragment holds the
    // 12pt header and 29 more lines (348pt).
    private static string Doc(string css, string rows, string before = "", string tableStyle = "") =>
        "<!doctype html><html><head><style>th{font-weight:normal}td,th{padding:0}" + css + "</style></head><body>" +
        before + $"<table class='report' style='width:200pt;font-size:10pt;line-height:12pt{tableStyle}'>" +
        "<colgroup><col style='width:60pt'><col style='width:140pt'></colgroup>" +
        "<thead><tr><th>ID</th><th>Description</th></tr></thead><tbody>" + rows +
        "</tbody></table><p>AFTER</p></body></html>";

    private static string Rows(int count, int lines = 3) => string.Concat(Enumerable.Range(1, count)
        .Select(i => $"<tr><td>R{i:00}</td><td>{Lines($"R{i:00}L", lines)}</td></tr>"));

    private static int[] LinesPerPage(List<LayoutPage> pages, string prefix) => pages
        .Select(page => page.Runs.Count(run => run.Text.Length >= prefix.Length + 2
            && run.Text.StartsWith(prefix, StringComparison.Ordinal) && run.Text[prefix.Length..].All(char.IsAsciiDigit)))
        .ToArray();

    private static int[] PagesOf(List<LayoutPage> pages, string text) => pages
        .Select((page, index) => (page, index))
        .Where(p => p.page.Runs.Any(run => run.Text.Contains(text, StringComparison.Ordinal)))
        .Select(p => p.index).ToArray();

    private static void RejectWithoutOutput(string html, string code, int offset)
    {
        using var output = new MemoryStream();
        output.Write("ORIGINAL"u8);
        output.Position = 3;
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, Page));
        Assert.That(error!.Code, Is.EqualTo(code));
        Assert.That(error.SourceOffset, Is.EqualTo(offset));
        Assert.That(output.ToArray(), Is.EqualTo("ORIGINAL"u8.ToArray()));
        Assert.That(output.Position, Is.EqualTo(3));
    }

    [TestCase(Avoid)]
    [TestCase(".report tr{break-inside:avoid}")]
    [TestCase(".report tr{page-break-inside:avoid}")]
    [TestCase("tr{break-inside:auto}")]
    [TestCase("tr{break-inside:initial}")]
    [TestCase("tr{break-inside:unset}")]
    [TestCase("tr{break-inside:inherit}")]
    [TestCase("thead{break-inside:avoid}")]
    [TestCase("thead,tr{page-break-inside:avoid!important}")]
    [TestCase("TR{Break-Inside:AVOID}")]
    public void MultiPageTableWithRepeatedHeaderRendersLikeTheUndeclaredPage(string css)
    {
        var rows = Rows(40);
        Assert.That(Layout(Doc("", rows)), Has.Count.GreaterThan(2));
        Assert.That(Pdf(Doc(css, rows)), Is.EqualTo(Pdf(Doc("", rows))), css);
    }

    [Test]
    public void RowsThatFitAFreshFragmentAreNeverSplitWithOrWithoutTheDeclaration()
    {
        var rows = Rows(40);
        var pages = Layout(Doc(Avoid, rows));
        for (var i = 1; i <= 40; i++)
            Assert.That(PagesOf(pages, $"R{i:00}L").Distinct().Count(), Is.EqualTo(1), $"row {i}");
        Assert.That(pages.Skip(1).All(page => page.Runs.Any(run => run.Text == "Description")), Is.True,
            "the header repeats on every fragment");
    }

    [Test]
    public void ShortTableOnAPageTailMovesWholeLikeTheUndeclaredPage()
    {
        var filler = string.Concat(Enumerable.Range(1, 22).Select(i => $"<p style='margin:0'>FILLER{i:00}</p>"));
        var html = Doc(Avoid, Rows(6), filler);
        var pages = Layout(html);
        Assert.That(PagesOf(pages, "FILLER22"), Is.EqualTo(new[] { 0 }));
        Assert.That(Enumerable.Range(1, 6).SelectMany(i => PagesOf(pages, $"R{i:00}L")).Distinct(),
            Is.EqualTo(new[] { 1 }), "the whole table moves to the second page");
        Assert.That(Pdf(html), Is.EqualTo(Pdf(Doc("", Rows(6), filler))));
    }

    [Test]
    public void InlineRowStyleRendersLikeTheUndeclaredPage()
    {
        var rows = Rows(40);
        var inline = rows.Replace("<tr>", "<tr style='break-inside:avoid'>", StringComparison.Ordinal);
        Assert.That(Pdf(Doc("", inline)), Is.EqualTo(Pdf(Doc("", rows))));
    }

    [Test]
    public void TheSdkAcceptsTheDeclaration()
    {
        var rows = Rows(40);
        Assert.That(FactsPDF.Pdf.Write().ToBytes(Doc(Avoid, rows)),
            Is.EqualTo(FactsPDF.Pdf.Write().ToBytes(Doc("", rows))));
    }

    // 40 lines (480pt) cannot fit a fresh 348pt fragment. Without the declaration the row starts on
    // the page tail; kept, it breaks before itself and continues from the fresh page under the header.
    [Test]
    public void KeptRowTallerThanAFreshFragmentStartsOnAFreshPageThenContinues()
    {
        var rows = Rows(2) + $"<tr><td>LONG</td><td>{Lines("LONG", 40)}</td></tr>" + Rows(2);
        var plain = Layout(Doc("", rows));
        Assert.That(LinesPerPage(plain, "LONG"), Is.EqualTo(new[] { 23, 17 }));
        var pages = Layout(Doc(Avoid, rows));
        Assert.That(LinesPerPage(pages, "LONG"), Is.EqualTo(new[] { 0, 29, 11 }));
        Assert.That(PagesOf(pages, "R02L"), Is.EqualTo(new[] { 0, 2 }), "the rows before stay on the first page, the rows after follow the kept row");
        Assert.That(PagesOf(pages, "Description"), Is.EqualTo(new[] { 0, 1, 2 }), "the header repeats above the kept row");
    }

    // At the top of the first page the kept row already has all the room a fresh page gives it.
    [Test]
    public void KeptOversizedFirstRowAtThePageTopSplitsLikeTheUndeclaredRow()
    {
        var rows = $"<tr><td>LONG</td><td>{Lines("LONG", 40)}</td></tr>" + Rows(2);
        Assert.That(LinesPerPage(Layout(Doc(Avoid, rows)), "LONG"), Is.EqualTo(new[] { 29, 11 }));
        Assert.That(Pdf(Doc(Avoid, rows)), Is.EqualTo(Pdf(Doc("", rows))));
    }

    // A kept row after a split unkept row breaks before itself on that row's last fragment.
    [Test]
    public void KeptOversizedRowAfterASplitRowStartsOnAFreshPage()
    {
        var rows = $"<tr><td>A</td><td>{Lines("SPLIT", 35)}</td></tr><tr class='kept'><td>B</td><td>{Lines("KEPT", 35)}</td></tr>";
        var pages = Layout(Doc(".report tr.kept{break-inside:avoid}", rows));
        Assert.That(LinesPerPage(pages, "SPLIT"), Is.EqualTo(new[] { 29, 6, 0, 0 }));
        Assert.That(LinesPerPage(pages, "KEPT"), Is.EqualTo(new[] { 0, 0, 29, 6 }));
    }

    // 26 lines (312pt) fit a fresh fragment with the header (348pt) but not with the table's 40pt
    // bottom padding, so the last row is split to carry that decoration with real content. Chrome
    // keeps the kept row whole and paints the padding past the page bottom; FactsPDF fails instead.
    [Test]
    public void LastRowSplitOnlyForTheTableBottomDecorationFails()
    {
        var rows = Rows(5, lines: 1) + $"<tr class='last'><td>LAST</td><td>{Lines("LAST", 26)}</td></tr>";
        var plain = Layout(Doc("", rows, tableStyle: ";padding-bottom:40pt"));
        Assert.That(PagesOf(plain, "LAST").Distinct().Count(), Is.GreaterThan(1), "without avoid the row splits");
        var html = Doc(Avoid, rows, tableStyle: ";padding-bottom:40pt");
        RejectWithoutOutput(html, "FPDF1302", html.IndexOf("<tr class='last'>", StringComparison.Ordinal));
        // The same row followed by another row is not terminal and stays whole.
        var followed = rows + Rows(1, lines: 1);
        Assert.That(Pdf(Doc(Avoid, followed, tableStyle: ";padding-bottom:40pt")),
            Is.EqualTo(Pdf(Doc("", followed, tableStyle: ";padding-bottom:40pt"))));
    }

    // 26 lines fit a fresh fragment under the repeated header but not under the table's 40pt top
    // padding as well. At the page top the kept first row splits like the undeclared row; after other
    // content the whole table breaks before itself (no orphan header), then the row splits there.
    [Test]
    public void KeptFirstRowSplitOnlyForTheTableTopDecorationMovesTheTable()
    {
        var rows = $"<tr><td>FIRST</td><td>{Lines("FIRST", 26)}</td></tr>" + Rows(10, lines: 1);
        Assert.That(LinesPerPage(Layout(Doc(Avoid + Widows1, rows, tableStyle: ";padding-top:40pt")), "FIRST"),
            Is.EqualTo(new[] { 25, 1 }));
        Assert.That(Pdf(Doc(Avoid + Widows1, rows, tableStyle: ";padding-top:40pt")),
            Is.EqualTo(Pdf(Doc(Widows1, rows, tableStyle: ";padding-top:40pt"))));
        var filler = string.Concat(Enumerable.Range(1, 5).Select(i => $"<p style='margin:0'>FILLER{i:00}</p>"));
        Assert.That(LinesPerPage(Layout(Doc(Widows1, rows, filler, ";padding-top:40pt")), "FIRST"), Is.EqualTo(new[] { 19, 7 }));
        var pages = Layout(Doc(Avoid + Widows1, rows, filler, ";padding-top:40pt"));
        Assert.That(LinesPerPage(pages, "FIRST"), Is.EqualTo(new[] { 0, 25, 1 }));
        Assert.That(PagesOf(pages, "FILLER05"), Is.EqualTo(new[] { 0 }));
        Assert.That(PagesOf(pages, "Description"), Is.EqualTo(new[] { 1, 2 }), "no header is left behind");
    }

    [Test]
    public void ASplitRowWithoutTheDeclarationStillRendersBesideKeptRows()
    {
        var rows = Rows(2) + $"<tr><td>LONG</td><td>{Lines("LONG", 40)}</td></tr>" + Rows(2);
        var css = ".report tr.kept{break-inside:avoid}";
        var kept = rows.Replace("<tr><td>R01", "<tr class='kept'><td>R01", StringComparison.Ordinal);
        Assert.That(Pdf(Doc(css, kept)), Is.EqualTo(Pdf(Doc("", rows))));
    }

    // Chrome 158 headless print-to-PDF of the same tables on A4 with 36pt margins (Arial, the same
    // 12pt lines): the page index of every line of the kept row, recorded with page.pdf and PyMuPDF.
    // The default options are A4 with 36pt margins, so a fresh fragment holds the header and 63 lines.
    [TestCase("oversized-mid", "BIGL", new[] { 0, 63, 37 })]
    [TestCase("oversized-first", "BIGL", new[] { 63, 37 })]
    [TestCase("first-top-padding", "FIRSTL", new[] { 59, 3 })]
    [TestCase("first-top-padding-after-filler", "FIRSTL", new[] { 0, 59, 3 })]
    [TestCase("fits-fresh", "MIDL", new[] { 0, 20 })]
    public void KeptRowsLandOnTheSamePagesAsInChrome(string name, string prefix, int[] expected)
    {
        static string Row(string id, int lines) => $"<tr><td>{id}</td><td>{Lines(id + "L", lines)}</td></tr>";
        static string Many(string id, int count, int lines) => string.Concat(Enumerable.Range(1, count).Select(i => Row($"{id}{i:00}", lines)));
        var filler = string.Concat(Enumerable.Range(1, 10).Select(i => $"<p style='margin:0;font-size:10pt;line-height:12pt'>FILL{i:00}</p>"));
        var (rows, before, tableStyle) = name switch
        {
            "oversized-mid" => (Many("B", 10, 2) + Row("BIG", 100) + Many("Z", 2, 1), "", ""),
            "oversized-first" => (Row("BIG", 100) + Many("Z", 2, 1), "", ""),
            "first-top-padding" => (Row("FIRST", 62) + Many("Z", 5, 1), "", ";padding-top:40pt"),
            "first-top-padding-after-filler" => (Row("FIRST", 62) + Many("Z", 5, 1), filler, ";padding-top:40pt"),
            _ => (Many("A", 30, 2) + Row("MID", 20) + Many("Z", 2, 1), "", ""),
        };
        var a4 = new PdfOptions();
        var pages = TextLayout.Layout(HtmlDocumentReader.ReadTree(Doc(Avoid, rows, before, tableStyle), a4, default), a4, default);
        Assert.That(LinesPerPage(pages, prefix), Is.EqualTo(expected));
    }

    [TestCase("tbody{break-inside:avoid}", "FPDF1201", "avoid}")]
    [TestCase("tfoot{break-inside:auto}", "FPDF1201", "auto}")]
    [TestCase("td{break-inside:avoid}", "FPDF1201", "avoid}")]
    [TestCase("th{page-break-inside:avoid}", "FPDF1201", "avoid}")]
    [TestCase("table{break-inside:avoid}", "FPDF1201", "avoid}")]
    [TestCase("tr{break-inside:avoid-page}", "FPDF1202", "avoid-page")]
    [TestCase("tr{break-inside:avoid-column}", "FPDF1202", "avoid-column")]
    [TestCase("thead{break-inside:always}", "FPDF1202", "always")]
    public void OtherTablePartsAndValuesStillFail(string css, string code, string at)
    {
        var tfoot = css.StartsWith("tfoot", StringComparison.Ordinal);
        var html = Doc(css, Rows(2));
        if (tfoot) html = html.Replace("</tbody>", "</tbody><tfoot><tr><td>T</td><td>Total</td></tr></tfoot>", StringComparison.Ordinal);
        RejectWithoutOutput(html, code, html.IndexOf(at, StringComparison.Ordinal));
    }
}
