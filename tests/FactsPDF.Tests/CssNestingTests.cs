using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// CSS Nesting: nested style rules, the nesting selector (&amp;, written or implied), relative
/// selectors, nested conditional rules and declarations mixed with nested rules. The nesting selector
/// stands for the parent's selector list as :is() does, so a resolved selector carries the specificity
/// of the most specific parent selector. Each case renders byte-identical to the flat rules Chrome
/// resolves the nest to.
/// </summary>
[TestFixture]
public sealed class CssNestingTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 400, Margin = 20 };

    private static byte[] Pdf(string html)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, Page);
        return output.ToArray();
    }

    private static string Doc(string css, string body) => $"<style>{css}</style>{body}";

    private const string Body = "<div class=a><div class=b><p class=c>T</p></div><span class=d>S</span></div><p class=e>E</p>";

    [TestCase(".a{.b{color:#c00}}", ".a .b{color:#c00}", Description = "an implied descendant")]
    [TestCase(".a{& .b{color:#c00}}", ".a .b{color:#c00}")]
    [TestCase(".a{& > .b{color:#c00}}", ".a > .b{color:#c00}")]
    [TestCase(".a{> .b{color:#c00}}", ".a > .b{color:#c00}", Description = "a leading combinator")]
    [TestCase(".b{+ .d{color:#c00}}", ".b + .d{color:#c00}")]
    [TestCase(".b{~ .d{color:#c00}}", ".b ~ .d{color:#c00}")]
    [TestCase(".a{.b{.c{color:#c00}}}", ".a .b .c{color:#c00}", Description = "three levels")]
    [TestCase("div{&.a{color:#c00}}", "div.a{color:#c00}", Description = "merged into one compound")]
    [TestCase("div{&.b{color:#c00}}", "div.b{color:#c00}")]
    [TestCase(".b{.a &{color:#c00}}", ".a .b{color:#c00}", Description = "the nesting selector after another compound")]
    [TestCase(".c{.b &{color:#c00}}", ".b .c{color:#c00}")]
    [TestCase(".a{& .b &{color:#c00}}", ".a .b.a{color:#c00}", Description = "two nesting selectors")]
    [TestCase(".a,.e{& .c{color:#c00}}", ".a .c,.e .c{color:#c00}", Description = "a parent selector list")]
    [TestCase(".a{.b,.d{color:#c00}}", ".a .b,.a .d{color:#c00}", Description = "a nested selector list")]
    [TestCase("div > .b{& p{color:#c00}}", "div > .b p{color:#c00}", Description = "a parent with combinators, nesting selector first")]
    [TestCase("div > .b{& > p{color:#c00}}", "div > .b > p{color:#c00}")]
    [TestCase("div > .b{&.b p{color:#c00}}", "div > .b.b p{color:#c00}")]
    [TestCase(".a{&:not(.zz){color:#c00}}", ".a:not(.zz){color:#c00}")]
    [TestCase(".b{&:first-child p{color:#c00}}", ".b:first-child p{color:#c00}")]
    [TestCase(".b{p{color:#c00}}", ".b p{color:#c00}", Description = "a type selector is relative too")]
    public void ResolvesToTheFlatRules(string nested, string flat)
        => Assert.That(Pdf(Doc(nested, Body)), Is.EqualTo(Pdf(Doc(flat, Body))));

    [Test]
    public void TheNestingSelectorHasTheSpecificityOfTheMostSpecificParentSelector()
    {
        // :is(#x, .y) p is (1,0,1): it beats .k .k p (0,2,1) even for the element that is only .y.
        const string body = "<div class='y k'><div class=k><p>T</p></div></div>";
        Assert.That(Pdf(Doc("#x,.y{& p{color:#c00}} .k .k p{color:#00c}", body)),
            Is.EqualTo(Pdf(Doc("p{color:#c00}", body))));
        // Without a parent list the nest is (0,1,1), losing to .k .k p.
        Assert.That(Pdf(Doc(".y{& p{color:#c00}} .k .k p{color:#00c}", body)),
            Is.EqualTo(Pdf(Doc("p{color:#00c}", body))));
        // The implied nesting selector counts as well: .a .b is (0,2,0) however the rule is written.
        Assert.That(Pdf(Doc(".a{.b{color:#c00}} .b{color:#00c}", Body)), Is.EqualTo(Pdf(Doc(".b{color:#c00}", Body))));
    }

    [Test]
    public void DeclarationsFollowTheNestedRulesInTheCascadeOrder()
    {
        const string body = "<p class=o>T</p>";
        // After the nested & rule, the later declaration wins at equal specificity.
        Assert.That(Pdf(Doc(".o{color:#c00; & {color:#00c} color:#080;}", body)), Is.EqualTo(Pdf(Doc(".o{color:#080}", body))));
        Assert.That(Pdf(Doc(".o{color:#c00; &{color:#00c}}", body)), Is.EqualTo(Pdf(Doc(".o{color:#00c}", body))));
        Assert.That(Pdf(Doc(".o{& {color:#00c} color:#c00;}", body)), Is.EqualTo(Pdf(Doc(".o{color:#c00}", body))));
    }

    [Test]
    public void NestedConditionalRulesApplyToTheParentSelector()
    {
        Assert.That(Pdf(Doc(".a{@media print{.b{color:#c00}}}", Body)), Is.EqualTo(Pdf(Doc(".a .b{color:#c00}", Body))));
        Assert.That(Pdf(Doc(".c{@media print{color:#c00}}", Body)), Is.EqualTo(Pdf(Doc(".c{color:#c00}", Body))));
        Assert.That(Pdf(Doc(".c{@media all{& {color:#c00}}}", Body)), Is.EqualTo(Pdf(Doc(".c{color:#c00}", Body))));
        Assert.That(Pdf(Doc("@media print{.a{.b{color:#c00}}}", Body)), Is.EqualTo(Pdf(Doc(".a .b{color:#c00}", Body))));
        Assert.That(Pdf(Doc("@media print{@media all{.e{color:#c00}}}", Body)), Is.EqualTo(Pdf(Doc(".e{color:#c00}", Body))));
        // A layer inside a style rule puts the nested declarations in the layer.
        Assert.That(Pdf(Doc(".c{@layer l{color:#c00}} p{color:#00c}", Body)), Is.EqualTo(Pdf(Doc("p{color:#00c}", Body))));
        Assert.That(Pdf(Doc(".c{@layer l{color:#c00}}", Body)), Is.EqualTo(Pdf(Doc(".c{color:#c00}", Body))));
    }

    [Test]
    public void PseudoElementsAndSiblingsNest()
    {
        const string body = "<div class=a>D</div><p class=b>P</p>";
        Assert.That(Pdf(Doc(".b{&::before{content:'> ';color:#c00}}", body)), Is.EqualTo(Pdf(Doc(".b::before{content:'> ';color:#c00}", body))));
        Assert.That(Pdf(Doc(".a{& + p{color:#c00}}", body)), Is.EqualTo(Pdf(Doc(".a + p{color:#c00}", body))));
        Assert.That(Pdf(Doc(".a{+ p::after{content:' <';color:#c00}}", body)), Is.EqualTo(Pdf(Doc(".a + p::after{content:' <';color:#c00}", body))));
    }

    [Test]
    public void ATopLevelNestingSelectorIsTheRootElement()
        => Assert.That(Pdf(Doc("&{color:#c00}", Body)), Is.EqualTo(Pdf(Doc(":root{color:#c00}", Body))));

    [Test]
    public void RulesInsideAnInvalidNestedSelectorAreDroppedOnlyForThatRule()
    {
        var html = Doc(".a{.b{color:#c00} ..y{color:#00c} .d{color:#080}}", Body);
        Assert.That(Assert.Throws<FactsPdfException>(() => Pdf(html))!.Code, Is.EqualTo("FPDF1203"));
        Assert.That(FactsPDF.Pdf.Write().WithPage(p => p.Size(300, 400).Margin(20)).WithStrict(false).ToBytes(html),
            Is.EqualTo(FactsPDF.Pdf.Write().WithPage(p => p.Size(300, 400).Margin(20)).ToBytes(Doc(".a .b{color:#c00} .a .d{color:#080}", Body))));
    }

    [TestCase("div > .b{.x &{color:red}}", Description = "a parent with combinators outside the first compound")]
    [TestCase("div > .b{& + &{color:red}}")]
    [TestCase("div{span&{color:red}}", Description = "a type selector before the nesting selector")]
    [TestCase(".a{&:not(&){color:red}}", Description = "the nesting selector inside :not()")]
    [TestCase("div{&p{color:red}}")]
    [TestCase("p::before{.x{color:red}}", Description = "a nested rule under a pseudo-element")]
    public void FormsThatNeedComplexIsMatchingFailExplicitly(string css)
        => Assert.That(Assert.Throws<FactsPdfException>(() => Pdf(Doc(css, Body)))!.Code, Is.EqualTo("FPDF1203"));

    [Test]
    public void ManyNestingSelectorsAreBoundedByTheSelectorBudget()
    {
        var css = ".a,.b,.c,.d,.e,.f,.g,.h{& & & & & & & {color:red}}";
        Assert.That(Assert.Throws<FactsPdfException>(() => Pdf(Doc(css, Body)))!.Code, Is.EqualTo("FPDF1205"));
    }

    [Test]
    public void NestedAtRulesOtherThanMediaSupportsAndLayerFail()
        => Assert.That(Assert.Throws<FactsPdfException>(() => Pdf(Doc(".a{@container (min-width:1px){color:red}}", Body)))!.Code, Is.EqualTo("FPDF1203"));
}
