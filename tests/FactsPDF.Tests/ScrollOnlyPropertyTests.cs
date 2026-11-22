using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// scroll-behavior, overscroll-behavior (and its -x/-y/-inline/-block longhands),
/// overflow-anchor, -webkit-overflow-scrolling, scrollbar-width and the scroll-margin and
/// scroll-padding offsets only steer a scroll
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
    [TestCase("-ms-scroll-chaining:none")]
    [TestCase("scroll-snap-type:x mandatory")]
    public void OtherScrollPropertiesStillFail(string declaration)
    {
        var error = Assert.Throws<FactsPdfException>(() => Pdf($"<p style='{declaration}'>A</p>"));
        Assert.That(error!.Code, Is.EqualTo("FPDF1201"));
    }

    // Scroll-into-view offsets: a PDF page is never scrolled to an anchor, so scroll-margin and
    // scroll-padding (and every side and axis longhand) are validated and dropped.
    [TestCase("scroll-margin-top:4rem")]
    [TestCase("scroll-margin:0")]
    [TestCase("scroll-margin:1px -2pt 3mm 4em")]
    [TestCase("scroll-margin-inline:1rem 2rem;scroll-margin-block:-8px")]
    [TestCase("scroll-margin-right:1in;scroll-margin-bottom:2cm;scroll-margin-left:3pc;scroll-margin-inline-start:1ch;scroll-margin-inline-end:1ex;scroll-margin-block-start:10vh;scroll-margin-block-end:1q")]
    [TestCase("scroll-padding:auto")]
    [TestCase("scroll-padding-top:80px")]
    [TestCase("scroll-padding:10% auto 0 2rem")]
    [TestCase("scroll-padding-inline:auto 5%;scroll-padding-block:1em")]
    [TestCase("scroll-padding-right:0;scroll-padding-bottom:1pt;scroll-padding-left:auto;scroll-padding-inline-start:2px;scroll-padding-inline-end:3px;scroll-padding-block-start:4px;scroll-padding-block-end:5px")]
    [TestCase("SCROLL-MARGIN-TOP:4REM;scroll-padding:inherit;scroll-margin:unset;scroll-padding-top:4rem !important")]
    public void ScrollOffsetsRenderLikeThePageWithoutThem(string declarations)
    {
        Assert.That(Pdf(Report(declarations)), Is.EqualTo(Pdf(Report(""))));
    }

    [TestCase("scroll-margin-top:auto", "scroll-margin-top supports only one length")]
    [TestCase("scroll-margin-top:10%", "scroll-margin-top supports only one length")]
    [TestCase("scroll-margin-top:1rem 2rem", "scroll-margin-top supports only one length")]
    [TestCase("scroll-margin:1px 2px 3px 4px 5px", "scroll-margin supports only one to four lengths")]
    [TestCase("scroll-margin-inline:1px 2px 3px", "scroll-margin-inline supports only one or two lengths")]
    [TestCase("scroll-margin:5", "scroll-margin supports only one to four lengths")]
    [TestCase("scroll-padding-top:-1px", "scroll-padding-top supports only one of auto or a non-negative length or percentage")]
    [TestCase("scroll-padding:calc(1px + 2px)", "scroll-padding supports only one to four of auto or a non-negative length or percentage")]
    [TestCase("scroll-padding-block:1deg", "scroll-padding-block supports only one or two of auto")]
    public void InvalidScrollOffsetsFail(string declaration, string message)
    {
        var html = $"<p style=\"{declaration}\">A</p>";
        var error = Assert.Throws<FactsPdfException>(() => Pdf(html));
        Assert.That(error!.Code, Is.EqualTo("FPDF1202"));
        Assert.That(error.Message, Does.Contain(message));
        Assert.That(error.SourceOffset, Is.EqualTo(html.IndexOf(':') + 1));
    }
}
