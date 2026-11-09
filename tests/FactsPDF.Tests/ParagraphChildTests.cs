using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

// A paragraph is its own block child: no wrapper object per paragraph in the read tree.
[TestFixture]
[NonParallelizable]
public sealed class ParagraphChildTests
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

    // Measured on top of Z6g2b2 (528 bytes per paragraph, including a 32-byte wrapper per
    // paragraph): 496 without the wrapper.
    [Test]
    public void ReadingAParagraphAllocatesNoWrapper()
    {
        // 1,000 and 2,000 paragraphs stay under the reader's 8,192-token pooled list.
        var options = new PdfOptions { MaxElements = 100_000 };
        var small = ReadAllocated(Paragraphs(1_000), options);
        var large = ReadAllocated(Paragraphs(2_000), options);
        var perParagraph = (large - small) / 1_000.0;
        TestContext.Out.WriteLine($"read per paragraph: {perParagraph:F1} bytes");
        Assert.That(perParagraph, Is.LessThan(515));
    }

    [Test]
    public void ElementParagraphsAndDirectTextKeepTheirAnonymity()
    {
        var tree = HtmlDocumentReader.ReadTree("<div>before<p>inside</p>after</div>", new PdfOptions(), default);
        var div = (BlockNode)tree.Children.Single();
        Assert.That(div.Children, Has.Count.EqualTo(3));
        var before = (Paragraph)div.Children[0];
        var p = (BlockNode)div.Children[1];
        var inside = (Paragraph)p.Children.Single();
        var after = (Paragraph)div.Children[2];
        Assert.That((before.IsAnonymous, inside.IsAnonymous, after.IsAnonymous), Is.EqualTo((true, false, true)));
        Assert.That(inside.Runs.Single().Text, Is.EqualTo("inside"));
    }

    // A hidden block between two runs of direct text resumes the anonymous paragraph before it,
    // which the reader re-adds as the same object, still anonymous.
    [Test]
    public void AHiddenBlockResumesTheAnonymousParagraph()
    {
        var tree = HtmlDocumentReader.ReadTree("<div>a<div style=\"display:none\">x</div>b</div>", new PdfOptions(), default);
        var div = (BlockNode)tree.Children.Single();
        var text = (Paragraph)div.Children.Single();
        Assert.That(text.IsAnonymous, Is.True);
        Assert.That(string.Concat(text.Runs.Select(r => r.Text)), Does.Contain("a").And.Contain("b"));
    }
}
