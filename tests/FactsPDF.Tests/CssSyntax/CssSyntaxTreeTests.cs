using FactsPDF.CssSyntax;
using NUnit.Framework;

namespace FactsPDF.Tests.CssSyntax;

[TestFixture]
public sealed class CssSyntaxTreeTests
{
    private static CssSyntaxResult Stylesheet(string css, CssSyntaxLimits? limits = null)
    {
        limits ??= new();
        return CssSyntaxParser.ParseStylesheet(CssSourceText.Create(css, 0, limits), limits);
    }

    private static CssSyntaxResult Declarations(string css, CssSyntaxLimits? limits = null)
    {
        limits ??= new();
        return CssSyntaxParser.ParseDeclarations(CssSourceText.Create(css, 0, limits), limits);
    }

    [Test]
    public void QualifiedRuleRetainsSelectorPreludeAndDeclarationContents()
    {
        var result = Stylesheet("p.note {color:red}");
        Assert.That(result.Diagnostics, Is.Empty);
        Assert.That(result.Rules, Has.Count.EqualTo(1));
        var rule = (CssQualifiedRuleNode)result.Rules[0];
        Assert.That(rule.Prelude.OfType<CssTokenComponent>().First().Token.Value, Is.EqualTo("p"));
        Assert.That(rule.Contents, Has.Count.EqualTo(1));
        Assert.That(((CssDeclarationNode)rule.Contents[0]).Name, Is.EqualTo("color"));
    }

    [Test]
    public void NestedFunctionValuesPreserveStructureWithoutSemanticEvaluation()
    {
        var parsed = Declarations("width:calc(100% - var(--x, 2px));");
        Assert.That(parsed.Diagnostics, Is.Empty);
        var declaration = parsed.Declarations.Single();
        Assert.That(declaration.Name, Is.EqualTo("width"));
        var calc = declaration.Values.OfType<CssFunctionComponent>().Single();
        Assert.That(calc.Name, Is.EqualTo("calc"));
        var vari = calc.Values.OfType<CssFunctionComponent>().Single();
        Assert.That(vari.Name, Is.EqualTo("var"));
        Assert.That(vari.Values.OfType<CssTokenComponent>().Any(x => x.Token.Value == "--x"), Is.True);
    }

    [Test]
    public void FutureAtRuleIsValidSyntaxAndLaterQualifiedRuleStillParses()
    {
        var parsed = Stylesheet("@media print { p{color:red} } p{color:blue}");
        Assert.That(parsed.Diagnostics, Is.Empty);
        Assert.That(parsed.Rules, Has.Count.EqualTo(2));
        Assert.That(((CssAtRuleNode)parsed.Rules[0]).Name, Is.EqualTo("media"));
        Assert.That(((CssAtRuleNode)parsed.Rules[0]).Contents, Has.Count.EqualTo(1));
        Assert.That(parsed.Rules[1], Is.TypeOf<CssQualifiedRuleNode>());
    }

    [Test]
    public void RuleBodyKeepsDeclarationsAndNestedRulesInOriginalOrder()
    {
        var parsed = Stylesheet("p{color:red; & span{color:blue} background:white}");
        Assert.That(parsed.Diagnostics, Is.Empty);
        var body = ((CssQualifiedRuleNode)parsed.Rules.Single()).Contents;
        Assert.That(body.Select(x => x.GetType()), Is.EqualTo(new[]
        { typeof(CssDeclarationNode), typeof(CssQualifiedRuleNode), typeof(CssDeclarationNode) }));
        Assert.That(((CssDeclarationNode)body[2]).Name, Is.EqualTo("background"));
    }

    [Test]
    public void DeclarationImportantOnlyMatchesTerminalTopLevelMarker()
    {
        var parsed = Declarations("color:red !important; x:var(--a, !important); content:'!important'");
        Assert.That(parsed.Diagnostics, Is.Empty);
        Assert.That(parsed.Declarations.Select(x => x.Important), Is.EqualTo(new[] { true, false, false }));
    }

    [Test]
    public void CustomPropertyNamesRemainCaseSensitiveAndValuesKeepFunctions()
    {
        var parsed = Declarations("--Theme:var(--x, red); --theme:blue");
        Assert.That(parsed.Diagnostics, Is.Empty);
        Assert.That(parsed.Declarations.Select(d => d.Name), Is.EqualTo(new[] { "--Theme", "--theme" }));
        Assert.That(parsed.Declarations[0].Values.OfType<CssFunctionComponent>().Single().Name, Is.EqualTo("var"));
    }

    [Test]
    public void MalformedDeclarationRecoversBeforeNextDeclaration()
    {
        var parsed = Declarations("bad value; color:blue");
        Assert.That(parsed.Declarations.Select(d => d.Name), Does.Contain("color"));
        Assert.That(parsed.Diagnostics, Is.Not.Empty);
        Assert.That(parsed.Diagnostics[0].Span.Start, Is.EqualTo(0));
    }

    [Test]
    public void UnterminatedBlockReturnsPartialTreeAndDiagnostic()
    {
        var parsed = Stylesheet("p{color:red");
        Assert.That(parsed.Rules, Has.Count.EqualTo(1));
        Assert.That(parsed.Diagnostics, Is.Not.Empty);
        Assert.That(parsed.Diagnostics.Any(d => d.Message.Contains("Unclosed", StringComparison.OrdinalIgnoreCase)), Is.True);
    }

    [Test]
    public void BadUrlProducesDiagnosticButNextDeclarationIsStillPresent()
    {
        var parsed = Stylesheet("p{background:url(a b);color:red}");
        var body = ((CssQualifiedRuleNode)parsed.Rules.Single()).Contents;
        Assert.That(body.OfType<CssDeclarationNode>().Any(d => d.Name == "color"), Is.True);
        Assert.That(parsed.Diagnostics, Is.Not.Empty);
    }

    [Test]
    public void NestingBeyondConfiguredDepthThrowsResourceLimit()
    {
        var css = "x:" + string.Concat(Enumerable.Repeat("calc(", 65)) + "1" + new string(')', 65);
        Assert.Throws<CssSyntaxLimitException>(() => Declarations(css));
    }

    [Test]
    public void UnmatchedClosingBraceIsDiagnosedAndFollowingRuleCanParse()
    {
        var parsed = Stylesheet("p{color:red}}q{color:blue}");
        Assert.That(parsed.Rules, Has.Count.EqualTo(2));
        Assert.That(parsed.Diagnostics, Is.Not.Empty);
    }

    [Test]
    public void ParseCancellationAppliesToLongMalformedCss()
    {
        var limits = new CssSyntaxLimits(Cancellation: new CancellationToken(true));
        Assert.Throws<OperationCanceledException>(() => Stylesheet("p{color:red", limits));
    }
}
