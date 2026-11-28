using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// Animations and their keyframes only run on screen. A written PDF page shows no keyframe, so a
/// well-formed animation (shorthand or longhand) and a well-formed @keyframes rule are validated
/// and dropped and the page renders byte-identical to the page without them.
/// </summary>
[TestFixture]
public sealed class AnimationTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 400, Margin = 20 };

    private static byte[] Pdf(string html)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, Page);
        return output.ToArray();
    }

    private static string Report(string declarations, string rules = "") =>
        $"<style>h1,h2,h3,h4,h5,h6,th{{font-weight:normal}}{rules} body{{{declarations}}} h2{{color:#234;{declarations}}} .box{{width:200pt;background:#eef;border:1pt solid #c00;{declarations}}} .unused{{{declarations}}}</style>" +
        "<h2>Invoice</h2>" +
        $"<div class='box'><p style='{declarations}'>Paid in full</p></div>" +
        $"<table style='width:200pt'><colgroup><col style='width:200pt'></colgroup><tbody><tr><td style='{declarations}'>Total</td></tr></tbody></table>";

    [TestCase("animation:none")]
    [TestCase("animation:spin 1s linear infinite")]
    [TestCase("animation:fade-in .3s ease-out 0.1s both")]
    [TestCase("animation:pulse 2s cubic-bezier(.4, 0, .6, 1) infinite alternate, slide 1s steps(4) 2 reverse forwards paused")]
    [TestCase("animation:1s ease")]
    [TestCase("animation:infinite 1s")]
    [TestCase("animation:spin auto ease")]
    [TestCase("animation:ease ease")]
    [TestCase("animation:\"slide\" 1s")]
    [TestCase("ANIMATION:SPIN 1S LINEAR")]
    [TestCase("animation:inherit")]
    [TestCase("animation:spin 1s !important")]
    [TestCase("animation-name:none")]
    [TestCase("animation-name:spin, fade")]
    [TestCase("animation-duration:auto, 0s, 250ms")]
    [TestCase("animation-delay:-1s")]
    [TestCase("animation-timing-function:ease-in, steps(2, jump-both)")]
    [TestCase("animation-iteration-count:infinite, 2.5, 0")]
    [TestCase("animation-direction:alternate-reverse")]
    [TestCase("animation-fill-mode:none, both")]
    [TestCase("animation-play-state:paused")]
    [TestCase("-webkit-animation:spin 1s linear infinite")]
    [TestCase("-webkit-animation-name:spin")]
    public void AcceptedValuesRenderLikeThePageWithoutThem(string declarations)
    {
        Assert.That(Pdf(Report(declarations)), Is.EqualTo(Pdf(Report(""))));
    }

    [TestCase("@keyframes spin{from{opacity:0}to{opacity:1}}")]
    [TestCase("@keyframes fade{0%,50%{opacity:0}100%{opacity:1;color:red}}")]
    [TestCase("@-webkit-keyframes spin{FROM{transform:rotate(0)}TO{transform:rotate(360deg)}}")]
    [TestCase("@keyframes \"slide\"{}")]
    [TestCase("@keyframes edge{1e2%{opacity:1}-0%{opacity:0}100.0%{opacity:1}}")]
    [TestCase("@media print{p{color:#123}} @keyframes x{to{top:0}}")]
    [TestCase("@media print{p{color:#123} @keyframes x{to{top:0}}}")]
    public void KeyframesRulesRenderLikeThePageWithoutThem(string rules)
    {
        var expected = rules.StartsWith("@media", StringComparison.Ordinal) ? Report("", "@media print{p{color:#123}}") : Report("");
        Assert.That(Pdf(Report("animation:spin 1s", rules)), Is.EqualTo(Pdf(expected)));
    }

    [TestCase("animation:spin fade 1s", "animation supports only none or a comma-separated list of animations")]
    [TestCase("animation:spin 1s 2s 3s", "animation supports only none")]
    [TestCase("animation:spin -1s", "animation supports only none")]
    [TestCase("animation:spin 1s -2", "animation supports only none")]
    [TestCase("animation:spin 1s,", "animation supports only none")]
    [TestCase("animation:spin 1px", "animation supports only none")]
    [TestCase("animation:spin, inherit", "animation supports only none")]
    [TestCase("animation:spin 1s steps(0)", "animation supports only none")]
    [TestCase("animation:spin 1s auto", "animation supports only none")]
    [TestCase("animation:none none none", "animation supports only none")]
    [TestCase("animation-name:spin fade", "animation-name supports only a comma-separated list of none or keyframes names")]
    [TestCase("animation-name:default", "animation-name supports only")]
    [TestCase("animation-duration:-1s", "animation-duration supports only a comma-separated list of auto or non-negative times")]
    [TestCase("animation-delay:auto", "animation-delay supports only a comma-separated list of times")]
    [TestCase("animation-timing-function:spin", "animation-timing-function supports only a comma-separated list of easing functions")]
    [TestCase("animation-iteration-count:-1", "animation-iteration-count supports only a comma-separated list of infinite or non-negative numbers")]
    [TestCase("animation-direction:forwards", "animation-direction supports only a comma-separated list of normal, reverse, alternate or alternate-reverse")]
    [TestCase("animation-fill-mode:reverse", "animation-fill-mode supports only a comma-separated list of none, forwards, backwards or both")]
    [TestCase("animation-play-state:stopped", "animation-play-state supports only a comma-separated list of running or paused")]
    public void OtherValuesFail(string declaration, string message)
    {
        var html = $"<p style=\"{declaration}\">A</p>";
        var error = Assert.Throws<FactsPdfException>(() => Pdf(html));
        Assert.That(error!.Code, Is.EqualTo("FPDF1202"));
        Assert.That(error.Message, Does.Contain(message));
        Assert.That(error.SourceOffset, Is.EqualTo(html.IndexOf(':') + 1));
    }

    [TestCase("@keyframes{to{opacity:1}}", "@keyframes needs one keyframes name")]
    [TestCase("@keyframes none{to{opacity:1}}", "@keyframes needs one keyframes name")]
    [TestCase("@keyframes a b{to{opacity:1}}", "@keyframes needs one keyframes name")]
    [TestCase("@keyframes spin;", "@keyframes needs one keyframes name")]
    [TestCase("@keyframes spin{half{opacity:1}}", "only from, to or percentage keyframes")]
    [TestCase("@keyframes spin{120%{opacity:1}}", "only from, to or percentage keyframes")]
    [TestCase("@keyframes spin{100.0001%{opacity:1}}", "only from, to or percentage keyframes")]
    [TestCase("@keyframes spin{from to{opacity:1}}", "only from, to or percentage keyframes")]
    [TestCase("@keyframes spin{to{@media print{p{color:red}}}}", "A keyframe contains only declarations")]
    public void MalformedKeyframesFail(string rules, string message)
    {
        var error = Assert.Throws<FactsPdfException>(() => Pdf($"<style>{rules}</style><p>A</p>"));
        Assert.That(error!.Code, Is.EqualTo("FPDF1204"));
        Assert.That(error.Message, Does.Contain(message));
    }
}
