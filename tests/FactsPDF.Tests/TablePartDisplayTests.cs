using NUnit.Framework;

namespace FactsPDF.Tests;

// Report stylesheets restate the table-part display values, such as thead{display:table-header-group}
// and tfoot{display:table-row-group}. Where the value names what the table already renders (R5co),
// the page renders byte-identical to the page without it; before, every such value failed FPDF1202.
// tfoot{display:table-footer-group} would repeat the footer on every fragment, which tfoot rows never
// do, so it still fails, as does every other display value on a table part and every table keyword
// on any other element.
[TestFixture]
public sealed class TablePartDisplayTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 400, Margin = 20 };

    private static byte[] Pdf(string html)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, Page);
        return output.ToArray();
    }

    private static string Doc(string css, int bodyRows = 1, string rowStyle = "") =>
        $"<!doctype html><html><head><style>th{{font-weight:normal}}{css}</style></head><body><h2>Orders</h2>" +
        "<table class='table' style='width:200pt'><colgroup><col style='width:120pt'><col style='width:80pt'></colgroup>" +
        $"<thead><tr><th>Item</th><th>Total</th></tr></thead><tbody>" +
        string.Concat(Enumerable.Range(1, bodyRows).Select(i => $"<tr{rowStyle}><td>Line {i}</td><td>{i}</td></tr>")) +
        "</tbody><tfoot><tr><td>Sum</td><td>42</td></tr></tfoot></table><p>Paid in full</p></body></html>";

    [TestCase("thead{display:table-header-group}")]
    [TestCase("tbody{display:table-row-group}")]
    [TestCase(".table tfoot{display:table-row-group}")]
    [TestCase("tr{display:table-row}")]
    [TestCase("td,th{display:table-cell}")]
    [TestCase("thead{display:table-header-group}tbody,tfoot{display:table-row-group}tr{display:table-row}td,th{display:table-cell}")]
    [TestCase("THEAD{DISPLAY:TABLE-HEADER-GROUP}TFoot{Display:Table-Row-Group !important}")]
    public void MatchingTablePartDisplayRendersLikeTheUndeclaredPage(string css)
    {
        Assert.That(Pdf(Doc(css)), Is.EqualTo(Pdf(Doc(""))), css);
    }

    [Test]
    public void RepeatedHeaderOnAMultiPageTableIsUnchanged()
    {
        var css = "thead{display:table-header-group}tbody,tfoot{display:table-row-group}";
        using var output = new MemoryStream();
        var result = PdfConverter.Convert(Doc(css, bodyRows: 40), output, Page);
        Assert.That(result.PageCount, Is.GreaterThan(1));
        Assert.That(output.ToArray(), Is.EqualTo(Pdf(Doc("", bodyRows: 40))));
    }

    [Test]
    public void InlineStyleOnARowRendersLikeTheUndeclaredPage()
    {
        Assert.That(Pdf(Doc("", rowStyle: " style='display:table-row'")), Is.EqualTo(Pdf(Doc(""))));
    }

    [TestCase("tfoot{display:table-footer-group}", "table-footer-group", "FPDF1202")]
    [TestCase("thead{display:table-row-group}", "table-row-group", "FPDF1202")]
    [TestCase("tbody{display:table-header-group}", "table-header-group", "FPDF1202")]
    [TestCase("tr{display:table-cell}", "table-cell", "FPDF1202")]
    [TestCase("td{display:table-row}", "table-row", "FPDF1202")]
    [TestCase("h2{display:table-cell}", "table-cell", "FPDF1202")]
    [TestCase("p{display:table-row-group}", "table-row-group", "FPDF1202")]
    [TestCase("tbody{display:table}", "table}", "FPDF1202")]
    [TestCase("td{display:inline-block}", "inline-block", "FPDF1202")]
    [TestCase("thead{display:initial}", "initial", "FPDF1202")]
    [TestCase("td{display:block}", "block", "FPDF1201")]
    [TestCase("tfoot{display:none}", "none", "FPDF1201")]
    [TestCase("h3{display:table-cell}", "table-cell", "FPDF1202")]
    [TestCase(".row{display:table-row}", "table-row", "FPDF1202")]
    [TestCase("*{display:table-cell}", "table-cell", "FPDF1202")]
    [TestCase("td,p{display:table-cell}", "table-cell", "FPDF1202")]
    [TestCase("p{display:table-cell}h2{display:table-row}", "table-cell", "FPDF1202")]
    [TestCase("h2{display:table-cell}p{color:notacolour}", "table-cell", "FPDF1202")]
    [TestCase("td{display:table-cell;display:inline}", "inline", "FPDF1202")]
    public void OtherDisplayValuesStillFailWithoutOutput(string css, string at, string code)
    {
        var html = Doc(css);
        using var output = new MemoryStream();
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, Page));
        Assert.That(error!.Code, Is.EqualTo(code), css);
        Assert.That(output.Length, Is.Zero);
        Assert.That(error.SourceOffset, Is.EqualTo(html.IndexOf(at, StringComparison.Ordinal)), css);
    }

    // A style text compiled once for a cell is replayed for later elements; the replay still checks
    // the element, and a table keyword fails before any later declaration in the same attribute.
    [TestCase("<p style='display:table-cell'>Paid</p>", "display:table-cell'>Paid")]
    [TestCase("<p style='display:table-cell;color:notacolour'>Paid</p>", "display:table-cell;")]
    public void InlineTableKeywordsOnOtherElementsStillFail(string paragraph, string at)
    {
        var html = Doc("").Replace("<p>Paid in full</p>", paragraph, StringComparison.Ordinal)
            .Replace("<td>Line 1</td>", "<td style='display:table-cell'>Line 1</td>", StringComparison.Ordinal);
        using var output = new MemoryStream();
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, Page));
        Assert.That(error!.Code, Is.EqualTo("FPDF1202"));
        Assert.That(error.Message, Does.Contain("only block, flex and none"));
        Assert.That(output.Length, Is.Zero);
        Assert.That(error.SourceOffset, Is.EqualTo(html.IndexOf(at, StringComparison.Ordinal) + "display:".Length));
    }

    [Test]
    public void TableFooterGroupNamesTheRowGroupBehaviour()
    {
        using var output = new MemoryStream();
        var error = Assert.Throws<FactsPdfException>(
            () => PdfConverter.Convert(Doc("tfoot{display:table-footer-group}"), output, Page));
        Assert.That(error!.Message, Does.Contain("table-row-group"));
    }

    [Test]
    public void TheSdkAcceptsTheDeclarations()
    {
        var declared = FactsPDF.Pdf.Write().ToBytes(Doc("thead{display:table-header-group}tfoot{display:table-row-group}"));
        Assert.That(declared, Is.EqualTo(FactsPDF.Pdf.Write().ToBytes(Doc(""))));
    }
}
