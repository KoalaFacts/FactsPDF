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
