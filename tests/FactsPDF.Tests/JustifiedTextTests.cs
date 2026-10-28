using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>Scoped left-to-right word-space justification on soft-wrapped report text.</summary>
[TestFixture]
public sealed class JustifiedTextTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 156, PageHeight = 260, Margin = 20 };

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

    private static void Reject(string html, string code)
    {
        using var output = new MemoryStream();
        output.Write("existing-report"u8);
        output.Position = 4;
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, Page));
        Assert.That(error!.Code, Is.EqualTo(code));
        Assert.That(output.ToArray(), Is.EqualTo("existing-report"u8.ToArray()));
        Assert.That(output.Position, Is.EqualTo(4));
    }

    private static IGrouping<double, PlacedText>[] Lines(List<LayoutPage> pages)
        => pages.SelectMany(page => page.Runs).GroupBy(run => run.Baseline).ToArray();

    [Test]
    public void SoftWrappedProseFillsTheRealContentWidthButTheLastLineStaysRagged()
    {
        const string text = "One two three four five six seven eight nine ten eleven";
        var left = Lines(Layout("<p>" + text + "</p>"));
        var justified = Lines(Layout("<p style='text-align:justify'>" + text + "</p>"));
        Assert.That(justified.Length, Is.GreaterThan(2));
        Assert.That(justified.Length, Is.EqualTo(left.Length));
        Assert.That(justified[0].Min(r => r.X), Is.EqualTo(20).Within(0.001));
        Assert.That(justified[0].Max(r => r.X + r.Width), Is.EqualTo(136).Within(0.001));
        Assert.That(left[0].Max(r => r.X + r.Width), Is.LessThan(135));
        Assert.That(justified[^1].Max(r => r.X + r.Width), Is.LessThan(135));
        var painted = Encoding.Latin1.GetString(Pdf("<p style='text-align:justify'>" + text + "</p>"));
        Assert.That(painted, Does.Contain(" TJ ET"), "The actual PDF must displace word spaces, not merely accept the CSS.");
        Assert.That(Pdf("<p style='text-align:justify'>" + text + "</p>"),
            Is.Not.EqualTo(Pdf("<p>" + text + "</p>")));
    }

    [Test]
    public void FirstLineIndentAndExistingWordSpacingUseMeasuredRemainingWidth()
    {
        const string text = "One two three four five six seven eight nine ten eleven";
        const string html = "<p style='text-indent:18pt;text-align:justify;word-spacing:1pt'>" + text + "</p>";
        var lines = Lines(Layout(html));
        Assert.That(lines.Length, Is.GreaterThan(2));
        Assert.That(lines[0].Min(r => r.X), Is.EqualTo(38).Within(0.001));
        Assert.That(lines[0].Max(r => r.X + r.Width), Is.EqualTo(136).Within(0.001));
        Assert.That(lines[1].Min(r => r.X), Is.EqualTo(20).Within(0.001));
        Assert.That(lines[1].Max(r => r.X + r.Width), Is.EqualTo(136).Within(0.001));
        Assert.That(Encoding.Latin1.GetString(Pdf(html)), Does.Contain(" TJ ET"));
    }

    [Test]
    public void ExplicitBreakAndFinalLineAreNotExpanded()
    {
        const string html = "<p style='text-align:justify'>Alpha beta<br>One two three four five six seven eight nine ten</p>";
        var lines = Lines(Layout(html));
        Assert.That(lines.Length, Is.GreaterThan(2));
        Assert.That(lines[0].Max(r => r.X + r.Width), Is.LessThan(130));
        Assert.That(lines[1].Max(r => r.X + r.Width), Is.EqualTo(136).Within(0.001));
        Assert.That(lines[^1].Max(r => r.X + r.Width), Is.LessThan(136));
    }

    [Test]
    public void FittingAndNowrapTextRemainByteIdenticalToLeftAlignment()
    {
        const string text = "Paid in full";
        Assert.That(Pdf("<p style='text-align:justify'>" + text + "</p>"),
            Is.EqualTo(Pdf("<p>" + text + "</p>")));
        Assert.That(Pdf("<p style='text-align:justify;white-space:nowrap'>" + text + "</p>"),
            Is.EqualTo(Pdf("<p style='white-space:nowrap'>" + text + "</p>")));
    }

    [Test]
    public void CascadeInheritanceAndWideKeywordsControlTheParagraphNotIndividualSpans()
    {
        const string report = "One two three four five six seven eight nine ten eleven";
        var html = "<style>section{text-align:justify}p{text-align:right}.prose{text-align:justify!important}" +
            ".reset{text-align:initial!important}.unset{text-align:unset!important}</style>" +
            "<section><p class='prose'>" + report + "</p>" +
            "<p class='reset'>Reset</p><p class='unset'>Inherited</p></section>";
        var paragraphs = HtmlDocumentReader.Read(html, Page, default);
        Assert.That(paragraphs.Select(p => p.Style.Alignment),
            Is.EqualTo(new[] { TextAlignment.Justify, TextAlignment.Left, TextAlignment.Justify }));
        Assert.That(Lines(Layout(html))[0].Max(r => r.X + r.Width),
            Is.EqualTo(136).Within(0.001));
    }

    [Test]
    public void InlineColoursAndCidTrueTypeRetainTextAndRealPdfSpacing()
    {
        var font = PdfFont.LoadTrueType(FontFixture.Create());
        var options = Page with { Fonts = [font] };
        const string plain = "<p style='text-align:justify'><span style='color:red'>One two </span>" +
            "<span style='color:blue'>three four five six seven eight nine ten eleven</span></p>";
        var layout = Layout(plain, options);
        Assert.That(Lines(layout).Length, Is.GreaterThan(1));
        Assert.That(Lines(layout)[0].Max(r => r.X + r.Width), Is.EqualTo(136).Within(0.001));
        Assert.That(layout.SelectMany(page => page.Runs).Any(run => run.Style.Color.R == 1), Is.True);
        Assert.That(layout.SelectMany(page => page.Runs).Any(run => run.Style.Color.B == 1), Is.True);
        var bytes = Encoding.Latin1.GetString(Pdf(plain, options));
        Assert.That(bytes, Does.Contain("/FontFile2"));
        Assert.That(bytes, Does.Contain(" TJ ET"));
    }

    [Test]
    public void JustificationContinuesAcrossActualPageBoundaries()
    {
        var small = Page with { PageHeight = 72 };
        const string text = "One two three four five six seven eight nine ten eleven twelve thirteen fourteen fifteen sixteen";
        var pages = Layout("<p style='text-align:justify'>" + text + "</p>", small);
        Assert.That(pages.Count, Is.GreaterThan(1));
        Assert.That(pages.SelectMany(p => p.Runs).Any(r => r.Style.WordSpacing > 0), Is.True);
        Assert.That(Pdf("<p style='text-align:justify'>" + text + "</p>", small), Is.Not.Empty);
    }

    [TestCase("text-align:start", "FPDF1202")]
    [TestCase("text-align:end", "FPDF1202")]
    [TestCase("text-align:justify-all", "FPDF1202")]
    [TestCase("text-align:match-parent", "FPDF1202")]
    [TestCase("text-align:justify left", "FPDF1202")]
    public void UnsupportedDirectionsAndValuesRejectWithoutReplacingExistingPdf(string css, string code)
        => Reject("<p style='" + css + "'>One two three</p>", code);

    [Test]
    public void InlineJustifyDoesNotSilentlyAffectAnAncestorParagraph()
        => Reject("<p>Start <span style='text-align:justify'>end</span></p>", "FPDF1201");

    [Test]
    public void ExistingNowrapOverflowStillFailsAndKeepsDestination()
        => Reject("<p style='text-align:justify;white-space:nowrap;width:50pt'>One two three four five</p>", "FPDF1302");
}
