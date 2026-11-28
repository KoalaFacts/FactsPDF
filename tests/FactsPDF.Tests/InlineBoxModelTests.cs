using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// The inline box model: padding, borders, margins and a background on inline elements take room in
/// the line and paint around the text they hold (one fragment per line, with no border where the box
/// continues), and vertical-align (sub, super, lengths, percentages) shifts the text and the line
/// box as Chrome does. The fixture font has 600-unit A and B, 300-unit spaces, an 800/-200 ascent and
/// descent and a 500 x-height, so every width and shift below is exact arithmetic.
/// </summary>
[TestFixture]
public sealed class InlineBoxModelTests
{
    private static readonly PdfFont Font = PdfFont.FromBytes(FontFixture.Create());
    private static readonly PdfOptions Page = new()
    {
        PageWidth = 300, PageHeight = 400, Margin = 20, FontSize = 10, Fonts = [Font],
    };
    private const string Reset = "<style>body{margin:0}p{margin:0}</style>";
    private const double PixelsPerPoint = 4d / 3;

    private static List<LayoutPage> Layout(string html, PdfOptions? options = null)
    {
        options ??= Page;
        return TextLayout.Layout(HtmlDocumentReader.ReadTree(Reset + html, options, default), options, default);
    }

    private static IEnumerable<PlacedImage> BoxPaint(LayoutPage page) => page.Images.Where(i => i.Image is null && i.Decoration is not null);

    private static byte[] Pdf(string html, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(Reset + html, output, options ?? Page);
        return output.ToArray();
    }

    [Test]
    public void PaddingBorderAndMarginTakeRoomInTheLineAndPaintAround()
    {
        // A (6) | margin 3, border 2, padding 6 | B (6) | padding 6, border 2, margin 3 | A (6).
        var page = Layout("<p>A<span style='padding:0 6pt;margin:0 3pt;border:2pt solid #f00'>B</span>A</p>")[0];
        var runs = page.Runs.OrderBy(r => r.X).ToArray();
        Assert.That(runs.Select(r => r.Text), Is.EqualTo(new[] { "A", "B", "A" }));
        Assert.That(runs[0].X, Is.EqualTo(20).Within(0.001));
        Assert.That(runs[1].X, Is.EqualTo(20 + 6 + 3 + 2 + 6).Within(0.001));
        Assert.That(runs[2].X, Is.EqualTo(20 + 6 + 3 + 2 + 6 + 6 + 6 + 2 + 3).Within(0.001));
        var box = BoxPaint(page).Single();
        Assert.That(box.X, Is.EqualTo(20 + 6 + 3).Within(0.001), "the box starts after its left margin");
        Assert.That(box.Width, Is.EqualTo(2 + 6 + 6 + 6 + 2).Within(0.001));
        // The font box is 11 + 3 pixels (the ascent and descent rounded) and the borders add 2pt twice.
        Assert.That(box.Height, Is.EqualTo(14 / PixelsPerPoint + 4).Within(0.001));
    }

    [Test]
    public void VerticalPaddingAndBorderNeitherGrowTheLineNorMoveTheText()
    {
        var plain = Layout("<p>A</p><p>B</p>")[0].Runs.Select(r => r.Baseline).ToArray();
        var padded = Layout("<p><span style='padding:9pt 0;border-top:3pt solid #000;background:#ff0'>A</span></p><p>B</p>")[0];
        Assert.That(padded.Runs.Select(r => r.Baseline), Is.EqualTo(plain).Within(0.001));
        // The painted box still reaches 9pt and the border above and below the font box.
        Assert.That(BoxPaint(padded).Single().Height, Is.EqualTo(14 / PixelsPerPoint + 18 + 3).Within(0.001));
    }

    [Test]
    public void AnEmptyInlineWithoutEdgesChangesNothing()
    {
        Assert.That(Pdf("<p>A<span></span>B</p>"), Is.EqualTo(Pdf("<p>AB</p>")));
        Assert.That(Pdf("<p>A<span style='vertical-align:super'></span>B</p>"), Is.EqualTo(Pdf("<p>AB</p>")));
    }

    [Test]
    public void AnEmptyInlineWithHorizontalPaddingTakesItsWidth()
    {
        var runs = Layout("<p>A<span style='padding:0 5pt'></span>B</p>")[0].Runs.OrderBy(r => r.X).ToArray();
        Assert.That(runs[1].X - (runs[0].X + 6), Is.EqualTo(10).Within(0.001));
    }

    [Test]
    public void SuperRaisesAndSubLowersByAThirdAndAFifthOfTheParentSizePlusAPixel()
    {
        // 10pt is 13.328125px in 1/64 units (853/64): 853/3 = 284 -> 4.4375 + 1 = 5.4375px; 853/5 = 170 -> 2.65625 + 1 px.
        var sup = Layout("<p>A<sup>B</sup></p>")[0].Runs.OrderBy(r => r.X).ToArray();
        Assert.That(sup[1].Baseline - sup[0].Baseline, Is.EqualTo(5.4375 / PixelsPerPoint).Within(0.001));
        var sub = Layout("<p>A<sub>B</sub></p>")[0].Runs.OrderBy(r => r.X).ToArray();
        Assert.That(sub[1].Baseline - sub[0].Baseline, Is.EqualTo(-3.65625 / PixelsPerPoint).Within(0.001));
        // sup and sub print at the user-agent sheet's smaller font size (10pt / 1.2).
        Assert.That(sup[1].Style.FontSize, Is.EqualTo(10 / 1.2).Within(0.001));
    }

    [Test]
    public void ARaisedBoxGrowsTheLineAboveItsBaseline()
    {
        // The line's ascent is the sup box's 9px font ascent raised 5.4375px; the strut's is 11px.
        var page = Layout("<p>A<sup>B</sup></p>")[0];
        var a = page.Runs.Single(r => r.Text == "A");
        Assert.That(a.Baseline, Is.EqualTo(400 - 20 - (9 + 5.4375) / PixelsPerPoint).Within(0.001));
    }

    [Test]
    public void ALoweredBoxGrowsTheLineBelowItsBaseline()
    {
        var plain = Layout("<p>A</p><p>C</p>")[0].Runs;
        var lowered = Layout("<p>A<sub>B</sub></p><p>C</p>")[0].Runs;
        var plainStep = plain.Single(r => r.Text == "A").Baseline - plain.Single(r => r.Text == "C").Baseline;
        var loweredStep = lowered.Single(r => r.Text == "A").Baseline - lowered.Single(r => r.Text == "C").Baseline;
        // The sub's font box (2px descent) sits 3.65625px lower: 5.65625px below the baseline against the strut's 3px.
        Assert.That(loweredStep - plainStep, Is.EqualTo((2 + 3.65625 - 3) / PixelsPerPoint).Within(0.001));
    }

    [TestCase("vertical-align:4pt", 4.0)]
    [TestCase("vertical-align:-4pt", -4.0)]
    [TestCase("vertical-align:50%", 5.25)] // half of the 14px (10.5pt) normal line height
    public void LengthsAndPercentagesShiftTheBaseline(string css, double shift)
    {
        var runs = Layout($"<p>A<span style='{css}'>B</span></p>")[0].Runs.OrderBy(r => r.X).ToArray();
        Assert.That(runs[1].Baseline - runs[0].Baseline, Is.EqualTo(shift).Within(0.001));
    }

    [Test]
    public void ShiftsAddUpAcrossNestedBoxes()
    {
        var runs = Layout("<p>A<span style='vertical-align:2pt'>B<span style='vertical-align:3pt'>A</span></span></p>")[0]
            .Runs.OrderBy(r => r.X).ToArray();
        Assert.That(runs[2].Baseline - runs[0].Baseline, Is.EqualTo(5).Within(0.001));
        Assert.That(runs[1].Baseline - runs[0].Baseline, Is.EqualTo(2).Within(0.001));
    }

    [Test]
    public void BaselineOverridesTheUserAgentSub()
    {
        var runs = Layout("<style>sub{vertical-align:baseline}</style><p>A<sub>B</sub></p>")[0].Runs.OrderBy(r => r.X).ToArray();
        Assert.That(runs[1].Baseline, Is.EqualTo(runs[0].Baseline).Within(0.001));
    }

    [Test]
    public void MarkPaintsAYellowBoxUnderBlackText()
    {
        var page = Layout("<p>A<mark>B</mark></p>")[0];
        var paint = BoxPaint(page).Single();
        Assert.That(paint.Decoration!.Style.BackgroundColor, Is.EqualTo(new Rgb(1, 1, 0)));
        Assert.That(page.Runs.Single(r => r.Text == "B").Style.Color, Is.EqualTo(new Rgb(0, 0, 0)));
        Assert.That(paint.X, Is.EqualTo(26).Within(0.001));
        Assert.That(paint.Width, Is.EqualTo(6).Within(0.001));
    }

    [Test]
    public void ABoxThatWrapsPaintsOneFragmentPerLineWithoutBordersWhereItContinues()
    {
        // The 32pt line holds the left edge and one 18pt word; the second word wraps to the next line.
        var options = Page with { PageWidth = 72 };
        var page = Layout("<p><span style='padding:0 5pt;border:1pt solid #000;background:#ff0'>AAA AAA</span></p>", options)[0];
        var fragments = BoxPaint(page).OrderBy(i => i.Top).ToArray();
        Assert.That(fragments, Has.Length.EqualTo(2));
        Assert.That(fragments[0].Decoration!.OmitLeftEdge, Is.False);
        Assert.That(fragments[0].Decoration!.OmitRightEdge, Is.True);
        Assert.That(fragments[1].Decoration!.OmitLeftEdge, Is.True);
        Assert.That(fragments[1].Decoration!.OmitRightEdge, Is.False);
        // The first line holds the edge and one word; the second starts at the line's left edge.
        Assert.That(fragments[0].Width, Is.EqualTo(1 + 5 + 18).Within(0.001));
        Assert.That(fragments[1].X, Is.EqualTo(20).Within(0.001));
        Assert.That(fragments[1].Width, Is.EqualTo(18 + 5 + 1).Within(0.001));
    }

    [Test]
    public void PercentagePaddingResolvesAgainstTheContainingWidth()
    {
        // 10% of the 260pt content width.
        var runs = Layout("<p>A<span style='padding-left:10%'>B</span></p>")[0].Runs.OrderBy(r => r.X).ToArray();
        Assert.That(runs[1].X - (runs[0].X + 6), Is.EqualTo(26).Within(0.001));
    }

    [Test]
    public void AnEdgeStaysWithTheTextItTouchesAtABreak()
    {
        // "AAA " then a padded box: the break after the space puts the box's left edge on the second line.
        var options = Page with { PageWidth = 72 };
        var page = Layout("<p>AAA <span style='padding-left:4pt;background:#ff0'>AAA</span></p>", options)[0];
        var box = BoxPaint(page).Single();
        Assert.That(box.X, Is.EqualTo(20).Within(0.001));
        Assert.That(box.Width, Is.EqualTo(4 + 18).Within(0.001));
    }

    [TestCase("<p>A<span style='border-radius:3pt'>B</span></p>")]
    [TestCase("<p>A<span style='outline:1pt solid red'>B</span></p>")]
    [TestCase("<div style='display:flex'><span style='padding:2pt'>A</span></div>")]
    public void WhatIsNotLaidOutAsAnInlineBoxStillFails(string html)
    {
        using var output = new MemoryStream();
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, Page));
        Assert.That(error!.Code, Is.EqualTo("FPDF1201"));
    }

    [Test]
    public void WidthHeightAndVerticalMarginsDoNotApplyToAnInlineBox()
    {
        Assert.That(Pdf("<p>A<span style='width:50pt;height:30pt;margin-top:9pt;margin-bottom:9pt'>B</span></p>"),
            Is.EqualTo(Pdf("<p>AB</p>")));
    }
}
