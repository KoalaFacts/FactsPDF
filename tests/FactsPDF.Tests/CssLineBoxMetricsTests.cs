using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>CSS inline line-box strut and fallback-font baseline contracts.</summary>
[TestFixture]
public sealed class CssLineBoxMetricsTests
{
    private static PdfFont UnusualFallback(int ascender = 1250, int descender = -150)
    {
        var bytes = FontFixture.Create();
        var os2 = FontFixture.Table(bytes, "OS/2");
        // Use the hhea metrics and vary them independently of the primary font.
        FontFixture.U16(bytes, os2 + 62, 0);
        var hhea = FontFixture.Table(bytes, "hhea");
        FontFixture.U16(bytes, hhea + 4, ascender);
        FontFixture.U16(bytes, hhea + 6, descender);
        return PdfFont.LoadTrueType(bytes);
    }

    private static LayoutPage Page(string markup, IReadOnlyList<PdfFont> fonts)
    {
        var options = new PdfOptions { Fonts = fonts };
        var paragraphs = HtmlDocumentReader.Read(
            "<style>p{font-size:12pt;line-height:1.3;margin-top:0pt;margin-bottom:0pt}</style>" + markup,
            options, CancellationToken.None);
        return TextLayout.Layout(paragraphs, options, CancellationToken.None)[0];
    }

    [Test]
    public void FallbackTypefaceCannotShiftThePrimaryFontLineBaseline()
    {
        var latin = PdfFont.LoadTrueType(FontFixture.Create(latinOnly: true));
        var fallback = UnusualFallback();
        var plain = Page("<p>A</p>", [latin, fallback]);
        var mixed = Page("<p>A中</p>", [latin, fallback]);
        Assert.That(mixed.Runs.Count, Is.EqualTo(2));
        Assert.That(mixed.Runs[1].Font, Is.SameAs(fallback));
        Assert.That(mixed.Runs[0].Baseline, Is.EqualTo(plain.Runs[0].Baseline).Within(0.0001));
        Assert.That(mixed.Runs[1].Baseline, Is.EqualTo(plain.Runs[0].Baseline).Within(0.0001));
    }

    [Test]
    public void FallbackTypefaceCannotChangeFollowingParagraphLineSpacing()
    {
        var latin = PdfFont.LoadTrueType(FontFixture.Create(latinOnly: true));
        var fallback = UnusualFallback();
        var plain = Page("<p>A</p><p>A</p>", [latin, fallback]);
        var mixed = Page("<p>A中</p><p>A</p>", [latin, fallback]);
        Assert.That(plain.Runs.Count, Is.EqualTo(2));
        Assert.That(mixed.Runs.Count, Is.EqualTo(3));
        Assert.That(mixed.Runs[2].Baseline, Is.EqualTo(plain.Runs[1].Baseline).Within(0.0001));
        Assert.That(plain.Runs[0].Baseline - plain.Runs[1].Baseline, Is.EqualTo(15.6).Within(0.0001));
    }

    [Test]
    public void ExplicitLineHeightMustNotGrowToTheFontBoundingMetrics()
    {
        var font = UnusualFallback(ascender: 1200, descender: -500);
        var options = new PdfOptions { Fonts = [font] };
        var paragraphs = HtmlDocumentReader.Read(
            "<style>p{font-size:12pt;line-height:1;margin-top:0pt;margin-bottom:0pt}</style>" +
            "<p>A</p><p>A</p>",
            options, CancellationToken.None);
        var runs = TextLayout.Layout(paragraphs, options, CancellationToken.None)[0].Runs;
        Assert.That(runs, Has.Count.EqualTo(2));
        Assert.That(runs[0].Baseline - runs[1].Baseline, Is.EqualTo(12).Within(0.0001));
    }

    [Test]
    public void LargerInlineFontStillContributesToLineBoxHeight()
    {
        var font = PdfFont.LoadTrueType(FontFixture.Create());
        var options = new PdfOptions { Fonts = [font] };
        var paragraphs = HtmlDocumentReader.Read(
            "<style>p{font-size:12pt;line-height:1.3;margin-top:0pt;margin-bottom:0pt}</style>" +
            "<p>A<span style='font-size:24pt'>B</span></p><p>A</p>",
            options, CancellationToken.None);
        var runs = TextLayout.Layout(paragraphs, options, CancellationToken.None)[0].Runs;
        Assert.That(runs, Has.Count.EqualTo(3));
        Assert.That(runs[0].Baseline, Is.EqualTo(runs[1].Baseline).Within(0.0001));
        Assert.That(runs[0].Baseline - runs[2].Baseline, Is.GreaterThan(24));
    }
}
