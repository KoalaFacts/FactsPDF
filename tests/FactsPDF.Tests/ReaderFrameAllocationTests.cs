using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

// The document reader keeps one frame per open element on its stack and one selector element
// per open element on its selector path. Both are values in those lists, so opening an element
// costs neither a frame object nor a selector-element object of its own.
[TestFixture]
[NonParallelizable]
public sealed class ReaderFrameAllocationTests
{
    private static string Nested(int depth, int repeats)
    {
        var html = new StringBuilder();
        for (var r = 0; r < repeats; r++)
        {
            for (var i = 0; i < depth; i++) html.Append("<div>");
            html.Append('A');
            for (var i = 0; i < depth; i++) html.Append("</div>");
        }
        return html.ToString();
    }

    private static long Allocated(string html, PdfOptions options)
    {
        for (var i = 0; i < 3; i++) HtmlDocumentReader.ReadTree(html, options, default);
        var before = GC.GetAllocatedBytesForCurrentThread();
        HtmlDocumentReader.ReadTree(html, options, default);
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    // Measured 498 bytes per nested div when each frame was its own object, 442 as a value.
    [Test]
    public void OpeningAnElementAllocatesNoFrameObject()
    {
        var options = new PdfOptions { MaxDepth = 64 };
        var small = Allocated(Nested(8, 100), options);
        var large = Allocated(Nested(8, 200), options);
        var perElement = (large - small) / 800.0;
        TestContext.Out.WriteLine($"per element: {perElement:F1} bytes");
        Assert.That(perElement, Is.LessThan(470));
    }

    // Measured 442 bytes per nested div when each selector element was its own object, 418 as a value.
    [Test]
    public void OpeningAnElementAllocatesNoSelectorElementObject()
    {
        var options = new PdfOptions { MaxDepth = 64 };
        var small = Allocated(Nested(8, 100), options);
        var large = Allocated(Nested(8, 200), options);
        Assert.That((large - small) / 800.0, Is.LessThan(430));
    }

    [Test]
    public void DescendantAndClassSelectorsStillMatchThroughValueElements()
    {
        const string html = "<style>section .a p{color:#c00}div#k>p{color:#00c}</style>"
            + "<section><div class='a b'><p>R</p></div></section><div id='k'><p>B</p></div><p>N</p>";
        var tree = HtmlDocumentReader.ReadTree(html, new PdfOptions(), default);
        var colours = new List<Rgb>();
        void Walk(BlockChild child)
        {
            if (child is BlockNode box) foreach (var c in box.Children) Walk(c);
            else if (child is ParagraphNode paragraph) colours.Add(paragraph.Paragraph.Runs[0].Style.Color);
        }
        foreach (var child in tree.Children) Walk(child);
        Assert.That(colours, Is.EqualTo(new[] { new Rgb(0xcc / 255d, 0, 0), new Rgb(0, 0, 0xcc / 255d), new Rgb(0, 0, 0) }));
    }

    [Test]
    public void DeepNestingStillRendersTheSameText()
    {
        var options = new PdfOptions { MaxDepth = 64 };
        var tree = HtmlDocumentReader.ReadTree(Nested(60, 2), options, default);
        Assert.That(tree.Children, Has.Count.EqualTo(2));
        foreach (var top in tree.Children)
        {
            var (depth, text) = Deepest(top, 0);
            Assert.That(depth, Is.EqualTo(60));
            Assert.That(text, Is.EqualTo("A"));
        }
    }

    private static (int Depth, string Text) Deepest(BlockChild child, int depth) => child switch
    {
        BlockNode { Children: [var only] } box => Deepest(only, depth + 1),
        ParagraphNode paragraph => (depth, string.Concat(paragraph.Paragraph.Runs.Select(r => r.Text))),
        _ => (-1, ""),
    };

    [Test]
    public void NestingPastTheLimitStillFails()
    {
        var error = Assert.Throws<FactsPdfException>(() => HtmlDocumentReader.ReadTree(Nested(17, 1), new PdfOptions { MaxDepth = 16 }, default));
        Assert.That(error!.Code, Is.EqualTo("FPDF1003"));
    }
}
