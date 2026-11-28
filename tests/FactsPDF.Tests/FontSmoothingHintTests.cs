using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// text-rendering, -webkit-font-smoothing and -moz-osx-font-smoothing only steer how a screen
/// rasteriser draws glyphs. A PDF stores glyph outlines and the viewer picks anti-aliasing and
/// hinting, so every keyword is validated and dropped and the page renders byte-identical to the
/// page without the declaration.
/// </summary>
[TestFixture]
public sealed class FontSmoothingHintTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 400, Margin = 20 };

    private static byte[] Pdf(string html)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, Page);
        return output.ToArray();
    }

    private static string Report(string declarations) =>
        $"<style>h2,th{{font-weight:normal}} html,body{{{declarations}}} h2{{color:#234;{declarations}}} .box{{width:200pt;background:#eef;border:1pt solid #c00;{declarations}}} .unused{{{declarations}}}</style>" +
        "<h2>Invoice</h2>" +
        $"<div class='box'><p style='color:#060;{declarations}'>Paid in full</p></div>" +
        $"<table style='width:200pt'><colgroup><col style='width:200pt'></colgroup><tbody><tr><td style='{declarations}'>Total</td></tr></tbody></table>";

    [TestCase("text-rendering:auto")]
    [TestCase("text-rendering:optimizeLegibility")]
    [TestCase("text-rendering:optimizeSpeed")]
    [TestCase("text-rendering:geometricPrecision")]
    [TestCase("-webkit-font-smoothing:antialiased")]
    [TestCase("-webkit-font-smoothing:subpixel-antialiased")]
    [TestCase("-webkit-font-smoothing:none")]
    [TestCase("-moz-osx-font-smoothing:grayscale")]
    [TestCase("-moz-osx-font-smoothing:auto")]
    [TestCase("-webkit-font-smoothing:antialiased;-moz-osx-font-smoothing:grayscale;text-rendering:optimizeLegibility")]
    [TestCase("-WEBKIT-Font-Smoothing:ANTIALIASED")]
    [TestCase("text-rendering:inherit;-webkit-font-smoothing:unset;-moz-osx-font-smoothing:initial")]
    [TestCase("text-rendering:optimizeLegibility !important")]
    public void KeywordValuesRenderLikeThePageWithoutThem(string declarations)
    {
        Assert.That(Pdf(Report(declarations)), Is.EqualTo(Pdf(Report(""))));
    }

    [TestCase("text-rendering:optimize-legibility", "text-rendering")]
    [TestCase("text-rendering:auto auto", "text-rendering")]
    [TestCase("-webkit-font-smoothing:grayscale", "-webkit-font-smoothing")]
    [TestCase("-webkit-font-smoothing:1px", "-webkit-font-smoothing")]
    [TestCase("-moz-osx-font-smoothing:antialiased", "-moz-osx-font-smoothing")]
    [TestCase("-moz-osx-font-smoothing:'grayscale'", "-moz-osx-font-smoothing")]
    public void OtherValuesFail(string declaration, string name)
    {
        var html = $"<p style=\"{declaration}\">A</p>";
        var error = Assert.Throws<FactsPdfException>(() => Pdf(html));
        Assert.That(error!.Code, Is.EqualTo("FPDF1202"));
        Assert.That(error.Message, Does.Contain($"{name} supports only a single keyword"));
        Assert.That(error.SourceOffset, Is.EqualTo(html.IndexOf(':') + 1));
    }

    [TestCase("font-smooth:always")]
    [TestCase("-webkit-text-stroke:1px")]
    public void NeighbouringUnsupportedNamesStillFail(string declaration)
    {
        var error = Assert.Throws<FactsPdfException>(() => Pdf($"<p style='{declaration}'>A</p>"));
        Assert.That(error!.Code, Is.EqualTo("FPDF1201"));
    }
}
