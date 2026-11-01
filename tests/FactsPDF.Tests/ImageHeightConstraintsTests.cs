using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>Physical image height bounds change actual PDF matrices, not just CSS parsing.</summary>
public sealed class ImageHeightConstraintsTests
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

    private static void SamePdf(string actual, string expected, PdfOptions? options = null)
        => Assert.That(Pdf(actual, options), Is.EqualTo(Pdf(expected, options)));

    private static void Reject(string html, string code)
    {
        using var output = new MemoryStream();
        output.Write("retained customer PDF"u8);
        var bytes = output.ToArray();
        output.Position = 3;
        var failure = Assert.Throws<FactsPdfException>(() =>
            PdfConverter.Convert(html, output, Options()));
        Assert.That(failure!.Code, Is.EqualTo(code));
        Assert.That(output.ToArray(), Is.EqualTo(bytes));
        Assert.That(output.Position, Is.EqualTo(3));
    }

    [Test]
    public void MaximumAndMinimumHeightScaleAutoHeightWithActualGlyphAndImageGeometry()
    {
        const string actual =
            "<img src=chart style='width:160pt;max-height:30pt'><p>FIRST</p>" +
            "<img src=chart style='width:60pt;min-height:50pt'><p>SECOND</p>";
        const string physical =
            "<img src=chart style='width:60pt'><p>FIRST</p>" +
            "<img src=chart style='width:100pt'><p>SECOND</p>";
        SamePdf(actual, physical);
        var page = TableLayoutTests.Layout(actual, Options()).Single();
        Assert.That(page.Images.Select(i => (i.Width, i.Height)),
            Is.EqualTo(new[] { (60d, 30d), (100d, 50d) }));
        Assert.That(page.Runs.Select(x => x.Text), Is.EqualTo(new[] { "FIRST", "SECOND" }));
        Assert.That(Encoding.Latin1.GetString(Pdf(actual)), Does.Contain("60 0 0 30"));
    }

    [Test]
    public void BorderBoxLimitCountsVerticalPaddingAndBordersOnce()
    {
        const string actual = "<img src=chart style='width:180pt;max-height:50pt;" +
            "box-sizing:border-box;padding:2pt;border:1pt solid red;background:white'>";
        const string physical = "<img src=chart style='width:88pt;" +
            "padding:2pt;border:1pt solid red;background:white'>";
        SamePdf(actual, physical);
        var page = TableLayoutTests.Layout(actual, Options()).Single();
        Assert.That(page.Images.Single().Height, Is.EqualTo(44).Within(0.000001));
        Assert.That(page.PaintBoxes.Single().Height, Is.EqualTo(50).Within(0.000001));
    }

    [Test]
    public void MinimumWinsMaximumAndExplicitPhysicalHeightCanBeClampedWithoutMovingWidth()
    {
        SamePdf("<img src=chart style='width:80pt;max-height:50pt;min-height:60pt'>",
            "<img src=chart style='width:120pt'>");
        SamePdf("<img src=chart style='height:36pt;max-height:20pt'>",
            "<img src=chart style='width:72pt;height:20pt'>");
    }

    [Test]
    public void ImportantCssWideResetsAndFixedColumnImageUseSharedGeometry()
    {
        SamePdf("<style>img.logo{max-height:24pt!important}img{max-height:60pt}</style>" +
            "<img class=logo src=chart style='width:160pt;max-height:20pt'>",
            "<img src=chart style='width:48pt'>");
        SamePdf("<img src=chart style='width:120pt;max-height:20pt;max-height:none'>",
            "<img src=chart style='width:120pt'>");
        SamePdf("<img src=chart style='width:60pt;min-height:60pt;min-height:initial'>",
            "<img src=chart style='width:60pt'>");
        const string cell =
            "<table style='width:160pt'><colgroup><col style='width:160pt'></colgroup>" +
            "<tbody><tr><td><img src=chart style='width:120pt;max-height:20pt'></td></tr></tbody></table>";
        SamePdf(cell, cell.Replace("width:120pt;max-height:20pt", "width:40pt", StringComparison.Ordinal));
    }

    [Test]
    public void MaximumHeightKeepsWholeImagesAndTextAcrossPages()
    {
        var options = Options() with { PageHeight = 120, Margin = 10 };
        const string bounded = "<img src=chart style='width:160pt;max-height:30pt'>";
        const string physical = "<img src=chart style='width:60pt'>";
        var source = bounded + "<p>ONE</p>" + bounded + "<p>TWO</p>" + bounded;
        var oracle = physical + "<p>ONE</p>" + physical + "<p>TWO</p>" + physical;
        SamePdf(source, oracle, options);
        var pages = TableLayoutTests.Layout(source, options);
        Assert.That(pages.Count, Is.GreaterThan(1));
        Assert.That(pages.Sum(p => p.Images.Count), Is.EqualTo(3));
        Assert.That(pages.SelectMany(p => p.Runs).Select(r => r.Text), Is.EqualTo(new[] { "ONE", "TWO" }));
    }

    [TestCase("<img src=chart style='max-height:30%'>", "FPDF1202")]
    [TestCase("<img src=chart style='min-height:20%'>", "FPDF1202")]
    [TestCase("<img src=chart style='max-height:2rem'>", "FPDF1201")]
    [TestCase("<img src=chart style='min-height:2em'>", "FPDF1201")]
    [TestCase("<img src=chart style='max-height:calc(20pt - 4pt)'>", "FPDF1202")]
    [TestCase("<img src=chart style='max-height:-5pt'>", "FPDF1202")]
    [TestCase("<img src=chart style='max-height:0'>", "FPDF1302")]
    [TestCase("<img src=chart style='width:140pt;min-width:130pt;max-height:20pt'>", "FPDF1302")]
    [TestCase("<img src=chart style='min-height:800pt'>", "FPDF1302")]
    [TestCase("<p style='max-height:30pt'>NO</p>", "FPDF1201")]
    [TestCase("<section style='max-height:30pt'>NO</section>", "FPDF1201")]
    [TestCase("<table style='max-height:30pt'>", "FPDF1201")]
    public void UnsupportedOrConflictingConstraintsPreserveExistingPdf(string html, string code)
        => Reject(html, code);

    [Test]
    public void UnmatchedUnsupportedSyntaxDoesNotGetSilentlyAccepted()
        => Reject("<style>.never{max-height:calc(40pt - 2pt)}</style><img src=chart>", "FPDF1202");
}
