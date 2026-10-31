using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// The bounded LTR-only logical text alignment values must result in the
/// same measured glyph coordinates and PDF bytes as physical alignment.
/// </summary>
[TestFixture]
public sealed class LogicalTextAlignmentTests
{
    private static readonly PdfOptions SmallPage = new()
    {
        PageWidth = 180, PageHeight = 180, Margin = 18
    };

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

    private static void Reject(string html, string expected)
    {
        using var output = new MemoryStream();
        output.Write("previous-pdf"u8);
        output.Position = 3;
        var error = Assert.Throws<FactsPdfException>(() =>
            PdfConverter.Convert(html, output, SmallPage));
        Assert.That(error!.Code, Is.EqualTo(expected));
        Assert.That(output.ToArray(), Is.EqualTo("previous-pdf"u8.ToArray()));
        Assert.That(output.Position, Is.EqualTo(3));
    }

    [TestCase("start", "left", false)]
    [TestCase("end", "right", true)]
    public void ParagraphAndHeadingValuesPaintAtPhysicalSideCoordinates(
        string logical, string physical, bool right)
    {
        var source = "<h2 style='text-align:" + logical + "'>Quality</h2>" +
                     "<p style='text-align:" + logical + "'>Invoice AB-24</p>";
        var oracle = "<h2 style='text-align:" + physical + "'>Quality</h2>" +
                     "<p style='text-align:" + physical + "'>Invoice AB-24</p>";
        Assert.That(Pdf(source), Is.EqualTo(Pdf(oracle)),
            "A logical value must reach glyph painting, not only parse.");
        var actual = Layout(source).SelectMany(p => p.Runs).ToArray();
        var expected = Layout(oracle).SelectMany(p => p.Runs).ToArray();
        Assert.That(actual.Select(r => r.X), Is.EqualTo(expected.Select(r => r.X)));
        var invoice = actual.First(r => r.Text.Contains("Invoice", StringComparison.Ordinal));
        Assert.That(invoice.X, right ? Is.GreaterThan(50) : Is.EqualTo(18).Within(.001));
    }

    [Test]
    public void EmbeddedRulesInheritanceInlineImportantAndWideKeywordsUseComputedSides()
    {
        const string logical = "<style>section{text-align:end}p{text-align:start}" +
            "p.reset{text-align:initial!important}p.inherit{text-align:inherit!important}" +
            "p.end{text-align:end!important}</style>" +
            "<section><p>A</p><p class='inherit'>B</p><p class='reset'>C</p>" +
            "<p class='end' style='text-align:start'>D</p></section>";
        const string physical = "<style>section{text-align:right}p{text-align:left}" +
            "p.reset{text-align:initial!important}p.inherit{text-align:inherit!important}" +
            "p.end{text-align:right!important}</style>" +
            "<section><p>A</p><p class='inherit'>B</p><p class='reset'>C</p>" +
            "<p class='end' style='text-align:left'>D</p></section>";
        Assert.That(Pdf(logical), Is.EqualTo(Pdf(physical)));
        var paragraphs = HtmlDocumentReader.Read(logical, SmallPage, default);
        Assert.That(paragraphs.Select(p => p.Style.Alignment),
            Is.EqualTo(new[] { TextAlignment.Left, TextAlignment.Right, TextAlignment.Left, TextAlignment.Right }));
    }

    [Test]
    public void PaginatedReportUsesActualPerLineAlignmentAndRetainsText()
    {
        static string Report(string a, string b) =>
            "<style>section{text-align:" + b + "}p.left{text-align:" + a + "!important}</style>" +
            "<section>" + string.Concat(Enumerable.Range(1, 46).Select(i =>
                "<p class='" + (i % 2 == 0 ? "left" : "right") + "'>Ref " +
                i.ToString("00", System.Globalization.CultureInfo.InvariantCulture) +
                " : service audit item</p>")) + "</section>";
        var logical = Report("start", "end");
        var physical = Report("left", "right");
        var pages = Layout(logical);
        Assert.That(pages.Count, Is.GreaterThan(1));
        Assert.That(pages.SelectMany(p => p.Runs).Count(r => r.Text.Contains("Ref", StringComparison.Ordinal)),
            Is.EqualTo(46));
        Assert.That(Pdf(logical), Is.EqualTo(Pdf(physical)));
    }

    [Test]
    public void ExplicitTrueTypeFontAndInlineColoursUseTheSameGlyphMetrics()
    {
        var font = PdfFont.LoadTrueType(FontFixture.Create());
        var opts = SmallPage with { Fonts = [font] };
        const string source = "<section style='text-align:end'><p>" +
            "<span style='color:blue'>AB CD</span> <span style='color:red'>EF GH</span>" +
            "</p></section>";
        const string oracle = "<section style='text-align:right'><p>" +
            "<span style='color:blue'>AB CD</span> <span style='color:red'>EF GH</span>" +
            "</p></section>";
        var actual = Pdf(source, opts);
        Assert.That(actual, Is.EqualTo(Pdf(oracle, opts)));
        Assert.That(Encoding.Latin1.GetString(actual), Does.Contain("/FontFile2"));
    }

    [TestCase("<p style='text-align:match-parent'>A</p>", "FPDF1202")]
    [TestCase("<p style='text-align:justify-all'>A</p>", "FPDF1202")]
    [TestCase("<p style='text-align:end start'>A</p>", "FPDF1202")]
    [TestCase("<style>.unused{text-align:logical}</style><p>A</p>", "FPDF1202")]
    [TestCase("<p><span style='text-align:end'>A</span></p>", "FPDF1201")]
    [TestCase("<p style='direction:rtl;text-align:start'>A</p>", "FPDF1201")]
    public void UnsupportedDirectionValuesAndInlineUsageKeepOriginalOutput(string html, string code)
        => Reject(html, code);
}
