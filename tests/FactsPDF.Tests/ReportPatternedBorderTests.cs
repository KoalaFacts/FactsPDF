using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>Actual PDF dash strokes for report boxes (not parser-only CSS support).</summary>
[TestFixture]
public sealed class ReportPatternedBorderTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 240, Margin = 20 };

    private static byte[] Pdf(string html, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, options ?? Page);
        return output.ToArray();
    }

    private static LayoutPage[] Layout(string html)
        => TextLayout.Layout(HtmlDocumentReader.ReadTree(html, Page, default), Page, default).ToArray();

    private static (string X1, string Y1, string X2, string Y2) ClippedLine(string pdf)
    {
        var line = pdf.Split('\n').Single(value => value.Contains(" re W n "));
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var move = Array.IndexOf(parts, "m");
        var draw = Array.IndexOf(parts, "l");
        Assert.That(move, Is.GreaterThan(1), "Missing PDF move-to operator.");
        Assert.That(draw, Is.GreaterThan(move + 2), "Missing PDF line-to operator.");
        return (parts[move - 2], parts[move - 1], parts[draw - 2], parts[draw - 1]);
    }

    private static void FailsAndRetains(string html, string code, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        output.Write("ORIGINAL"u8);
        var error = Assert.Throws<FactsPdfException>(() =>
            PdfConverter.Convert(html, output, options ?? Page));
        Assert.That(error!.Code, Is.EqualTo(code));
        Assert.That(output.ToArray(), Is.EqualTo("ORIGINAL"u8.ToArray()));
        Assert.That(Pdf("<p>Recovery</p>").Length, Is.GreaterThan(100));
    }

    [TestCase("dashed", "1 0 0 RG", "[6 6] 0 d")]
    [TestCase("dotted", "1 0 0 RG", "1 J [0.001 4] 0 d")]
    public void ShorthandPaintsRealPdfStrokesWithIndependentColour(string kind, string colour, string pattern)
    {
        var html = "<section style='width:120pt;border:2pt " + kind +
            " red;background:#e5f1f6;padding:5pt'>INVOICE 22</section>";
        var box = Layout(html)[0].PaintBoxes.Single();
        Assert.That(box.CommandCount, Is.EqualTo(5), "One background and four stroke edges.");
        Assert.That(box.Width, Is.EqualTo(134).Within(0.00001));
        var pdf = Encoding.ASCII.GetString(Pdf(html));
        Assert.That(pdf, Does.Contain(colour));
        Assert.That(pdf, Does.Contain(pattern));
        Assert.That(pdf.Split(" l S Q\n").Length - 1, Is.EqualTo(4));
        Assert.That(pdf, Does.Contain("INVOICE").Or.Contain("Tm <"));
    }

    [Test]
    public void DifferentSidesAndCascadeKeepComputedGeometryAndLegacySolidOutput()
    {
        var stylesheet = "<style>section.report{border-style:dashed dotted solid none;" +
            "border-width:1pt 2pt 3pt 4pt;border-color:blue!important}</style>";
        var html = stylesheet + "<section class=report style='width:120pt;" +
            "border-left:5pt dashed red;border-top-style:dotted'>TEXT</section>";
        var box = ((BlockNode)HtmlDocumentReader.ReadTree(html, Page, default).Children.Single()).BoxStyle;
        Assert.That(box.BorderTop.Pattern, Is.EqualTo(BorderPattern.Dotted));
        Assert.That(box.BorderRight.Pattern, Is.EqualTo(BorderPattern.Dotted));
        Assert.That(box.BorderBottom.Pattern, Is.EqualTo(BorderPattern.Solid));
        Assert.That(box.BorderLeft.Pattern, Is.EqualTo(BorderPattern.Dashed));
        Assert.That(box.BorderLeft.EffectiveWidth, Is.EqualTo(5));
        Assert.That(box.BorderTop.Color, Is.EqualTo(new Rgb(0, 0, 1)));
        Assert.That(Pdf("<div style='border:2pt solid blue'>TEXT</div>"),
            Is.EqualTo(Pdf("<div style='border-width:2pt;border-style:solid;border-color:blue'>TEXT</div>")),
            "Original solid-filled rectangle bytes must stay exactly identical.");
        var dashed = Layout("<div style='width:100pt;border:2pt dashed blue'>TEXT</div>")[0];
        var solid = Layout("<div style='width:100pt;border:2pt solid blue'>TEXT</div>")[0];
        Assert.That(dashed.Runs.Single().X, Is.EqualTo(solid.Runs.Single().X).Within(0.00001));
        Assert.That(dashed.PaintBoxes.Single().Width, Is.EqualTo(solid.PaintBoxes.Single().Width));
    }

    [Test]
    public void CssWideInheritanceAndImportanceApplyToEachBorderSide()
    {
        const string html = "<style>section{border:2pt dashed blue}" +
            "article{border-style:inherit;border-top-style:dotted!important}</style>" +
            "<section><article>INHERITED</article>" +
            "<article style='border-style:solid'>NOTICE</article></section>";
        var root = (BlockNode)HtmlDocumentReader.ReadTree(html, Page, default).Children.Single();
        var inherited = (BlockNode)root.Children[0];
        var overridden = (BlockNode)root.Children[1];
        Assert.That(inherited.BoxStyle.BorderTop.Pattern, Is.EqualTo(BorderPattern.Dotted));
        foreach (var edge in new[] { inherited.BoxStyle.BorderRight,
                                     inherited.BoxStyle.BorderBottom,
                                     inherited.BoxStyle.BorderLeft })
        {
            Assert.That(edge.Pattern, Is.EqualTo(BorderPattern.Dashed),
                "The winning inherit must retain the parent's dashed pattern.");
            Assert.That(edge.EffectiveWidth, Is.EqualTo(2.25).Within(0.00001),
                "The border pattern inherits, but its width remains the medium default.");
        }
        Assert.That(overridden.BoxStyle.BorderTop.Pattern, Is.EqualTo(BorderPattern.Dotted),
            "The important top style still wins over the inline shorthand.");
        Assert.That(overridden.BoxStyle.BorderRight.Pattern, Is.EqualTo(BorderPattern.Solid));
        Assert.That(overridden.BoxStyle.BorderRight.EffectiveWidth,
            Is.EqualTo(2.25).Within(0.00001));
        Assert.That(Encoding.ASCII.GetString(Pdf(html)), Does.Contain("[6.75 6.75] 0 d"),
            "Inherited default-width dashed edges must actually paint in the PDF.");
    }

    [Test]
    public void PaginationKeepsTopAndBottomStrokesOnTheirOwnFragments()
    {
        var paragraphs = string.Concat(Enumerable.Range(1, 48).Select(i => "<p>Facility item " +
            i.ToString("D2") + ": inspected</p>"));
        var html = "<section style='border:2pt dashed #23446b;background:#f0f6fa;padding:5pt'>" +
            paragraphs + "</section>";
        var pages = Layout(html);
        Assert.That(pages.Length, Is.GreaterThan(2));
        var first = pages[0].PaintBoxes.Single();
        var last = pages[^1].PaintBoxes.Single();
        Assert.That(first.IsFirstFragment, Is.True);
        Assert.That(first.IsLastFragment, Is.False);
        Assert.That(last.IsFirstFragment, Is.False);
        Assert.That(last.IsLastFragment, Is.True);
        Assert.That(first.TopBorderHeight, Is.EqualTo(2));
        Assert.That(first.BottomBorderHeight, Is.Zero);
        Assert.That(last.TopBorderHeight, Is.Zero);
        Assert.That(last.BottomBorderHeight, Is.EqualTo(2));
        var pdf = Encoding.ASCII.GetString(Pdf(html));
        Assert.That(pdf.Split(" l S Q\n").Length - 1, Is.GreaterThan(pages.Length * 2));
        Assert.That(pdf, Does.Contain("Tm <"));
    }

    [Test]
    public void StandaloneHrUsesPatternedTopStrokeWithoutChangingFollowingText()
    {
        const string dashed = "<hr style='border-top:3pt dashed red'>";
        const string solid = "<hr style='border-top:3pt solid red'>";
        var first = Layout(dashed + "<p style='margin:0'>After</p>")[0];
        var second = Layout(solid + "<p style='margin:0'>After</p>")[0];
        Assert.That(first.Runs.Single().X, Is.EqualTo(second.Runs.Single().X));
        Assert.That(first.Runs.Single().Baseline, Is.EqualTo(second.Runs.Single().Baseline));
        Assert.That(Encoding.ASCII.GetString(Pdf(dashed)), Does.Contain("[9 9] 0 d"));
    }

    [TestCase("dashed", "[60 60] 0 d", 1)]
    [TestCase("dotted", "1 J [0.001 40] 0 d", 1)]
    [TestCase("dashed", "[60 60] 0 d", 20)]
    [TestCase("dotted", "1 J [0.001 40] 0 d", 20)]
    public void ShortAndSquarePatternedSideEdgesAreVisibleAndClippedToTheirMeasuredBox(
        string kind, string expectedPattern, int length)
    {
        var html = "<div style='border-left:20pt " + kind +
            " red;padding-bottom:" + length + "pt'></div>";
        var box = Layout(html).Single().PaintBoxes.Single();
        Assert.That(box.Height, Is.EqualTo(length).Within(0.00001));
        Assert.That(box.CommandCount, Is.EqualTo(1));
        var pdf = Encoding.ASCII.GetString(Pdf(html));
        Assert.That(pdf, Does.Contain(expectedPattern));
        Assert.That(pdf, Does.Contain(" re W n "),
            "The short or square line and its caps must be clipped to the measured border slice.");
        Assert.That(pdf, Does.Contain(" l S Q"), "A real noncollapsed stroke must be emitted.");
        var (x1, y1, x2, y2) = ClippedLine(pdf);
        Assert.That(x1, Is.EqualTo(x2), "The side stroke must stay vertical.");
        Assert.That(y1, Is.Not.EqualTo(y2),
            "The serialized stroke must have distinct endpoints even at equal thickness and length.");
    }

    [TestCase("dashed", "[60 60] 0 d")]
    [TestCase("dotted", "1 J [0.001 40] 0 d")]
    public void ShortHorizontalEdgesUseTheSameClippingContract(
        string kind, string expectedPattern)
    {
        var html = "<div style='width:1pt;border-top:20pt " + kind + " red'></div>";
        var box = Layout(html).Single().PaintBoxes.Single();
        Assert.That(box.Width, Is.EqualTo(1).Within(0.00001));
        var pdf = Encoding.ASCII.GetString(Pdf(html));
        Assert.That(pdf, Does.Contain(expectedPattern));
        Assert.That(pdf, Does.Contain(" re W n "));
        Assert.That(pdf, Does.Contain(" l S Q"));
        var (x1, y1, x2, y2) = ClippedLine(pdf);
        Assert.That(x1, Is.Not.EqualTo(x2), "Short horizontal strokes must have real length.");
        Assert.That(y1, Is.EqualTo(y2), "The horizontal stroke must stay level.");
    }

    [TestCase("dashed")]
    [TestCase("dotted")]
    public void NearEqualEdgesRetainDistinctSerializedEndpointsAndMeasuredClipping(string kind)
    {
        var vertical = "<div style='border-left:20pt " + kind +
            " red;padding-bottom:20.0001pt'></div>";
        var verticalBox = Layout(vertical).Single().PaintBoxes.Single();
        Assert.That(verticalBox.Height, Is.EqualTo(20.0001).Within(0.000001));
        var v = ClippedLine(Encoding.ASCII.GetString(Pdf(vertical)));
        Assert.That(v.X1, Is.EqualTo(v.X2));
        Assert.That(v.Y1, Is.Not.EqualTo(v.Y2),
            "Rounding 210 and 209.9999 to three PDF decimals must never erase a border.");

        var horizontal = "<div style='width:20.0001pt;border-top:20pt " + kind + " red'></div>";
        var h = ClippedLine(Encoding.ASCII.GetString(Pdf(horizontal)));
        Assert.That(h.X1, Is.Not.EqualTo(h.X2),
            "A near-equal horizontal edge must not collapse after PDF serialization.");
        Assert.That(h.Y1, Is.EqualTo(h.Y2));
    }

    [TestCase(20)]
    [TestCase(36)]
    public void MinimumVisiblePatternWidthIsIndependentOfPageMargin(int margin)
    {
        const string html = "<div style='border-top:0.001pt dashed red'></div>";
        var pdf = Encoding.ASCII.GetString(Pdf(html, Page with { Margin = margin }));
        Assert.That(pdf, Does.Contain("0.001 w"),
            "Subtraction of the page Y coordinates must not reject a valid linewidth.");
        Assert.That(pdf, Does.Contain("[0.003 0.003] 0 d"));
    }

    [Test]
    public void ZeroAndNoneDoNotPaintOrConsumeCommands()
    {
        var html = "<div style='border:2pt none red'>EMPTY</div>";
        Assert.That(Pdf(html), Is.EqualTo(Pdf("<div>EMPTY</div>")));
        var zero = Layout("<div style='border:0 dashed blue'>EMPTY</div>");
        Assert.That(zero[0].PaintBoxes, Is.Empty);
    }

    [Test]
    public void UnsupportedStylesResourcesAndCommandLimitsRetainOriginalOutput()
    {
        FailsAndRetains("<div style='border:2pt double blue'>X</div>", "FPDF1202");
        foreach (var side in new[] { "top", "right", "bottom", "left" })
            FailsAndRetains("<div style='border-" + side + ":2pt double blue'>X</div>", "FPDF1202");
        FailsAndRetains("<div style='border-style:groove'>X</div>", "FPDF1202");
        FailsAndRetains("<div style='border:2pt dashed dotted'>X</div>", "FPDF1202");
        FailsAndRetains("<span style='border:2pt dashed blue'>X</span>", "FPDF1201");
        FailsAndRetains("<div style='width:260pt;border:3pt dotted blue'>X</div>", "FPDF1302");
        FailsAndRetains("<div style='border:2pt dashed red'>X</div>", "FPDF1401",
            Page with { MaxDisplayCommands = 2 });
    }
}
