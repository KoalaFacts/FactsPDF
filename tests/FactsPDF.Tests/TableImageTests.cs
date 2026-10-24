using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace FactsPDF.Tests;

public sealed class TableImageTests
{
    private const string Img = "<img src='logo'>";
    internal static PdfImage Logo()
    {
        var rows = new byte[16 * 129];
        for (var y = 0; y < 16; y++)
            for (var x = 0; x < 32; x++)
            { var i = y * 129 + 1 + x * 4; rows[i] = 255; rows[i + 2] = 100; rows[i + 3] = (byte)(x % 3 == 0 ? 0 : x % 3 == 1 ? 128 : 255); }
        return PdfImage.LoadImage(PngFixture.Image(rows, 32, 16));
    }
    internal static PdfOptions Options() => new() { Images = new Dictionary<string, PdfImage> { ["logo"] = Logo() } };
    private static string Cell(string content, string cellStyle = "", string tableStyle = "width:80pt")
        => TableLayoutTests.Table(content, tableStyle, cellStyle);

    [Test]
    public void ExistingTextTableAndStandaloneImageBytesRemainUnchanged()
    {
        Assert.That(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(TableLayoutTests.Pdf(TableLayoutTests.Fixture("points")))), Is.EqualTo("B110463C5918BC221DA31AA4F5434187447B558C627452A9AD9175D562F34424"));
        Assert.That(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(TableLayoutTests.Pdf("<section style='width:100pt;padding:2pt;border:1pt solid red'><img src='logo' style='width:50%;padding:2%;border:1pt solid blue;background-color:white'></section>", Options()))), Is.EqualTo("74A615A66C26317095B679A5C30669CDC0BCA7C308B1920926CBC4F3722829C5"));
    }

    [TestCase("left", 44)] [TestCase("center", 58.5)] [TestCase("right", 73)]
    public void CellAlignmentUsesCompleteDecoratedImageBox(string alignment, double x)
    {
        var html = "<table style='width:160pt;font-size:10pt;line-height:1'><colgroup><col style='width:80pt'><col style='width:80pt'></colgroup><tbody><tr>"
            + $"<td style='padding:4pt;border:1pt solid red;background-color:blue;text-align:{alignment}'><img src='logo' style='width:50%;padding:2pt;border:1pt solid green;background-color:white;text-align:right'></td><td>Z</td></tr></tbody></table>";
        var page = TableLayoutTests.Layout(html, Options()).Single(); var image = page.Images.Single();
        Assert.That(image.X, Is.EqualTo(x)); Assert.That(image.Top, Is.EqualTo(44));
        Assert.That(image.Width, Is.EqualTo(35)); Assert.That(image.Height, Is.EqualTo(17.5));
        Assert.That(page.PaintBoxes.Select(b => b.Height), Is.EqualTo(new[] { 33.5, 23.5 }));
        Assert.That(page.PaintBoxes.Select(b => b.Width), Is.EqualTo(new[] { 80d, 41 }));
        Assert.That(page.PaintBoxes.Select(b => b.Style.BackgroundColor), Is.EqualTo(new Rgb?[] { new(0, 0, 1), new(1, 1, 1) }));
        Assert.That(page.PaintBoxes[0].Order, Is.LessThan(page.PaintBoxes[1].Order));
        Assert.That(page.Runs.Select(r => r.Text), Is.EqualTo(new[] { "Z" }));
    }

    [TestCase("", 24, 12)] [TestCase("width:32pt", 32, 16)] [TestCase("width:50%;padding:10%", 40, 20)]
    public void NaturalPointAndPercentageGeometryUsesCellContent(string style, double width, double height)
    {
        var image = TableLayoutTests.Layout(Cell($"<img src='logo' style='{style}'>"), Options()).Single().Images.Single();
        Assert.That(image.Width, Is.EqualTo(width)); Assert.That(image.Height, Is.EqualTo(height));
        Assert.That(image.Top, Is.EqualTo(style.Contains("padding", StringComparison.Ordinal) ? 44 : 36));
    }

    [Test]
    public void FormattingWhitespaceCommentsAndInheritedAlignmentKeepImageInCell()
    {
        var options = Options();
        var html = Cell(" \n<!-- before -->" + Img + "\t<!-- after -->\r ", tableStyle: "width:80pt;text-align:right") + "<p>AFTER</p>";
        var root = HtmlDocumentReader.ReadTree(html, options, default);
        Assert.That(root.Children, Has.Count.EqualTo(2));
        Assert.That(BlockTreeTraversal.Paragraphs(root, default).SelectMany(p => p.Runs).Select(r => r.Text), Is.EqualTo(new[] { "AFTER" }));
        var page = TextLayout.Layout(root, options, default).Single();
        Assert.That(page.Images.Single().Image, Is.SameAs(options.Images["logo"]));
        Assert.That(page.Images.Single().X, Is.EqualTo(92));
        Assert.That(page.Runs.Single().Baseline, Is.LessThan(841.89 - 48));
    }

    [TestCase("<div></div>")][TestCase("<ul><li>A</li></ul>")][TestCase(TableStructureTests.One)]
    public void ForbiddenBlockContentBothOrdersFailsWithoutChangingOutput(string other)
    {
        TableStructureTests.Fails(Cell(other + Img), "FPDF1101", Options());
        TableStructureTests.Fails(Cell(Img + other), "FPDF1101", Options());
    }

    [TestCase("span")][TestCase("p")]
    public void WrappedImageAndTextBothOrdersFailWithoutChangingOutput(string wrapper)
    {
        TableStructureTests.Fails(Cell($"<{wrapper}>A" + Img + $"</{wrapper}>"), "FPDF1101", Options());
        TableStructureTests.Fails(Cell($"<{wrapper}>" + Img + $"A</{wrapper}>"), "FPDF1101", Options());
    }

    [Test]
    public void WrappedImageIsRejectedAndResourceDiagnosticsStayLocated()
    {
        TableStructureTests.Fails(Cell("<span>" + Img + "</span>"), "FPDF1101", Options());
        foreach (var (content, code) in new[] { ("<img src='missing'>", "FPDF1604"), ("<img src='/tmp/a.png'>", "FPDF1103"), ("<img>", "FPDF1103") })
        {
            var html = Cell(content); using var output = new MemoryStream();
            var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, Options()))!;
            Assert.That(error.Code, Is.EqualTo(code)); Assert.That(error.SourceOffset, Is.EqualTo(html.IndexOf("<img", StringComparison.Ordinal)));
            Assert.That(output.Length, Is.Zero);
            TableStructureTests.Fails(html, code, Options());
        }
    }

    [Test]
    public void ImageOnlyHeaderRequiresNoBoldFontAndHasNoTextLine()
    {
        var options = Options() with { Fonts = [PdfFont.LoadTrueType(FontFixture.Create())] };
        var html = "<table style='width:160pt'><colgroup><col style='width:80pt'><col style='width:80pt'></colgroup><thead><tr><th>" + Img + "</th><th style='font-weight:normal'>A</th></tr></thead><tbody><tr><td>A</td><td>A</td></tr></tbody></table>";
        var page = TableLayoutTests.Layout(html, options).Single();
        Assert.That(page.Runs.Select(r => r.Text), Is.EqualTo(new[] { "A", "A", "A" }));
        Assert.That(page.Images.Single().Top, Is.EqualTo(36));
        Assert.That(TableLayoutTests.Pdf(html, options), Is.Not.Empty);
    }

    [Test]
    public void WholeTableMovementTranslatesPositiveImageTopExactlyOnce()
    {
        var options = Options() with { PageHeight = 100, Margin = 10 };
        var html = "<p style='font-size:10pt;line-height:2;margin-top:0;margin-bottom:0'>A<br>B</p><div style='padding:3pt;border:1pt solid red'>"
            + Cell("<img src='logo' style='width:60pt;padding:2pt;border:1pt solid blue'>", "padding:4pt;border:1pt solid red") + "</div>";
        var pages = TableLayoutTests.Layout(html, options);
        Assert.That(pages, Has.Count.EqualTo(2)); Assert.That(pages[0].Images, Is.Empty);
        var image = pages[1].Images.Single(); Assert.That(image.Top, Is.EqualTo(22)); Assert.That(image.X, Is.EqualTo(22));
        Assert.That(pages[1].PaintBoxes.OrderBy(b => b.Order).Select(b => b.Top), Is.EqualTo(new[] { 10d, 14, 19 }));
    }

    private static string RepeatedHeader()
        => "<style>td,th{padding:2pt;border:1pt solid red;background-color:blue}</style><table style='width:100pt;font-size:10pt;line-height:2;padding:2pt;border:1pt solid green;background-color:white'><colgroup><col style='width:100%'></colgroup><thead><tr><th><img src='logo' style='padding:1pt;border:1pt solid red;background-color:white'></th></tr></thead><tbody>"
        + string.Concat(Enumerable.Range(1, 4).Select(i => $"<tr><td>R{i}</td></tr>")) + "</tbody></table>";

    [Test]
    public void RepeatedHeaderTranslatesDecorationsSharesResourceAndChargesEveryDraw()
    {
        var options = Options() with { PageWidth = 240, PageHeight = 100, Margin = 10 };
        var html = RepeatedHeader(); var pages = TableLayoutTests.Layout(html, options);
        Assert.That(pages, Has.Count.EqualTo(2));
        Assert.That(pages.SelectMany(p => p.Images).Select(i => i.Top), Is.EqualTo(new[] { 18d, 15 }));
        Assert.That(pages.SelectMany(p => p.Images).All(i => ReferenceEquals(i.Image, options.Images["logo"])), Is.True);
        Assert.That(pages.SelectMany(p => p.Images).Select(i => i.X), Is.EqualTo(new[] { 18d, 18 }));
        Assert.That(pages.SelectMany(p => p.Runs).Select(r => r.Text), Is.EqualTo(new[] { "R1", "R2", "R3", "R4" }));
        Assert.That(pages.SelectMany(p => p.PaintBoxes).All(b => b.Top >= 10 && b.Top + b.Height <= 90), Is.True);
        Assert.That(pages.Sum(p => p.PaintBoxes.Sum(b => b.CommandCount) + p.Runs.Count + p.Images.Count), Is.EqualTo(54));
        var pdf = TableLayoutTests.Pdf(html, options with { MaxDisplayCommands = 54 });
        Assert.That(PngFixture.ImageObjects(pdf), Has.Count.EqualTo(2));
        Assert.That(Regex.Matches(Encoding.Latin1.GetString(pdf), @"/Im1 Do"), Has.Count.EqualTo(2));
        TableStructureTests.Fails(html, "FPDF1401", options with { MaxDisplayCommands = 53 });
        using var output = new MemoryStream(); output.Write("sentinel"u8); output.Position = 3;
        Assert.That(Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, options with { MaxPages = 1 }))!.Code, Is.EqualTo("FPDF1303"));
        Assert.That(output.ToArray(), Is.EqualTo("sentinel"u8.ToArray())); Assert.That(output.Position, Is.EqualTo(3));
        Assert.That(TableLayoutTests.Pdf(html, options), Is.EqualTo(pdf));
    }

    [TestCase("width:81pt")] [TestCase("width:80pt;padding:1pt")] [TestCase("width:0pt")]
    public void InvalidImageGeometryFailsAtomically(string style)
        => TableStructureTests.Fails(Cell($"<img src='logo' style='{style}'>"), "FPDF1302", Options());

    [Test]
    public void ImpossibleInitialImageEdgeFailsButFormerOversizeHeaderCombinationContinues()
    {
        var options = Options() with { PageHeight = 100, Margin = 10 };
        TableStructureTests.Fails(Cell("<img src='logo' style='width:80pt'>", "padding-top:41pt"), "FPDF1302", options);
        var continued = TableLayoutTests.Layout(RepeatedHeader().Replace("R1", "A<br>B<br>C"), options);
        Assert.That(continued.Count, Is.GreaterThan(1));
        Assert.That(continued.SelectMany(p => p.Runs).Count(r => r.Text == "A"), Is.EqualTo(1));
        Assert.That(continued.SelectMany(p => p.Images).Count(), Is.EqualTo(continued.Count));
        Assert.That(TableLayoutTests.Pdf(Cell(Img), options), Is.Not.Empty);
    }
}
