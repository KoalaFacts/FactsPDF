using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace FactsPDF.Tests;

public sealed class ImageCompositionTests
{
    private const string Body = "<body style='font-size:10pt;line-height:2'>";
    private const string Image = "<img src='chart' style='width:120pt'>";
    private static PdfOptions Options(bool embedded = false)
    {
        var normal = embedded ? PdfFont.LoadTrueType(FontFixture.Create(listDigits: true)) : null;
        return new()
        {
            PageWidth = 220, PageHeight = 100, Margin = 10, SubsetFonts = true,
            Images = new Dictionary<string, PdfImage> { ["chart"] = PdfImage.LoadImage(JpegResourceTests.Bytes("rgb-420")) },
            Fonts = normal is null ? [] : [normal],
            BoldFonts = embedded ? [PdfFont.LoadTrueType(FontFixture.Create(listDigits: true))] : [],
            ItalicFonts = embedded ? [PdfFont.LoadTrueType(FontFixture.Create(listDigits: true))] : []
        };
    }
    private static List<LayoutPage> Layout(string content, PdfOptions options)
        => TextLayout.Layout(HtmlDocumentReader.ReadTree(Body + content + "</body>", options, default), options, default);

    [TestCase(false)]
    [TestCase(true)]
    public void ImageExactlyFitsAfterListAndNextMarkerMovesWithItsBody(bool embedded)
    {
        var pages = Layout("<ol><li>A</li></ol>" + Image + "<ol><li>B</li></ol>", Options(embedded));
        Assert.That(pages, Has.Count.EqualTo(2));
        Assert.That(pages[0].Runs.Select(r => r.Text), Is.EqualTo(new[] { "1.", "A" }));
        Assert.That(pages[0].Images.Single().Top, Is.EqualTo(30));
        Assert.That(pages[0].Images.Single().Height, Is.EqualTo(60));
        Assert.That(pages[1].Images, Is.Empty);
        Assert.That(pages[1].Runs.Select(r => r.Text), Is.EqualTo(new[] { "1.", "B" }));
        Assert.That(pages[1].Runs.Select(r => r.Baseline), Is.All.EqualTo(77).Within(0.000001));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ImageMovesAfterTwoListLinesAndNextListUsesRemainingSpace(bool embedded)
    {
        var pages = Layout("<ol><li>A</li><li>B</li></ol>" + Image + "<ul><li>A</li></ul>", Options(embedded));
        Assert.That(pages, Has.Count.EqualTo(2));
        Assert.That(pages[0].Runs.Select(r => r.Text), Is.EqualTo(new[] { "1.", "A", "2.", "B" }));
        Assert.That(pages[0].Images, Is.Empty);
        Assert.That(pages[1].Images.Single().Top, Is.EqualTo(10));
        Assert.That(pages[1].Runs.Single().Text, Is.EqualTo("A"));
        Assert.That(pages[1].Runs.Single().Baseline, Is.EqualTo(17).Within(0.000001));
        Assert.That(pages[1].Discs.Single().Y, Is.EqualTo(20).Within(0.000001));
    }

    [Test]
    public void FontRolesAndListCountersSurviveAnImageOnlyMiddlePage()
    {
        var options = Options(true);
        var pages = Layout("<ol style='font-weight:bold'><li><p style='margin-top:0;margin-bottom:0;break-after:page'>A</p></li></ol>"
            + "<img src='chart' style='width:160pt'>"
            + "<ol style='font-style:italic'><li>B</li></ol>", options);
        Assert.That(pages, Has.Count.EqualTo(3));
        Assert.That(pages[1].Runs, Is.Empty); Assert.That(pages[1].Images, Has.Count.EqualTo(1));
        Assert.That(pages[1].Images.Single().Top, Is.EqualTo(10));
        Assert.That(pages[0].Runs.Select(r => r.Text), Is.EqualTo(new[] { "1.", "A" }));
        Assert.That(pages[2].Runs.Select(r => r.Text), Is.EqualTo(new[] { "1.", "B" }));
        Assert.That(pages[0].Runs.All(r => ReferenceEquals(r.Font, options.BoldFonts[0])), Is.True);
        Assert.That(pages[2].Runs.All(r => ReferenceEquals(r.Font, options.ItalicFonts[0])), Is.True);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DecoratedAncestorSlicesAcrossImageAndFollowingNestedList(bool embedded)
    {
        var options = Options(embedded) with { PageHeight = 130 };
        var pages = Layout("<section style='padding:4pt;border:1pt solid red'>"
            + "<ol><li>A</li><li>B</li></ol>" + Image
            + "<ol><li>A<ul><li>B</li></ul></li></ol></section>", options);
        Assert.That(pages, Has.Count.EqualTo(2));
        Assert.That(pages[0].Images.Single().Top, Is.EqualTo(55));
        Assert.That(pages[0].Images.Single().X, Is.EqualTo(15));
        Assert.That(pages[1].Runs.Select(r => r.Text), Is.EqualTo(new[] { "1.", "A", "B" }));
        Assert.That(pages[1].Discs, Has.Count.EqualTo(1));
        var first = pages[0].PaintBoxes.Single(); var last = pages[1].PaintBoxes.Single();
        Assert.That((first.Top, first.Height, first.IsFirstFragment, first.IsLastFragment), Is.EqualTo((10d, 110d, true, false)));
        Assert.That((last.Top, last.Height, last.IsFirstFragment, last.IsLastFragment), Is.EqualTo((10d, 45d, false, true)));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void MultiPageImageAndListResourceBudgetsFailBeforeDestinationChanges(bool embedded)
    {
        var options = Options(embedded);
        var html = Body + "<ol><li>A</li></ol>" + Image + "<ol><li>B</li></ol></body>";
        using var full = new MemoryStream();
        var result = PdfConverter.Convert(html, full, options);
        Assert.That(result.PageCount, Is.EqualTo(2));
        var text = Encoding.Latin1.GetString(full.ToArray());
        Assert.That(Regex.Matches(text, @"/Subtype /Image").Count, Is.EqualTo(1));
        foreach (var limited in new[] { options with { MaxPages = 1 }, options with { MaxDisplayCommands = 4 },
            options with { MaxOutputBytes = checked((int)full.Length - 1) } })
        {
            using var output = new MemoryStream(); output.Write("sentinel"u8); output.Position = 2;
            var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, limited));
            Assert.That(error!.Code, Is.EqualTo(limited.MaxPages == 1 ? "FPDF1303" : "FPDF1401"));
            Assert.That(output.ToArray(), Is.EqualTo("sentinel"u8.ToArray())); Assert.That(output.Position, Is.EqualTo(2));
        }
        using var exact = new MemoryStream();
        PdfConverter.Convert(html, exact, options with { MaxPages = 2, MaxDisplayCommands = 5, MaxOutputBytes = checked((int)full.Length) });
        Assert.That(exact.ToArray(), Is.EqualTo(full.ToArray()));
    }
}
