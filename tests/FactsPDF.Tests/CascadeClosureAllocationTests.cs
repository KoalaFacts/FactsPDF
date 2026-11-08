using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

// Computing an element's style runs its cascade through local functions. A lambda in the same
// method that captured one of its locals would turn all of them into one closure object per
// element, so reading a page would allocate it once for every element.
[TestFixture]
[NonParallelizable]
public sealed class CascadeClosureAllocationTests
{
    private static string Paragraphs(int count)
    {
        var html = new StringBuilder();
        for (var i = 0; i < count; i++) html.Append("<p>Paragraph ").Append(i).Append("</p>");
        return html.ToString();
    }

    private static long ReadAllocated(string html, PdfOptions options)
    {
        for (var i = 0; i < 3; i++) HtmlDocumentReader.ReadTree(html, options, default);
        var before = GC.GetAllocatedBytesForCurrentThread();
        HtmlDocumentReader.ReadTree(html, options, default);
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    // Measured on main 27cae7d: 728 bytes read per paragraph with a closure per element;
    // 656 without it.
    [Test]
    public void ReadingAParagraphAllocatesNoCascadeClosure()
    {
        // 1,000 and 2,000 paragraphs (3 tokens each) keep both reads under the reader's 8,192-token
        // pooled list, so list growth does not count as per-paragraph cost.
        var options = new PdfOptions { MaxElements = 100_000 };
        var small = ReadAllocated(Paragraphs(1_000), options);
        var large = ReadAllocated(Paragraphs(2_000), options);
        var perParagraph = (large - small) / 1_000.0;
        TestContext.Out.WriteLine($"read per paragraph: {perParagraph:F1} bytes");
        Assert.That(perParagraph, Is.LessThan(PerParagraphLimit));
    }

    // A rounded box with an uneven border still fails, now checked without lambdas.
    [TestCase("border: 1pt solid red; border-left-color: blue")]
    [TestCase("border: 1pt solid; border-left-width: 2pt")]
    [TestCase("border: 1pt dashed")]
    [TestCase("border: 1pt solid; border-left-width: 0")]
    [TestCase("color: blue; border: 1pt solid red; border-left-color: currentcolor")]
    public void RoundedBoxesStillRequireAnEvenSolidBorder(string border)
    {
        var html = $"<div style=\"border-radius: 4pt; {border}\">x</div>";
        var error = Assert.Throws<FactsPdfException>(() => Pdf.Write().ToBytes(html));
        Assert.That(error!.Code, Is.EqualTo("FPDF1201"));
        Assert.That(error.Message, Does.Contain("Rounded report boxes"));
    }

    // currentcolor on three sides and the text colour given explicitly on the fourth is one ink.
    [TestCase("border: 1pt solid red")]
    [TestCase("color: red; border: 1pt solid; border-left-color: red")]
    [TestCase("border-radius: 4pt")]
    public void RoundedBoxesWithAnEvenBorderOrNoneRender(string css)
    {
        var html = $"<div style=\"border-radius: 4pt; {css}\">x</div>";
        Assert.That(Pdf.Write().ToBytes(html), Is.Not.Empty);
    }

    private const double PerParagraphLimit = 690;
}
