using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace FactsPDF.Tests;

// An img or svg with the default display:inline is an atomic inline in its paragraph's line boxes,
// as in Chrome: its bottom sits on the baseline (so the line keeps the strut's descender space
// below it), vertical-align moves it, it wraps as one unit and a link covers its box.
// display:block keeps the block layout. Before, both were always laid out as blocks.
// No fonts: built-in Courier at 12pt with line-height 1.2, whose strut is 10.8pt above the
// baseline and 3.6pt below it, and whose x-height is 0.426em. A4, 36pt margins.
public sealed class InlineReplacedElementTests
{
    private const double PageHeight = 841.89;
    private const string Svg20 = "<svg width='20' height='20'></svg>"; // 15 by 15pt

    private static PdfOptions Options() => new()
    { Images = new Dictionary<string, PdfImage> { ["gray"] = PdfImage.FromBytes(JpegResourceTests.Bytes("gray")) } };

    private static LayoutPage Page(string html, PdfOptions? options = null)
        => TableLayoutTests.Layout(html, options ?? Options()).Single();

    private static string Text(string html, PdfOptions? options = null)
        => Encoding.Latin1.GetString(TableLayoutTests.Pdf(html, options ?? Options()));

    private static FactsPdfException Reject(string html, string code, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        output.Write("retained"u8);
        var failure = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, options ?? Options()))!;
        Assert.That(failure.Code, Is.EqualTo(code), failure.Message);
        Assert.That(Encoding.ASCII.GetString(output.ToArray()), Is.EqualTo("retained"));
        return failure;
    }

    // Line layout --------------------------------------------------------------------------------

    [Test]
    public void LogoHeaderKeepsTheDescenderGapBelowADefaultInlineSvg()
    {
        // A 120 by 36pt logo alone in a div: the line is 36pt above the baseline and the strut's
        // 3.6pt below it, so the next paragraph starts 39.6pt down, not 36pt (Chrome: about 4px).
        const string logo = "<svg width='160' height='48'{0}><rect width='160' height='48'/></svg>";
        const string next = "<p style='margin:0'>Next</p>";
        var inline = Page("<div>" + string.Format(CultureInfo.InvariantCulture, logo, "") + "</div>" + next);
        Assert.That(inline.Images.Single().Top, Is.EqualTo(36).Within(1e-9));
        Assert.That(inline.Runs.Single().Baseline, Is.EqualTo(PageHeight - 36 - 39.6 - 10.8).Within(1e-9));
        var block = Page("<div>" + string.Format(CultureInfo.InvariantCulture, logo, " style='display:block'") + "</div>" + next);
        Assert.That(block.Images.Single().Top, Is.EqualTo(36).Within(1e-9));
        Assert.That(block.Runs.Single().Baseline, Is.EqualTo(PageHeight - 36 - 36 - 10.8).Within(1e-9));
        var image = Page("<div><img src='gray'></div>" + next); // 48 by 24pt
        Assert.That(image.Images.Single().Top, Is.EqualTo(36).Within(1e-9));
        Assert.That(image.Runs.Single().Baseline, Is.EqualTo(PageHeight - 36 - 27.6 - 10.8).Within(1e-9));
    }

    [Test]
    public void InlineImagesSitOnTheBaselineBetweenTextRuns()
    {
        var page = Page("<p style='margin:0'>AB" + Svg20 + "CD</p>");
        var image = page.Images.Single();
        Assert.That(image.X, Is.EqualTo(36 + 14.4).Within(1e-9));
        Assert.That(image.Top, Is.EqualTo(36).Within(1e-9));
        Assert.That(page.Runs.Select(r => r.Text), Is.EqualTo(new[] { "AB", "CD" }));
        Assert.That(page.Runs.Select(r => r.Baseline), Is.All.EqualTo(PageHeight - 36 - 15).Within(1e-9));
        Assert.That(page.Runs[1].X, Is.EqualTo(36 + 14.4 + 15).Within(1e-9));

        // Collapsed spaces around the image stay, one on each side.
        var spaced = Page("<p style='margin:0'>A " + Svg20 + " B</p>");
        Assert.That(spaced.Images.Single().X, Is.EqualTo(36 + 14.4).Within(1e-9));
        Assert.That(spaced.Runs.Select(r => r.Text), Is.EqualTo(new[] { "A ", " B" }));
        Assert.That(spaced.Runs[1].X, Is.EqualTo(36 + 14.4 + 15).Within(1e-9));
    }

    // Each case: the text baseline and the 15pt svg's top. Middle centres the svg 2.556pt
    // (half the x-height) above the baseline; text-top and text-bottom use Courier's 9.6pt
    // ascent and 2.4pt descent; top and bottom align to the line box; a length or percentage
    // (of the 14.4pt line-height) raises the svg's bottom above the baseline.
    [TestCase("baseline", 15, 36)]
    [TestCase("middle", 10.8, 36 + 10.8 - 10.056)]
    [TestCase("text-top", 10.8, 36 + 10.8 - 9.6)]
    [TestCase("text-bottom", 12.6, 36)]
    [TestCase("top", 10.8, 36)]
    [TestCase("bottom", 11.4, 36)]
    [TestCase("3pt", 18, 36)]
    [TestCase("0.25em", 18, 36)]
    [TestCase("4px", 18, 36)]
    [TestCase("-3pt", 12, 36)]
    [TestCase("50%", 22.2, 36)]
    public void VerticalAlignPlacesTheImageInTheLineBox(string align, double ascent, double top)
    {
        var page = Page($"<p style='margin:0'>X<svg width='20' height='20' style='vertical-align:{align}'></svg></p>");
        Assert.That(page.Runs.Single().Baseline, Is.EqualTo(PageHeight - 36 - ascent).Within(1e-9));
        Assert.That(page.Images.Single().Top, Is.EqualTo(top).Within(1e-9));
    }

    [Test]
    public void LaterVerticalAlignBaselineWinsTheCascade()
        => Assert.That(Text("<style>svg{vertical-align:middle}svg.logo{vertical-align:baseline}</style>"
                + "<p style='margin:0'>X<svg class='logo' width='20' height='20'></svg></p>"),
            Is.EqualTo(Text("<p style='margin:0'>X" + Svg20 + "</p>")));

    [Test]
    public void ImagesWrapAsAtomicInlines()
    {
        // Two 30pt svgs fill a 60pt line; the third wraps. The first line is 15 + 3.6pt high.
        var page = Page("<div style='width:60pt'>" + string.Concat(Enumerable.Repeat("<svg width='40' height='20'></svg>", 3)) + "</div>");
        Assert.That(page.Images.Select(i => i.X), Is.EqualTo(new[] { 36d, 66d, 36d }).Within(1e-9));
        Assert.That(page.Images.Select(i => i.Top), Is.EqualTo(new[] { 36d, 36d, 36 + 18.6 }).Within(1e-9));

        // There is a soft wrap opportunity before an image even right after a word.
        var wrapped = Page("<div style='width:40pt'>ABCD" + Svg20 + "</div>");
        Assert.That(wrapped.Images.Single().X, Is.EqualTo(36).Within(1e-9));
        Assert.That(wrapped.Images.Single().Top, Is.EqualTo(36 + 14.4).Within(1e-9));
    }

    [Test]
    public void InlineImagesInsideSpansLinksListItemsAndTableCellsAlignToTheirLine()
    {
        Assert.That(Page("<div><span style='color:red'>A" + Svg20 + "</span></div>").Images.Single().X,
            Is.EqualTo(36 + 7.2).Within(1e-9));
        Assert.That(Page("<ul><li>" + Svg20 + "</li></ul>").Images, Has.Count.EqualTo(1));
        // In a 10pt cell with line-height 2 the strut is 13pt above the baseline; the 6pt svg's
        // bottom sits on the text baseline.
        var cell = Page(TableLayoutTests.Table("X<svg width='20' height='8'><rect width='20' height='8'/></svg>"));
        Assert.That(cell.Images.Single().Top, Is.EqualTo(PageHeight - cell.Runs.Single().Baseline - 6).Within(1e-9));
    }

    [Test]
    public void LinkAroundAnInlineImageCoversItsBox()
    {
        const string html = "<p style='margin:0'><a href='https://example.com/'>" + Svg20 + "</a></p>";
        Assert.That(Page(html).Images.Single().LinkUri, Is.EqualTo("https://example.com/"));
        var pdf = Text(html);
        Assert.That(Regex.Count(pdf, "/Subtype /Link"), Is.EqualTo(1));
        Assert.That(pdf, Does.Contain("/Subtype /Link /Rect [36 790.89 51 805.89]"));
    }

    [Test]
    public void InlineImageDecorationPaintsWithTheImage()
    {
        var page = Page("<p style='margin:0'><img src='gray' style='padding:2pt;border:1pt solid red;background-color:white'></p>");
        var image = page.Images.Single();
        Assert.That(image.X, Is.EqualTo(39).Within(1e-9));
        Assert.That(image.Top, Is.EqualTo(39).Within(1e-9));
        Assert.That(image.Decoration, Is.Not.Null);
        Assert.That((image.Decoration!.X, image.Decoration.Top, image.Decoration.Width, image.Decoration.Height),
            Is.EqualTo((-3d, -3d, 54d, 30d)));
        Assert.That(page.PaintBoxes, Is.Empty);
    }

    // Display -----------------------------------------------------------------------------------

    [Test]
    public void InlineAndInlineBlockAreTheDefaultLayout()
    {
        var plain = Text("<div>A<img src='gray'>B</div>");
        Assert.That(Text("<div>A<img src='gray' style='display:inline'>B</div>"), Is.EqualTo(plain));
        Assert.That(Text("<style>img{display:inline-block}</style><div>A<img src='gray'>B</div>"), Is.EqualTo(plain));
        Assert.That(Text("<div>A" + Svg20 + "B</div>"),
            Is.EqualTo(Text("<div>A<svg width='20' height='20' style='display:inline-block'></svg>B</div>")));
    }

    [Test]
    public void DisplayBlockKeepsTheBlockLayout()
    {
        var page = Page("<p style='margin:0'>A</p><img src='gray' style='display:block'><p style='margin:0'>B</p>");
        Assert.That(page.Images.Single().Top, Is.EqualTo(36 + 14.4).Within(1e-9));
        Assert.That(page.Runs.Single(r => r.Text == "B").Baseline, Is.EqualTo(PageHeight - 36 - 14.4 - 24 - 10.8).Within(1e-9));
    }

    // Pagination --------------------------------------------------------------------------------

    [Test]
    public void ALineWithATallImageMovesToTheNextPageAsAUnit()
    {
        // Five 14.4pt lines fill 72 of the 128pt usable height; the next line is 75 + 3.6pt high.
        var options = Options() with { PageHeight = 200 };
        var html = string.Concat(Enumerable.Repeat("<p style='margin:0'>L</p>", 5))
            + "<p style='margin:0'>X<svg width='100' height='100'></svg></p>";
        var pages = TableLayoutTests.Layout(html, options);
        Assert.That(pages, Has.Count.EqualTo(2));
        Assert.That(pages[0].Images, Is.Empty);
        Assert.That(pages[0].Runs.Select(r => r.Text), Has.None.EqualTo("X"));
        Assert.That(pages[1].Images.Single().Top, Is.EqualTo(36).Within(1e-9));
        Assert.That(pages[1].Runs.Single().Baseline, Is.EqualTo(200 - 36 - 75).Within(1e-9));
    }

    [Test]
    public void ALineTallerThanThePageFailsAtTheImage()
    {
        const string html = "<p style='margin:0'>X<svg width='200' height='200'></svg></p>";
        var failure = Reject(html, "FPDF1302", Options() with { PageHeight = 200 });
        Assert.That(failure.SourceOffset, Is.EqualTo(html.IndexOf("<svg", StringComparison.Ordinal)));
    }

    // Failures ----------------------------------------------------------------------------------

    [TestCase("<p>A<img src='gray' style='display:block'></p>", "FPDF1101")]
    [TestCase("<p><a href='https://example.com/'><svg style='display:block'></svg></a></p>", "FPDF1101")]
    [TestCase("<svg style='display:inline-flex'></svg>", "FPDF1202")]
    [TestCase("<img src='gray' style='display:none'>", "FPDF1201")]
    [TestCase("<p><img src='gray' style='vertical-align:sub'></p>", "FPDF1202")]
    [TestCase("<p><img src='gray' style='vertical-align:env(x)'></p>", "FPDF1202")]
    [TestCase("<p>A <span style='vertical-align:2pt'>B</span></p>", "FPDF1201")]
    [TestCase("<table><colgroup><col style='width:100pt'></colgroup><tbody><tr><td style='vertical-align:text-top'>A</td></tr></tbody></table>", "FPDF1202")]
    [TestCase("<table><tbody><tr><td>A<svg width='50%' height='10'></svg></td></tr></tbody></table>", "FPDF1302")]
    [TestCase("<div style='width:20pt'><img src='gray'></div>", "FPDF1302")]
    [TestCase("<style>.pic{display:inline}</style><p>A</p>", "FPDF1202")]
    public void UnsupportedInlineReplacedInputsFailExplicitly(string html, string code) => Reject(html, code);

    // SVG transparency ----------------------------------------------------------------------------

    private static readonly CompositeFormat Square = CompositeFormat.Parse("<path d='M0 0h1v1z'{0}/>");

    private static string SvgPdf(string content, PdfOptions? options = null)
        => Text("<svg width='4' height='4'>" + content + "</svg>", options);

    [TestCase(" fill-opacity='0.5'", "q /A80 gs 0 0 0 rg 0 0 m 1 0 l 1 1 l h f Q\n")]
    [TestCase(" fill-opacity='50%'", "q /A80 gs 0 0 0 rg 0 0 m 1 0 l 1 1 l h f Q\n")]
    [TestCase(" fill='rgba(255, 0, 0, 0.5)'", "q /A80 gs 1 0 0 rg 0 0 m 1 0 l 1 1 l h f Q\n")]
    [TestCase(" fill='#ff000080'", "q /A80 gs 1 0 0 rg 0 0 m 1 0 l 1 1 l h f Q\n")]
    [TestCase(" opacity='0.5'", "q /A80 gs 0 0 0 rg 0 0 m 1 0 l 1 1 l h f Q\n")]
    [TestCase(" fill-opacity='2'", "q 0 0 0 rg 0 0 m 1 0 l 1 1 l h f Q\n")]
    [TestCase(" fill='none' stroke='red' stroke-opacity='0.5'", "q /A80 gs 1 0 0 RG 1 w 4 M 0 0 m 1 0 l 1 1 l h S Q\n")]
    [TestCase(" stroke='red' fill-opacity='0.5' stroke-opacity='0.5'", "q /A80 gs 0 0 0 rg 1 0 0 RG 1 w 4 M 0 0 m 1 0 l 1 1 l h B Q\n")]
    public void TranslucentSvgPaintUsesConstantAlpha(string attributes, string operators)
    {
        var pdf = SvgPdf(string.Format(CultureInfo.InvariantCulture, Square, attributes));
        Assert.That(pdf, Does.Contain(operators));
        Assert.That(pdf.Contains("/A80 gs", StringComparison.Ordinal),
            Is.EqualTo(pdf.Contains("/ExtGState << /A80 << /CA 0.502 /ca 0.502 >> >>", StringComparison.Ordinal)));
    }

    [Test]
    public void FillAndStrokeOfDifferentAlphaPaintSeparately()
    {
        var pdf = SvgPdf(string.Format(CultureInfo.InvariantCulture, Square, " stroke='red' fill-opacity='0.5'"));
        Assert.That(pdf, Does.Contain("q /A80 gs 0 0 0 rg 0 0 m 1 0 l 1 1 l h f Q\nq 1 0 0 RG 1 w 4 M 0 0 m 1 0 l 1 1 l h S Q\n"));
        // The second paint charges the path again: 2 × (1 shape + 4 segments) commands.
        Assert.That(SvgPdf(string.Format(CultureInfo.InvariantCulture, Square, " stroke='red' fill-opacity='0.5'"), Options() with { MaxDisplayCommands = 10 }),
            Does.StartWith("%PDF"));
        Reject("<svg width='4' height='4'>" + string.Format(CultureInfo.InvariantCulture, Square, " stroke='red' fill-opacity='0.5'") + "</svg>",
            "FPDF1401", Options() with { MaxDisplayCommands = 9 });
    }

    [Test]
    public void OpacityMultipliesThroughGroupsOfDisjointShapes()
    {
        // 0.5 × 0.5 = 0.25 rounds to byte 64.
        Assert.That(SvgPdf("<g opacity='0.5'>" + string.Format(CultureInfo.InvariantCulture, Square, " fill-opacity='0.5'") + "</g>"), Does.Contain("q /A40 gs 0 0 0 rg"));
        var pdf = SvgPdf("<g opacity='0.5'><rect width='1' height='1'/><rect x='2' width='1' height='1'/></g>");
        Assert.That(Regex.Count(pdf, "/A80 gs"), Is.EqualTo(2));
        Assert.That(Text("<svg width='4' height='4' opacity='0.5'><rect width='1' height='1'/></svg>"), Does.Contain("q /A80 gs 0 0 0 rg"));
        // Opacity 0 paints nothing, like an absent shape.
        Assert.That(SvgPdf(string.Format(CultureInfo.InvariantCulture, Square, " opacity='0'")), Is.EqualTo(SvgPdf("")));
        Assert.That(SvgPdf("<g opacity='0'><rect width='2' height='2'/><rect width='2' height='2'/></g>"), Is.EqualTo(SvgPdf("")));
    }

    [TestCase("<g opacity='0.5'><rect width='2' height='2'/><rect x='1' width='2' height='2'/></g>")]
    [TestCase("<g opacity='0.5'><rect width='1' height='1'/><rect x='1.5' width='1' height='1' stroke='red' fill='none' stroke-width='2'/></g>")]
    [TestCase("<rect width='1' height='1' stroke='red' opacity='0.5'/>")]
    [TestCase("<g opacity='0.5'><g><rect width='1' height='1' stroke='red'/></g></g>")]
    public void GroupOpacityThatNeedsATransparencyGroupFailsFpdf1103(string content)
        => Reject("<svg width='4' height='4'>" + content + "</svg>", "FPDF1103");
}
