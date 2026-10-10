using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

[TestFixture]
public sealed class BoundedGlyphWrappingTests
{
    private static readonly PdfOptions Page = new()
    {
        PageWidth = 300, PageHeight = 500, Margin = 20,
        MaxPages = 10000, MaxDisplayCommands = 20000
    };
    private static readonly TextStyle Style = new(12, 1.2, new Rgb(0, 0, 0), TextAlignment.Left);

    private static byte[] Render(string html, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, options ?? Page);
        return output.ToArray();
    }

    [Test]
    public void LongParagraphDoesNotAllocateAParagraphWideGlyphCopy()
    {
        // Input construction is deliberately outside the measured interval.
        // The bound distinguishes a second full glyph copy from line storage;
        // it is not a total-memory or timing claim.
        var paragraph = new Paragraph(Style);
        paragraph.Runs.Add(new TextRun(string.Concat(Enumerable.Repeat("A ", 100000)), Style));
        var input = new List<Paragraph> { paragraph };
        var warmup = new Paragraph(Style);
        warmup.Runs.Add(new TextRun("A", Style));
        TextLayout.Layout([warmup], Page, default);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var pages = TextLayout.Layout(input, Page, default);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.That(pages.Sum(page => page.Runs.Sum(run => run.Text.Count(c => c == 'A'))),
            Is.EqualTo(100000), "The memory reduction must not drop any text.");
        Assert.That(pages.Count, Is.GreaterThan(1));
        TestContext.WriteLine($"M10 long-paragraph layout managed allocation: {allocated} bytes");
        Assert.That(allocated, Is.LessThan(32L * 1024 * 1024),
            "Wrapping should retain lines and bounded word scratch, not a second paragraph-wide glyph list.");
    }

    [TestCase("<p>A   <br>   B<br><br>C   </p>", "<p>A<br>B<br><br>C</p>")]
    [TestCase("<p>   <br> A   </p>", "<p><br>A</p>")]
    public void WhitespaceAndExplicitBreaksKeepByteIdenticalOutput(string expanded, string normalized)
        => Assert.That(Render(expanded), Is.EqualTo(Render(normalized)));

    [Test]
    public void CollapsedSpaceRetainsTheFirstWhitespaceRunStyle()
    {
        var actual = Render("<p>A<span style='color:red'> </span><span>  B</span></p>");
        var expected = Render("<p>A<span style='color:red'> </span>B</p>");
        Assert.That(actual, Is.EqualTo(expected));
        Assert.That(Encoding.ASCII.GetString(actual), Does.Contain("1 0 0 rg"));
    }

    [TestCase("中文", "FPDF1301")]
    [TestCase("\uD800", "FPDF1304")]
    public void LaterGlyphValidationStillPrecedesAnEarlierWidthError(string suffix, string code)
    {
        var paragraph = new Paragraph(Style);
        paragraph.Runs.Add(new TextRun(new string('A', 100) + " " + suffix, Style));
        var error = Assert.Throws<FactsPdfException>(() => TextLayout.Layout([paragraph], Page, default));
        Assert.That(error!.Code, Is.EqualTo(code),
            "Removing temporary storage must not change the established paragraph diagnostic order.");
    }

    [Test]
    public void ExactDisplayBudgetKeepsLongWrappedPdfBytes()
    {
        var html = "<section style='padding:3pt;border:2pt solid blue;background-color:#cee8fb'><p>" +
            string.Join(" ", Enumerable.Repeat("alpha beta gamma", 1000)) + "</p></section>";
        var pages = TextLayout.Layout(HtmlDocumentReader.ReadTree(html, Page, default), Page, default);
        var commands = pages.Sum(page => page.Runs.Count + page.PaintBoxes.Sum(box => box.CommandCount));
        Assert.That(Render(html, Page with { MaxDisplayCommands = commands }), Is.EqualTo(Render(html)));
    }
}
