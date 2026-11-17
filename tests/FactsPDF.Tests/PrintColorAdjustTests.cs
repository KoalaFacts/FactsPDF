using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// print-color-adjust (and its common -webkit- spelling) only allows a printer to drop or
/// adjust colours. The renderer always paints backgrounds, borders and text colours exactly
/// as declared, which both exact and economy permit, so the declaration is validated and
/// dropped and the page renders byte-identical to the page without it.
/// </summary>
[TestFixture]
public sealed class PrintColorAdjustTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 400, Margin = 20 };

    private static byte[] Pdf(string html)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, Page);
        return output.ToArray();
    }

    private static string Report(string declarations) =>
        $"<style>body{{{declarations}}} h2{{color:#234;{declarations}}} .box{{width:200pt;background:#eef;border:1pt solid #c00;{declarations}}} .unused{{{declarations}}}</style>" +
        "<h2>Invoice</h2>" +
        $"<div class='box'><p style='color:#060;{declarations}'>Paid in full</p></div>" +
        $"<table style='width:200pt'><colgroup><col style='width:200pt'></colgroup><tbody><tr><td style='background-color:#ffe;{declarations}'>Total</td></tr></tbody></table>";

    [TestCase("print-color-adjust:exact")]
    [TestCase("print-color-adjust:economy")]
    [TestCase("-webkit-print-color-adjust:exact")]
    [TestCase("-webkit-print-color-adjust:exact;print-color-adjust:exact")]
    [TestCase("-WebKit-Print-Color-Adjust:EXACT")]
    [TestCase("print-color-adjust:inherit;-webkit-print-color-adjust:unset")]
    [TestCase("print-color-adjust:exact !important")]
    public void KeywordValuesRenderLikeThePageWithoutThem(string declarations)
    {
        Assert.That(Pdf(Report(declarations)), Is.EqualTo(Pdf(Report(""))));
    }

    [TestCase("print-color-adjust:auto")]
    [TestCase("print-color-adjust:exact exact")]
    [TestCase("-webkit-print-color-adjust:1")]
    [TestCase("print-color-adjust:'exact'")]
    public void OtherValuesFail(string declaration)
    {
        var html = $"<p style=\"{declaration}\">A</p>";
        var error = Assert.Throws<FactsPdfException>(() => Pdf(html));
        Assert.That(error!.Code, Is.EqualTo("FPDF1202"));
        Assert.That(error.Message, Does.Contain("print-color-adjust supports only a single keyword"));
        Assert.That(error.SourceOffset, Is.EqualTo(html.IndexOf(':') + 1));
    }

    [Test]
    public void TheLegacyColorAdjustNameStillFails()
    {
        var error = Assert.Throws<FactsPdfException>(() => Pdf("<p style='color-adjust:exact'>A</p>"));
        Assert.That(error!.Code, Is.EqualTo("FPDF1201"));
    }

    [Test]
    public void TheSdkAcceptsPrintColorAdjust()
    {
        const string page = "<p style='background-color:#eef'>Paid</p>";
        var bytes = FactsPDF.Pdf.Write().ToBytes($"<style>p{{-webkit-print-color-adjust:exact;print-color-adjust:exact}}</style>{page}");
        Assert.That(bytes, Is.EqualTo(FactsPDF.Pdf.Write().ToBytes(page)));
    }
}
