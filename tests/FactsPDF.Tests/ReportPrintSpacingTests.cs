using System.Text;
using FactsPDF.CommandLine;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>Print-unit lengths plus the truthful vertical-only paragraph margin shorthand.</summary>
[TestFixture]
public sealed class ReportPrintSpacingTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 500, Margin = 20 };

    private static byte[] Render(string html, PdfOptions? options = null)
    {
        using var pdf = new MemoryStream();
        PdfConverter.Convert(html, pdf, options ?? Page);
        return pdf.ToArray();
    }

    private static void Reject(string css, string tag = "p", string expectedCode = "FPDF1202")
    {
        var html = $"<style>.never{{{css}}}</style><{tag} style='{css}'>Report</{tag}>";
        using var destination = new MemoryStream();
        destination.Write("unchanged"u8);
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, destination, Page));
        Assert.That(error!.Code, Is.EqualTo(expectedCode));
        Assert.That(Encoding.ASCII.GetString(destination.ToArray()), Is.EqualTo("unchanged"));
    }

    [TestCase("margin:0", "margin-top:0;margin-bottom:0")]
    [TestCase("margin:10mm 0", "margin-top:10mm;margin-bottom:10mm")]
    [TestCase("margin:1cm 0 2mm", "margin-top:1cm;margin-bottom:2mm")]
    [TestCase("margin:1in 0 12pt 0", "margin-top:1in;margin-bottom:12pt")]
    [TestCase("margin:0pt 0px 0cm 0mm", "margin-top:0pt;margin-bottom:0cm")]
    [TestCase("margin:inherit", "margin-top:inherit;margin-bottom:inherit")]
    [TestCase("margin:initial", "margin-top:initial;margin-bottom:initial")]
    [TestCase("margin:unset", "margin-top:unset;margin-bottom:unset")]
    public void ValidShorthandMatchesExplicitLonghandsInRealPdf(string shorthand, string longhands)
    {
        const string before = "<div style='background:#cee8fb;padding:2mm'><h1 style='";
        const string after = "'>Maintenance report</h1><p>Checked machine safety.</p></div>";
        Assert.That(Render(before + shorthand + after), Is.EqualTo(Render(before + longhands + after)));
    }

    [Test]
    public void InlineAndStylesheetMarginUsePerLonghandCascadeAndImportant()
    {
        const string html = "<style>.x{margin:10pt 0 4pt;margin-bottom:20pt!important}" +
            "p.x{margin-top:6pt}</style><p class=x style='margin:12pt 0 14pt'>A</p>";
        var p = HtmlDocumentReader.Read(html, Page, CancellationToken.None).Single();
        Assert.That(p.Style.MarginBefore, Is.EqualTo(12));
        Assert.That(p.Style.MarginAfter, Is.EqualTo(20));

        const string matching = "<p style='margin-top:12pt;margin-bottom:20pt'>A</p>";
        Assert.That(Render(html), Is.EqualTo(Render(matching)));
    }

    [Test]
    public void PhysicalPrintUnitsAffectRealGeometryAndFontSize()
    {
        const string html = "<div style='width:2in;padding-left:6.35mm;border-left:0.25cm solid blue'>" +
            "<p style='font-size:0.25in;margin:0 0 0.5cm'>ITEM</p><p>TAIL</p></div>";
        var tree = HtmlDocumentReader.ReadTree(html, Page, default);
        var div = (BlockNode)tree.Children.Single();
        Assert.That(div.BoxStyle.Width!.Value.Value, Is.EqualTo(144).Within(0.00001));
        Assert.That(div.BoxStyle.PaddingLeft.Value, Is.EqualTo(18).Within(0.00001));
        Assert.That(div.BoxStyle.BorderLeft.EffectiveWidth, Is.EqualTo(72d / 2.54d * 0.25d).Within(0.00001));
        var runs = TextLayout.Layout(tree, Page, default)[0].Runs;
        Assert.That(runs.Select(x => x.Text), Is.EqualTo(new[] { "ITEM", "TAIL" }));
        Assert.That(runs[0].X, Is.EqualTo(20 + 18 + 72d / 2.54d * 0.25d).Within(0.00001));
        Assert.That(runs[0].Style.FontSize, Is.EqualTo(18).Within(0.00001));
        Assert.That(runs[0].Baseline - runs[1].Baseline, Is.GreaterThan(25));
    }

    [Test]
    public void InchCentimetreMillimetreAndPixelEquivalentsMatchActualPdfBytes()
    {
        const string start = "<div style='width:144pt;padding-left:18pt;border:9pt solid red'>" +
            "<h2 style='font-size:18pt;margin:9pt 0 18pt'>Business report</h2></div>";
        var physical = start.Replace("144pt", "2in").Replace("18pt", "0.25in").Replace("9pt", "0.125in");
        Assert.That(Render(physical), Is.EqualTo(Render(start)));

        var cm = Render("<p style='font-size:2.54cm;margin-top:0'>A</p>");
        var inch = Render("<p style='font-size:1in;margin-top:0'>A</p>");
        Assert.That(cm, Is.EqualTo(inch));
        var mm = Render("<div style='padding-left:25.4mm'><p>A</p></div>");
        var pt = Render("<div style='padding-left:72pt'><p>A</p></div>");
        Assert.That(mm, Is.EqualTo(pt));
        var px = Render("<div style='padding-left:96px'><p>A</p></div>");
        Assert.That(px, Is.EqualTo(pt));
    }

    [Test]
    public void ShorthandSurvivesExplicitPageBreakAndRetainsOrderedText()
    {
        const string shorthand = "<p style='margin:3mm 0 7mm'>First</p>" +
            "<p style='break-before:page;margin:5mm 0 6mm'>Second</p>";
        const string longhands = "<p style='margin-top:3mm;margin-bottom:7mm'>First</p>" +
            "<p style='break-before:page;margin-top:5mm;margin-bottom:6mm'>Second</p>";
        var a = Render(shorthand, Page with { PageHeight = 100 });
        var b = Render(longhands, Page with { PageHeight = 100 });
        Assert.That(a, Is.EqualTo(b));
        using var stream = new MemoryStream(a);
        Assert.That(Encoding.ASCII.GetString(stream.ToArray()), Does.Contain("/Count 2"));
    }

    [Test]
    public void ManagedCliMatchesTypedPdfOutputForPrintSpacing()
    {
        const string html = "<style>.card{width:2in;padding:2mm;border:0.5mm solid blue}" +
            "h1{margin:0 0 5mm}p{margin:0 0 3mm}</style>" +
            "<div class=card><h1>Inspection</h1><p>Service summary</p></div>";
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(html));
        using var actual = new MemoryStream();
        using var diagnostics = new StringWriter();
        var result = CliApplication.Run(["-", "-"], input, actual, diagnostics);
        Assert.That(result, Is.Zero, diagnostics.ToString());
        using var expected = new MemoryStream();
        PdfConverter.Convert(html, expected); // Native entry shares this converter.
        Assert.That(actual.ToArray(), Is.EqualTo(expected.ToArray()));
    }

    [TestCase("margin:12pt")] // One side actually means four sides in standard CSS.
    [TestCase("margin:2pt 1pt")]
    [TestCase("margin:2pt 0 3pt 4pt")]
    [TestCase("margin:0 0 auto")]
    [TestCase("margin:0 0 -1mm")]
    [TestCase("margin:0%")]
    [TestCase("margin:inherit 0")]
    [TestCase("margin:0 0 0 0 0")]
    [TestCase("margin:calc(1mm + 2mm) 0")]
    [TestCase("width:1em")]
    [TestCase("padding:1rem")]
    [TestCase("border-width:3%")]
    [TestCase("font-size:300mm")]
    [TestCase("padding:5100mm")]
    public void UnsupportedValuesRejectWithNoDestinationMutation(string css)
        => Reject(css);

    [Test]
    public void MarginRemainsParagraphOnlyAndUnmatchedRulesStillValidate()
    {
        Reject("margin:2mm 0", "div", "FPDF1201");
        using var target = new MemoryStream();
        target.Write("still intact"u8);
        var error = Assert.Throws<FactsPdfException>(() =>
            PdfConverter.Convert("<style>.unmatched{margin:1mm}</style><p>safe</p>", target, Page));
        Assert.That(error!.Code, Is.EqualTo("FPDF1202"));
        Assert.That(Encoding.ASCII.GetString(target.ToArray()), Is.EqualTo("still intact"));
    }

    [Test]
    public void ExpandedMarginValuesCountTowardExistingCssBudget()
    {
        using var target = new MemoryStream();
        target.Write("persist"u8);
        var ex = Assert.Throws<FactsPdfException>(() =>
            PdfConverter.Convert("<p style='margin:2mm 0'>A</p>", target,
                Page with { MaxCssDeclarations = 1 }));
        Assert.That(ex!.Code, Is.EqualTo("FPDF1205"));
        Assert.That(Encoding.ASCII.GetString(target.ToArray()), Is.EqualTo("persist"));
    }
}
