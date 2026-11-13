using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

// Z6h: the HTML token list is sized once from a pre-count instead of doubling past the pooled
// capacity, and a block node keeps its list ordinals as plain ints.
[TestFixture]
[NonParallelizable]
public sealed class ReadCapacityTests
{
    private static string Paragraphs(int count)
    {
        var html = new StringBuilder();
        for (var i = 0; i < count; i++) html.Append("<p>Paragraph ").Append(i).Append("</p>");
        return html.ToString();
    }

    private static readonly TextStyle Style = new(12, 1.2, new Rgb(0, 0, 0), TextAlignment.Left);

    private static int TokenCount(string html) => HtmlTokens.Read(html, default).Count();

    [TestCase("", 0)]
    [TestCase("text", 1)]
    [TestCase("<p>a</p>", 3)]
    [TestCase("<p></p>", 2)]
    [TestCase("a<br>b", 3)]
    [TestCase("<p>a</p>\n<p>b</p>", 7)]
    [TestCase("<p>a</p>x", 4)]
    public void EstimateIsExactWhereTagsAndTextAlternate(string html, int expected)
    {
        Assert.That(CssStylesheets.EstimateTokens(html), Is.EqualTo(expected));
        Assert.That(TokenCount(html), Is.EqualTo(expected));
    }

    [Test]
    public void EstimateCoversEveryExample()
    {
        var folder = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "examples");
        var files = Directory.GetFiles(folder, "*.html");
        Assert.That(files, Is.Not.Empty);
        foreach (var file in files)
        {
            var html = File.ReadAllText(file);
            var actual = TokenCount(html);
            var estimate = CssStylesheets.EstimateTokens(html);
            // A '>' inside a comment, attribute or stylesheet over-counts; nothing under-counts.
            Assert.That(estimate, Is.InRange(actual, actual + actual / 4 + 8), Path.GetFileName(file));
        }
    }

    // 20,000 paragraphs (60,000 tokens, far past the pooled list): on main the token list doubled
    // from 8,192 slots to 65,536 and each block node carried two int? ordinals. Measured
    // 9,970 KiB on main and 9,258 KiB after Z6h; the limit fails on main.
    [Test]
    public void ReadingManyParagraphsSizesTheTokenListOnce()
    {
        var html = Paragraphs(20_000);
        var options = new PdfOptions { MaxElements = 100_000 };
        for (var i = 0; i < 3; i++) HtmlDocumentReader.ReadTree(html, options, default);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var tree = HtmlDocumentReader.ReadTree(html, options, default);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        TestContext.Out.WriteLine($"read 20,000 paragraphs: {allocated / 1024} KiB");
        Assert.That(tree.Children, Has.Count.EqualTo(20_000));
        Assert.That(allocated, Is.LessThan(9_600 * 1024L));
    }

    [Test]
    public void ListOrdinalsSurviveTheirCompactStorage()
    {
        var node = new BlockNode("ol", Style, BoxStyle.Initial, 0) { ListStart = 7, ListValue = null, ListReversed = true };
        Assert.That(node.ListStart, Is.EqualTo(7));
        Assert.That(node.ListValue, Is.Null);
        Assert.That(new BlockNode("li", Style, BoxStyle.Initial, 0) { ListValue = 999_999 }.ListValue, Is.EqualTo(999_999));
        Assert.That(new BlockNode("ul", Style, BoxStyle.Initial, 0).ListStart, Is.Null);
    }

    [Test]
    public void PackedListOrdinalsKeepStartValueAndReversedApart()
    {
        var reversed = new BlockNode("ol", Style, BoxStyle.Initial, 0) { ListReversed = true, ListStart = 999_999 };
        Assert.That((reversed.ListStart, reversed.ListValue, reversed.ListReversed), Is.EqualTo(((int?)999_999, (int?)null, true)));
        var plain = new BlockNode("ol", Style, BoxStyle.Initial, 0) { ListStart = 1 };
        Assert.That((plain.ListStart, plain.ListValue, plain.ListReversed), Is.EqualTo(((int?)1, (int?)null, false)));
        var item = new BlockNode("li", Style, BoxStyle.Initial, 0) { ListValue = 3, ListStart = null, ListReversed = false };
        Assert.That((item.ListStart, item.ListValue, item.ListReversed), Is.EqualTo(((int?)null, (int?)3, false)));
        var onlyReversed = new BlockNode("ol", Style, BoxStyle.Initial, 0) { ListReversed = true, ListStart = null };
        Assert.That((onlyReversed.ListStart, onlyReversed.ListReversed), Is.EqualTo(((int?)null, true)));
        Assert.Throws<InvalidOperationException>(() => _ = new BlockNode("ol", Style, BoxStyle.Initial, 0) { ListStart = 2, ListValue = 3 });
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = new BlockNode("ol", Style, BoxStyle.Initial, 0) { ListStart = 1_000_000 });
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = new BlockNode("li", Style, BoxStyle.Initial, 0) { ListValue = 0 });
    }

    [Test]
    public void ReversedListWithStartStillNumbersDownward()
    {
        var tree = HtmlDocumentReader.ReadTree("<ol reversed start=\"5\"><li>a</li><li value=\"9\">b</li></ol>", new PdfOptions(), default);
        var list = (BlockNode)tree.Children[0];
        Assert.That((list.ListStart, list.ListValue, list.ListReversed), Is.EqualTo(((int?)5, (int?)null, true)));
        var second = (BlockNode)list.Children[1];
        Assert.That((second.ListStart, second.ListValue, second.ListReversed), Is.EqualTo(((int?)null, (int?)9, false)));
    }
}
