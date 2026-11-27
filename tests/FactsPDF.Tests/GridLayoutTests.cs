using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// Grid containers with explicit length, percentage, fr and auto columns and auto-placed items in
/// auto-sized implicit rows. The built-in Courier font advances 7.2pt per glyph at 12pt with a
/// 14.4pt line; the page content box is 260pt wide from x=20.
/// </summary>
[TestFixture]
public sealed class GridLayoutTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 500, Margin = 20 };
    private const double FirstBaseline = 500 - 20 - 10.8;

    private static List<LayoutPage> Layout(string html)
        => TextLayout.Layout(HtmlDocumentReader.ReadTree(html, Page, default), Page, default);

    private static PlacedText Run(List<LayoutPage> pages, string text, int page = 0)
        => pages[page].Runs.Single(run => run.Text == text);

    private static void Fails(string html, string code)
    {
        using var output = new MemoryStream();
        output.Write("keep"u8);
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, Page));
        Assert.That(error!.Code, Is.EqualTo(code), error.Message);
        Assert.That(Encoding.ASCII.GetString(output.ToArray()), Is.EqualTo("keep"));
    }

    [Test]
    public void AnAutoColumnTakesItsMaxContentAndTheFrColumnTheRest()
    {
        // The auto column is as wide as BB (14.4pt); 1fr gets 260 - 12 - 14.4.
        var pages = Layout("<div style='display:grid;grid-template-columns:1fr auto;gap:12pt;align-items:start'>" +
            "<div>A</div><div>BB</div><div>C<br>C</div><div>D</div></div><p>E</p>");
        Assert.That(Run(pages, "A").X, Is.EqualTo(20).Within(1e-9));
        Assert.That(Run(pages, "BB").X, Is.EqualTo(20 + 233.6 + 12).Within(1e-9));
        Assert.That(Run(pages, "D").X, Is.EqualTo(265.6).Within(1e-9));
        // The second row starts after the first row and the 12pt row gap.
        Assert.That(Run(pages, "D").Baseline, Is.EqualTo(FirstBaseline - 26.4).Within(1e-9));
        Assert.That(Run(pages, "E").Baseline, Is.EqualTo(FirstBaseline - 26.4 - 28.8).Within(1e-9));
    }

    [Test]
    public void ItemsStretchToTheirAreaAndRowByDefault()
    {
        var box = Layout("<div style='display:grid;grid-template-columns:1fr 1fr'><div style='background:#eeeeee'>A</div>" +
            "<div>B<br>B</div></div>")[0].PaintBoxes.Single();
        Assert.That(box.Width, Is.EqualTo(130).Within(1e-9));
        Assert.That(box.Height, Is.EqualTo(28.8).Within(1e-9));
    }

    [Test]
    public void FixedAndFrColumnsShareTheLeftoverSpaceByFactor()
    {
        var pages = Layout("<div style='display:grid;grid-template-columns:100pt 1fr 2fr;column-gap:6pt'>" +
            "<div>A</div><div>B</div><div>C</div></div>");
        Assert.That(Run(pages, "B").X, Is.EqualTo(126).Within(1e-9));
        Assert.That(Run(pages, "C").X, Is.EqualTo(126 + 148 / 3.0 + 6).Within(1e-9));
    }

    [TestCase("normal", 20 + 7.2 + (260 - 2 * 7.2) / 2)]
    [TestCase("start", 27.2)]
    public void JustifyContentNormalStretchesAutoColumns(string value, double second)
        => Assert.That(Run(Layout($"<div style='display:grid;grid-template-columns:auto auto;justify-content:{value}'>" +
            "<div>A</div><div>B</div></div>"), "B").X, Is.EqualTo(second).Within(1e-9));

    [TestCase("center", 70, 150)]
    [TestCase("end", 120, 200)]
    [TestCase("space-between", 20, 200)]
    public void JustifyContentPlacesFixedColumns(string value, double first, double second)
    {
        var pages = Layout($"<div style='display:grid;grid-template-columns:80pt 80pt;justify-content:{value}'>" +
            "<div>A</div><div>B</div></div>");
        Assert.That(Run(pages, "A").X, Is.EqualTo(first).Within(1e-9));
        Assert.That(Run(pages, "B").X, Is.EqualTo(second).Within(1e-9));
    }

    [Test]
    public void JustifyItemsAndJustifySelfPlaceItemsInTheirArea()
    {
        var pages = Layout("<div style='display:grid;grid-template-columns:100pt;justify-items:center'>" +
            "<div>AB</div><div style='justify-self:end'>CD</div></div>");
        Assert.That(Run(pages, "AB").X, Is.EqualTo(20 + (100 - 14.4) / 2).Within(1e-9));
        Assert.That(Run(pages, "CD").X, Is.EqualTo(20 + 100 - 14.4).Within(1e-9));
    }

    [TestCase("center", 7.2)]
    [TestCase("end", 14.4)]
    [TestCase("start", 0)]
    public void AlignItemsPlacesItemsInTheirRow(string value, double drop)
        => Assert.That(Run(Layout($"<div style='display:grid;grid-template-columns:1fr 1fr;align-items:{value}'>" +
            "<div>A<br>A</div><div>B</div></div>"), "B").Baseline, Is.EqualTo(FirstBaseline - drop).Within(1e-9));

    [Test]
    public void OrderChangesAutoPlacement()
    {
        var pages = Layout("<div style='display:grid;grid-template-columns:auto auto;justify-content:start'>" +
            "<div style='order:1'>A</div><div>B</div></div>");
        Assert.That(Run(pages, "B").X, Is.EqualTo(20).Within(1e-9));
        Assert.That(Run(pages, "A").X, Is.EqualTo(27.2).Within(1e-9));
    }

    [Test]
    public void InlineChildrenAndTextRunsAreGridItems()
        => Assert.That(Run(Layout("<div style='display:grid;grid-template-columns:1fr auto'>Label<span>VAL</span></div>"), "VAL").X,
            Is.EqualTo(280 - 21.6).Within(1e-9));

    [Test]
    public void AGridListItemIsNotNumbered()
    {
        var texts = Layout("<ol><li style='display:grid'>A</li><li>B</li></ol>")[0].Runs.Select(run => run.Text).ToList();
        Assert.That(texts, Does.Contain("1."));
        Assert.That(texts, Does.Not.Contain("2."));
    }

    [Test]
    public void APageBreaksBetweenRowsAndDropsTheRowGap()
    {
        var pages = Layout("<div style='height:440pt'></div><div style='display:grid;row-gap:10pt'><div>A</div><div>B</div></div>");
        Assert.That(pages, Has.Count.EqualTo(2));
        Assert.That(Run(pages, "A").Baseline, Is.EqualTo(FirstBaseline - 440).Within(1e-9));
        Assert.That(Run(pages, "B", page: 1).Baseline, Is.EqualTo(FirstBaseline).Within(1e-9));
    }

    [Test]
    public void AGridInsideAFlexItemIsSizedFromItsColumns()
        => Assert.That(Run(Layout("<div style='display:flex'><div style='display:grid;grid-template-columns:auto auto;column-gap:4pt'>" +
            "<div>A</div><div>B</div></div><div>C</div></div>"), "C").X, Is.EqualTo(20 + 18.4).Within(1e-9));

    [Test]
    public void InitialPlacementAndImplicitTrackValuesAreAccepted()
    {
        var pages = Layout("<div style='display:grid;grid-template-columns:none;grid-template-rows:none;grid-auto-flow:row;" +
            "grid-auto-rows:auto;grid-auto-columns:auto'><div style='grid-column:auto;grid-row:auto;grid-area:auto'>A</div><div>B</div></div>");
        Assert.That(Run(pages, "B").Baseline, Is.EqualTo(FirstBaseline - 14.4).Within(1e-9));
    }

    [TestCase("<div style='display:grid;grid-template-columns:repeat(2,1fr)'><div>A</div></div>", "FPDF1202")]
    [TestCase("<div style='display:grid;grid-template-columns:minmax(10pt,1fr) auto'><div>A</div></div>", "FPDF1202")]
    [TestCase("<div style='display:grid;grid-template-columns:[start] 1fr'><div>A</div></div>", "FPDF1202")]
    [TestCase("<div style='display:grid;grid-template-columns:min-content 1fr'><div>A</div></div>", "FPDF1202")]
    [TestCase("<div style='display:grid;grid-template-columns:-1fr'><div>A</div></div>", "FPDF1202")]
    [TestCase("<div style='display:grid;grid-template-columns:1fr 1fr'><div style='grid-column:1 / 3'>A</div></div>", "FPDF1202")]
    [TestCase("<div style='display:grid;grid-template-rows:20pt'><div>A</div></div>", "FPDF1202")]
    [TestCase("<div style='display:grid;grid-template-areas:\"a b\"'><div>A</div></div>", "FPDF1202")]
    [TestCase("<div style='display:grid;grid-auto-flow:column'><div>A</div></div>", "FPDF1202")]
    [TestCase("<div style='display:grid;justify-items:baseline'><div>A</div></div>", "FPDF1202")]
    [TestCase("<div style='display:inline-grid'><div>A</div></div>", "FPDF1202")]
    [TestCase("<html style='display:grid'><body><p>A</p></body></html>", "FPDF1201")]
    [TestCase("<p style='display:grid'>A</p>", "FPDF1201")]
    [TestCase("<div style='display:grid;min-height:50pt'><div>A</div></div>", "FPDF1201")]
    [TestCase("<div style='display:grid;break-inside:avoid'><div>A</div></div>", "FPDF1201")]
    [TestCase("<div style='display:grid'><table><colgroup><col style='width:100pt'></colgroup><tbody><tr><td>x</td></tr></tbody></table></div>", "FPDF1201")]
    [TestCase("<div style='display:grid;grid-template-columns:50pt 50pt 50pt 50pt 50pt 50pt'><div>A</div></div>", "FPDF1302")]
    [TestCase("<div style='display:grid'><div style='height:470pt'>A</div></div>", "FPDF1302")]
    public void UnsupportedGridInputFailsWithoutOutput(string html, string code) => Fails(html, code);
}
