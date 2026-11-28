using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// Where a table may stand: after inline text in a block, in a list item and after an open p (which
/// the table start tag closes), as a browser parses and prints them. Ten point text on 20pt lines.
/// </summary>
[TestFixture]
public sealed class TablePlacementTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 500, Margin = 20 };
    private const string Css = "<style>html,body{margin:0;padding:0}body{font-size:10pt;line-height:20pt}" +
        "p,ul,ol,li,table,div{margin:0;padding:0}table{border-collapse:separate;border-spacing:0}td{padding:0}</style>";
    private const string Table = "<table style='width:120pt'><tr><td>T1</td><td>T2</td></tr></table>";

    private static List<LayoutPage> Layout(string html)
        => TextLayout.Layout(HtmlDocumentReader.ReadTree(Css + html, Page, default), Page, default);

    private static PlacedText Run(List<LayoutPage> pages, string text) => pages[0].Runs.Single(r => r.Text == text);

    private static byte[] Pdf(string html)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(Css + html, output, Page);
        return output.ToArray();
    }

    private static FactsPdfException Fails(string html, string code)
    {
        using var output = new MemoryStream();
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(Css + html, output, Page));
        Assert.That(error!.Code, Is.EqualTo(code), error.Message);
        return error;
    }

    [Test]
    public void ATableStartTagClosesAnOpenParagraph()
    {
        Assert.That(Pdf("<p>Intro" + Table + "<p>After</p>"), Is.EqualTo(Pdf("<p>Intro</p>" + Table + "<p>After</p>")));
        var pages = Layout("<p>Intro" + Table + "<p>After</p>");
        Assert.That(Run(pages, "Intro").Baseline - Run(pages, "T1").Baseline, Is.EqualTo(20).Within(1e-9));
        Assert.That(Run(pages, "T1").Baseline - Run(pages, "After").Baseline, Is.EqualTo(20).Within(1e-9));
    }

    [Test]
    public void TheEndTagOfTheParagraphTheTableClosedWouldMakeAnEmptyParagraphAndFails()
    {
        var error = Fails("<p>Intro" + Table + "After</p>", "FPDF1101");
        Assert.That(error.Message, Does.Contain("</p>"));
    }

    [Test]
    public void TextBeforeAndAfterATableInABlockFormsItsOwnLines()
    {
        var pages = Layout("<div>Intro" + Table + "After</div>");
        Assert.That(Run(pages, "Intro").Baseline - Run(pages, "T1").Baseline, Is.EqualTo(20).Within(1e-9));
        Assert.That(Run(pages, "T1").Baseline - Run(pages, "After").Baseline, Is.EqualTo(20).Within(1e-9));
        // Inline elements around the table are split by it: both halves keep their style.
        var inline = Layout("<div><span>Intro" + Table + "After</span></div>");
        Assert.That(Run(inline, "Intro").Baseline - Run(inline, "After").Baseline, Is.EqualTo(40).Within(1e-9));
    }

    [Test]
    public void ATableInAListItemFollowsTheItemsTextAndTheItemContinuesAfterIt()
    {
        var pages = Layout("<ul style='padding-left:20pt'><li>Intro" + Table + "After</li><li>Second</li></ul>");
        Assert.That(Run(pages, "Intro").X, Is.EqualTo(40).Within(1e-9));
        Assert.That(Run(pages, "T1").X, Is.EqualTo(40).Within(1e-9));
        Assert.That(Run(pages, "T2").X, Is.EqualTo(40 + 60).Within(1e-9));
        Assert.That(Run(pages, "After").X, Is.EqualTo(40).Within(1e-9));
        Assert.That(Run(pages, "Intro").Baseline - Run(pages, "T1").Baseline, Is.EqualTo(20).Within(1e-9));
        Assert.That(Run(pages, "T1").Baseline - Run(pages, "After").Baseline, Is.EqualTo(20).Within(1e-9));
        Assert.That(Run(pages, "After").Baseline - Run(pages, "Second").Baseline, Is.EqualTo(20).Within(1e-9));
        // One marker per item, the first beside the item's first line (Intro), none beside the table or After.
        Assert.That(pages[0].Discs, Has.Count.EqualTo(2));
        Assert.That(pages[0].Discs[0].Y, Is.EqualTo(Run(pages, "Intro").Baseline).Within(10));
    }

    // A browser aligns the marker of an item that starts with a table in a way that is not modelled.
    [Test]
    public void AMarkedListItemThatStartsWithATableFailsExplicitly()
    {
        var error = Fails("<ul>" + "<li>" + Table + "After</li></ul>", "FPDF1201");
        Assert.That(error.Message, Does.Contain("marker"));
        Fails("<ol><li>" + Table + "</li></ol>", "FPDF1201");
        // Without a marker nothing is misaligned.
        Assert.DoesNotThrow(() => Layout("<ul style='list-style:none'><li>" + Table + "After</li></ul>"));
    }

    [Test]
    public void TablesStillFailInsideHeadings()
    {
        Fails("<h2>Head" + Table + "</h2>", "FPDF1101");
    }

    [Test]
    public void ATableInAListItemPaginatesWithTheList()
    {
        var rows = string.Concat(Enumerable.Range(0, 30).Select(i => $"<tr><td>R{i}</td><td>X</td></tr>"));
        var pages = Layout("<ul style='padding-left:20pt'><li>Intro<table style='width:120pt'>" + rows + "</table>After</li></ul>");
        Assert.That(pages, Has.Count.GreaterThan(1));
        Assert.That(pages.SelectMany(p => p.Runs).Select(r => r.Text).Where(t => t.StartsWith('R')),
            Is.EqualTo(Enumerable.Range(0, 30).Select(i => $"R{i}")));
        // The item's marker appears once, on the first page.
        Assert.That(pages.Sum(p => p.Discs.Count), Is.EqualTo(1));
    }
}
