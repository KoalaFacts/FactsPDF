using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// backface-visibility only matters for a 3D-transformed element that shows its back face (which fails)
/// and touch-action only selects which touch gestures a screen handles. Neither can change a
/// PDF page, so their keywords are validated and dropped and the page renders byte-identical
/// to the page without the declaration.
/// </summary>
[TestFixture]
public sealed class BackfaceTouchActionTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 400, Margin = 20 };

    private static byte[] Pdf(string html)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, Page);
        return output.ToArray();
    }

    private static string Report(string declarations) =>
        $"<style>body{{{declarations}}} h2{{font-weight:normal;color:#234;{declarations}}} .box{{width:200pt;background:#eef;border:1pt solid #c00;{declarations}}} .unused{{{declarations}}}</style>" +
        "<h2>Invoice</h2>" +
        $"<div class='box'><p style='{declarations}'>Paid in full</p></div>" +
        $"<table style='width:200pt'><colgroup><col style='width:200pt'></colgroup><tbody><tr><td style='{declarations}'>Total</td></tr></tbody></table>";

    [TestCase("backface-visibility:hidden")]
    [TestCase("backface-visibility:visible")]
    [TestCase("-webkit-backface-visibility:hidden")]
    [TestCase("-webkit-backface-visibility:hidden;backface-visibility:hidden;transform:none")]
    [TestCase("touch-action:manipulation")]
    [TestCase("touch-action:none")]
    [TestCase("touch-action:pan-y")]
    [TestCase("-WEBKIT-BACKFACE-VISIBILITY:HIDDEN;TOUCH-ACTION:AUTO")]
    [TestCase("backface-visibility:inherit;touch-action:unset")]
    [TestCase("touch-action:manipulation !important")]
    public void KeywordValuesRenderLikeThePageWithoutThem(string declarations)
    {
        Assert.That(Pdf(Report(declarations)), Is.EqualTo(Pdf(Report(""))));
    }

    [TestCase("backface-visibility:none", "backface-visibility")]
    [TestCase("-webkit-backface-visibility:hidden hidden", "backface-visibility")]
    [TestCase("touch-action:pan-x pan-y", "touch-action")]
    [TestCase("touch-action:double-tap-zoom", "touch-action")]
    [TestCase("touch-action:0", "touch-action")]
    public void OtherValuesFail(string declaration, string name)
    {
        var html = $"<p style=\"{declaration}\">A</p>";
        var error = Assert.Throws<FactsPdfException>(() => Pdf(html));
        Assert.That(error!.Code, Is.EqualTo("FPDF1202"));
        Assert.That(error.Message, Does.Contain($"{name} supports only a single keyword"));
        Assert.That(error.SourceOffset, Is.EqualTo(html.IndexOf(':') + 1));
    }

    [TestCase("-moz-backface-visibility:hidden")]
    [TestCase("-ms-touch-action:none")]
    public void OtherPrefixesStillFail(string declaration)
    {
        var error = Assert.Throws<FactsPdfException>(() => Pdf($"<p style='{declaration}'>A</p>"));
        Assert.That(error!.Code, Is.EqualTo("FPDF1201"));
    }
}
