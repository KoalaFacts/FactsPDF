using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

// An element whose box declarations and box inputs (element name, parent box, root font size,
// computed font size and colour) match an earlier element's shares that element's computed box
// instead of a private copy. Sharing must never change a computed value or a diagnostic.
[TestFixture]
[NonParallelizable]
public sealed class SharedComputedBoxTests
{
    private static readonly PdfOptions Options = new() { MaxElements = 100_000 };

    private static BoxStyle[] Boxes(string html) =>
        HtmlDocumentReader.ReadTree(html, Options, default).Children.Cast<BlockNode>().Select(block => block.BoxStyle).ToArray();

    private static byte[] Render(string html)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, new PdfOptions());
        return output.ToArray();
    }

    private static double ReadBytesPerParagraph(string style)
    {
        long Read(int count)
        {
            var html = new StringBuilder();
            for (var i = 0; i < count; i++) html.Append("<p style=\"").Append(style).Append("\">AB</p>");
            var source = html.ToString();
            for (var i = 0; i < 10; i++) HtmlDocumentReader.ReadTree(source, Options, default);
            var least = long.MaxValue;
            for (var i = 0; i < 3; i++)
            {
                var before = GC.GetAllocatedBytesForCurrentThread();
                HtmlDocumentReader.ReadTree(source, Options, default);
                least = Math.Min(least, GC.GetAllocatedBytesForCurrentThread() - before);
            }
            return least;
        }
        return (Read(2_000) - Read(1_000)) / 1_000.0;
    }

    [AllocationBudget]
    [Test]
    public void ARepeatedBoxStyleIsNotCopiedAgain()
    {
        var repeated = ReadBytesPerParagraph("padding:3pt;border:0.5pt solid #789");
        TestContext.Out.WriteLine($"repeated box: {repeated:F1} bytes per paragraph");
        Assert.That(repeated, Is.LessThan(1_600));
    }

    [Test]
    public void ElementsWithTheSameBoxInputsShareOneBox()
    {
        var boxes = Boxes("<div style=\"padding:3pt;border:0.5pt solid #789\">A</div><div style=\"border:0.5pt solid #789;padding:3pt\">B</div>"
            + "<div style=\"padding:3pt;border:0.5pt solid #789\">C</div>");
        Assert.That(boxes[1], Is.SameAs(boxes[0]));
        Assert.That(boxes[2], Is.SameAs(boxes[0]));
        Assert.That(boxes[0].PaddingLeft.Value, Is.EqualTo(3));
        Assert.That(boxes[0].BorderTop.EffectiveWidth, Is.EqualTo(0.5));
    }

    [TestCase("<div style=\"font-size:10pt;padding:1em\">A</div><div style=\"font-size:20pt;padding:1em\">B</div>", 10, 20)]
    [TestCase("<div style=\"font-size:10pt\"><div style=\"padding:1em\">A</div></div><div style=\"font-size:20pt\"><div style=\"padding:1em\">B</div></div>", 10, 20)]
    [TestCase("<div style=\"padding:4pt\"><div style=\"padding:inherit\">A</div></div><div style=\"padding:8pt\"><div style=\"padding:inherit\">B</div></div>", 4, 8)]
    public void DifferentBoxInputsKeepTheirOwnValues(string html, double first, double second)
    {
        var outer = HtmlDocumentReader.ReadTree(html, Options, default).Children.Cast<BlockNode>().ToArray();
        BoxStyle Inner(BlockNode block) => block.Children.Count > 0 && block.Children[0] is BlockNode inner ? inner.BoxStyle : block.BoxStyle;
        Assert.That(Inner(outer[0]).PaddingLeft.Value, Is.EqualTo(first));
        Assert.That(Inner(outer[1]).PaddingLeft.Value, Is.EqualTo(second));
    }

    [Test]
    public void CurrentColorBordersFollowEachElementsColor()
    {
        const string Shared = "<div style=\"color:red;border:1pt solid currentcolor;border-radius:2pt\">A</div>"
            + "<div style=\"color:blue;border:1pt solid currentcolor;border-radius:2pt\">B</div>";
        const string Separate = "<div style=\"color:red;border:1pt solid red;border-radius:2pt\">A</div>"
            + "<div style=\"color:blue;border:1pt solid blue;border-radius:2pt\">B</div>";
        Assert.That(Render(Shared), Is.EqualTo(Render(Separate)));
    }

    [Test]
    public void AnElementAfterASharedBoxStillReportsItsOwnError()
    {
        // Same box declarations as the first paragraph (shared box), but an invalid text value.
        var text = Assert.Throws<FactsPdfException>(() => Boxes(
            "<div style=\"padding:3pt\">A</div><div style=\"padding:3pt;color:#78g\">B</div>"))!;
        Assert.That(text.Code, Is.EqualTo("FPDF1202"));
        Assert.That(text.SourceOffset, Is.EqualTo(62));
        // Different box value: computed again, so its own failure is reported at its own offset.
        var box = Assert.Throws<FactsPdfException>(() => Boxes(
            "<div style=\"padding:3pt\">A</div><div style=\"padding:3qq\">B</div>"))!;
        Assert.That(box.Code, Is.EqualTo("FPDF1202"));
        Assert.That(box.SourceOffset, Is.EqualTo(52));
    }

    [Test]
    public void SharingNeverChangesTheInitialOrParentBox()
    {
        Boxes("<div style=\"padding:4pt\"><p style=\"padding:inherit\">A</p><p style=\"padding:inherit\">B</p></div>"
            + "<div style=\"padding:4pt\"><p style=\"padding:inherit\">C</p></div>");
        Assert.That(BoxStyle.Initial, Is.EqualTo(new BoxStyle()));
    }
}
