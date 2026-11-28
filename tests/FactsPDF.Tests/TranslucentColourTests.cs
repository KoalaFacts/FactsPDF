using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// Translucent colours (rgba()/hsla(), the "/ alpha" space form, #rgba/#rrggbbaa and transparent)
/// paint with PDF constant alpha: each translucent paint is enclosed in q/Q and selects its page's
/// ExtGState for the alpha byte (/ca fill, /CA stroke), so it composites over whatever is below
/// it as Chrome does. Alpha that rounds to 255 keeps the opaque bytes; alpha 0 paints nothing.
/// </summary>
[TestFixture]
public sealed class TranslucentColourTests
{
    private static readonly PdfOptions Options = new() { PageWidth = 300, PageHeight = 500, Margin = 20 };
    private static readonly PdfFont Font = PdfFont.FromBytes(FontFixture.Create());

    private static string Render(string html, PdfOptions? options = null)
    {
        using var stream = new MemoryStream();
        PdfConverter.Convert(html, stream, options ?? Options);
        return Encoding.Latin1.GetString(stream.ToArray());
    }

    private static string Page(string c) => $"<style>h1{{font-weight:normal;color:{c}}}</style><h1>Total</h1>" +
        $"<div style='background:{c};border:2pt dashed {c};outline:1pt solid {c};text-decoration:underline {c}'>A</div>" +
        $"<div style='border:3pt solid {c};border-radius:4pt'>R</div>" +
        $"<ul style='color:{c}'><li>Item</li></ul><p style='color:{c}'>B</p>";

    [TestCase("rgba(35, 68, 107, 0.5)")]
    [TestCase("rgba(35,68,107,.5)")]
    [TestCase("rgb(35 68 107 / 50%)")]
    [TestCase("rgb(35, 68, 107, 0.5)")]
    [TestCase("RGBA(35 68 107 / 0.5)")]
    [TestCase("#23446b80")]
    public void EveryAlphaSpellingPaintsTheSameBytes(string colour) =>
        Assert.That(Render(Page(colour)), Is.EqualTo(Render(Page("#23446b80"))));

    [TestCase("#abc8", "#aabbcc88")]
    [TestCase("hsla(210, 50%, 40%, 0.25)", "#33669940")]
    [TestCase("hsl(210 50% 40% / 25%)", "hsla(210,50%,40%,.25)")]
    [TestCase("rgba(0%, 50%, 100%, 0.75)", "#0080ffbf")]
    public void HexShorthandHslAndPercentChannelsLowerToTheSameTranslucentColour(string colour, string control) =>
        Assert.That(Render(Page(colour)), Is.EqualTo(Render(Page(control))));

    [Test]
    public void TranslucentPaintSelectsOneDeduplicatedGraphicsStatePerAlpha()
    {
        var pdf = Render("<div style='background:#1e5aa0'>" +
            "<div style='background:rgba(255,255,255,0.5);border-top:1pt solid rgba(164,176,195,0.2)'>" +
            "<p style='color:rgba(0,0,0,0.5)'>A</p></div>" +
            "<div style='background:rgba(255,0,0,0.5)'>B</div></div>");
        Assert.That(Regex.Count(pdf, "/ExtGState"), Is.EqualTo(1));
        Assert.That(pdf, Does.Contain("/ExtGState << /A33 << /CA 0.2 /ca 0.2 >> /A80 << /CA 0.502 /ca 0.502 >> >>"));
        Assert.That(pdf, Does.Contain("q 0.118 0.353 0.627 rg"), "The opaque outer background keeps its bytes.");
        Assert.That(pdf, Does.Contain("q /A80 gs 1 1 1 rg"));
        Assert.That(pdf, Does.Contain("q /A33 gs 0.643 0.69 0.765 rg"));
        Assert.That(pdf, Does.Contain("q /A80 gs 1 0 0 rg"));
        Assert.That(pdf, Does.Match(@"q /A80 gs BT /F1 \S+ Tf 0 0 0 rg [^\n]* Tj ET\nQ\n"));
    }

    [Test]
    public void StrokedBordersOutlinesDecorationsAndMarkersUseTheSameState()
    {
        var pdf = Render("<div style='border:2pt dashed rgba(0,128,0,0.6);outline:1pt dotted rgba(0,128,0,0.6)'>" +
            "<p style='text-decoration:underline dotted rgba(0,128,0,0.6)'>A</p></div>" +
            "<ul style='color:rgba(0,128,0,0.6)'><li>B</li></ul>" +
            "<ul style='list-style-type:circle;color:rgba(0,128,0,0.6)'><li>C</li></ul>" +
            "<ul style='list-style-type:square;color:rgba(0,128,0,0.6)'><li>D</li></ul>");
        Assert.That(pdf, Does.Contain("/A99 << /CA 0.6 /ca 0.6 >>"));
        Assert.That(Regex.Count(pdf, @"q /A99 gs 0 0\.502 0 RG"), Is.EqualTo(4 + 1 + 1 + 1),
            "Four dashed edges, the outline, the dotted underline and the circle marker stroke.");
        Assert.That(Regex.Count(pdf, @"q /A99 gs 0 0\.502 0 rg"), Is.EqualTo(2), "The disc and square markers fill.");
    }

    [Test]
    public void CurrentColorBordersAndBackgroundsInheritTheTextAlpha()
    {
        var pdf = Render("<div style='color:rgba(200,0,0,0.4);border:2pt solid;background:currentcolor'>A</div>");
        Assert.That(Regex.Count(pdf, @"q /A66 gs 0\.784 0 0 rg"), Is.EqualTo(1 + 4));
        Assert.That(pdf, Does.Contain("/A66 << /CA 0.4 /ca 0.4 >>"));
    }

    [TestCase("rgba(35,68,107,1)")]
    [TestCase("rgb(35 68 107 / 100%)")]
    [TestCase("#23446bff")]
    [TestCase("hsla(212.3, 50.7%, 27.8%, 1)")]
    public void AlphaOneIsByteIdenticalToTheOpaqueColour(string colour)
    {
        var control = colour.StartsWith("hsla", StringComparison.Ordinal) ? "hsl(212.3, 50.7%, 27.8%)" : "#23446b";
        var pdf = Render(Page(colour));
        Assert.That(pdf, Is.EqualTo(Render(Page(control))));
        Assert.That(pdf, Does.Not.Contain("/ExtGState"));
        Assert.That(pdf, Does.Not.Contain(" gs "));
    }

    [TestCase("rgba(255,0,0,0)")]
    [TestCase("#f000")]
    [TestCase("#ff000000")]
    [TestCase("hsl(0 100% 50% / 0%)")]
    public void AlphaZeroPaintsLikeTransparentAndAddsNoGraphicsState(string colour)
    {
        static string Boxes(string c) =>
            $"<div style='background:{c};border:2pt solid {c};outline:1pt solid {c}'>A</div>" +
            $"<div style='border:2pt dashed {c}'><p style='text-decoration:underline {c}'>B</p></div>";
        var pdf = Render(Boxes(colour));
        Assert.That(pdf, Is.EqualTo(Render(Boxes("transparent"))));
        Assert.That(pdf, Does.Not.Contain(" gs ").And.Not.Contain("/ExtGState"));
        Assert.That(pdf, Does.Not.Contain(" re f Q").And.Not.Contain(" S Q"), "No box, edge, outline or underline paint.");
    }

    [Test]
    public void TransparentTextKeepsItsLayoutButPaintsNoGlyphsWhileItsDecorationStillPaints()
    {
        var hidden = Render("<p style='color:rgba(0,0,0,0)'>Hidden</p><p>Shown</p>");
        Assert.That(hidden, Is.EqualTo(Render("<p style='color:transparent'>Hidden</p><p>Shown</p>")));
        Assert.That(Regex.Count(hidden, " Tj ET"), Is.EqualTo(1), "Only the visible paragraph paints glyphs.");
        var underlined = Render("<p style='color:transparent;text-decoration:underline rgba(255,0,0,0.5)'>Hidden</p>");
        Assert.That(underlined, Does.Not.Contain(" Tj ET"));
        Assert.That(underlined, Does.Contain("q /A80 gs 1 0 0 rg"));
    }

    [Test]
    public void TranslucentRunRestoresCharacterSpacingForTheNextRun()
    {
        // Q restores Tc with the rest of the graphics state, so the opaque run that follows a
        // tracked translucent run must set its own (equal) tracking again.
        var pdf = Render("<p style='letter-spacing:1pt;color:rgba(0,0,0,0.5)'>A</p><p style='letter-spacing:1pt'>B</p>" +
            "<p style='letter-spacing:1pt'>C</p>");
        Assert.That(Regex.Count(pdf, " 1 Tc "), Is.EqualTo(2), "Once inside q/Q and once after it; C reuses B's.");
        var matches = Regex.Matches(pdf, @"(q /A80 gs )?BT /F1 \S+ Tf (1 Tc )?");
        Assert.That(matches.Select(m => (m.Groups[1].Success, m.Groups[2].Success)),
            Is.EqualTo(new[] { (true, true), (false, true), (false, false) }));
    }

    [Test]
    public void EmbeddedFontTextUsesTheSameGraphicsStates()
    {
        var options = Options with { Fonts = [Font], SubsetFonts = true };
        var pdf = Render("<div style='background:rgba(0,0,255,0.25)'><p style='color:rgb(0 0 0 / 0.5)'>AB</p></div>", options);
        Assert.That(pdf, Does.Contain("/ExtGState << /A40 << /CA 0.251 /ca 0.251 >> /A80 << /CA 0.502 /ca 0.502 >> >>"));
        Assert.That(pdf, Does.Match(@"q /A80 gs BT /F1 \S+ Tf 0 0 0 rg [^\n]* Tj ET\nQ\n"));
        Assert.That(pdf, Does.Contain("q /A40 gs 0 0 1 rg"));
    }

    [Test]
    public void EachPageDeclaresOnlyTheAlphaItPaints()
    {
        var pdf = Render("<p style='color:rgba(0,0,0,0.2)'>A</p><p style='break-before:page;color:rgba(0,0,0,0.4)'>B</p>" +
            "<p style='break-before:page'>C</p>");
        var pages = Regex.Matches(pdf, @"/Type /Page /Parent[^\n]*").Select(m => m.Value).ToArray();
        Assert.That(pages, Has.Length.EqualTo(3));
        Assert.That(pages[0], Does.Contain("/ExtGState << /A33 << /CA 0.2 /ca 0.2 >> >>"));
        Assert.That(pages[1], Does.Contain("/ExtGState << /A66 << /CA 0.4 /ca 0.4 >> >>"));
        Assert.That(pages[2], Does.Not.Contain("/ExtGState"));
    }

    [TestCase("0.1", "1a")]
    [TestCase("0.3", "4d")]
    [TestCase("0.7", "b3")]
    [TestCase("50%", "80")]
    [TestCase("12.5%", "20")]
    [TestCase("0.996", "fe")]
    public void AlphaRoundsToTheByteChromePaints(string alpha, string hex) =>
        Assert.That(Render(Page($"rgba(35,68,107,{alpha})")), Is.EqualTo(Render(Page("#23446b" + hex))));

    [Test]
    public void ZeroAlphaInlineBackgroundIsDroppedLikeTransparent()
    {
        const string body = "<p>Paid <a href='https://example.com'>in full</a></p>";
        var control = Render(body);
        foreach (var css in new[] { "a{background-color:rgba(0,0,0,0)}", "a{background:#0000}", "a{background:#12345600}" })
            Assert.That(Render($"<style>{css}</style>" + body), Is.EqualTo(control), css);
    }

    [TestCase("color:rgba(1,2,3,none)")]
    [TestCase("color:rgb(1 2 3 / 1px)")]
    [TestCase("color:rgba(1,2,3,50deg)")]
    [TestCase("color:rgb(1 2 3 /)")]
    [TestCase("color:rgb(1 2 3 / 0.5 / 1)")]
    [TestCase("color:#12345")]
    [TestCase("opacity:0.5")]
    public void MalformedAlphaAndOpacityStillFailBeforeWriting(string css)
    {
        using var stream = new MemoryStream();
        stream.Write("keep"u8);
        var error = Assert.Throws<FactsPdfException>(
            () => PdfConverter.Convert($"<div style='{css}'>A</div>", stream, Options));
        Assert.That(error!.Code, Is.EqualTo("FPDF1202"), css);
        Assert.That(stream.ToArray(), Is.EqualTo("keep"u8.ToArray()));
    }
}
