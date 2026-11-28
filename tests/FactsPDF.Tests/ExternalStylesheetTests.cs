using System.Text;
using FactsPDF.CommandLine;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// External stylesheets (link rel=stylesheet and @import) render when the caller supplies their text
/// under the exact href the page writes (R5x-resources-links-media). FactsPDF never downloads: a
/// stylesheet that is not supplied fails FPDF1204, and the supplied CSS goes through the same parser,
/// cascade and budgets as an embedded style element.
/// </summary>
[TestFixture]
public sealed class ExternalStylesheetTests
{
    private static readonly PdfWriter Writer = Pdf.Write().WithPage(p => p.Size(300, 220).Margin(20));
    private const string Body = "<body><p id=total>Total due</p></body></html>";

    private static string Head(string head) => "<!doctype html><html><head><title>T</title>" + head + "</head>" + Body;

    private static FactsPdfException Fails(PdfWriter writer, string html, string code)
    {
        var error = Assert.Throws<FactsPdfException>(() => writer.ToBytes(html))!;
        Assert.That(error.Code, Is.EqualTo(code));
        return error;
    }

    [Test]
    public void ALinkedStylesheetRendersLikeTheSameEmbeddedStyle()
    {
        var linked = Writer.WithStylesheet("css/site.css", "p{color:red;font-size:14pt}")
            .ToBytes(Head("<link rel=stylesheet href=css/site.css>"));
        Assert.That(linked, Is.EqualTo(Writer.ToBytes(Head("<style>p{color:red;font-size:14pt}</style>"))));
    }

    [Test]
    public void LinkedAndEmbeddedSheetsCascadeInDocumentOrder()
    {
        var writer = Writer.WithStylesheet("https://cdn.example/a.css", "p{color:red}");
        var blue = Writer.ToBytes(Head("<style>p{color:blue}</style>"));
        var red = Writer.ToBytes(Head("<style>p{color:red}</style>"));
        Assert.That(writer.ToBytes(Head("<link rel=stylesheet href=\"https://cdn.example/a.css\"><style>p{color:blue}</style>")), Is.EqualTo(blue));
        Assert.That(writer.ToBytes(Head("<style>p{color:blue}</style><link rel=STYLESHEET href=\"https://cdn.example/a.css\" type=text/css crossorigin>")), Is.EqualTo(red));
    }

    [TestCase("@import 'base.css';")]
    [TestCase("@import \"base.css\";")]
    [TestCase("@import url(base.css);")]
    [TestCase("@import url('base.css');")]
    [TestCase("@layer x;@import 'base.css';")]
    public void ImportAppliesTheSuppliedSheetBeforeTheRestOfTheStylesheet(string import)
    {
        var writer = Writer.WithStylesheet("base.css", "p{color:red;margin:0}");
        var html = Head("<style>" + import + "p{margin:4pt}</style>");
        Assert.That(writer.ToBytes(html), Is.EqualTo(Writer.ToBytes(Head("<style>p{color:red;margin:0}p{margin:4pt}</style>"))));
    }

    [Test]
    public void ImportsNestInsideSuppliedSheets()
    {
        var writer = Writer.WithStylesheet("a.css", "@import 'b.css';p{font-size:14pt}").WithStylesheet("b.css", "p{color:red}");
        Assert.That(writer.ToBytes(Head("<link rel=stylesheet href=a.css>")),
            Is.EqualTo(Writer.ToBytes(Head("<style>p{color:red}p{font-size:14pt}</style>"))));
    }

    [Test]
    public void ImportConditionsDecideWhetherTheSheetApplies()
    {
        var writer = Writer.WithStylesheet("print.css", "p{color:red}");
        var red = Writer.ToBytes(Head("<style>p{color:red}</style>"));
        var plain = Writer.ToBytes(Head(""));
        Assert.That(writer.ToBytes(Head("<style>@import 'print.css' print;</style>")), Is.EqualTo(red));
        Assert.That(writer.ToBytes(Head("<style>@import 'print.css' supports(display:grid) print and (color);</style>")), Is.EqualTo(red));
        Assert.That(writer.ToBytes(Head("<style>@import 'print.css' screen;</style>")), Is.EqualTo(plain));
        Assert.That(writer.ToBytes(Head("<style>@import 'print.css' supports(display:bogus);</style>")), Is.EqualTo(plain));
        // A sheet whose condition does not hold is never needed, so it need not be supplied.
        Assert.That(Writer.ToBytes(Head("<style>@import 'missing.css' screen;</style>")), Is.EqualTo(plain));
        Assert.That(Writer.ToBytes(Head("<link rel=stylesheet href=missing.css media=screen>")), Is.EqualTo(plain));
        Assert.That(writer.ToBytes(Head("<link rel=stylesheet href=print.css media=\"print and (orientation:portrait)\">")), Is.EqualTo(red));
    }

    [Test]
    public void ImportLayerPutsTheSheetInACascadeLayer()
    {
        var writer = Writer.WithStylesheet("theme.css", "#total{color:red}");
        // Unlayered declarations beat layered ones whatever their specificity.
        Assert.That(writer.ToBytes(Head("<style>@import 'theme.css' layer(theme);p{color:blue}</style>")),
            Is.EqualTo(Writer.ToBytes(Head("<style>p{color:blue}</style>"))));
        Assert.That(writer.ToBytes(Head("<style>@import 'theme.css' layer;p{color:blue}</style>")),
            Is.EqualTo(Writer.ToBytes(Head("<style>p{color:blue}</style>"))));
        // Over-correction: without a layer the more specific rule wins.
        Assert.That(writer.ToBytes(Head("<style>@import 'theme.css';p{color:blue}</style>")),
            Is.EqualTo(Writer.ToBytes(Head("<style>p{color:red}</style>"))));
        Fails(writer, Head("<style>@import 'theme.css' layer(theme) screen;</style>"), "FPDF1204");
    }

    [Test]
    public void AStylesheetThatIsNotSuppliedFailsAtItsLinkOrImport()
    {
        var html = Head("<link rel=stylesheet href=/site.css>");
        var error = Fails(Writer, html, "FPDF1204");
        Assert.That(error.SourceOffset, Is.EqualTo(html.IndexOf("<link", StringComparison.Ordinal)));
        Assert.That(error.Message, Does.Contain("never downloads"));
        html = Head("<style>@import 'https://example.invalid/a.css';</style>");
        error = Fails(Writer, html, "FPDF1204");
        Assert.That(error.SourceOffset, Is.EqualTo(html.IndexOf("@import", StringComparison.Ordinal)));
        // The href matches exactly: case and base resolution do not apply.
        Fails(Writer.WithStylesheet("Site.css", "p{}"), Head("<link rel=stylesheet href=site.css>"), "FPDF1204");
        Fails(Writer.WithStylesheet("https://example.com/site.css", "p{}"),
            "<head><base href=\"https://example.com/\"><link rel=stylesheet href=site.css></head><p>A</p>", "FPDF1204");
        // Non-strict mode cannot drop it either: Chrome would apply whatever the URL serves.
        Fails(Writer.WithStrict(false), Head("<link rel=stylesheet href=/site.css>"), "FPDF1204");
    }

    [Test]
    public void ErrorsInsideASuppliedSheetReportTheLinkAndTheSheet()
    {
        var html = Head("<link rel=stylesheet href=bad.css>");
        var error = Fails(Writer.WithStylesheet("bad.css", "p{display:inline-flex}"), html, "FPDF1202");
        Assert.That(error.SourceOffset, Is.EqualTo(html.IndexOf("<link", StringComparison.Ordinal)));
        Assert.That(error.Message, Does.Contain("In stylesheet 'bad.css'"));
    }

    [Test]
    public void ImportsAfterOtherRulesCyclesAndDepthAreBounded()
    {
        Fails(Writer.WithStylesheet("a.css", "p{}"), Head("<style>p{color:blue}@import 'a.css';</style>"), "FPDF1204");
        var ignored = Writer.WithStrict(false).WithStylesheet("a.css", "p{color:red}")
            .ToBytes(Head("<style>p{color:blue}@import 'a.css';</style>"));
        Assert.That(ignored, Is.EqualTo(Writer.WithStrict(false).ToBytes(Head("<style>p{color:blue}</style>"))));

        Fails(Writer.WithStylesheet("a.css", "@import 'b.css';").WithStylesheet("b.css", "@import 'a.css';"),
            Head("<link rel=stylesheet href=a.css>"), "FPDF1204");
        var chain = Writer;
        for (var i = 0; i < 16; i++) chain = chain.WithStylesheet($"s{i}.css", $"@import 's{i + 1}.css';");
        chain = chain.WithLimits(l => l.MaxStylesheetResources(17)).WithStylesheet("s16.css", "p{color:red}");
        Fails(chain, Head("<link rel=stylesheet href=s0.css>"), "FPDF1205");
        // Over-correction: the same sheet imported twice (not a cycle) applies twice.
        Assert.DoesNotThrow(() => Writer.WithStylesheet("a.css", "p{color:red}")
            .ToBytes(Head("<style>@import 'a.css';@import 'a.css';</style>")));
    }

    [Test]
    public void SuppliedSheetsCountAgainstTheCssBudgetsAndResourceLimit()
    {
        var css = "p{color:red}";
        Fails(Writer.WithLimits(l => l.MaxCssCharacters(css.Length + 5)).WithStylesheet("a.css", css),
            Head("<link rel=stylesheet href=a.css><link rel=stylesheet href=a.css>"), "FPDF1205");
        Fails(Writer.WithLimits(l => l.MaxStylesheetResources(1)).WithStylesheet("a.css", css).WithStylesheet("b.css", css),
            Head(""), "FPDF1205");
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        Assert.Catch<OperationCanceledException>(() => Writer.WithStylesheet("a.css", css).ToBytes(Head("<link rel=stylesheet href=a.css>"), cancelled.Token));
    }

    [TestCase("<link rel=stylesheet href=a.css title=Main>", "FPDF1103")]
    [TestCase("<link rel=stylesheet href=a.css integrity=sha384-x>", "FPDF1103")]
    [TestCase("<link rel=stylesheet href=a.css disabled>", "FPDF1103")]
    [TestCase("<link rel=stylesheet href=''>", "FPDF1103")]
    [TestCase("<link rel=stylesheet>", "FPDF1103")]
    [TestCase("<link rel=stylesheet href=a.css type=text/less>", "FPDF1204")]
    [TestCase("<link rel='alternate stylesheet' href=a.css>", "FPDF1204")]
    [TestCase("<link rel=stylesheet href=a.css media='(hover)'>", "FPDF1204")]
    public void UnsupportedStylesheetLinksFail(string link, string code)
        => Fails(Writer.WithStylesheet("a.css", "p{color:red}"), Head(link), code);

    [Test]
    public void TheSdkValidatesHrefsAndReplacesBySameHref()
    {
        Assert.Throws<ArgumentException>(() => Writer.WithStylesheet("a b.css", "p{}"));
        Assert.Throws<ArgumentException>(() => Writer.WithStylesheet("", "p{}"));
        Assert.Throws<ArgumentException>(() => Writer.WithStylesheet(new string('a', 2049), "p{}"));
        Assert.Throws<ArgumentNullException>(() => Writer.WithStylesheet("a.css", null!));
        var replaced = Writer.WithStylesheet("a.css", "p{color:blue}").WithStylesheet("a.css", "p{color:red}");
        Assert.That(replaced.ToBytes(Head("<link rel=stylesheet href=a.css>")), Is.EqualTo(Writer.ToBytes(Head("<style>p{color:red}</style>"))));
    }

    [Test]
    public void TheCliSuppliesStylesheetsByHref()
    {
        var directory = Directory.CreateTempSubdirectory("factspdf-css-");
        try
        {
            var css = Path.Combine(directory.FullName, "site.css");
            File.WriteAllText(css, "p{color:red}");
            var html = Head("<link rel=stylesheet href=css/site.css>");
            using var input = new MemoryStream(Encoding.UTF8.GetBytes(html));
            using var output = new MemoryStream();
            using var diagnostics = new StringWriter();
            Assert.That(CliApplication.Run(["-", "-", "--page-width", "300pt", "--page-height", "220pt", "--margin", "20pt",
                "--stylesheet", "css/site.css=" + css], input, output, diagnostics), Is.Zero, diagnostics.ToString());
            Assert.That(output.ToArray(), Is.EqualTo(Writer.ToBytes(Head("<style>p{color:red}</style>"))));
        }
        finally { directory.Delete(recursive: true); }
    }
}
