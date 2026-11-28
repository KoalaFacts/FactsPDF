using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

// Tagging keeps its structure and marked-content tables in pooled arrays and the serializers' builders per
// thread, so a conversion allocates no more for tagging than the bytes of the structure it writes.
[TestFixture]
[NonParallelizable]
public sealed class TaggedAllocationTests
{
    private static string Paragraphs(int count)
    {
        var html = new StringBuilder("<html lang=\"en\"><head><title>T</title></head><body>");
        for (var i = 0; i < count; i++) html.Append("<p>Paragraph ").Append(i).Append("</p>");
        return html.Append("</body></html>").ToString();
    }

    private static (long Allocated, int Length) Converted(string html, bool tagged)
    {
        var writer = Pdf.Write().WithTaggedPdf(tagged);
        var length = 0;
        for (var i = 0; i < 3; i++) length = writer.ToBytes(html).Length;
        var before = GC.GetAllocatedBytesForCurrentThread();
        writer.ToBytes(html);
        return (GC.GetAllocatedBytesForCurrentThread() - before, length);
    }

    [AllocationBudget]
    [Test]
    public void TaggingAllocatesNoMoreThanTheStructureItAddsToTheOutput()
    {
        var (taggedLarge, taggedLargeLength) = Converted(Paragraphs(400), tagged: true);
        var (taggedSmall, taggedSmallLength) = Converted(Paragraphs(200), tagged: true);
        var (plainLarge, plainLargeLength) = Converted(Paragraphs(400), tagged: false);
        var (plainSmall, plainSmallLength) = Converted(Paragraphs(200), tagged: false);
        var taggedPerParagraph = (taggedLarge - taggedSmall) / 200.0;
        var plainPerParagraph = (plainLarge - plainSmall) / 200.0;
        var outputPerParagraph = ((taggedLargeLength - taggedSmallLength) - (plainLargeLength - plainSmallLength)) / 200.0;
        TestContext.Out.WriteLine($"per paragraph: tagged {taggedPerParagraph:F1}, untagged {plainPerParagraph:F1}, output growth {outputPerParagraph:F1} bytes");
        Assert.That(outputPerParagraph, Is.GreaterThan(100), "the structure is written to the output");
        // The output array is the one allocation tagging may add, and it is the output itself.
        Assert.That(taggedPerParagraph - plainPerParagraph, Is.LessThan(outputPerParagraph + 24));
    }

    [AllocationBudget]
    [Test]
    public void ATaggedConversionStaysWithinItsAllocationBudget()
    {
        var (allocated, _) = Converted(Paragraphs(200), tagged: true);
        TestContext.Out.WriteLine($"200 tagged paragraphs: {allocated / 1024} KiB per conversion");
        Assert.That(allocated, Is.LessThan(210 * 1024));
    }
}
