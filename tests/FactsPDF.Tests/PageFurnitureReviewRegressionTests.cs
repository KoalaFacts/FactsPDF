using NUnit.Framework;

namespace FactsPDF.Tests;

// Regressions for the independently reproduced findings in PR #57 comment
// 6387632840. Expected order is contractual, not derived from renderer output.
[TestFixture]
public sealed class PageFurnitureReviewRegressionTests
{
    private static PdfOptions Options() => new()
    {
        Fonts = [PdfFont.LoadTrueType(FontFixture.Create(latinOnly: true)),
            PdfFont.LoadTrueType(FontFixture.Create())]
    };

    private static byte[] Pdf(PdfOptions options)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert("", output, options);
        return output.ToArray();
    }

    private static void Fails(PdfOptions options, string code)
    {
        using var output = new MemoryStream();
        output.Write("sentinel"u8); output.Position = 3;
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert("", output, options));
        Assert.Multiple(() =>
        {
            Assert.That(error!.Code, Is.EqualTo(code));
            Assert.That(output.ToArray(), Is.EqualTo("sentinel"u8.ToArray()));
            Assert.That(output.Position, Is.EqualTo(3));
        });
    }

    private static PdfPageFurniture WithBand(bool footer, bool firstPage, PdfPageText? next)
    {
        var left = new PdfPageText { Template = "A中A" };
        var band = footer
            ? new PdfPageFurnitureRegions { FooterLeft = left, FooterCenter = next }
            : new PdfPageFurnitureRegions { HeaderLeft = left, HeaderCenter = next };
        return firstPage ? new() { FirstPage = band } : new() { Default = band };
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public void StagedActualFallbackRunsExhaustBudgetBeforeNextRegionFontResolution(bool footer, bool firstPage)
    {
        var options = Options() with
        {
            MaxDisplayCommands = 3,
            PageFurniture = WithBand(footer, firstPage, new() { Template = "B", Bold = true })
        };
        Fails(options, "FPDF1401");
        // One more command allows the actual missing-role diagnostic instead.
        Fails(options with { MaxDisplayCommands = 4 }, "FPDF1507");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ExactFallbackAndFollowingRegionBudgetKeepsPdfBytes(bool footer)
    {
        var options = Options() with { PageFurniture = WithBand(footer, false, null) };
        var pages = new List<LayoutPage> { new() };
        PageFurnitureLayout.Append(pages, options, 0, default);
        Assert.That(pages[0].Runs.Select(run => run.Text), Is.EqualTo(new[] { "A", "中", "A" }));
        Assert.That(Pdf(options with { MaxDisplayCommands = 3 }), Is.EqualTo(Pdf(options)));
        Fails(options with { MaxDisplayCommands = 2 }, "FPDF1401");
        options = options with { PageFurniture = WithBand(footer, false, new() { Template = "B" }) };
        Assert.That(Pdf(options with { MaxDisplayCommands = 4 }), Is.EqualTo(Pdf(options)));
        Fails(options with { MaxDisplayCommands = 3 }, "FPDF1401");
    }

    [Test]
    public void StagedRunCountingDoesNotChargeBeforeBandGeometry()
    {
        var options = Options() with
        {
            Margin = 10,
            MaxDisplayCommands = 2,
            PageFurniture = WithBand(false, false, null)
        };
        // All three runs are known, but the 10pt margin cannot hold the band.
        Fails(options, "FPDF1302");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void LaterNumericDefinitionPrecedesEarlierRawLength(bool inFirstPage)
    {
        var earlier = new PdfPageText { Template = new string('A', 1025) };
        var invalid = new PdfPageText { Template = "A", FontSize = double.NaN };
        var furniture = inFirstPage
            ? new PdfPageFurniture { Default = new() { HeaderLeft = earlier }, FirstPage = new() { FooterRight = invalid } }
            : new PdfPageFurniture { Default = new() { HeaderLeft = earlier, HeaderRight = invalid } };
        using var output = new MemoryStream();
        output.Write("sentinel"u8); output.Position = 3;
        Assert.Throws<ArgumentOutOfRangeException>(() => PdfConverter.Convert("", output, new() { PageFurniture = furniture }));
        Assert.That(output.ToArray(), Is.EqualTo("sentinel"u8.ToArray()));
        Assert.That(output.Position, Is.EqualTo(3));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void LaterNullDefinitionPrecedesEarlierRawLength(bool inFirstPage)
    {
        var earlier = new PdfPageText { Template = new string('A', 1025) };
        var invalid = new PdfPageText { Template = null! };
        var furniture = inFirstPage
            ? new PdfPageFurniture { Default = new() { HeaderLeft = earlier }, FirstPage = new() { FooterRight = invalid } }
            : new PdfPageFurniture { Default = new() { HeaderLeft = earlier, HeaderRight = invalid } };
        using var output = new MemoryStream();
        output.Write("sentinel"u8); output.Position = 3;
        Assert.Throws<ArgumentException>(() => PdfConverter.Convert("", output, new() { PageFurniture = furniture }));
        Assert.That(output.ToArray(), Is.EqualTo("sentinel"u8.ToArray()));
        Assert.That(output.Position, Is.EqualTo(3));
    }

    [Test]
    public void FontResourceSnapshotPrecedesRawTemplateLength()
    {
        var options = Options() with
        {
            MaxTotalFontBytes = 1,
            PageFurniture = new() { Default = new() { HeaderLeft = new() { Template = new string('A', 1025) } } }
        };
        Fails(options, "FPDF1503");
        Fails(options with { MaxTotalFontBytes = 64 * 1024 * 1024 }, "FPDF1001");
    }

    [Test]
    public void ImageResourceSnapshotPrecedesRawTemplateLength()
    {
        using var output = new MemoryStream();
        output.Write("sentinel"u8); output.Position = 3;
        Assert.Throws<ArgumentException>(() => PdfConverter.Convert("", output, new()
        {
            Images = new Dictionary<string, PdfImage> { ["bad/name"] = null! },
            PageFurniture = new() { Default = new() { HeaderLeft = new() { Template = new string('A', 1025) } } }
        }));
        Assert.That(output.ToArray(), Is.EqualTo("sentinel"u8.ToArray()));
        Assert.That(output.Position, Is.EqualTo(3));
    }
}
