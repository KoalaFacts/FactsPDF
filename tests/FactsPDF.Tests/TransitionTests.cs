using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// Transitions only animate a change of computed value on screen. A written PDF page is never
/// restyled, so a well-formed transition (shorthand or longhand) is validated and dropped and
/// the page renders byte-identical to the page without it.
/// </summary>
[TestFixture]
public sealed class TransitionTests
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
        "<h2 style='font-weight:normal'>Invoice</h2>" +
        $"<div class='box'><p style='{declarations}'>Paid in full</p></div>" +
        $"<table style='width:200pt'><colgroup><col style='width:200pt'></colgroup><tbody><tr><td style='{declarations}'>Total</td></tr></tbody></table>";

    [TestCase("transition:none")]
    [TestCase("transition:all .2s ease")]
    [TestCase("transition:color 150ms ease-in-out 50ms")]
    [TestCase("transition:opacity .3s, transform .3s cubic-bezier(.4, 0, .2, 1)")]
    [TestCase("transition:background-color 1s steps(4, jump-end) -0.5s allow-discrete")]
    [TestCase("transition:0.2s")]
    [TestCase("transition:none 1s")]
    [TestCase("TRANSITION:ALL 1S EASE")]
    [TestCase("transition:inherit")]
    [TestCase("transition:all .2s !important")]
    [TestCase("transition-property:none")]
    [TestCase("transition-property:opacity, transform")]
    [TestCase("transition-duration:0s, 250ms")]
    [TestCase("transition-delay:-1s, 0.1s")]
    [TestCase("transition-timing-function:ease, steps(2), cubic-bezier(0, 1.5, 1, -0.5)")]
    [TestCase("transition-behavior:allow-discrete, normal")]
    [TestCase("-webkit-transition:opacity .15s linear")]
    [TestCase("-webkit-transition-duration:.3s")]
    public void AcceptedValuesRenderLikeThePageWithoutThem(string declarations)
    {
        Assert.That(Pdf(Report(declarations)), Is.EqualTo(Pdf(Report(""))));
    }

    [TestCase("transition:1s 2s 3s", "transition supports only none or a comma-separated list of transitions")]
    [TestCase("transition:color opacity 1s", "transition supports only none")]
    [TestCase("transition:none, opacity 1s", "transition supports only none")]
    [TestCase("transition:opacity 1s,", "transition supports only none")]
    [TestCase("transition:-1s", "transition supports only none")]
    [TestCase("transition:opacity 1", "transition supports only none")]
    [TestCase("transition:opacity 1px", "transition supports only none")]
    [TestCase("transition:ease ease-in", "transition supports only none")]
    [TestCase("transition:opacity cubic-bezier(2, 0, 1, 1)", "transition supports only none")]
    [TestCase("transition:opacity steps(0)", "transition supports only none")]
    [TestCase("transition:opacity steps(1, jump-none)", "transition supports only none")]
    [TestCase("transition:opacity linear(0, 1)", "transition supports only none")]
    [TestCase("transition:opacity, inherit", "transition supports only none")]
    [TestCase("transition-property:none, opacity", "transition-property supports only none or a comma-separated list of property names")]
    [TestCase("transition-property:revert, opacity", "transition-property supports only none")]
    [TestCase("transition-property:default", "transition-property supports only none")]
    [TestCase("transition-property:1s", "transition-property supports only none")]
    [TestCase("transition-duration:-1s", "transition-duration supports only a comma-separated list of non-negative times")]
    [TestCase("transition-duration:1s 2s", "transition-duration supports only")]
    [TestCase("transition-delay:auto", "transition-delay supports only a comma-separated list of times")]
    [TestCase("transition-timing-function:opacity", "transition-timing-function supports only a comma-separated list of easing functions")]
    [TestCase("transition-behavior:discrete", "transition-behavior supports only a comma-separated list of normal or allow-discrete")]
    public void OtherValuesFail(string declaration, string message)
    {
        var html = $"<p style=\"{declaration}\">A</p>";
        var error = Assert.Throws<FactsPdfException>(() => Pdf(html));
        Assert.That(error!.Code, Is.EqualTo("FPDF1202"));
        Assert.That(error.Message, Does.Contain(message));
        Assert.That(error.SourceOffset, Is.EqualTo(html.IndexOf(':') + 1));
    }
}
