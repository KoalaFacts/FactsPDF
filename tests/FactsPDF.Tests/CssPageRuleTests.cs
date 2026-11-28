using System.Globalization;
using System.Text;
using FactsPDF.CommandLine;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>CSS paged media: @page size, margins and the six top/bottom page-margin boxes.</summary>
[TestFixture]
public sealed class CssPageRuleTests
{
    private const string Body = "<p>First line of the report</p><p style='break-before:page'>Second page</p>";
    private static readonly double Millimetre = 72d / 25.4d;

    private static byte[] Pdf(string html, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, options ?? new PdfOptions());
        return output.ToArray();
    }

    private static PdfOptions Effective(string html, PdfOptions? options = null)
    {
        options ??= new PdfOptions();
        var document = HtmlDocumentReader.ReadTree(html, options, default);
        return document.PageRules?.Apply(options, document.RootStyle) ?? options;
    }

    private static List<LayoutPage> Layout(string html, PdfOptions? options = null)
    {
        options ??= new PdfOptions();
        var document = HtmlDocumentReader.ReadTree(html, options, default);
        var effective = document.PageRules?.Apply(options, document.RootStyle) ?? options;
        return TextLayout.Layout(document, effective, default);
    }

    private static FactsPdfException Fails(string html, string code, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        output.Write("KEEP"u8);
        output.Position = 2;
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, options ?? new PdfOptions()));
        Assert.That(error!.Code, Is.EqualTo(code), error.Message);
        Assert.That(Encoding.ASCII.GetString(output.ToArray()), Is.EqualTo("KEEP"));
        Assert.That(output.Position, Is.EqualTo(2));
        return error;
    }

    private static string MediaBox(double width, double height)
        => string.Create(CultureInfo.InvariantCulture, $"/MediaBox [0 0 {width:0.###} {height:0.###}]");

    [TestCase("@page{size:240pt 120pt;margin:20pt}")]
    [TestCase("@media print{@page{size:240pt 120pt;margin:20pt}}")]
    [TestCase("@media all{@page{size:240pt 120pt}}@page{margin:20pt}")]
    [TestCase("@page{size:240pt 120pt;margin:5pt;margin-top:20pt;margin-right:20pt;margin-bottom:20pt;margin-left:20pt}")]
    public void SizeAndUniformMarginRenderLikeTheEquivalentWriterOptions(string css)
    {
        var expected = Pdf(Body, new PdfOptions { PageWidth = 240, PageHeight = 120, Margin = 20 });
        Assert.That(Pdf("<style>" + css + "</style>" + Body), Is.EqualTo(expected));
    }

    [TestCase("A4", 595.28, 841.89)]
    [TestCase("a4 portrait", 595.28, 841.89)]
    [TestCase("landscape A4", 841.89, 595.28)]
    [TestCase("A5 landscape", 210 * (72 / 25.4), 148 * (72 / 25.4))]
    [TestCase("A3", 297 * (72 / 25.4), 420 * (72 / 25.4))]
    [TestCase("letter", 612, 792)]
    [TestCase("legal landscape", 1008, 612)]
    [TestCase("ledger", 792, 1224)]
    [TestCase("JIS-B5", 182 * (72 / 25.4), 257 * (72 / 25.4))]
    [TestCase("landscape", 841.89, 595.28)]
    [TestCase("portrait", 595.28, 841.89)]
    [TestCase("auto", 595.28, 841.89)]
    [TestCase("150mm 100mm", 150 * (72 / 25.4), 100 * (72 / 25.4))]
    [TestCase("5in", 360, 360)]
    [TestCase("800px 600px", 600, 450)]
    public void SizeKeywordsOrientationsAndLengthsSetTheMediaBox(string value, double width, double height)
    {
        var html = "<style>@page{size:" + value + "}</style>" + Body;
        var effective = Effective(html);
        Assert.That(effective.PageWidth, Is.EqualTo(width).Within(0.000001));
        Assert.That(effective.PageHeight, Is.EqualTo(height).Within(0.000001));
        var pdf = Encoding.ASCII.GetString(Pdf(html));
        Assert.That(pdf.Split(MediaBox(width, height)).Length - 1, Is.EqualTo(2), "Both pages need the @page size.");
        Assert.That(Pdf(html), Is.EqualTo(Pdf(Body, new PdfOptions { PageWidth = width, PageHeight = height })));
    }

    [Test]
    public void LandscapeTurnsTheWritersPaperWhenNoSizeIsGiven()
    {
        var effective = Effective("<style>@page{size:landscape}</style><p>x</p>",
            new PdfOptions { PageWidth = 612, PageHeight = 792 });
        Assert.That((effective.PageWidth, effective.PageHeight), Is.EqualTo((792d, 612d)));
    }

    [Test]
    public void AsymmetricMarginsMoveAndNarrowTheBodyAndPaginateOnTheRemainingHeight()
    {
        const string longText = "<p>" + "alpha beta gamma delta epsilon zeta eta theta iota kappa " + "</p>";
        var html = "<style>@page{size:300pt 200pt;margin:10pt 20pt 30pt 40pt}body{margin:0}p{margin:0;font-size:10pt;line-height:12pt}</style>"
            + string.Concat(Enumerable.Repeat(longText, 10));
        var effective = Effective(html);
        Assert.That(effective.SideMargins, Is.EqualTo(new PageMargins(10, 20, 30, 40)));
        Assert.That(effective.ContentWidth, Is.EqualTo(240));
        Assert.That(effective.ContentHeight, Is.EqualTo(160));
        var pages = Layout(html);
        var runs = pages.SelectMany(p => p.Runs).ToArray();
        Assert.That(runs.Min(r => r.X), Is.EqualTo(40).Within(0.000001), "Body starts at the left margin.");
        Assert.That(runs.Max(r => r.X + r.Width), Is.LessThanOrEqualTo(280 + 0.000001), "Body stays inside the right margin.");
        // 12pt lines: the first line box starts at the top margin and no line passes the bottom margin.
        Assert.That(pages[0].Runs.Max(r => r.Baseline), Is.GreaterThan(200 - 10 - 12).And.LessThan(200 - 10));
        Assert.That(pages.SelectMany(p => p.Runs).Min(r => r.Baseline), Is.GreaterThanOrEqualTo(30 - 0.000001));
        Assert.That(pages, Has.Count.GreaterThan(1), "The witness must paginate.");
    }

    [Test]
    public void MarginLonghandsCascadeBySourceOrderAndImportance()
    {
        var effective = Effective("<style>@page{margin:20pt;margin-left:50pt !important}@page{margin:30pt}</style><p>x</p>");
        Assert.That(effective.SideMargins, Is.EqualTo(new PageMargins(30, 30, 30, 50)));
        var uniform = Effective("<style>@page{margin:1cm}</style><p>x</p>");
        Assert.That(uniform.SideMargins, Is.Null);
        Assert.That(uniform.Margin, Is.EqualTo(72 / 2.54).Within(0.000001));
        var initial = Effective("<style>@page{margin:1cm}@page{margin-top:initial}</style><p>x</p>");
        Assert.That(initial.SideMargins, Is.EqualTo(new PageMargins(36, 72 / 2.54, 72 / 2.54, 72 / 2.54)));
        var relative = Effective("<html><head><style>html{font-size:10pt}@page{margin:2em 1rem}</style></head><body><p>x</p></body></html>");
        Assert.That(relative.SideMargins, Is.EqualTo(new PageMargins(20, 10, 20, 10)));
    }

    [Test]
    public void MarginBoxesRenderLikeTheEquivalentStructuredFurniture()
    {
        const string css = "<style>@media print{@page{size:A4 portrait;margin:1cm;" +
            "@bottom-center{content:counter(page) ' / ' counter(pages);font-size:0.8em}" +
            "@top-left{content:'Service {report}';color:#336699}" +
            "@top-right{content:'Page ' counter(page, decimal);font-size:9pt}}}</style>";
        var furniture = new PdfPageFurniture
        {
            Default = new()
            {
                HeaderLeft = new() { Template = "Service {{report}}", FontSize = 12, Color = new(0x33, 0x66, 0x99) },
                HeaderRight = new() { Template = "Page {page}", FontSize = 9 },
                FooterCenter = new() { Template = "{page} / {pages}", FontSize = 12 * 0.8 },
            }
        };
        var expected = Pdf(Body, new PdfOptions { Margin = 72 / 2.54, PageFurniture = furniture });
        Assert.That(Pdf(css + Body), Is.EqualTo(expected));
        Assert.That(Effective(css + Body).PageFurniture!.Default, Is.EqualTo(furniture.Default));
    }

    [Test]
    public void MarginBoxesInheritTheHtmlElementTextStyle()
    {
        const string html = "<html><head><style>html{font-size:15pt;color:#800000}body{font-size:12pt;color:black}" +
            "@page{@bottom-center{content:counter(page);font-size:0.8em}@top-center{content:'Head'}}</style></head><body><p>x</p></body></html>";
        var furniture = Effective(html).PageFurniture!.Default;
        Assert.That(furniture.FooterCenter!.FontSize, Is.EqualTo(12).Within(0.000001));
        Assert.That(furniture.FooterCenter.Color, Is.EqualTo(new PdfRgbColor(0x80, 0, 0)));
        Assert.That(furniture.HeaderCenter!.FontSize, Is.EqualTo(15).Within(0.000001));
        var withoutHtml = Effective("<style>@page{@bottom-left{content:'x';font-size:larger;color:inherit}}</style><p>x</p>",
            new PdfOptions { FontSize = 10 }).PageFurniture!.Default.FooterLeft!;
        Assert.That(withoutHtml.FontSize, Is.EqualTo(12).Within(0.000001));
        Assert.That(withoutHtml.Color, Is.EqualTo(new PdfRgbColor(0, 0, 0)));
    }

    [Test]
    public void BoldAndItalicMarginBoxesUseTheExplicitStyleChains()
    {
        var bold = Effective("<style>@page{@bottom-right{content:'x';font-weight:bold;font-style:italic}}</style><p>x</p>")
            .PageFurniture!.Default.FooterRight!;
        Assert.That((bold.Bold, bold.Italic), Is.EqualTo((true, true)));
        // Without a bold-italic chain the footer fails like any other bold-italic text.
        Fails("<style>@page{@bottom-right{content:'x';font-weight:700;font-style:italic}}</style><p>x</p>", "FPDF1507");
    }

    [Test]
    public void LaterContentNoneRemovesAnEarlierMarginBoxUnlessItWasImportant()
    {
        const string footer = "@page{margin:1cm;@bottom-center{content:counter(page) ' / ' counter(pages);font-size:0.8em}}";
        var plain = Pdf(Body, new PdfOptions { Margin = 72 / 2.54 });
        Assert.That(Pdf("<style>@media print{" + footer + "}@page{@bottom-center{content:none}}</style>" + Body), Is.EqualTo(plain));
        Assert.That(Pdf("<style>" + footer + "@page{@bottom-center{content:normal}}</style>" + Body), Is.EqualTo(plain));
        Assert.That(Effective("<style>@page{@bottom-center{content:'kept' !important}}@page{@bottom-center{content:none}}</style><p>x</p>")
            .PageFurniture!.Default.FooterCenter!.Template, Is.EqualTo("kept"));
        Assert.That(Effective("<style>@page{@bottom-center{font-size:9pt}}</style><p>x</p>").PageFurniture, Is.Null,
            "A margin box without content generates nothing.");
    }

    [Test]
    public void MarginBoxesCentreOnThePageAreaAndAlignToTheSideMargins()
    {
        var options = Effective("<style>@page{size:300pt 200pt;margin:30pt 20pt 30pt 60pt;" +
            "@top-left{content:'L'}@top-right{content:'R'}@bottom-center{content:'C'}}</style><p>x</p>");
        var pages = Layout("<style>@page{size:300pt 200pt;margin:30pt 20pt 30pt 60pt}</style><p>x</p>");
        var bodyCommands = pages.Sum(p => (long)p.Runs.Count);
        PageFurnitureLayout.Append(pages, options, bodyCommands, default);
        var runs = pages[0].Runs.Skip(1).ToDictionary(r => r.Text);
        Assert.That(runs["L"].X, Is.EqualTo(60).Within(0.000001));
        Assert.That(runs["R"].X + runs["R"].Width, Is.EqualTo(280).Within(0.000001));
        Assert.That(runs["C"].X + runs["C"].Width / 2, Is.EqualTo(170).Within(0.000001), "Centre of the page area, not the page.");
        Assert.That(runs["C"].Baseline, Is.LessThan(30));
        Assert.That(runs["L"].Baseline, Is.GreaterThan(170));
    }

    [Test]
    public void ExplicitWriterOptionsMustAgreeWithPageRules()
    {
        const string sized = "<style>@page{size:A4}</style><p>x</p>";
        var letter = Assert.Throws<FactsPdfException>(() => FactsPDF.Pdf.Write().WithPage(p => p.Letter()).ToBytes(sized));
        Assert.That(letter!.Code, Is.EqualTo("FPDF1206"));
        var landscape = Assert.Throws<FactsPdfException>(() => FactsPDF.Pdf.Write().WithPage(p => p.Landscape()).ToBytes(sized));
        Assert.That(landscape!.Code, Is.EqualTo("FPDF1206"));
        Assert.That(FactsPDF.Pdf.Write().WithPage(p => p.A4()).ToBytes(sized), Is.EqualTo(FactsPDF.Pdf.Write().ToBytes(sized)),
            "An explicit size equal to the @page size is accepted.");

        const string margined = "<style>@page{margin:10mm}</style><p>x</p>";
        var margin = Assert.Throws<FactsPdfException>(() => FactsPDF.Pdf.Write().WithPage(p => p.Margin(20)).ToBytes(margined));
        Assert.That(margin!.Code, Is.EqualTo("FPDF1206"));
        Assert.That(FactsPDF.Pdf.Write().WithPage(p => p.Margin(10, PdfUnit.Millimetre)).ToBytes(margined),
            Is.EqualTo(FactsPDF.Pdf.Write().ToBytes(margined)));
        Assert.That(FactsPDF.Pdf.Write().WithPage(p => p.Margin(20)).ToBytes(sized), Is.Not.Empty,
            "A writer margin combines with a CSS size.");
    }

    [Test]
    public void MarginBoxesCannotBeCombinedWithWriterPageText()
    {
        const string html = "<style>@page{@bottom-center{content:counter(page)}}</style><p>x</p>";
        foreach (var options in new[]
        {
            new PdfOptions { PageNumbers = true },
            new PdfOptions { HeaderText = "" },
            new PdfOptions { FooterText = "Internal" },
            new PdfOptions { PageFurniture = new() },
        })
            Fails(html, "FPDF1206", options);
        const string none = "<style>@page{@bottom-center{content:none}}</style><p>x</p>";
        Assert.That(Pdf(none, new PdfOptions { PageNumbers = true }), Is.EqualTo(Pdf("<p>x</p>", new PdfOptions { PageNumbers = true })),
            "content:none generates no box, so writer page numbers apply.");
    }

    [TestCase("@page :first{margin:1cm}", "FPDF1204")]
    [TestCase("@page cover{margin:1cm}", "FPDF1204")]
    [TestCase("@page :left{@bottom-left{content:'x'}}", "FPDF1204")]
    [TestCase("@page;", "FPDF1204")]
    [TestCase("@page{p{color:red}}", "FPDF1204")]
    [TestCase("@page{@top-left-corner{content:'x'}}", "FPDF1204")]
    [TestCase("@page{@left-middle{content:'x'}}", "FPDF1204")]
    [TestCase("@page{@media print{}}", "FPDF1204")]
    [TestCase("@page{@bottom-center x{content:'x'}}", "FPDF1204")]
    [TestCase("@page{@bottom-center{p{}}}", "FPDF1204")]
    [TestCase("@media screen{@page{margin:1cm}}", "FPDF1204")]
    [TestCase("@page{padding:1cm}", "FPDF1201")]
    [TestCase("@page{margin-inline:1cm}", "FPDF1201")]
    [TestCase("@page{bleed:3mm}", "FPDF1201")]
    [TestCase("@page{@bottom-center{content:'x';text-align:left}}", "FPDF1201")]
    [TestCase("@page{@bottom-center{content:'x';vertical-align:top}}", "FPDF1201")]
    [TestCase("@page{size:A4 10cm}", "FPDF1202")]
    [TestCase("@page{size:10em}", "FPDF1202")]
    [TestCase("@page{size:50%}", "FPDF1202")]
    [TestCase("@page{size:1pt 100pt}", "FPDF1202")]
    [TestCase("@page{size:A4 A5}", "FPDF1202")]
    [TestCase("@page{size:landscape portrait}", "FPDF1202")]
    [TestCase("@page{size:A6}", "FPDF1202")]
    [TestCase("@page{size:auto landscape}", "FPDF1202")]
    [TestCase("@page{margin:10%}", "FPDF1202")]
    [TestCase("@page{margin:auto}", "FPDF1202")]
    [TestCase("@page{size:100pt 100pt;margin:50pt}", "FPDF1202")]
    [TestCase("@page{size:100pt 100pt;margin-left:60pt;margin-right:40pt}", "FPDF1202")]
    [TestCase("@page{@bottom-center{content:counter(page, upper-roman)}}", "FPDF1202")]
    [TestCase("@page{@bottom-center{content:counter(section)}}", "FPDF1202")]
    [TestCase("@page{@bottom-center{content:counter(PAGE)}}", "FPDF1202")]
    [TestCase("@page{@bottom-center{content:counters(page, '.')}}", "FPDF1202")]
    [TestCase("@page{@bottom-center{content:attr(title)}}", "FPDF1202")]
    [TestCase("@page{@bottom-center{content:string(chapter)}}", "FPDF1202")]
    [TestCase("@page{@bottom-center{content:'x' none}}", "FPDF1202")]
    [TestCase("@page{@bottom-center{content:'x';font-size:200pt}}", "FPDF1202")]
    [TestCase("@page{@bottom-center{content:'x';color:nonsense}}", "FPDF1202")]
    [TestCase("@page{@bottom-center{content:'a\\A b'}}", "FPDF1305")]
    public void UnsupportedPageRuleFormsFailExplicitly(string css, string code)
        => Fails("<style>" + css + "</style><p>x</p>", code);

    [Test]
    public void MarginBoxTextMustFitItsMargin()
    {
        Fails("<style>@page{margin:5pt;@bottom-center{content:counter(page)}}</style><p>x</p>", "FPDF1302");
        Fails("<style>@page{size:200pt 200pt;@bottom-left{content:'" + new string('W', 60) + "'}}</style><p>x</p>", "FPDF1302");
        Fails("<style>@page{@top-center{content:'" + new string('x', 1025) + "'}}</style><p>x</p>", "FPDF1001");
    }

    [Test]
    public void PageRulesCountTowardTheDeclarationBudget()
    {
        var css = "<style>@page{" + string.Concat(Enumerable.Repeat("size:A4;", 5)) + "}</style><p>x</p>";
        Fails(css, "FPDF1205", new PdfOptions { MaxCssDeclarations = 4 });
        Assert.That(Pdf(css, new PdfOptions { MaxCssDeclarations = 5 }), Is.Not.Empty);
    }

    [Test]
    public void DocumentsWithoutPageRulesKeepTheirBytes()
    {
        var options = new PdfOptions { PageWidth = 300, PageHeight = 400, Margin = 24, PageNumbers = true };
        Assert.That(Pdf("<style>@page{}</style>" + Body, options), Is.EqualTo(Pdf(Body, options)));
        Assert.That(Effective(Body, options), Is.SameAs(options));
    }

    private static (int Exit, byte[] Pdf, string Stderr) Cli(string html, params string[] options)
    {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(html));
        using var output = new MemoryStream();
        using var error = new StringWriter();
        var exit = CliApplication.Run(["-", "-", .. options], input, output, error);
        return (exit, output.ToArray(), error.ToString());
    }

    [Test]
    public void CliAppliesPageRulesWhenPageOptionsAreOmittedAndRejectsConflicts()
    {
        const string html = "<style>@page{size:A5 landscape;margin:12mm;@bottom-right{content:'Page ' counter(page)}}</style><p>Body</p>";
        var rendered = Cli(html);
        Assert.That(rendered.Exit, Is.Zero, rendered.Stderr);
        Assert.That(rendered.Pdf, Is.EqualTo(Pdf(html)));
        Assert.That(Encoding.ASCII.GetString(rendered.Pdf), Does.Contain(MediaBox(210 * Millimetre, 148 * Millimetre)));

        Assert.That(Cli(html, "--margin", "12mm").Pdf, Is.EqualTo(rendered.Pdf), "An agreeing explicit margin is accepted.");
        string[][] conflicts = [["--page-size", "a4"], ["--landscape"], ["--page-width", "100mm", "--page-height", "100mm"],
            ["--margin", "10mm"], ["--page-numbers"], ["--footer-text", "Internal"]];
        foreach (var conflict in conflicts)
        {
            var failed = Cli(html, conflict);
            Assert.That(failed.Exit, Is.EqualTo(3), string.Join(' ', conflict));
            Assert.That(failed.Stderr, Does.Contain("FPDF1206"), string.Join(' ', conflict));
            Assert.That(failed.Pdf, Is.Empty);
        }
    }
}
