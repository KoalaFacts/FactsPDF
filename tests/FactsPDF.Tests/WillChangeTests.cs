using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// will-change only tells a browser compositor which features are about to animate. A PDF
/// page never changes after it is written, so auto or a comma-separated list of feature names
/// is validated and dropped and the page renders byte-identical to the page without it.
/// </summary>
[TestFixture]
public sealed class WillChangeTests
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

    [TestCase("will-change:auto")]
    [TestCase("will-change:transform")]
    [TestCase("will-change:opacity")]
    [TestCase("will-change:transform, opacity")]
    [TestCase("will-change:transform,opacity,scroll-position")]
    [TestCase("will-change:contents")]
    [TestCase("WILL-CHANGE:TRANSFORM")]
    [TestCase("will-change:inherit")]
    [TestCase("will-change:transform !important")]
    public void AcceptedValuesRenderLikeThePageWithoutThem(string declarations)
    {
        Assert.That(Pdf(Report(declarations)), Is.EqualTo(Pdf(Report(""))));
    }

    [TestCase("will-change:none")]
    [TestCase("will-change:auto, transform")]
    [TestCase("will-change:transform, auto")]
    [TestCase("will-change:transform opacity")]
    [TestCase("will-change:transform,")]
    [TestCase("will-change:, transform")]
    [TestCase("will-change:transform, inherit")]
    [TestCase("will-change:will-change")]
    [TestCase("will-change:1")]
    [TestCase("will-change:'transform'")]
    public void OtherValuesFail(string declaration)
    {
        var html = $"<p style=\"{declaration}\">A</p>";
        var error = Assert.Throws<FactsPdfException>(() => Pdf(html));
        Assert.That(error!.Code, Is.EqualTo("FPDF1202"));
        Assert.That(error.Message, Does.Contain("will-change supports only auto or a comma-separated list of property names"));
        Assert.That(error.SourceOffset, Is.EqualTo(html.IndexOf(':') + 1));
    }
}
