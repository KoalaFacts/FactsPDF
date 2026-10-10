using FactsPDF.CssSyntax;
using NUnit.Framework;

namespace FactsPDF.Tests.CssSyntax;

/// <summary>
/// Independent Codex review of PR #7 at 9d25f0835a identified these
/// standards and compatibility cases. Tests were authored before fixes.
/// </summary>
[TestFixture]
public sealed class CssIndependentReviewTests
{
    private static CssSyntaxResult Sheet(string css)
    {
        var limits = new CssSyntaxLimits();
        return CssSyntaxParser.ParseStylesheet(CssSourceText.Create(css, 0, limits), limits);
    }
    private static CssSyntaxResult Inline(string css)
    {
        var limits = new CssSyntaxLimits();
        return CssSyntaxParser.ParseDeclarations(CssSourceText.Create(css, 0, limits), limits);
    }

    [Test]
    public void LeadingSemicolonCannotDisappearFromQualifiedRulePrelude()
    {
        var syntax = Sheet(";p{color:red}");
        Assert.That(syntax.Rules, Has.Count.EqualTo(1));
        var rule = (CssQualifiedRuleNode)syntax.Rules[0];
        Assert.That(rule.Prelude[0], Is.TypeOf<CssTokenComponent>());
        Assert.That(((CssTokenComponent)rule.Prelude[0]).Token.Kind,
            Is.EqualTo(CssSyntaxTokenKind.Semicolon));
        using var result = new MemoryStream();
        var ex = Assert.Throws<FactsPdfException>(() =>
            PdfConverter.Convert("<style>;p{color:red}</style><p>A</p>", result));
        Assert.That(ex!.Code, Is.EqualTo("FPDF1203"));
        Assert.That(result.Length, Is.Zero);
    }

    [Test]
    public void UnterminatedCommentIsReportedAsCoreRecoveryDiagnostic()
    {
        var syntax = Sheet("p{color:red}/*unfinished");
        Assert.That(syntax.Rules, Has.Count.EqualTo(1));
        Assert.That(syntax.Diagnostics.Any(x =>
            x.Message.Contains("comment", StringComparison.OrdinalIgnoreCase)), Is.True);
    }

    [Test]
    public void UnknownAtRulePreservesRawTokensAndDiagnosesInvalidBlockContents()
    {
        var syntax = Sheet("@future { 1 }");
        Assert.That(syntax.Diagnostics, Is.Not.Empty);
        Assert.That(syntax.Rules, Has.Count.EqualTo(1));
        var rule = (CssAtRuleNode)syntax.Rules[0];
        Assert.That(rule.Name, Is.EqualTo("future"));
        Assert.That(rule.RawBlock, Is.Not.Null);
        Assert.That(rule.RawBlock!.OfType<CssTokenComponent>()
            .Any(c => c.Token.Kind == CssSyntaxTokenKind.Number &&
                      c.Token.Value == "1"), Is.True);
    }

    [TestCase('\u0080')]
    [TestCase('\u00AD')]
    [TestCase('\u202E')]
    [TestCase('\u2060')]
    public void ExcludedUnescapedNonAsciiCodePointIsNotAnIdentifier(char forbidden)
    {
        var limits = new CssSyntaxLimits();
        var tokens = CssSyntaxTokenizer.Tokenize(
            CssSourceText.Create("." + forbidden + "{color:red}", 0, limits), limits);
        Assert.That(tokens[0].Kind, Is.EqualTo(CssSyntaxTokenKind.Delim));
        Assert.That(tokens[1].Kind, Is.EqualTo(CssSyntaxTokenKind.Delim));
    }

    [TestCase('\u00B7')]
    [TestCase('\u00C0')]
    [TestCase('\u200C')]
    [TestCase('\u203F')]
    [TestCase('\u2070')]
    public void AllowedNonAsciiIdentifierCodepointsAreAccepted(char allowed)
    {
        var limits = new CssSyntaxLimits();
        var tokens = CssSyntaxTokenizer.Tokenize(
            CssSourceText.Create("." + allowed + "{color:red}", 0, limits), limits);
        Assert.That(tokens[1].Kind, Is.EqualTo(CssSyntaxTokenKind.Ident));
        Assert.That(tokens[1].Value, Is.EqualTo(allowed.ToString()));
    }

    [Test]
    public void SupplementaryScalarStaysOneIdentifierRatherThanTwoSurrogates()
    {
        var limits = new CssSyntaxLimits();
        var codepoint = char.ConvertFromUtf32(0x20000);
        var tokens = CssSyntaxTokenizer.Tokenize(
            CssSourceText.Create("." + codepoint + "{}", 0, limits), limits);
        Assert.That(tokens[1].Kind, Is.EqualTo(CssSyntaxTokenKind.Ident));
        Assert.That(tokens[1].Value, Is.EqualTo(codepoint));
        Assert.That(tokens[1].Span.Length, Is.EqualTo(2));
    }

    [Test]
    public void SoleBraceBlockAfterColonIsADeclarationValue()
    {
        var syntax = Sheet("p{x:{a:b};color:red}");
        Assert.That(syntax.Diagnostics, Is.Empty);
        var body = ((CssQualifiedRuleNode)syntax.Rules.Single()).Contents;
        Assert.That(body.Select(x => x.GetType()),
            Is.EqualTo(new[] { typeof(CssDeclarationNode), typeof(CssDeclarationNode) }));
        var first = (CssDeclarationNode)body[0];
        Assert.That(first.Name, Is.EqualTo("x"));
        Assert.That(first.Values.OfType<CssBlockComponent>(), Has.Exactly(1).Items);
        Assert.That(((CssDeclarationNode)body[1]).Name, Is.EqualTo("color"));
    }

    [Test]
    public void InvalidNestedRuleEndsAtSemicolonLeavingLaterDeclarations()
    {
        var syntax = Sheet("p{.bad;color:red}");
        var body = ((CssQualifiedRuleNode)syntax.Rules.Single()).Contents;
        Assert.That(body.OfType<CssDeclarationNode>().Select(x => x.Name),
            Is.EqualTo(new[] { "color" }));
        Assert.That(syntax.Diagnostics, Is.Not.Empty);
    }

    [Test]
    public void MismatchedCloserInsideAnotherComponentBlockIsPreserved()
    {
        var syntax = Inline("x:[)]");
        Assert.That(syntax.Diagnostics, Is.Not.Empty, "Mismatched close token must be preserved AND diagnosed.");
        var block = syntax.Declarations.Single().Values.OfType<CssBlockComponent>().Single();
        Assert.That(block.Opening, Is.EqualTo(CssSyntaxTokenKind.OpenSquare));
        Assert.That(block.Values, Has.Count.EqualTo(1));
        Assert.That(((CssTokenComponent)block.Values[0]).Token.Kind,
            Is.EqualTo(CssSyntaxTokenKind.CloseParen));
    }
}
