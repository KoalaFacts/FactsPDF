using System.Text;
using FactsPDF.CommandLine;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>Print/all media blocks must execute the existing constrained PDF cascade, not merely parse.</summary>
[TestFixture]
public sealed class PrintMediaReportTests
{
    private static readonly PdfOptions SmallPage = new() { PageWidth = 310, PageHeight = 195, Margin = 18 };

    private static byte[] Pdf(string html, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, options ?? SmallPage);
        return output.ToArray();
    }

    private static void Reject(string css, string code)
    {
        using var output = new MemoryStream();
        output.Write("KEEP-APPROVED-PDF"u8);
        output.Position = 4;
        var failure = Assert.Throws<FactsPdfException>(() =>
            PdfConverter.Convert("<style>" + css + "</style><p>Do not change</p>", output, SmallPage));
        Assert.That(failure!.Code, Is.EqualTo(code));
        Assert.That(Encoding.ASCII.GetString(output.ToArray()), Is.EqualTo("KEEP-APPROVED-PDF"));
        Assert.That(output.Position, Is.EqualTo(4));
    }

    [TestCase("print")]
    [TestCase("all")]
    [TestCase("PRINT")]
    [TestCase("All")]
    public void MediaBlocksProduceRealPaintTextAndPageGeometry(string medium)
    {
        var source = "<style>p{color:blue}@media " + medium +
            " { section.report{width:180pt;padding:6pt;background:#e8f1f7;border:2pt solid #315f82}" +
            " section.report p{color:red;font-size:14pt;line-height:1.6;margin:0} }</style>" +
            "<section class=report><p>First service note</p><p>Second service note</p></section>";
        const string oracle = "<style>p{color:blue}</style>" +
            "<section style='width:180pt;padding:6pt;background:#e8f1f7;border:2pt solid #315f82'>" +
            "<p style='color:red;font-size:14pt;line-height:1.6;margin:0'>First service note</p>" +
            "<p style='color:red;font-size:14pt;line-height:1.6;margin:0'>Second service note</p></section>";
        Assert.That(Pdf(source), Is.EqualTo(Pdf(oracle)));
        var pages = TextLayout.Layout(HtmlDocumentReader.ReadTree(source, SmallPage, default), SmallPage, default);
        Assert.That(pages.SelectMany(p => p.PaintBoxes).Any(), Is.True);
        Assert.That(pages.SelectMany(p => p.Runs).Select(x => x.Text),
            Does.Contain("Second service note"));
    }

    [Test]
    public void SourceOrderSpecificityAndImportantRemainUnchangedAcrossMediaBoundaries()
    {
        const string media = "<style>p{color:blue}@media print{p{color:red}}p{color:green}</style><p>A</p>";
        const string winner = "<p style='color:green'>A</p>";
        Assert.That(Pdf(media), Is.EqualTo(Pdf(winner)));
        const string important = "<style>p{color:blue}@media print{p{color:red!important}}p{color:green}</style><p>A</p>";
        Assert.That(Pdf(important), Is.EqualTo(Pdf("<p style='color:red'>A</p>")));
    }

    [Test]
    public void DifferentMediaBlocksAndSucceedingStylesApplyToEarlierContent()
    {
        const string source = "<p>BEFORE</p><style>@media print { p { color:red } }</style>" +
            "<style>@media all { p {font-size:18pt} }</style><p>AFTER</p>";
        const string oracle = "<p style='color:red;font-size:18pt'>BEFORE</p>" +
            "<p style='color:red;font-size:18pt'>AFTER</p>";
        Assert.That(Pdf(source), Is.EqualTo(Pdf(oracle)));
    }

    [Test]
    public void CheckedInTwoPageServiceRegisterMatchesIndependentlyWrittenInlineHtml()
    {
        var root = FindRoot();
        var source = File.ReadAllText(Path.Combine(root, "examples", "report-print-media.html"));
        var oracle = File.ReadAllText(Path.Combine(root, "examples", "report-print-media-oracle.html"));
        var options = new PdfOptions();
        Assert.That(Pdf(source, options), Is.EqualTo(Pdf(oracle, options)));
        var pages = TextLayout.Layout(HtmlDocumentReader.ReadTree(source, options, default), options, default);
        Assert.That(pages.Count, Is.EqualTo(2));
        var rows = pages.SelectMany(p => p.Runs).Select(r => r.Text)
            .Where(t => t.StartsWith("MEDIA-", StringComparison.Ordinal)).ToArray();
        Assert.That(rows, Is.EqualTo(Enumerable.Range(1, 44)
            .Select(n => $"MEDIA-{n:00} inspected").ToArray()));
    }

    [Test]
    public void TrueTypeApiAndCliExecuteSameScopedPrintStyles()
    {
        var fontBytes = FontFixture.Create();
        var font = PdfFont.LoadTrueType(fontBytes);
        var options = SmallPage with { Fonts = [font], BoldFonts = [font] };
        const string html = "<style>@media print { .status{font-weight:bold;color:blue;font-size:15pt} }</style>" +
            "<p class=status>AB</p>";
        var expected = Pdf(html, options);
        var name = Path.Combine(Path.GetTempPath(), $"factspdf-media-{Guid.NewGuid():N}.ttf");
        try
        {
            File.WriteAllBytes(name, fontBytes);
            using var input = new MemoryStream(Encoding.UTF8.GetBytes(html));
            using var output = new MemoryStream();
            using var diagnostics = new StringWriter();
            Assert.That(CliApplication.Run(["-", "-", "--page-width", "310pt",
                "--page-height", "195pt", "--margin", "18pt",
                "--font", name, "--font-bold", name], input, output, diagnostics),
                Is.Zero, diagnostics.ToString());
            Assert.That(output.ToArray(), Is.EqualTo(expected));
        }
        finally { File.Delete(name); }
    }

    [TestCase("@media screen{p{color:red}}", "FPDF1204")]
    [TestCase("@media print,screen{p{color:red}}", "FPDF1204")]
    [TestCase("@media (min-width:300px){p{color:red}}", "FPDF1204")]
    [TestCase("@media print and (color){p{color:red}}", "FPDF1204")]
    [TestCase("@media print;", "FPDF1204")]
    [TestCase("@media print{ @media all{p{color:red}} }", "FPDF1204")]
    [TestCase("@media print{ @supports(display:grid){p{color:red}} }", "FPDF1204")]
    [TestCase("@media print{color:red; p{color:blue}}", "FPDF1204")]
    [TestCase("@supports(display:grid){p{color:red}}", "FPDF1204")]
    [TestCase("@import 'https://example.invalid/a.css';", "FPDF1204")]
    [TestCase("@media print{.unmatched{padding:-1pt}}", "FPDF1202")]
    [TestCase("@media all{.unmatched{display:flex}}", "FPDF1202")]
    public void UnsupportedMediaAndEvenUnmatchedInvalidDeclarationsAreRejected(string css, string code)
        => Reject(css, code);

    [Test]
    public void NestedMediaDeclarationsConsumeNormalSelectorAndDeclarationBudgets()
    {
        using var output = new MemoryStream();
        output.Write("KEEP"u8);
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(
            "<style>@media print{ p{color:red} .x{color:blue}}</style><p>A</p>",
            output, SmallPage with { MaxCssDeclarations = 1 }));
        Assert.That(error!.Code, Is.EqualTo("FPDF1205"));
        Assert.That(output.ToArray(), Is.EqualTo("KEEP"u8.ToArray()));
        Reject("@media print{p{color:potato}}", "FPDF1202");
        Assert.That(Pdf("<style>@media print{p{color:red}}</style><p>RECOVERED</p>"),
            Is.EqualTo(Pdf("<p style='color:red'>RECOVERED</p>")));
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "FactsPDF.slnx")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Repository root not found.");
    }
}
