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
    public void LastFragmentUsesFinalPageCursorNotItsFirstPageStartingY()
    {
        // A decorated box begins late on page 1 but has very little content
        // on page 2. Reusing the first-page startY as a minimum on page 2
        // erroneously extends its last background well below the last line.
        const string html = "<p>Lead1</p><p>Lead2</p><p>Lead3</p>" +
            "<section style='background-color:red;padding-top:5pt;padding-bottom:10pt'>" +
            "<p style='margin-bottom:0pt'>Inside</p>" +
            "<p style='break-before:page;margin-bottom:0pt'>End</p></section>";
        var pages = Layout(html);
        Assert.That(pages, Has.Count.EqualTo(2));
        var first = pages[0].PaintBoxes.Single();
        var last = pages[1].PaintBoxes.Single();
        Assert.That(first.Top, Is.GreaterThan(80));
        Assert.That(last.Top, Is.EqualTo(Small.Margin).Within(0.00001));
        Assert.That(last.Top + last.Height, Is.LessThan(70),
            "The last paint edge must be based only on the final page's text and bottom padding.");
        Assert.That(last.IsFirstFragment, Is.False);
        Assert.That(last.IsLastFragment, Is.True);
    }

    [Test]
    public void NestedFinalFragmentsDoNotInheritFirstPageVerticalMinima()
    {
        const string html = "<p>Lead1</p><p>Lead2</p><p>Lead3</p>" +
            "<section style='background-color:red;padding:3pt'>" +
            "<article style='background-color:blue;padding:3pt'>" +
            "<p style='margin-bottom:0pt'>Inside</p>" +
            "<p style='break-before:page;margin-bottom:0pt'>End</p>" +
            "</article></section>";
        var pages = Layout(html);
        Assert.That(pages, Has.Count.EqualTo(2));
        var final = pages[1].PaintBoxes.OrderBy(x => x.Order).ToArray();
        Assert.That(final, Has.Length.EqualTo(2));
        Assert.That(final[0].Top + final[0].Height, Is.LessThan(70));
        Assert.That(final[1].Top + final[1].Height, Is.LessThan(70));
    }

    [Test]
    public void AlreadyConsumedAncestorTopPaddingDoesNotRepeatOnNextPage()
    {
        var page = Small with { PageHeight = 100 };
        const string html = "<div style='background-color:red;padding-top:20pt'>" +
            "<div style='padding:20pt 0'></div><p>B</p></div>";
        var pages = Layout(html, page);
        var standalone = Layout("<p>B</p>", page);
        Assert.That(pages, Has.Count.EqualTo(2));
        Assert.That(pages[0].PaintBoxes.Single().IsFirstFragment, Is.True);
        Assert.That(pages[1].PaintBoxes.Single().IsLastFragment, Is.True);
        Assert.That(pages[1].Runs.Single().Baseline,
            Is.EqualTo(standalone[0].Runs.Single().Baseline).Within(0.00001),
            "The parent already consumed its first-page top padding before a padded child.");
    }

    [Test]
    public void ThinFirstFragmentClipsOversizedHorizontalBorderToPageBounds()
    {
        var style = new BoxStyle
        {
            BorderTop = new BorderEdge(10, true, new Rgb(1, 0, 0)),
            BorderBottom = new BorderEdge(10, true, new Rgb(0, 1, 0)),
            BorderLeft = new BorderEdge(2, true, new Rgb(0, 0, 1))
        };
        var first = new PaintedBox(20, 75, 100, 5, style, new Rgb(0, 0, 0), 0,
            IsFirstFragment: true, IsLastFragment: false);
        var last = new PaintedBox(20, 20, 100, 4, style, new Rgb(0, 0, 0), 0,
            IsFirstFragment: false, IsLastFragment: true);
        string Painted(PaintedBox box)
        {
            var content = new StringBuilder();
            PdfPaintSerializer.Append(content, [box], 100, 10000, default);
            return content.ToString();
        }
        var a = Painted(first);
        var b = Painted(last);
        Assert.That(a, Does.Contain("20 20 100 5 re f Q"),
            "First page border must stop at the fragment's bottom edge.");
        Assert.That(b, Does.Contain("20 76 100 4 re f Q"),
            "Last page border must begin at the fragment's top edge.");
        Assert.That(a, Does.Not.Contain("100 10 re f Q"));
        Assert.That(b, Does.Not.Contain("100 10 re f Q"));
        Assert.That(first.CommandCount, Is.EqualTo(1));
        Assert.That(last.CommandCount, Is.EqualTo(1));
    }

    [Test]
    public void TwoPageUnicodePdfKeepsEmbeddedFontAndAllPagePaint()
    {
        var options = Small with
        {
            Fonts = [PdfFont.LoadTrueType(FontFixture.Create())]
        };
        const string html = "<section style='background-color:#cee8fb;border:2pt solid blue'>" +
            "<p>A中文B</p><p style='break-before:page'>中文B</p></section>";
        var (result, bytes) = Render(html, options);
        var pdf = Encoding.Latin1.GetString(bytes);
        Assert.That(result.PageCount, Is.EqualTo(2));
        Assert.That(pdf, Does.Contain("/ToUnicode"));
        Assert.That(pdf, Does.Contain("/Subtype /Type0"));
        Assert.That(pdf.Split(" re f Q", StringSplitOptions.None).Length - 1,
            Is.GreaterThanOrEqualTo(6));
    }

    [Test]
    public void RepeatedDecorationConsumesExactDisplayCommandBudget()
    {
        // First: background + top + 2 sides = 4;
        // middle: background + 2 sides = 3;
        // last: background + bottom + 2 sides = 4;
        // plus three single text runs = 14 commands total.
        var (result, _) = Render(ThreePages, Small with { MaxDisplayCommands = 14 });
        Assert.That(result.PageCount, Is.EqualTo(3));
        FailsWithoutWriting(ThreePages, "FPDF1401",
            Small with { MaxDisplayCommands = 13 });
    }

    [Test]
    public void TransparentBorderlessSpanningBoxProducesNoGraphics()
    {
        var baseHtml = "<section><p>One</p><p style='break-before:page'>Two</p></section>";
        var invisible = "<section style='background-color:transparent;border:0 solid red'>" +
            "<p>One</p><p style='break-before:page'>Two</p></section>";
        var (a, pdfA) = Render(baseHtml);
        var (b, pdfB) = Render(invisible);
        Assert.That(a.PageCount, Is.EqualTo(2));
        Assert.That(b.PageCount, Is.EqualTo(2));
        Assert.That(pdfB, Is.EqualTo(pdfA),
            "Explicitly invisible CSS must not materialize decoration fragments.");
    }

    [Test]
    public void PageFragmentsRetainNestedSourceOrderOnEveryPage()
    {
        var pages = Layout("<section style='background-color:red;padding:5pt'>" +
            "<div style='background-color:blue'><p>A</p>" +
            "<p style='break-before:page'>B</p></div></section>");
        Assert.That(pages, Has.Count.EqualTo(2));
        foreach (var page in pages)
        {
            var paint = page.PaintBoxes.OrderBy(box => box.Order).ToArray();
            Assert.That(paint, Has.Length.EqualTo(2));
            Assert.That(paint[0].Style.BackgroundColor, Is.EqualTo(new Rgb(1, 0, 0)));
            Assert.That(paint[1].Style.BackgroundColor, Is.EqualTo(new Rgb(0, 0, 1)));
        }
    }

    [Test]
    public void FragmentedBoxOutputLimitFailsBeforeCallerWrite()
        => FailsWithoutWriting(ThreePages, "FPDF1401",
            Small with { MaxOutputBytes = 300 });

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
