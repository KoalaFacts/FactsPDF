using FactsPDF.CssSyntax;
using NUnit.Framework;

namespace FactsPDF.Tests.CssSyntax;

[TestFixture]
public sealed class CssIdentifierTokenTests
{
    private static CssSyntaxToken[] Tokens(string css)
    {
        var limits = new CssSyntaxLimits();
        return CssSyntaxTokenizer.Tokenize(CssSourceText.Create(css, 0, limits), limits)
            .Where(token => token.Kind is not (CssSyntaxTokenKind.Whitespace or CssSyntaxTokenKind.Eof)).ToArray();
    }

    [Test]
    public void CommentsPreserveAdjacentIdentifierBoundaries()
    {
        var tokens = Tokens("p/**/span");
        Assert.That(tokens.Select(t => (t.Kind, t.Value)), Is.EqualTo(new[]
        {
            (CssSyntaxTokenKind.Ident, "p"), (CssSyntaxTokenKind.Ident, "span")
        }));
    }

    [Test]
    public void EscapedHexSequencesAndUnicodeIdentifiersAreDecoded()
    {
        var tokens = Tokens("café \\31 23");
        Assert.That(tokens.Length, Is.EqualTo(2));
        Assert.That(tokens[0].Kind, Is.EqualTo(CssSyntaxTokenKind.Ident));
        Assert.That(tokens[0].Value, Is.EqualTo("café"));
        Assert.That(tokens[1].Value, Is.EqualTo("123"));
        Assert.That(tokens[1].Raw, Is.EqualTo("\\31 23"));
        Assert.That(tokens[1].Span, Is.EqualTo(new CssSourceSpan(5, 6)));
    }

    [Test]
    public void HandlesHashTypeAtKeywordAndCustomPropertyCase()
    {
        var tokens = Tokens("#x #123 @media --Theme");
        Assert.That(tokens.Select(t => t.Kind), Is.EqualTo(new[]
        {
            CssSyntaxTokenKind.Hash, CssSyntaxTokenKind.Hash,
            CssSyntaxTokenKind.AtKeyword, CssSyntaxTokenKind.Ident
        }));
        Assert.That(tokens[0].HashIsId, Is.True);
        Assert.That(tokens[1].HashIsId, Is.False);
        Assert.That(tokens[2].Value, Is.EqualTo("media"));
        Assert.That(tokens[3].Value, Is.EqualTo("--Theme"));
    }

    [Test]
    public void InvalidEscapesBecomeReplacementCharactersNotUnpairedSurrogates()
    {
        var tokens = Tokens("\\110000  \\000000");
        Assert.That(tokens.Select(t => t.Value), Is.EqualTo(new[] { "\uFFFD", "\uFFFD" }));
        Assert.That(tokens.All(t => t.Kind == CssSyntaxTokenKind.Ident), Is.True);
    }

    [Test]
    public void IdentFollowedByOpenParenCreatesFunctionToken()
    {
        var tokens = Tokens("calc(12px)");
        Assert.That(tokens[0].Kind, Is.EqualTo(CssSyntaxTokenKind.Function));
        Assert.That(tokens[0].Value, Is.EqualTo("calc"));
        Assert.That(tokens[1].Kind, Is.EqualTo(CssSyntaxTokenKind.Dimension));
        Assert.That(tokens[2].Kind, Is.EqualTo(CssSyntaxTokenKind.CloseParen));
    }

    [Test]
    public void LoneBackslashEscapeAtEofIsRecoverable()
    {
        var tokens = Tokens("\\");
        Assert.That(tokens.Length, Is.EqualTo(1));
        Assert.That(tokens[0].Kind, Is.EqualTo(CssSyntaxTokenKind.Ident));
        Assert.That(tokens[0].Value, Is.EqualTo("\uFFFD"));
    }

    [Test]
    public void DimensionUnitCanUseCssIdentifierEscapes()
    {
        var tokens = Tokens("12\\70x");
        Assert.That(tokens[0].Kind, Is.EqualTo(CssSyntaxTokenKind.Dimension));
        Assert.That(tokens[0].Value, Is.EqualTo("12"));
        Assert.That(tokens[0].Unit, Is.EqualTo("px"));
    }
}
