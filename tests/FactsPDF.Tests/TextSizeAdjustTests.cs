using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// text-size-adjust (and the -webkit-, -ms- and -moz- spellings that normalize.css and resets use) only
/// controls mobile browsers' automatic font inflation. A PDF page is never inflated, so auto,
/// none and non-negative percentages are validated and dropped and the page renders
/// byte-identical to the page without the declaration.
/// </summary>
[TestFixture]
public sealed class TextSizeAdjustTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 400, Margin = 20 };

    private static byte[] Pdf(string html)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, Page);
        return output.ToArray();
    }

    private static string Report(string declarations) =>
        $"<style>html{{line-height:1.15;{declarations}}} body{{{declarations}}} h2{{font-weight:normal;color:#234;{declarations}}} .box{{width:200pt;background:#eef;{declarations}}}</style>" +
        "<h2>Invoice</h2>" +
        $"<div class='box'><p style='{declarations}'>Paid in full</p></div>" +
        $"<table style='width:200pt'><colgroup><col style='width:200pt'></colgroup><tbody><tr><td style='{declarations}'>Total</td></tr></tbody></table>";

    [TestCase("-webkit-text-size-adjust:100%")]
    [TestCase("text-size-adjust:100%")]
    [TestCase("-webkit-text-size-adjust:100%;text-size-adjust:100%")]
    [TestCase("-webkit-text-size-adjust:none")]
    [TestCase("text-size-adjust:auto")]
    [TestCase("text-size-adjust:0%")]
    [TestCase("text-size-adjust:150.5%")]
    [TestCase("-WEBKIT-TEXT-SIZE-ADJUST:NONE")]
    [TestCase("text-size-adjust:inherit;-webkit-text-size-adjust:unset")]
    [TestCase("-webkit-text-size-adjust:100% !important")]
    [TestCase("-ms-text-size-adjust:100%")]
    [TestCase("-ms-text-size-adjust:100%;-webkit-text-size-adjust:100%")]
    [TestCase("-moz-text-size-adjust:none")]
    [TestCase("-MS-TEXT-SIZE-ADJUST:AUTO")]
    [TestCase("-ms-text-size-adjust:inherit !important")]
    public void AcceptedValuesRenderLikeThePageWithoutThem(string declarations)
    {
        Assert.That(Pdf(Report(declarations)), Is.EqualTo(Pdf(Report(""))));
    }

    [TestCase("text-size-adjust:-10%")]
    [TestCase("text-size-adjust:1")]
    [TestCase("text-size-adjust:12pt")]
    [TestCase("-webkit-text-size-adjust:100% 100%")]
    [TestCase("-webkit-text-size-adjust:always")]
    [TestCase("text-size-adjust:env(x)")]
    [TestCase("-ms-text-size-adjust:12pt")]
    [TestCase("-moz-text-size-adjust:always")]
    public void OtherValuesFail(string declaration)
    {
        var html = $"<p style=\"{declaration}\">A</p>";
        var error = Assert.Throws<FactsPdfException>(() => Pdf(html));
        Assert.That(error!.Code, Is.EqualTo("FPDF1202"));
        Assert.That(error.Message, Does.Contain("text-size-adjust supports only auto, none or a non-negative percentage"));
        Assert.That(error.SourceOffset, Is.EqualTo(html.IndexOf(':') + 1));
    }

    [TestCase("-o-text-size-adjust")]
    [TestCase("-khtml-text-size-adjust")]
    public void OtherPrefixesStillFail(string name)
    {
        var error = Assert.Throws<FactsPdfException>(() => Pdf($"<p style='{name}:100%'>A</p>"));
        Assert.That(error!.Code, Is.EqualTo("FPDF1201"));
    }
}
