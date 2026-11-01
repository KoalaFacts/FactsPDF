using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>Images share a real constrained box measure across standalone and fixed-track cell layout.</summary>
public sealed class ResponsiveImageSizingTests
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

    private static void SamePdf(string constrained, string explicitWidth, PdfOptions? options = null)
        => Assert.That(Pdf(constrained, options), Is.EqualTo(Pdf(explicitWidth, options)));

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
    public void UpperAndLowerWidthsChangeActualImageMatrixAndFollowOnText()
    {
        const string constrained =
            "<section style='width:200pt'><img src='chart' style='width:180pt;max-width:50%'>" +
            "<p style='margin:0'>AFTER</p><img src='chart' style='width:50pt;min-width:120pt'></section>";
        const string control =
            "<section style='width:200pt'><img src='chart' style='width:100pt'>" +
            "<p style='margin:0'>AFTER</p><img src='chart' style='width:120pt'></section>";
        SamePdf(constrained, control);
        var layout = TableLayoutTests.Layout(constrained, Options()).Single();
        Assert.That(layout.Images.Select(x => (x.Width, x.Height)),
            Is.EqualTo(new[] { (100d, 50d), (120d, 60d) }));
        Assert.That(layout.Runs.Single().Text, Is.EqualTo("AFTER"));
        Assert.That(Encoding.Latin1.GetString(Pdf(constrained)), Does.Contain("100 0 0 50"));
    }

    [Test]
    public void BorderBoxWidthAndConstraintsCountPaddingAndBordersOnce()
    {
        const string constrained =
            "<img src='chart' style='width:180pt;max-width:96pt;box-sizing:border-box;" +
            "padding:2pt;border:1pt solid red;background:white'>";
        const string control =
            "<img src='chart' style='width:90pt;padding:2pt;border:1pt solid red;background:white'>";
        SamePdf(constrained, control);
        var item = TableLayoutTests.Layout(constrained, Options()).Single();
        Assert.That(item.Images.Single().Width, Is.EqualTo(90).Within(0.000001));
        Assert.That(item.PaintBoxes.Single().Width, Is.EqualTo(96).Within(0.000001));
        Assert.That(item.Images.Single().Height, Is.EqualTo(45).Within(0.000001));
    }

    [Test]
    public void MinimumOverridesMaximumAndImageKeepsNaturalAspectRatio()
    {
        SamePdf(
            "<img src='chart' style='width:80pt;max-width:100pt;min-width:130pt'>",
            "<img src='chart' style='width:130pt'>");
        SamePdf(
            "<section style='width:200pt'><img src='chart' style='max-width:50%'></section>",
            "<section style='width:200pt'><img src='chart' style='width:100pt'></section>");
    }

    [Test]
    public void ImportantAndCssWideResetsUseExistingCascadeNotLastTextToken()
    {
        const string rules = "<style>img.brand{max-width:84pt!important}img{max-width:140pt}</style>";
        SamePdf(
            rules + "<img class=brand src=chart style='width:180pt;max-width:100pt'>",
            "<img src=chart style='width:84pt'>");
        SamePdf(
            "<section style='max-width:96pt'><img src=chart style='width:160pt;max-width:inherit'></section>",
            "<section style='max-width:96pt'><img src=chart style='width:96pt'></section>");
        SamePdf(
            "<img src=chart style='width:100pt;max-width:40pt;max-width:unset'>",
            "<img src=chart style='width:100pt'>");
        SamePdf(
            "<img src=chart style='width:100pt;box-sizing:border-box;box-sizing:initial;padding:2pt'>",
            "<img src=chart style='width:100pt;padding:2pt'>");
    }

    [Test]
    public void FixedTrackCellAndColspanUseAvailableContentWidthAndPreserveResources()
    {
        var actual = "<table style='width:200pt'><colgroup><col style='width:40%'><col style='width:60%'></colgroup>" +
            "<tbody><tr><td colspan='2' style='padding:2pt'><img src=chart style='max-width:50%'></td></tr>" +
            "<tr><td>PART</td><td><img src=chart style='width:120pt;max-width:100%'></td></tr></tbody></table>";
        var oracle = "<table style='width:200pt'><colgroup><col style='width:40%'><col style='width:60%'></colgroup>" +
            "<tbody><tr><td colspan='2' style='padding:2pt'><img src=chart style='width:98pt'></td></tr>" +
            "<tr><td>PART</td><td><img src=chart style='width:120pt'></td></tr></tbody></table>";
        SamePdf(actual, oracle);
        var page = TableLayoutTests.Layout(actual, Options()).Single();
        Assert.That(page.Images.Select(i => i.Width), Is.EqualTo(new[] { 98d, 120d }));
        Assert.That(page.Runs.Select(i => i.Text), Is.EqualTo(new[] { "PART" }));
        Assert.That(Encoding.Latin1.GetString(Pdf(actual)).Split("/Subtype /Image").Length - 1,
            Is.EqualTo(1), "Repeated placements must share the explicit image resource");
    }

    [Test]
    public void BoundedImageCanFitPageWithoutImplicitAutoShrink()
    {
        var options = Options() with { PageHeight = 100, Margin = 10 };
        const string constrained = "<img src=chart style='width:200pt;max-width:140pt;padding:2pt'>";
        const string control = "<img src=chart style='width:140pt;padding:2pt'>";
        SamePdf(constrained, control, options);
        Reject("<img src=chart style='width:200pt;padding:2pt'>", "FPDF1302", options);
    }

    [TestCase("<img src=chart style='max-width:calc(100% - 4pt)'>", "FPDF1202")]
    [TestCase("<img src=chart style='max-width:auto'>", "FPDF1202")]
    [TestCase("<img src=chart style='min-width:none'>", "FPDF1202")]
    [TestCase("<img src=chart style='min-width:2rem'>", "FPDF1201")]
    [TestCase("<img src=chart style='max-width:3em'>", "FPDF1201")]
    [TestCase("<img src=chart style='box-sizing:border-box;max-width:5pt;padding:5pt'>", "FPDF1302")]
    [TestCase("<img src=chart style='min-width:700pt'>", "FPDF1302")]
    [TestCase("<img src=chart style='width:0;max-width:100%'>", "FPDF1302")]
    [TestCase("<style>.never{max-width:6vw}</style><img src=chart>", "FPDF1202")]
    [TestCase("<table style='max-width:120pt'>", "FPDF1201")]
    [TestCase("<span style='box-sizing:border-box'>A</span>", "FPDF1201")]
    public void UnsupportedOrUnplaceableValuesPreserveOriginalOutput(string html, string code)
        => Reject(html, code);

    [Test]
    public void RepeatedConstrainedImageRemainsAtomicAcrossPageBoundaries()
    {
        var options = Options() with { PageHeight = 120, Margin = 10 };
        var image = "<img src=chart style='width:180pt;max-width:100pt'>";
        var control = "<img src=chart style='width:100pt'>";
        var constrained = image + "<p style='margin:0'>ITEM1</p>" + image + "<p style='margin:0'>ITEM2</p>" + image;
        var explicitWidth = control + "<p style='margin:0'>ITEM1</p>" + control + "<p style='margin:0'>ITEM2</p>" + control;
        SamePdf(constrained, explicitWidth, options);
        var pages = TableLayoutTests.Layout(constrained, options);
        Assert.That(pages.Count, Is.GreaterThan(1));
        Assert.That(pages.Sum(page => page.Images.Count), Is.EqualTo(3));
        Assert.That(pages.SelectMany(page => page.Runs).Select(run => run.Text), Is.EqualTo(new[] { "ITEM1", "ITEM2" }));
    }
}
