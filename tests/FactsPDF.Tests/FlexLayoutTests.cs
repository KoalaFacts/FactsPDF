using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// Single-line, row-direction flex containers. The built-in Courier font advances 7.2pt per glyph
/// at 12pt with a 14.4pt line, so every expected coordinate is exact: the page content box is
/// 260pt wide from x=20, and a first line's baseline sits 10.8pt below its top.
/// </summary>
[TestFixture]
public sealed class FlexLayoutTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 500, Margin = 20 };
    private const double FirstBaseline = 500 - 20 - 10.8;

    private static List<LayoutPage> Layout(string html, PdfOptions? options = null)
    {
        var settings = options ?? Page;
        return TextLayout.Layout(HtmlDocumentReader.ReadTree(html, settings, default), settings, default);
    }

    private static PlacedText Run(List<LayoutPage> pages, string text, int page = 0)
        => pages[page].Runs.Single(run => run.Text == text);

    private static byte[] Pdf(string html, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, options ?? Page);
        return output.ToArray();
    }

    private static FactsPdfException Fails(string html, string code, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        output.Write("keep"u8);
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, options ?? Page));
        Assert.That(error!.Code, Is.EqualTo(code), error.Message);
        Assert.That(Encoding.ASCII.GetString(output.ToArray()), Is.EqualTo("keep"));
        return error;
    }

    [Test]
    public void SpaceBetweenPutsTheFirstAndLastItemsAtTheContainerEdges()
    {
        var pages = Layout("<div style='display:flex;justify-content:space-between'><div>AA</div><div>BBB</div></div>");
        Assert.That(Run(pages, "AA").X, Is.EqualTo(20).Within(1e-9));
        Assert.That(Run(pages, "BBB").X, Is.EqualTo(280 - 21.6).Within(1e-9));
        Assert.That(Run(pages, "AA").Baseline, Is.EqualTo(FirstBaseline).Within(1e-9));
        Assert.That(Run(pages, "BBB").Baseline, Is.EqualTo(FirstBaseline).Within(1e-9));
    }

    // Items AA (14.4pt) and B (7.2pt) leave 238.4pt of free space.
    [TestCase("flex-start", 20, 34.4)]
    [TestCase("normal", 20, 34.4)]
    [TestCase("flex-end", 258.4, 272.8)]
    [TestCase("end", 258.4, 272.8)]
    [TestCase("center", 139.2, 153.6)]
    [TestCase("space-between", 20, 272.8)]
    [TestCase("space-around", 79.6, 213.2)]
    [TestCase("space-evenly", 99.466666666666, 193.333333333333)]
    public void JustifyContentDistributesTheFreeSpace(string value, double first, double second)
    {
        var pages = Layout($"<div style='display:flex;justify-content:{value}'><div>AA</div><div>B</div></div>");
        Assert.That(Run(pages, "AA").X, Is.EqualTo(first).Within(1e-6));
        Assert.That(Run(pages, "B").X, Is.EqualTo(second).Within(1e-6));
    }

    [Test]
    public void FlexGrowTakesTheFreeSpaceAfterTheColumnGap()
    {
        var page = Layout("<div style='display:flex;column-gap:10pt'><div style='flex-grow:1;background:#eeeeee'>A</div>" +
            "<div>BB</div></div>")[0];
        var box = page.PaintBoxes.Single();
        Assert.That(box.X, Is.EqualTo(20).Within(1e-9));
        Assert.That(box.Width, Is.EqualTo(260 - 10 - 14.4).Within(1e-9));
        Assert.That(page.Runs.Single(run => run.Text == "BB").X, Is.EqualTo(280 - 14.4).Within(1e-9));
    }

    [Test]
    public void FlexShorthandNumbersUseAZeroBasisAndShareTheWidthByFactor()
    {
        var page = Layout("<div style='display:flex'><div style='flex:1;background:#eeeeee'>A</div>" +
            "<div style='flex:3;background:#dddddd'>B</div></div>")[0];
        var widths = page.PaintBoxes.OrderBy(box => box.X).Select(box => box.Width).ToArray();
        Assert.That(widths[0], Is.EqualTo(65).Within(1e-9));
        Assert.That(widths[1], Is.EqualTo(195).Within(1e-9));
    }

    [Test]
    public void GapShorthandSetsTheColumnGap()
    {
        var pages = Layout("<div style='display:flex;gap:4pt 12pt'><div>A</div><div>B</div></div>");
        Assert.That(Run(pages, "B").X, Is.EqualTo(20 + 7.2 + 12).Within(1e-9));
    }

    [Test]
    public void ShrinkingIsProportionalToTheFlexBaseSize()
    {
        // Max-content widths 280.8pt and 136.8pt overflow 260pt by 157.6pt; each shrinks by
        // 157.6 * base / 417.6, so the first item ends at 280.8 - 105.97241379...
        var a = string.Join(' ', Enumerable.Repeat("A", 20));
        var b = string.Join(' ', Enumerable.Repeat("B", 10));
        var page = Layout($"<div style='display:flex'><div>{a}</div><div>{b}</div></div>")[0];
        var second = page.Runs.Where(run => run.Text.StartsWith('B')).Min(run => run.X);
        Assert.That(second, Is.EqualTo(20 + 280.8 - 157.6 * 280.8 / 417.6).Within(1e-6));
    }

    [Test]
    public void AnItemNeverShrinksBelowItsMinContentWidth()
    {
        var b = string.Join(' ', Enumerable.Repeat("B", 20));
        var page = Layout($"<div style='display:flex'><div>{new string('A', 20)}</div><div>{b}</div></div>")[0];
        Assert.That(page.Runs.Where(run => run.Text.StartsWith('B')).Min(run => run.X), Is.EqualTo(20 + 144).Within(1e-6));
    }

    [Test]
    public void InlineChildrenAndTextRunsAreSeparateFlexItems()
    {
        var spans = Layout("<div style='display:flex;justify-content:space-between'><span>AB</span> <span>CD</span></div>");
        Assert.That(Run(spans, "AB").X, Is.EqualTo(20).Within(1e-9));
        Assert.That(Run(spans, "CD").X, Is.EqualTo(280 - 14.4).Within(1e-9));
        var text = Layout("<div style='display:flex;justify-content:space-between'>Label<span>VAL</span></div>");
        Assert.That(Run(text, "Label").X, Is.EqualTo(20).Within(1e-9));
        Assert.That(Run(text, "VAL").X, Is.EqualTo(280 - 21.6).Within(1e-9));
    }

    [Test]
    public void AFlexListItemIsALabelValueRowWithoutAMarker()
    {
        var page = Layout("<ul style='list-style:none'><li style='display:flex;justify-content:space-between'>" +
            "<span>A</span><span>B</span></li></ul>")[0];
        Assert.That(page.Discs, Is.Empty);
        Assert.That(page.Runs.Single(run => run.Text == "B").X, Is.EqualTo(280 - 7.2).Within(1e-9));
    }

    [Test]
    public void AFlexListItemIsNotCountedByItsList()
    {
        var texts = Layout("<ol><li>A</li><li style='display:flex'>B</li><li>C</li></ol>")[0].Runs.Select(run => run.Text).ToList();
        Assert.That(texts, Does.Contain("1."));
        Assert.That(texts, Does.Contain("2."));
        Assert.That(texts, Does.Not.Contain("3."));
    }

    // A two-line item (28.8pt) and a one-line item (14.4pt).
    [TestCase("flex-start", 0)]
    [TestCase("start", 0)]
    [TestCase("center", 7.2)]
    [TestCase("flex-end", 14.4)]
    [TestCase("baseline", 0)]
    [TestCase("stretch", 0)]
    public void AlignItemsPlacesShorterItemsInTheLine(string value, double drop)
    {
        var pages = Layout($"<div style='display:flex;align-items:{value}'><div>A<br>A</div><div>B</div></div>");
        Assert.That(Run(pages, "B").Baseline, Is.EqualTo(FirstBaseline - drop).Within(1e-9));
    }

    [Test]
    public void AlignSelfOverridesAlignItems()
    {
        var pages = Layout("<div style='display:flex;align-items:flex-start'><div>A<br>A</div>" +
            "<div style='align-self:flex-end'>B</div></div>");
        Assert.That(Run(pages, "B").Baseline, Is.EqualTo(FirstBaseline - 14.4).Within(1e-9));
    }

    [Test]
    public void StretchedItemsPaintTheFullLineHeight()
    {
        var box = Layout("<div style='display:flex'><div>A<br>A</div><div style='background:#eeeeee'>B</div></div>")[0]
            .PaintBoxes.Single();
        Assert.That(box.Top, Is.EqualTo(20).Within(1e-9));
        Assert.That(box.Height, Is.EqualTo(28.8).Within(1e-9));
        Assert.That(box.X, Is.EqualTo(27.2).Within(1e-9));
    }

    [Test]
    public void BaselineAlignmentLinesUpFirstBaselinesOfDifferentFontSizes()
    {
        var pages = Layout("<div style='display:flex;align-items:baseline'><div style='font-size:24pt'>A</div><div>B</div>" +
            "</div><p>C</p>");
        // 24pt Courier: 28.8pt line, baseline 21.6pt below the top; the line is 21.6 + 7.2 tall.
        Assert.That(Run(pages, "A").Baseline, Is.EqualTo(500 - 20 - 21.6).Within(1e-9));
        Assert.That(Run(pages, "B").Baseline, Is.EqualTo(500 - 20 - 21.6).Within(1e-9));
        Assert.That(Run(pages, "C").Baseline, Is.EqualTo(500 - 20 - 28.8 - 10.8).Within(1e-9));
    }

    [Test]
    public void OrderMovesAnItemAfterItsSiblings()
    {
        var pages = Layout("<div style='display:flex'><div style='order:2'>A</div><div>B</div></div>");
        Assert.That(Run(pages, "B").X, Is.EqualTo(20).Within(1e-9));
        Assert.That(Run(pages, "A").X, Is.EqualTo(27.2).Within(1e-9));
    }

    [Test]
    public void AutoMarginsTakeTheFreeSpaceBeforeJustifyContent()
    {
        var pages = Layout("<div style='display:flex;justify-content:center'><div style='margin-left:auto'>A</div></div>");
        Assert.That(Run(pages, "A").X, Is.EqualTo(280 - 7.2).Within(1e-9));
    }

    [Test]
    public void PercentageWidthsResolveAgainstTheContainer()
    {
        var pages = Layout("<div style='display:flex'><div style='width:25%'>A</div><div style='flex-grow:1'>B</div></div>");
        Assert.That(Run(pages, "B").X, Is.EqualTo(20 + 65).Within(1e-9));
    }

    [Test]
    public void MaxWidthClampsAnItemAndItsTextWrapsInside()
    {
        var pages = Layout("<div style='display:flex;justify-content:space-between'><div style='min-width:100pt'>A</div>" +
            "<div style='max-width:30pt'>BB CC</div></div>");
        Assert.That(Run(pages, "BB").X, Is.EqualTo(250).Within(1e-9));
        Assert.That(Run(pages, "CC").X, Is.EqualTo(250).Within(1e-9));
        Assert.That(Run(pages, "CC").Baseline, Is.EqualTo(FirstBaseline - 14.4).Within(1e-9));
    }

    [Test]
    public void ContentAfterTheRowStartsBelowItsTallestItem()
    {
        var pages = Layout("<div style='display:flex'><div>A<br>A<br>A</div><div>B</div></div><p>C</p>");
        Assert.That(Run(pages, "C").Baseline, Is.EqualTo(FirstBaseline - 43.2).Within(1e-9));
    }

    [Test]
    public void AFixedHeightContainerStretchesItsItems()
    {
        var page = Layout("<div style='display:flex;height:50pt;align-items:center'><div>A</div>" +
            "<div style='align-self:stretch;background:#eeeeee'>B</div></div>")[0];
        Assert.That(page.PaintBoxes.Single().Height, Is.EqualTo(50).Within(1e-9));
        Assert.That(page.Runs.Single(run => run.Text == "A").Baseline, Is.EqualTo(FirstBaseline - (50 - 14.4) / 2).Within(1e-9));
    }

    [Test]
    public void ImagesAreStretchedToTheLineUnlessAligned()
    {
        var options = Page with
        {
            PageWidth = 400,
            Images = new Dictionary<string, PdfImage> { ["chart"] = PdfImage.FromBytes(JpegResourceTests.Bytes("rgb-420")) }
        };
        const string text = "<div>A<br>A<br>A<br>A<br>A</div>";
        var stretched = Layout($"<div style='display:flex'><img src='chart' style='width:60pt'>{text}</div>", options)[0].Images.Single();
        Assert.That(stretched.X, Is.EqualTo(20).Within(1e-9));
        Assert.That(stretched.Width, Is.EqualTo(60).Within(1e-9));
        Assert.That(stretched.Height, Is.EqualTo(72).Within(1e-9));
        var aligned = Layout($"<div style='display:flex;align-items:flex-start'><img src='chart' style='width:60pt'>{text}</div>",
            options)[0].Images.Single();
        Assert.That(aligned.Height, Is.EqualTo(30).Within(1e-9));
    }

    [Test]
    public void ARowOfShortItemsMovesWholeToTheNextPage()
    {
        var pages = Layout("<div style='height:440pt'></div><div style='display:flex'><div>A<br>A</div><div>B</div></div>");
        Assert.That(pages, Has.Count.EqualTo(2));
        Assert.That(pages[0].Runs, Is.Empty);
        Assert.That(Run(pages, "B", page: 1).Baseline, Is.EqualTo(FirstBaseline).Within(1e-9));
    }

    [Test]
    public void ARowABrowserWouldSplitFailsInsteadOfMoving()
        => Fails("<div style='height:440pt'></div><div style='display:flex'><div>A<br>A<br>A<br>A</div><div>B</div></div>",
            "FPDF1302");

    [Test]
    public void ARowTallerThanThePageFails()
        => Fails("<div style='display:flex'><div style='height:470pt'>A</div></div>", "FPDF1302");

    [Test]
    public void ItemsThatCannotShrinkToFitFail()
        => Fails($"<div style='display:flex'><div>{new string('A', 20)}</div><div>{new string('B', 20)}</div></div>", "FPDF1302");

    [Test]
    public void HtmlFlexIsANoOpWhenTheBodyFillsThePage()
    {
        const string body = "<body><p>This paragraph is longer than one full line of the page.</p></body>";
        Assert.That(Pdf("<html style='display:flex;justify-content:center;width:100%'>" + body + "</html>"),
            Is.EqualTo(Pdf("<html>" + body + "</html>")));
    }

    [Test]
    public void HtmlFlexCentringANarrowBodyFails()
        => Fails("<html style='display:flex;justify-content:center'><body><p>Short</p></body></html>", "FPDF1201");

    [TestCase("<div style='display:flex;flex-wrap:wrap'><div>A</div></div>", "FPDF1202")]
    [TestCase("<div style='display:flex;flex-direction:column'><div>A</div></div>", "FPDF1202")]
    [TestCase("<div style='display:flex;flex-flow:row wrap'><div>A</div></div>", "FPDF1202")]
    [TestCase("<div style='display:inline-flex'><div>A</div></div>", "FPDF1202")]
    [TestCase("<p style='display:flex'>A</p>", "FPDF1201")]
    [TestCase("<div style='display:flex;justify-content:safe center'><div>A</div></div>", "FPDF1202")]
    [TestCase("<div style='display:flex;align-items:first baseline'><div>A</div></div>", "FPDF1202")]
    [TestCase("<div style='display:flex'><div style='flex:1 auto 2'>A</div></div>", "FPDF1202")]
    [TestCase("<div style='display:flex'><div style='flex-grow:-1'>A</div></div>", "FPDF1202")]
    [TestCase("<div style='display:flex'><div style='order:1.5'>A</div></div>", "FPDF1202")]
    [TestCase("<div style='display:flex;break-inside:avoid'><div>A</div></div>", "FPDF1201")]
    [TestCase("<style>.unused{flex-direction:row-reverse}</style><p>A</p>", "FPDF1202")]
    public void UnsupportedFlexInputFailsWithoutOutput(string html, string code) => Fails(html, code);

    [TestCase("flex:none", 0, 0)]
    [TestCase("flex:auto", 1, 1)]
    [TestCase("flex:2", 2, 1)]
    [TestCase("flex:2 3", 2, 3)]
    [TestCase("flex:0 0 40pt", 0, 0)]
    [TestCase("flex:40pt", 1, 1)]
    public void FlexShorthandComputesItsFactors(string declaration, double grow, double shrink)
    {
        var root = HtmlDocumentReader.ReadTree($"<div style='display:flex'><div style='{declaration}'>A</div></div>", Page, default);
        var item = (BlockNode)((BlockNode)root.Children[0]).Children[0];
        Assert.That(item.BoxStyle.Flex.Grow, Is.EqualTo(grow));
        Assert.That(item.BoxStyle.Flex.Shrink, Is.EqualTo(shrink));
    }

    [Test]
    public void FlexPropertiesOnBlocksOutsideFlexContainersChangeNothing()
        => Assert.That(Pdf("<div style='justify-content:center;align-items:center;gap:8pt;flex:1;order:2'><p>A</p></div>"),
            Is.EqualTo(Pdf("<div><p>A</p></div>")));

    [Test]
    public void EveryFlexItemChargesTheDisplayCommandBudget()
        => Fails("<div style='display:flex'><div>A</div><div>B</div></div>", "FPDF1401", Page with { MaxDisplayCommands = 1 });
}
