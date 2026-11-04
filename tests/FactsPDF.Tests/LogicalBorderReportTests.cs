using System.Text;
using FactsPDF.CommandLine;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>LTR logical border shorthands must paint the same PDF edges as physical borders.</summary>
[TestFixture]
public sealed class LogicalBorderReportTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 310, PageHeight = 165, Margin = 18 };

    private static byte[] Pdf(string html, PdfOptions? options = null)
    {
        using var stream = new MemoryStream();
        PdfConverter.Convert(html, stream, options ?? Page);
        return stream.ToArray();
    }

    private static void Reject(string html, string expected)
    {
        using var stream = new MemoryStream();
        stream.Write("KEEP CUSTOMER PDF"u8);
        stream.Position = 4;
        var failure = Assert.Throws<FactsPdfException>(() =>
            PdfConverter.Convert(html, stream, Page));
        Assert.That(failure!.Code, Is.EqualTo(expected));
        Assert.That(Encoding.ASCII.GetString(stream.ToArray()), Is.EqualTo("KEEP CUSTOMER PDF"));
        Assert.That(stream.Position, Is.EqualTo(4));
    }

    [TestCase("border-inline:2pt dashed #27618b", "border-left:2pt dashed #27618b;border-right:2pt dashed #27618b")]
    [TestCase("border-block:3pt dotted rebeccapurple", "border-top:3pt dotted rebeccapurple;border-bottom:3pt dotted rebeccapurple")]
    [TestCase("border-inline-start:thin solid red", "border-left:thin solid red")]
    [TestCase("border-inline-end:medium solid blue", "border-right:medium solid blue")]
    [TestCase("border-block-start:thick solid green", "border-top:thick solid green")]
    [TestCase("border-block-end:1pc solid #004466", "border-bottom:1pc solid #004466")]
    public void AllSixLogicalBorderShorthandsPaintRealPhysicalEdges(string logical, string physical)
    {
        const string start = "<section style='width:185pt;padding:7pt;background:#eff4f9;";
        const string end = "'><p style='margin:0'>Inspection record</p></section>";
        Assert.That(Pdf(start + logical + end), Is.EqualTo(Pdf(start + physical + end)));
    }

    [Test]
    public void BothAxesAndPhysicalLonghandsCascadeIndependentlyWithImportant()
    {
        const string logical = "<style>.card{border:1pt solid black;" +
            "border-inline:2pt dashed red;border-block:3pt dotted blue;" +
            "border-left:4pt solid green!important}" +
            "section.card{border-inline-end:5pt solid #723548}</style>" +
            "<section class=card style='border-inline-start:6pt dotted yellow;" +
            "border-block-end:1pt solid #345678'>" +
            "<p style='margin:0'>CASCADE</p></section>";
        const string physical = "<style>.card{border:1pt solid black;" +
            "border-left:2pt dashed red;border-right:2pt dashed red;" +
            "border-top:3pt dotted blue;border-bottom:3pt dotted blue;" +
            "border-left:4pt solid green!important}" +
            "section.card{border-right:5pt solid #723548}</style>" +
            "<section class=card style='border-left:6pt dotted yellow;" +
            "border-bottom:1pt solid #345678'>" +
            "<p style='margin:0'>CASCADE</p></section>";
        Assert.That(Pdf(logical), Is.EqualTo(Pdf(physical)));
    }

    [Test]
    public void CssWideKeywordsAndDefaultsMatchPhysicalEdges()
    {
        const string logical = "<section style='border-left:2pt solid red;border-right:4pt dashed blue;" +
            "border-top:3pt dotted green;border-bottom:5pt solid black'>" +
            "<blockquote style='border-inline:inherit;border-block:unset'>" +
            "<p style='margin:0'>INHERITED</p></blockquote>" +
            "<blockquote style='border-inline-start:initial;border-block-end:inherit'>" +
            "<p style='margin:0'>RESET</p></blockquote></section>";
        const string physical = "<section style='border-left:2pt solid red;border-right:4pt dashed blue;" +
            "border-top:3pt dotted green;border-bottom:5pt solid black'>" +
            "<blockquote style='border-left:inherit;border-right:inherit;" +
            "border-top:unset;border-bottom:unset'>" +
            "<p style='margin:0'>INHERITED</p></blockquote>" +
            "<blockquote style='border-left:initial;border-bottom:inherit'>" +
            "<p style='margin:0'>RESET</p></blockquote></section>";
        Assert.That(Pdf(logical), Is.EqualTo(Pdf(physical)));
    }

    [Test]
    public void MultiPageBusinessReportRetainsPaintAndAllRecordPositions()
    {
        var records = string.Join("", Enumerable.Range(1, 44).Select(i =>
            $"<section style='padding:4pt;border-inline-start:3pt solid #27618b;" +
            $"border-block-end:1pt dotted #7294ab'><p style='margin:0'>ITEM-{i:00} CONFIRMED</p></section>"));
        var control = string.Join("", Enumerable.Range(1, 44).Select(i =>
            $"<section style='padding:4pt;border-left:3pt solid #27618b;" +
            $"border-bottom:1pt dotted #7294ab'><p style='margin:0'>ITEM-{i:00} CONFIRMED</p></section>"));
        var logical = "<article style='width:195pt;border-inline:2pt solid currentColor;" +
            "border-block:3pt solid #315f82;background:#e8f1f7'>" +
            records + "</article><p style='margin:0'>COMPLETE</p>";
        var physical = "<article style='width:195pt;border-left:2pt solid currentColor;" +
            "border-right:2pt solid currentColor;border-top:3pt solid #315f82;" +
            "border-bottom:3pt solid #315f82;background:#e8f1f7'>" +
            control + "</article><p style='margin:0'>COMPLETE</p>";
        Assert.That(Pdf(logical), Is.EqualTo(Pdf(physical)));
        var pages = TextLayout.Layout(HtmlDocumentReader.ReadTree(logical, Page, default), Page, default);
        Assert.That(pages.Count, Is.GreaterThan(1));
        Assert.That(pages.SelectMany(x => x.Runs)
            .Count(x => x.Text.StartsWith("ITEM-", StringComparison.Ordinal)), Is.EqualTo(44));
        Assert.That(pages.SelectMany(x => x.Runs).Last().Text, Is.EqualTo("COMPLETE"));
        Assert.That(pages.SelectMany(x => x.PaintBoxes).Any(x => x.BorderLeft.EffectiveWidth > 0), Is.True);
    }

    [Test]
    public void SuppliedTrueTypeAndManagedCliPaintSameBorderGeometry()
    {
        var font = PdfFont.LoadTrueType(FontFixture.Create());
        var options = Page with { Fonts = [font], BoldFonts = [font] };
        const string source = "<div style='border-inline:2pt solid #315f82;" +
            "border-block:1pt dashed #246637;padding:4pt'>" +
            "<p style='margin:0;font-weight:bold'>AB</p></div>";
        const string control = "<div style='border-left:2pt solid #315f82;" +
            "border-right:2pt solid #315f82;border-top:1pt dashed #246637;" +
            "border-bottom:1pt dashed #246637;padding:4pt'>" +
            "<p style='margin:0;font-weight:bold'>AB</p></div>";
        Assert.That(Pdf(source, options), Is.EqualTo(Pdf(control, options)));
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(source));
        using var output = new MemoryStream();
        using var diagnostics = new StringWriter();
        Assert.That(CliApplication.Run(["-", "-"], input, output, diagnostics), Is.Zero,
            diagnostics.ToString());
        using var expected = new MemoryStream();
        PdfConverter.Convert(source, expected);
        Assert.That(output.ToArray(), Is.EqualTo(expected.ToArray()));
    }

    [TestCase("border-inline:1pt solid red blue", "FPDF1202")]
    [TestCase("border-block:1pt solid double", "FPDF1202")]
    [TestCase("border-inline:1pt solid rgba(1,2,3,0.5)", "FPDF1202")]
    [TestCase("border-block:calc(2pt + 1pt) solid blue", "FPDF1202")]
    [TestCase("border-inline:inherit 2pt", "FPDF1202")]
    [TestCase("border-inline-start:2pt solid red blue", "FPDF1202")]
    [TestCase("border-block-end:-2pt solid red", "FPDF1202")]
    [TestCase("border-inline:2pt 3pt solid red", "FPDF1202")]
    public void InvalidLogicalBordersProtectExistingPdf(string css, string code)
        => Reject("<section style='" + css + "'><p>NO CHANGE</p></section>", code);

    [Test]
    public void InvalidUnmatchedRuleStillFailsAndInapplicableInlineBorderIsRejected()
    {
        Reject("<style>.unused{border-inline:1pt double red}</style><p>SAFE</p>", "FPDF1202");
        Reject("<p>Before <span style='border-inline:2pt solid red'>After</span></p>", "FPDF1201");
    }

    [Test]
    public void ExpandedShorthandCountsBothSidesAndAllSixDeclarations()
    {
        using var stream = new MemoryStream();
        stream.Write("SAFE"u8);
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(
            "<section style='border-inline:1pt solid blue'>Budget</section>", stream,
            Page with { MaxCssDeclarations = 5 }));
        Assert.That(error!.Code, Is.EqualTo("FPDF1205"));
        Assert.That(stream.ToArray(), Is.EqualTo("SAFE"u8.ToArray()));
    }
    [Test]
    public void CheckedInReportAndIndependentPhysicalEdgeOracleGenerateIdenticalRealPdf()
    {
        var root = FindRoot();
        var html = File.ReadAllText(Path.Combine(root, "examples", "report-logical-borders.html"));
        var control = File.ReadAllText(Path.Combine(root, "examples", "report-logical-borders-oracle.html"));
        Assert.That(Pdf(html), Is.EqualTo(Pdf(control)));
        var pages = TextLayout.Layout(HtmlDocumentReader.ReadTree(html, Page, default), Page, default);
        Assert.That(pages.Count, Is.GreaterThan(1));
        Assert.That(pages.SelectMany(p => p.Runs)
            .Count(r => r.Text.StartsWith("EDGE-", StringComparison.Ordinal)), Is.EqualTo(44));
        Assert.That(pages.SelectMany(p => p.Runs).Last().Text, Is.EqualTo("COMPLETE"));
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
