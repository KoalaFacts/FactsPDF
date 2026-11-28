using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// A zero margin is the initial value on list items and on table rows, row groups, columns,
/// column groups and cells, so *{margin:0} renders byte-identical to the page without it there.
/// Nonzero margins on those elements still fail with FPDF1201 and write nothing.
/// </summary>
[TestFixture]
public sealed class TablePartMarginTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 400, Margin = 20 };

    private const string Content =
        "<h2>Invoice</h2><table style='width:200pt'><colgroup><col style='width:120pt'><col style='width:80pt'></colgroup>" +
        "<thead><tr><th>Item</th><th>Total</th></tr></thead><tbody><tr><td>Paid</td><td>42</td></tr></tbody>" +
        "</table><ul><li>One</li><li>Two</li></ul>";

    private static byte[] Pdf(string html)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, Page);
        return output.ToArray();
    }

    private static string Doc(string css) =>
        $"<!doctype html><html><head><style>h1,h2,h3,h4,h5,h6,th{{font-weight:normal}}{css}</style></head><body>{Content}</body></html>";

    [TestCase("td,th{margin:0}")]
    [TestCase("tr,thead,tbody,col,colgroup,td,th{margin:0}")]
    [TestCase("td{margin:0px 0pt 0em 0%}")]
    [TestCase("th{margin-inline:0;margin-block:0rem}")]
    [TestCase("td{margin:initial}th{margin:unset}tr{margin:0!important}")]
    [TestCase("li{margin:0}")]
    [TestCase("li{margin:0px 0pt;margin-block:0}")]
    [TestCase("li{margin:initial}")]
    public void ZeroMarginRendersLikeTheUndeclaredPage(string css)
    {
        Assert.That(Pdf(Doc(css)), Is.EqualTo(Pdf(Doc(""))), css);
    }

    [TestCase("li{margin:auto}", "auto", "FPDF1202")]
    [TestCase("td{margin:4px}", "4px", "FPDF1201")]
    [TestCase("th{margin-left:1em}", "1em", "FPDF1201")]
    [TestCase("tr{margin:0 auto}", "auto", "FPDF1201")]
    [TestCase("td{margin:inherit}", "inherit", "FPDF1201")]
    public void NonZeroMarginsStillFailWithoutOutput(string css, string at, string code)
    {
        var html = Doc(css);
        using var output = new MemoryStream();
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, Page));
        Assert.That(error!.Code, Is.EqualTo(code), css);
        Assert.That(output.Length, Is.Zero);
        Assert.That(error.SourceOffset, Is.EqualTo(html.IndexOf(at, StringComparison.Ordinal)), css);
    }

    [TestCase("td{margin:4vh}")]
    [TestCase("td{margin:-}")]
    public void MalformedCellMarginsStillFail(string css)
    {
        using var output = new MemoryStream();
        Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(Doc(css), output, Page));
        Assert.That(output.Length, Is.Zero);
    }

    [Test]
    public void TheSdkAcceptsTheReset()
    {
        var reset = FactsPDF.Pdf.Write().ToBytes(Doc("tr,td,th,li{margin:0}"));
        Assert.That(reset, Is.EqualTo(FactsPDF.Pdf.Write().ToBytes(Doc(""))));
    }
}
