using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// Vertical margins on block boxes the way a browser prints them: CSS 2 margin collapsing between
/// siblings, a parent and its first or last child and through empty blocks, padding and borders
/// keeping margins apart, margins truncated at unforced page breaks and kept after forced ones,
/// and cells, flex items and grid items keeping their children's margins inside. Every expected
/// distance was measured against a browser's printed PDF of the same markup.
/// </summary>
[TestFixture]
public sealed class BlockMarginCollapseTests
{
    // Ten point text on 20pt lines in a 300x400pt page with 20pt margins; every user-agent margin is
    // zeroed so each scenario names the margins it tests.
    private const string Css = "<style>html,body{margin:0;padding:0}body{font-size:10pt;line-height:20pt}" +
        "p,ul,ol,li,table,div,h2{margin:0;padding:0}table{border-collapse:separate;border-spacing:0;width:100pt}" +
        "td{padding:0}</style>";

    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 400, Margin = 20 };

    private static List<LayoutPage> Layout(string html)
        => TextLayout.Layout(HtmlDocumentReader.ReadTree(html, Page, default), Page, default);

    private static PlacedText Find(List<LayoutPage> pages, string text, out int page)
    {
        for (var i = 0; i < pages.Count; i++)
        {
            var run = pages[i].Runs.FirstOrDefault(r => r.Text == text);
            if (run is not null) { page = i; return run; }
        }
        throw new AssertionException($"No run '{text}'.");
    }

    // The distance from the AAA baseline to the BBB baseline on one page.
    private static double Distance(string body, string css = "")
    {
        var pages = Layout(Css + css + body);
        var a = Find(pages, "AAA", out var pageA);
        var b = Find(pages, "BBB", out var pageB);
        Assert.That(pageB, Is.EqualTo(pageA));
        return a.Baseline - b.Baseline;
    }

    private const string A = "<p>AAA</p>";
    private const string B = "<p>BBB</p>";

    [Test]
    public void ParagraphAndTableMarginsCollapseToTheLargerOne()
        => Assert.That(Distance("<p style='margin-bottom:10pt'>AAA</p><table style='margin-top:20pt'><tr><td>T</td></tr></table>" + B),
            Is.EqualTo(20 + 20 + 20).Within(0.001));

    [Test]
    public void TwoTablesWithTwentyPointMarginsSitTwentyPointsApartNotForty()
        => Assert.That(Distance(A + "<table style='margin-bottom:20pt'><tr><td>T1</td></tr></table>" +
                "<table style='margin-top:20pt'><tr><td>T2</td></tr></table>" + B),
            Is.EqualTo(20 + 20 + 20 + 20).Within(0.001));

    [Test]
    public void TableBottomMarginSeparatesItFromTheNextParagraph()
        => Assert.That(Distance(A + "<table style='margin-bottom:20pt'><tr><td>T</td></tr></table>" + B),
            Is.EqualTo(20 + 20 + 20).Within(0.001));

    [Test]
    public void ListTopMarginCollapsesWithTheParagraphAboveIt()
        => Assert.That(Distance("<p style='margin-bottom:5pt'>AAA</p><ul style='margin-top:12pt'><li>L</li></ul>" + B),
            Is.EqualTo(20 + 12 + 20).Within(0.001));

    [Test]
    public void ListItemMarginsCollapseAndTheLastOneLeavesTheList()
        => Assert.That(Distance(A + "<ol><li style='margin-bottom:4pt'>L1</li><li style='margin-bottom:4pt'>L2</li></ol>" + B),
            Is.EqualTo(20 + 20 + 4 + 20 + 4).Within(0.001));

    [Test]
    public void ParentAndFirstChildTopMarginsCollapse()
        => Assert.That(Distance(A + "<div style='margin-top:20pt'><p style='margin-top:10pt'>BBB</p></div>"),
            Is.EqualTo(20 + 20).Within(0.001));

    // The over-correction case: padding or a border between parent and child keeps both margins.
    [TestCase("padding-top:5pt", 20 + 5 + 10)]
    [TestCase("border-top:1pt solid #000", 20 + 1 + 10)]
    public void PaddingOrBorderKeepsParentAndChildTopMarginsApart(string edge, double extra)
        => Assert.That(Distance(A + $"<div style='margin-top:20pt;{edge}'><p style='margin-top:10pt'>BBB</p></div>"),
            Is.EqualTo(20 + extra).Within(0.001));

    [Test]
    public void ParentAndLastChildBottomMarginsCollapse()
        => Assert.That(Distance("<div style='margin-bottom:5pt'><p style='margin-bottom:15pt'>AAA</p></div>" + B),
            Is.EqualTo(20 + 15).Within(0.001));

    [Test]
    public void BottomPaddingKeepsTheLastChildMarginInsideThePaintedParent()
    {
        Assert.That(Distance("<div style='margin-bottom:5pt;padding-bottom:3pt'><p style='margin-bottom:15pt'>AAA</p></div>" + B),
            Is.EqualTo(20 + 15 + 3 + 5).Within(0.001));
        var box = Layout(Css + "<div style='background:#ccc;padding-bottom:3pt'><p style='margin-bottom:15pt'>AAA</p></div>")[0]
            .PaintBoxes.Single();
        Assert.That(box.Height, Is.EqualTo(20 + 15 + 3).Within(0.001));
    }

    [Test]
    public void CollapsedMarginIsOutsideThePaintedParent()
    {
        // The child's margin passes through the parent's top and bottom edges: the background
        // covers only the line.
        var box = Layout(Css + "<div style='background:#ccc'><p style='margin:30pt 0'>AAA</p></div>")[0].PaintBoxes.Single();
        Assert.That(box.Top, Is.EqualTo(Page.Margin + 30).Within(0.001));
        Assert.That(box.Height, Is.EqualTo(20).Within(0.001));
    }

    [Test]
    public void EmptyBlockMarginsCollapseThroughIt()
        => Assert.That(Distance(A + "<div style='margin-top:10pt;margin-bottom:20pt'></div>" + B),
            Is.EqualTo(20 + 20).Within(0.001));

    [Test]
    public void PaddingStopsAnEmptyBlockFromCollapsingThrough()
        => Assert.That(Distance(A + "<div style='margin-top:10pt;margin-bottom:20pt;padding-top:1pt'></div>" + B),
            Is.EqualTo(20 + 10 + 1 + 20).Within(0.001));

    [Test]
    public void FirstListItemMarginCollapsesWithTheParagraphInsideIt()
        => Assert.That(Distance(A + "<ul><li style='margin-top:6pt'><p style='margin-top:4pt'>BBB</p></li></ul>"),
            Is.EqualTo(20 + 6).Within(0.001));

    // A table cell is its own formatting context: margins inside never collapse with its edges and
    // count inside the row height. Cell paragraphs take padding.
    [Test]
    public void CellChildMarginsStayInsideTheCell()
        => Assert.That(Distance(A + "<table><tr><td><p style='margin-top:6pt'>BBB</p></td></tr></table>"),
            Is.EqualTo(20 + 6).Within(0.001));

    [Test]
    public void CellParagraphPaddingSitsInsideTheCell()
    {
        Assert.That(Distance(A + "<table><tr><td><p style='padding-top:7pt'>BBB</p></td></tr></table>"),
            Is.EqualTo(20 + 7).Within(0.001));
        // Padding adds to the margin instead of collapsing with it.
        Assert.That(Distance("<table><tr><td><p style='margin-bottom:5pt'>AAA</p><p style='padding-top:7pt;margin-top:3pt'>BBB</p></td></tr></table>"),
            Is.EqualTo(20 + 5 + 7).Within(0.001));
        var row = Layout(Css + "<table><tr><td style='background:#ccc'><p style='padding:2pt 0 4pt'>AAA</p></td></tr></table>")[0]
            .PaintBoxes.Single();
        Assert.That(row.Height, Is.EqualTo(2 + 20 + 4).Within(0.001));
    }

    [Test]
    public void FlexItemChildMarginsStayInsideTheItem()
    {
        // The item holds the 15pt bottom margin, so the flex container is 15pt taller.
        Assert.That(Distance(A + "<div style='display:flex'><div><p style='margin-bottom:15pt'>X</p></div></div>" + B),
            Is.EqualTo(20 + 20 + 15).Within(0.001));
        Assert.That(Distance(A + "<div style='display:flex'><div><p style='margin-top:10pt'>BBB</p></div></div>"),
            Is.EqualTo(20 + 10).Within(0.001));
    }

    // The user-agent sheet gives ul, ol and menu 1em above and below, and nested lists none.
    [TestCase("ul")]
    [TestCase("ol")]
    [TestCase("menu")]
    public void ListsHaveOneEmAboveAndBelowByDefault(string tag)
        => Assert.That(Distance(A + $"<{tag}><li>L</li></{tag}>" + B, "<style>" + tag + "{margin:revert}</style>"),
            Is.EqualTo(20 + 10 + 20 + 10).Within(0.001));

    [Test]
    public void ListMarginDefaultUsesTheListsOwnFontSize()
        => Assert.That(Distance(A + "<ul style='font-size:20pt;line-height:20pt'><li>L</li></ul>" + B, "<style>ul{margin:revert}</style>"),
            Is.EqualTo(20 + 20 + 20 + 20).Within(0.001));

    [Test]
    public void NestedListHasNoDefaultMargin()
        => Assert.That(Distance("<ul><li>AAA<ul><li>BBB</li></ul></li></ul>", "<style>ul{margin:revert}</style>"),
            Is.EqualTo(20).Within(0.001));

    [Test]
    public void ListMarginOverridesTheUserAgentDefault()
        => Assert.That(Distance(A + "<ul style='margin:0 0 7pt'><li>L</li></ul>" + B, "<style>ul{margin:revert}</style>"),
            Is.EqualTo(20 + 20 + 7).Within(0.001));

    [Test]
    public void ListMarginFollowsTheEmAndRemUnitsOfItsOwnFontSize()
    {
        Assert.That(Distance(A + "<ul style='font-size:10pt;margin-top:2em'><li>L</li></ul>" + B), Is.EqualTo(20 + 20 + 20).Within(0.001));
        Assert.That(Distance(A + "<ul style='margin-bottom:3rem'><li>L</li></ul>" + B), Is.EqualTo(20 + 20 + 3 * Page.FontSize).Within(0.001));
    }

    [Test]
    public void ListMarginsAcceptTheCssWideKeywords()
    {
        Assert.That(Distance(A + "<div style='margin-top:9pt'><ul style='margin-top:inherit'><li>L</li></ul></div>" + B),
            Is.EqualTo(20 + 9 + 20).Within(0.001));
        Assert.That(Distance(A + "<ul style='margin-top:initial;margin-bottom:unset'><li>L</li></ul>" + B, "<style>ul{margin:revert}</style>"),
            Is.EqualTo(20 + 20).Within(0.001));
    }

    [Test]
    public void ListAndItemHorizontalMarginsMoveTheirText()
    {
        var plain = Find(Layout(Css + "<ul><li>AAA</li></ul>"), "AAA", out _).X;
        var moved = Find(Layout(Css + "<ul style='margin-left:20pt'><li style='margin-left:10pt'>AAA</li></ul>"), "AAA", out _).X;
        Assert.That(moved - plain, Is.EqualTo(30).Within(0.001));
    }

    [Test]
    public void TableAcceptsVerticalMarginsAndZeroHorizontalOnes()
        => Assert.That(Distance(A + "<table style='margin:20pt 0'><tr><td>T</td></tr></table>" + B),
            Is.EqualTo(20 + 20 + 20 + 20).Within(0.001));

    [TestCase("<table style='margin-left:5pt'><tr><td>T</td></tr></table>")]
    [TestCase("<table style='margin:0 auto'><tr><td>T</td></tr></table>")]
    [TestCase("<ul style='margin-top:5%'><li>L</li></ul>")]
    [TestCase("<table style='margin-top:5%'><tr><td>T</td></tr></table>")]
    [TestCase("<table><tr><td><p style='padding:5%'>T</p></td></tr></table>")]
    public void UnsupportedListAndTableMarginsFailExplicitly(string body)
    {
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(Css + body, new MemoryStream(), Page));
        Assert.That(error!.Code, Is.EqualTo("FPDF1201"));
    }

    // Page breaks: margins adjoining an unforced break are truncated, those after a forced break stay.
    [Test]
    public void UnforcedPageBreakTruncatesTheMarginOfTheBlockItMoves()
    {
        var pages = Layout(Css + "<p style='line-height:330pt'>AAA</p><table style='margin-top:30pt'><tr><td>BBB</td></tr></table>");
        Find(pages, "AAA", out var pageA);
        var b = Find(pages, "BBB", out var pageB);
        Assert.That((pageA, pageB), Is.EqualTo((0, 1)));
        var topOfPage = Find(Layout(Css + "<table><tr><td>BBB</td></tr></table>"), "BBB", out _);
        Assert.That(b.Baseline, Is.EqualTo(topOfPage.Baseline).Within(0.001));
    }

    [Test]
    public void ForcedPageBreakKeepsTheMarginOfTheNextBlock()
    {
        var pages = Layout(Css + A + "<p style='break-before:page;margin-top:30pt'>BBB</p>");
        var b = Find(pages, "BBB", out var pageB);
        Assert.That(pageB, Is.EqualTo(1));
        var top = Find(Layout(Css + B), "BBB", out _);
        Assert.That(top.Baseline - b.Baseline, Is.EqualTo(30).Within(0.001));
    }

    [Test]
    public void TrailingMarginOfTheLastBlockOnAPageIsDropped()
    {
        // Seventeen lines and the table fill the 360pt page exactly. The 40pt margin after the table
        // would not fit, and the next block starts at the top of page two, not 40pt below it.
        var lines = string.Join("<br>", Enumerable.Repeat("x", 17));
        var pages = Layout(Css + "<p>" + lines + "</p><table style='margin-bottom:40pt'><tr><td>T</td></tr></table>" + B);
        var b = Find(pages, "BBB", out var pageB);
        Assert.That(pageB, Is.EqualTo(1));
        Assert.That(b.Baseline, Is.EqualTo(Find(Layout(Css + B), "BBB", out _).Baseline).Within(0.001));
    }
}
