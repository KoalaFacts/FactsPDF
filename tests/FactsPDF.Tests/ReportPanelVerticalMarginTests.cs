using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// Real outside-the-border vertical spacing for a bounded report-panel subset.
/// Exact PDF oracles use already-supported empty padding blocks, not a parser mock.
/// </summary>
[TestFixture]
public sealed class ReportPanelVerticalMarginTests
{
    private static readonly PdfOptions Page = new()
    {
        PageWidth = 300, PageHeight = 500, Margin = 20
    };

    private static byte[] Render(string html, PdfOptions? options = null)
    {
        using var destination = new MemoryStream();
        PdfConverter.Convert(html, destination, options ?? Page);
        return destination.ToArray();
    }

    private static List<LayoutPage> Layout(string html, PdfOptions? options = null)
    {
        var settings = options ?? Page;
        return TextLayout.Layout(HtmlDocumentReader.ReadTree(html, settings, default), settings, default);
    }

    private static void Reject(string html, string code)
    {
        using var destination = new MemoryStream();
        destination.Write("existing-document"u8);
        destination.Position = 5;
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, destination, Page));
        Assert.That(error!.Code, Is.EqualTo(code));
        Assert.That(destination.ToArray(), Is.EqualTo("existing-document"u8.ToArray()));
        Assert.That(destination.Position, Is.EqualTo(5));
    }

    [TestCase("div")]
    [TestCase("section")]
    [TestCase("article")]
    public void PanelShorthandMovesPaintOutsideBordersAndMatchesIndependentSpacingOracle(string tag)
    {
        var first = "<" + tag + " style='width:150pt;margin:12pt auto 7pt;padding:3pt;" +
            "border:1pt solid blue;background:#e5f1f6'><p style='margin:0'>SERVICE-001</p></" + tag + ">";
        var second = "<article style='background:red'><p style='margin:0'>NEXT-ENTRY</p></article>";
        var actual = first + second;
        var oracle = "<div style='padding-top:12pt'></div>" +
            first.Replace("margin:12pt auto 7pt", "margin:0 auto") +
            "<div style='padding-top:7pt'></div>" + second;
        Assert.That(Render(actual), Is.EqualTo(Render(oracle)));
        var painted = Layout(actual).Single().PaintBoxes.OrderBy(b => b.Top).ToArray();
        Assert.That(painted, Has.Length.EqualTo(2));
        Assert.That(painted[0].Top, Is.EqualTo(32).Within(0.000001));
        Assert.That(painted[0].X, Is.EqualTo(71).Within(0.000001)); // (260 - 158)/2 + 20
        Assert.That(painted[1].Top - (painted[0].Top + painted[0].Height),
            Is.EqualTo(7).Within(0.000001));
    }

    [Test]
    public void NestedVerticalMarginsOccupyParentContentWithoutPaintingTheMargins()
    {
        const string html = "<article style='padding:4pt;background:#e5f1f6'>" +
            "<section style='margin:6pt 0 5pt;background:blue'>" +
            "<p style='margin:0'>ACTUAL-PDF</p></section></article>";
        var boxes = Layout(html).Single().PaintBoxes.Where(b => b.Style.BackgroundColor.HasValue).ToArray();
        Assert.That(boxes, Has.Length.EqualTo(2));
        var outer = boxes.Single(b => Math.Abs(b.X - 20) < 0.000001);
        var inner = boxes.Single(b => Math.Abs(b.X - 24) < 0.000001);
        Assert.That(outer.Top, Is.EqualTo(20).Within(0.000001));
        Assert.That(inner.Top, Is.EqualTo(30).Within(0.000001));
        Assert.That(outer.Top + outer.Height - (inner.Top + inner.Height),
            Is.EqualTo(9).Within(0.000001)); // 5pt inner margin + 4pt outer padding
    }

    [Test]
    public void RootRemAndFinalElementEmProduceTheSameActualPdfAsExplicitPointMargins()
    {
        const string start = "<html style='font-size:10pt'><body>" +
            "<section style='font-size:16pt;width:130pt;padding:4pt;background:blue;";
        const string end = "'><p style='margin:0'>FONT-SCALED</p></section>" +
            "<p style='margin:0'>AFTER</p></body></html>";
        Assert.That(Render(start + "margin-top:2em;margin-bottom:1rem" + end),
            Is.EqualTo(Render(start + "margin-top:32pt;margin-bottom:10pt" + end)));
    }

    [Test]
    public void InheritanceWideResetAndCascadeKeepSidesIndependent()
    {
        const string before = "<div style='margin-top:11pt'>" +
            "<section style='margin-top:inherit;margin-bottom:unset'><p style='margin:0'>A</p></section></div>";
        const string oracle = "<div style='margin-top:11pt'>" +
            "<section style='margin-top:11pt;margin-bottom:0'><p style='margin:0'>A</p></section></div>";
        Assert.That(Render(before), Is.EqualTo(Render(oracle)));

        const string stylesheet = "<style>.panel{margin-top:6pt!important;margin-bottom:4pt}</style>";
        const string actual = stylesheet + "<section class='panel' style='margin-top:12pt;margin-bottom:8pt'>" +
            "<p style='margin:0'>B</p></section>";
        const string cascadeOracle = "<section style='margin-top:6pt;margin-bottom:8pt'>" +
            "<p style='margin:0'>B</p></section>";
        Assert.That(Render(actual), Is.EqualTo(Render(cascadeOracle)));

        Assert.That(Render("<section style='margin:initial'><p style='margin:0'>C</p></section>"),
            Is.EqualTo(Render("<section><p style='margin:0'>C</p></section>")));
    }

    [Test]
    public void PanelStartMarginAppliesOnFirstPageOnlyWhilePaintedFragmentsContinue()
    {
        var page = Page with { PageHeight = 150 };
        var rows = string.Join("<br>", Enumerable.Range(1, 18).Select(i => $"Item-{i:00}"));
        var body = "<section style='width:110pt;margin:9pt auto 5pt;padding:5pt;" +
            "border:2pt solid red;background:#e5f1f6'><p style='margin:0'>" +
            rows + "</p></section>";
        var oracle = "<div style='padding-top:9pt'></div>" +
            body.Replace("margin:9pt auto 5pt", "margin:0 auto") +
            "<div style='padding-top:5pt'></div>";
        Assert.That(Render(body, page), Is.EqualTo(Render(oracle, page)));
        var pages = Layout(body, page);
        Assert.That(pages, Has.Count.GreaterThan(1));
        Assert.That(pages.SelectMany(p => p.Runs).Count(), Is.EqualTo(18));
        var sections = pages.SelectMany(p => p.PaintBoxes)
            .Where(b => b.Style.BackgroundColor.HasValue).ToArray();
        Assert.That(sections, Has.Length.EqualTo(pages.Count));
        Assert.That(sections[0].Top, Is.EqualTo(29).Within(0.000001));
        Assert.That(sections.Skip(1).All(b => Math.Abs(b.Top - 20) < 0.000001), Is.True);
        Assert.That(sections.All(b => Math.Abs(b.X - 88) < 0.000001), Is.True);
    }

    [TestCase("<div style='margin-top:auto'>A</div>", "FPDF1202")]
    [TestCase("<section style='margin-bottom:10%'>A</section>", "FPDF1202")]
    [TestCase("<article style='margin-top:-1pt'>A</article>", "FPDF1202")]
    [TestCase("<article style='margin-top:calc(1pt + 2pt)'>A</article>", "FPDF1202")]
    [TestCase("<div style='margin:15000pt 0'>A</div>", "FPDF1202")]
    [TestCase("<body style='margin-top:4pt'>A</body>", "FPDF1201")]
    [TestCase("<span style='margin-top:4pt'>A</span>", "FPDF1201")]
    [TestCase("<ol style='margin-top:4pt'><li>A</li></ol>", "FPDF1201")]
    [TestCase("<table style='margin-top:4pt'><colgroup><col style='width:100pt'></colgroup><tbody><tr><td>A</td></tr></tbody></table>", "FPDF1201")]
    public void UnsupportedValuesAndElementsFailWithoutDamagingDestination(string html, string code)
        => Reject(html, code);

    [Test]
    public void LosingNonzeroPanelMarginIsValidatedButZeroWinnerKeepsOldPdfBytes()
    {
        const string original = "<div style='padding:2pt;background:blue'><p style='margin:0'>BASE</p></div>";
        const string stylesheet = "<style>.panel{margin-top:9pt;margin-top:0;margin-bottom:0}</style>";
        const string updated = stylesheet +
            "<div class='panel' style='padding:2pt;background:blue'><p style='margin:0'>BASE</p></div>";
        Assert.That(Render(updated), Is.EqualTo(Render(original)));
        Reject("<style>section{margin-top:-2pt;margin-top:0}</style><section>A</section>", "FPDF1202");
    }

    [TestCase("p")]
    [TestCase("h2")]
    public void ChildParagraphAndHeadingInheritComputedPanelVerticalMargins(string tag)
    {
        const string prefix = "<div style='margin-top:11pt;margin-bottom:4pt'>";
        var actual = prefix + "<" + tag + " style='margin-top:inherit;margin-bottom:inherit'>A</" + tag + "></div>";
        var oracle = prefix + "<" + tag + " style='margin-top:11pt;margin-bottom:4pt'>A</" + tag + "></div>";
        Assert.That(Render(actual), Is.EqualTo(Render(oracle)));
    }

    [Test]
    public void UnconsumedPanelTopMarginMovesWithFirstPaintedFragment()
    {
        var page = Page with { PageHeight = 150 };
        var leading = "<p style='margin:0'>" + string.Join("<br>",
            Enumerable.Range(1, 7).Select(index => $"Row-{index:00}")) + "</p>";
        var document = leading + "<section style='margin-top:25pt;background:blue'>" +
            "<p style='margin:0'>FIRST-PANEL-TEXT</p></section>";
        var pages = Layout(document, page);
        Assert.That(pages, Has.Count.EqualTo(2));
        var box = pages[1].PaintBoxes.Single(painted => painted.Style.BackgroundColor.HasValue);
        Assert.That(box.Top, Is.EqualTo(45).Within(0.000001));
        Assert.That(pages[0].PaintBoxes, Is.Empty);
    }

    [Test]
    public void OversizedFirstPagePanelMarginRejectsInsteadOfDroppingOrBlankPaging()
    {
        var options = Page with { PageHeight = 150, MaxPages = 1 };
        using var target = new MemoryStream();
        target.Write("existing-document"u8);
        target.Position = 5;
        var exception = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(
            "<section style='margin-top:120pt;background:blue'><p style='margin:0'>A</p></section>",
            target, options));
        Assert.That(exception!.Code, Is.EqualTo("FPDF1302"));
        Assert.That(target.ToArray(), Is.EqualTo("existing-document"u8.ToArray()));
        Assert.That(target.Position, Is.EqualTo(5));
    }

    [Test]
    public void ExactPageBoundaryTopMarginDoesNotAllocateExtraPage()
    {
        var options = Page with { PageHeight = 150, MaxPages = 1 };
        var pages = Layout("<section style='margin-top:95.6pt;background:blue'>" +
            "<p style='margin:0'>A</p></section>", options);
        Assert.That(pages, Has.Count.EqualTo(1));
        Assert.That(pages[0].PaintBoxes.Single().Top, Is.EqualTo(115.6).Within(0.000001));
    }

    [TestCase("p")]
    [TestCase("h2")]
    public void MovingAnUnconsumedPanelPreservesItsChildFirstParagraphMargin(string tag)
    {
        var options = Page with { PageHeight = 150 };
        var leading = "<p style='margin:0'>" + string.Join("<br>",
            Enumerable.Range(1, 7).Select(index => $"Row-{index:00}")) + "</p>";
        var panel = "<section style='margin-top:25pt;background:blue'>" +
            $"<{tag} style='font-size:12pt;margin-top:12pt;margin-bottom:0'>FIRST-PANEL-TEXT</{tag}>" +
            "</section>";
        var fresh = Layout(panel, options);
        var moved = Layout(leading + panel, options);
        Assert.That(fresh, Has.Count.EqualTo(1));
        Assert.That(moved, Has.Count.EqualTo(2));
        Assert.That(moved[1].PaintBoxes.Single().Top,
            Is.EqualTo(fresh[0].PaintBoxes.Single().Top).Within(0.000001));
        // No margin collapse: the panel's 25pt and the child's 12pt are
        // both first-fragment spacing even when earlier content forces a new page.
        Assert.That(moved[1].Runs.Single().Baseline,
            Is.EqualTo(fresh[0].Runs.Single().Baseline).Within(0.000001),
            "An unconsumed parent must not drop its child's own margin-top on relocation");
    }
}
