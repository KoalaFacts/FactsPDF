using System.Globalization;
using FactsPDF.CssSyntax;
using NUnit.Framework;

namespace FactsPDF.Tests.CssSyntax;

[TestFixture]
public sealed class CssNumericTokenTests
{
    private static CssSyntaxToken[] Tokens(string css, int sourceOffset = 0)
    {
        var limits = new CssSyntaxLimits();
        var source = CssSourceText.Create(css, sourceOffset, limits);
        return CssSyntaxTokenizer.Tokenize(source, limits).ToArray();
    }
    private static CssSyntaxToken[] Meaningful(string css)
        => Tokens(css).Where(t => t.Kind is not (CssSyntaxTokenKind.Whitespace or CssSyntaxTokenKind.Eof)).ToArray();

    [Test]
    public void NumericTokensPreserveExponentUnitsAndNumberType()
    {
        var tokens = Meaningful("1e3px -0.5% +2.0 12px 4.25 0");
        Assert.That(tokens.Select(t => t.Kind), Is.EqualTo(new[]
        {
            CssSyntaxTokenKind.Dimension, CssSyntaxTokenKind.Percentage, CssSyntaxTokenKind.Number,
            CssSyntaxTokenKind.Dimension, CssSyntaxTokenKind.Number, CssSyntaxTokenKind.Number
        }));
        Assert.That(tokens[0].Value, Is.EqualTo("1e3"));
        Assert.That(tokens[0].Raw, Is.EqualTo("1e3px"));
        Assert.That(tokens[0].Unit, Is.EqualTo("px"));
        Assert.That(tokens[0].IsInteger, Is.False);
        Assert.That(tokens[1].Value, Is.EqualTo("-0.5"));
        Assert.That(tokens[2].Value, Is.EqualTo("+2.0"));
        Assert.That(tokens[3].IsInteger, Is.True);
        Assert.That(tokens[5].IsInteger, Is.True);
    }

    [Test]
    public void BasicPunctuationAndCdoCdcTokensAreDistinct()
    {
        var tokens = Meaningful("{ } ( ) [ ] : ; , > <!-- -->");
        Assert.That(tokens.Select(t => t.Kind), Is.EqualTo(new[]
        {
            CssSyntaxTokenKind.OpenBrace, CssSyntaxTokenKind.CloseBrace,
            CssSyntaxTokenKind.OpenParen, CssSyntaxTokenKind.CloseParen,
            CssSyntaxTokenKind.OpenSquare, CssSyntaxTokenKind.CloseSquare,
            CssSyntaxTokenKind.Colon, CssSyntaxTokenKind.Semicolon, CssSyntaxTokenKind.Comma,
            CssSyntaxTokenKind.Delim, CssSyntaxTokenKind.Cdo, CssSyntaxTokenKind.Cdc
        }));
        Assert.That(tokens[9].Value, Is.EqualTo(">"));
    }

    [Test]
    public void CommentsDoNotInsertWhitespaceOrJoinTokens()
    {
        var tokens = Tokens("1/**/2");
        Assert.That(tokens.Select(t => t.Kind), Is.EqualTo(new[]
        { CssSyntaxTokenKind.Number, CssSyntaxTokenKind.Number, CssSyntaxTokenKind.Eof }));
        Assert.That(tokens[0].Raw, Is.EqualTo("1"));
        Assert.That(tokens[1].Raw, Is.EqualTo("2"));
    }

    [Test]
    public void DecimalPointRequiresFollowingDigit()
    {
        var tokens = Meaningful("+2. .5e-2px");
        Assert.That(tokens.Select(t => t.Kind), Is.EqualTo(new[]
        { CssSyntaxTokenKind.Number, CssSyntaxTokenKind.Delim, CssSyntaxTokenKind.Dimension }));
        Assert.That(tokens[0].Value, Is.EqualTo("+2"));
        Assert.That(tokens[1].Value, Is.EqualTo("."));
        Assert.That(tokens[2].Value, Is.EqualTo(".5e-2"));
        Assert.That(tokens[2].Unit, Is.EqualTo("px"));
    }

    [Test]
    public void TokenSpansUseOriginalAbsoluteUtf16Offsets()
    {
        var tokens = Tokens("12px\r\n7%", 60);
        Assert.That(tokens[0].Span, Is.EqualTo(new CssSourceSpan(60, 4)));
        Assert.That(tokens[1].Kind, Is.EqualTo(CssSyntaxTokenKind.Whitespace));
        Assert.That(tokens[1].Span, Is.EqualTo(new CssSourceSpan(64, 2)));
        Assert.That(tokens[2].Kind, Is.EqualTo(CssSyntaxTokenKind.Percentage));
        Assert.That(tokens[2].Span, Is.EqualTo(new CssSourceSpan(66, 2)));
        Assert.That(tokens[^1].Kind, Is.EqualTo(CssSyntaxTokenKind.Eof));
        Assert.That(tokens[^1].Span, Is.EqualTo(new CssSourceSpan(68, 0)));
    }

    [Test]
    public void TokenizationDoesNotDependOnCurrentCulture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var expected = Tokens("1.2px -3.5%").Select(x => (x.Kind,x.Value,x.Unit)).ToArray();
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var actual = Tokens("1.2px -3.5%").Select(x => (x.Kind,x.Value,x.Unit)).ToArray();
            Assert.That(actual, Is.EqualTo(expected));
        }
        finally { CultureInfo.CurrentCulture = original; }
    }
}
