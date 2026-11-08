using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

// A paragraph element's run list and its block's child list are sized exactly, so a one-run
// paragraph keeps two one-slot arrays instead of List's default four slots each.
[TestFixture]
[NonParallelizable]
public sealed class ParagraphArrayCapacityTests
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

    // Measured on main 7ecab3f: 600 bytes read per paragraph; 552 with exact arrays.
    [Test]
    public void ReadingAParagraphKeepsExactArrays()
    {
        // 1,000 and 2,000 paragraphs stay under the reader's 8,192-token pooled list.
        var options = new PdfOptions { MaxElements = 100_000 };
        var small = ReadAllocated(Paragraphs(1_000), options);
        var large = ReadAllocated(Paragraphs(2_000), options);
        var perParagraph = (large - small) / 1_000.0;
        TestContext.Out.WriteLine($"read per paragraph: {perParagraph:F1} bytes");
        Assert.That(perParagraph, Is.LessThan(575));
    }

    [TestCase("<p>one</p>", 1)]
    [TestCase("<p></p>", 0)]
    [TestCase("<p>a <b>b</b> c</p>", 3)]
    [TestCase("<p>a<br>b<br/>c</p>", 5)]
    [TestCase("<h2>a <span>b</span></h2>", 2)]
    [TestCase("<pre>a\n  b</pre>", 1)]
    [TestCase("<div><p>a <i>b</i><p>c</div>", 2)] // the first p closes at the next p
    public void RunListIsSizedToTheParagraphsRuns(string html, int runs)
    {
        var paragraph = FirstParagraphElement(HtmlDocumentReader.ReadTree(html, new PdfOptions(), default).Children);
        Assert.That(paragraph.Children, Has.Count.EqualTo(1));
        Assert.That(paragraph.Children.Capacity, Is.EqualTo(1));
        var text = ((ParagraphNode)paragraph.Children[0]).Paragraph;
        Assert.That(text.Runs, Has.Count.EqualTo(runs));
        Assert.That(text.Runs.Capacity, Is.EqualTo(runs));
    }

    [Test]
    public void AParagraphClosedByAnEnclosingEndTagStillRendersAllItsRuns()
    {
        var tree = HtmlDocumentReader.ReadTree("<div><p>a <span>b</span> c</div><p>d</p>", new PdfOptions(), default);
        var paragraph = FirstParagraphElement(tree.Children);
        var runs = ((ParagraphNode)paragraph.Children[0]).Paragraph.Runs;
        Assert.That(runs.Select(r => r.Text), Is.EqualTo(new[] { "a ", "b", " c" }));
    }

    [Test]
    public void ParagraphsWithManyRunsRenderTheSameText()
    {
        var html = new StringBuilder("<p>");
        for (var i = 0; i < 40; i++) html.Append('w').Append(i).Append(" <span>x</span> ");
        html.Append("</p>");
        var tree = HtmlDocumentReader.ReadTree(html.ToString(), new PdfOptions(), default);
        var runs = ((ParagraphNode)FirstParagraphElement(tree.Children).Children[0]).Paragraph.Runs;
        Assert.That(runs, Has.Count.EqualTo(81));
        Assert.That(runs.Capacity, Is.EqualTo(81));
        Assert.That(Pdf.Write().ToBytes(html.ToString()), Is.Not.Empty);
    }

    private static BlockNode FirstParagraphElement(List<BlockChild> children)
    {
        foreach (var child in children)
        {
            if (child is BlockNode { Name: "p" or "h2" or "pre" } paragraph) return paragraph;
            if (child is BlockNode box) return FirstParagraphElement(box.Children);
        }
        throw new AssertionException("No paragraph element.");
    }
}
