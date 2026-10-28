using System.Text;
using System.Text.RegularExpressions;
using FactsPDF.CommandLine;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>Report underlines and strike-throughs are true PDF paint, not accepted-only CSS.</summary>
[TestFixture]
public sealed class TextDecorationTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 180, PageHeight = 160, Margin = 12 };

    private static byte[] Pdf(string html, PdfOptions? options = null)
    {
        using var stream = new MemoryStream();
        PdfConverter.Convert(html, stream, options ?? Page);
        return stream.ToArray();
    }

    private static List<LayoutPage> Layout(string html, PdfOptions? options = null)
    {
        var page = options ?? Page;
        return TextLayout.Layout(HtmlDocumentReader.ReadTree(html, page, default), page, default);
    }

    private static void Reject(string html, string code, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        output.Write("sentinel"u8);
        output.Position = 2;
        var failure = Assert.Throws<FactsPdfException>(() =>
            PdfConverter.Convert(html, output, options ?? Page));
        Assert.That(failure!.Code, Is.EqualTo(code));
        Assert.That(output.ToArray(), Is.EqualTo("sentinel"u8.ToArray()));
        Assert.That(output.Position, Is.EqualTo(2));
    }

    [Test]
    public void DefaultAndExplicitNoneProduceExactlyTheSameOriginalPdf()
    {
        var baseline = Pdf("<p>Net amount</p>");
        Assert.That(Pdf("<p style='text-decoration:none'>Net amount</p>"), Is.EqualTo(baseline));
        Assert.That(Pdf("<p style='text-decoration-line:none'>Net amount</p>"), Is.EqualTo(baseline));
    }

    [TestCase("underline", 1)]
    [TestCase("line-through", 1)]
    public void BothStylesPaintARealOpaqueRectangleWithActualGlyphWidth(string value, int count)
    {
        var html = "<p style='font-size:10pt;text-decoration:" + value + "'>AB</p>";
        var lines = Layout(html);
        var text = lines.Single().Runs.Single();
        Assert.That(text.Width, Is.EqualTo(12).Within(0.000001));
        var pdf = Encoding.Latin1.GetString(Pdf(html));
        Assert.That(Regex.Matches(pdf, @" re f Q").Count, Is.EqualTo(count));
        Assert.That(pdf, Does.Contain("12 0.6 re f Q"));
        Assert.That(pdf, Does.Contain("<4142> Tj ET"));
        Assert.That(Pdf(html), Is.Not.EqualTo(Pdf("<p style='font-size:10pt'>AB</p>")));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ExplicitTrueTypePaintUsesActualResolvedAdvanceAndBothPdfSerializers(bool subset)
    {
        var font = PdfFont.LoadTrueType(FontFixture.Create());
        var options = Page with { Fonts = [font], BoldFonts = [font], SubsetFonts = subset };
        const string html = "<p style='font-size:10pt;text-decoration:underline'>A<b>B</b>A</p>";
        var runs = Layout(html, options).Single().Runs;
        Assert.That(runs.Select(x => x.Text), Is.EqualTo(new[] { "A", "B", "A" }));
        Assert.That(runs.All(x => ReferenceEquals(x.Font, font)), Is.True);
        foreach (var run in runs)
        {
            var glyph = font.GlyphFor(run.Text[0]);
            Assert.That(glyph, Is.GreaterThan(0));
            Assert.That(run.Width, Is.EqualTo(font.Width1000(glyph) * 0.01).Within(0.000001));
        }
        var result = Encoding.Latin1.GetString(Pdf(html, options));
        Assert.That(result, Does.Contain("/Subtype /Type0"));
        Assert.That(Regex.Matches(result, @" re f Q").Count, Is.EqualTo(3));
        Assert.That(Pdf(html, options), Is.Not.EqualTo(Pdf("<p style='font-size:10pt'>A<b>B</b>A</p>", options)));
    }
    [Test]
    public void DecorationLineAndShorthandFollowOneCascadeAndImportantOrder()
    {
        const string stylesheet = "<style>.price{text-decoration:line-through!important}" +
            "p.price{text-decoration-line:underline}</style>";
        var styled = stylesheet + "<p class=price style='text-decoration:underline'>Old price</p>";
        var expected = "<p style='text-decoration:line-through'>Old price</p>";
        Assert.That(Pdf(styled), Is.EqualTo(Pdf(expected)));
        Assert.That(Pdf("<p style='text-decoration:underline;text-decoration-line:line-through'>Old</p>"),
            Is.EqualTo(Pdf("<p style='text-decoration:line-through'>Old</p>")));
    }

    [Test]
    public void InlineSegmentsUseEachStyleAndActualHorizontalAdvance()
    {
        const string html = "<p>A<span style='text-decoration:underline;color:red'>BC</span>" +
            "<span style='text-decoration:line-through'>D</span>E</p>";
        var runs = Layout(html).Single().Runs;
        Assert.That(runs.Select(r => r.Text), Is.EqualTo(new[] { "A", "BC", "D", "E" }));
        Assert.That(runs.Select(r => r.Style.Decoration), Is.EqualTo(new[] {
            TextDecoration.None, TextDecoration.Underline, TextDecoration.LineThrough, TextDecoration.None }));
        Assert.That(runs[1].X, Is.EqualTo(runs[0].X + runs[0].Width).Within(0.000001));
        Assert.That(runs[2].X, Is.EqualTo(runs[1].X + runs[1].Width).Within(0.000001));
        Assert.That(Regex.Matches(Encoding.Latin1.GetString(Pdf(html)), @" re f Q").Count, Is.EqualTo(2));
    }

    [Test]
    public void StyledTextStillWrapsAndRepaintsAcrossPhysicalPages()
    {
        var options = Page with { PageWidth = 90, PageHeight = 72, Margin = 10 };
        var html = "<p style='font-size:10pt;line-height:1.2;text-decoration:underline'>" +
            string.Join(" ", Enumerable.Repeat("AB", 24)) + "</p>";
        var pages = Layout(html, options);
        Assert.That(pages.Count, Is.GreaterThan(1));
        Assert.That(pages.Sum(p => p.Runs.Count), Is.GreaterThan(4));
        Assert.That(pages.SelectMany(p => p.Runs).All(r => r.Style.Decoration == TextDecoration.Underline), Is.True);
        var pdf = Encoding.Latin1.GetString(Pdf(html, options));
        Assert.That(Regex.Matches(pdf, @" re f Q").Count,
            Is.EqualTo(pages.Sum(p => p.Runs.Count)), "Every wrapped line has a drawn underline.");
    }

    [Test]
    public void CssWideKeywordAndNonInheritedResetAreDeterministic()
    {
        const string initial = "<p style='text-decoration:underline'><span style='text-decoration:initial'>A</span></p>";
        const string none = "<p style='text-decoration:underline'><span style='text-decoration:none'>A</span></p>";
        Assert.That(Pdf(initial), Is.EqualTo(Pdf(none)));
        const string inherited = "<p style='text-decoration:underline'><span style='text-decoration:inherit'>A</span></p>";
        Assert.That(Pdf(inherited), Is.EqualTo(Pdf("<p style='text-decoration:underline'>A</p>")));
        Assert.That(Pdf("<p style='text-decoration:underline'><span style='text-decoration:unset'>A</span></p>"),
            Is.EqualTo(Pdf(none)));
    }

    [TestCase("text-decoration:underline wavy", "FPDF1202")]
    [TestCase("text-decoration:overline", "FPDF1202")]
    [TestCase("text-decoration:blink", "FPDF1202")]
    [TestCase("text-decoration:underline line-through", "FPDF1202")]
    [TestCase("text-decoration-line:dotted", "FPDF1202")]
    [TestCase("text-decoration-color:red", "FPDF1201")]
    [TestCase("text-decoration-style:dotted", "FPDF1201")]
    public void UnsupportedPaintIsRejectedEvenInsideUnmatchedStylesheet(string css, string code)
        => Reject("<style>.absent{" + css + "}</style><p>Report remains safe</p>", code);

    [Test]
    public void DisplayCommandBudgetCountsExtraPaintAndPreservesOutput()
    {
        var html = "<p style='text-decoration:underline'>A</p>";
        Reject(html, "FPDF1401", Page with { MaxDisplayCommands = 1 });
        Assert.That(Pdf(html, Page with { MaxDisplayCommands = 2 }), Is.Not.Empty);
    }

    [Test]
    public void PageFurnitureUsesTheSameChargedDecorationBudgetAsBodyLayout()
    {
        const string html = "<p style='text-decoration:underline'>A</p>";
        var options = Page with { FooterText = "Internal", MaxDisplayCommands = 2 };
        Reject(html, "FPDF1401", options);
        Assert.That(Pdf(html, options with { MaxDisplayCommands = 3 }), Is.Not.Empty);
    }
    [Test]
    public void ManagedCliAndLibraryEmitSamePaintedPdf()
    {
        const string html = "<p style='text-decoration:underline'>Invoice reference 20A</p>";
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(html));
        using var output = new MemoryStream();
        using var diagnostics = new StringWriter();
        var result = CliApplication.Run(["-", "-"], input, output, diagnostics);
        Assert.That(result, Is.Zero, diagnostics.ToString());
        Assert.That(output.ToArray(), Is.EqualTo(Pdf(html, new())));
    }
}
