using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// Lists in table cells the way a browser prints them: markers, margins that stay inside the cell
/// (a cell is a formatting-context root) but collapse among its own blocks, flex list items as
/// key/value rows, row heights and whole-row page movement. The expected distances were measured
/// against a browser's printed PDF of the same markup (ten point text on 20pt lines).
/// </summary>
[TestFixture]
public sealed class TableCellListTests
{
    private const string Css = "<style>html,body{margin:0;padding:0}body{font-size:10pt;line-height:20pt}" +
        "p,ul,ol,li,table,div,h2{margin:0;padding:0}table{border-collapse:separate;border-spacing:0}td,th{padding:0}" +
        "ul.x li{display:flex;justify-content:space-between;column-gap:10pt}</style>";

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

    private static double Distance(string body, string first = "AAA", string second = "BBB")
    {
        var pages = Layout(body);
        var a = Find(pages, first, out var pageA);
        var b = Find(pages, second, out var pageB);
        Assert.That(pageB, Is.EqualTo(pageA));
        return a.Baseline - b.Baseline;
    }

    private static string Cell(string content, string table = "width:100pt") =>
        $"<p>AAA</p><table style='{table}'><tr><td>{content}</td></tr></table><p>BBB</p>";

    private static string InCell(string content) => "<table style='width:100pt'><tr><td>" + content + "</td></tr></table>";

    private static byte[] Pdf(string html, bool strict = true)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(Css + html, output, Page with { Strict = strict });
        return output.ToArray();
    }

    [Test]
    public void ListItemsStackInsideTheCellAndTheRowTakesTheirHeight()
        => Assert.That(Distance(Cell("<ul><li>L1</li><li>L2</li></ul>")), Is.EqualTo(20 + 20 + 20).Within(0.75));

    [Test]
    public void EveryItemOfAnUnorderedListGetsAMarkerDiscBesideItsText()
    {
        var pages = Layout(InCell("<ul style='padding-left:20pt'><li>L1</li><li>L2</li></ul>"));
        Assert.That(pages[0].Discs, Has.Count.EqualTo(2));
        var first = Find(pages, "L1", out _);
        Assert.That(first.X, Is.EqualTo(20 + 20).Within(1e-9));
        // The disc sits left of the text, on the item's first line.
        Assert.That(pages[0].Discs[0].X, Is.LessThan(first.X));
        Assert.That(pages[0].Discs[0].Y, Is.GreaterThan(first.Baseline).And.LessThan(first.Baseline + 10));
    }

    // Over-correction: the list margins of a cell stay inside the cell instead of collapsing out
    // through it, so the closing margin still lengthens the row and the opening one still opens it.
    [Test]
    public void ListMarginsStayInsideTheCell()
    {
        Assert.That(Distance(Cell("<ul style='margin:0 0 8pt'><li>L1</li><li>L2</li></ul>")), Is.EqualTo(20 + 20 + 20 + 8).Within(0.75));
        Assert.That(Distance(Cell("<ul style='margin:8pt 0 0'><li>L1</li></ul>")), Is.EqualTo(20 + 8 + 20).Within(0.75));
    }

    [Test]
    public void ListAndFirstItemMarginsCollapseInsideTheCell()
        => Assert.That(Distance(Cell("<ul style='margin:5pt 0 0'><li style='margin-top:9pt'>L1</li></ul>")),
            Is.EqualTo(20 + 9 + 20).Within(0.75));

    [Test]
    public void TextAndListInOneCellShareTheirMargins()
    {
        Assert.That(Distance(Cell("Intro<ul style='margin:6pt 0'><li>L1</li></ul>Outro")),
            Is.EqualTo(20 + 26 + 26 + 20).Within(0.75));
        // A paragraph's bottom margin and the list's top margin collapse to the larger one.
        Assert.That(Distance(Cell("<p style='margin:0 0 6pt'>Intro</p><ul style='margin:10pt 0 0'><li>L1</li></ul>")),
            Is.EqualTo(20 + 30 + 20).Within(0.75));
    }

    [Test]
    public void FlexListItemsAreKeyValueRowsWithoutMarkers()
    {
        var pages = Layout("<p>AAA</p><table style='width:200pt'><tr><td><ul class='x' style='margin:0'>" +
            "<li><span>Label</span><span>Value</span></li><li><span>Other</span><span>V2</span></li></ul></td></tr></table><p>BBB</p>");
        Assert.That(pages[0].Discs, Is.Empty);
        // space-between puts the value at the cell's right edge, on the label's line.
        var label = Find(pages, "Label", out _);
        var value = Find(pages, "Value", out _);
        Assert.That(label.X, Is.EqualTo(20).Within(1e-9));
        Assert.That(value.X + 5 * 6, Is.EqualTo(220).Within(1e-6));
        Assert.That(value.Baseline, Is.EqualTo(label.Baseline).Within(1e-9));
        Assert.That(Distance("<p>AAA</p><table style='width:200pt'><tr><td><ul class='x' style='margin:0'>" +
            "<li><span>Label</span><span>Value</span></li><li><span>Other</span><span>V2</span></li></ul></td></tr></table><p>BBB</p>"),
            Is.EqualTo(20 + 20 + 20).Within(0.75));
    }

    // The author's padding-left, zero included, is a list's whole indentation in a browser; the
    // markers hang outside the item whatever it is.
    [Test]
    public void ListPaddingResetRemovesTheIndentation()
    {
        var reset = Layout(InCell("<ul style='padding:0'><li>Item</li></ul>"));
        Assert.That(Find(reset, "Item", out _).X, Is.EqualTo(20).Within(1e-9));
        var padded = Layout(InCell("<ul style='padding-left:12pt'><li>Item</li></ul>"));
        Assert.That(Find(padded, "Item", out _).X, Is.EqualTo(32).Within(1e-9));
    }

    [Test]
    public void OrderedListNumbersTheItemsInsideTheCell()
    {
        var pages = Layout("<table style='width:100pt'><tr><td><ol style='padding-left:20pt'><li>A</li><li>B</li></ol></td></tr></table>");
        Assert.That(pages[0].Runs.Select(r => r.Text), Is.EqualTo(new[] { "1.", "A", "2.", "B" }));
    }

    [Test]
    public void ACellWithAListGivesItsColumnTheListsWidth()
    {
        // An automatic column is as wide as its widest item (the longest line, plus the gutter).
        var pages = Layout("<table><tr><td><ul style='padding:0'><li>1234567890</li></ul></td><td>Z</td></tr></table>");
        Assert.That(Find(pages, "Z", out _).X, Is.EqualTo(20 + 60).Within(1e-9));
    }

    [Test]
    public void ListMarkersAndTextMoveWithTheCellsVerticalAlignment()
    {
        var pages = Layout("<table style='width:200pt'><tr><td style='vertical-align:middle'><ul style='padding-left:20pt'><li>L1</li></ul></td>" +
            "<td><ul style='padding-left:20pt'><li>A</li><li>B</li><li>C</li></ul></td></tr></table>");
        var middle = Find(pages, "L1", out _);
        var top = Find(pages, "A", out _);
        // The row is 60pt high and the one-line cell is centred: 20pt lower than top-aligned.
        Assert.That(top.Baseline - middle.Baseline, Is.EqualTo(20).Within(1e-6));
        var discs = pages[0].Discs.OrderByDescending(d => d.Y).ToList();
        Assert.That(discs[0].Y - discs[1].Y, Is.EqualTo(0).Within(1e-6).Or.EqualTo(20).Within(1e-6));
    }

    private static string Rows(int count, string cell)
        => string.Concat(Enumerable.Range(0, count).Select(i => $"<tr><td>R{i}</td><td>{cell.Replace("{i}", i.ToString(System.Globalization.CultureInfo.InvariantCulture))}</td></tr>"));

    // A row that fits a page moves whole, as any row does (a browser would also split it between
    // its lines; that existing difference is documented), and its markers move with it.
    [Test]
    public void ARowWithAListMovesToTheNextPageWholeLikeATextRow()
    {
        var list = Layout("<table style='width:200pt'>" + Rows(14, "<ul style='padding-left:20pt'><li>a{i}</li><li>b{i}</li><li>c{i}</li></ul>") + "</table>");
        var text = Layout("<table style='width:200pt'>" + Rows(14, "<p>a{i}</p><p>b{i}</p><p>c{i}</p>") + "</table>");
        Assert.That(list, Has.Count.EqualTo(text.Count));
        Assert.That(list, Has.Count.GreaterThan(1));
        for (var i = 0; i < 14; i++)
        {
            var inList = Find(list, $"R{i}", out var listPage);
            var inText = Find(text, $"R{i}", out var textPage);
            Assert.That(listPage, Is.EqualTo(textPage), $"row {i}");
            Assert.That(inList.Baseline, Is.EqualTo(inText.Baseline).Within(1e-9), $"row {i}");
            // The row's three markers are on the row's page.
            Assert.That(list[listPage].Runs.Select(r => r.Text), Does.Contain($"c{i}"));
        }
        Assert.That(list.Sum(p => p.Discs.Count), Is.EqualTo(14 * 3));
        Assert.That(list[1].Discs, Is.Not.Empty);
    }

    [Test]
    public void AHeaderRowWithAListRepeatsItsMarkersOnEveryPage()
    {
        var pages = Layout("<table style='width:200pt'><thead><tr><td>H</td><td><ul style='padding-left:20pt'><li>Head</li></ul></td></tr></thead><tbody>" +
            Rows(14, "<ul style='padding-left:20pt'><li>a{i}</li><li>b{i}</li><li>c{i}</li></ul>") + "</tbody></table>");
        Assert.That(pages, Has.Count.GreaterThan(1));
        for (var i = 0; i < pages.Count; i++)
        {
            Assert.That(pages[i].Runs.Count(r => r.Text == "Head"), Is.EqualTo(1), $"page {i}");
            Assert.That(pages[i].Discs.Count, Is.EqualTo(1 + pages[i].Runs.Count(r => System.Text.RegularExpressions.Regex.IsMatch(r.Text, "^[abc][0-9]+$"))), $"page {i}");
        }
    }

    [Test]
    public void ARowWithAListTallerThanAPageFailsExplicitly()
    {
        var html = Css + "<table style='width:200pt'><tr><td><ul>" + string.Concat(Enumerable.Repeat("<li>item</li>", 30)) + "</ul></td></tr></table>";
        using var output = new MemoryStream();
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, Page));
        Assert.That(error!.Code, Is.EqualTo("FPDF1302"));
        Assert.That(error.Message, Does.Contain("list"));
    }

    [Test]
    public void ARowWithAListThatMustSplitBesideAnotherCellFailsExplicitly()
    {
        var html = Css + "<table style='width:200pt'><tr><td>" + string.Join(" ", Enumerable.Repeat("word", 900)) +
            "</td><td><ul><li>item</li></ul></td></tr></table>";
        using var output = new MemoryStream();
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, Page));
        Assert.That(error!.Code, Is.EqualTo("FPDF1302"));
    }

    [Test]
    public void NonStrictModeRendersTheSameBytes()
        => Assert.That(Pdf(Cell("<ul class='x'><li><span>Label</span><span>Value</span></li></ul>"), strict: false),
            Is.EqualTo(Pdf(Cell("<ul class='x'><li><span>Label</span><span>Value</span></li></ul>"))));

    [Test]
    public void ListSupportIsLimitedToTheOutermostLevelOfOneCellNotTables()
    {
        // A table inside a list in a cell, or a table part in a list, stays an explicit failure.
        foreach (var html in new[]
        {
            "<table><tr><td><ul><li><table><tr><td>x</td></tr></table></li></ul></td></tr></table>",
            "<table><tr><td><ul><li><b><td>x</td></b></li></ul></td></tr></table>",
        })
        {
            using var output = new MemoryStream();
            var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(Css + html, output, Page));
            Assert.That(error!.Code, Is.EqualTo("FPDF1101"));
        }
    }
}
