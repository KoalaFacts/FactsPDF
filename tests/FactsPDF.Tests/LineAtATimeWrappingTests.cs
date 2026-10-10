using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

[TestFixture]
public sealed class LineAtATimeWrappingTests
{
    private static readonly PdfOptions Page = new()
    {
        PageWidth = 300, PageHeight = 500, Margin = 20,
        MaxPages = 10000, MaxDisplayCommands = 20000
    };
    private static readonly TextStyle Style = new(12, 1.2, new Rgb(0, 0, 0), TextAlignment.Left);

    private static Paragraph Paragraph(string text, TextStyle? style = null)
    {
        var result = new Paragraph(style ?? Style);
        result.Runs.Add(new TextRun(text, style ?? Style));
        return result;
    }

    [Test]
    public void LongParagraphReusesLineGlyphStorageAndRetainsAllText()
    {
        var input = new List<Paragraph> { Paragraph(string.Concat(Enumerable.Repeat("A ", 100000))) };
        TextLayout.Layout([Paragraph("A")], Page, default);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var pages = TextLayout.Layout(input, Page, default);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.That(pages.Sum(p => p.Runs.Sum(r => r.Text.Count(c => c == 'A'))), Is.EqualTo(100000));
        Assert.That(pages.Count, Is.GreaterThan(1));
        TestContext.WriteLine($"M11 long-paragraph managed allocation: {allocated} bytes");
        Assert.That(allocated, Is.LessThan(8L * 1024 * 1024),
            "Completed line glyph buffers must be reused, not accumulated for the whole paragraph.");
    }

    [TestCase(1, "FPDF1302")]
    [TestCase(10000, "FPDF1302")]
    public void LateOverwideWordStillPrecedesEarlierPageOverflow(int maxPages, string code)
    {
        var paragraph = Paragraph(string.Concat(Enumerable.Repeat("A ", 1000)) + new string('X', 100));
        var error = Assert.Throws<FactsPdfException>(() => TextLayout.Layout([paragraph],
            Page with { MaxPages = maxPages }, default));
        Assert.That(error!.Code, Is.EqualTo(code));
    }

    [Test]
    public void LateWidthErrorStillPrecedesEarlierDisplayBudgetError()
    {
        var paragraph = Paragraph(string.Concat(Enumerable.Repeat("A ", 1000)) + new string('X', 100));
        var error = Assert.Throws<FactsPdfException>(() => TextLayout.Layout([paragraph],
            Page with { MaxDisplayCommands = 1 }, default));
        Assert.That(error!.Code, Is.EqualTo("FPDF1302"));
    }

    [Test]
    public void LateGlyphErrorStillPrecedesWidthAndPageErrors()
    {
        var paragraph = Paragraph(new string('X', 100) + " 中文");
        var error = Assert.Throws<FactsPdfException>(() => TextLayout.Layout([paragraph],
            Page with { MaxPages = 1, MaxDisplayCommands = 1 }, default));
        Assert.That(error!.Code, Is.EqualTo("FPDF1301"));
    }

    [Test]
    public void ReusedLineStorageCannotModifyPreviouslyPlacedRuns()
    {
        var paragraph = Paragraph("A B C D E F G H I J");
        var pages = TextLayout.Layout([paragraph], Page with { PageWidth = 60 }, default);
        Assert.That(pages.SelectMany(p => p.Runs).Select(r => r.Text),
            Is.EqualTo(new[] { "A B", "C D", "E F", "G H", "I J" }));
    }

    [Test]
    public void ExplicitBlankLinesKeepBaselineSpacingAndNoTrailingBlankPage()
    {
        var p = new Paragraph(Style);
        p.Runs.Add(new TextRun("A", Style));
        p.Runs.Add(new TextRun("", Style, true));
        p.Runs.Add(new TextRun("", Style, true));
        p.Runs.Add(new TextRun("B", Style));
        p.Runs.Add(new TextRun("", Style, true));
        var pages = TextLayout.Layout([p], Page, default);
        Assert.That(pages, Has.Count.EqualTo(1));
        Assert.That(pages[0].Runs.Select(r => r.Text), Is.EqualTo(new[] { "A", "B" }));
        Assert.That(pages[0].Runs[0].Baseline - pages[0].Runs[1].Baseline, Is.EqualTo(28.8).Within(0.00001));
    }

    [Test]
    public void NarrowAndWideParagraphsDoNotShareMutableLineState()
    {
        var html = "<section style='width:20pt'>A B C D</section><p>Wide line stays here</p>";
        var pages = TextLayout.Layout(HtmlDocumentReader.ReadTree(html, Page, default), Page, default);
        Assert.That(pages.SelectMany(p => p.Runs).Select(r => r.Text),
            Is.EqualTo(new[] { "A B", "C D", "Wide line stays here" }));
    }

    [Test]
    public void FailedIncrementalLayoutStillLeavesCallerBytesUntouched()
    {
        using var output = new MemoryStream();
        output.Write("keep"u8);
        var html = "<p>" + string.Concat(Enumerable.Repeat("A ", 1000)) + "</p>";
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output,
            Page with { MaxPages = 1 }));
        Assert.That(error!.Code, Is.EqualTo("FPDF1303"));
        Assert.That(Encoding.ASCII.GetString(output.ToArray()), Is.EqualTo("keep"));
    }
}
