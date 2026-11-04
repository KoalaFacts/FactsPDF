using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

[TestFixture]
public sealed class SemanticInlineReportTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 260, PageHeight = 180, Margin = 12 };

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

    private static void Reject(string html, string code, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        output.Write("existing-report"u8);
        output.Position = 4;
        var ex = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, options ?? Page))!;
        Assert.Multiple(() =>
        {
            Assert.That(ex.Code, Is.EqualTo(code));
            Assert.That(output.ToArray(), Is.EqualTo("existing-report"u8.ToArray()));
            Assert.That(output.Position, Is.EqualTo(4));
        });
    }

    [Test]
    public void FiveReportInlineTagsDrawRealMeasuredGlyphAndDecorationStyles()
    {
        const string source = "<p style='font-size:12pt;color:#234567'>AB " +
            "<small>AB</small> <u>AB</u> <s>AB</s> <del>AB</del> <ins>AB</ins></p>";
        const string oracle = "<p style='font-size:12pt;color:#234567'>AB " +
            "<span style='font-size:10pt'>AB</span> " +
            "<span style='text-decoration:underline'>AB</span> " +
            "<span style='text-decoration:line-through'>AB</span> " +
            "<span style='text-decoration:line-through'>AB</span> " +
            "<span style='text-decoration:underline'>AB</span></p>";
        var runs = Layout(source).SelectMany(p => p.Runs).Where(r => r.Text == "AB").ToArray();
        Assert.That(runs.Length, Is.EqualTo(6));
        Assert.That(runs[1].Style.FontSize, Is.EqualTo(10d).Within(0.000001));
        Assert.That(runs[2].Style.Decoration, Is.EqualTo(TextDecoration.Underline));
        Assert.That(runs[3].Style.Decoration, Is.EqualTo(TextDecoration.LineThrough));
        Assert.That(runs[4].Style.Decoration, Is.EqualTo(TextDecoration.LineThrough));
        Assert.That(runs[5].Style.Decoration, Is.EqualTo(TextDecoration.Underline));
        Assert.That(Pdf(source), Is.EqualTo(Pdf(oracle)));
        Assert.That(Pdf(source), Is.Not.EqualTo(Pdf("<p>AB AB AB AB AB AB</p>")));
    }

    [Test]
    public void ImportantCssOverridesTagDefaultsAndNestedSmallUsesParentSize()
    {
        const string source = "<style>small.note{font-size:14pt!important}" +
            "del.cancelled{text-decoration:none!important}</style>" +
            "<p style='font-size:12pt'><small class=note style='font-size:7pt'>AB</small> " +
            "<del class=cancelled>AB</del> <small><u>AB</u></small> " +
            "<ins style='text-decoration:line-through'>AB</ins></p>";
        const string oracle = "<p style='font-size:12pt'>" +
            "<span style='font-size:14pt'>AB</span> AB " +
            "<span style='font-size:10pt'><span style='text-decoration:underline'>AB</span></span> " +
            "<span style='text-decoration:line-through'>AB</span></p>";
        Assert.That(Pdf(source), Is.EqualTo(Pdf(oracle)));
    }

    [Test]
    public void SuppliedTrueTypeUsesExistingFontAndInkPaths()
    {
        var font = PdfFont.LoadTrueType(FontFixture.Create());
        var options = Page with { Fonts = [font] };
        const string source = "<p style='font-size:12pt'>AB <small>AB</small> <del>AB</del> <ins>AB</ins></p>";
        const string oracle = "<p style='font-size:12pt'>AB <span style='font-size:10pt'>AB</span> " +
            "<span style='text-decoration:line-through'>AB</span> " +
            "<span style='text-decoration:underline'>AB</span></p>";
        Assert.That(Layout(source, options).SelectMany(p => p.Runs).All(run => ReferenceEquals(run.Font, font)), Is.True);
        Assert.That(Pdf(source, options), Is.EqualTo(Pdf(oracle, options)));
    }

    [Test]
    public void FortyFourUpdatedQuotesHaveRealPagesAndExactExplicitCssOracle()
    {
        var source = Example("report-inline-changes.html");
        var control = Example("report-inline-changes-oracle.html");
        var pages = Layout(source);
        Assert.That(pages.Count, Is.GreaterThan(1));
        Assert.That(pages.Sum(p => p.Runs.Count(r => r.Style.Decoration != TextDecoration.None)), Is.GreaterThan(150));
        Assert.That(Pdf(source), Is.EqualTo(Pdf(control)));
        var pdf = Encoding.Latin1.GetString(Pdf(source));
        Assert.That(pdf, Does.Contain(" re f Q"));
        Reject(source, "FPDF1303", Page with { MaxPages = 1 });
    }


    [Test]
    public void NewInlineTagsWorkInFixedTableCellsAndFlatCellParagraphs()
    {
        const string before = "<table style='width:140pt'><colgroup><col style='width:140pt'></colgroup>" +
            "<tbody><tr><td style='padding:3pt'>";
        const string after = "</td></tr></tbody></table>";
        const string source = "AB <small>AB</small> <del>AB</del> <ins>AB</ins> " +
            "<p>AB <u>AB</u> <s>AB</s></p>";
        const string oracle = "AB <span style='font-size:10pt'>AB</span> " +
            "<span style='text-decoration:line-through'>AB</span> " +
            "<span style='text-decoration:underline'>AB</span> " +
            "<p>AB <span style='text-decoration:underline'>AB</span> " +
            "<span style='text-decoration:line-through'>AB</span></p>";
        var sample = before + source + after;
        Assert.That(Pdf(sample), Is.EqualTo(Pdf(before + oracle + after)));
        Assert.That(Layout(sample).SelectMany(page => page.Runs).Any(run =>
            run.Style.Decoration == TextDecoration.Underline), Is.True);
        Assert.That(Layout(sample).SelectMany(page => page.Runs).Any(run =>
            run.Style.Decoration == TextDecoration.LineThrough), Is.True);
    }

    [TestCase("small", "justify")]
    [TestCase("u", "start")]
    [TestCase("s", "end")]
    [TestCase("del", "justify")]
    [TestCase("ins", "justify")]
    public void UnsupportedInlineAlignmentFailsClosedInsteadOfBeingIgnored(string tag, string alignment)
    {
        Reject($"<p>A <{tag} style='text-align:{alignment}'>AB</{tag}></p>", "FPDF1201");
        Reject($"<style>{tag}.unused{{text-align:{alignment}}}</style>" +
            $"<p><{tag} class='unused'>AB</{tag}></p>", "FPDF1201");
    }

    [TestCase("<small style='font-size:999pt'>AB</small>", "FPDF1202")]
    [TestCase("<p style='font-size:1pt'><small>AB</small></p>", "FPDF1202")]
    [TestCase("<del datetime='2026-11-01'>AB</del>", "FPDF1103")]
    [TestCase("<ins cite='https://example.com'>AB</ins>", "FPDF1103")]
    [TestCase("<small><div>Bad nesting</div></small>", "FPDF1101")]
    [TestCase("<p><u>Unclosed</p>", "FPDF1101")]
    [TestCase("<style>.absent{font-size:nope}</style><small>AB</small>", "FPDF1202")]
    public void InvalidValuesAndUnsupportedMarkupPreserveDestination(string html, string code)
        => Reject(html, code);

    private static string Example(string name)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FactsPDF.slnx")))
            directory = directory.Parent;
        Assert.That(directory, Is.Not.Null);
        return File.ReadAllText(Path.Combine(directory!.FullName, "examples", name));
    }
}
