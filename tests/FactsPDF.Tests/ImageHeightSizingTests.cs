using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>Direct JPEG/PNG image height is an actual PDF matrix dimension.</summary>
public sealed class ImageHeightSizingTests
{
    private static PdfOptions Options() => new()
    {
        Images = new Dictionary<string, PdfImage>
        {
            ["chart"] = PdfImage.LoadImage(JpegResourceTests.Bytes("rgb-420"))
        }
    };

    private static byte[] Pdf(string html, PdfOptions? options = null)
        => TableLayoutTests.Pdf(html, options ?? Options());

    private static void SamePdf(string actual, string physicalControl, PdfOptions? options = null)
        => Assert.That(Pdf(actual, options), Is.EqualTo(Pdf(physicalControl, options)));

    private static void Reject(string html, string code, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        output.Write("previous PDF"u8);
        var before = output.ToArray();
        output.Position = 4;
        var error = Assert.Throws<FactsPdfException>(() =>
            PdfConverter.Convert(html, output, options ?? Options()));
        Assert.That(error!.Code, Is.EqualTo(code));
        Assert.That(output.ToArray(), Is.EqualTo(before));
        Assert.That(output.Position, Is.EqualTo(4));
    }

    [Test]
    public void PhysicalHeightDerivesIntrinsicWidthAndMovesFollowingText()
    {
        const string actual = "<img src=chart style='height:36pt'><p>AFTER</p>";
        const string control = "<img src=chart style='width:72pt'><p>AFTER</p>";
        SamePdf(actual, control);
        var page = TableLayoutTests.Layout(actual, Options()).Single();
        Assert.That(page.Images.Single().Width, Is.EqualTo(72).Within(0.000001));
        Assert.That(page.Images.Single().Height, Is.EqualTo(36).Within(0.000001));
        Assert.That(page.Runs.Single().Text, Is.EqualTo("AFTER"));
    }

    [Test]
    public void BorderBoxHeightCountsVerticalEdgesExactlyOnce()
    {
        const string actual =
            "<img src=chart style='height:50pt;box-sizing:border-box;" +
            "padding:2pt;border:1pt solid red;background:white'>";
        const string control =
            "<img src=chart style='width:88pt;padding:2pt;" +
            "border:1pt solid red;background:white'>";
        SamePdf(actual, control);
        var page = TableLayoutTests.Layout(actual, Options()).Single();
        Assert.That(page.Images.Single().Width, Is.EqualTo(88).Within(0.000001));
        Assert.That(page.Images.Single().Height, Is.EqualTo(44).Within(0.000001));
        Assert.That(page.PaintBoxes.Single().Height, Is.EqualTo(50).Within(0.000001));
    }

    [Test]
    public void BothPhysicalDimensionsProduceDeliberatelyStretchedPdfImage()
    {
        const string html = "<img src=chart style='width:90pt;height:35pt'>";
        var page = TableLayoutTests.Layout(html, Options()).Single();
        Assert.That(page.Images.Single().Width, Is.EqualTo(90).Within(0.000001));
        Assert.That(page.Images.Single().Height, Is.EqualTo(35).Within(0.000001));
        Assert.That(Encoding.Latin1.GetString(Pdf(html)), Does.Contain("90 0 0 35"));
    }

    [Test]
    public void WidthConstraintsAffectWidthButNotAnExplicitHeight()
    {
        const string html =
            "<section style='width:200pt'><img src=chart " +
            "style='height:36pt;min-width:120pt;max-width:100pt'></section>";
        var image = TableLayoutTests.Layout(html, Options()).Single().Images.Single();
        Assert.That(image.Width, Is.EqualTo(120).Within(0.000001));
        Assert.That(image.Height, Is.EqualTo(36).Within(0.000001));
    }

    [Test]
    public void CssCascadeCssWideResetAndDirectCellImageAreMeasured()
    {
        SamePdf(
            "<style>img.logo{height:36pt!important}img{height:18pt}</style>" +
            "<img src=chart class=logo style='height:20pt'>",
            "<img src=chart style='width:72pt'>");
        SamePdf(
            "<img src=chart style='height:36pt;height:auto'>",
            "<img src=chart>");
        SamePdf(
            "<section style='height:36pt'><img src=chart style='height:inherit'></section>",
            "<section style='height:36pt'><img src=chart style='width:72pt'></section>");
        const string cell =
            "<table style='width:160pt'><colgroup><col style='width:160pt'></colgroup>" +
            "<tbody><tr><td><img src=chart style='height:36pt'></td></tr></tbody></table>";
        SamePdf(cell, cell.Replace("height:36pt", "width:72pt", StringComparison.Ordinal));
    }

    [TestCase("<img src=chart style='height:0'>", "FPDF1302")]
    [TestCase("<img src=chart style='height:0.1pt;box-sizing:border-box;padding:2pt'>", "FPDF1302")]
    [TestCase("<img src=chart style='height:50%'>", "FPDF1202")]
    [TestCase("<img src=chart style='height:2em'>", "FPDF1201")]
    [TestCase("<img src=chart style='height:2rem'>", "FPDF1201")]
    [TestCase("<img src=chart style='height:calc(20pt - 1pt)'>", "FPDF1202")]
    [TestCase("<img src=chart style='height:-5pt'>", "FPDF1202")]
    [TestCase("<img src=chart style='height:999pt'>", "FPDF1302")]
    [TestCase("<p style='height:20pt'>BAD</p>", "FPDF1201")]
    [TestCase("<table style='height:20pt'>", "FPDF1201")]
    public void UnsupportedOrUnplaceableHeightPreservesExistingOutput(string html, string code)
        => Reject(html, code);

    [Test]
    public void RepeatedHeightConstrainedImagesStayAtomicAcrossPages()
    {
        var options = Options() with { PageHeight = 120, Margin = 10 };
        var source = "<img src=chart style='height:36pt'>";
        var control = "<img src=chart style='width:72pt'>";
        var actual = source + "<p>ITEM1</p>" + source + "<p>ITEM2</p>" + source;
        var expected = control + "<p>ITEM1</p>" + control + "<p>ITEM2</p>" + control;
        SamePdf(actual, expected, options);
        var pages = TableLayoutTests.Layout(actual, options);
        Assert.That(pages.Count, Is.GreaterThan(1));
        Assert.That(pages.Sum(page => page.Images.Count), Is.EqualTo(3));
    }
}
