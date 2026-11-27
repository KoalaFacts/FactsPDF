using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// position, float, clear, overflow(-x/-y), visibility, z-index, opacity, box-shadow,
/// text-shadow, transform, filter, background-image, clip-path, backdrop-filter, mix-blend-mode,
/// isolation and the font tuning properties (font-feature-settings, font-variation-settings,
/// font-variant-ligatures/-numeric, font-stretch, font-optical-sizing, font-size-adjust), zoom,
/// contain, content-visibility, line-break, text-justify and image-rendering are accepted at
/// the one value the report renderer already lays out (their initial value, or a CSS-wide
/// keyword that computes to it): the page renders byte-identical to the page without them.
/// Every other value still fails with FPDF1202, and nothing is written.
/// </summary>
[TestFixture]
public sealed class InitialValueNoOpTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 400, Margin = 20 };

    private static byte[] Pdf(string html)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, Page);
        return output.ToArray();
    }

    private static string Report(string declarations) =>
        $"<style>h2{{{declarations}}} .box{{width:200pt;border:1pt solid #333;{declarations}}}</style>" +
        "<h2>Invoice</h2>" +
        $"<div class='box'><p style='{declarations}'>Paid in full</p></div>" +
        $"<table style='width:200pt'><colgroup><col style='width:200pt'></colgroup><tbody><tr><td style='{declarations}'>Total</td></tr></tbody></table>" +
        $"<ul><li style='{declarations}'>Line <span style='{declarations}'>item</span></li></ul>";

    [TestCase("position:static")]
    [TestCase("float:none")]
    [TestCase("clear:none")]
    [TestCase("overflow:visible")]
    [TestCase("overflow-x:visible;overflow-y:visible")]
    [TestCase("visibility:visible")]
    [TestCase("z-index:auto")]
    [TestCase("opacity:1")]
    [TestCase("opacity:1.0")]
    [TestCase("opacity:100%")]
    [TestCase("box-shadow:none;text-shadow:none")]
    [TestCase("transform:none;filter:none;background-image:none")]
    [TestCase("box-shadow:inherit;transform:initial;background-image:unset")]
    [TestCase("POSITION:STATIC;Float:None")]
    [TestCase("position:inherit;float:initial;clear:unset;visibility:inherit;opacity:initial")]
    [TestCase("position:static!important;z-index:auto !important")]
    [TestCase("mix-blend-mode:normal;isolation:auto")]
    [TestCase("clip-path:none;backdrop-filter:none")]
    [TestCase("MIX-BLEND-MODE:NORMAL;clip-path:inherit;isolation:unset")]
    [TestCase("font-feature-settings:normal;font-variation-settings:normal")]
    [TestCase("-webkit-font-feature-settings:normal;-moz-font-feature-settings:normal")]
    [TestCase("font-variant-ligatures:normal;font-variant-numeric:normal;font-stretch:normal")]
    [TestCase("font-optical-sizing:auto;font-size-adjust:none")]
    [TestCase("FONT-FEATURE-SETTINGS:NORMAL;font-stretch:inherit;font-size-adjust:unset !important")]
    [TestCase("font-variant:normal")]
    [TestCase("font-variant-caps:normal;font-variant-east-asian:normal")]
    [TestCase("font-variant-alternates:normal;font-variant-position:normal")]
    [TestCase("FONT-VARIANT:NORMAL;font-variant-caps:inherit;font-variant-position:initial !important")]
    [TestCase("font-language-override:normal;font-variant-emoji:normal;font-palette:normal")]
    [TestCase("text-emphasis:none;text-emphasis-style:none")]
    [TestCase("FONT-PALETTE:NORMAL;font-language-override:inherit;text-emphasis:initial;text-emphasis-style:unset !important")]
    [TestCase("direction:ltr;unicode-bidi:normal;writing-mode:horizontal-tb")]
    [TestCase("text-orientation:mixed;text-combine-upright:none")]
    [TestCase("DIRECTION:LTR;unicode-bidi:inherit;writing-mode:initial;text-orientation:unset !important")]
    [TestCase("column-count:auto;column-width:auto;columns:auto;column-gap:normal")]
    [TestCase("column-span:none;column-fill:balance;column-rule:none;column-rule-style:none")]
    [TestCase("COLUMNS:AUTO;column-count:inherit;column-gap:initial;column-rule:unset !important")]
    [TestCase("gap:normal;row-gap:normal")]
    [TestCase("GAP:NORMAL;row-gap:inherit;gap:initial;row-gap:unset !important")]
    [TestCase("text-overflow:clip;line-clamp:none;-webkit-line-clamp:none")]
    [TestCase("box-decoration-break:slice;-webkit-box-decoration-break:slice")]
    [TestCase("TEXT-OVERFLOW:CLIP;line-clamp:inherit;box-decoration-break:initial !important")]
    [TestCase("rotate:none;scale:none;translate:none;perspective:none;transform-style:flat")]
    [TestCase("mask:none;mask-image:none;-webkit-mask:none;-webkit-mask-image:none")]
    [TestCase("shape-outside:none")]
    [TestCase("hanging-punctuation:none;hyphenate-character:auto;text-spacing-trim:normal;text-underline-position:auto")]
    [TestCase("HANGING-PUNCTUATION:NONE;hyphenate-character:inherit;text-underline-position:unset !important")]
    [TestCase("animation-composition:replace;animation-timeline:auto;animation-range:normal;animation-range-start:normal;animation-range-end:normal")]
    [TestCase("scroll-timeline:none;scroll-timeline-name:none;scroll-timeline-axis:block;timeline-scope:none")]
    [TestCase("view-timeline:none;view-timeline-name:none;view-timeline-axis:block;view-timeline-inset:auto")]
    [TestCase("view-transition-name:none;view-transition-class:none")]
    [TestCase("anchor-name:none;anchor-scope:none;position-area:none;position-try:none;position-try-fallbacks:none;position-try-order:normal;overlay:none")]
    [TestCase("ANCHOR-NAME:NONE;position-area:inherit;position-try-order:unset;overlay:none !important")]
    [TestCase("interpolate-size:numeric-only;reading-flow:normal;field-sizing:fixed;dynamic-range-limit:no-limit;caret:auto;caret-animation:auto;caret-shape:auto")]
    [TestCase("INTERPOLATE-SIZE:NUMERIC-ONLY;field-sizing:inherit;caret-shape:unset;dynamic-range-limit:no-limit !important")]
    [TestCase("text-wrap-style:auto;text-box:normal;text-box-trim:none;text-box-edge:auto;paint-order:normal;math-style:normal;math-shift:normal")]
    [TestCase("TEXT-WRAP-STYLE:AUTO;text-box-trim:inherit;paint-order:unset;math-shift:normal !important")]
    [TestCase("contain-intrinsic-size:none;contain-intrinsic-width:none;contain-intrinsic-height:none;contain-intrinsic-block-size:none;contain-intrinsic-inline-size:none;transform-box:view-box")]
    [TestCase("CONTAIN-INTRINSIC-SIZE:NONE;contain-intrinsic-width:inherit;transform-box:unset !important")]
    [TestCase("top:auto;right:auto;bottom:auto;left:auto;inset:auto;inset-block:auto;inset-inline:auto;inset-block-start:auto;inset-block-end:auto;inset-inline-start:auto;inset-inline-end:auto")]
    [TestCase("TOP:AUTO;left:inherit !important;border-image:none;border-image-source:NONE")]
    [TestCase("ANIMATION-TIMELINE:AUTO;view-transition-name:inherit;scroll-timeline-axis:unset;animation-composition:replace !important")]
    [TestCase("background-attachment:scroll;background-blend-mode:normal;background-origin:padding-box;container-type:normal;container:none")]
    [TestCase("aspect-ratio:auto;mask-repeat:repeat;mask-size:auto;mask-origin:border-box;mask-clip:border-box;mask-mode:match-source;mask-composite:add;mask-type:luminance")]
    [TestCase("ASPECT-RATIO:AUTO;mask-repeat:inherit;mask-size:initial;mask-mode:unset;mask-type:luminance !important")]
    [TestCase("BACKGROUND-ATTACHMENT:SCROLL;background-blend-mode:inherit;background-origin:initial;container-type:unset;container:none !important")]
    [TestCase("ROTATE:NONE;scale:inherit;transform-style:initial;-webkit-mask-image:unset;shape-outside:none !important")]
    [TestCase("zoom:normal")]
    [TestCase("zoom:1")]
    [TestCase("zoom:100%")]
    [TestCase("contain:none;content-visibility:visible")]
    [TestCase("line-break:auto;text-justify:auto;image-rendering:auto")]
    [TestCase("ZOOM:1;Contain:inherit;line-break:unset !important")]
    [TestCase("border-collapse:separate;border-spacing:0")]
    [TestCase("border-spacing:0pt 0;Border-Collapse:SEPARATE !important;border-spacing:-0.0px")]
    [TestCase("border-collapse:inherit;border-spacing:initial;border-spacing:0 0mm")]
    [TestCase("empty-cells:show;caption-side:top")]
    [TestCase("Empty-Cells:SHOW !important;caption-side:inherit;caption-side:TOP")]
    [TestCase("offset-path:none;offset-rotate:auto;offset-anchor:auto;initial-letter:normal;text-emphasis-color:currentcolor")]
    [TestCase("Offset-Path:NONE !important;offset-rotate:inherit;offset-anchor:AUTO;initial-letter:unset;text-emphasis-color:CurrentColor")]
    public void InitialValueRendersLikeTheUndeclaredPage(string declarations)
    {
        Assert.That(Pdf(Report(declarations)), Is.EqualTo(Pdf(Report(""))));
    }

    [Test]
    public void InitialValueDoesNotDisturbTheCascade()
    {
        // Interleaving no-op declarations leaves the later colour winning, exactly as without them.
        Assert.That(Pdf("<p style='color:#c00;position:static;color:#00c;opacity:1'>Due</p>"),
            Is.EqualTo(Pdf("<p style='color:#c00;color:#00c'>Due</p>")));
        Assert.That(Pdf("<style>p{float:none;color:#00c}</style><p>Due</p>"),
            Is.EqualTo(Pdf("<style>p{color:#00c}</style><p>Due</p>")));
    }

    [TestCase("position:absolute")]
    [TestCase("position:relative")]
    [TestCase("float:left")]
    [TestCase("clear:both")]
    [TestCase("overflow:hidden")]
    [TestCase("overflow-x:auto")]
    [TestCase("visibility:hidden")]
    [TestCase("z-index:1")]
    [TestCase("opacity:0.5")]
    [TestCase("opacity:50%")]
    [TestCase("opacity:visible")]
    [TestCase("box-shadow:0 0 2pt #000")]
    [TestCase("text-shadow:1px 1px #ccc")]
    [TestCase("transform:rotate(90deg)")]
    [TestCase("filter:grayscale(1)")]
    [TestCase("background-image:url(a.png)")]
    [TestCase("background-image:none none")]
    [TestCase("position:'static'")]
    [TestCase("float:none none")]
    [TestCase("overflow:visible visible")]
    [TestCase("position:")]
    [TestCase("mix-blend-mode:multiply")]
    [TestCase("isolation:isolate")]
    [TestCase("clip-path:circle(50%)")]
    [TestCase("backdrop-filter:blur(4px)")]
    [TestCase("font-feature-settings:'tnum'")]
    [TestCase("-webkit-font-feature-settings:'liga' 0")]
    [TestCase("font-variation-settings:'wght' 700")]
    [TestCase("font-variant-numeric:tabular-nums")]
    [TestCase("font-variant-ligatures:none")]
    [TestCase("font-stretch:condensed")]
    [TestCase("font-stretch:100%")]
    [TestCase("font-optical-sizing:none")]
    [TestCase("font-size-adjust:0.5")]
    [TestCase("font-variant:small-caps")]
    [TestCase("font-variant:none")]
    [TestCase("font-variant:normal small-caps")]
    [TestCase("font-variant-caps:small-caps")]
    [TestCase("font-variant-east-asian:jis78")]
    [TestCase("font-variant-alternates:historical-forms")]
    [TestCase("font-variant-position:super")]
    [TestCase("font-language-override:'TRK'")]
    [TestCase("font-variant-emoji:emoji")]
    [TestCase("font-variant-emoji:text")]
    [TestCase("font-palette:dark")]
    [TestCase("font-palette:--brand")]
    [TestCase("text-emphasis:filled")]
    [TestCase("text-emphasis:none red")]
    [TestCase("text-emphasis-style:'x'")]
    [TestCase("text-emphasis-style:open dot")]
    [TestCase("direction:rtl")]
    [TestCase("unicode-bidi:isolate")]
    [TestCase("unicode-bidi:bidi-override")]
    [TestCase("unicode-bidi:plaintext")]
    [TestCase("writing-mode:vertical-rl")]
    [TestCase("writing-mode:lr-tb")]
    [TestCase("text-orientation:upright")]
    [TestCase("text-combine-upright:all")]
    [TestCase("direction:ltr rtl")]
    [TestCase("column-count:2")]
    [TestCase("column-width:12em")]
    [TestCase("columns:2 auto")]
    [TestCase("columns:auto auto")]
    [TestCase("gap:1rem 2rem 3rem")]
    [TestCase("row-gap:auto")]
    [TestCase("column-gap:-1pt")]
    [TestCase("column-span:all")]
    [TestCase("column-fill:auto")]
    [TestCase("column-rule:1px solid #ccc")]
    [TestCase("column-rule-style:solid")]
    [TestCase("text-overflow:ellipsis")]
    [TestCase("text-overflow:clip clip")]
    [TestCase("line-clamp:3")]
    [TestCase("-webkit-line-clamp:2")]
    [TestCase("box-decoration-break:clone")]
    [TestCase("-webkit-box-decoration-break:clone")]
    [TestCase("rotate:45deg")]
    [TestCase("scale:1.5")]
    [TestCase("translate:10px 20px")]
    [TestCase("perspective:500px")]
    [TestCase("transform-style:preserve-3d")]
    [TestCase("mask:url(#m)")]
    [TestCase("mask-image:linear-gradient(black, transparent)")]
    [TestCase("-webkit-mask-image:url(m.png)")]
    [TestCase("shape-outside:circle(50%)")]
    [TestCase("shape-outside:none none")]
    [TestCase("hanging-punctuation:first")]
    [TestCase("hyphenate-character:'-'")]
    [TestCase("text-spacing-trim:trim-start")]
    [TestCase("text-underline-position:under")]
    [TestCase("animation-composition:add")]
    [TestCase("animation-timeline:scroll()")]
    [TestCase("animation-timeline:--reveal")]
    [TestCase("animation-range:entry 10% exit 90%")]
    [TestCase("animation-range-start:20%")]
    [TestCase("scroll-timeline:--page block")]
    [TestCase("scroll-timeline-axis:inline")]
    [TestCase("view-timeline-name:--card")]
    [TestCase("view-timeline-inset:10px")]
    [TestCase("timeline-scope:all")]
    [TestCase("view-transition-name:header")]
    [TestCase("view-transition-class:card")]
    [TestCase("anchor-name:--tooltip")]
    [TestCase("anchor-scope:all")]
    [TestCase("position-area:top center")]
    [TestCase("position-area:bottom")]
    [TestCase("position-try:flip-block")]
    [TestCase("position-try-fallbacks:flip-inline")]
    [TestCase("position-try-order:most-height")]
    [TestCase("overlay:auto")]
    [TestCase("interpolate-size:allow-keywords")]
    [TestCase("reading-flow:grid-rows")]
    [TestCase("field-sizing:content")]
    [TestCase("dynamic-range-limit:standard")]
    [TestCase("caret:red bar")]
    [TestCase("caret-animation:manual")]
    [TestCase("caret-shape:block")]
    [TestCase("text-wrap-style:balance")]
    [TestCase("text-wrap-style:pretty")]
    [TestCase("text-box:trim-both cap alphabetic")]
    [TestCase("text-box-trim:trim-both")]
    [TestCase("text-box-edge:cap alphabetic")]
    [TestCase("paint-order:stroke")]
    [TestCase("math-style:compact")]
    [TestCase("math-shift:compact")]
    [TestCase("contain-intrinsic-size:auto 300px")]
    [TestCase("contain-intrinsic-size:500px")]
    [TestCase("contain-intrinsic-width:100px")]
    [TestCase("contain-intrinsic-block-size:auto none")]
    [TestCase("transform-box:fill-box")]
    [TestCase("transform-box:border-box")]
    [TestCase("top:0")]
    [TestCase("left:10px")]
    [TestCase("inset:auto auto")]
    [TestCase("inset-inline-start:5%")]
    [TestCase("border-image:url(a.png) 30 round")]
    [TestCase("border-image-source:linear-gradient(red, blue)")]
    [TestCase("background-attachment:fixed")]
    [TestCase("background-blend-mode:multiply")]
    [TestCase("background-origin:content-box")]
    [TestCase("container-type:inline-size")]
    [TestCase("container:card / inline-size")]
    [TestCase("aspect-ratio:16 / 9")]
    [TestCase("aspect-ratio:auto 1 / 1")]
    [TestCase("mask-repeat:no-repeat")]
    [TestCase("mask-size:cover")]
    [TestCase("mask-type:alpha")]
    [TestCase("zoom:2")]
    [TestCase("zoom:150%")]
    [TestCase("zoom:reset")]
    [TestCase("contain:layout")]
    [TestCase("content-visibility:auto")]
    [TestCase("line-break:anywhere")]
    [TestCase("text-justify:inter-word")]
    [TestCase("image-rendering:pixelated")]
    [TestCase("border-collapse:collapse")]
    [TestCase("border-spacing:2pt")]
    [TestCase("border-spacing:0 1px")]
    [TestCase("border-spacing:1e-999px")]
    [TestCase("border-spacing:0 0 0")]
    [TestCase("border-spacing:0%")]
    [TestCase("border-spacing:none")]
    [TestCase("empty-cells:hide")]
    [TestCase("caption-side:bottom")]
    [TestCase("caption-side:top top")]
    [TestCase("offset-path:path('M0 0 L100 0')")]
    [TestCase("offset-path:ray(45deg)")]
    [TestCase("offset-rotate:90deg")]
    [TestCase("offset-rotate:auto 45deg")]
    [TestCase("offset-anchor:center")]
    [TestCase("initial-letter:3")]
    [TestCase("initial-letter:3 2")]
    [TestCase("text-emphasis-color:red")]
    public void AnyOtherValueFailsWithoutOutput(string declaration)
    {
        foreach (var html in new[] { $"<p style=\"{declaration}\">A</p>", $"<style>.unused{{{declaration}}}</style><p>A</p>" })
        {
            using var output = new MemoryStream();
            var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, Page));
            Assert.That(error!.Code, Is.EqualTo("FPDF1202"), html);
            Assert.That(output.Length, Is.Zero);
        }
    }

    [Test]
    public void RejectedValueNamesTheOnlySupportedValueAndItsSourceOffset()
    {
        const string html = "<p style='float:left'>A</p>";
        var error = Assert.Throws<FactsPdfException>(() => Pdf(html));
        Assert.That(error!.Message, Does.Contain("float supports only none"));
        Assert.That(error.SourceOffset, Is.EqualTo(html.IndexOf("left", StringComparison.Ordinal)));
    }

    [TestCase("<p style='transform:rotate(90deg)'>A</p>", "transform supports only none", "rotate")]
    [TestCase("<style>.unused{background-image:url(a.png)}</style><p>A</p>", "background-image supports only none", "url")]
    [TestCase("<p style='opacity:calc(1)'>A</p>", "opacity supports only 1", "calc")]
    [TestCase("<style>table{border-collapse:collapse}</style><p>A</p>", "border-collapse supports only separate", "collapse}")]
    [TestCase("<p style='border-spacing:2pt'>A</p>", "border-spacing supports only 0 (one or two zero lengths)", "2pt")]
    [TestCase("<style>td{empty-cells:hide}</style><p>A</p>", "empty-cells supports only show", "hide")]
    [TestCase("<p style='offset-rotate:reverse'>A</p>", "offset-rotate supports only auto", "reverse")]
    [TestCase("<p style='initial-letter:2'>A</p>", "initial-letter supports only normal", "2")]
    public void FunctionValuesNameTheOnlySupportedValue(string html, string message, string at)
    {
        var error = Assert.Throws<FactsPdfException>(() => Pdf(html));
        Assert.That(error!.Code, Is.EqualTo("FPDF1202"));
        Assert.That(error.Message, Does.Contain(message));
        Assert.That(error.SourceOffset, Is.EqualTo(html.IndexOf(at, StringComparison.Ordinal)));
    }

    [Test]
    public void PrefixedFontFeatureSettingsFailsWithTheStandardMessage()
    {
        const string html = "<p style='-webkit-font-feature-settings:\"kern\"'>A</p>";
        var error = Assert.Throws<FactsPdfException>(() => Pdf(html));
        Assert.That(error!.Code, Is.EqualTo("FPDF1202"));
        Assert.That(error.Message, Does.Contain("font-feature-settings supports only normal"));
        Assert.That(error.SourceOffset, Is.EqualTo(html.IndexOf("\"kern\"", StringComparison.Ordinal)));
    }

    [TestCase("-o-font-feature-settings:normal")]
    [TestCase("-ms-zoom:1")]
    public void UnlistedFontPropertiesStillFail(string declaration)
    {
        var error = Assert.Throws<FactsPdfException>(() => Pdf($"<p style='{declaration}'>A</p>"));
        Assert.That(error!.Code, Is.EqualTo("FPDF1201"));
    }

    [Test]
    public void OtherUnsupportedPropertiesStillFail()
    {
        var error = Assert.Throws<FactsPdfException>(() => Pdf("<p style='place-items:center'>A</p>"));
        Assert.That(error!.Code, Is.EqualTo("FPDF1201"));
    }

    [Test]
    public void TheSdkAcceptsTheInitialValues()
    {
        var bytes = FactsPDF.Pdf.Write().ToBytes("<p style='position:static;overflow:visible;opacity:1'>Paid</p>");
        Assert.That(bytes, Is.EqualTo(FactsPDF.Pdf.Write().ToBytes("<p>Paid</p>")));
    }
}
