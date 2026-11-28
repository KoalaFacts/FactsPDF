using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// The next-sibling (+) and subsequent-sibling (~) combinators select by element siblings only (text,
/// comments and other nodes never count), add nothing to specificity, and cost no more than the
/// selector match budget. Each case renders byte-identical to the page where the matched elements
/// carry the style through a class.
/// </summary>
[TestFixture]
public sealed class SiblingCombinatorTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 400, Margin = 20 };

    private static byte[] Pdf(string html, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, options ?? Page);
        return output.ToArray();
    }

    private static string Doc(string css, string body) => $"<style>{css}</style>{body}";

    [TestCase("div + p{color:#c00}", "<div>a</div><p>b</p><p>c</p>", "<div>a</div><p class=x>b</p><p>c</p>")]
    [TestCase("div ~ p{color:#c00}", "<p>z</p><div>a</div><p>b</p><p>c</p>", "<p>z</p><div>a</div><p class=x>b</p><p class=x>c</p>")]
    [TestCase("div+p{color:#c00}", "<div>a</div><p>b</p>", "<div>a</div><p class=x>b</p>")]
    [TestCase("div   +   p{color:#c00}", "<div>a</div><p>b</p>", "<div>a</div><p class=x>b</p>")]
    [TestCase("div + div + div{color:#c00}", "<div>1</div><div>2</div><div>3</div><div>4</div>", "<div>1</div><div>2</div><div class=x>3</div><div class=x>4</div>")]
    [TestCase("div ~ p ~ span{color:#c00}", "<p>0</p><div>1</div><p>2</p><span>S</span>", "<p>0</p><div>1</div><p>2</p><span class=x>S</span>")]
    [TestCase("section > p + span{color:#c00}", "<section><p>P</p><span>S</span></section><p>Q</p><span>T</span>",
        "<section><p>P</p><span class=x>S</span></section><p>Q</p><span>T</span>")]
    [TestCase("section p + span ~ u{color:#c00}", "<section><div><p>P</p><span>S</span><u>I</u><u>B</u></div></section><u>Z</u>",
        "<section><div><p>P</p><span>S</span><u class=x>I</u><u class=x>B</u></div></section><u>Z</u>")]
    [TestCase("li + li{color:#c00}", "<ul><li>1</li><li>2</li><li>3</li></ul>", "<ul><li>1</li><li class=x>2</li><li class=x>3</li></ul>")]
    [TestCase("p + p{color:#c00}", "<p>1<p>2<p>3", "<p>1<p class=x>2<p class=x>3")]
    public void SelectsBySiblingPosition(string css, string body, string equivalentBody)
        => Assert.That(Pdf(Doc(css, body)), Is.EqualTo(Pdf(Doc(".x{color:#c00}", equivalentBody))));

    [TestCase("p + span{color:#c00}", "<div><p>P</p></div><span>S</span>")]
    [TestCase("p + span{color:#c00}", "<p>P</p><div>D</div><span>S</span>")]
    [TestCase("div + p{color:#c00}", "<p>P</p><div>D</div>")]
    public void SiblingsMustBeElementsInOrderUnderOneParent(string css, string body)
        => Assert.That(Pdf(Doc(css, body)), Is.EqualTo(Pdf(Doc("", body))));

    // Text and comments are not elements: they sit between siblings without separating them.
    [TestCase("p + span{color:#c00}", "<p>P</p>text<span>S</span>", "<p>P</p>text<span class=x>S</span>")]
    [TestCase("p + span{color:#c00}", "<p>P</p><!-- note --><span>S</span>", "<p>P</p><!-- note --><span class=x>S</span>")]
    [TestCase("p ~ span{color:#c00}", "<p>P</p>a<u>B</u>b<span>S</span>", "<p>P</p>a<u>B</u>b<span class=x>S</span>")]
    public void TextAndCommentsDoNotSeparateSiblings(string css, string body, string equivalentBody)
        => Assert.That(Pdf(Doc(css, body)), Is.EqualTo(Pdf(Doc(".x{color:#c00}", equivalentBody))));

    [TestCase("p + span{color:#c00}", "<p>P</p><script>var a = 1;</script><span>S</span>")]
    [TestCase("p + span{color:#c00}", "<p>P</p><template><b>x</b></template><span>S</span>")]
    public void ScriptAndTemplateElementsAreSiblingsEvenThoughTheyNeverRender(string css, string body)
        => Assert.That(Pdf(Doc(css, body)), Is.EqualTo(Pdf(Doc("", body))));

    [TestCase("script + span{color:#c00}", "<p>P</p><script id=s>var a = 1;</script><span>S</span>", "<p>P</p><script id=s>var a = 1;</script><span class=x>S</span>")]
    [TestCase("template + span{color:#c00}", "<p>P</p><template><b>x</b></template><span>S</span>", "<p>P</p><template><b>x</b></template><span class=x>S</span>")]
    [TestCase("script#s + span{color:#c00}", "<p>P</p><script id=s></script><span>S</span>", "<p>P</p><script id=s></script><span class=x>S</span>")]
    public void ADroppedElementCanBeTheLeftOperand(string css, string body, string equivalentBody)
        => Assert.That(Pdf(Doc(css, body)), Is.EqualTo(Pdf(Doc(".x{color:#c00}", equivalentBody))));

    // The combinators add nothing to specificity: only the compounds count.
    [Test]
    public void SpecificityIsTheSumOfTheCompoundsAndOrderBreaksTies()
    {
        const string body = "<div class=c>D</div><p class=a>A</p><p>B</p>";
        // div.c + p is (0,1,2); .a alone is (0,1,0): the combinator rule wins on the first p.
        Assert.That(Pdf(Doc("div.c + p{color:#c00}.a{color:#00c}", body)),
            Is.EqualTo(Pdf(Doc("p.a{color:#c00}", body))));
        // div + p.a is (0,1,2), the same as p.a plus one type: it wins over p.a whichever is later.
        Assert.That(Pdf(Doc("div + p.a{color:#c00}p.a{color:#00c}", body)),
            Is.EqualTo(Pdf(Doc("p.a{color:#c00}", body))));
        Assert.That(Pdf(Doc("p.a{color:#00c}div + p.a{color:#c00}", body)),
            Is.EqualTo(Pdf(Doc("p.a{color:#c00}", body))));
        // Equal specificity: the later rule wins.
        Assert.That(Pdf(Doc("div + p{color:#c00}div ~ p{color:#00c}", "<div>D</div><p>A</p>")),
            Is.EqualTo(Pdf(Doc("p{color:#00c}", "<div>D</div><p>A</p>"))));
    }

    [Test]
    public void StructuralPseudoClassesAndNotWorkOnSiblings()
    {
        Assert.That(Pdf(Doc("p:nth-child(2) + p{color:#c00}", "<p>1</p><p>2</p><p>3</p><p>4</p>")),
            Is.EqualTo(Pdf(Doc(".x{color:#c00}", "<p>1</p><p>2</p><p class=x>3</p><p>4</p>"))));
        Assert.That(Pdf(Doc("p:not(.a) + p{color:#c00}", "<p>1</p><p>2</p><p class=a>3</p><p>4</p>")),
            Is.EqualTo(Pdf(Doc(".x{color:#c00}", "<p>1</p><p class=x>2</p><p class='a x'>3</p><p>4</p>"))));
        Assert.That(Pdf(Doc("p[data-k] ~ p{color:#c00}", "<p>1</p><p data-k=1>2</p><p>3</p><p>4</p>")),
            Is.EqualTo(Pdf(Doc(".x{color:#c00}", "<p>1</p><p data-k=1>2</p><p class=x>3</p><p class=x>4</p>"))));
    }

    [Test]
    public void SiblingsOfTableCellsMatch()
    {
        Assert.That(Pdf(Doc("td + td{color:#c00}", "<table><tr><td>1</td><td>2</td><td>3</td></tr></table>")),
            Is.EqualTo(Pdf(Doc(".x{color:#c00}", "<table><tr><td>1</td><td class=x>2</td><td class=x>3</td></tr></table>"))));
    }

    [Test]
    public void ABeforePseudoElementTakesTheSiblingConditionOfItsElement()
    {
        Assert.That(Pdf(Doc("p + p::before{content:'> ';color:#c00}", "<p>1</p><p>2</p>")),
            Is.EqualTo(Pdf(Doc(".x::before{content:'> ';color:#c00}", "<p>1</p><p class=x>2</p>"))));
    }

    [TestCase("div + {color:red}")]
    [TestCase("+ div{color:red}")]
    [TestCase("div ++ p{color:red}")]
    [TestCase("div ~~ p{color:red}")]
    [TestCase("div + > p{color:red}")]
    [TestCase("div > ~ p{color:red}")]
    [TestCase("p:not(a + b){color:red}")]
    public void MalformedCombinatorSequencesStayInvalid(string css)
    {
        var ex = Assert.Throws<FactsPdfException>(() => Pdf(Doc(css, "<p>x</p>")))!;
        Assert.That(ex.Code, Is.EqualTo("FPDF1203"));
    }

    [Test]
    public void MatchingWorkIsBoundedByTheSelectorBudget()
    {
        // A thousand siblings and a selector that scans all earlier ones for every element: quadratic
        // work, which the match operation budget stops with its own code instead of running unbounded.
        var body = "<div>d</div>" + string.Concat(Enumerable.Repeat("<p>x</p>", 1000));
        var options = new PdfOptions { PageWidth = 300, PageHeight = 400, Margin = 20, MaxCssMatchOperations = 20_000 };
        var ex = Assert.Throws<FactsPdfException>(() => Pdf(Doc("span ~ p ~ p ~ p ~ p{color:red}", body), options))!;
        Assert.That(ex.Code, Is.EqualTo("FPDF1205"));
        Assert.DoesNotThrow(() => Pdf(Doc("div ~ p{color:red}", "<div>d</div><p>x</p>"), options));
    }

    [Test]
    public void SiblingsInsideAnSvgMatchLikeHtmlSiblings()
    {
        // display on svg content is an explicit failure, so it shows which rect the selector reached.
        const string two = "<svg width='20' height='20'><rect width='5' height='5'/><rect width='5' height='5'/></svg>";
        const string apart = "<svg width='20' height='20'><rect width='5' height='5'/><circle r='2'/><rect width='5' height='5'/></svg>";
        Assert.That(Assert.Throws<FactsPdfException>(() => Pdf(Doc("rect + rect{display:none}", "<p>x</p>" + two)))!.Code, Is.EqualTo("FPDF1201"));
        Assert.DoesNotThrow(() => Pdf(Doc("rect + rect{display:none}", "<p>x</p>" + apart)));
        Assert.That(Assert.Throws<FactsPdfException>(() => Pdf(Doc("rect ~ rect{display:none}", "<p>x</p>" + apart)))!.Code, Is.EqualTo("FPDF1201"));
    }
}
