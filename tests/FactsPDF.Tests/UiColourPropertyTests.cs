using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// caret-color, accent-color, -webkit-tap-highlight-color, scrollbar-color and scrollbar-gutter
/// only style a text caret, native form controls, the WebKit tap flash or scrollbars. A PDF page
/// has none of them, so auto, keywords and well-formed colours (alpha included, because nothing
/// is painted) are validated and dropped and the page renders byte-identical without them.
/// </summary>
[TestFixture]
public sealed class UiColourPropertyTests
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

    [TestCase("-webkit-tap-highlight-color:transparent")]
    [TestCase("-webkit-tap-highlight-color:rgba(0,0,0,0)")]
    [TestCase("-webkit-tap-highlight-color:rgb(0 0 0 / 0%)")]
    [TestCase("-webkit-tap-highlight-color:#0000")]
    [TestCase("caret-color:auto")]
    [TestCase("caret-color:red")]
    [TestCase("caret-color:currentColor")]
    [TestCase("caret-color:hsl(210deg 50% 40%)")]
    [TestCase("accent-color:auto")]
    [TestCase("accent-color:#0d6efd")]
    [TestCase("accent-color:#0d6efd80")]
    [TestCase("scrollbar-color:auto")]
    [TestCase("scrollbar-color:#888 transparent")]
    [TestCase("scrollbar-color:rgba(0,0,0,.3) #f1f1f1")]
    [TestCase("scrollbar-gutter:auto")]
    [TestCase("scrollbar-gutter:stable")]
    [TestCase("scrollbar-gutter:stable both-edges")]
    [TestCase("scrollbar-gutter:both-edges stable")]
    [TestCase("-WEBKIT-TAP-HIGHLIGHT-COLOR:TRANSPARENT;CARET-COLOR:AUTO")]
    [TestCase("caret-color:inherit;accent-color:unset;scrollbar-color:initial;scrollbar-gutter:inherit")]
    [TestCase("-webkit-tap-highlight-color:transparent !important")]
    public void ValuesRenderLikeThePageWithoutThem(string declarations)
    {
        Assert.That(Pdf(Report(declarations)), Is.EqualTo(Pdf(Report(""))));
    }

    [TestCase("-webkit-tap-highlight-color:auto", "-webkit-tap-highlight-color supports only a colour")]
    [TestCase("-webkit-tap-highlight-color:#12", "-webkit-tap-highlight-color supports only a colour")]
    [TestCase("-webkit-tap-highlight-color:red blue", "-webkit-tap-highlight-color supports only a colour")]
    [TestCase("caret-color:notacolour", "caret-color supports only auto or a colour")]
    [TestCase("caret-color:url(x.png)", "caret-color supports only auto or a colour")]
    [TestCase("caret-color:rgb(1,2)", "caret-color supports only auto or a colour")]
    [TestCase("accent-color:color-mix(in srgb, red, blue)", "accent-color supports only auto or a colour")]
    [TestCase("accent-color:#ggg", "accent-color supports only auto or a colour")]
    [TestCase("scrollbar-color:red", "scrollbar-color supports only auto or two colours")]
    [TestCase("scrollbar-color:auto red", "scrollbar-color supports only auto or two colours")]
    [TestCase("scrollbar-color:red blue green", "scrollbar-color supports only auto or two colours")]
    [TestCase("scrollbar-gutter:both-edges", "scrollbar-gutter supports only auto, stable or stable both-edges")]
    [TestCase("scrollbar-gutter:stable stable", "scrollbar-gutter supports only auto, stable or stable both-edges")]
    [TestCase("scrollbar-gutter:8px", "scrollbar-gutter supports only auto, stable or stable both-edges")]
    public void OtherValuesFail(string declaration, string message)
    {
        var html = $"<p style=\"{declaration}\">A</p>";
        var error = Assert.Throws<FactsPdfException>(() => Pdf(html));
        Assert.That(error!.Code, Is.EqualTo("FPDF1202"));
        Assert.That(error.Message, Does.Contain(message));
        Assert.That(error.SourceOffset, Is.EqualTo(html.IndexOf(':') + 1));
    }

    [TestCase("-moz-tap-highlight-color:transparent")]
    [TestCase("-webkit-scrollbar-color:red blue")]
    [TestCase("-ms-overflow-style:none")]
    public void OtherPrefixesStillFail(string declaration)
    {
        var error = Assert.Throws<FactsPdfException>(() => Pdf($"<p style='{declaration}'>A</p>"));
        Assert.That(error!.Code, Is.EqualTo("FPDF1201"));
    }
}
