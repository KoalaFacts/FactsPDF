using System.Text;
using System.Text.RegularExpressions;
using FactsPDF.CommandLine;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>Real searchable-PDF text-decoration paint: line, ink, stroke and layout budgets.</summary>
[TestFixture]
public sealed class TextDecorationPaintTests
{
    private static readonly PdfOptions Page = new()
    {
        PageWidth = 170, PageHeight = 128, Margin = 10
    };

    private static byte[] Pdf(string html, PdfOptions? options = null)
    {
        using var stream = new MemoryStream();
        PdfConverter.Convert(html, stream, options ?? Page);
        return stream.ToArray();
    }

    private static List<LayoutPage> Layout(string html, PdfOptions? options = null)
    {
        var o = options ?? Page;
        return TextLayout.Layout(HtmlDocumentReader.ReadTree(html, o, default), o, default);
    }

    private static void Reject(string html, string code, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        output.Write("original-pdf-sentinel"u8);
        output.Position = 4;
        var ex = Assert.Throws<FactsPdfException>(() =>
            PdfConverter.Convert(html, output, options ?? Page));
        Assert.That(ex!.Code, Is.EqualTo(code));
        Assert.That(output.ToArray(), Is.EqualTo("original-pdf-sentinel"u8.ToArray()));
        Assert.That(output.Position, Is.EqualTo(4));
    }

    [Test]
    public void DefaultSolidCurrentcolorStillUsesByteIdenticalLegacyRectangle()
    {
        const string legacy = "<p style='color:blue;text-decoration:underline'>AB</p>";
        Assert.That(Pdf(legacy),
            Is.EqualTo(Pdf("<p style='color:blue;text-decoration:underline;" +
                "text-decoration-color:currentcolor;text-decoration-style:solid'>AB</p>")));
        var stream = Encoding.Latin1.GetString(Pdf(legacy));
        Assert.That(stream, Does.Contain("0 0 1 rg "));
        Assert.That(Regex.Matches(stream, @" re f Q").Count, Is.EqualTo(1));
        Assert.That(stream, Does.Not.Contain(" re W n "));
    }

    [TestCase("underline", -0.12)]
    [TestCase("line-through", 0.29)]
    [TestCase("overline", 0.78)]
    public void SolidCustomInkPaintsAtRealFontMeasuredGlyphPosition(string kind, double baselineFactor)
    {
        var html = "<p style='font-size:10pt;color:blue;text-decoration-line:" +
            kind + ";text-decoration-color:red'>AB</p>";
        var glyph = Layout(html).Single().Runs.Single();
        Assert.That(glyph.Width, Is.EqualTo(12).Within(0.000001));
        var pdf = Encoding.Latin1.GetString(Pdf(html));
        var rectY = glyph.Baseline + baselineFactor * glyph.Style.FontSize - 0.3;
        var rectText = glyph.X.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
            + " " + rectY.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
            + " 12 0.6 re f Q";
        Assert.That(pdf, Does.Contain("1 0 0 rg " + rectText));
        Assert.That(pdf, Does.Contain("0 0 1 rg "));
        Assert.That(pdf, Does.Contain("<4142> Tj ET"));
    }

    [TestCase("dashed", "] 0 d ")]
    [TestCase("dotted", "1 J [0.001 ")]
    public void PatternedTextRuleUsesClippedPdfStrokeAndNotAFakeTextRun(string stroke, string marker)
    {
        var html = "<p style='font-size:10pt;text-decoration:overline " +
            stroke + " #ff0000'>ABCD</p>";
        var run = Layout(html).Single().Runs.Single();
        var data = Encoding.Latin1.GetString(Pdf(html));
        Assert.That(data, Does.Contain("1 0 0 RG "));
        Assert.That(data, Does.Contain(marker));
        Assert.That(data, Does.Contain(" re W n "));
        Assert.That(data, Does.Contain(" l S Q"));
        Assert.That(data, Does.Not.Contain(" re f Q"));
        Assert.That(data, Does.Contain("<41424344> Tj ET"));
        Assert.That(run.Width, Is.EqualTo(24).Within(0.000001));
        Assert.That(Pdf(html), Is.Not.EqualTo(Pdf("<p>ABCD</p>")));
    }

    [Test]
    public void ShorthandResetsOmittedInkAndPatternAndLonghandsCascadeIndependently()
    {
        var reset = Pdf("<p style='text-decoration:underline;text-decoration-color:red;" +
            "text-decoration-style:dotted;text-decoration:overline'>AB</p>");
        Assert.That(reset, Is.EqualTo(Pdf("<p style='text-decoration-line:overline'>AB</p>")));

        var sheet = "<style>.flag{text-decoration-color:blue!important;" +
            "text-decoration-style:dashed}p.flag{text-decoration:underline dotted red}</style>";
        var actual = Pdf(sheet + "<p class='flag'>AB</p>");
        var expected = Pdf("<p style='text-decoration-line:underline;" +
            "text-decoration-style:dotted;text-decoration-color:blue'>AB</p>");
        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void CssWideKeywordsResetOrInheritInkAndStrokeOnInlineRuns()
    {
        var initial = Pdf("<p style='color:blue;text-decoration:underline;" +
            "text-decoration-color:red;text-decoration-style:dotted'>" +
            "<span style='text-decoration-color:initial;text-decoration-style:initial'>AB</span></p>");
        var expected = Pdf("<p style='color:blue;text-decoration:underline;" +
            "text-decoration-color:red;text-decoration-style:dotted'>" +
            "<span style='text-decoration-color:currentcolor;text-decoration-style:solid'>AB</span></p>");
        Assert.That(initial, Is.EqualTo(expected));

        var inherited = Pdf("<p style='text-decoration:underline dashed red'>" +
            "<span style='text-decoration-color:inherit;text-decoration-style:inherit'>AB</span></p>");
        var direct = Pdf("<p style='text-decoration:underline dashed red'><span>AB</span></p>");
        Assert.That(inherited, Is.EqualTo(direct));
    }

    [Test]
    public void TrueTypeInlineSpansAndLongReportKeepRealGlyphAdvanceAndPagedStroke()
    {
        var font = PdfFont.LoadTrueType(FontFixture.Create());
        var options = Page with { PageWidth = 96, PageHeight = 128, Margin = 8, Fonts = [font] };
        var sample = "<p style='font-size:10pt;text-decoration:underline dashed red'>" +
            "A<span style='text-decoration-color:blue'>B</span>A</p>";
        var runs = Layout(sample, options).Single().Runs;
        Assert.That(runs.Select(r => r.Text), Is.EqualTo(new[] { "A", "B", "A" }));
        Assert.That(runs.All(r => ReferenceEquals(r.Font, font)), Is.True);
        Assert.That(runs[1].X, Is.EqualTo(runs[0].X + runs[0].Width).Within(0.000001));
        Assert.That(runs[2].X, Is.EqualTo(runs[1].X + runs[1].Width).Within(0.000001));
        var pdf = Encoding.Latin1.GetString(Pdf(sample, options));
        Assert.That(Regex.Matches(pdf, @" re W n ").Count, Is.EqualTo(3));

        var html = string.Concat(Enumerable.Repeat(
            "<p style='font-size:10pt;margin-bottom:0pt;text-decoration:underline dotted red'>ABA</p>", 45));
        var pages = Layout(html, options);
        Assert.That(pages.Count, Is.GreaterThan(2));
        var expectedCount = pages.Sum(p => p.Runs.Count(r => r.Style.Decoration != TextDecoration.None));
        var paged = Encoding.Latin1.GetString(Pdf(html, options));
        Assert.That(Regex.Matches(paged, @" re W n ").Count, Is.EqualTo(expectedCount));
    }

    [TestCase("text-decoration:underline wavy", "FPDF1202")]
    [TestCase("text-decoration:underline dashed dotted", "FPDF1202")]
    [TestCase("text-decoration:underline red blue", "FPDF1202")]
    [TestCase("text-decoration:underline line-through", "FPDF1202")]
    [TestCase("text-decoration:none red", "FPDF1202")]
    [TestCase("text-decoration-color:transparent", "FPDF1202")]
    [TestCase("text-decoration-style:double", "FPDF1202")]
    [TestCase("text-decoration:underline dashed rgba(255,0,0,0.4)", "FPDF1202")]
    public void UnsupportedDecorationValuesFailEvenInUnmatchedRulesWithoutTouchingDestination(string css, string code)
        => Reject("<style>.absent{" + css + "}</style><p>Safe output</p>", code);

    [TestCase("text-decoration-style:dotted")]
    [TestCase("text-decoration-color:red")]
    [TestCase("text-decoration-line:overline")]
    [TestCase("text-decoration:underline dashed red")]
    public void DecorationStylesOnStandaloneImagesAreNotSilentlyAccepted(string declaration)
        => Reject("<img src='brand' style='" + declaration + "'>", "FPDF1201");

    [Test]
    public void OneStrokeConsumesOneRealDisplayCommandAndProtectsOriginalFile()
    {
        const string html = "<p style='text-decoration:overline dotted red'>AB</p>";
        Reject(html, "FPDF1401", Page with { MaxDisplayCommands = 1 });
        Assert.That(Pdf(html, Page with { MaxDisplayCommands = 2 }), Is.Not.Empty);
    }

    [Test]
    public void ManagedCliAndLibraryGenerateIdenticalDecoratedPdfBytes()
    {
        const string html = "<p style='text-decoration:overline dashed blue'>Invoice AB</p>";
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(html));
        using var output = new MemoryStream();
        using var diagnostics = new StringWriter();
        var result = CliApplication.Run(["-", "-"], input, output, diagnostics);
        Assert.That(result, Is.Zero, diagnostics.ToString());
        Assert.That(output.ToArray(), Is.EqualTo(Pdf(html, new())));
    }
}
