using FactsPDF.CssSyntax;
using NUnit.Framework;

namespace FactsPDF.Tests.CssSyntax;

[TestFixture]
public sealed class CssStringUrlTokenTests
{
    private static CssSyntaxToken[] Tokens(string css)
    {
        var limits = new CssSyntaxLimits();
        return CssSyntaxTokenizer.Tokenize(CssSourceText.Create(css, 0, limits), limits)
            .Where(t => t.Kind is not (CssSyntaxTokenKind.Whitespace or CssSyntaxTokenKind.Eof)).ToArray();
    }

    [Test]
    public void QuotedCssStringsDecodeEscapedQuotes()
    {
        var tokens = Tokens("'a\\'b'");
        Assert.That(tokens.Length, Is.EqualTo(1));
        Assert.That(tokens[0].Kind, Is.EqualTo(CssSyntaxTokenKind.String));
        Assert.That(tokens[0].Value, Is.EqualTo("a'b"));
    }

    [Test]
    public void EscapedNewlineInCssStringDoesNotProduceContentOrBadString()
    {
        var tokens = Tokens("\"a" + "\\" + "\n" + "b\"");
        Assert.That(tokens.Length, Is.EqualTo(1));
        Assert.That(tokens[0].Kind, Is.EqualTo(CssSyntaxTokenKind.String));
        Assert.That(tokens[0].Value, Is.EqualTo("ab"));
    }

    [Test]
    public void RawAndQuotedUrlsHaveDifferentTokenKinds()
    {
        var plain = Tokens("url(test.png)");
        Assert.That(plain.Length, Is.EqualTo(1));
        Assert.That(plain[0].Kind, Is.EqualTo(CssSyntaxTokenKind.Url));
        Assert.That(plain[0].Value, Is.EqualTo("test.png"));
        Assert.That(plain[0].Raw, Is.EqualTo("url(test.png)"));

        var quoted = Tokens("url('a b')");
        Assert.That(quoted.Select(t => t.Kind), Is.EqualTo(new[]
        {
            CssSyntaxTokenKind.Function, CssSyntaxTokenKind.String, CssSyntaxTokenKind.CloseParen
        }));
        Assert.That(quoted[0].Value, Is.EqualTo("url"));
        Assert.That(quoted[1].Value, Is.EqualTo("a b"));
    }

    [Test]
    public void BadUrlRecoversBeforeNextValidCssRule()
    {
        var tokens = Tokens("url(a b) p{color:red}");
        Assert.That(tokens[0].Kind, Is.EqualTo(CssSyntaxTokenKind.BadUrl));
        Assert.That(tokens[1].Kind, Is.EqualTo(CssSyntaxTokenKind.Ident));
        Assert.That(tokens[1].Value, Is.EqualTo("p"));
        Assert.That(tokens[2].Kind, Is.EqualTo(CssSyntaxTokenKind.OpenBrace));
    }

    [Test]
    public void BadStringReconsumesNewlineAndLaterTokensRemainAvailable()
    {
        var tokens = Tokens("'bad\np{color:red}");
        Assert.That(tokens[0].Kind, Is.EqualTo(CssSyntaxTokenKind.BadString));
        Assert.That(tokens[0].Value, Is.EqualTo("bad"));
        Assert.That(tokens[1].Kind, Is.EqualTo(CssSyntaxTokenKind.Ident));
        Assert.That(tokens[1].Value, Is.EqualTo("p"));
    }

    [Test]
    public void EscapedRightParenthesisIsNotAUrlTerminator()
    {
        var tokens = Tokens("url(a\\)b)");
        Assert.That(tokens[0].Kind, Is.EqualTo(CssSyntaxTokenKind.Url));
        Assert.That(tokens[0].Value, Is.EqualTo("a)b"));
    }

    [Test]
    public void UnterminatedUrlAtEofDoesNotHangOrFetchAnything()
    {
        var tokens = Tokens("url(https://example.invalid/file.css");
        Assert.That(tokens.Length, Is.EqualTo(1));
        Assert.That(tokens[0].Kind, Is.EqualTo(CssSyntaxTokenKind.Url));
        Assert.That(tokens[0].Value, Is.EqualTo("https://example.invalid/file.css"));
    }

    [Test]
    public void QuotedStringAtEofIsKeptAsAStringForParserDiagnostics()
    {
        var tokens = Tokens("\"unfinished");
        Assert.That(tokens.Length, Is.EqualTo(1));
        Assert.That(tokens[0].Kind, Is.EqualTo(CssSyntaxTokenKind.String));
        Assert.That(tokens[0].Value, Is.EqualTo("unfinished"));
    }
}
