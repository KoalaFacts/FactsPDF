using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using FactsPDF.CommandLine;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>Independent PDF ink and pagination evidence for scoped multi-line report decorations.</summary>
[TestFixture]
public sealed class CombinedTextDecorationReportTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 240, PageHeight = 148, Margin = 10 };

    private static byte[] Pdf(string html, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, options ?? Page);
        return output.ToArray();
    }

    private static List<LayoutPage> Layout(string html, PdfOptions? options = null)
    {
        var o = options ?? Page;
        return TextLayout.Layout(HtmlDocumentReader.ReadTree(html, o, default), o, default);
    }

    private static void Reject(string html, string code)
    {
        using var output = new MemoryStream();
        output.Write("previous-invoice"u8);
        output.Position = 4;
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, Page));
        Assert.That(error!.Code, Is.EqualTo(code));
        Assert.That(output.ToArray(), Is.EqualTo("previous-invoice"u8.ToArray()));
        Assert.That(output.Position, Is.EqualTo(4));
        Assert.That(Pdf("<p>Recovered invoice</p>"), Is.Not.Empty);
    }

    [Test]
    public void AllThreeLinesPaintThreeMeasuredRulesAroundOneSearchableGlyphRun()
    {
        const string html = "<p style='margin:0;font-size:10pt;text-decoration:underline line-through overline solid #334455 1pt'>AB</p>";
        var run = Layout(html).Single().Runs.Single();
        Assert.That(run.Text, Is.EqualTo("AB"));
        Assert.That(run.Width, Is.EqualTo(12).Within(0.000001));
        Assert.That(TextDecorationCommands.Count(run.Style.Decoration), Is.EqualTo(3));
        var text = Encoding.Latin1.GetString(Pdf(html));
        Assert.That(text, Does.Contain("<4142> Tj ET"));
        var boxes = Regex.Matches(text, @"rg (?<x>-?[0-9.]+) (?<y>-?[0-9.]+) (?<w>[0-9.]+) (?<th>[0-9.]+) re f Q");
        Assert.That(boxes.Count, Is.EqualTo(3));
        var expected = new[] { run.Baseline - 1.2 - .5, run.Baseline + 2.9 - .5, run.Baseline + 7.8 - .5 };
        for (var i = 0; i < boxes.Count; i++)
        {
            double Value(string group) => double.Parse(boxes[i].Groups[group].Value, CultureInfo.InvariantCulture);
            Assert.That(Value("x"), Is.EqualTo(run.X).Within(0.001));
            Assert.That(Value("y"), Is.EqualTo(expected[i]).Within(0.002));
            Assert.That(Value("w"), Is.EqualTo(run.Width).Within(0.001));
            Assert.That(Value("th"), Is.EqualTo(1).Within(0.001));
        }
    }

    [Test]
    public void CombinationsOfLineLonghandAndShorthandMatchRealPdfAcrossPages()
    {
        var entries = string.Concat(Enumerable.Range(1, 44).Select(i =>
            $"<p class='changed' style='margin:0'>Invoice item {i:00}: revised AUD 120</p>"));
        const string sheet = "<style>.changed{font-size:10pt;text-decoration:overline underline line-through dashed #285d91 1pt}</style>";
        const string control = "<style>.changed{font-size:10pt;text-decoration-line:underline line-through overline;" +
            "text-decoration-style:dashed;text-decoration-color:#285d91;text-decoration-thickness:1pt}</style>";
        var source = sheet + entries;
        Assert.That(Layout(source).Count, Is.GreaterThan(2));
        Assert.That(Pdf(source), Is.EqualTo(Pdf(control + entries)));
        Assert.That(Layout(source).SelectMany(p => p.Runs)
            .Count(r => r.Text.StartsWith("Invoice item", StringComparison.Ordinal)), Is.EqualTo(44));
        Assert.That(Encoding.Latin1.GetString(Pdf(source)), Does.Contain(" re W n "));
    }

    [Test]
    public void ImportantLineWinnerAndCssWideResetOnlyReplaceTheLineSet()
    {
        const string source = "<style>.changed{text-decoration-line:underline line-through!important}</style>" +
            "<p class=changed style='text-decoration:overline'>AB</p>";
        Assert.That(Pdf(source), Is.EqualTo(Pdf("<p style='text-decoration:underline line-through'>AB</p>")));
        const string inherited = "<p style='text-decoration-line:underline overline'>" +
            "<span style='text-decoration-line:inherit'>AB</span>" +
            "<span style='text-decoration-line:unset'>CD</span></p>";
        const string oracle = "<p style='text-decoration-line:underline overline'>" +
            "<span style='text-decoration-line:underline overline'>AB</span>" +
            "<span style='text-decoration-line:none'>CD</span></p>";
        Assert.That(Pdf(inherited), Is.EqualTo(Pdf(oracle)));
        Assert.That(Pdf("<p style='text-decoration-line:underline line-through'>AB</p>"),
            Is.Not.EqualTo(Pdf("<p style='text-decoration-line:underline'>AB</p>")));
    }

    [Test]
    public void ExplicitTrueTypeTableCellDrawsAllSelectedLines()
    {
        var font = PdfFont.LoadTrueType(FontFixture.Create());
        var options = Page with { Fonts = [font] };
        const string html = "<table style='width:170pt'><colgroup><col style='width:170pt'></colgroup>" +
            "<tbody><tr><td><p style='margin:0;font-size:10pt;" +
            "text-decoration:underline overline line-through solid red 1pt'>AB</p></td></tr></tbody></table>";
        var run = Layout(html, options).SelectMany(p => p.Runs).Single();
        Assert.That(ReferenceEquals(run.Font, font), Is.True);
        Assert.That(run.Style.DecorationThickness, Is.EqualTo(1));
        var pdf = Encoding.Latin1.GetString(Pdf(html, options));
        Assert.That(Regex.Matches(pdf, @" re f Q").Count, Is.EqualTo(3));
    }

    [Test]
    public void CliAndDisplayCommandQuotaAccountForEveryStroke()
    {
        const string html = "<p style='margin:0;text-decoration:underline line-through overline'>AB</p>";
        using var stdin = new MemoryStream(Encoding.UTF8.GetBytes(html));
        using var stdout = new MemoryStream();
        using var diagnostics = new StringWriter();
        Assert.That(CliApplication.Run(["-", "-"], stdin, stdout, diagnostics), Is.Zero, diagnostics.ToString());
        Assert.That(stdout.ToArray(), Is.EqualTo(Pdf(html, new())));
        RejectWithBudget(html, 3);
        Assert.That(Pdf(html, Page with { MaxDisplayCommands = 4 }), Is.Not.Empty);
    }

    private static void RejectWithBudget(string html, int limit)
    {
        using var output = new MemoryStream();
        output.Write("previous-invoice"u8);
        output.Position = 4;
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output,
            Page with { MaxDisplayCommands = limit }));
        Assert.That(error!.Code, Is.EqualTo("FPDF1401"));
        Assert.That(output.ToArray(), Is.EqualTo("previous-invoice"u8.ToArray()));
        Assert.That(output.Position, Is.EqualTo(4));
    }

    [TestCase("text-decoration-line:none underline")]
    [TestCase("text-decoration-line:underline underline")]
    [TestCase("text-decoration-line:underline overline line-through overline")]
    [TestCase("text-decoration-line:underline dotted")]
    [TestCase("text-decoration:underline underline red")]
    [TestCase("text-decoration:underline none")]
    [TestCase("text-decoration:underline overline line-through overline")]
    [TestCase("text-decoration:underline overline red 1pt 2pt")]
    [TestCase("text-decoration-line:underline,overline")]
    public void InvalidOrUnsupportedLineListsRejectEvenInUnmatchedStylesheets(string css)
        => Reject("<style>.unused{" + css + "}</style><p>AB</p>", "FPDF1202");

    [Test]
    public void StandaloneImageCannotClaimCombinedTextDecoration()
        => Reject("<img src='brand' style='text-decoration-line:underline overline'>", "FPDF1201");
}
