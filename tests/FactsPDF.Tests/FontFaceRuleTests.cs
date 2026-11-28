using NUnit.Framework;
using static FactsPDF.Tests.FontFamilyMatchingTests;

namespace FactsPDF.Tests;

/// <summary>
/// @font-face: faces matched by their descriptors, sources loaded on first use from caller-bound
/// resources (by the exact url() text), data: URLs or local() names, format() hints honoured as
/// Chrome honours them, and every descriptor value the renderer cannot print failing explicitly.
/// </summary>
[TestFixture]
public sealed class FontFaceRuleTests
{
    private static readonly PdfFont Regular = Face(200), Web = Face(400), WebBold = Face(700, 700), WebItalic = Face(600, italic: true);

    private static PdfWriter Writer() => Pdf.Write().WithFonts(f => f.Regular(Regular))
        .WithFontResource("fonts/web.woff2", Web).WithFontResource("fonts/web-bold.woff2", WebBold)
        .WithFontResource("fonts/web-italic.ttf", WebItalic);

    private const string Faces = "<style>"
        + "@font-face{font-family:Web;src:url(fonts/web.woff2) format('woff2')}"
        + "@font-face{font-family:Web;font-weight:600 800;src:url('fonts/web-bold.woff2') format(woff2)}"
        + "@font-face{font-family:'Web';font-style:italic;font-display:swap;src:url(fonts/web-italic.ttf) format('truetype')}"
        + "p{font-family:Web,serif}</style>";

    private static PdfFont[] Fonts(PdfWriter writer, string html) => [.. Runs(writer, html).Select(r => r.Font!)];

    [Test]
    public void DescriptorsSelectEachFace()
        => Assert.That(Fonts(Writer(), Faces + "<p>A<b>A</b><i>A</i><span style='font-weight:650'>A</span></p>"),
            Is.EqualTo(new[] { Web, WebBold, WebItalic, WebBold }));

    [Test]
    public void ARegularOnlyWebFamilyFailsWhereChromeWouldSynthesize()
    {
        const string css = "<style>@font-face{font-family:Web;src:url(fonts/web.woff2)}p{font-family:Web}</style>";
        Assert.That(Fails(Writer(), css + "<p><b>A</b></p>", "FPDF1507").Message, Does.Contain("synthesize bold"));
        Fails(Writer(), css + "<p><i>A</i></p>", "FPDF1507");
        // The web-font rule is "600 or heavier against a face under 600": 550 draws the face as it is.
        Assert.That(Fonts(Writer(), css + "<p style='font-weight:550'>A</p>")[0], Is.SameAs(Web));
    }

    [Test]
    public void AnUnboundSourceFailsAtItsUrlOnlyWhenUsed()
    {
        const string css = "<style>@font-face{font-family:Missing;src:url(fonts/missing.woff2)}</style>";
        Assert.DoesNotThrow(() => Writer().ToBytes(css + "<p>A</p>"), "Fonts load on demand: an unused face needs no resource.");
        var html = css + "<p style='font-family:Missing'>A</p>";
        var error = Fails(Writer(), html, "FPDF1508");
        Assert.That(error.SourceOffset, Is.EqualTo(html.IndexOf("url(fonts/missing", StringComparison.Ordinal)));
    }

    [Test]
    public void TheFirstBoundSourceIsUsed()
        => Assert.That(Fonts(Writer(), "<style>@font-face{font-family:W;src:url(fonts/none.woff2) format('woff2'),url(fonts/web-italic.ttf)}"
            + "p{font-family:W}</style><p>A</p>")[0], Is.SameAs(WebItalic));

    [Test]
    public void SourcesInFormatsChromeCannotLoadAreSkipped()
        => Assert.That(Fonts(Writer(), "<style>@font-face{font-family:W;src:url(fonts/web.woff2) format('embedded-opentype')}"
            + "p{font-family:W}</style><p>A</p>")[0], Is.SameAs(Regular), "No loadable source: the family falls back like an absent one.");

    [Test]
    public void DataUrlsDecodeInPlace()
    {
        var bytes = FontFixture.Create();
        FontFixture.U16(bytes, FontFixture.Table(bytes, "hmtx") + 8, 900);
        var url = "data:font/woff;base64," + Convert.ToBase64String(FontContainerTests.Woff(bytes));
        var font = Fonts(Writer(), $"<style>@font-face{{font-family:Inline;src:url({url}) format('woff')}}p{{font-family:Inline}}</style><p>A</p>")[0];
        Assert.That(font.Width1000(2), Is.EqualTo(900));
        Fails(Writer(), "<style>@font-face{font-family:I;src:url(data:font/ttf,abc)}p{font-family:I}</style><p>A</p>", "FPDF1202");
    }

    [Test]
    public void LocalMatchesASuppliedFontByName()
    {
        var installed = Face(800);
        var writer = Pdf.Write().WithFonts(f => f.Regular(Regular).Family("Installed", installed));
        Assert.That(Fonts(writer, "<style>@font-face{font-family:L;src:local('FactsPDFTest'),url(fonts/x.woff2)}p{font-family:L}</style><p>A</p>")[0],
            Is.SameAs(installed));
    }

    [Test]
    public void AFontFaceHidesTheRegisteredFamilyOfItsName()
    {
        var registered = Face(800);
        var writer = Pdf.Write().WithFonts(f => f.Regular(Regular).Family("Web", registered)).WithFontResource("fonts/web.woff2", Web);
        Assert.That(Fonts(writer, "<style>@font-face{font-family:Web;src:url(fonts/web.woff2)}p{font-family:Web}</style><p>A</p>")[0], Is.SameAs(Web));
        Assert.That(Fonts(writer, "<p style='font-family:Web'>A</p>")[0], Is.SameAs(registered));
    }

    [Test]
    public void FontFaceRulesWorkInsidePrintMedia()
        => Assert.That(Fonts(Writer(), "<style>@media print{@font-face{font-family:P;src:url(fonts/web.woff2)}}p{font-family:P}</style><p>A</p>")[0],
            Is.SameAs(Web));

    // Valid descriptors that change what is printed in ways FactsPDF does not implement.
    [TestCase("unicode-range:U+0-7F")]
    [TestCase("size-adjust:90%")]
    [TestCase("ascent-override:90%")]
    [TestCase("font-feature-settings:'liga' 0")]
    [TestCase("font-weight:auto")]
    [TestCase("font-style:oblique -10deg")]
    public void UnsupportedDescriptorsFailInBothModes(string descriptor)
    {
        var html = $"<style>@font-face{{font-family:W;src:url(fonts/web.woff2);{descriptor}}}</style><p>A</p>";
        Fails(Writer(), html, "FPDF1202");
        Assert.That(Assert.Throws<FactsPdfException>(() => Writer().WithStrict(false).ToBytes(html))!.Code, Is.EqualTo("FPDF1202"));
    }

    [Test]
    public void TechHintsFail()
        => Fails(Writer(), "<style>@font-face{font-family:W;src:url(fonts/web.woff2) tech(variations)}</style><p>A</p>", "FPDF1202");

    // Invalid rules (no src, a generic family name, an unknown descriptor) are dropped by Chrome:
    // strict mode fails FPDF1204, non-strict mode warns and keeps the page.
    [TestCase("@font-face{font-family:W}")]
    [TestCase("@font-face{font-family:serif;src:url(fonts/web.woff2)}")]
    [TestCase("@font-face{font-family:W;src:url(fonts/web.woff2);colour:red}")]
    [TestCase("@font-face{font-family:W;src:fonts/web.woff2}")]
    public void InvalidRulesFailStrictAndAreDroppedNonStrict(string rule)
    {
        var html = $"<style>{rule}</style><p>A</p>";
        Fails(Writer(), html, "FPDF1204");
        PdfWarnings? warnings = null;
        Assert.That(Writer().WithStrict(false).WithWarnings(w => warnings = w).ToBytes(html), Is.EqualTo(Writer().ToBytes("<p>A</p>")));
        Assert.That(warnings!.Items.Select(w => w.Code), Has.Member("FPDF1204"));
    }
}
