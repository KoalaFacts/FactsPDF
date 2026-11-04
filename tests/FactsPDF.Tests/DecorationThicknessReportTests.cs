using System.Text;
using System.Text.RegularExpressions;
using FactsPDF.CommandLine;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>Bounded author-controlled PDF rule widths, not parse-only CSS.</summary>
[TestFixture]
public sealed class DecorationThicknessReportTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 220, PageHeight = 150, Margin = 10 };

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

    private static void Reject(string html, string code)
    {
        using var output = new MemoryStream();
        output.Write("existing-invoice"u8);
        output.Position = 3;
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, Page));
        Assert.That(error!.Code, Is.EqualTo(code));
        Assert.That(output.ToArray(), Is.EqualTo("existing-invoice"u8.ToArray()));
        Assert.That(output.Position, Is.EqualTo(3));
        Assert.That(Pdf("<p>Recovered AB</p>"), Is.Not.Empty);
    }

    [Test]
    public void AutoAndUnspecifiedThicknessRetainIdenticalLegacyPdfBytes()
    {
        const string normal = "<p style='font-size:10pt;text-decoration:underline dotted red'>AB</p>";
        const string automatic = "<p style='font-size:10pt;text-decoration:underline dotted red;text-decoration-thickness:auto'>AB</p>";
        Assert.That(Pdf(normal), Is.EqualTo(Pdf(automatic)));
        Assert.That(Layout(automatic).Single().Runs.Single().Style.DecorationThickness, Is.Null);
    }

    [TestCase("underline", "solid")]
    [TestCase("overline", "dashed")]
    [TestCase("line-through", "dotted")]
    public void CustomWidthChangesRealMeasuredPdfDecorationInEachPaintMode(string line, string pattern)
    {
        var source = $"<p style='font-size:10pt;text-decoration:{line} {pattern} red;text-decoration-thickness:1.5pt'>AB</p>";
        var run = Layout(source).Single().Runs.Single();
        Assert.That(run.Width, Is.EqualTo(12).Within(0.000001));
        Assert.That(run.Style.DecorationThickness, Is.EqualTo(1.5));
        var contents = Encoding.Latin1.GetString(Pdf(source));
        Assert.That(contents, Does.Contain(pattern == "solid" ? " 12 1.5 re f Q" : "1.5 w "));
        Assert.That(contents, Does.Contain("<4142> Tj ET"));
        Assert.That(Pdf(source), Is.Not.EqualTo(Pdf(source.Replace("1.5pt", "auto", StringComparison.Ordinal))));
    }

    [Test]
    public void RelativeWidthsResolveAgainstFinalFontAndRootAfterImportantCascade()
    {
        const string source = "<style>html{font-size:15pt}.label{font-size:20pt!important;text-decoration-thickness:0.1em}</style>" +
            "<p class=label style='font-size:9pt;text-decoration-line:underline'>AB</p>" +
            "<p style='font-size:8pt;text-decoration:overline;text-decoration-thickness:0.1rem'>AB</p>";
        const string oracle = "<style>html{font-size:15pt}</style>" +
            "<p style='font-size:20pt;text-decoration:underline;text-decoration-thickness:2pt'>AB</p>" +
            "<p style='font-size:8pt;text-decoration:overline;text-decoration-thickness:1.5pt'>AB</p>";
        Assert.That(Pdf(source), Is.EqualTo(Pdf(oracle)));
    }

    [Test]
    public void HtmlRootRemThicknessUsesFinalComputedRootRatherThanConfiguredInitial()
    {
        const string source = "<html style='font-size:20pt;text-decoration-thickness:0.1rem'><body>" +
            "<p style='text-decoration-line:underline'>AB</p></body></html>";
        const string expected = "<html style='font-size:20pt;text-decoration-thickness:2pt'><body>" +
            "<p style='text-decoration-line:underline'>AB</p></body></html>";
        Assert.That(Pdf(source), Is.EqualTo(Pdf(expected)));
        Reject("<html style='font-size:144pt;text-decoration-thickness:0.1rem'><body>" +
            "<p style='text-decoration-line:underline'>AB</p></body></html>", "FPDF1202");
    }

    [Test]
    public void CssWideResetInheritanceAndIndependentImportanceDoNotLoseWidth()
    {
        const string source = "<style>.ink{text-decoration-thickness:2pt!important}</style>" +
            "<p style='text-decoration:underline;text-decoration-thickness:1pt'>" +
            "<span class=ink style='text-decoration-thickness:0.5pt'>AB</span>" +
            "<span style='text-decoration-thickness:inherit'>AB</span>" +
            "<span style='text-decoration-thickness:unset'>AB</span></p>";
        const string oracle = "<p style='text-decoration:underline;text-decoration-thickness:1pt'>" +
            "<span style='text-decoration-thickness:2pt'>AB</span>" +
            "<span style='text-decoration-thickness:1pt'>AB</span>" +
            "<span style='text-decoration-thickness:auto'>AB</span></p>";
        Assert.That(Pdf(source), Is.EqualTo(Pdf(oracle)));
    }

    [Test]
    public void FourPartShorthandWidthMatchesExplicitLonghandAndResetsOmittedWidth()
    {
        const string source = "<p style='font-size:10pt;text-decoration:overline dotted blue 1.5pt'>AB</p>";
        const string oracle = "<p style='font-size:10pt;text-decoration:overline dotted blue;text-decoration-thickness:1.5pt'>AB</p>";
        Assert.That(Pdf(source), Is.EqualTo(Pdf(oracle)));
        Assert.That(Pdf("<p style='text-decoration-thickness:2pt;text-decoration:underline red'>AB</p>"),
            Is.EqualTo(Pdf("<p style='text-decoration:underline red;text-decoration-thickness:auto'>AB</p>")));
        const string important = "<style>.x{text-decoration-thickness:2pt!important}</style><p class=x style='text-decoration:underline red 1pt'>AB</p>";
        Assert.That(Pdf(important), Is.EqualTo(Pdf("<p style='text-decoration:underline red 2pt'>AB</p>")));
        Reject("<p style='text-decoration:underline 1pt 2pt'>AB</p>", "FPDF1202");
        Reject("<style>.unused{text-decoration:underline 50%}</style><p>AB</p>", "FPDF1202");
        Reject("<style>.unused{text-decoration:underline 0em}</style><p>AB</p>", "FPDF1202");
    }

    [Test]
    public void RunnableFortyFourEntryInvoiceMatchesIndependentPointUnitOracle()
    {
        var source = Example("report-decoration-thickness.html");
        var oracle = Example("report-decoration-thickness-oracle.html");
        Assert.That(Layout(source).Count, Is.GreaterThan(1));
        Assert.That(Pdf(source), Is.EqualTo(Pdf(oracle)));
    }

    [Test]
    public void TrueTypeTableTextAndPagedReportHaveActualWidthAndStableOracle()
    {
        var font = PdfFont.LoadTrueType(FontFixture.Create());
        var options = Page with { PageWidth = 138, PageHeight = 145, Margin = 8, Fonts = [font] };
        var before = "<table style='width:112pt'><colgroup><col style='width:112pt'></colgroup><tbody><tr><td>";
        var content = "<p style='font-size:10pt;text-decoration:line-through solid blue;text-decoration-thickness:1.1pt'>AB</p>";
        var table = before + content + "</td></tr></tbody></table>";
        var run = Layout(table, options).SelectMany(p => p.Runs).Single();
        Assert.That(ReferenceEquals(run.Font, font), Is.True);
        Assert.That(run.Style.DecorationThickness, Is.EqualTo(1.1).Within(0.000001));
        Assert.That(Encoding.Latin1.GetString(Pdf(table, options)), Does.Contain(" 1.1 re f Q"));

        var styled = "<style>.line{font-size:10pt;text-decoration:underline dashed #235577;text-decoration-thickness:1pt;margin:0}</style>";
        var control = "<style>.line{font-size:10pt;text-decoration:underline dashed #235577;margin:0}</style>";
        var rows = string.Concat(Enumerable.Repeat("<p class=line>AB AB AB</p>", 44));
        var oracleRows = string.Concat(Enumerable.Repeat("<p class=line style='text-decoration-thickness:1pt'>AB AB AB</p>", 44));
        var pages = Layout(styled + rows, options);
        Assert.That(pages.Count, Is.GreaterThan(1));
        Assert.That(Pdf(styled + rows, options), Is.EqualTo(Pdf(control + oracleRows, options)));
        Assert.That(Regex.Matches(Encoding.Latin1.GetString(Pdf(styled + rows, options)), @" re W n ").Count,
            Is.GreaterThan(40));
    }

    [TestCase("text-decoration-thickness:0", "FPDF1202")]
    [TestCase("text-decoration-thickness:0em", "FPDF1202")]
    [TestCase("text-decoration-thickness:0rem", "FPDF1202")]
    [TestCase("text-decoration-thickness:0.0001em", "FPDF1202")]
    [TestCase("text-decoration-thickness:13rem", "FPDF1202")]
    [TestCase("text-decoration-thickness:-1pt", "FPDF1202")]
    [TestCase("text-decoration-thickness:13pt", "FPDF1202")]
    [TestCase("text-decoration-thickness:50%", "FPDF1202")]
    [TestCase("text-decoration-thickness:1pt 2pt", "FPDF1202")]
    [TestCase("text-decoration-thickness:calc(1pt + 1pt)", "FPDF1202")]
    [TestCase("text-decoration-thickness:1e-999pt", "FPDF1202")]
    public void InvalidOrNonPaintableThicknessFailsEvenWhenSelectorDoesNotMatch(string declaration, string code)
        => Reject("<style>.absent{" + declaration + "}</style><p>AB</p>", code);

    [Test]
    public void OutOfRangeComputedEmRejectsAndProtectsOutput()
        => Reject("<p style='font-size:144pt;text-decoration:underline;text-decoration-thickness:0.2em'>AB</p>", "FPDF1202");

    [Test]
    public void StandaloneImageCannotClaimTextPaintWidth()
        => Reject("<img src='brand' style='text-decoration-thickness:2pt'>", "FPDF1201");

    private static string Example(string name)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FactsPDF.slnx")))
            directory = directory.Parent;
        Assert.That(directory, Is.Not.Null);
        return File.ReadAllText(Path.Combine(directory!.FullName, "examples", name));
    }

    [Test]
    public void CliProducesSameRealPdfAndDisplayCommandBudgetStillApplies()
    {
        const string html = "<p style='text-decoration:underline dotted red;text-decoration-thickness:2pt'>AB</p>";
        using var stdin = new MemoryStream(Encoding.UTF8.GetBytes(html));
        using var stdout = new MemoryStream();
        using var diagnostics = new StringWriter();
        Assert.That(CliApplication.Run(["-", "-"], stdin, stdout, diagnostics), Is.Zero, diagnostics.ToString());
        Assert.That(stdout.ToArray(), Is.EqualTo(Pdf(html, new())));
        using var tooSmall = new MemoryStream();
        var ex = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, tooSmall, Page with { MaxDisplayCommands = 1 }));
        Assert.That(ex!.Code, Is.EqualTo("FPDF1401"));
    }
}
