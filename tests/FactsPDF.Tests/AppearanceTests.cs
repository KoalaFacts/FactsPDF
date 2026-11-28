using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// appearance (and its -webkit-/-moz- spellings) only switches native form-control styling.
/// The renderer draws no native widgets, so every keyword is validated and dropped and the
/// page renders byte-identical to the page without the declaration.
/// </summary>
[TestFixture]
public sealed class AppearanceTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 400, Margin = 20 };

    private static byte[] Pdf(string html)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, Page);
        return output.ToArray();
    }

    private static string Report(string declarations) =>
        $"<style>h1,h2,h3,h4,h5,h6,th{{font-weight:normal}}body{{{declarations}}} h2{{color:#234;{declarations}}} .box{{width:200pt;background:#eef;border:1pt solid #c00;{declarations}}} .unused{{{declarations}}}</style>" +
        "<h2>Invoice</h2>" +
        $"<div class='box'><p style='{declarations}'>Paid in full</p></div>" +
        $"<table style='width:200pt'><colgroup><col style='width:200pt'></colgroup><tbody><tr><td style='{declarations}'>Total</td></tr></tbody></table>";

    [TestCase("appearance:none")]
    [TestCase("-webkit-appearance:none")]
    [TestCase("-moz-appearance:none")]
    [TestCase("-webkit-appearance:none;-moz-appearance:none;appearance:none")]
    [TestCase("appearance:auto")]
    [TestCase("-webkit-appearance:button")]
    [TestCase("-moz-appearance:textfield")]
    [TestCase("appearance:menulist-button")]
    [TestCase("-WEBKIT-APPEARANCE:NONE")]
    [TestCase("appearance:inherit;-webkit-appearance:unset")]
    [TestCase("appearance:none !important")]
    public void KeywordValuesRenderLikeThePageWithoutThem(string declarations)
    {
        Assert.That(Pdf(Report(declarations)), Is.EqualTo(Pdf(Report(""))));
    }

    [TestCase("appearance:none none")]
    [TestCase("-webkit-appearance:caret")]
    [TestCase("appearance:0")]
    [TestCase("-moz-appearance:'none'")]
    public void OtherValuesFail(string declaration)
    {
        var html = $"<p style=\"{declaration}\">A</p>";
        var error = Assert.Throws<FactsPdfException>(() => Pdf(html));
        Assert.That(error!.Code, Is.EqualTo("FPDF1202"));
        Assert.That(error.Message, Does.Contain("appearance supports only a single keyword"));
        Assert.That(error.SourceOffset, Is.EqualTo(html.IndexOf(':') + 1));
    }

    [Test]
    public void OtherPrefixesStillFail()
    {
        var error = Assert.Throws<FactsPdfException>(() => Pdf("<p style='-ms-appearance:none'>A</p>"));
        Assert.That(error!.Code, Is.EqualTo("FPDF1201"));
    }
}
