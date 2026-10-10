using NUnit.Framework;

namespace FactsPDF.Tests;

[TestFixture]
public sealed class LayoutResourceGuardReviewTests
{
    private static readonly PdfOptions Page = new()
    {
        PageWidth = 300, PageHeight = 140, Margin = 20, MaxDisplayCommands = 1
    };

    private static List<LayoutPage> Layout(string html, PdfOptions? options = null)
    {
        var o = options ?? Page;
        return TextLayout.Layout(HtmlDocumentReader.ReadTree(html, o, default), o, default);
    }

    [TestCase("中文")]
    [TestCase("&nbsp;")]
    [TestCase("ABCDEFGHIJKLMNOPQRSTUVWXYZABCDEFGHIJKLMNOPQRSTUVWXYZ")]
    public void ExactBudgetRejectsNextTextBeforeGlyphResolutionOrWrapping(string next)
    {
        var error = Assert.Throws<FactsPdfException>(() => Layout("<p>A</p><p>" + next + "</p>"));
        Assert.That(error!.Code, Is.EqualTo("FPDF1401"));
    }

    [Test]
    public void ExactPaintBudgetRejectsFollowingTextBeforeGlyphResolution()
    {
        var error = Assert.Throws<FactsPdfException>(() => Layout(
            "<div style='background-color:red;padding:2pt'></div><p>中文</p>"));
        Assert.That(error!.Code, Is.EqualTo("FPDF1401"));
    }

    [Test]
    public void LeadingBreaksDoNotHideFollowingTextAfterBudgetExhaustion()
    {
        var error = Assert.Throws<FactsPdfException>(() => Layout("<p>A</p><p><br><span>中文</span></p>"));
        Assert.That(error!.Code, Is.EqualTo("FPDF1401"));
    }

    [Test]
    public void CommandFreeTrailingParagraphsRemainValidAtExactBudget()
    {
        var pages = Layout("<p>A</p><p> \t\r\n </p><p><br><br></p>" +
            "<div style='background-color:red'></div>");
        Assert.That(pages.Sum(p => p.Runs.Count + p.PaintBoxes.Sum(b => b.CommandCount)), Is.EqualTo(1));
    }

    [Test]
    public void RemainingBudgetStillReportsRealGlyphErrors()
    {
        var error = Assert.Throws<FactsPdfException>(() => Layout("<p>A</p><p>中文</p>",
            Page with { MaxDisplayCommands = 2 }));
        Assert.That(error!.Code, Is.EqualTo("FPDF1301"));
    }

    [Test]
    public void ExhaustedBudgetDoesNotMaterializeNextLargeParagraph()
    {
        // Build the input before measuring; only synchronous layout allocation
        // is counted. This generous bound detects eagerly wrapping 200k chars,
        // not a timing or general performance threshold.
        var style = new TextStyle(12, 1.2, new Rgb(0, 0, 0), TextAlignment.Left);
        var first = new Paragraph(style);
        first.Runs.Add(new TextRun("A", style));
        var next = new Paragraph(style);
        next.Runs.Add(new TextRun(string.Concat(Enumerable.Repeat("A ", 100000)), style));
        var paragraphs = new List<Paragraph> { first, next };
        FactsPdfException? failure = null;
        var before = GC.GetAllocatedBytesForCurrentThread();
        try { TextLayout.Layout(paragraphs, Page, default); }
        catch (FactsPdfException error) { failure = error; }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.That(failure, Is.Not.Null);
        Assert.That(failure!.Code, Is.EqualTo("FPDF1401"));
        Assert.That(allocated, Is.LessThan(256 * 1024),
            "An exhausted budget must reject before allocating the next glyph and line lists.");
    }
}
