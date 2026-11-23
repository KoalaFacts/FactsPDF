using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// transform-origin only moves the point a transform pivots around. The only accepted transform
/// is none, so a well-formed origin moves nothing: it is validated and dropped and the page
/// renders byte-identical to the page without it.
/// </summary>
[TestFixture]
public sealed class TransformOriginTests
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
        $"<div class='box'><p style='{declarations}'>Paid in full</p></div>" +
        $"<table style='width:200pt'><colgroup><col style='width:200pt'></colgroup><tbody><tr><td style='{declarations}'>Total</td></tr></tbody></table>";

    [TestCase("transform-origin:center")]
    [TestCase("transform-origin:50% 50%")]
    [TestCase("transform-origin:top left")]
    [TestCase("transform-origin:left top")]
    [TestCase("transform-origin:center left")]
    [TestCase("transform-origin:bottom center")]
    [TestCase("transform-origin:0 0")]
    [TestCase("transform-origin:-0.0 +0e5")]
    [TestCase("transform-origin:10px 2em")]
    [TestCase("transform-origin:right 25%")]
    [TestCase("transform-origin:50% 50% 0")]
    [TestCase("transform-origin:left bottom -3pt")]
    [TestCase("TRANSFORM-ORIGIN:TOP RIGHT")]
    [TestCase("transform-origin:inherit")]
    [TestCase("transform-origin:center !important")]
    [TestCase("-webkit-transform-origin:0 0")]
    public void AcceptedValuesRenderLikeThePageWithoutThem(string declarations)
    {
        Assert.That(Pdf(Report(declarations)), Is.EqualTo(Pdf(Report(""))));
    }

    [TestCase("transform-origin:middle")]
    [TestCase("transform-origin:left right")]
    [TestCase("transform-origin:top bottom")]
    [TestCase("transform-origin:top 10px")]
    [TestCase("transform-origin:10px left")]
    [TestCase("transform-origin:1 2")]
    [TestCase("transform-origin:1e-999")]
    [TestCase("transform-origin:-1e-999 top")]
    [TestCase("transform-origin:10deg")]
    [TestCase("transform-origin:0 0 50%")]
    [TestCase("transform-origin:0 0 left")]
    [TestCase("transform-origin:0 0 0 0")]
    [TestCase("transform-origin:0, 0")]
    [TestCase("transform-origin:calc(50% + 1px)")]
    public void OtherValuesFail(string declaration)
    {
        var html = $"<p style=\"{declaration}\">A</p>";
        var error = Assert.Throws<FactsPdfException>(() => Pdf(html));
        Assert.That(error!.Code, Is.EqualTo("FPDF1202"));
        Assert.That(error.Message, Does.Contain("transform-origin supports only one or two positions"));
        Assert.That(error.SourceOffset, Is.EqualTo(html.IndexOf(':') + 1));
    }
}
