using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>Root rem box lengths must resolve to actual printable PDF geometry, not just parse.</summary>
[TestFixture]
public sealed class RootRelativeBoxLengthTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 155, Margin = 20 };

    private static byte[] Pdf(string html, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, options ?? Page);
        return output.ToArray();
    }

    private static List<LayoutPage> Layout(string html, PdfOptions? options = null)
    {
        var settings = options ?? Page;
        return TextLayout.Layout(HtmlDocumentReader.ReadTree(html, settings, default), settings, default);
    }

    private static void Reject(string html, string code)
    {
        using var output = new MemoryStream();
        output.Write("existing-report"u8);
        output.Position = 3;
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, Page));
        Assert.That(error!.Code, Is.EqualTo(code));
        Assert.That(output.ToArray(), Is.EqualTo("existing-report"u8.ToArray()));
        Assert.That(output.Position, Is.EqualTo(3));
    }

    [Test]
    public void HtmlRootNotBodySizeControlsBlockWidthAndAllFourPaddingEdges()
    {
        const string relative = "<html style='font-size:15pt'><body style='font-size:9pt'>" +
            "<section style='width:8rem;padding:0.5rem;border:1pt solid blue;background:#f0f6fa'>AB WA</section></body></html>";
        const string absolute = "<html style='font-size:15pt'><body style='font-size:9pt'>" +
            "<section style='width:120pt;padding:7.5pt;border:1pt solid blue;background:#f0f6fa'>AB WA</section></body></html>";
        Assert.That(Pdf(relative), Is.EqualTo(Pdf(absolute)));
        var page = Layout(relative).Single();
        var paint = page.PaintBoxes.Single(p => p.Style.BackgroundColor.HasValue);
        Assert.That(paint.X, Is.EqualTo(20).Within(0.00001));
        Assert.That(paint.Width, Is.EqualTo(137).Within(0.00001));
        Assert.That(page.Runs.Single().X, Is.EqualTo(28.5).Within(0.00001));
    }

    [Test]
    public void RemMinMaxWidthsAndAutoMarginsUseTheOriginalContainingBlock()
    {
        const string head = "<html style='font-size:20pt'><body>";
        const string relative = "<p style='box-sizing:border-box;width:12rem;max-width:6rem;" +
            "padding:0.5rem;border:1pt solid blue;margin:0 auto;background:red'>SUMMARY</p>";
        const string absolute = "<p style='box-sizing:border-box;width:120pt;padding:10pt;" +
            "border:1pt solid blue;margin:0 auto;background:red'>SUMMARY</p>";
        Assert.That(Pdf(head + relative + "</body></html>"),
            Is.EqualTo(Pdf(head + absolute + "</body></html>")));
        var paint = Layout(head + relative + "</body></html>").Single()
            .PaintBoxes.Single(p => p.Style.BackgroundColor.HasValue);
        Assert.That(paint.X, Is.EqualTo(90).Within(0.00001));
        Assert.That(paint.Width, Is.EqualTo(120).Within(0.00001));

        const string minWins = "<p style='box-sizing:border-box;width:4rem;min-width:6rem;max-width:5rem;" +
            "padding:0.5rem;border:1pt solid blue;margin:0 auto;background:red'>SUMMARY</p>";
        Assert.That(Pdf(head + minWins + "</body></html>"),
            Is.EqualTo(Pdf(head + absolute + "</body></html>")));
    }

    [Test]
    public void ExplicitInheritAndImportantUseResolvedRootValuesWithoutImplicitInheritance()
    {
        const string relative = "<html style='font-size:15pt'><style>" +
            "section{width:6rem}p.a{width:inherit!important}p.b{width:5rem!important}" +
            "</style><body><section>" +
            "<p class=a style='width:8rem'>INHERITED</p>" +
            "<p class=b style='width:8rem'>OVERRIDE</p>" +
            "<p style='padding:0.5rem'>PADDED</p></section></body></html>";
        const string absolute = "<html style='font-size:15pt'><style>" +
            "section{width:90pt}p.a{width:inherit!important}p.b{width:75pt!important}" +
            "</style><body><section>" +
            "<p class=a style='width:120pt'>INHERITED</p>" +
            "<p class=b style='width:120pt'>OVERRIDE</p>" +
            "<p style='padding:7.5pt'>PADDED</p></section></body></html>";
        Assert.That(Pdf(relative), Is.EqualTo(Pdf(absolute)));
    }

    [Test]
    public void RemWidthDoesNotSilentlyClipAnUnbreakableTextSegment()
    {
        // At a 15pt Courier root, this nine-letter word is 81pt wide;
        // 5rem gives a real 75pt text box. Reject rather than truncate.
        Reject("<html style='font-size:15pt'><body>" +
            "<p style='width:5rem'>IMPORTANT</p></body></html>", "FPDF1302");
    }

    [Test]
    public void ConfiguredInitialRootAndRootSelfReferenceControlRealBoxLengths()
    {
        const string direct = "<body style='font-size:20pt'><p style='width:8rem;padding:0.5rem'>AB</p></body>";
        const string points = "<body style='font-size:20pt'><p style='width:96pt;padding:6pt'>AB</p></body>";
        Assert.That(Pdf(direct), Is.EqualTo(Pdf(points)));
        const string self = "<html style='font-size:2rem'><body><p style='width:8rem;padding:0.5rem'>AB</p></body></html>";
        const string selfPoints = "<html style='font-size:24pt'><body><p style='width:192pt;padding:12pt'>AB</p></body></html>";
        Assert.That(Pdf(self), Is.EqualTo(Pdf(selfPoints)));
        var configured = Page with { FontSize = 16 };
        Assert.That(Pdf(direct, configured),
            Is.EqualTo(Pdf("<body style='font-size:20pt'><p style='width:128pt;padding:8pt'>AB</p></body>", configured)));
    }

    [Test]
    public void ActualPaginatedBackgroundBorderAndSearchableTextMatchPointProjection()
    {
        var lines = string.Join("<br>", Enumerable.Range(1, 9).Select(n => $"Service line {n:00}"));
        var before = "<html style='font-size:15pt'><body>";
        var relative = "<section style='box-sizing:border-box;width:12rem;padding:0.3rem;" +
            "border:1pt solid blue;background:#f0f6fa'><p style='margin:0'>" + lines + "</p></section>";
        var absolute = "<section style='box-sizing:border-box;width:180pt;padding:4.5pt;" +
            "border:1pt solid blue;background:#f0f6fa'><p style='margin:0'>" + lines + "</p></section>";
        var html = before + relative + "</body></html>";
        Assert.That(Pdf(html), Is.EqualTo(Pdf(before + absolute + "</body></html>")));
        var pages = Layout(html);
        Assert.That(pages.Count, Is.GreaterThan(1));
        Assert.That(pages.SelectMany(x => x.Runs).Select(x => x.Text),
            Is.EqualTo(Enumerable.Range(1, 9).Select(n => $"Service line {n:00}")));
        Assert.That(pages.SelectMany(x => x.PaintBoxes)
            .Where(x => x.Style.BackgroundColor.HasValue)
            .All(x => Math.Abs(x.Width - 180) < 0.00001), Is.True);
    }

    [Test]
    public void DefaultWidthsAndExplicitZeroRemKeepPreviousPdfBytes()
    {
        const string normal = "<p style='width:120pt;padding:0'>AB</p>";
        const string explicitZero = "<p style='width:120pt;padding:0rem'>AB</p>";
        Assert.That(Pdf(normal), Is.EqualTo(Pdf(explicitZero)));
        Assert.That(Pdf("<p>AB</p>"), Is.EqualTo(Pdf("<p style='padding:0rem'>AB</p>")));
    }

    [TestCase("<p style='width:-1rem'>AB</p>", "FPDF1202")]
    [TestCase("<p style='padding:-1rem'>AB</p>", "FPDF1202")]
    [TestCase("<p style='width:20000rem'>AB</p>", "FPDF1202")]
    [TestCase("<html style='font-size:144pt'><body><p style='width:101rem'>AB</p></body></html>", "FPDF1202")]
    [TestCase("<p style='width:1em'>AB</p>", "FPDF1202")]
    [TestCase("<p style='margin-left:1rem'>AB</p>", "FPDF1202")]
    [TestCase("<p style='border-width:1rem'>AB</p>", "FPDF1202")]
    [TestCase("<p style='padding:calc(1rem + 1pt)'>AB</p>", "FPDF1202")]
    [TestCase("<p style='box-sizing:border-box;width:1rem;padding:1rem'>AB</p>", "FPDF1302")]
    [TestCase("<style>.never{width:-2rem}</style><p>AB</p>", "FPDF1202")]
    [TestCase("<style>.never{padding:20000rem}</style><p>AB</p>", "FPDF1202")]
    [TestCase("<img src='logo' style='width:1rem'>", "FPDF1201")]
    [TestCase("<img src='logo' style='padding:1rem'>", "FPDF1201")]
    [TestCase("<table style='padding:1rem'>", "FPDF1201")]
    [TestCase("<table><colgroup><col style='width:1rem'></colgroup><tbody><tr><td>AB</td></tr></tbody></table>", "FPDF1201")]
    public void InvalidOrOutOfScopeRemBoxesPreserveExistingTarget(string html, string error)
        => Reject(html, error);
}
