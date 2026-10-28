using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>Actual PDF word gaps must match measured U+0020 advances.</summary>
[TestFixture]
public sealed class WordSpacingTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 180, PageHeight = 240, Margin = 20 };

    private static List<LayoutPage> Layout(string html, PdfOptions? options = null)
    {
        var o = options ?? Page;
        return TextLayout.Layout(HtmlDocumentReader.ReadTree(html, o, default), o, default);
    }

    private static byte[] Pdf(string html, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, options ?? Page);
        return output.ToArray();
    }

    private static void Reject(string html, string expected, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        output.Write("keep-original"u8);
        output.Position = 4;
        var exception = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, options ?? Page));
        Assert.That(exception!.Code, Is.EqualTo(expected));
        Assert.That(output.ToArray(), Is.EqualTo("keep-original"u8.ToArray()));
        Assert.That(output.Position, Is.EqualTo(4));
    }

    [Test]
    public void NormalAndZeroPreserveLegacyPdfBytes()
    {
        const string sample = "Monthly <span>service and</span> safety report";
        var old = Pdf("<p>" + sample + "</p>");
        foreach (var gap in new[] { "normal", "0", "0pt", "0px", "0mm" })
            Assert.That(Pdf("<p style='word-spacing:" + gap + "'>" + sample + "</p>"),
                Is.EqualTo(old), gap);
    }

    [Test]
    public void RealCourierAdvanceMatchesPdfTjAndPrintLengthProjection()
    {
        const string content = "AB CD";
        var normal = Layout("<p>" + content + "</p>").Single().Runs.Single();
        var spaced = Layout("<p style='word-spacing:2pt'>" + content + "</p>").Single().Runs.Single();
        Assert.That(normal.Width, Is.EqualTo(36).Within(0.001));
        Assert.That(spaced.Width, Is.EqualTo(38).Within(0.001));
        var pdf = Encoding.Latin1.GetString(Pdf("<p style='word-spacing:2pt'>" + content + "</p>"));
        Assert.That(pdf, Does.Contain("-166.667"));
        Assert.That(pdf, Does.Contain(" TJ ET"));
        Assert.That(pdf, Does.Not.Contain(" Tw "));
        Assert.That(Pdf("<p style='word-spacing:0.1in'>" + content + "</p>"),
            Is.EqualTo(Pdf("<p style='word-spacing:7.2pt'>" + content + "</p>")));
    }

    [Test]
    public void WordGapChangesLineBreakingAndNowrapRejectsOverflow()
    {
        var narrow = Page with { PageWidth = 110, Margin = 20 };
        const string words = "AA BB CC";
        Assert.That(Layout("<p>" + words + "</p>", narrow).Single().Runs, Has.Count.EqualTo(1));
        var wrapped = Layout("<p style='word-spacing:8pt'>" + words + "</p>", narrow).Single().Runs;
        Assert.That(wrapped.Count, Is.GreaterThan(1));
        Assert.That(Pdf("<p style='word-spacing:8pt'>" + words + "</p>", narrow), Is.Not.Empty);
        Reject("<p style='white-space:nowrap;word-spacing:8pt'>" + words + "</p>", "FPDF1302", narrow);
    }

    [Test]
    public void InheritanceCssCascadeAndWideKeywordsComputeRealStyles()
    {
        const string html = "<style>p{word-spacing:2pt!important}.loose{word-spacing:7pt}" +
            ".reset{word-spacing:initial!important}</style>" +
            "<p class=loose>AA BB</p><p class=reset>CC DD</p>" +
            "<div style='word-spacing:3pt'><p>EE<span> FF GG</span></p></div>";
        var paragraphs = HtmlDocumentReader.Read(html, Page, default);
        Assert.That(paragraphs.Select(x => x.Style.WordSpacing), Is.EqualTo(new[] { 2d, 0d, 2d }));
        Assert.That(paragraphs.Last().Runs.Last().Style.WordSpacing, Is.EqualTo(2));
        Assert.That(Pdf(html), Is.Not.Empty);
    }

    [Test]
    public void LetterAndWordSpacingCoexistInPdfPaintAndRunMetrics()
    {
        const string html = "<p style='letter-spacing:1pt;word-spacing:2pt'>AB CD</p>";
        var run = Layout(html).Single().Runs.Single();
        Assert.That(run.Width, Is.EqualTo(43).Within(0.001));
        var pdf = Encoding.Latin1.GetString(Pdf(html));
        Assert.That(pdf, Does.Contain("1 Tc"));
        Assert.That(pdf, Does.Contain(" TJ ET"));
        Assert.That(pdf, Does.Contain("-166.667"));
    }

    [Test]
    public void TrueTypeCidSpaceUsesTjAndMatchesMeasuredAdvance()
    {
        var font = PdfFont.LoadTrueType(FontFixture.Create());
        var o = Page with { Fonts = [font] };
        const string html = "<p style='word-spacing:3pt'>AB AB</p>";
        var normal = Layout("<p>AB AB</p>", o).Single().Runs.Single();
        var spaced = Layout(html, o).Single().Runs.Single();
        Assert.That(spaced.Width - normal.Width, Is.EqualTo(3).Within(0.001));
        var pdf = Encoding.Latin1.GetString(Pdf(html, o));
        Assert.That(pdf, Does.Contain("/FontFile2"));
        Assert.That(pdf, Does.Contain(" TJ ET"));
        Assert.That(pdf, Does.Not.Contain(" Tw "));
    }

    [TestCase("word-spacing:-1pt")]
    [TestCase("word-spacing:24.01pt")]
    [TestCase("word-spacing:1em")]
    [TestCase("word-spacing:12%")]
    [TestCase("word-spacing:calc(1pt + 1pt)")]
    [TestCase("word-spacing:2pt 3pt")]
    public void UnsupportedValuesRejectAndKeepDestination(string css)
        => Reject("<p style='" + css + "'>A B</p>", "FPDF1202");

    [Test]
    public void UnmatchedBadStylesheetStillRejected()
        => Reject("<style>.missing{word-spacing:-1pt}</style><p>Actual report</p>", "FPDF1202");
}
