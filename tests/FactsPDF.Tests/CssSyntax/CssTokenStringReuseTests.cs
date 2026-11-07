using FactsPDF.CssSyntax;
using NUnit.Framework;

namespace FactsPDF.Tests.CssSyntax;

// The tokenizer returns an escape-free name as its source slice, reuses that string as the raw
// text, shares one-character ASCII strings and keeps one token list per thread. None of this may
// change a token's kind, value, raw text or span.
[TestFixture]
public sealed class CssTokenStringReuseTests
{
    private static CssSyntaxToken[] Tokens(string css)
    {
        var limits = new CssSyntaxLimits();
        return [.. CssSyntaxTokenizer.Tokenize(CssSourceText.Create(css, 0, limits), limits)];
    }

    [Test]
    public void AnEscapeAfterPlainCharactersDecodesTheWholeName()
    {
        var token = Tokens("pad\\64 ing")[0];
        Assert.That(token.Kind, Is.EqualTo(CssSyntaxTokenKind.Ident));
        Assert.That(token.Value, Is.EqualTo("padding"));
        Assert.That(token.Raw, Is.EqualTo("pad\\64 ing"));
        Assert.That(token.Span, Is.EqualTo(new CssSourceSpan(0, 10)));
    }

    [Test]
    public void SurrogatePairNamesKeepTheirSourceText()
    {
        var token = Tokens("a\U0001F600b:1")[0];
        Assert.That(token.Kind, Is.EqualTo(CssSyntaxTokenKind.Ident));
        Assert.That(token.Value, Is.EqualTo("a\U0001F600b"));
        Assert.That(token.Raw, Is.EqualTo("a\U0001F600b"));
    }

    [Test]
    public void EscapeFreeIdentifiersAndNumbersShareValueAndRaw()
    {
        var tokens = Tokens("padding:3 12.5pt;x");
        Assert.That(tokens.Select(t => (t.Kind, t.Value, t.Raw)), Is.EqualTo(new[]
        {
            (CssSyntaxTokenKind.Ident, "padding", "padding"),
            (CssSyntaxTokenKind.Colon, ":", ":"),
            (CssSyntaxTokenKind.Number, "3", "3"),
            (CssSyntaxTokenKind.Whitespace, " ", " "),
            (CssSyntaxTokenKind.Dimension, "12.5", "12.5pt"),
            (CssSyntaxTokenKind.Semicolon, ";", ";"),
            (CssSyntaxTokenKind.Ident, "x", "x"),
            (CssSyntaxTokenKind.Eof, "", "")
        }));
        Assert.That(tokens[0].Raw, Is.SameAs(tokens[0].Value));
        Assert.That(tokens[4].Unit, Is.EqualTo("pt"));
        Assert.That(tokens[1].Raw, Is.SameAs(Tokens("a:b")[1].Raw), "one-character punctuation is shared");
    }

    [Test]
    public void TheReusedTokenListNeverCarriesTokensIntoTheNextResult()
    {
        var large = string.Join(';', Enumerable.Range(0, 3000).Select(i => $"a{i}:b"));
        Assert.That(Tokens(large), Has.Length.EqualTo(3000 * 4));
        var small = Tokens("x");
        Assert.That(small.Select(t => t.Kind), Is.EqualTo(new[] { CssSyntaxTokenKind.Ident, CssSyntaxTokenKind.Eof }));
        var medium = Tokens("a b");
        Assert.That(medium.Select(t => t.Value), Is.EqualTo(new[] { "a", " ", "b", "" }));
        Assert.That(Tokens("x"), Has.Length.EqualTo(2));
    }

    [Test]
    public void AFailedTokenizationDoesNotLeakTokensIntoTheNextOne()
    {
        var limits = new CssSyntaxLimits();
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var cancelledLimits = limits with { Cancellation = cancelled.Token };
        Assert.Throws<OperationCanceledException>(() =>
            CssSyntaxTokenizer.Tokenize(CssSourceText.Create("a b c d", 0, limits), cancelledLimits));
        Assert.That(Tokens("z").Select(t => t.Value), Is.EqualTo(new[] { "z", "" }));
    }

    [Test]
    public void UnterminatedCommentsStillReportTheirDiagnostic()
    {
        var limits = new CssSyntaxLimits();
        var result = CssSyntaxTokenizer.TokenizeWithDiagnostics(CssSourceText.Create("a /* open", 0, limits), limits);
        Assert.That(result.Diagnostics, Has.Count.EqualTo(1));
        Assert.That(CssSyntaxTokenizer.TokenizeWithDiagnostics(CssSourceText.Create("a", 0, limits), limits).Diagnostics, Is.Empty);
    }

    [Test]
    public void SourceWithoutCharactersToNormalizeIsUsedAsIsWithItsOriginalOffsets()
    {
        var limits = new CssSyntaxLimits();
        const string Css = "a:b";
        var source = CssSourceText.Create(Css, 0, limits, [10, 11, 15, 16]);
        Assert.That(source.Normalized, Is.SameAs(Css));
        Assert.That(source.Span(1, 2), Is.EqualTo(new CssSourceSpan(11, 5)));
        Assert.That(CssSourceText.Create(Css, 40, limits).Span(0, 3), Is.EqualTo(new CssSourceSpan(40, 3)));
    }

    [Test]
    public void SourceNeedingNormalizationIsStillNormalized()
    {
        var limits = new CssSyntaxLimits();
        var source = CssSourceText.Create("a\r\nb\fc\0d\uD800e\U0001F600", 5, limits);
        Assert.That(source.Normalized, Is.EqualTo("a\nb\nc\uFFFDd\uFFFDe\U0001F600"));
        Assert.That(source.Span(1, 1), Is.EqualTo(new CssSourceSpan(6, 2)), "CRLF is one normalized newline");
        Assert.That(source.Span(2, 1), Is.EqualTo(new CssSourceSpan(8, 1)));
    }

    [Test]
    public void ThePooledTokenListHoldsNoTokensBetweenTokenizations()
    {
        // The reused list must not keep a finished document's token strings alive.
        Tokens("a { color: red } " + new string(' ', 1000) + "b");
        var field = typeof(CssSyntaxTokenizer).GetField("pooledTokens",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var pooled = (List<CssSyntaxToken>?)field.GetValue(null);
        Assert.That(pooled, Is.Not.Null);
        Assert.That(pooled!, Is.Empty);
    }
}
