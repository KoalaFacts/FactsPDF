using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// The prefixed user-select spellings (-webkit-, -moz-, -ms-) are aliases of user-select, and
/// -webkit-user-drag, -webkit-touch-callout and -ms-overflow-style only steer pointer drag, the
/// iOS long-press callout and the legacy Edge/IE scrollbar. A PDF page has none of these, so
/// every keyword (and a CSS-wide keyword) is validated and dropped; other values fail FPDF1202.
/// </summary>
[TestFixture]
public sealed class ScreenGestureHintTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 400, Margin = 20 };

    private static byte[] Pdf(string html)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, Page);
        return output.ToArray();
    }

    private static string Report(string declarations) =>
        $"<style>h2{{{declarations}}} .box{{width:200pt;border:1pt solid #333;{declarations}}} .unused{{{declarations}}}</style>" +
        "<h2>Invoice</h2>" +
        $"<div class='box'><p style='{declarations}'>Paid in full</p></div>" +
        $"<table style='width:200pt'><colgroup><col style='width:200pt'></colgroup><tbody><tr><td style='{declarations}'>Total</td></tr></tbody></table>" +
        $"<ul><li style='{declarations}'>Line <a href='https://example.com/' style='{declarations}'>item</a></li></ul>";

    [TestCase("-webkit-user-select:none;-moz-user-select:none;-ms-user-select:none;user-select:none")]
    [TestCase("-webkit-user-select:text;-moz-user-select:all;-ms-user-select:auto")]
    [TestCase("-webkit-user-drag:none")]
    [TestCase("-webkit-user-drag:auto;-webkit-user-drag:element")]
    [TestCase("-webkit-touch-callout:none")]
    [TestCase("-webkit-touch-callout:default")]
    [TestCase("-ms-overflow-style:none")]
    [TestCase("-ms-overflow-style:auto;-ms-overflow-style:scrollbar;-ms-overflow-style:-ms-autohiding-scrollbar")]
    [TestCase("-WEBKIT-USER-DRAG:None;-Webkit-Touch-Callout:NONE;-MS-USER-SELECT:None")]
    [TestCase("-webkit-user-drag:inherit;-webkit-touch-callout:initial;-ms-overflow-style:unset;-moz-user-select:inherit")]
    [TestCase("-webkit-user-select:none!important;-webkit-touch-callout:none !important")]
    public void KeywordValuesRenderLikeThePageWithoutThem(string declarations)
    {
        Assert.That(Pdf(Report(declarations)), Is.EqualTo(Pdf(Report(""))));
    }

    [TestCase("-webkit-user-select:element")]
    [TestCase("-moz-user-select:-moz-none")]
    [TestCase("-ms-user-select:none none")]
    [TestCase("-webkit-user-drag:yes")]
    [TestCase("-webkit-user-drag:none auto")]
    [TestCase("-webkit-touch-callout:auto")]
    [TestCase("-webkit-touch-callout:0")]
    [TestCase("-ms-overflow-style:hidden")]
    [TestCase("-ms-overflow-style:'none'")]
    public void OtherValuesFail(string declaration)
    {
        var error = Assert.Throws<FactsPdfException>(() => Pdf($"<p style=\"{declaration}\">A</p>"));
        Assert.That(error!.Code, Is.EqualTo("FPDF1202"));
    }

    [Test]
    public void APrefixedUserSelectFailsLikeUserSelect()
    {
        var standard = Assert.Throws<FactsPdfException>(() => Pdf("<p style='user-select:element'>A</p>"));
        var prefixed = Assert.Throws<FactsPdfException>(() => Pdf("<p style='-webkit-user-select:element'>A</p>"));
        Assert.That(prefixed!.Message, Is.EqualTo(standard!.Message));
    }

    [TestCase("-o-user-select")]
    [TestCase("-khtml-user-drag")]
    [TestCase("-webkit-touch-callout-x")]
    [TestCase("-ms-overflow-style-x")]
    public void OtherNamesStillFail(string name)
    {
        var error = Assert.Throws<FactsPdfException>(() => Pdf($"<p style='{name}:none'>A</p>"));
        Assert.That(error!.Code, Is.EqualTo("FPDF1201"));
    }
}
