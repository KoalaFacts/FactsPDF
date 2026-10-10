namespace FactsPDF.CssSyntax;

internal static class CssSyntaxTokenizer
{
    internal static IReadOnlyList<CssSyntaxToken> Tokenize(CssSourceText source, CssSyntaxLimits limits)
    {
        limits.Cancellation.ThrowIfCancellationRequested();
        return [new CssSyntaxToken(CssSyntaxTokenKind.Eof, "", "", source.Span(source.Normalized.Length, 0))];
    }
}
