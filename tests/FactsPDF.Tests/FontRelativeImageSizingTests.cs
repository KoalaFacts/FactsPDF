using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>Computed em/rem image sizes must reach actual PDF matrices, table rows and pagination.</summary>
public sealed class FontRelativeImageSizingTests
{
    private static PdfOptions Options(double pageHeight = 300) => new()
    {
        PageWidth = 360,
        PageHeight = pageHeight,
        Margin = 10,
        Images = new Dictionary<string, PdfImage>
        {
            ["chart"] = PdfImage.FromBytes(JpegResourceTests.Bytes("rgb-420"))
        }
    };

    private static byte[] Pdf(string html, PdfOptions? options = null)
        => TableLayoutTests.Pdf(html, options ?? Options());

    private static void SamePdf(string relative, string physical, PdfOptions? options = null)
        => Assert.That(Pdf(relative, options), Is.EqualTo(Pdf(physical, options)));

    private static void Reject(string html, string code)
    {
        using var destination = new MemoryStream();
        destination.Write("original report PDF"u8);
        var prior = destination.ToArray();
        destination.Position = 5;
        var error = Assert.Throws<FactsPdfException>(() =>
            PdfConverter.Convert(html, destination, Options()));
        Assert.That(error!.Code, Is.EqualTo(code));
        Assert.That(destination.ToArray(), Is.EqualTo(prior));
        Assert.That(destination.Position, Is.EqualTo(5));
    }

    [TestCase("width:8em", "width:80pt")]
    [TestCase("width:6rem", "width:96pt")]
    [TestCase("width:8rem;max-width:6em", "width:60pt")]
    [TestCase("width:2em;min-width:4rem", "width:64pt")]
    [TestCase("height:4em", "height:40pt")]
    [TestCase("height:2rem", "height:32pt")]
    [TestCase("width:120pt;max-height:2em", "width:40pt")]
    [TestCase("width:40pt;min-height:4rem", "width:128pt")]
    [TestCase("width:70pt;padding:0.25em 0.5rem", "width:70pt;padding:2.5pt 8pt")]
    [TestCase("width:8em;box-sizing:border-box;padding:0.25em;border:1pt solid red",
        "width:80pt;box-sizing:border-box;padding:2.5pt;border:1pt solid red")]
    [TestCase("width:10em;height:2rem", "width:100pt;height:32pt")]
    public void DirectImageRelativeLengthsMatchComputedPhysicalPdf(string relative, string physical)
    {
        const string before = "<html style='font-size:16pt'><body>" +
            "<section style='font-size:10pt;width:260pt'>";
        const string after = "<p style='margin:0'>SIGNED</p></section></body></html>";
        var actual = before + "<img src=chart style='" + relative + "'>" + after;
        SamePdf(actual, before + "<img src=chart style='" + physical + "'>" + after);
        var pages = TableLayoutTests.Layout(actual, Options());
        Assert.That(pages.SelectMany(page => page.Runs).Select(run => run.Text),
            Is.EqualTo(new[] { "SIGNED" }));
        Assert.That(pages.Sum(page => page.Images.Count), Is.EqualTo(1));
    }

    [Test]
    public void WinningImageFontSizeAndImportantWidthsControlImageGeometry()
    {
        const string actual = "<html style='font-size:16pt'><body>" +
            "<style>img.logo{width:5em!important}img{width:2rem}</style>" +
            "<section style='font-size:10pt;width:230pt'>" +
            "<img class=logo src=chart style='font-size:20pt;width:3em;max-width:6rem'>" +
            "<p style='margin:0'>APPROVED</p></section></body></html>";
        const string physical = "<html style='font-size:16pt'><body>" +
            "<section style='font-size:10pt;width:230pt'>" +
            "<img src=chart style='width:96pt'>" +
            "<p style='margin:0'>APPROVED</p></section></body></html>";
        SamePdf(actual, physical);
        Assert.That(TableLayoutTests.Layout(actual, Options()).Single().Images.Single().Width,
            Is.EqualTo(96).Within(0.000001));
    }

    [Test]
    public void DirectTableCellImageUsesInheritedFontAndFixedCellWidth()
    {
        const string before = "<html style='font-size:16pt'><body><table style='width:200pt'>" +
            "<colgroup><col style='width:100pt'><col style='width:100pt'></colgroup>" +
            "<tbody><tr><td style='font-size:10pt'>PART</td><td style='font-size:10pt'>";
        const string after = "</td></tr></tbody></table></body></html>";
        const string relative = "<img src=chart style='width:5em;max-width:4rem;padding:0.2em'>";
        const string physical = "<img src=chart style='width:50pt;max-width:64pt;padding:2pt'>";
        SamePdf(before + relative + after, before + physical + after);
        var row = TableLayoutTests.Layout(before + relative + after, Options()).Single();
        Assert.That(row.Images.Single().Width, Is.EqualTo(50).Within(0.000001));
        Assert.That(row.Runs.Select(x => x.Text), Does.Contain("PART"));
    }

    [Test]
    public void RelativeHeightLimitedImagesMoveAsWholeItemsAcrossPages()
    {
        var options = Options(120);
        const string relative = "<img src=chart style='font-size:10pt;width:12em;max-height:3em'>";
        const string physical = "<img src=chart style='width:60pt'>";
        var actual = relative + "<p style='margin:0'>FIRST</p>" +
            relative + "<p style='margin:0'>SECOND</p>" + relative;
        var oracle = physical + "<p style='margin:0'>FIRST</p>" +
            physical + "<p style='margin:0'>SECOND</p>" + physical;
        SamePdf(actual, oracle, options);
        var pages = TableLayoutTests.Layout(actual, options);
        Assert.That(pages.Count, Is.GreaterThan(1));
        Assert.That(pages.Sum(p => p.Images.Count), Is.EqualTo(3));
        Assert.That(pages.SelectMany(p => p.Runs).Select(r => r.Text),
            Is.EqualTo(new[] { "FIRST", "SECOND" }));
        Assert.That(Encoding.Latin1.GetString(Pdf(actual, options)), Does.Contain("60 0 0 30"));
    }

    [TestCase("<img src=chart style='height:50%'>", "FPDF1202")]
    [TestCase("<img src=chart style='max-height:25%'>", "FPDF1202")]
    [TestCase("<img src=chart style='height:calc(2em + 1pt)'>", "FPDF1202")]
    [TestCase("<img src=chart style='width:-2em'>", "FPDF1202")]
    [TestCase("<img src=chart style='height:0em'>", "FPDF1302")]
    [TestCase("<img src=chart style='width:0rem'>", "FPDF1302")]
    [TestCase("<img src=chart style='height:100rem'>", "FPDF1302")]
    [TestCase("<img src=chart style='width:100rem'>", "FPDF1302")]
    [TestCase("<img src=chart style='width:4em;min-height:4em;max-width:3em'>", "FPDF1302")]
    [TestCase("<table style='width:4em;min-width:1em'><colgroup><col style='width:40pt'></colgroup><tbody><tr><td>A</td></tr></tbody></table>", "FPDF1201")]
    [TestCase("<table><colgroup><col style='width:4rem;padding:1rem'></colgroup><tbody><tr><td>A</td></tr></tbody></table>", "FPDF1201")]
    [TestCase("<table style='width:100pt;padding:1em;min-width:1em'><colgroup><col style='width:100pt'></colgroup><tbody><tr><td>A</td></tr></tbody></table>", "FPDF1201")]
    public void UnsupportedOrUnplaceableLengthsPreserveExistingPdf(string html, string code)
        => Reject(html, code);

    [Test]
    public void InvalidUnmatchedRelativeLengthStillRejects()
        => Reject("<style>.unused{max-height:-2em}</style><img src=chart>", "FPDF1202");
}
