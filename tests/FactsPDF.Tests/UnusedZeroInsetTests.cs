using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// A table has no horizontal margin geometry, rows, row groups, columns and column groups take
/// no padding, and a br lays out no box, so the rest of *{margin:0;padding:0} renders
/// byte-identical to the page without it. Nonzero values and inherit still fail with no output.
/// </summary>
[TestFixture]
public sealed class UnusedZeroInsetTests
{
    private static readonly PdfOptions Page = new() { Tagged = false, PageWidth = 300, PageHeight = 400, Margin = 20 };

    private const string Content =
        "<h2>Invoice</h2><p>Paid<br>in full</p><table style='width:200pt'><colgroup><col style='width:120pt'><col style='width:80pt'></colgroup>" +
        "<thead><tr><th>Item</th><th>Total</th></tr></thead><tbody><tr><td>Paid</td><td>42</td></tr></tbody>" +
        "</table><ul><li>One</li><li>Two</li></ul><ol><li>Three</li></ol>";

    private static byte[] Pdf(string html)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, Page);
        return output.ToArray();
    }

    private static string Doc(string css) =>
        $"<!doctype html><html><head><style>h2,th{{font-weight:normal}}{css}</style></head><body>{Content}</body></html>";

    [TestCase("table{margin:0}")]
    [TestCase("table{margin:initial}")]
    [TestCase("tr,thead,tbody,tfoot,col,colgroup{padding:0}")]
    [TestCase("tr{padding:0px 0pt}tbody{padding-inline:0;padding-block:0em}")]
    [TestCase("colgroup{padding:initial}col{padding:unset}")]
    [TestCase("br{margin:0;padding:0}")]
    public void ZeroInsetRendersLikeTheUndeclaredPage(string css)
    {
        Assert.That(Pdf(Doc(css)), Is.EqualTo(Pdf(Doc(""))), css);
    }

    // The universal reset now reaches every element of a list and table page; only the elements
    // with real insets (the body margin, headings, paragraphs, list and cell padding) change,
    // exactly as if the reset named them alone.
    [TestCase("*{margin:0;padding:0}")]
    [TestCase("html,body,h2,p,br,table,colgroup,col,thead,tbody,tfoot,tr,th,td,ul,ol,li{margin:0;padding:0}")]
    public void TheUniversalResetEqualsTheResetOfTheElementsWithInsets(string css)
    {
        Assert.That(Pdf(Doc(css)), Is.EqualTo(Pdf(Doc(Insets))), css);
    }

    private const string Insets = "body,h2,p{margin:0;padding:0}ul,ol{margin:0}ul,ol,li,th,td{padding:0}";

    // Lists have the user-agent 1em above and below, so a zero margin is a real declaration there.
    [TestCase("ul,ol{margin:0}")]
    [TestCase("ul{margin:0px 0pt 0em 0%}ol{margin-block:0rem;margin-inline:0}")]
    [TestCase("ul{margin:unset}ol{margin:0!important}")]
    public void ZeroListMarginRendersLikeTheExplicitZeroListMargin(string css)
    {
        Assert.That(Pdf(Doc(css)), Is.EqualTo(Pdf(Doc("ul,ol{margin:0}"))), css);
    }

    [TestCase("table{margin:0 auto}", "auto", "FPDF1201")]
    [TestCase("table{margin-left:2px}", "2px", "FPDF1201")]
    [TestCase("tr{padding:2pt}", "2pt", "FPDF1201")]
    [TestCase("tbody{padding:inherit}", "inherit", "FPDF1201")]
    [TestCase("col{padding-left:1px}", "1px", "FPDF1201")]
    [TestCase("br{margin:1pt}", "<br>", "FPDF1103")]
    public void NonZeroInsetsStillFailWithoutOutput(string css, string at, string code)
    {
        var html = Doc(css);
        using var output = new MemoryStream();
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, Page));
        Assert.That(error!.Code, Is.EqualTo(code), css);
        Assert.That(output.Length, Is.Zero);
        Assert.That(error.SourceOffset, Is.EqualTo(html.IndexOf(at, StringComparison.Ordinal)), css);
    }

    [TestCase("ul{margin:4vh}")]
    [TestCase("tr{padding:-}")]
    public void MalformedInsetsStillFail(string css)
    {
        using var output = new MemoryStream();
        Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(Doc(css), output, Page));
        Assert.That(output.Length, Is.Zero);
    }

    [Test]
    public void TheSdkAcceptsTheReset()
    {
        var reset = FactsPDF.Pdf.Write().WithTaggedPdf(false).ToBytes(Doc("*{margin:0;padding:0}"));
        Assert.That(reset, Is.EqualTo(FactsPDF.Pdf.Write().WithTaggedPdf(false).ToBytes(Doc(Insets))));
    }
}
