using FactsPDF.CssSyntax;
using NUnit.Framework;

namespace FactsPDF.Tests.CssSyntax;

[TestFixture]
public sealed class CssSyntaxStressTests
{
    private static CssSyntaxResult Parse(string css, CssSyntaxLimits? limits = null)
    {
        limits ??= new CssSyntaxLimits();
        return CssSyntaxParser.ParseStylesheet(CssSourceText.Create(css, 0, limits), limits);
    }

    [Test]
    public void NestedAtRuleEndsBeforeParentsClosingBrace()
    {
        // CSS Syntax Level 3: a nested at-rule without a semicolon or its own block
        // ends at the parent's closing brace (which must remain unconsumed).
        var parsed = Parse("p{@future x}q{color:blue}");
        Assert.That(parsed.Diagnostics, Is.Empty);
        Assert.That(parsed.Rules, Has.Count.EqualTo(2));
        var first = (CssQualifiedRuleNode)parsed.Rules[0];
        Assert.That(first.Contents, Has.Count.EqualTo(1));
        Assert.That(first.Contents[0], Is.TypeOf<CssAtRuleNode>());
        Assert.That(((CssAtRuleNode)first.Contents[0]).Name, Is.EqualTo("future"));
        Assert.That(parsed.Rules[1], Is.TypeOf<CssQualifiedRuleNode>());
    }

    [Test]
    public void NestedAtRuleWithOwnBlockLeavesNextDeclarationInParent()
    {
        var parsed = Parse("p{@future test{q{color:red}}color:blue}h1{color:green}");
        Assert.That(parsed.Diagnostics, Is.Empty);
        Assert.That(parsed.Rules, Has.Count.EqualTo(2));
        Assert.That(((CssQualifiedRuleNode)parsed.Rules[0]).Contents
            .Select(x => x.GetType()), Is.EqualTo(new[]
            { typeof(CssAtRuleNode), typeof(CssDeclarationNode) }));
    }

    [Test]
    public void ExcessiveComponentNodesFailWithinConfiguredBudget()
    {
        var limits = new CssSyntaxLimits(MaxNodes: 3);
        Assert.Throws<CssSyntaxLimitException>(() => Parse("p{color:red;color:blue;color:red;color:blue}", limits));
    }

    [Test]
    public void DeepFunctionsFailBeforeStackExhaustion()
    {
        var css = "p{x:" + string.Concat(Enumerable.Repeat("calc(", 65)) + "1" +
            new string(')', 65) + "}";
        Assert.Throws<CssSyntaxLimitException>(() => Parse(css));
    }

    [Test]
    public void VeryLargeExponentsRemainSyntaxTokensRatherThanCrashing()
    {
        var limits = new CssSyntaxLimits();
        var value = CssSyntaxTokenizer.Tokenize(
            CssSourceText.Create("1e999999999999999px", 0, limits), limits)[0];
        Assert.That(value.Kind, Is.EqualTo(CssSyntaxTokenKind.Dimension));
        Assert.That(value.Value, Is.EqualTo("1e999999999999999"));
        Assert.That(value.Unit, Is.EqualTo("px"));
    }

    [Test]
    public void ParsingSameInputConcurrentlyProducesIndependentTrees()
    {
        const string css = "p{@media print{q{color:red}}color:blue}";
        Parallel.For(0, 24, _ =>
        {
            var parsed = Parse(css);
            Assert.That(parsed.Diagnostics, Is.Empty);
            Assert.That(parsed.Rules, Has.Count.EqualTo(1));
            Assert.That(((CssQualifiedRuleNode)parsed.Rules[0]).Contents,
                Has.Count.EqualTo(2));
        });
    }

    [Test]
    public void PreCancelledParsingNeverReturnsPartialTree()
    {
        var limits = new CssSyntaxLimits(Cancellation: new CancellationToken(true));
        Assert.Throws<OperationCanceledException>(() => Parse("p{color:red}", limits));
    }
}
