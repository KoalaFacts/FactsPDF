using System.Text;
using FactsPDF.CommandLine;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>All 18 LTR logical border component spellings must render physical PDF edges.</summary>
[TestFixture]
public sealed class LogicalBorderComponentTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 310, PageHeight = 165, Margin = 18 };

    private static byte[] Pdf(string html, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, options ?? Page);
        return output.ToArray();
    }

    private static void Reject(string html, string expected)
    {
        using var output = new MemoryStream();
        output.Write("KEEP CUSTOMER PDF"u8);
        output.Position = 4;
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, Page));
        Assert.That(error!.Code, Is.EqualTo(expected));
        Assert.That(Encoding.ASCII.GetString(output.ToArray()), Is.EqualTo("KEEP CUSTOMER PDF"));
        Assert.That(output.Position, Is.EqualTo(4));
    }

    [TestCase("border-inline-width:2pt 4pt", "border-left-width:2pt;border-right-width:4pt")]
    [TestCase("border-block-width:1pt 3pt", "border-top-width:1pt;border-bottom-width:3pt")]
    [TestCase("border-inline-style:solid dashed", "border-left-style:solid;border-right-style:dashed")]
    [TestCase("border-block-style:dotted solid", "border-top-style:dotted;border-bottom-style:solid")]
    [TestCase("border-inline-color:red blue", "border-left-color:red;border-right-color:blue")]
    [TestCase("border-block-color:rebeccapurple #345678", "border-top-color:rebeccapurple;border-bottom-color:#345678")]
    [TestCase("border-inline-start-width:1pt", "border-left-width:1pt")]
    [TestCase("border-inline-end-width:2pt", "border-right-width:2pt")]
    [TestCase("border-block-start-width:3pt", "border-top-width:3pt")]
    [TestCase("border-block-end-width:4pt", "border-bottom-width:4pt")]
    [TestCase("border-inline-start-style:solid", "border-left-style:solid")]
    [TestCase("border-inline-end-style:dashed", "border-right-style:dashed")]
    [TestCase("border-block-start-style:dotted", "border-top-style:dotted")]
    [TestCase("border-block-end-style:solid", "border-bottom-style:solid")]
    [TestCase("border-inline-start-color:red", "border-left-color:red")]
    [TestCase("border-inline-end-color:blue", "border-right-color:blue")]
    [TestCase("border-block-start-color:rgb(31 61 89)", "border-top-color:rgb(31 61 89)")]
    [TestCase("border-block-end-color:hsl(240,100%,50%)", "border-bottom-color:hsl(240,100%,50%)")]
    public void AllEighteenComponentPropertiesMatchActualPhysicalPaint(string logical, string physical)
    {
        const string start = "<section style='width:185pt;padding:7pt;background:#eff4f9;" +
            "border:2pt solid #455667;";
        const string end = "'><p style='margin:0'>Inspection record</p></section>";
        Assert.That(Pdf(start + logical + end), Is.EqualTo(Pdf(start + physical + end)));
    }

    [Test]
    public void PairComponentsActuallyChangeBorderGeometryAndPaint()
    {
        const string source = "<article style='width:160pt;padding:5pt;background:#edf4f8;" +
            "border-inline-width:3pt 5pt;border-inline-style:solid dashed;" +
            "border-inline-color:red blue;border-block-width:2pt 4pt;" +
            "border-block-style:dotted solid;border-block-color:green rebeccapurple'>" +
            "<p style='margin:0'>VISIBLE PDF EDGES</p></article>";
        const string control = "<article style='width:160pt;padding:5pt;background:#edf4f8;" +
            "border-left-width:3pt;border-right-width:5pt;" +
            "border-left-style:solid;border-right-style:dashed;" +
            "border-left-color:red;border-right-color:blue;" +
            "border-top-width:2pt;border-bottom-width:4pt;" +
            "border-top-style:dotted;border-bottom-style:solid;" +
            "border-top-color:green;border-bottom-color:rebeccapurple'>" +
            "<p style='margin:0'>VISIBLE PDF EDGES</p></article>";
        Assert.That(Pdf(source), Is.EqualTo(Pdf(control)));
        var pages = TextLayout.Layout(HtmlDocumentReader.ReadTree(source, Page, default), Page, default);
        var painted = pages.SelectMany(page => page.PaintBoxes)
            .First(box => box.Style.BackgroundColor.HasValue);
        Assert.Multiple(() =>
        {
            Assert.That(painted.Style.BorderLeft.EffectiveWidth, Is.EqualTo(3));
            Assert.That(painted.Style.BorderRight.EffectiveWidth, Is.EqualTo(5));
            Assert.That(painted.Style.BorderTop.EffectiveWidth, Is.EqualTo(2));
            Assert.That(painted.Style.BorderBottom.EffectiveWidth, Is.EqualTo(4));
        });
    }

    [Test]
    public void ComponentCascadeCompetesWithPhysicalAndShorthandImportantPerEdge()
    {
        const string logical = "<style>.card{border:1pt solid black;" +
            "border-inline-width:2pt 3pt;border-inline-color:red blue;" +
            "border-block-style:dotted dashed;border-left-width:6pt!important}" +
            "article.card{border-inline-end-color:green}</style>" +
            "<article class=card style='border-inline-start-width:9pt;" +
            "border-block-start-color:rebeccapurple;border-block-end-style:solid'>" +
            "<p style='margin:0'>CASCADE</p></article>";
        const string physical = "<style>.card{border:1pt solid black;" +
            "border-left-width:2pt;border-right-width:3pt;" +
            "border-left-color:red;border-right-color:blue;" +
            "border-top-style:dotted;border-bottom-style:dashed;" +
            "border-left-width:6pt!important}" +
            "article.card{border-right-color:green}</style>" +
            "<article class=card style='border-left-width:9pt;" +
            "border-top-color:rebeccapurple;border-bottom-style:solid'>" +
            "<p style='margin:0'>CASCADE</p></article>";
        Assert.That(Pdf(logical), Is.EqualTo(Pdf(physical)));
    }

    [Test]
    public void WideKeywordsAreIndependentAndUnmatchedDeclarationsAreStillValidated()
    {
        const string logical = "<section style='border-left:5pt dashed red;border-right:2pt solid blue;" +
            "border-top:3pt dotted green;border-bottom:4pt solid black'>" +
            "<blockquote style='border-inline-width:inherit;border-block-style:unset;" +
            "border-inline-start-color:initial;border-block-end-width:inherit'>" +
            "<p style='margin:0'>INHERITED</p></blockquote></section>";
        const string physical = "<section style='border-left:5pt dashed red;border-right:2pt solid blue;" +
            "border-top:3pt dotted green;border-bottom:4pt solid black'>" +
            "<blockquote style='border-left-width:inherit;border-right-width:inherit;" +
            "border-top-style:unset;border-bottom-style:unset;" +
            "border-left-color:initial;border-bottom-width:inherit'>" +
            "<p style='margin:0'>INHERITED</p></blockquote></section>";
        Assert.That(Pdf(logical), Is.EqualTo(Pdf(physical)));
        Reject("<style>.unused{border-inline-style:solid double}</style><p>SAFE</p>", "FPDF1202");
    }

    [Test]
    public void TrueTypeApiAndManagedCliMatchPhysicalEdgeOracle()
    {
        var fontBytes = FontFixture.Create();
        var font = PdfFont.LoadTrueType(fontBytes);
        var options = Page with { Fonts = [font], BoldFonts = [font] };
        const string logical = "<section style='border-inline-width:2pt;" +
            "border-inline-style:solid;border-inline-color:#315f82;" +
            "border-block-width:1pt;border-block-style:dashed;border-block-color:#246637;" +
            "padding:4pt'><p style='margin:0;font-weight:bold'>AB</p></section>";
        const string physical = "<section style='border-left-width:2pt;border-right-width:2pt;" +
            "border-left-style:solid;border-right-style:solid;" +
            "border-left-color:#315f82;border-right-color:#315f82;" +
            "border-top-width:1pt;border-bottom-width:1pt;" +
            "border-top-style:dashed;border-bottom-style:dashed;" +
            "border-top-color:#246637;border-bottom-color:#246637;" +
            "padding:4pt'><p style='margin:0;font-weight:bold'>AB</p></section>";
        Assert.That(Pdf(logical, options), Is.EqualTo(Pdf(physical, options)));
        var path = Path.Combine(Path.GetTempPath(),
            $"factspdf-logical-components-{Guid.NewGuid():N}.ttf");
        try
        {
            File.WriteAllBytes(path, fontBytes);
            using var input = new MemoryStream(Encoding.UTF8.GetBytes(logical));
            using var output = new MemoryStream();
            using var diagnostics = new StringWriter();
            Assert.That(CliApplication.Run(["-", "-", "--page-width", "310pt",
                "--page-height", "165pt", "--margin", "18pt",
                "--font", path, "--font-bold", path], input, output, diagnostics),
                Is.Zero, diagnostics.ToString());
            Assert.That(output.ToArray(), Is.EqualTo(Pdf(logical, options)));
        }
        finally { File.Delete(path); }
    }

    [TestCase("border-inline-width:1pt 2pt 3pt")]
    [TestCase("border-block-style:dashed double")]
    [TestCase("border-inline-color:rgba(1,2,3,0.5)")]
    [TestCase("border-block-start-width:-2pt")]
    [TestCase("border-inline-end-color:linear-gradient(red,blue)")]
    [TestCase("border-block-color:inherit blue")]
    [TestCase("border-inline-start-width:2pt 3pt")]
    public void MalformedComponentsNeverReplaceExistingPdf(string css)
        => Reject("<section style='" + css + "'><p>SAFE</p></section>", "FPDF1202");

    [Test]
    public void InapplicableAndExpandedDeclarationsFailClosed()
    {
        Reject("<p>Before <span style='border-inline-width:2pt'>After</span></p>", "FPDF1201");
        using var output = new MemoryStream();
        output.Write("KEEP"u8);
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(
            "<section style='border-inline-width:2pt'>Budget</section>", output,
            Page with { MaxCssDeclarations = 1 }));
        Assert.That(error!.Code, Is.EqualTo("FPDF1205"));
        Assert.That(output.ToArray(), Is.EqualTo("KEEP"u8.ToArray()));
    }

    [Test]
    public void CheckedInTwoPageRegisterEqualsIndependentPhysicalOracle()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "FactsPDF.slnx")))
            directory = directory.Parent;
        if (directory is null) throw new InvalidOperationException("Repository root not found.");
        var source = File.ReadAllText(Path.Combine(directory.FullName, "examples", "report-logical-border-components.html"));
        var oracle = File.ReadAllText(Path.Combine(directory.FullName, "examples", "report-logical-border-components-oracle.html"));
        var options = new PdfOptions();
        Assert.That(Pdf(source, options), Is.EqualTo(Pdf(oracle, options)));
        var pages = TextLayout.Layout(HtmlDocumentReader.ReadTree(source, options, default), options, default);
        Assert.That(pages.Count, Is.GreaterThan(1));
        Assert.That(pages.SelectMany(p => p.Runs)
            .Count(x => x.Text.StartsWith("COMP-", StringComparison.Ordinal)), Is.EqualTo(44));
        Assert.That(pages.SelectMany(p => p.Runs).Last().Text, Is.EqualTo("COMPLETE"));
    }
}
