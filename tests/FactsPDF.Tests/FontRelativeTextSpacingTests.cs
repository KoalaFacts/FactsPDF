using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>Bounded em/rem text insets and advances must alter actual PDF geometry.</summary>
[TestFixture]
public sealed class FontRelativeTextSpacingTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 220, PageHeight = 250, Margin = 20 };

    private static List<LayoutPage> Layout(string html, PdfOptions? options = null)
    {
        var page = options ?? Page;
        return TextLayout.Layout(HtmlDocumentReader.ReadTree(html, page, default), page, default);
    }

    private static byte[] Pdf(string html, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, options ?? Page);
        return output.ToArray();
    }

    private static void Rejected(string html, string code, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        output.Write("previous-pdf"u8);
        output.Position = 4;
        var error = Assert.Throws<FactsPdfException>(() =>
            PdfConverter.Convert(html, output, options ?? Page));
        Assert.That(error!.Code, Is.EqualTo(code));
        Assert.That(output.ToArray(), Is.EqualTo("previous-pdf"u8.ToArray()));
        Assert.That(output.Position, Is.EqualTo(4));
    }

    [Test]
    public void RelativeValuesEqualIndependentPointValuesInRealPdf()
    {
        const string start = "<html style='font-size:14pt'><body><section style='font-size:12pt;width:140pt'>";
        const string end = "</section></body></html>";
        const string relative = "<p style='font-size:1.25em;text-indent:0.4em;letter-spacing:0.1em;word-spacing:0.15rem'>AB CD EF GH IJ KL</p>";
        const string points = "<p style='font-size:15pt;text-indent:6pt;letter-spacing:1.5pt;word-spacing:2.1pt'>AB CD EF GH IJ KL</p>";
        var actual = Layout(start + relative + end).Single().Runs;
        Assert.That(actual.Count, Is.GreaterThan(1), "Computed advances must really wrap within the 140pt report box.");
        Assert.That(actual[0].X, Is.EqualTo(26).Within(0.001));
        Assert.That(actual[1].X, Is.EqualTo(20).Within(0.001));
        Assert.That(Pdf(start + relative + end), Is.EqualTo(Pdf(start + points + end)));
        Assert.That(Pdf(start + relative + end), Is.Not.EqualTo(Pdf(start +
            "<p style='font-size:15pt'>AB CD EF GH IJ KL</p>" + end)));
        var pdf = Encoding.Latin1.GetString(Pdf(start + relative + end));
        Assert.That(pdf, Does.Contain("1.5 Tc"));
        Assert.That(pdf, Does.Contain(" TJ ET"));
    }

    [Test]
    public void EmUsesWinningFontSizeAndRemUsesComputedRootAfterCascade()
    {
        const string html = "<html style='font-size:16pt;letter-spacing:0.1rem;word-spacing:0.125rem'>" +
            "<body><style>p{font-size:18pt;letter-spacing:0.25em!important;word-spacing:0.2em}" +
            ".smaller{font-size:10pt}.reset{letter-spacing:inherit!important;word-spacing:inherit!important}</style>" +
            "<p class='smaller' style='letter-spacing:0.1rem'>AB CD</p>" +
            "<p class='reset' style='font-size:12pt'>EF GH</p></body></html>";
        var items = HtmlDocumentReader.Read(html, Page, default);
        Assert.That(items[0].Style.FontSize, Is.EqualTo(10));
        Assert.That(items[0].Style.LetterSpacing, Is.EqualTo(2.5).Within(0.001),
            "Important em must win over inline rem and use the final 10pt font.");
        Assert.That(items[0].Style.WordSpacing, Is.EqualTo(2).Within(0.001));
        Assert.That(items[1].Style.LetterSpacing, Is.EqualTo(1.6).Within(0.001));
        Assert.That(items[1].Style.WordSpacing, Is.EqualTo(2).Within(0.001));
        Assert.That(Pdf(html), Is.Not.Empty);
    }

    [Test]
    public void HtmlSelfRemSpacingUsesComputedRootAndInheritsFixedPoints()
    {
        const string relative = "<html style='font-size:16pt;letter-spacing:0.1rem;word-spacing:0.125rem'>" +
            "<body><p>AB CD</p></body></html>";
        const string points = "<html style='font-size:16pt;letter-spacing:1.6pt;word-spacing:2pt'>" +
            "<body><p>AB CD</p></body></html>";
        Assert.That(Pdf(relative), Is.EqualTo(Pdf(points)));
        Assert.That(HtmlDocumentReader.Read(relative, Page, default)[0].Style.LetterSpacing,
            Is.EqualTo(1.6).Within(0.001));
        Assert.That(Pdf("<p style='letter-spacing:0.25rem'>AB</p>"),
            Is.EqualTo(Pdf("<p style='letter-spacing:3pt'>AB</p>")),
            "Without html, rem uses the configured initial font.");
    }

    [Test]
    public void InlineTextUsesOwnFontAndRestoresInheritedSpacing()
    {
        const string relative = "<p style='font-size:10pt;letter-spacing:0.2em;word-spacing:0.1em'>" +
            "AB <span style='font-size:15pt;letter-spacing:0.2em;word-spacing:0.2em'>CD EF</span> GH</p>";
        const string points = "<p style='font-size:10pt;letter-spacing:2pt;word-spacing:1pt'>" +
            "AB <span style='font-size:15pt;letter-spacing:3pt;word-spacing:3pt'>CD EF</span> GH</p>";
        Assert.That(Pdf(relative), Is.EqualTo(Pdf(points)));
        var runs = Layout(relative).Single().Runs;
        Assert.That(runs.Count, Is.GreaterThanOrEqualTo(3));
        Assert.That(runs[0].Style.LetterSpacing, Is.EqualTo(2));
        Assert.That(runs[1].Style.LetterSpacing, Is.EqualTo(3));
        Assert.That(runs[^1].Style.LetterSpacing, Is.EqualTo(2));
    }

    [Test]
    public void TrueTypeChineseAndContinuedPagesKeepRelativeAdvanceAndAllGlyphs()
    {
        var font = PdfFont.LoadTrueType(FontFixture.Create());
        var options = Page with { Fonts = [font], PageHeight = 84, Margin = 10 };
        var relative = "<html style='font-size:12pt'><body><p style='font-size:12pt;" +
            "letter-spacing:0.1em;word-spacing:0.1rem;text-indent:0.5em'>" +
            string.Concat(Enumerable.Repeat("中文 AB ", 30)) + "</p></body></html>";
        var points = relative.Replace("letter-spacing:0.1em", "letter-spacing:1.2pt", StringComparison.Ordinal)
            .Replace("word-spacing:0.1rem", "word-spacing:1.2pt", StringComparison.Ordinal)
            .Replace("text-indent:0.5em", "text-indent:6pt", StringComparison.Ordinal);
        var pages = Layout(relative, options);
        Assert.That(pages.Count, Is.GreaterThan(1));
        var visible = string.Concat(pages.SelectMany(p => p.Runs).Select(r => r.Text));
        Assert.That(visible.Count(c => c == '中'), Is.EqualTo(30));
        Assert.That(visible.Count(c => c == '文'), Is.EqualTo(30));
        Assert.That(Pdf(relative, options), Is.EqualTo(Pdf(points, options)));
        Assert.That(Encoding.Latin1.GetString(Pdf(relative, options)), Does.Contain("/FontFile2"));
    }

    [Test]
    public void ZeroAndNormalPreserveLegacyPdfBytes()
    {
        var bytes = Pdf("<p>AB CD EF</p>");
        foreach (var css in new[] {
            "text-indent:0em;letter-spacing:0rem;word-spacing:0em",
            "text-indent:0rem;letter-spacing:normal;word-spacing:normal"
        })
            Assert.That(Pdf("<p style='" + css + "'>AB CD EF</p>"), Is.EqualTo(bytes), css);
    }

    [TestCase("letter-spacing:-0.1em", "FPDF1202")]
    [TestCase("letter-spacing:3em", "FPDF1202")]
    [TestCase("word-spacing:3rem", "FPDF1202")]
    [TestCase("word-spacing:-0.1rem", "FPDF1202")]
    [TestCase("word-spacing:10%", "FPDF1202")]
    [TestCase("text-indent:-0.2em", "FPDF1202")]
    [TestCase("text-indent:calc(1pt + 2pt)", "FPDF1202")]
    [TestCase("text-indent:60rem", "FPDF1302")]
    public void InvalidOrUnsafeInputDoesNotChangeExistingTarget(string css, string code)
        => Rejected("<html style='font-size:14pt'><body><p style='" + css +
            "'>Service summary</p></body></html>", code);

    [Test]
    public void UnmatchedInvalidRulesAndUnsupportedIndentElementsFail()
    {
        Rejected("<style>.missing{word-spacing:-1em}</style><p>Unchanged</p>", "FPDF1202");
        Rejected("<div style='text-indent:0.1rem'><p>Unchanged</p></div>", "FPDF1201");
        Rejected("<p><span style='text-indent:0.1em'>Unchanged</span></p>", "FPDF1201");
        Rejected("<table><colgroup><col style='width:90pt'></colgroup><tbody><tr><td>" +
            "<p style='text-indent:0.2em'>Unchanged</p></td></tr></tbody></table>", "FPDF1201");
    }
}
