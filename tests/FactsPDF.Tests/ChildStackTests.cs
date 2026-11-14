using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

// The reader collects every open block's children on one pooled stack and moves them into an
// exact-size array (or the lone-child slot) when the block closes, instead of each block growing
// its own array by doubling.
[TestFixture]
[NonParallelizable]
public sealed class ChildStackTests
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

    [TestCase(1, 1)]
    [TestCase(2, 2)]
    [TestCase(3, 3)]
    [TestCase(5, 5)]
    [TestCase(1_000, 1_000)]
    public void TheRootKeepsExactlyItsChildren(int paragraphs, int capacity)
    {
        var root = HtmlDocumentReader.ReadTree(Paragraphs(paragraphs), new PdfOptions(), default);
        Assert.That(root.Children, Has.Count.EqualTo(paragraphs));
        Assert.That(root.Children.Capacity, Is.EqualTo(capacity));
        for (var i = 0; i < paragraphs; i++)
            Assert.That(((BlockNode)root.Children[i]).SourceOffset, Is.EqualTo(Paragraphs(i).Length));
    }

    [Test]
    public void NestedBlocksKeepTheirChildrenInOrderInExactArrays()
    {
        const string html = "<div><p>A</p><div><p>B</p><p>C</p><p>D</p></div><p>E</p></div><p>F</p>";
        var root = HtmlDocumentReader.ReadTree(html, new PdfOptions(), default);
        Assert.That(root.Children, Has.Count.EqualTo(2));
        Assert.That(root.Children.Capacity, Is.EqualTo(2));
        var outer = (BlockNode)root.Children[0];
        Assert.That(outer.Children.Select(Name), Is.EqualTo(new[] { "p", "div", "p" }));
        Assert.That(outer.Children.Capacity, Is.EqualTo(3)); // 4 on main
        var inner = (BlockNode)outer.Children[1];
        Assert.That(inner.Children, Has.Count.EqualTo(3));
        Assert.That(inner.Children.Capacity, Is.EqualTo(3));
        Assert.That(inner.Children.Select(Text), Is.EqualTo(new[] { "B", "C", "D" }));
        Assert.That(Text(outer.Children[2]), Is.EqualTo("E"));
        Assert.That(Text(root.Children[1]), Is.EqualTo("F"));
    }

    [Test]
    public void AHiddenBlockIsDroppedWithItsChildrenAndDirectTextResumes()
    {
        const string html = "<div>Before <div style=\"display:none\"><p>X</p><div><p>Y</p></div></div>after<p>Z</p></div>";
        var root = HtmlDocumentReader.ReadTree(html, new PdfOptions(), default);
        var outer = (BlockNode)root.Children[0];
        Assert.That(outer.Children, Has.Count.EqualTo(2));
        var resumed = (Paragraph)outer.Children[0];
        Assert.That(resumed.IsAnonymous, Is.True);
        Assert.That(string.Concat(resumed.Runs.Select(run => run.Text)), Is.EqualTo("Before after"));
        Assert.That(Text(outer.Children[1]), Is.EqualTo("Z"));
        Assert.That(HtmlDocumentReader.PooledChildren?.Count ?? 0, Is.Zero);
    }

    [Test]
    public void StackIsEmptyAndPooledAfterARead()
    {
        HtmlDocumentReader.ReadTree("<div><p>A</p><p>B</p></div>", new PdfOptions(), default);
        var pooled = HtmlDocumentReader.PooledChildren;
        Assert.That(pooled, Is.Not.Null);
        Assert.That(pooled!.Value.Count, Is.Zero);
        Assert.That(pooled.Value.Capacity, Is.GreaterThan(0).And.LessThanOrEqualTo(HtmlDocumentReader.MaxPooledChildren));
    }

    [TestCase("<div><p>A</p><div><p>B</p><blink>C</blink></div></div>", "FPDF1102")]
    [TestCase("<div><p>A</p><p>B &bogus;</p></div>", "FPDF1104")]
    [TestCase("<div><p>A</p><div><p>B</p></div>", "FPDF1101")]
    public void StackIsEmptyAfterAFailedRead(string html, string code)
    {
        var error = Assert.Throws<FactsPdfException>(() => HtmlDocumentReader.ReadTree(html, new PdfOptions(), default));
        Assert.That(error!.Code, Is.EqualTo(code));
        Assert.That(HtmlDocumentReader.PooledChildren?.Count ?? 0, Is.Zero);
        // The next read starts from an empty stack and keeps only its own children.
        var root = HtmlDocumentReader.ReadTree("<p>Next</p>", new PdfOptions(), default);
        Assert.That(root.Children, Has.Count.EqualTo(1));
    }

    [Test]
    public void StackLargerThanTheLimitIsNotKept()
    {
        HtmlDocumentReader.ReadTree(Paragraphs(HtmlDocumentReader.MaxPooledChildren + 1), new PdfOptions { MaxElements = 100_000 }, default);
        Assert.That(HtmlDocumentReader.PooledChildren, Is.Null);
        HtmlDocumentReader.ReadTree("<p>A</p>", new PdfOptions(), default);
        Assert.That(HtmlDocumentReader.PooledChildren!.Value.Capacity, Is.LessThanOrEqualTo(HtmlDocumentReader.MaxPooledChildren));
    }

    // Main ef83cbe: 312.4 bytes read per paragraph (the root's array doubled up to 2,048 slots).
    [Test]
    public void ReadingParagraphsAllocatesNoDoublingChildArrays()
    {
        var options = new PdfOptions { MaxElements = 100_000 };
        var perParagraph = (ReadAllocated(Paragraphs(2_000), options) - ReadAllocated(Paragraphs(1_000), options)) / 1_000.0;
        TestContext.Out.WriteLine($"read per paragraph: {perParagraph:F1} bytes");
        Assert.That(perParagraph, Is.LessThan(308));
    }

    // Main ef83cbe: 792.3 bytes per paragraph converted.
    [Test]
    public void ConvertingParagraphsKeepsTheExactChildArraySaving()
    {
        static long Converted(string html)
        {
            for (var i = 0; i < 3; i++) Pdf.Write().ToBytes(html);
            var before = GC.GetAllocatedBytesForCurrentThread();
            Pdf.Write().ToBytes(html);
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }
        var perParagraph = (Converted(Paragraphs(400)) - Converted(Paragraphs(200))) / 200.0;
        TestContext.Out.WriteLine($"converted per paragraph: {perParagraph:F1} bytes");
        Assert.That(perParagraph, Is.LessThan(786));
    }

    private static string Name(BlockChild child) => ((BlockNode)child).Name;

    private static string Text(BlockChild child)
    {
        var node = (BlockNode)child;
        return string.Concat(((Paragraph)node.Children[0]).Runs.Select(run => run.Text));
    }
}
