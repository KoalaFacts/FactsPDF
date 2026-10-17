using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

[TestFixture]
public sealed class ListStructureTests
{
    private static DocumentRoot Read(string html, PdfOptions? options = null)
        => HtmlDocumentReader.ReadTree(html, options ?? new(), default);

    [Test]
    public void RetainsMixedTwoLevelListAndParagraphOrder()
    {
        var tree = Read("<ol><li>A<p>B</p><ul><li>C</li></ul>D</li><li>E</li></ol><p>F</p>");
        var list = (BlockNode)tree.Children[0];
        var item = (BlockNode)list.Children[0];
        Assert.That(list.Name, Is.EqualTo("ol"));
        Assert.That(list.Children.Cast<BlockNode>().Select(n => n.Name), Is.EqualTo(new[] { "li", "li" }));
        Assert.That(item.Children.Select(n => n is BlockNode b ? b.Name : "text"),
            Is.EqualTo(new[] { "text", "p", "ul", "text" }));
        Assert.That(BlockTreeTraversal.Paragraphs(tree, default).SelectMany(p => p.Runs).Select(r => r.Text),
            Is.EqualTo(new[] { "A", "B", "C", "D", "E", "F" }));
    }

    [TestCase("<ol><li>A<li>B</ol>")]
    [TestCase("<ol><li><p>A<li><p>B</ol>")]
    [TestCase("<OL><LI>A</LI><LI>B</LI></OL>")]
    public void OptionalItemEndTagsPreserveSiblings(string html)
    {
        var list = (BlockNode)Read(html).Children.Single();
        Assert.That(list.Children, Has.Count.EqualTo(2));
        Assert.That(list.Children.Cast<BlockNode>().All(n => n.Name == "li"), Is.True);
    }

    [Test]
    public void OptionalItemEndTagsPreserveNesting()
    {
        var list = (BlockNode)Read("<ol><li>A<ul><li>B<li>C</ul><li>D</ol>").Children.Single();
        Assert.That(list.Children, Has.Count.EqualTo(2));
        var inner = (BlockNode)((BlockNode)list.Children[0]).Children[1];
        Assert.That(inner.Name, Is.EqualTo("ul"));
        Assert.That(inner.Children, Has.Count.EqualTo(2));
    }

    [Test]
    public void EmptyItemsAndListsRetainStructure()
    {
        var tree = Read("<ol><li></li><li> </li></ol><ul></ul>");
        Assert.That(((BlockNode)tree.Children[0]).Children, Has.Count.EqualTo(2));
        Assert.That(((BlockNode)tree.Children[1]).Children, Is.Empty);
    }

    [Test]
    public void ListBoxesUseExistingSelectorsAndBoxStyles()
    {
        var list = (BlockNode)Read("<style>ol{padding:3pt}ol>li{width:50%;color:red}</style><ol><li>A</li></ol>").Children.Single();
        var item = (BlockNode)list.Children.Single();
        Assert.That(list.BoxStyle.PaddingLeft.Value, Is.EqualTo(3));
        Assert.That(item.BoxStyle.Width, Is.EqualTo(new CssLength(50, true)));
        Assert.That(item.Style.Color, Is.EqualTo(new Rgb(1, 0, 0)));
    }

    [TestCase("<li>A</li>")]
    [TestCase("<ol>A</ol>")]
    [TestCase("<ol>&#160;</ol>")]
    [TestCase("<ol><p>A</p></ol>")]
    [TestCase("<ol><ul></ul></ol>")]
    [TestCase("<ol><style>p{color:red}</style></ol>")]
    [TestCase("<ol><li><ul><li><ol></ol></li></ul></li></ol>")]
    [TestCase("<ol><li><span>A<li>B</ol>")]
    [TestCase("<ol><li><div>A<li>B</ol>")]
    [TestCase("<ol><li>A")]
    [TestCase("<ol><li/></ol>")]
    public void InvalidListStructureProtectsOutput(string html) => Fails(html, "FPDF1101");

    [TestCase("<ol start='2'><li>A</li></ol>")]
    [TestCase("<ol reversed><li>A</li></ol>")]
    [TestCase("<ul type='square'><li>A</li></ul>")]
    [TestCase("<ol><li value='4'>A</li></ol>")]
    public void UnsupportedListAttributesProtectOutput(string html) => Fails(html, "FPDF1103");

    [TestCase("<ol style='margin-top:1pt'><li>A</li></ol>")]
    [TestCase("<ul><li style='break-before:page'>A</li></ul>")]
    public void ListsKeepContainerCssApplicability(string html) => Fails(html, "FPDF1201");

    [Test]
    public void CssValidationStillPrecedesListStructureValidation()
        => Fails("<ol><style>p{not-a-property:x}</style></ol>", "FPDF1201");

    [Test]
    public void ListsCountTowardsExistingElementAndDepthLimits()
    {
        const string html = "<ol><li>A</li></ol>";
        Assert.DoesNotThrow(() => Read(html, new() { MaxElements = 2, MaxDepth = 2 }));
        Fails(html, "FPDF1002", new() { MaxElements = 1 });
        Fails(html, "FPDF1003", new() { MaxDepth = 1 });
    }

    private static void Fails(string html, string code, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        output.Write("sentinel"u8);
        output.Position = 3;
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, options));
        Assert.That(error!.Code, Is.EqualTo(code));
        Assert.That(Encoding.ASCII.GetString(output.ToArray()), Is.EqualTo("sentinel"));
        Assert.That(output.Position, Is.EqualTo(3));
        Assert.That(error.SourceOffset, Is.GreaterThanOrEqualTo(0));
    }
}
