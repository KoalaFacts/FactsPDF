using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// opacity, transform, transform-origin and box-shadow on report panels, paragraphs and headings, as
/// Chrome prints them. A box with opacity below 1 or a transform creates a stacking context, which
/// paints after the in-flow content around it in tree order (CSS 2 Appendix E, step 8): opacity as a
/// transparency group form XObject painted with the opacity as its constant alpha (quantised to a byte,
/// as Chrome's PDF backend stores it), a transform as a cm about the transform-origin, 3D transforms
/// flattened orthographically. box-shadow without blur paints the offset, spread rectangle outside the
/// border box (under the background) or inside the padding box (over it), the first layer on top.
/// Everything else fails explicitly.
/// </summary>
[TestFixture]
public sealed class PaintEffectTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 400, Margin = 20 };

    private static byte[] Pdf(string html, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, options ?? Page);
        return output.ToArray();
    }

    private static string Text(string html, PdfOptions? options = null) => Encoding.Latin1.GetString(Pdf(html, options));

    private static FactsPdfException Fails(string html, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, options ?? Page))!;
        Assert.That(output.Length, Is.Zero, "nothing is written");
        return error;
    }

    private const string Box = "width:40pt;height:40pt;background:#c00";

    // ---- opacity -----------------------------------------------------------------------------------

    [Test]
    public void OpacityPaintsTheBoxAndItsTextAsOneTransparencyGroup()
    {
        var pdf = Text($"<div style='opacity:0.5;{Box}'>A</div>");
        Assert.That(pdf, Does.Contain("q /A80 gs /Fx6 Do Q\n"));
        Assert.That(pdf, Does.Contain("/XObject << /Fx6 6 0 R >> /ExtGState << /A80 << /CA 0.502 /ca 0.502 >> >>"));
        Assert.That(pdf, Does.Contain("6 0 obj\n<< /Type /XObject /Subtype /Form /BBox [-300 -400 600 800] /Group << /S /Transparency >>"));
        // The background and the text are inside the group, painted opaque there.
        var form = pdf[pdf.IndexOf("6 0 obj", StringComparison.Ordinal)..];
        Assert.That(form, Does.Contain("0.8 0 0 rg"));
        Assert.That(form, Does.Contain("BT /F1"));
        var page = pdf[..pdf.IndexOf("6 0 obj", StringComparison.Ordinal)];
        Assert.That(page, Does.Not.Contain("BT /F1"));
    }

    [TestCase("opacity:1")]
    [TestCase("opacity:100%")]
    [TestCase("opacity:2")]
    [TestCase("opacity:initial")]
    public void OpaqueValuesPrintLikeNone(string declaration)
    {
        Assert.That(Pdf($"<div style='{declaration};{Box}'>A</div>"), Is.EqualTo(Pdf($"<div style='{Box}'>A</div>")));
    }

    [Test]
    public void PercentagesAndNumbersAgreeAndNegativeClampsToZero()
    {
        Assert.That(Pdf($"<div style='opacity:50%;{Box}'>A</div>"), Is.EqualTo(Pdf($"<div style='opacity:.5;{Box}'>A</div>")));
        Assert.That(Pdf($"<div style='opacity:-3;{Box}'>A</div>"), Is.EqualTo(Pdf($"<div style='opacity:0;{Box}'>A</div>")));
        Assert.That(Text($"<div style='opacity:0;{Box}'>A</div>"), Does.Contain("q /A00 gs /Fx6 Do Q"));
    }

    [Test]
    public void AlmostOpaqueStillPaintsAfterItsLaterSiblingsButNeedsNoGroup()
    {
        // 0.999 quantises to an opaque byte: no transparency group, but still its own stacking context.
        var pdf = Text("<div style='opacity:0.999;height:20pt;background:#f00'></div><div style='height:20pt;background:#00f'></div>");
        Assert.That(pdf, Does.Not.Contain("/Group"));
        Assert.That(pdf.IndexOf("0 0 1 rg", StringComparison.Ordinal), Is.LessThan(pdf.IndexOf("1 0 0 rg", StringComparison.Ordinal)));
    }

    [Test]
    public void ATranslucentBoxPaintsAboveTheBackgroundOfALaterSibling()
    {
        var pdf = Text("<div style='opacity:.5;height:20pt;background:#f00'></div><div style='height:20pt;background:#00f'></div>");
        Assert.That(pdf.IndexOf("0 0 1 rg", StringComparison.Ordinal), Is.LessThan(pdf.IndexOf("/Fx6 Do", StringComparison.Ordinal)));
        // Without opacity the boxes paint in tree order.
        var plain = Text("<div style='height:20pt;background:#f00'></div><div style='height:20pt;background:#00f'></div>");
        Assert.That(plain.IndexOf("1 0 0 rg", StringComparison.Ordinal), Is.LessThan(plain.IndexOf("0 0 1 rg", StringComparison.Ordinal)));
    }

    [Test]
    public void NestedOpacityNestsTheGroups()
    {
        var pdf = Text("<div style='opacity:.5;background:#eee'><p style='opacity:.25;background:#00f'>A</p></div>");
        Assert.That(pdf, Does.Contain("q /A80 gs /Fx6 Do Q"));
        var outer = pdf[pdf.IndexOf("6 0 obj", StringComparison.Ordinal)..pdf.IndexOf("7 0 obj", StringComparison.Ordinal)];
        Assert.That(outer, Does.Contain("q /A40 gs /Fx7 Do Q"));
        Assert.That(outer, Does.Contain("/Fx7 7 0 R"));
    }

    [Test]
    public void ATranslucentBoxThatSpansPagesIsAGroupOnEachPage()
    {
        var tall = string.Concat(Enumerable.Repeat("<p>Line</p>", 40));
        var pdf = Text($"<div style='opacity:.5'>{tall}</div>");
        Assert.That(pdf.Split("/Subtype /Form").Length - 1, Is.GreaterThanOrEqualTo(2));
    }

    [TestCase("<p>A <span style='opacity:.5'>b</span></p>")]
    [TestCase("<ul><li style='opacity:.5'>A</li></ul>")]
    [TestCase("<table><tr><td style='opacity:.5'>A</td></tr></table>")]
    [TestCase("<p style='opacity:.5'>A <span style='opacity:inherit'>b</span></p>")]
    public void OpacityOutsideBlockBoxesFails(string html)
    {
        Assert.That(Fails(html).Code, Is.EqualTo("FPDF1201"));
    }

    [TestCase("<p>A <span style='opacity:1'>b</span></p>")]
    [TestCase("<ul><li style='opacity:100%'>A</li></ul>")]
    [TestCase("<p>A <span style='opacity:inherit'>b</span></p>")]
    public void OpaqueValuesOutsideBlockBoxesAreDropped(string html)
    {
        Assert.That(Pdf(html), Is.EqualTo(Pdf(html.Replace("opacity:100%", "").Replace("opacity:1", "").Replace("opacity:inherit", ""))));
    }

    [Test]
    public void OpacityInsideAFlexItemFails()
    {
        Assert.That(Fails("<div style='display:flex'><div style='opacity:.5'>A</div></div>").Code, Is.EqualTo("FPDF1201"));
    }

    // ---- transform ---------------------------------------------------------------------------------

    [Test]
    public void TranslateMovesTheBoxByLengthsInPagePoints()
    {
        var pdf = Text("<div style='width:100pt;height:20pt;background:#c00;transform:translate(10px, 20px)'></div>");
        Assert.That(pdf, Does.Contain("q 1 0 0 1 7.5 -15 cm \n"));
    }

    [Test]
    public void RotateTurnsClockwiseAboutTheCentre()
    {
        // The 40pt square at (20, 20) turns about (40, 40): PDF y up, so the cm is [0 -1 1 0 -320 400].
        Assert.That(Text($"<div style='{Box};transform:rotate(90deg)'></div>"), Does.Contain("q 0 -1 1 0 -320 400 cm \n"));
        Assert.That(Pdf($"<div style='{Box};transform:rotate(.25turn)'></div>"), Is.EqualTo(Pdf($"<div style='{Box};transform:rotate(90deg)'></div>")));
    }

    [Test]
    public void TransformOriginMovesThePivot()
    {
        Assert.That(Text($"<div style='{Box};transform:scale(.5)'></div>"), Does.Contain("q 0.5 0 0 0.5 20 180 cm \n"));
        Assert.That(Text($"<div style='{Box};transform:scale(.5);transform-origin:left top'></div>"), Does.Contain("q 0.5 0 0 0.5 10 190 cm \n"));
        Assert.That(Pdf($"<div style='{Box};transform:scale(.5);transform-origin:0 0'></div>"),
            Is.EqualTo(Pdf($"<div style='{Box};transform:matrix(.5, 0, 0, .5, 0, 0);transform-origin:top left'></div>")));
    }

    [Test]
    public void ThreeDimensionalTransformsFlattenOrthographically()
    {
        Assert.That(Pdf($"<div style='{Box};transform:rotateX(60deg)'></div>"), Is.EqualTo(Pdf($"<div style='{Box};transform:scaleY(.5)'></div>")));
        Assert.That(Pdf($"<div style='{Box};transform:translateZ(30px) scale3d(.5, .5, 4)'></div>"), Is.EqualTo(Pdf($"<div style='{Box};transform:scale(.5)'></div>")));
        // Perspective acting only on a 2D transform leaves the page plane affine.
        Assert.That(Pdf($"<div style='{Box};transform:perspective(100px) scale(.5)'></div>"), Is.EqualTo(Pdf($"<div style='{Box};transform:scale(.5)'></div>")));
    }

    [Test]
    public void AnIdentityTransformStillPaintsAfterLaterSiblings()
    {
        var pdf = Text("<div style='height:20pt;background:#f00;transform:translateZ(0)'></div><div style='height:20pt;background:#00f'></div>");
        Assert.That(pdf.IndexOf("0 0 1 rg", StringComparison.Ordinal), Is.LessThan(pdf.IndexOf("1 0 0 rg", StringComparison.Ordinal)));
    }

    [Test]
    public void ACollapsingTransformPaintsNothing()
    {
        Assert.That(Text($"<div style='{Box};transform:scale(0)'></div>"), Does.Not.Contain("0.8 0 0 rg"));
        Assert.That(Text($"<div style='{Box};transform:rotateY(90deg)'></div>"), Does.Not.Contain("0.8 0 0 rg"));
    }

    [TestCase("transform:perspective(100px) rotateY(30deg)", "FPDF1202")]
    [TestCase("transform:rotateY(180deg)", "FPDF1201")]
    [TestCase("transform:translateX(2ex)", "FPDF1202")]
    [TestCase("transform:rotate(45deg, 1deg)", "FPDF1202")]
    [TestCase("transform:translate(1px 2px)", "FPDF1202")]
    [TestCase("transform:scale(.5);transform-origin:1ex 0", "FPDF1202")]
    [TestCase("transform:translateX(400pt)", "FPDF1302")]
    [TestCase("transform:translateY(-30pt)", "FPDF1302")]
    public void UnsupportedTransformsFail(string declarations, string code)
    {
        Assert.That(Fails($"<div style='{Box};{declarations}'></div>").Code, Is.EqualTo(code));
    }

    [Test]
    public void AnOriginInExUnitsIsAcceptedWithoutATransform()
    {
        Assert.That(Pdf($"<div style='{Box};transform-origin:1ex 0'></div>"), Is.EqualTo(Pdf($"<div style='{Box}'></div>")));
    }

    [Test]
    public void ATransformedBoxThatSpansPagesFails()
    {
        var tall = string.Concat(Enumerable.Repeat("<p>Line</p>", 40));
        Assert.That(Fails($"<div style='transform:scale(.9)'>{tall}</div>").Code, Is.EqualTo("FPDF1302"));
    }

    [Test]
    public void LinksInsideATransformedBoxFail()
    {
        Assert.That(Fails("<div style='width:100pt;transform:translateX(1pt)'><a href='https://example.com'>A</a></div>").Code, Is.EqualTo("FPDF1201"));
    }

    [TestCase("<p>A <span style='transform:rotate(1deg)'>b</span></p>", "FPDF1201")]
    [TestCase("<p>A <span style='transform:none'>b</span></p>", null)]
    [TestCase("<p>A <span style='transform-origin:left'>b</span></p>", null)]
    public void TransformsOutsideBlockBoxes(string html, string? code)
    {
        if (code is null) Assert.That(Pdf(html), Is.EqualTo(Pdf("<p>A <span>b</span></p>")));
        else Assert.That(Fails(html).Code, Is.EqualTo(code));
    }

    [Test]
    public void OpacityAndTransformCombineInOneGroup()
    {
        Assert.That(Text("<div style='width:100pt;height:20pt;background:#c00;opacity:.5;transform:translate(10px, 20px)'></div>"),
            Does.Contain("q 1 0 0 1 7.5 -15 cm /A80 gs /Fx6 Do Q\n"));
    }

    // ---- box-shadow --------------------------------------------------------------------------------

    private const string Panel = "width:100pt;height:20pt";

    [Test]
    public void AnOuterShadowPaintsOutsideTheBorderBoxUnderTheBackground()
    {
        var pdf = Text($"<div style='{Panel};background:#eee;box-shadow:3pt 4pt #000'></div>");
        const string shadow = "q -1000000 -1000000 2000000 2000000 re 20 360 100 20 re W* n 0 0 0 rg 23 356 100 20 re f Q\n";
        Assert.That(pdf, Does.Contain(shadow));
        Assert.That(pdf.IndexOf(shadow, StringComparison.Ordinal), Is.LessThan(pdf.IndexOf("0.933 0.933 0.933 rg", StringComparison.Ordinal)));
    }

    [Test]
    public void SpreadGrowsAndShrinksTheShadow()
    {
        Assert.That(Text($"<div style='{Panel};box-shadow:0 0 0 2pt red'></div>"), Does.Contain("1 0 0 rg 18 358 104 24 re f Q"));
        Assert.That(Text($"<div style='{Panel};box-shadow:0 5pt 0 -2pt red'></div>"), Does.Contain("1 0 0 rg 22 357 96 16 re f Q"));
    }

    [Test]
    public void AnInsetShadowFillsThePaddingBoxOutsideTheMovedHole()
    {
        var pdf = Text($"<div style='{Panel};background:#eee;box-shadow:inset 2pt 2pt #00f'></div>");
        const string shadow = "q 20 360 100 20 re W n 0 0 1 rg -1000000 -1000000 2000000 2000000 re 22 358 100 20 re f* Q\n";
        Assert.That(pdf, Does.Contain(shadow));
        // Over the background.
        Assert.That(pdf.IndexOf("0.933 0.933 0.933 rg", StringComparison.Ordinal), Is.LessThan(pdf.IndexOf(shadow, StringComparison.Ordinal)));
    }

    [Test]
    public void TheFirstLayerIsOnTop()
    {
        var pdf = Text($"<div style='{Panel};box-shadow:1pt 1pt red, 2pt 2pt blue'></div>");
        Assert.That(pdf.IndexOf("0 0 1 rg", StringComparison.Ordinal), Is.LessThan(pdf.IndexOf("1 0 0 rg", StringComparison.Ordinal)));
    }

    [Test]
    public void ShadowColoursKeepTheirAlphaAndDefaultToCurrentColor()
    {
        Assert.That(Text($"<div style='{Panel};box-shadow:1pt 1pt rgba(0,0,0,.5)'></div>"), Does.Contain("q /A80 gs -1000000"));
        Assert.That(Pdf($"<div style='{Panel};color:#00f;box-shadow:1pt 1pt'></div>"), Is.EqualTo(Pdf($"<div style='{Panel};color:#00f;box-shadow:1pt 1pt #00f'></div>")));
        Assert.That(Pdf($"<div style='{Panel};box-shadow:1pt 1pt transparent'></div>"), Is.EqualTo(Pdf($"<div style='{Panel}'></div>")));
        Assert.That(Pdf($"<div style='{Panel};box-shadow:1pt 1pt 0 red'></div>"), Is.EqualTo(Pdf($"<div style='{Panel};box-shadow:1pt 1pt red'></div>")));
    }

    [TestCase("box-shadow:0 0 2pt #000", "FPDF1202")]
    [TestCase("box-shadow:1pt 1pt -1pt #000", "FPDF1202")]
    [TestCase("box-shadow:1pt #000", "FPDF1202")]
    [TestCase("box-shadow:1pt 1pt 0 0 0 #000", "FPDF1202")]
    [TestCase("box-shadow:1pt 1pt 10% #000", "FPDF1202")]
    [TestCase("box-shadow:1pt 1pt #000;border-radius:4pt", "FPDF1201")]
    public void UnsupportedShadowsFail(string declarations, string code)
    {
        Assert.That(Fails($"<div style='{Panel};{declarations}'></div>").Code, Is.EqualTo(code));
    }

    [Test]
    public void AShadowedBoxThatSpansPagesFails()
    {
        var tall = string.Concat(Enumerable.Repeat("<p>Line</p>", 40));
        Assert.That(Fails($"<div style='box-shadow:1pt 1pt #000'>{tall}</div>").Code, Is.EqualTo("FPDF1302"));
    }

    [TestCase("<p>A <span style='box-shadow:1pt 1pt red'>b</span></p>", "FPDF1201")]
    [TestCase("<p>A <span style='box-shadow:none'>b</span></p>", null)]
    public void ShadowsOutsideBlockBoxes(string html, string? code)
    {
        if (code is null) Assert.That(Pdf(html), Is.EqualTo(Pdf("<p>A <span>b</span></p>")));
        else Assert.That(Fails(html).Code, Is.EqualTo(code));
    }
}
