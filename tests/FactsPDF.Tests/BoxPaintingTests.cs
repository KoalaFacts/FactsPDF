using System.Globalization;
using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// M7 single-page box painting contracts; cross-page painted fragmentation belongs to M8.
/// </summary>
[TestFixture]
public sealed class BoxPaintingTests
{
    private static readonly PdfOptions Options = new() { PageWidth = 300, PageHeight = 500, Margin = 20 };
    private static BlockNode Block(string html, PdfOptions? options = null)
        => (BlockNode)HtmlDocumentReader.ReadTree(html, options ?? Options, default).Children.Single();
    private static (PdfConversionResult Result, string Pdf) Render(string html, PdfOptions? options = null)
    {
        using var stream = new MemoryStream();
        var result = PdfConverter.Convert(html, stream, options ?? Options);
        return (result, Encoding.ASCII.GetString(stream.ToArray()));
    }
    private static FactsPdfException Failure(string html, string code, PdfOptions? options = null)
    {
        using var stream = new MemoryStream();
        stream.Write("keep"u8);
        var error = Assert.Throws<FactsPdfException>(
            () => PdfConverter.Convert(html, stream, options ?? Options));
        Assert.That(error!.Code, Is.EqualTo(code));
        Assert.That(Encoding.ASCII.GetString(stream.ToArray()), Is.EqualTo("keep"),
            "StrictPdf must fail before emitting bytes.");
        return error;
    }

    [Test]
    public void SeparateBackgroundAndFourSolidSidesHaveComputedColorsAndWidths()
    {
        const string html = "<div style='color:blue;background-color:#abc;" +
            "border-top:6pt solid red;border-right:5pt solid #008000;" +
            "border-bottom:4pt solid currentColor;border-left:7pt solid #a0601c'>A</div>";
        var box = Block(html);
        Assert.That(box.BoxStyle.BackgroundColor, Is.EqualTo(new Rgb(0xaa / 255d, 0xbb / 255d, 0xcc / 255d)));
        Assert.That(box.BoxStyle.BorderTop.EffectiveWidth, Is.EqualTo(6));
        Assert.That(box.BoxStyle.BorderRight.EffectiveWidth, Is.EqualTo(5));
        Assert.That(box.BoxStyle.BorderBottom.EffectiveWidth, Is.EqualTo(4));
        Assert.That(box.BoxStyle.BorderLeft.EffectiveWidth, Is.EqualTo(7));
        Assert.That(box.BoxStyle.BorderTop.Color, Is.EqualTo(new Rgb(1, 0, 0)));
        Assert.That(box.BoxStyle.BorderBottom.Color, Is.Null, "currentColor is resolved against final computed text color.");
    }

    [Test]
    public void BorderShorthandUsesMediumWidthAndFinalCurrentColor()
    {
        var box = Block("<div style='border:solid;color:#113355'>X</div>");
        Assert.That(box.BoxStyle.BorderTop.EffectiveWidth, Is.EqualTo(2.25).Within(0.00001));
        Assert.That(box.BoxStyle.BorderRight.EffectiveWidth, Is.EqualTo(2.25).Within(0.00001));
        var (_, pdf) = Render("<div style='border:solid;color:#113355'>X</div>");
        Assert.That(pdf, Does.Contain("0.067 0.2 0.333 rg"), "Paint must follow final color.");
    }

    [Test]
    public void BackgroundAndBordersPaintBeforeTextAndRestoreGraphicsState()
    {
        var (result, pdf) = Render("<div style='width:150pt;padding:10pt;" +
            "background-color:#cee8fb;border:3pt solid #1d4568'>PAINT</div>");
        Assert.That(result.PageCount, Is.EqualTo(1));
        Assert.That(pdf, Does.Contain(" re f Q"));
        Assert.That(pdf, Does.Contain("0.808 0.91 0.984 rg"));
        Assert.That(pdf, Does.Contain("0.114 0.271 0.408 rg"));
        Assert.That(pdf.IndexOf(" re f Q", StringComparison.Ordinal),
            Is.LessThan(pdf.IndexOf(" Tj ET", StringComparison.Ordinal)));
        Assert.That(pdf, Does.Contain("Tm <5041494E54> Tj"));
        Assert.That(pdf, Does.Not.Contain(" NaN "));
    }

    [Test]
    public void BordersContributeToWidthAndTextXCoordinates()
    {
        const string html = "<div style='width:150pt;padding:10pt;border-left:4pt solid red;" +
            "border-right:2pt solid blue'>A</div>";
        var tree = HtmlDocumentReader.ReadTree(html, Options, default);
        var pages = TextLayout.Layout(tree, Options, default);
        Assert.That(pages[0].Runs.Single().X, Is.EqualTo(34).Within(0.00001));
        Failure("<div style='width:260pt;border:1pt solid red'>X</div>", "FPDF1302");
    }

    [Test]
    public void NoneBorderDoesNotConsumeLayoutWidthOrPaint()
    {
        var (_, pdf) = Render("<div style='border:10pt none red;width:260pt'>A</div>");
        Assert.That(pdf, Does.Not.Contain(" re f Q"));
        var box = Block("<div style='border:10pt none red;width:260pt'>A</div>");
        Assert.That(box.BoxStyle.BorderLeft.EffectiveWidth, Is.Zero);
    }

    [Test]
    public void CascadeExpandsShorthandsBeforePerSideWinnerSelection()
    {
        const string html = "<style>.x{border:2pt solid red;border-left-color:blue !important;" +
            "background-color:#111111}div.x{border-right:4pt solid #008000}</style>" +
            "<div class=x style='border:3pt solid #ffffff;border-left-color:#222222;" +
            "background-color:transparent'>X</div>";
        var box = (BlockNode)HtmlDocumentReader.ReadTree(html, Options, default).Children.Single();
        Assert.That(box.BoxStyle.BorderTop.EffectiveWidth, Is.EqualTo(3));
        Assert.That(box.BoxStyle.BorderRight.EffectiveWidth, Is.EqualTo(3));
        Assert.That(box.BoxStyle.BorderLeft.Color, Is.EqualTo(new Rgb(0, 0, 1)));
        Assert.That(box.BoxStyle.BackgroundColor, Is.Null);
    }

    [Test]
    public void SideShorthandsAndMultiValueListsRespectCssPriority()
    {
        var box = Block("<div style='border-width:1pt 2pt 3pt 4pt;" +
            "border-style:solid none solid solid;" +
            "border-color:red blue #008000 #111111;" +
            "border-left:5pt solid #ff0000'>X</div>");
        Assert.That(new[] { box.BoxStyle.BorderTop.EffectiveWidth, box.BoxStyle.BorderRight.EffectiveWidth,
            box.BoxStyle.BorderBottom.EffectiveWidth, box.BoxStyle.BorderLeft.EffectiveWidth },
            Is.EqualTo(new[] { 1d, 0d, 3d, 5d }));
    }

    [Test]
    public void NonInheritedBackgroundAndCssWideKeywordsApply()
    {
        const string html = "<div style='background-color:#008000;border:3pt solid red'>" +
            "<div style='background-color:inherit;border-color:inherit'>A</div>" +
            "<div style='background-color:unset;border:initial'>B</div></div>";
        var root = Block(html);
        var children = root.Children.OfType<BlockNode>().ToArray();
        Assert.That(children[0].BoxStyle.BackgroundColor, Is.EqualTo(root.BoxStyle.BackgroundColor));
        Assert.That(children[1].BoxStyle.BackgroundColor, Is.Null);
        Assert.That(children[1].BoxStyle.BorderTop.EffectiveWidth, Is.Zero);
        Assert.That(children[0].BoxStyle.BorderTop.EffectiveWidth, Is.Zero,
            "border-color:inherit must not inherit border width/style.");
    }

    [Test]
    public void EmptyPaintedBoxHasPaddingHeightAndProducesRectangle()
    {
        var pages = TextLayout.Layout(
            HtmlDocumentReader.ReadTree("<div style='background-color:red;" +
                "padding-top:10pt;padding-bottom:15pt'></div>", Options, default), Options, default);
        Assert.That(pages[0].PaintBoxes, Has.Count.EqualTo(1));
        Assert.That(pages[0].PaintBoxes[0].Height, Is.EqualTo(25).Within(0.00001));
        Assert.That(pages[0].Runs, Is.Empty);
    }

    [Test]
    public void NestedColorsPaintInAncestorOrderIndependentOfTextOrder()
    {
        var (_, pdf) = Render("<section style='background-color:red;padding:5pt'>" +
            "<article style='background-color:blue;padding:5pt'><p>X</p></article></section>");
        Assert.That(pdf.IndexOf("1 0 0 rg", StringComparison.Ordinal),
            Is.LessThan(pdf.IndexOf("0 0 1 rg", StringComparison.Ordinal)));
        Assert.That(pdf.IndexOf("0 0 1 rg", StringComparison.Ordinal),
            Is.LessThan(pdf.IndexOf(" Tj ET", StringComparison.Ordinal)));
    }

    [Test]
    public void DecoratedBoxSpanningTwoPagesFailsUntilM8()
    {
        Failure("<div style='background-color:blue'>" +
            "<p>First</p><p style='break-before:page'>Second</p></div>", "FPDF1302");
        Failure("<section style='border:1pt solid red'>" +
            "<p>First</p><p style='break-before:page'>Second</p></section>", "FPDF1302");
    }

    [Test]
    public void UndecoratedPaginationStillWorks()
        => Assert.That(Render("<p>A</p><p style='break-before:page'>B</p>").Result.PageCount,
            Is.EqualTo(2));

    [Test]
    public void DisplayCommandBudgetProtectsBeforeWriting()
        => Failure("<div style='background-color:red;border:1pt solid blue'>A</div>", "FPDF1401",
            Options with { MaxDisplayCommands = 2 });

    [TestCase("background-color:linear-gradient(red,blue)", "FPDF1202")]
    [TestCase("border:1pt dashed red", "FPDF1202")]
    [TestCase("border-width:2%", "FPDF1202")]
    [TestCase("border:2pt solid #xyz", "FPDF1202")]
    [TestCase("border:1pt solid 2pt", "FPDF1202")]
    [TestCase("border-color:red blue green yellow black", "FPDF1202")]
    [TestCase("border-radius:4pt", "FPDF1201")]
    [TestCase("background:url(foo)", "FPDF1201")]
    public void InvalidOrUnsupportedPaintCssFailsBeforePdfWrite(string css, string code)
        => Failure("<div style='" + css + "'>A</div>", code);

    [Test]
    public void StylingInlineSpansAndHtmlCanvasRemainsUnsupported()
    {
        Failure("<span style='border:1pt solid red'>X</span>", "FPDF1201");
        Failure("<html style='background-color:red'><body>X</body></html>", "FPDF1201");
    }

    [Test]
    public void EmbeddedUnicodeFontUsesTheSamePaintCommandsAsAscii()
    {
        var fonts = new PdfOptions { PageWidth = 300, PageHeight = 500, Margin = 20,
            Fonts = [PdfFont.LoadTrueType(FontFixture.Create())] };
        using var output = new MemoryStream();
        PdfConverter.Convert("<div style='background-color:#cee8fb;" +
            "border:2pt solid red;padding:10pt'><p>A中文B</p></div>", output, fonts);
        var pdf = Encoding.Latin1.GetString(output.ToArray());
        Assert.That(pdf, Does.Contain("/ToUnicode"));
        Assert.That(pdf, Does.Contain("/Subtype /Type0"));
        Assert.That(pdf, Does.Contain(" re f Q"));
        Assert.That(pdf.IndexOf(" re f Q", StringComparison.Ordinal),
            Is.LessThan(pdf.IndexOf(" Tj ET", StringComparison.Ordinal)));
    }

    [Test]
    public void TransparentOrZeroWidthPaintProducesByteIdenticalLegacyOutput()
    {
        var baseline = Render("<div>A</div>").Pdf;
        var explicitlyInvisible = Render(
            "<div style='background-color:transparent;border:0 solid red'>A</div>").Pdf;
        Assert.That(explicitlyInvisible, Is.EqualTo(baseline));
    }

    [Test]
    public void BorderShorthandExpansionChargesTwelveDeclarations()
    {
        Failure("<div style='border:1pt solid red'>A</div>", "FPDF1205",
            Options with { MaxCssDeclarations = 11 });
    }

    [Test]
    public void PaintingIsLocaleIndependent()
    {
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var french = Render("<div style='border:1.5pt solid red;background-color:#abcdef'>X</div>").Pdf;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            Assert.That(Render("<div style='border:1.5pt solid red;background-color:#abcdef'>X</div>").Pdf,
                Is.EqualTo(french));
        }
        finally { CultureInfo.CurrentCulture = culture; }
    }
}
