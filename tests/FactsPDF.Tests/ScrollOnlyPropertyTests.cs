using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// scroll-behavior, overscroll-behavior (and its -x/-y/-inline/-block longhands),
/// overflow-anchor, -webkit-overflow-scrolling and scrollbar-width only steer a scroll
/// container. A PDF page never scrolls (overflow accepts only visible), so their keywords are
/// validated and dropped and the page renders byte-identical to the page without them.
/// </summary>
[TestFixture]
public sealed class ScrollOnlyPropertyTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 400, Margin = 20 };

    private static byte[] Pdf(string html)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, Page);
        return output.ToArray();
    }

    private static string Report(string declarations) =>
        $"<style>html{{{declarations}}} body{{{declarations}}} h2{{color:#234;{declarations}}} .box{{width:200pt;background:#eef;border:1pt solid #c00;{declarations}}} .unused{{{declarations}}}</style>" +
        "<h2>Invoice</h2>" +
        $"<div class='box'><p style='{declarations}'>Paid in full</p></div>" +
        $"<table style='width:200pt'><colgroup><col style='width:200pt'></colgroup><tbody><tr><td style='{declarations}'>Total</td></tr></tbody></table>";

    [TestCase("scroll-behavior:smooth")]
    [TestCase("scroll-behavior:auto")]
    [TestCase("overscroll-behavior:none")]
    [TestCase("overscroll-behavior:contain")]
    [TestCase("overscroll-behavior-x:auto;overscroll-behavior-y:contain")]
    [TestCase("overscroll-behavior-inline:none;overscroll-behavior-block:auto")]
    [TestCase("overflow-anchor:none")]
    [TestCase("overflow-anchor:auto")]
    [TestCase("-webkit-overflow-scrolling:touch")]
    [TestCase("-webkit-overflow-scrolling:auto")]
    [TestCase("scrollbar-width:thin")]
    [TestCase("scrollbar-width:none")]
    [TestCase("scrollbar-width:auto")]
    [TestCase("SCROLL-BEHAVIOR:SMOOTH;-WEBKIT-OVERFLOW-SCROLLING:TOUCH")]
    [TestCase("scroll-behavior:inherit;overscroll-behavior:unset;scrollbar-width:initial")]
    [TestCase("scroll-behavior:smooth !important")]
    [TestCase("overflow:visible;scroll-behavior:smooth;overscroll-behavior:none;scrollbar-width:none")]
    public void KeywordValuesRenderLikeThePageWithoutThem(string declarations)
    {
        Assert.That(Pdf(Report(declarations)), Is.EqualTo(Pdf(Report(""))));
    }

    [TestCase("scroll-behavior:instant", "scroll-behavior")]
    [TestCase("scroll-behavior:smooth smooth", "scroll-behavior")]
    [TestCase("overscroll-behavior:contain none", "overscroll-behavior")]
    [TestCase("overscroll-behavior-x:scroll", "overscroll-behavior-x")]
    [TestCase("overflow-anchor:0", "overflow-anchor")]
    [TestCase("-webkit-overflow-scrolling:momentum", "-webkit-overflow-scrolling")]
    [TestCase("scrollbar-width:8px", "scrollbar-width")]
    [TestCase("scrollbar-width:thick", "scrollbar-width")]
    public void OtherValuesFail(string declaration, string name)
    {
        var html = $"<p style=\"{declaration}\">A</p>";
        var error = Assert.Throws<FactsPdfException>(() => Pdf(html));
        Assert.That(error!.Code, Is.EqualTo("FPDF1202"));
        Assert.That(error.Message, Does.Contain($"{name} supports only a single keyword"));
        Assert.That(error.SourceOffset, Is.EqualTo(html.IndexOf(':') + 1));
    }

    [TestCase("-moz-scroll-behavior:smooth")]
    [TestCase("-ms-overflow-style:none")]
    [TestCase("scrollbar-color:auto")]
    [TestCase("scrollbar-gutter:stable")]
    public void OtherScrollPropertiesStillFail(string declaration)
    {
        var error = Assert.Throws<FactsPdfException>(() => Pdf($"<p style='{declaration}'>A</p>"));
        Assert.That(error!.Code, Is.EqualTo("FPDF1201"));
    }
}
