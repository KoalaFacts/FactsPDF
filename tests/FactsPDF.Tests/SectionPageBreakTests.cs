using System.Text;
using FactsPDF.CommandLine;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>Printed section boundaries, including legacy page-break aliases,
/// must move real PDF glyphs and painted fragments, not merely parse CSS.</summary>
[TestFixture]
public sealed class SectionPageBreakTests
{
    private static readonly PdfOptions Page = new()
    {
        PageWidth = 300, PageHeight = 220, Margin = 20
    };

    private static byte[] Render(string html)
    {
        using var stream = new MemoryStream();
        PdfConverter.Convert(html, stream, Page);
        return stream.ToArray();
    }

    private static List<LayoutPage> Layout(string html)
        => TextLayout.Layout(HtmlDocumentReader.ReadTree(html, Page, default), Page, default);

    private static void RejectAndRecover(string html, string expected)
    {
        using var destination = new MemoryStream();
        destination.Write("CUSTOMER-PDF-ORIGINAL"u8);
        destination.Position = 4;
        var failure = Assert.Throws<FactsPdfException>(() =>
            PdfConverter.Convert(html, destination, Page));
        Assert.That(failure!.Code, Is.EqualTo(expected));
        Assert.That(destination.ToArray(), Is.EqualTo("CUSTOMER-PDF-ORIGINAL"u8.ToArray()));
        Assert.That(destination.Position, Is.EqualTo(4));
        Assert.That(Render("<p>Recovered</p>").Length, Is.GreaterThan(100));
    }

    [TestCase("div")]
    [TestCase("section")]
    [TestCase("article")]
    [TestCase("blockquote")]
    public void BreakBeforeStartsPaintedPanelOnTheFollowingPdfPage(string tag)
    {
        var html = "<p style='margin:0'>BEFORE</p><" + tag +
            " style='break-before:page;background:#edf5fa;border:1pt solid #23446b;" +
            "padding:5pt'><p style='margin:0'>PANEL</p></" + tag + ">" +
            "<p style='margin:0'>AFTER</p>";
        var pages = Layout(html);
        Assert.That(pages, Has.Count.EqualTo(2));
        Assert.That(pages[0].Runs.Select(r => r.Text), Is.EqualTo(new[] { "BEFORE" }));
        Assert.That(pages[1].Runs.Select(r => r.Text), Is.EqualTo(new[] { "PANEL", "AFTER" }));
        Assert.That(pages[1].PaintBoxes, Has.Count.EqualTo(1));
        Assert.That(pages[1].PaintBoxes[0].Top,
            Is.EqualTo(tag == "blockquote" ? 28 : 20).Within(0.00001));
        Assert.That(Encoding.ASCII.GetString(Render(html)), Does.Contain("/Count 2"));
    }

    [Test]
    public void BreakAfterASectionAppliesBeforeAnUnpaintedFollowingPanel()
    {
        const string html = "<section style='break-after:page;background:#dae8f5'>" +
            "<p style='margin:0'>FIRST</p></section>" +
            "<div style='padding-top:8pt'><p style='margin:0'>SECOND</p></div>";
        var pages = Layout(html);
        Assert.That(pages, Has.Count.EqualTo(2));
        Assert.That(pages[0].Runs.Single().Text, Is.EqualTo("FIRST"));
        Assert.That(pages[1].Runs.Single().Text, Is.EqualTo("SECOND"));
        Assert.That(pages[1].Runs.Single().Baseline, Is.LessThan(
            Layout("<p style='margin:0'>SECOND</p>")[0].Runs[0].Baseline));
        Assert.That(Encoding.ASCII.GetString(Render(html)), Does.Contain("/Count 2"));
    }

    [Test]
    public void ExistingAndNewPageBreaksCoalesceWithoutBlankMiddlePage()
    {
        const string html = "<p style='margin:0;break-after:page'>FIRST</p>" +
            "<section style='break-before:page;background:#ddddee'>" +
            "<p style='margin:0'>SECOND</p></section>";
        var pages = Layout(html);
        Assert.That(pages, Has.Count.EqualTo(2));
        Assert.That(pages[0].Runs.Single().Text, Is.EqualTo("FIRST"));
        Assert.That(pages[1].Runs.Single().Text, Is.EqualTo("SECOND"));
        Assert.That(Render(html), Is.EqualTo(Render(
            html.Replace("break-after:page", "break-after:auto"))));
    }

    [Test]
    public void LegacyAlwaysAliasesRenderIdenticalPdfBytesToModernPageBreaks()
    {
        const string old = "<p style='page-break-after:always;margin:0'>FIRST</p>" +
            "<section style='page-break-before:auto;page-break-after:always;" +
            "background:#edf5fa'><p style='margin:0'>SECOND</p></section>" +
            "<article style='page-break-before:auto'><p style='margin:0'>THIRD</p></article>";
        var modern = old.Replace("page-break-after:always", "break-after:page")
            .Replace("page-break-before:auto", "break-before:auto");
        Assert.That(Render(old), Is.EqualTo(Render(modern)));
        var pages = Layout(old);
        Assert.That(pages, Has.Count.EqualTo(3));
        Assert.That(pages.Select(p => p.Runs.Single().Text),
            Is.EqualTo(new[] { "FIRST", "SECOND", "THIRD" }));
    }

    [Test]
    public void ScopedPanelCascadeImportantAndCssWideKeywordsSelectTheBoundary()
    {
        const string html = "<style>section.report{page-break-before:always!important}" +
            ".report{break-before:auto}</style><p style='margin:0'>FIRST</p>" +
            "<section class='report' style='break-before:auto;break-after:page'>" +
            "<p style='margin:0'>SECOND</p></section>" +
            "<article style='break-before:inherit'><p style='margin:0'>THIRD</p></article>";
        var doc = HtmlDocumentReader.ReadTree(html, Page, default);
        var section = doc.Children.OfType<BlockNode>().Single(n => n.Name == "section");
        var article = doc.Children.OfType<BlockNode>().Single(n => n.Name == "article");
        Assert.That(section.BoxStyle.BreakBefore, Is.True);
        Assert.That(section.BoxStyle.BreakAfter, Is.True);
        Assert.That(article.BoxStyle.BreakBefore, Is.False);
        Assert.That(Layout(html), Has.Count.EqualTo(3));
        Assert.That(Render(html), Is.EqualTo(Render(
            "<p style='margin:0'>FIRST</p>" +
            "<section style='break-before:page;break-after:page'>" +
            "<p style='margin:0'>SECOND</p></section>" +
            "<article><p style='margin:0'>THIRD</p></article>")));
    }

    [Test]
    public void NestedSectionBeforeBreakPaintsTwoActualOuterFragments()
    {
        const string html = "<section style='background:#eeeeff;padding:4pt'>" +
            "<p style='margin:0'>BEFORE</p>" +
            "<div style='page-break-before:always;background:#ffeecc'>" +
            "<p style='margin:0'>INNER</p></div>" +
            "<p style='margin:0'>AFTER</p></section>";
        var pages = Layout(html);
        Assert.That(pages, Has.Count.EqualTo(2));
        Assert.That(pages[0].Runs.Single().Text, Is.EqualTo("BEFORE"));
        Assert.That(pages[1].Runs.Select(r => r.Text), Is.EqualTo(new[] { "INNER", "AFTER" }));
        Assert.That(pages[0].PaintBoxes, Has.Count.EqualTo(1));
        Assert.That(pages[1].PaintBoxes, Has.Count.EqualTo(2));
        Assert.That(Encoding.ASCII.GetString(Render(html)), Does.Contain("/Count 2"));
    }

    [Test]
    public void AnOpeningSectionBreakNeverCreatesAnEmptyFirstPage()
    {
        const string html = "<section style='margin-top:12pt;break-before:page;" +
            "background:#ddddee'><p style='margin:0'>FIRST</p></section>";
        Assert.That(Layout(html), Has.Count.EqualTo(1));
        Assert.That(Encoding.ASCII.GetString(Render(html)), Does.Contain("/Count 1"));
    }

    [Test]
    public void HiddenSectionDoesNotProducePhantomPageBreaks()
    {
        const string html = "<p style='margin:0'>BEFORE</p>" +
            "<section style='display:none;page-break-before:always;page-break-after:always'>" +
            "<p>HIDDEN</p></section><p style='margin:0'>AFTER</p>";
        Assert.That(Layout(html), Has.Count.EqualTo(1));
        Assert.That(Render(html), Is.EqualTo(Render(
            "<p style='margin:0'>BEFORE</p><p style='margin:0'>AFTER</p>")));
    }


    [TestCase("min-height:80pt")]
    [TestCase("padding-top:22pt;padding-bottom:5pt")]
    [TestCase("margin-top:19pt;margin-bottom:3pt")]
    public void AClosedEmptyUnpaintedPanelStillOccupiesThePageBeforeAForcedBreak(string css)
    {
        var html = "<div style='" + css + "'></div>" +
            "<section style='break-before:page'><p style='margin:0'>SECOND</p></section>";
        var pages = Layout(html);
        Assert.That(pages, Has.Count.EqualTo(2));
        Assert.That(pages[0].Runs, Is.Empty);
        Assert.That(pages[1].Runs.Single().Text, Is.EqualTo("SECOND"));
        Assert.That(Encoding.ASCII.GetString(Render(html)), Does.Contain("/Count 2"));
        Assert.That(Render(html), Is.Not.EqualTo(Render(
            "<section style='break-before:page'><p style='margin:0'>SECOND</p></section>")));
    }

    [Test]
    public void AStillOpenAncestorTopMarginDoesNotCreateAnEmptyLeadingPage()
    {
        const string html = "<div style='margin-top:12pt'>" +
            "<section style='break-before:page'><p style='margin:0'>FIRST</p>" +
            "</section></div>";
        var pages = Layout(html);
        Assert.That(pages, Has.Count.EqualTo(1));
        Assert.That(pages[0].Runs.Single().Text, Is.EqualTo("FIRST"));
        Assert.That(Encoding.ASCII.GetString(Render(html)), Does.Contain("/Count 1"));
    }

    [Test]
    public void SuppressedLeadingSectionBreakPreservesOpenAncestorMarginAndPaint()
    {
        const string baseline = "<div style='margin-top:12pt;background:#eeeeff'>" +
            "<section><p style='margin:0'>FIRST</p></section></div>";
        var forced = baseline.Replace("<section>", "<section style='break-before:page'>");
        var actual = Layout(forced);
        var control = Layout(baseline);
        Assert.That(actual, Has.Count.EqualTo(1));
        Assert.That(actual[0].Runs.Single().Baseline,
            Is.EqualTo(control[0].Runs.Single().Baseline).Within(0.000001));
        Assert.That(actual[0].PaintBoxes.Single().Top,
            Is.EqualTo(control[0].PaintBoxes.Single().Top).Within(0.000001));
        Assert.That(Render(forced), Is.EqualTo(Render(baseline)));
    }

    [TestCase("padding-top:22pt;padding-bottom:5pt", 27d)]
    [TestCase("padding-bottom:13pt", 13d)]
    [TestCase("margin-top:11pt;margin-bottom:4pt", 15d)]
    public void BreakAfterMovesBeforeEmptyUnpaintedSuccessorAndKeepsItsSpace(
        string geometry, double reserved)
    {
        var html = "<section style='break-after:page'><p style='margin:0'>FIRST</p></section>" +
            "<div style='" + geometry + "'></div><p style='margin:0'>SECOND</p>";
        var equivalent = html.Replace("break-after:page", "break-after:auto")
            .Replace("<div style='", "<div style='break-before:page;");
        var actual = Layout(html);
        Assert.That(actual, Has.Count.EqualTo(2));
        Assert.That(actual[0].Runs.Single().Text, Is.EqualTo("FIRST"));
        Assert.That(actual[1].Runs.Single().Text, Is.EqualTo("SECOND"));
        var freshBaseline = Layout("<p style='margin:0'>SECOND</p>")[0].Runs.Single().Baseline;
        Assert.That(freshBaseline - actual[1].Runs.Single().Baseline,
            Is.EqualTo(reserved).Within(0.000001));
        Assert.That(Render(html), Is.EqualTo(Render(equivalent)));
    }

    [Test]
    public void DuplicateParagraphBreakAfterSectionPageBoundaryKeepsPanelMargin()
    {
        const string html = "<section style='break-after:page'>" +
            "<p style='margin:0'>FIRST</p></section>" +
            "<div style='margin-top:12pt;background:#eeeeff'>" +
            "<p style='margin:0;break-before:page'>SECOND</p></div>";
        var control = html.Replace("break-before:page", "break-before:auto");
        var actual = Layout(html);
        var expected = Layout(control);
        Assert.That(actual, Has.Count.EqualTo(2));
        Assert.That(actual[0].Runs.Single().Text, Is.EqualTo("FIRST"));
        Assert.That(actual[1].Runs.Single().Text, Is.EqualTo("SECOND"));
        Assert.That(actual[1].Runs.Single().Baseline,
            Is.EqualTo(expected[1].Runs.Single().Baseline).Within(0.000001));
        Assert.That(actual[1].PaintBoxes.Single().Top,
            Is.EqualTo(Page.Margin + 12).Within(0.000001));
        Assert.That(Render(html), Is.EqualTo(Render(control)));
    }

    [Test]
    public void EmptyNestedSectionMustNotReusePriorPageDescendantOccupancy()
    {
        const string html = "<div><p>FIRST</p>" +
            "<section style='break-before:page'></section></div>" +
            "<section style='break-before:page'><p>SECOND</p></section>";
        var pages = Layout(html);
        Assert.That(pages, Has.Count.EqualTo(2));
        Assert.That(pages[0].Runs.Single().Text, Is.EqualTo("FIRST"));
        Assert.That(pages[1].Runs.Single().Text, Is.EqualTo("SECOND"));
        Assert.That(Encoding.ASCII.GetString(Render(html)), Does.Contain("/Count 2"));
    }

    [Test]
    public void EmptyParagraphConsumesPrecedingSectionBreakWithoutAnExtraBlankPage()
    {
        const string html = "<section style='break-after:page'><p style='margin:0'>FIRST</p></section>" +
            "<p style='break-before:page'></p><p style='margin:0'>SECOND</p>";
        var control = html.Replace("break-before:page", "break-before:auto");
        var pages = Layout(html);
        Assert.That(pages, Has.Count.EqualTo(2));
        Assert.That(pages[0].Runs.Single().Text, Is.EqualTo("FIRST"));
        Assert.That(pages[1].Runs.Single().Text, Is.EqualTo("SECOND"));
        Assert.That(Render(html), Is.EqualTo(Render(control)));
        Assert.That(Encoding.ASCII.GetString(Render(html)), Does.Contain("/Count 2"));
    }

    [TestCase("margin-top:12pt")]
    [TestCase("padding-top:12pt")]
    public void ConsumedOpeningSpaceCannotReoccupyAnEmptyContinuationPage(string opening)
    {
        var html = "<div style='" + opening + "'><div style='min-height:80pt'></div>" +
            "<section style='break-before:page'></section></div>" +
            "<section style='break-before:page'><p style='margin:0'>SECOND</p></section>";
        var pages = Layout(html);
        Assert.That(pages, Has.Count.EqualTo(2));
        Assert.That(pages[0].Runs, Is.Empty);
        Assert.That(pages[1].Runs.Single().Text, Is.EqualTo("SECOND"));
        var freshBaseline = Layout("<p style='margin:0'>SECOND</p>")[0].Runs.Single().Baseline;
        Assert.That(pages[1].Runs.Single().Baseline,
            Is.EqualTo(freshBaseline).Within(0.000001));
        Assert.That(Encoding.ASCII.GetString(Render(html)), Does.Contain("/Count 2"));
    }

    [Test]
    public void ParagraphExplicitInheritReadsContainingPanelPageBreak()
    {
        const string inherit = "<section style='break-after:page'>" +
            "<p style='margin:0;break-after:inherit'>A</p>" +
            "<p style='margin:0'>B</p></section>";
        var pages = Layout(inherit);
        Assert.That(pages, Has.Count.EqualTo(2));
        Assert.That(pages[0].Runs.Single().Text, Is.EqualTo("A"));
        Assert.That(pages[1].Runs.Single().Text, Is.EqualTo("B"));
        Assert.That(Encoding.ASCII.GetString(Render(inherit)), Does.Contain("/Count 2"));
        var reset = inherit.Replace("break-after:inherit", "break-after:unset");
        Assert.That(Layout(reset), Has.Count.EqualTo(1));
        Assert.That(Render(inherit), Is.Not.EqualTo(Render(reset)));
    }

    [Test]
    public void ParagraphExplicitBeforeInheritUsesPanelComputedPageBoundary()
    {
        const string html = "<p style='margin:0'>FIRST</p>" +
            "<section style='break-before:page'>" +
            "<p style='margin:0;break-before:inherit'>SECOND</p></section>";
        var pages = Layout(html);
        Assert.That(pages, Has.Count.EqualTo(2));
        Assert.That(pages[0].Runs.Single().Text, Is.EqualTo("FIRST"));
        Assert.That(pages[1].Runs.Single().Text, Is.EqualTo("SECOND"));
    }

    [Test]
    public void UnsupportedBreakKindsAndTargetsRejectAndPreserveCustomerPdf()
    {
        RejectAndRecover("<section style='page-break-before:left'>NO</section>", "FPDF1202");
        RejectAndRecover("<section style='page-break-after:right'>NO</section>", "FPDF1202");
        RejectAndRecover("<section style='break-after:avoid'>NO</section>", "FPDF1201");
        RejectAndRecover("<section style='page-break-before:avoid'>NO</section>", "FPDF1202");
        RejectAndRecover("<p>YES<span style='page-break-before:always'>NO</span></p>", "FPDF1201");
        RejectAndRecover("<ol style='break-before:page'><li>NO</li></ol>", "FPDF1201");
        RejectAndRecover("<section style='break-inside:avoid'>NO</section>", "FPDF1201");
    }
}
