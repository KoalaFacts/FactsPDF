using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// overflow-wrap:anywhere (CSS Text 3) breaks an otherwise unbreakable string exactly like
/// break-word; the two differ only in min-content sizing, which no FactsPDF layout computes
/// (columns and boxes have explicit or containing-block widths), so the PDF is byte-identical.
/// </summary>
[TestFixture]
public sealed class OverflowWrapAnywhereTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 260, PageHeight = 400, Margin = 20 };
    private const string Code = "REF2026ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    private static byte[] Pdf(string html, PdfOptions? page = null)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, page ?? Page);
        return output.ToArray();
    }

    private static List<LayoutPage> Layout(string html, PdfOptions? page = null)
        => TableLayoutTests.Layout(html, page ?? Page);

    private static string Table(string cellStyle, string cell, string stylesheet = "")
        => stylesheet + "<table class=ledger style='width:200pt;font-size:10pt'>" +
            "<colgroup><col style='width:60pt'><col style='width:140pt'></colgroup>" +
            "<thead><tr><th style='font-weight:normal'>KEY</th><th style='font-weight:normal'>MEMO</th></tr></thead>" +
            $"<tbody><tr><td style='{cellStyle}'>{cell}</td><td>Settled</td></tr></tbody></table>";

    public static IEnumerable<TestCaseData> Contexts()
    {
        yield return new TestCaseData("<p style='width:60pt;@'>PAID " + Code + "</p>").SetName("Paragraph");
        yield return new TestCaseData("<h2 style='font-weight:normal;width:80pt;@'>" + Code + "</h2>").SetName("Heading");
        yield return new TestCaseData("<div style='@'><p style='width:60pt'>" + Code + "</p></div>").SetName("InheritedFromDiv");
        yield return new TestCaseData("<html><body style='@'><p style='width:60pt'>" + Code + "</p></body></html>").SetName("InheritedFromBody");
        yield return new TestCaseData("<blockquote style='@'><p style='width:60pt'>" + Code + "</p></blockquote>").SetName("InheritedFromBlockquote");
        yield return new TestCaseData("<div style='width:80pt;@'><ul><li>" + Code + "</li></ul></div>").SetName("InheritedIntoListItem");
        yield return new TestCaseData(Table("@", Code)).SetName("TableCellAnonymousText");
        yield return new TestCaseData(Table("@", "<p>" + Code + "</p>")).SetName("TableCellFlatParagraph");
        yield return new TestCaseData(Table("", Code, "<style>.ledger td{@}</style>")).SetName("LedgerStylesheetRule");
        yield return new TestCaseData(Table("", "A").Replace("<th style='font-weight:normal'>KEY</th>",
            "<th style='font-weight:normal;@'>" + Code + "</th>", StringComparison.Ordinal)).SetName("TableHeaderCell");
    }

    [TestCaseSource(nameof(Contexts))]
    public void LegacyWordWrapAnywhereRendersLikeOverflowWrapAnywhere(string template)
        => Assert.That(Pdf(template.Replace("@", "word-wrap:anywhere", StringComparison.Ordinal)),
            Is.EqualTo(Pdf(template.Replace("@", "overflow-wrap:anywhere", StringComparison.Ordinal))));

    [Test]
    public void AnywhereSplitsOnlyTheOverlongTokenAndKeepsEveryGlyph()
    {
        var runs = Layout("<p style='width:60pt;overflow-wrap:anywhere'>PAID " + Code + " OK</p>")
            .SelectMany(page => page.Runs).ToArray();
        Assert.That(runs.Length, Is.GreaterThan(3));
        Assert.That(runs.All(run => run.Width <= 60.000001), Is.True);
        Assert.That(string.Concat(runs.Select(run => run.Text)).Replace(" ", "", StringComparison.Ordinal), Is.EqualTo("PAID" + Code + "OK"));
        // Fitting words still move whole: anywhere is emergency wrapping, not break-all.
        Assert.That(Layout("<p style='width:60pt;overflow-wrap:anywhere'>PAID EXAMPLE</p>")
            .SelectMany(page => page.Runs).Select(run => run.Text), Is.EqualTo(new[] { "PAID", "EXAMPLE" }));
        Assert.That(Pdf("<p style='overflow-wrap:anywhere'>PAID IN FULL</p>"), Is.EqualTo(Pdf("<p>PAID IN FULL</p>")));
    }

    [Test]
    public void MultiPageLedgerWithRepeatedHeadersIsByteIdenticalToBreakWord()
    {
        var rows = string.Concat(Enumerable.Range(1, 40).Select(i =>
            $"<tr><td>INV{i:000}-{Code}</td><td>Settlement {i:000} for the quarter</td></tr>"));
        string Ledger(string value) => $"<style>.ledger td{{overflow-wrap:{value}}}</style>" +
            "<table class=ledger style='width:200pt;font-size:10pt'>" +
            "<colgroup><col style='width:60pt'><col style='width:140pt'></colgroup>" +
            "<thead><tr><th style='font-weight:normal'>KEY</th><th style='font-weight:normal'>MEMO</th></tr></thead><tbody>" + rows + "</tbody></table>";
        var anywhere = Ledger("anywhere");
        Assert.That(Pdf(anywhere), Is.EqualTo(Pdf(Ledger("break-word"))));
        var pages = Layout(anywhere);
        Assert.That(pages.Count, Is.GreaterThan(2));
        Assert.That(pages.All(page => page.Runs.Any(run => run.Text == "KEY")), Is.True);
        var text = string.Concat(pages.SelectMany(page => page.Runs).Select(run => run.Text));
        for (var i = 1; i <= 40; i++)
            Assert.That(text, Does.Contain($"INV{i:000}-{Code}"));
    }

    [Test]
    public void CascadeKeywordsAndLaterDeclarationsResolveAnywhere()
    {
        var items = HtmlDocumentReader.Read(
            "<style>div{overflow-wrap:anywhere}.n{overflow-wrap:normal}.i{overflow-wrap:initial}" +
            ".w{word-wrap:normal}p.imp{overflow-wrap:anywhere!important}</style><div><p>A</p><p class=n>B</p>" +
            "<p class=i>C</p><p class=w>D</p><p class='imp n' style='overflow-wrap:normal'>E</p></div>", Page, default);
        Assert.That(items.Select(p => p.Style.BreakWords), Is.EqualTo(new[] { true, false, false, false, true }));
    }

    [TestCase("<p style='overflow-wrap:anywhere break-word'>A</p>", "FPDF1202")]
    [TestCase("<p style='word-wrap:anywhere normal'>A</p>", "FPDF1202")]
    public void UnsupportedContextsAndValuesStillFail(string html, string code)
    {
        using var output = new MemoryStream();
        output.Write("existing output"u8);
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, Page));
        Assert.That(error!.Code, Is.EqualTo(code));
        Assert.That(output.ToArray(), Is.EqualTo("existing output"u8.ToArray()));
    }

    [Test]
    public void UnsupportedOverflowWrapMessageNamesAnywhere()
    {
        var error = Assert.Throws<FactsPdfException>(() => Pdf("<p style='overflow-wrap:auto'>A</p>"));
        Assert.That(error!.Message, Does.Contain("normal, break-word and anywhere"));
    }
}
