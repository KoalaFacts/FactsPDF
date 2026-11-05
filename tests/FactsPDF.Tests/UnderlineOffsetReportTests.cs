using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using FactsPDF.CommandLine;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>Bounded text-underline-offset must change actual PDF geometry.</summary>
[TestFixture]
public sealed class UnderlineOffsetReportTests
{
    private static readonly PdfOptions SmallPage = new() { PageWidth = 220, PageHeight = 150, Margin = 10 };

    private static byte[] Pdf(string html, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, options ?? SmallPage);
        return output.ToArray();
    }

    private static List<LayoutPage> Layout(string html, PdfOptions? options = null)
    {
        var settings = options ?? SmallPage;
        return TextLayout.Layout(HtmlDocumentReader.ReadTree(html, settings, default), settings, default);
    }

    private static void Reject(string html, string expectedCode)
    {
        using var output = new MemoryStream();
        output.Write("saved-quotation"u8);
        output.Position = 4;
        var error = Assert.Throws<FactsPdfException>(
            () => PdfConverter.Convert(html, output, SmallPage));
        Assert.That(error!.Code, Is.EqualTo(expectedCode));
        Assert.That(output.ToArray(), Is.EqualTo("saved-quotation"u8.ToArray()));
        Assert.That(output.Position, Is.EqualTo(4));
        Assert.That(Pdf("<p>Recovered AB</p>"), Is.Not.Empty);
    }

    [Test]
    public void AutoRetainsUnmodifiedLegacyBytesAndNonUnderlineLines()
    {
        const string original = "<p style='font-size:10pt;text-decoration:underline dashed blue'>AB</p>";
        const string automatic = "<p style='font-size:10pt;text-decoration:underline dashed blue;text-underline-offset:auto'>AB</p>";
        Assert.That(Pdf(automatic), Is.EqualTo(Pdf(original)));
        Assert.That(Layout(automatic).Single().Runs.Single().Style.UnderlineOffset, Is.Null);
        const string strike = "<p style='font-size:10pt;text-decoration:line-through dotted red'>AB</p>";
        const string overline = "<p style='font-size:10pt;text-decoration:overline dotted red'>AB</p>";
        Assert.That(Pdf(overline.Replace("'>AB", ";text-underline-offset:4pt'>AB", StringComparison.Ordinal)),
            Is.EqualTo(Pdf(overline)));
        Assert.That(Pdf(strike.Replace("'>AB", ";text-underline-offset:4pt'>AB", StringComparison.Ordinal)),
            Is.EqualTo(Pdf(strike)));
    }

    [Test]
    public void ExplicitOffsetMovesRealFilledAndDashedPdfDecorationBelowBaseline()
    {
        foreach (var stroke in new[] { "solid", "dashed", "dotted" })
        {
            var html = $"<p style='font-size:10pt;text-decoration:underline {stroke} blue;text-underline-offset:2pt'>AB</p>";
            var run = Layout(html).Single().Runs.Single();
            Assert.That(run.Style.UnderlineOffset, Is.EqualTo(2));
            Assert.That(run.Width, Is.GreaterThan(0));
            var content = Encoding.Latin1.GetString(Pdf(html));
            var path = Regex.Match(content, stroke == "solid"
                ? @" rg (?<x>-?[0-9.]+) (?<y>-?[0-9.]+) (?<w>[0-9.]+) (?<t>[0-9.]+) re f Q"
                : @" re W n (?<x>-?[0-9.]+) (?<y>-?[0-9.]+) m ");
            Assert.That(path.Success, Is.True, stroke);
            var x = double.Parse(path.Groups["x"].Value, CultureInfo.InvariantCulture);
            var y = double.Parse(path.Groups["y"].Value, CultureInfo.InvariantCulture);
            Assert.That(x, Is.EqualTo(run.X).Within(0.01), stroke);
            Assert.That(y, Is.EqualTo(stroke == "solid" ? run.Baseline - 2 - 0.3 : run.Baseline - 2)
                .Within(0.01), stroke);
            Assert.That(Pdf(html), Is.Not.EqualTo(Pdf(html.Replace("2pt", "auto", StringComparison.Ordinal))));
        }
    }

    [Test]
    public void FinalFontAndRootResolveEmRemAfterImportantCascade()
    {
        const string source = "<html><head><style>html{font-size:15pt}.a{font-size:20pt!important;text-underline-offset:0.1em!important}</style></head><body>" +
            "<p class=a style='font-size:9pt;text-decoration:underline;text-underline-offset:1pt'>AB</p>" +
            "<p style='font-size:8pt;text-decoration:underline;text-underline-offset:0.2rem'>AB</p></body></html>";
        const string oracle = "<html><head><style>html{font-size:15pt}.a{font-size:20pt!important;text-underline-offset:2pt!important}</style></head><body>" +
            "<p class=a style='font-size:9pt;text-decoration:underline;text-underline-offset:1pt'>AB</p>" +
            "<p style='font-size:8pt;text-decoration:underline;text-underline-offset:3pt'>AB</p></body></html>";
        Assert.That(Pdf(source), Is.EqualTo(Pdf(oracle)));
    }

    [Test]
    public void RootDeclarationUsesItsOwnFinalFontForRem()
    {
        const string source = "<html style='font-size:20pt;text-underline-offset:0.1rem'><body><p style='text-decoration:underline'>AB</p></body></html>";
        const string oracle = "<html style='font-size:20pt;text-underline-offset:2pt'><body><p style='text-decoration:underline'>AB</p></body></html>";
        Assert.That(Pdf(source), Is.EqualTo(Pdf(oracle)));
    }

    [Test]
    public void InheritUnsetAndInitialUseIndependentCascadeWinners()
    {
        const string source = "<p style='text-decoration:underline;text-underline-offset:2pt'>A" +
            "<span style='text-underline-offset:inherit'>B</span>" +
            "<span style='text-underline-offset:unset'>A</span>" +
            "<span style='text-underline-offset:initial'>B</span></p>";
        const string control = "<p style='text-decoration:underline;text-underline-offset:2pt'>A" +
            "<span style='text-underline-offset:2pt'>B</span>" +
            "<span style='text-underline-offset:2pt'>A</span>" +
            "<span style='text-underline-offset:auto'>B</span></p>";
        Assert.That(Pdf(source), Is.EqualTo(Pdf(control)));
    }

    [Test]
    public void MultiPageQuotationMatchesSeparatePointValueControlAndTrueType()
    {
        var html = Example("report-underline-offset.html");
        var oracle = Example("report-underline-offset-oracle.html");
        var settings = new PdfOptions();
        Assert.That(Layout(html, settings).Count, Is.GreaterThan(1));
        Assert.That(Pdf(html, settings), Is.EqualTo(Pdf(oracle, settings)));

        var font = PdfFont.LoadTrueType(FontFixture.Create());
        var options = SmallPage with { Fonts = [font] };
        var small = "<p style='font-size:10pt;text-decoration:underline;text-underline-offset:1pt'>AB</p>";
        var run = Layout(small, options).Single().Runs.Single();
        Assert.That(ReferenceEquals(run.Font, font), Is.True);
        Assert.That(run.Style.UnderlineOffset, Is.EqualTo(1));
        Assert.That(Pdf(small, options), Is.Not.EqualTo(Pdf(small.Replace("1pt", "auto", StringComparison.Ordinal), options)));
    }

    [TestCase("text-underline-offset:-1pt", "FPDF1202")]
    [TestCase("text-underline-offset:25pt", "FPDF1202")]
    [TestCase("text-underline-offset:40%", "FPDF1202")]
    [TestCase("text-underline-offset:calc(1pt + 1pt)", "FPDF1202")]
    [TestCase("text-underline-offset:2pt 3pt", "FPDF1202")]
    [TestCase("text-underline-offset:1e-999pt", "FPDF1202")]
    public void InvalidValuesFailEvenInNonmatchingRulesWithoutClobberingOutput(string declaration, string code)
        => Reject("<style>.unused{" + declaration + "}</style><p>AB</p>", code);

    [Test]
    public void ComputedOutOfRangeOffsetAndStandaloneImageFailWithoutClobberingOutput()
    {
        Reject("<p style='font-size:144pt;text-decoration:underline;text-underline-offset:0.2em'>AB</p>", "FPDF1202");
        Reject("<img src='brand' style='text-underline-offset:1pt'>", "FPDF1201");
    }

    [Test]
    public void RealCliMatchesApiAndDisplayBudgetStillApplies()
    {
        const string html = "<p style='font-size:10pt;text-decoration:underline dotted red;text-underline-offset:2pt'>AB</p>";
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(html));
        using var output = new MemoryStream();
        using var diagnostics = new StringWriter();
        Assert.That(CliApplication.Run(["-", "-"], input, output, diagnostics), Is.Zero, diagnostics.ToString());
        Assert.That(output.ToArray(), Is.EqualTo(Pdf(html, new PdfOptions())));
        using var limited = new MemoryStream();
        var ex = Assert.Throws<FactsPdfException>(() =>
            PdfConverter.Convert(html, limited, SmallPage with { MaxDisplayCommands = 1 }));
        Assert.That(ex!.Code, Is.EqualTo("FPDF1401"));
    }

    private static string Example(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "FactsPDF.slnx")))
            dir = dir.Parent;
        Assert.That(dir, Is.Not.Null);
        return File.ReadAllText(Path.Combine(dir!.FullName, "examples", name));
    }
}
