using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// lab(), lch(), oklab(), oklch() and color() resolve to the sRGB byte triple Chrome writes into its
/// PDF: conversion through XYZ with the Bradford D50/D65 adaptation, the sRGB transfer curve and a
/// channel-by-channel clip to the sRGB gamut (no chroma reduction). The expected bytes below are the
/// colour operands of Chrome's PDF for the same declarations, rounded to a byte.
/// </summary>
[TestFixture]
public sealed class ModernColorTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 400, Margin = 20 };

    private static byte[] Pdf(string html)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, Page);
        return output.ToArray();
    }

    private static string Doc(string background) =>
        $"<style>div{{height:12pt;background:{background}}}</style><div></div>";

    [TestCase("lab(50% 40 59.5)", "#bf5700")]
    [TestCase("lab(100% 0 0)", "#ffffff")]
    [TestCase("lab(0% 0 0)", "#000000")]
    [TestCase("lab(120% 10 10)", "#fff8ec")]
    [TestCase("lab(-10% 10 10)", "#1a0000")]
    [TestCase("lab(60% 50% -50%)", "#cd60ff")]
    [TestCase("lab(50 0 0)", "#777777")]
    [TestCase("lch(50% 100 40)", "#eb0005")]
    [TestCase("lch(50% 20 0.5turn)", "#4d8176")]
    [TestCase("lch(50% 20 180deg)", "#4d8176")]
    [TestCase("oklab(70% 0.1 -0.1)", "#bf81da")]
    [TestCase("oklab(0.6 50% 20%)", "#e42e41")]
    [TestCase("oklch(0.7 0.2 150)", "#00bf54")]
    [TestCase("oklch(0.8 0.1 0.25turn)", "#d7bb70")]
    [TestCase("oklch(40% 100% 20)", "#d20000")]
    [TestCase("color(display-p3 1 0 0)", "#ff0000")]
    [TestCase("color(display-p3 0.2 0.6 0.9)", "#009cec")]
    [TestCase("color(srgb 0.2 0.4 0.6)", "#336699")]
    [TestCase("color(srgb 120% -10% 0.5)", "#ff0080")]
    [TestCase("color(srgb-linear 0.5 0.5 0.5)", "#bcbcbc")]
    [TestCase("color(a98-rgb 0.5 0.2 0.1)", "#933010")]
    [TestCase("color(prophoto-rgb 0.5 0.2 0.1)", "#c20715")]
    [TestCase("color(rec2020 0.5 0.2 0.1)", "#a93121")]
    [TestCase("color(xyz 0.2 0.3 0.4)", "#00a7a4")]
    [TestCase("color(xyz-d65 0.2 0.3 0.4)", "#00a7a4")]
    [TestCase("color(xyz-d50 0.2 0.3 0.4)", "#00a8bd")]
    [TestCase("LAB(50% 40 59.5)", "#bf5700")]
    [TestCase("lab(50%    40    59.5)", "#bf5700")]
    [TestCase("lab(none 0 0)", "#000000")]
    [TestCase("lab(50% none none)", "#777777")]
    public void ResolvesToTheByteTripleChromeWrites(string modern, string hex)
        => Assert.That(Pdf(Doc(modern)), Is.EqualTo(Pdf(Doc(hex))));

    [TestCase("lab(50% 0 0 / 0.5)", "#77777780")]
    [TestCase("lab(50% 0 0 / 50%)", "#77777780")]
    [TestCase("color(srgb 1 0 0 / 0.25)", "#ff000040")]
    [TestCase("oklab(0.5 0.1 0.1 / 2)", "#a14203")]
    [TestCase("lab(50% 0 0 / none)", "#00000000")]
    public void AlphaFollowsTheSameByteRulesAsRgb(string modern, string hex)
        => Assert.That(Pdf(Doc(modern)), Is.EqualTo(Pdf(Doc(hex))));

    [Test]
    public void TheColourReachesTextBordersAndDecorations()
    {
        const string modern = "lab(60% 50 40)";
        const string hex = "#e8674d";
        Assert.That(Pdf($"<style>p{{color:{modern}}}</style><p>A</p>"), Is.EqualTo(Pdf($"<style>p{{color:{hex}}}</style><p>A</p>")));
        Assert.That(Pdf($"<style>div{{border:2pt solid {modern}}}</style><div>A</div>"),
            Is.EqualTo(Pdf($"<style>div{{border:2pt solid {hex}}}</style><div>A</div>")));
        Assert.That(Pdf($"<style>p{{text-decoration:underline {modern}}}</style><p>A</p>"),
            Is.EqualTo(Pdf($"<style>p{{text-decoration:underline {hex}}}</style><p>A</p>")));
        Assert.That(Pdf($"<style>:root{{--c:{modern}}}div{{height:12pt;background:var(--c)}}</style><div></div>"),
            Is.EqualTo(Pdf(Doc(hex))));
    }

    [TestCase("lab(50% 0 0, 1)")]
    [TestCase("lab(50% 0)")]
    [TestCase("lab(50% 0 0 0)")]
    [TestCase("lab(50% 0 0 / 1 / 1)")]
    [TestCase("lab(50% 10deg 0)")]
    [TestCase("lch(50% 10 20px)")]
    [TestCase("lch(50% 10 10%)")]
    [TestCase("color(srgb 1 0)")]
    [TestCase("color(srgb 1 0 0 0.5)")]
    [TestCase("color(1 0 0)")]
    [TestCase("lab(from red l a b)")]
    [TestCase("oklch(from #123 l c h)")]
    [TestCase("color(from red srgb r g b)")]
    [TestCase("color(--brand 1 0 0)")]
    [TestCase("color(display-p4 1 0 0)")]
    [TestCase("lab(calc(1) 0 0)")]
    public void InvalidOrUnsupportedFormsFailExplicitly(string modern)
    {
        var ex = Assert.Throws<FactsPdfException>(() => Pdf(Doc(modern)))!;
        Assert.That(ex.Code, Is.EqualTo("FPDF1202"));
    }
}
