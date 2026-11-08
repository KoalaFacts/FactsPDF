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

    // Measured on main 27cae7d: 860 bytes read per paragraph (2,000 to 4,000 paragraphs) with a closure per
    // element; 788 without it.
    [Test]
    public void ReadingAParagraphAllocatesNoCascadeClosure()
    {
        var options = new PdfOptions { MaxElements = 100_000 };
        var small = ReadAllocated(Paragraphs(2_000), options);
        var large = ReadAllocated(Paragraphs(4_000), options);
        var perParagraph = (large - small) / 2_000.0;
        TestContext.Out.WriteLine($"read per paragraph: {perParagraph:F1} bytes");
        Assert.That(perParagraph, Is.LessThan(PerParagraphLimit));
    }

    // A rounded box with an uneven border still fails, now checked without lambdas.
    [TestCase("border: 1pt solid red; border-left-color: blue")]
    [TestCase("border: 1pt solid; border-left-width: 2pt")]
    [TestCase("border: 1pt dashed")]
    [TestCase("border: 1pt solid; border-left-width: 0")]
    public void RoundedBoxesStillRequireAnEvenSolidBorder(string border)
    {
        var html = $"<div style=\"border-radius: 4pt; {border}\">x</div>";
        var error = Assert.Throws<FactsPdfException>(() => Pdf.Write().ToBytes(html));
        Assert.That(error!.Code, Is.EqualTo("FPDF1201"));
        Assert.That(error.Message, Does.Contain("Rounded report boxes"));
    }

    // currentcolor on one side and the same explicit text colour on the others is one ink.
    [TestCase("border: 1pt solid red")]
    [TestCase("color: red; border: 1pt solid; border-left-color: red")]
    [TestCase("border-radius: 4pt")]
    public void RoundedBoxesWithAnEvenBorderOrNoneRender(string css)
    {
        var html = $"<div style=\"border-radius: 4pt; {css}\">x</div>";
        Assert.That(Pdf.Write().ToBytes(html), Is.Not.Empty);
    }

    private const double PerParagraphLimit = 825;
}
