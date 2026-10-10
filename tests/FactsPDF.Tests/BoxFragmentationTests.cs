using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>Real M8 block-fragmentation contracts; M7 single-page geometry remains intact.</summary>
[TestFixture]
public sealed class BoxFragmentationTests
{
    private static readonly PdfOptions Small = new()
    {
        PageWidth = 300,
        PageHeight = 140,
        Margin = 20
    };

    private static List<LayoutPage> Layout(string html, PdfOptions? options = null)
    {
        var o = options ?? Small;
        return TextLayout.Layout(HtmlDocumentReader.ReadTree(html, o, default), o, default);
    }

    private static (PdfConversionResult Result, byte[] Pdf) Render(string html, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        var result = PdfConverter.Convert(html, output, options ?? Small);
        return (result, output.ToArray());
    }

    private static FactsPdfException FailsWithoutWriting(string html, string code, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        output.Write("keep"u8);
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(
            html, output, options ?? Small));
        Assert.That(error!.Code, Is.EqualTo(code));
        Assert.That(Encoding.ASCII.GetString(output.ToArray()), Is.EqualTo("keep"));
        return error;
    }

    private const string ThreePages = "<section style='width:200pt;padding:12pt;" +
        "background-color:#e7f2fa;border:4pt solid #1d4568'>" +
        "<p>Fragment one</p><p style='break-before:page'>Fragment two</p>" +
        "<p style='break-before:page'>Fragment three</p></section>";

    [Test]
    public void ThreePageSlicedBoxHasOnlyFirstTopAndFinalBottomBorders()
    {
        var pages = Layout(ThreePages);
        Assert.That(pages, Has.Count.EqualTo(3));
        Assert.That(pages.Select(page => page.PaintBoxes.Count), Is.EqualTo(new[] { 1, 1, 1 }));
        var boxes = pages.Select(page => page.PaintBoxes.Single()).ToArray();
        Assert.That(boxes.Select(x => x.IsFirstFragment), Is.EqualTo(new[] { true, false, false }));
        Assert.That(boxes.Select(x => x.IsLastFragment), Is.EqualTo(new[] { false, false, true }));
        Assert.That(boxes.Select(x => x.X), Is.All.EqualTo(Small.Margin));
        Assert.That(boxes.Select(x => x.Width), Is.All.EqualTo(232).Within(0.00001));
        Assert.That(boxes[0].Top, Is.EqualTo(Small.Margin).Within(0.00001));
        Assert.That(boxes[0].Top + boxes[0].Height, Is.EqualTo(Small.PageHeight - Small.Margin).Within(0.00001));
        Assert.That(boxes[1].Top, Is.EqualTo(Small.Margin).Within(0.00001));
        Assert.That(boxes[1].Height, Is.EqualTo(Small.PageHeight - 2 * Small.Margin).Within(0.00001));
        Assert.That(boxes[2].Top, Is.EqualTo(Small.Margin).Within(0.00001));
        Assert.That(boxes[2].Height, Is.GreaterThan(0).And.LessThan(100));
        Assert.That(boxes.Select(x => x.CommandCount), Is.EqualTo(new[] { 4, 3, 4 }));
        Assert.That(pages.Select(page => page.Runs.Single().Text), Is.EqualTo(
            new[] { "Fragment one", "Fragment two", "Fragment three" }));
    }

    [Test]
    public void PdfPainterAppliesTopAndBottomOnlyOnTheirOwnFragments()
    {
        var style = new BoxStyle
        {
            BackgroundColor = new Rgb(0.5, 0.5, 0.5),
            BorderTop = new BorderEdge(3, true, new Rgb(1, 0, 0)),
            BorderBottom = new BorderEdge(5, true, new Rgb(0, 1, 0)),
            BorderLeft = new BorderEdge(2, true, new Rgb(0, 0, 1)),
            BorderRight = new BorderEdge(2, true, new Rgb(0, 0, 1))
        };
        string Paint(bool first, bool last)
        {
            var fragment = new PaintedBox(20, 20, 100, 80, style, new Rgb(0, 0, 0), 0,
                IsFirstFragment: first, IsLastFragment: last);
            var output = new StringBuilder();
            PdfPaintSerializer.Append(output, [fragment], Small.PageHeight, 10000, default);
            return output.ToString();
        }
        var first = Paint(true, false);
        var middle = Paint(false, false);
        var last = Paint(false, true);
        Assert.That(first, Does.Contain("1 0 0 rg").And.Not.Contain("0 1 0 rg"));
        Assert.That(middle, Does.Not.Contain("1 0 0 rg").And.Not.Contain("0 1 0 rg"));
        Assert.That(last, Does.Contain("0 1 0 rg").And.Not.Contain("1 0 0 rg"));
        Assert.That(first, Does.Contain("0 0 1 rg"));
        Assert.That(middle, Does.Contain("0 0 1 rg"));
        Assert.That(last, Does.Contain("0 0 1 rg"));
    }

    [Test]
    public void FullPdfActuallySerializesThreePagesAndRepeatedBackgrounds()
    {
        var (result, bytes) = Render(ThreePages);
        var pdf = Encoding.ASCII.GetString(bytes);
        Assert.That(result.PageCount, Is.EqualTo(3));
        Assert.That(pdf.Split("/Type /Page /Parent", StringSplitOptions.None).Length - 1, Is.EqualTo(3));
        Assert.That(pdf.Split("0.906 0.949 0.98 rg", StringSplitOptions.None).Length - 1,
            Is.EqualTo(3), "Background fill should be emitted for every page fragment.");
        Assert.That(pdf, Does.Contain("Tm <467261676D656E74206F6E65> Tj"));
        Assert.That(pdf, Does.Contain("Tm <467261676D656E742074776F> Tj"));
        Assert.That(pdf, Does.Contain("Tm <467261676D656E74207468726565> Tj"));
    }

    [Test]
    public void LongSingleParagraphCanFragmentAcrossPagesWithoutRepeatedTopPadding()
    {
        var words = string.Join(" ", Enumerable.Repeat("long", 100));
        var pages = Layout("<div style='width:180pt;padding-top:12pt;padding-bottom:8pt;" +
            "border:2pt solid blue;background-color:red'><p>" + words + "</p></div>");
        Assert.That(pages.Count, Is.GreaterThanOrEqualTo(3));
        Assert.That(pages[0].PaintBoxes.Single().IsFirstFragment, Is.True);
        Assert.That(pages[^1].PaintBoxes.Single().IsLastFragment, Is.True);
        Assert.That(pages.Skip(1).All(page => page.PaintBoxes.Single().IsFirstFragment == false), Is.True);
        Assert.That(pages.Take(pages.Count - 1).All(page => page.PaintBoxes.Single().IsLastFragment == false), Is.True);
        Assert.That(pages.All(page => page.Runs.Count > 0), Is.True);
    }

    [Test]
    public void NestedPaintedContainersProduceCorrespondingPageFragments()
    {
        const string html = "<section style='background-color:red;border:2pt solid red;padding:6pt'>" +
            "<article style='background-color:blue;border:2pt solid blue;padding:5pt'>" +
            "<p>Start</p><p style='break-before:page'>Middle</p>" +
            "<p style='break-before:page'>End</p></article></section>";
        var pages = Layout(html);
        Assert.That(pages, Has.Count.EqualTo(3));
        foreach (var page in pages)
        {
            Assert.That(page.PaintBoxes, Has.Count.EqualTo(2));
            var sorted = page.PaintBoxes.OrderBy(x => x.Order).ToArray();
            Assert.That(sorted[0].Width, Is.GreaterThan(sorted[1].Width));
            Assert.That(sorted[0].X, Is.LessThan(sorted[1].X));
            Assert.That(sorted[0].Top, Is.LessThanOrEqualTo(sorted[1].Top));
        }
        foreach (var p in pages.Skip(1).Take(1))
            Assert.That(p.PaintBoxes.All(x => !x.IsFirstFragment && !x.IsLastFragment), Is.True);
    }

    [Test]
    public void DecoratedAncestorCanSpanPagesAfterUnpaintedPaddedChild()
    {
        var o = Small with { PageHeight = 100 };
        var html = "<div style='background-color:red'>" +
            "<div style='padding:25pt 0'></div><p>B</p></div>";
        var pages = Layout(html, o);
        Assert.That(pages, Has.Count.EqualTo(2));
        Assert.That(pages[0].PaintBoxes.Single().IsFirstFragment, Is.True);
        Assert.That(pages[1].PaintBoxes.Single().IsLastFragment, Is.True);
        Assert.That(pages[1].Runs.Single().Text, Is.EqualTo("B"));
    }

    [Test]
    public void PageBreakAtEndDoesNotCreatePhantomTrailingFragment()
    {
        var pages = Layout("<div style='background-color:blue;border:1pt solid blue'>" +
            "<p style='break-after:page'>Only text</p></div>");
        Assert.That(pages, Has.Count.EqualTo(1));
        Assert.That(pages[0].PaintBoxes.Single().IsFirstFragment, Is.True);
        Assert.That(pages[0].PaintBoxes.Single().IsLastFragment, Is.True);
    }

    [Test]
    public void UndecoratedMultiPageLayoutDoesNotCreatePaintFragments()
    {
        var pages = Layout("<p>A</p><p style='break-before:page'>B</p>");
        Assert.That(pages, Has.Count.EqualTo(2));
        Assert.That(pages.All(x => x.PaintBoxes.Count == 0), Is.True);
    }

    [Test]
    public void MaxPagesStillRejectsBeforeCallerStreamWrites()
        => FailsWithoutWriting(ThreePages, "FPDF1303", Small with { MaxPages = 2 });

    [Test]
    public void DisplayBudgetCountsEveryRepeatedPageDecoration()
        => FailsWithoutWriting(ThreePages, "FPDF1401", Small with { MaxDisplayCommands = 12 });

    [Test]
    public void TooTallTopPaddingAndFirstLineFailsRatherThanLooping()
        => FailsWithoutWriting("<div style='background-color:red;padding-top:120pt'>X</div>",
            "FPDF1302");

    [Test]
    public void CancellationInFragmentedLayoutPreservesOutput()
    {
        using var output = new MemoryStream();
        output.Write("keep"u8);
        Assert.Throws<OperationCanceledException>(() => PdfConverter.Convert(
            ThreePages, output, Small, new CancellationToken(true)));
        Assert.That(Encoding.ASCII.GetString(output.ToArray()), Is.EqualTo("keep"));
    }
}
