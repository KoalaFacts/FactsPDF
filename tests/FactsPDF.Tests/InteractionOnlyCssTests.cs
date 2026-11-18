using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// cursor, pointer-events, user-select and resize only change how a screen reacts to a
/// pointer; a PDF page has none. Every keyword value of them (and a CSS-wide keyword) is
/// validated and dropped, so the page renders byte-identical to the page without them.
/// Any other value (an image, a number, several components) fails with FPDF1202.
/// </summary>
[TestFixture]
public sealed class InteractionOnlyCssTests
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

    [TestCase("cursor:pointer")]
    [TestCase("cursor:auto;cursor:default;cursor:not-allowed;cursor:nwse-resize;cursor:zoom-out")]
    [TestCase("pointer-events:none")]
    [TestCase("pointer-events:auto;pointer-events:visiblePainted")]
    [TestCase("user-select:none")]
    [TestCase("user-select:text;user-select:all;user-select:auto")]
    [TestCase("resize:none;resize:both;resize:vertical")]
    [TestCase("CURSOR:Pointer;User-Select:NONE")]
    [TestCase("cursor:inherit;pointer-events:initial;user-select:unset;resize:inherit")]
    [TestCase("cursor:pointer!important;user-select:none !important")]
    [TestCase("cursor: pointer ;pointer-events: none")]
    public void KeywordValuesRenderLikeThePageWithoutThem(string declarations)
    {
        Assert.That(Pdf(Report(declarations)), Is.EqualTo(Pdf(Report(""))));
    }

    [TestCase("cursor:url(hand.png), pointer")]
    [TestCase("cursor:url(hand.png)")]
    [TestCase("cursor:hand")]
    [TestCase("cursor:pointer pointer")]
    [TestCase("cursor:'pointer'")]
    [TestCase("cursor:0")]
    [TestCase("pointer-events:bounding-box")]
    [TestCase("user-select:element")]
    [TestCase("resize:auto")]
    [TestCase("resize:var(--r)")]
    [TestCase("user-select:")]
    public void OtherValuesFail(string declaration)
    {
        var error = Assert.Throws<FactsPdfException>(() => Pdf($"<p style=\"{declaration}\">A</p>"));
        Assert.That(error!.Code, Is.EqualTo("FPDF1202"));
    }

    [TestCase("<style>.unused{cursor:url(a.png)}</style><p>A</p>", "cursor supports only a single keyword", "url")]
    [TestCase("<p style='resize:auto'>A</p>", "resize supports only a single keyword", "auto")]
    public void FailuresNameThePropertyAndTheValue(string html, string message, string at)
    {
        var error = Assert.Throws<FactsPdfException>(() => Pdf(html));
        Assert.That(error!.Code, Is.EqualTo("FPDF1202"));
        Assert.That(error.Message, Does.Contain(message));
        Assert.That(error.SourceOffset, Is.EqualTo(html.IndexOf(at, StringComparison.Ordinal)));
    }

    [Test]
    public void UnaliasedVendorPrefixedNamesStillFail()
    {
        // -webkit-/-moz-/-ms-user-select are aliases of user-select (ScreenGestureHintTests).
        var error = Assert.Throws<FactsPdfException>(() => Pdf("<p style='-o-user-select:none'>A</p>"));
        Assert.That(error!.Code, Is.EqualTo("FPDF1201"));
    }

    [Test]
    public void TheSdkAcceptsInteractionKeywords()
    {
        var bytes = FactsPDF.Pdf.Write().ToBytes("<p style='cursor:pointer;user-select:none;pointer-events:none'>Paid</p>");
        Assert.That(bytes, Is.EqualTo(FactsPDF.Pdf.Write().ToBytes("<p>Paid</p>")));
    }
}
