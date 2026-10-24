using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

public sealed class PageFurnitureTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 240, PageHeight = 120, Margin = 20 };
    private const string Css = "<style>p{font-size:10pt;line-height:2;margin-top:0;margin-bottom:0}</style>";
    private static string Explicit(int count) => Css + string.Concat(Enumerable.Range(1, count)
        .Select(i => $"<p style='break-before:{(i == 1 ? "auto" : "page")}'>A</p>"));
    private static byte[] Pdf(string html, PdfOptions options)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, options);
        return output.ToArray();
    }
    private static List<LayoutPage> Layout(string html, PdfOptions options)
    {
        var pages = TableLayoutTests.Layout(html, options);
        var commands = pages.Sum(p => (long)p.Runs.Count + p.Discs.Count + p.Images.Count +
            p.PaintBoxes.Sum(box => box.CommandCount));
        PageFurnitureLayout.Append(pages, options, commands, default);
        return pages;
    }
    private static void Fails(string html, PdfOptions options, string code)
    {
        using var stream = new MemoryStream();
        stream.Write("sentinel"u8);
        stream.Position = 3;
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, stream, options));
        Assert.That(error!.Code, Is.EqualTo(code));
        Assert.That(stream.ToArray(), Is.EqualTo("sentinel"u8.ToArray()));
        Assert.That(stream.Position, Is.EqualTo(3));
    }
    private static PdfPageFurniture Furniture(PdfPageText? header = null, PdfPageText? footer = null)
        => new() { Default = new() { HeaderLeft = header, FooterRight = footer } };
    private static PdfPageText Text(string value, double size = 10) => new() { Template = value, FontSize = size };

    [Test]
    public void DisabledStructuredFurniturePreservesBytes()
    {
        var html = Explicit(2);
        var baseline = Pdf(html, Page);
        Assert.That(Pdf(html, Page with { PageFurniture = new() }), Is.EqualTo(baseline));
        Assert.That(Pdf(html, Page with { PageFurniture = new() { FirstPage = new() } }), Is.EqualTo(baseline));
        Assert.That(Pdf(html, Page with { HeaderText = "Literal {page}" }), Is.EqualTo(
            Pdf(html, Page with { HeaderText = "Literal {page}", PageFurniture = null })));
    }

    [Test]
    public void LegacyFieldsStayLiteralAndMixedModeFails()
    {
        foreach (var options in new[] {
            Page with { PageFurniture = new(), PageNumbers = true },
            Page with { PageFurniture = new(), HeaderText = "" },
            Page with { PageFurniture = new(), FooterText = "" } })
            Assert.Throws<ArgumentException>(() => Pdf("", options));
        var raw = Encoding.ASCII.GetString(Pdf("", Page with { HeaderText = "{page}" }));
        Assert.That(raw, Does.Contain("<7B706167657D> Tj"));
    }

    [Test]
    public void TotalsUseActualPagesWithoutRepagination()
    {
        var opts = Page with { PageFurniture = Furniture(footer: Text("Page {page} of {pages}")) };
        var pages = Layout(Explicit(10), opts);
        Assert.That(pages, Has.Count.EqualTo(10));
        for (var i = 0; i < pages.Count; i++)
            Assert.That(pages[i].Runs.Last().Text, Is.EqualTo($"Page {i + 1} of 10"));
        Assert.That(pages[0].Runs.Last().X, Is.EqualTo(240 - 20 - 12 * 6).Within(0.000001));
        Assert.That(Pdf(Explicit(10), opts), Is.Not.Empty);
    }

    [Test]
    public void FirstPageReplacementDoesNotRestartNumbers()
    {
        var opts = Page with { PageFurniture = new()
        {
            Default = new() {
                HeaderLeft = Text("Regular"),
                FooterRight = Text("{page}/{pages}") },
            FirstPage = new() { HeaderCenter = Text("Cover") }
        } };
        var pages = Layout(Explicit(3), opts);
        Assert.That(pages[0].Runs.Last().Text, Is.EqualTo("Cover"));
        Assert.That(pages[0].Runs.All(r => !r.Text.Contains("1/3", StringComparison.Ordinal)), Is.True);
        Assert.That(pages[1].Runs.TakeLast(2).Select(r => r.Text),
            Is.EqualTo(new[] { "Regular", "2/3" }));
        Assert.That(pages[2].Runs.Last().Text, Is.EqualTo("3/3"));
    }

    [Test]
    public void SixRegionsHaveSharedBaselineAndExplicitStyle()
    {
        var opts = Page with { PageWidth = 400, PageFurniture = new() { Default = new()
        {
            HeaderLeft = new() { Template = "L", FontSize = 8, Color = new PdfRgbColor(12, 34, 56) },
            HeaderCenter = Text("C", 10), HeaderRight = Text("R", 12),
            FooterLeft = Text("l", 8), FooterCenter = Text("c", 10), FooterRight = Text("r", 12)
        } } };
        var pages = Layout("", opts);
        var text = pages.Single().Runs;
        Assert.That(text.Select(r => r.Text), Is.EqualTo(new[] { "L", "C", "R", "l", "c", "r" }));
        Assert.That(text.Take(3).Select(r => r.Baseline).Distinct().Count(), Is.EqualTo(1));
        Assert.That(text.Skip(3).Select(r => r.Baseline).Distinct().Count(), Is.EqualTo(1));
        Assert.That(text[0].Style.FontSize, Is.EqualTo(8));
        Assert.That(text[0].Style.Color.R, Is.EqualTo(12d / 255));
        Assert.That(Pdf("", opts), Is.Not.Empty);
    }

    [Test]
    public void AllSuppliedDefinitionsAreBoundedButUnusedGlyphsAreNotResolved()
    {
        Fails("", Page with { PageFurniture = Furniture(Text(new string('A', 1025))) }, "FPDF1001");
        Fails("A", Page with { PageFurniture = Furniture(Text("A")), MaxInputCharacters = 1 }, "FPDF1001");
        Fails("", Page with { PageFurniture = new() { Default = new(),
            FirstPage = new() { FooterCenter = Text("{not-page}") } } }, "FPDF1306");
        Assert.DoesNotThrow(() => Pdf("", Page with { PageFurniture = new()
        {
            Default = new() { HeaderLeft = Text("中") }, FirstPage = new()
        } }));
        Assert.Throws<ArgumentException>(() => Pdf("", Page with { PageFurniture = new()
        {
            Default = new() { HeaderLeft = new PdfPageText { Template = null! } }
        } }));
        Assert.Throws<ArgumentOutOfRangeException>(() => Pdf("", Page with { PageFurniture = new()
        {
            Default = new() { HeaderLeft = Text("A", double.NaN) }
        } }));
    }

    [TestCase("{{page}}", "{page}")]
    [TestCase("{{{page}}}", "{1}")]
    [TestCase("{{pages}}", "{pages}")]
    [TestCase("  A    B  ", "A B")]
    public void ExactEscapingAndSpaceNormalization(string template, string expected)
    {
        var options = Page with { PageFurniture = Furniture(footer: Text(template)) };
        Assert.That(Layout("", options).Single().Runs.Last().Text, Is.EqualTo(expected));
    }

    [TestCase("{PAGE}", "FPDF1306")]
    [TestCase("{nope}", "FPDF1306")]
    [TestCase("{page", "FPDF1306")]
    [TestCase("x}", "FPDF1306")]
    [TestCase("A\nB", "FPDF1305")]
    [TestCase("A\u2028B", "FPDF1305")]
    public void InvalidTemplatesFailWithoutDestinationWrite(string template, string code)
        => Fails("", Page with { PageFurniture = Furniture(footer: Text(template)) }, code);

    [Test]
    public void MissingStyledRoleAndNonAsciiRemainExplicit()
    {
        Fails("", Page with { PageFurniture = Furniture(Text("A") with { Bold = true }) }, "FPDF1507");
        Fails("", Page with { PageFurniture = Furniture(Text("中")) }, "FPDF1301");
    }

    [Test]
    public void HorizontalCollisionAndTooShallowMarginAreRejected()
    {
        var both = Page with { PageFurniture = new() { Default = new()
        {
            HeaderLeft = Text(new string('A', 18)),
            HeaderCenter = Text(new string('B', 18))
        } } };
        Fails("", both, "FPDF1302");
        Fails("", Page with { Margin = 10, PageFurniture = Furniture(Text("A", 12)) }, "FPDF1302");
    }

    [Test]
    public void BudgetAndCancellationPreserveCallerStream()
    {
        Fails("", Page with { MaxDisplayCommands = 1, PageFurniture = new() { Default = new()
        {
            HeaderLeft = Text("A"), FooterLeft = Text("B")
        } } }, "FPDF1401");
        using var stream = new MemoryStream();
        stream.Write("sentinel"u8);
        Assert.Throws<OperationCanceledException>(() => PdfConverter.Convert("", stream,
            Page with { PageFurniture = Furniture(Text("A")) }, new CancellationToken(true)));
        Assert.That(stream.ToArray(), Is.EqualTo("sentinel"u8.ToArray()));
    }
}
