using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace FactsPDF.Tests;

public sealed class ImageLayoutTests
{
    private static PdfOptions Options(string file = "rgb-420") => new()
    { Images = new Dictionary<string, PdfImage> { ["chart"] = PdfImage.LoadImage(JpegResourceTests.Bytes(file)) } };
    private static (byte[] Bytes, string Text, int Pages) Render(string html, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        var result = PdfConverter.Convert(html, output, options ?? Options());
        return (output.ToArray(), Encoding.Latin1.GetString(output.ToArray()), result.PageCount);
    }

    [TestCase("rgb-420", "", "240 0 0 120 36 685.89")]
    [TestCase("density-300", "", "240 0 0 120 36 685.89")]
    [TestCase("gray", "", "48 0 0 24 36 781.89")]
    [TestCase("rgb-420", "width:200pt", "200 0 0 100 36 705.89")]
    public void ImageOnlyUsesAuthoredAspectRatioAndFixedNaturalDensity(string file, string style, string matrix)
    {
        var pdf = Render($"<img src='chart' style='{style}'>", Options(file));
        Assert.That(pdf.Pages, Is.EqualTo(1)); Assert.That(pdf.Text, Does.Contain(matrix + " cm /Im1 Do"));
        Assert.That(pdf.Text, Does.Contain("/BitsPerComponent 8 /ColorSpace /Device" + (file == "gray" ? "Gray" : "RGB")));
        Assert.That(pdf.Text, Does.Contain("/ColorTransform " + (file == "gray" ? "0" : "1")));
        var data = JpegResourceTests.Bytes(file);
        Assert.That(pdf.Bytes.AsSpan().IndexOf(data), Is.GreaterThan(0));
        Assert.That(pdf.Text, Does.Contain("/Length " + data.Length + " >>\nstream\n"));
    }

    [Test]
    public void PercentWidthResolvesAgainstParentContentAndPaddingIsOutsideImage()
    {
        var pdf = Render("<section style='width:300pt;padding:8pt;border:1pt solid red;text-align:right'>"
            + "<img src='chart' style='width:50%;padding:2pt;border:1pt solid blue;background-color:white'></section>");
        Assert.That(pdf.Text, Does.Contain("150 0 0 75 48 718.89 cm /Im1 Do"));
        Assert.That(pdf.Pages, Is.EqualTo(1));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void BothSerializersPreserveBinaryBytesAndUseIdentityReuse(bool unicode)
    {
        var o = Options(); var shared = o.Images["chart"];
        o = o with { Images = new Dictionary<string, PdfImage> { ["chart"] = shared, ["alias"] = shared,
            ["unused"] = PdfImage.LoadImage(JpegResourceTests.Bytes("gray")) },
            Fonts = unicode ? [PdfFont.LoadTrueType(FontFixture.Create())] : [] };
        var pdf = Render((unicode ? "<p>中文</p>" : "<p>BEFORE</p>")
            + "<img src='chart' style='width:80pt'><img src='alias' style='width:80pt'>", o);
        Assert.That(Regex.Matches(pdf.Text, "/Subtype /Image").Count, Is.EqualTo(1));
        Assert.That(Regex.Matches(pdf.Text, "/Im1 Do").Count, Is.EqualTo(2));
        var data = JpegResourceTests.Bytes("rgb-420"); var at = pdf.Bytes.AsSpan().IndexOf(data);
        Assert.That(at, Is.GreaterThan(0)); Assert.That(pdf.Bytes.AsSpan(at + data.Length).IndexOf(data), Is.EqualTo(-1));
    }

    [Test]
    public void EqualBytesInDifferentInstancesRemainDistinctAndUnusedBindingsDoNotChangePdf()
    {
        var o = Options(); var original = Render("<p>ASCII</p>").Bytes;
        Assert.That(Render("<p>ASCII</p>", new()).Bytes, Is.EqualTo(original));
        o = o with { Images = new Dictionary<string, PdfImage> { ["chart"] = o.Images["chart"],
            ["copy"] = PdfImage.LoadImage(JpegResourceTests.Bytes("rgb-420")) } };
        Assert.That(Regex.Matches(Render("<img src='chart'><img src='copy'>", o).Text, "/Subtype /Image").Count, Is.EqualTo(2));
    }

    [TestCase("<img src='chart' style='width:160pt'>", 100, 1)]
    [TestCase("<img src='chart' style='width:148pt;padding:2pt;border:1pt solid red'>", 100, 1)]
    [TestCase("<img src='chart' style='width:80pt'><img src='chart' style='width:100pt'>", 100, 2)]
    [TestCase("<p style='margin-top:0;margin-bottom:0;font-size:10pt;line-height:2'>A</p><div style='padding-top:5pt;border-top:1pt solid red'><img src='chart' style='width:140pt'></div>", 100, 2)]
    [TestCase("<img src='chart' style='width:100pt'><p style='break-before:page'>A</p>", 100, 2)]
    [TestCase("<p style='break-after:page'>A</p><img src='chart' style='width:100pt'>", 100, 2)]
    public void WholeOuterBoxPaginationAndExplicitBreaks(string html, int height, int pages)
        => Assert.That(Render(html, Options() with { PageHeight = height, Margin = 10 }).Pages, Is.EqualTo(pages));

    [TestCase("<img src='chart' style='width:0'>", "FPDF1302")]
    [TestCase("<img src='chart' style='width:600pt'>", "FPDF1302")]
    [TestCase("<img src='chart' style='width:162pt'>", "FPDF1302")]
    [TestCase("<img src='chart' style='width:150pt;padding:2pt;border:1pt solid red'>", "FPDF1302")]
    [TestCase("<div style='padding-top:11pt'><img src='chart' style='width:140pt'></div>", "FPDF1302")]
    public void InvalidWholeGeometryFailsBeforeWriting(string html, string code)
        => FailsWithoutWriting(html, Options() with { PageHeight = 100, Margin = 10 }, code);

    [Test]
    public void ImagesAndTheirDecorationAreChargedBeforeRetainingCommands()
    {
        FailsWithoutWriting("<img src='chart'><img src='chart'>", Options() with { MaxDisplayCommands = 1 }, "FPDF1401");
        FailsWithoutWriting("<img src='chart' style='background-color:red'>", Options() with { MaxDisplayCommands = 1 }, "FPDF1401");
        Assert.That(Render("<img src='chart' style='background-color:red'>", Options() with { MaxDisplayCommands = 2 }).Pages, Is.EqualTo(1));
        FailsWithoutWriting("<img src='chart' style='width:100pt'><img src='chart' style='width:100pt'>",
            Options() with { MaxPages = 1, PageHeight = 100, Margin = 10 }, "FPDF1303");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ExactOutputBudgetIncludesBinaryStreamsAndXref(bool unicode)
    {
        var o = Options() with { Fonts = unicode ? [PdfFont.LoadTrueType(FontFixture.Create())] : [] };
        var html = (unicode ? "<p>中文</p>" : "") + "<img src='chart'>";
        var full = Render(html, o);
        Assert.That(Render(html, o with { MaxOutputBytes = full.Bytes.Length }).Bytes, Is.EqualTo(full.Bytes));
        FailsWithoutWriting(html, o with { MaxOutputBytes = full.Bytes.Length - 1 }, "FPDF1401");
        FailsWithoutWriting(html, o with { MaxOutputBytes = 1000 }, "FPDF1401");
    }

    private static void FailsWithoutWriting(string html, PdfOptions options, string code)
    {
        using var output = new MemoryStream(); output.Write([1, 2, 3]); output.Position = 1;
        Assert.That(Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, options))!.Code, Is.EqualTo(code));
        Assert.That(output.ToArray(), Is.EqualTo(new byte[] { 1, 2, 3 })); Assert.That(output.Position, Is.EqualTo(1));
    }

    [Test]
    public void ImageContentAndTextKeepTheirOwnVerticalIntervalsInsideContinuedDecoration()
    {
        var o = Options() with { PageHeight = 130, Margin = 10 };
        const string html = "<div style='padding:3pt;border:1pt solid red'>"
            + "<p style='margin-top:0;margin-bottom:0;font-size:10pt;line-height:2'>BEFORE</p>"
            + "<img src='chart' style='width:200pt;padding:1pt;border:1pt solid blue'>"
            + "<p style='margin-top:0;margin-bottom:0;font-size:10pt;line-height:2'>AFTER</p></div>";
        var pages = TextLayout.Layout(HtmlDocumentReader.ReadTree(html, o, default), o, default);
        Assert.That(pages.Count, Is.EqualTo(3));
        Assert.That(pages[0].Runs.Single().Text, Is.EqualTo("BEFORE"));
        var image = pages[1].Images.Single();
        Assert.That(image.Top, Is.EqualTo(12)); Assert.That(image.Height, Is.EqualTo(100));
        Assert.That(pages[2].Runs.Single().Text, Is.EqualTo("AFTER"));
        var ancestors = pages.SelectMany(p => p.PaintBoxes).Where(b => b.X == 10).ToArray();
        Assert.That(ancestors, Has.Length.EqualTo(3));
        Assert.That(ancestors[0].IsFirstFragment, Is.True); Assert.That(ancestors[1].IsFirstFragment, Is.False);
        Assert.That(ancestors[1].IsLastFragment, Is.False); Assert.That(ancestors[2].IsLastFragment, Is.True);
    }

    [Test]
    public void CurrentColorBorderAndPageLocalResourcesArePreserved()
    {
        var o = Options() with { Images = new Dictionary<string, PdfImage>
            { ["chart"] = Options().Images["chart"], ["gray"] = PdfImage.LoadImage(JpegResourceTests.Bytes("gray")) } };
        var pdf = Render("<img src='chart' style='color:red;border:1pt solid;width:20pt'>"
            + "<p style='break-before:page'>PAGE2</p><img src='gray'>", o);
        var resources = Regex.Matches(pdf.Text, @"/XObject <<([^>]+)>>");
        Assert.That(resources, Has.Count.EqualTo(2));
        Assert.That(resources[0].Value, Does.Contain("/Im1 ").And.Not.Contain("/Im2 "));
        Assert.That(resources[1].Value, Does.Contain("/Im2 ").And.Not.Contain("/Im1 "));
        Assert.That(pdf.Text, Does.Contain("1 0 0 rg"));
    }
}
