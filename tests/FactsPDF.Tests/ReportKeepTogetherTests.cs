using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>Scoped page preflight for flat text report cards, not general paged-media keep.</summary>
[TestFixture]
public sealed class ReportKeepTogetherTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 220, Margin = 20 };

    private static List<LayoutPage> Layout(string html)
        => TextLayout.Layout(HtmlDocumentReader.ReadTree(html, Page, default), Page, default);

    private static byte[] Render(string html)
    {
        using var stream = new MemoryStream();
        PdfConverter.Convert(html, stream, Page);
        return stream.ToArray();
    }

    private static void RejectAndRecover(string html, string code)
    {
        using var stream = new MemoryStream();
        stream.Write("ORIGINAL-CUSTOMER-PDF"u8);
        stream.Position = 3;
        var failure = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, stream, Page));
        Assert.That(failure!.Code, Is.EqualTo(code));
        Assert.That(stream.ToArray(), Is.EqualTo("ORIGINAL-CUSTOMER-PDF"u8.ToArray()));
        Assert.That(stream.Position, Is.EqualTo(3));
        Assert.That(Render("<p>RECOVERED</p>").Length, Is.GreaterThan(100));
    }

    private const string Panel = "<section style='break-inside:avoid;" +
        "background:#eaf2fa;border:1pt solid #223344;padding:6pt'>" +
        "<h3 style='margin:0;font-size:10pt;line-height:12pt'>SIGN OFF</h3>" +
        "<p style='margin:0;font-size:10pt;line-height:12pt'>Approved by reviewer</p></section>";

    [Test]
    public void KeptCardMovesAllGlyphsAndPaintToNextPageAndMatchesManualPageBreak()
    {
        var filler = string.Concat(Enumerable.Repeat(
            "<p style='margin:0;font-size:10pt;line-height:12pt'>FILLER</p>", 13));
        var html = filler + Panel + "<p style='margin:0'>FOLLOWING</p>";
        var pages = Layout(html);
        Assert.That(pages, Has.Count.EqualTo(2));
        Assert.That(pages[0].Runs.Select(r => r.Text), Does.Not.Contain("SIGN OFF"));
        Assert.That(pages[0].Runs.Select(r => r.Text), Does.Not.Contain("Approved by reviewer"));
        Assert.That(pages[1].Runs.Select(r => r.Text), Does.Contain("SIGN OFF"));
        Assert.That(pages[1].Runs.Select(r => r.Text), Does.Contain("Approved by reviewer"));
        Assert.That(pages[1].PaintBoxes, Has.Count.EqualTo(1));
        var manual = html.Replace("break-inside:avoid", "break-before:page");
        Assert.That(Render(html), Is.EqualTo(Render(manual)));
        Assert.That(Encoding.ASCII.GetString(Render(html)), Does.Contain("/Count 2"));
    }

    [Test]
    public void CardThatAlreadyFitsDoesNotAddAnUnnecessaryPage()
    {
        var current = "<p style='margin:0'>PREVIOUS</p>" + Panel;
        var normal = current.Replace("break-inside:avoid", "break-inside:auto");
        Assert.That(Render(current), Is.EqualTo(Render(normal)));
        Assert.That(Layout(current), Has.Count.EqualTo(1));
    }

    [Test]
    public void LegacyAliasMatchesModernAvoidAndCascadeRespectsImportantAndWideReset()
    {
        Assert.That(Render(Panel), Is.EqualTo(Render(
            Panel.Replace("break-inside:avoid", "page-break-inside:avoid"))));
        const string html = "<style>section{break-inside:avoid!important}" +
            ".card{page-break-inside:auto}</style>" +
            "<section class='card' style='break-inside:initial'>" +
            "<p>ONE</p></section>";
        var node = HtmlDocumentReader.ReadTree(html, Page, default)
            .Children.OfType<BlockNode>().Single(n => n.Name == "section");
        Assert.That(node.BoxStyle.AvoidBreakInside, Is.True);
        var reset = html.Replace("break-inside:avoid!important", "break-inside:avoid");
        var resetNode = HtmlDocumentReader.ReadTree(reset, Page, default)
            .Children.OfType<BlockNode>().Single(n => n.Name == "section");
        Assert.That(resetNode.BoxStyle.AvoidBreakInside, Is.False);
    }


    [Test]
    public void CompleteHtmlBodyAllowsDirectKeptCardButNotNestedCards()
    {
        var filler = string.Concat(Enumerable.Repeat(
            "<p style='margin:0;font-size:10pt;line-height:12pt'>FILLER</p>", 13));
        var html = "<!doctype html><html><head><meta charset='utf-8'></head><body>" +
            filler + Panel + "<p style='margin:0'>FOLLOWING</p></body></html>";
        Assert.That(Layout(html), Has.Count.EqualTo(2));
        Assert.That(Render(html), Is.EqualTo(Render(
            html.Replace("break-inside:avoid", "break-before:page"))));
        RejectAndRecover("<!doctype html><html><body><div><section style='break-inside:avoid'>" +
            "<p>NESTED</p></section></div></body></html>", "FPDF1201");
    }

    [Test]
    public void KeptCardPreflightDoesNotReadMissingGlyphsAfterDisplayBudgetIsExhausted()
    {
        var options = Page with { MaxDisplayCommands = 1 };
        var html = "<p style='margin:0'>USED</p>" +
            "<section style='break-inside:avoid'><p>中文</p></section>";
        var ex = Assert.Throws<FactsPdfException>(() => TextLayout.Layout(
            HtmlDocumentReader.ReadTree(html, options, default), options, default));
        Assert.That(ex!.Code, Is.EqualTo("FPDF1401"));
    }

    [Test]
    public void UnsupportedStructureAndOversizedCardFailWithoutOverwritingExistingPdf()
    {
        RejectAndRecover("<p style='break-inside:avoid'>NO</p>", "FPDF1201");
        RejectAndRecover("<section style='break-inside:always'><p>NO</p></section>", "FPDF1202");
        RejectAndRecover("<section style='break-inside:avoid'><div><p>NO</p></div></section>", "FPDF1201");
        RejectAndRecover("<section style='break-inside:avoid;min-height:20pt'><p>NO</p></section>", "FPDF1201");
        RejectAndRecover("<div><section style='break-inside:avoid'><p>NO</p></section></div>", "FPDF1201");
        RejectAndRecover("<ol><li><section style='break-inside:avoid'><p>NO</p></section></li></ol>", "FPDF1201");
        RejectAndRecover("<section style='break-inside:avoid'><p style='break-before:page'>NO</p></section>", "FPDF1201");
        var lines = string.Concat(Enumerable.Repeat("Very long paragraph line<br>", 40));
        RejectAndRecover("<section style='break-inside:avoid'><p style='margin:0'>" +
            lines + "</p></section>", "FPDF1302");
    }
}
