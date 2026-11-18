using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// color-scheme lets a browser pick light or dark user-agent defaults and forced-color-adjust
/// opts out of a forced high-contrast palette. A PDF page has no preferred or forced colour
/// scheme and paints every colour exactly as declared, so well-formed values are validated and
/// dropped and the page renders byte-identical without them.
/// </summary>
[TestFixture]
public sealed class ColorSchemeHintTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 400, Margin = 20 };

    private static byte[] Pdf(string html)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, Page);
        return output.ToArray();
    }

    private static string Report(string declarations) =>
        $"<style>html{{{declarations}}} body{{{declarations}}} h2{{color:#234;{declarations}}} .box{{width:200pt;background:#eef;{declarations}}} .unused{{{declarations}}}</style>" +
        "<h2>Invoice</h2>" +
        $"<div class='box'><p style='{declarations}'>Paid in full</p></div>" +
        $"<table style='width:200pt'><colgroup><col style='width:200pt'></colgroup><tbody><tr><td style='{declarations}'>Total</td></tr></tbody></table>";

    [TestCase("color-scheme:normal")]
    [TestCase("color-scheme:light")]
    [TestCase("color-scheme:dark")]
    [TestCase("color-scheme:light dark")]
    [TestCase("color-scheme:dark light")]
    [TestCase("color-scheme:only light")]
    [TestCase("color-scheme:light only")]
    [TestCase("color-scheme:light dark brand-theme")]
    [TestCase("COLOR-SCHEME:LIGHT DARK")]
    [TestCase("color-scheme:inherit")]
    [TestCase("color-scheme:light dark !important")]
    [TestCase("forced-color-adjust:auto")]
    [TestCase("forced-color-adjust:none")]
    [TestCase("forced-color-adjust:preserve-parent-color")]
    [TestCase("forced-color-adjust:unset")]
    [TestCase("color-scheme:light dark;forced-color-adjust:none")]
    public void AcceptedValuesRenderLikeThePageWithoutThem(string declarations)
    {
        Assert.That(Pdf(Report(declarations)), Is.EqualTo(Pdf(Report(""))));
    }

    [TestCase("color-scheme:only", "color-scheme supports only normal, or scheme names")]
    [TestCase("color-scheme:light normal", "color-scheme supports only normal, or scheme names")]
    [TestCase("color-scheme:only light only", "color-scheme supports only normal, or scheme names")]
    [TestCase("color-scheme:light only dark", "color-scheme supports only normal, or scheme names")]
    [TestCase("color-scheme:light inherit", "color-scheme supports only normal, or scheme names")]
    [TestCase("color-scheme:light, dark", "color-scheme supports only normal, or scheme names")]
    [TestCase("color-scheme:#fff", "color-scheme supports only normal, or scheme names")]
    [TestCase("color-scheme:default", "color-scheme supports only normal, or scheme names")]
    [TestCase("forced-color-adjust:exact", "forced-color-adjust supports only a single keyword")]
    [TestCase("forced-color-adjust:auto none", "forced-color-adjust supports only a single keyword")]
    public void MalformedValuesFail(string declaration, string message)
    {
        var html = $"<p style=\"{declaration}\">A</p>";
        var error = Assert.Throws<FactsPdfException>(() => Pdf(html));
        Assert.That(error!.Code, Is.EqualTo("FPDF1202"));
        Assert.That(error.Message, Does.Contain(message));
        Assert.That(error.SourceOffset, Is.EqualTo(html.IndexOf(':') + 1));
    }

    [TestCase("-webkit-color-scheme")]
    [TestCase("-ms-high-contrast-adjust")]
    [TestCase("supported-color-schemes")]
    public void OtherNamesStillFail(string name)
    {
        var error = Assert.Throws<FactsPdfException>(() => Pdf($"<p style='{name}:light'>A</p>"));
        Assert.That(error!.Code, Is.EqualTo("FPDF1201"));
    }
}
