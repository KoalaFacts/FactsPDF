using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// Block Box Tree structure without expanding the rendered CSS subset.
/// These tests name observable tree and PDF contracts, not future box paint.
/// </summary>
[TestFixture]
public sealed class BlockBoxTreeTests
{
    private static DocumentRoot Tree(string html, PdfOptions? options = null,
        CancellationToken cancellation = default)
        => HtmlDocumentReader.ReadTree(html, options ?? new(), cancellation);

    private static BlockNode Box(BlockChild node) => (BlockNode)node;
    private static ParagraphNode Text(BlockChild node) => (ParagraphNode)node;
    private static string Content(ParagraphNode leaf)
        => string.Concat(leaf.Paragraph.Runs.Where(r => !r.IsBreak).Select(r => r.Text));

    [Test]
    public void NestedBlockHierarchyFollowsActualHtmlContainers()
    {
        var root = Tree("<!doctype html><html><head><title>hidden</title></head>" +
            "<body><section><article><div><p>A</p></div></article></section></body></html>");
        var body = Box(root.Children.Single());
        Assert.That(body.Name, Is.EqualTo("body"));
        var section = Box(body.Children.Single());
        Assert.That(section.Name, Is.EqualTo("section"));
        var article = Box(section.Children.Single());
        Assert.That(article.Name, Is.EqualTo("article"));
        var div = Box(article.Children.Single());
        Assert.That(div.Name, Is.EqualTo("div"));
        var paragraph = Box(div.Children.Single());
        Assert.That(paragraph.Name, Is.EqualTo("p"));
        Assert.That(paragraph.Children, Has.Count.EqualTo(1));
        Assert.That(Text(paragraph.Children[0]).IsAnonymous, Is.False);
        Assert.That(Content(Text(paragraph.Children[0])), Is.EqualTo("A"));
        Assert.That(root.Children.OfType<BlockNode>().Any(n => n.Name == "html"), Is.False);
    }

    [Test]
    public void DirectTextBeforeAndAfterNestedBlockStaysInSourceOrder()
    {
        var root = Tree("<div>before <span style='color:red'>inline</span>" +
            "<section><p>middle</p></section>after</div>");
        var div = Box(root.Children.Single());
        Assert.That(div.Children, Has.Count.EqualTo(3));
        var before = Text(div.Children[0]);
        Assert.That(before.IsAnonymous, Is.True);
        Assert.That(Content(before), Is.EqualTo("before inline"));
        Assert.That(before.Paragraph.Runs, Has.Count.EqualTo(2));
        Assert.That(before.Paragraph.Runs[1].Style.Color, Is.EqualTo(new Rgb(1, 0, 0)));
        var middle = Box(div.Children[1]);
        Assert.That(middle.Name, Is.EqualTo("section"));
        Assert.That(Box(middle.Children.Single()).Name, Is.EqualTo("p"));
        Assert.That(Content(Text(div.Children[2])), Is.EqualTo("after"));
        Assert.That(Text(div.Children[2]).IsAnonymous, Is.True);
    }

    [Test]
    public void WhitespaceBetweenBlocksDoesNotCreatePhantomParagraphs()
    {
        var div = Box(Tree("<div>\n  <p>A</p> \n  <p>B</p>\n</div>").Children.Single());
        Assert.That(div.Children, Has.Count.EqualTo(2));
        Assert.That(div.Children, Is.All.TypeOf<BlockNode>());
        Assert.That(div.Children.Cast<BlockNode>().Select(b => b.Name),
            Is.EqualTo(new[] { "p", "p" }));
    }

    [Test]
    public void EmptyExplicitParagraphIsRetainedButNoGhostAnonymousTextAppears()
    {
        var body = Box(Tree("<body> \n <p></p> \n <p>A</p> </body>").Children.Single());
        Assert.That(body.Children, Has.Count.EqualTo(2));
        var empty = Box(body.Children[0]);
        Assert.That(empty.Children, Has.Count.EqualTo(1));
        Assert.That(Text(empty.Children[0]).IsAnonymous, Is.False);
        Assert.That(Text(empty.Children[0]).Paragraph.Runs, Is.Empty);
    }

    [Test]
    public void AnonymousParagraphInheritsContainingBlockInsteadOfInitialSpanStyle()
    {
        var div = Box(Tree("<div style='font-size:14pt'><span style='font-size:20pt'>X</span> Y</div>")
            .Children.Single());
        var leaf = Text(div.Children.Single());
        Assert.That(leaf.IsAnonymous, Is.True);
        Assert.That(leaf.Paragraph.Style.FontSize, Is.EqualTo(14));
        Assert.That(leaf.Paragraph.Runs[0].Style.FontSize, Is.EqualTo(20));
        Assert.That(leaf.Paragraph.Runs[1].Style.FontSize, Is.EqualTo(14));
    }

    [Test]
    public void OmittedParagraphEndTagsProduceSiblingBoxesUnderBody()
    {
        var root = Tree("<html><body><p>A<p>B</body></html>");
        var body = Box(root.Children.Single());
        Assert.That(body.Children, Has.Count.EqualTo(2));
        Assert.That(body.Children.Select(n => Box(n).Name),
            Is.EqualTo(new[] { "p", "p" }));
        Assert.That(Content(Text(Box(body.Children[0]).Children.Single())), Is.EqualTo("A"));
        Assert.That(Content(Text(Box(body.Children[1]).Children.Single())), Is.EqualTo("B"));
    }

    [Test]
    public void ContainerTextBeforeAndAfterNestedContainerPreservesThreeLeaves()
    {
        var div = Box(Tree("<div>A<div>B</div>C</div>").Children.Single());
        Assert.That(div.Children, Has.Count.EqualTo(3));
        Assert.That(Content(Text(div.Children[0])), Is.EqualTo("A"));
        Assert.That(Content(Text(Box(div.Children[1]).Children.Single())), Is.EqualTo("B"));
        Assert.That(Content(Text(div.Children[2])), Is.EqualTo("C"));
    }

    [Test]
    public void LateCssRulesStillStyleNestedParagraphThroughRealAncestorPath()
    {
        var root = Tree("<body><section class='outer'><article><p>nested</p></article></section></body>" +
            "<style>.outer > article p{color:red}</style>");
        var body = Box(root.Children.Single());
        var p = Box(Box(Box(body.Children.Single()).Children.Single()).Children.Single());
        Assert.That(p.Name, Is.EqualTo("p"));
        Assert.That(Text(p.Children.Single()).Paragraph.Style.Color, Is.EqualTo(new Rgb(1, 0, 0)));
    }

    [Test]
    public void BrRemainsInlineBreakNotBlockNode()
    {
        var para = Box(Tree("<p>A<br>B</p>").Children.Single());
        Assert.That(para.Children, Has.Count.EqualTo(1));
        var leaf = Text(para.Children.Single());
        Assert.That(leaf.Paragraph.Runs.Count(r => r.IsBreak), Is.EqualTo(1));
        Assert.That(Content(leaf), Is.EqualTo("AB"));
    }

    [Test]
    public void InvalidBlockInsideSpanStillFailsWithOriginalCode()
    {
        var ex = Assert.Throws<FactsPdfException>(() =>
            Tree("<div><span><p>bad</p></span></div>"));
        Assert.That(ex!.Code, Is.EqualTo("FPDF1101"));
    }

    [Test]
    public void DeeplyNestedBlocksRespectConfiguredDepthBudget()
    {
        var html = "<div><section><article><p>A</p></article></section></div>";
        var ex = Assert.Throws<FactsPdfException>(() =>
            Tree(html, new PdfOptions { MaxDepth = 3 }));
        Assert.That(ex!.Code, Is.EqualTo("FPDF1003"));
    }

    [Test]
    public void PreCancelledTreeReadDoesNotProducePartialResult()
    {
        Assert.Throws<OperationCanceledException>(() =>
            Tree("<div><p>text</p></div>", cancellation: new CancellationToken(true)));
    }
}
