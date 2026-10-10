namespace FactsPDF.CssSyntax;

/// <summary>Location in original UTF-16 code units, absolute when supplied by the caller.</summary>
internal readonly record struct CssSourceSpan(int Start, int Length);

internal sealed record CssSyntaxLimits(
    int MaxCharacters = 262_144,
    int MaxNodes = 131_072,
    int MaxDepth = 64,
    CancellationToken Cancellation = default);

internal sealed class CssSyntaxLimitException(string reason, CssSourceSpan span) : Exception(reason)
{
    internal CssSourceSpan Span { get; } = span;
}

internal sealed record CssSyntaxDiagnostic(
    string Code, string Message, CssSourceSpan Span, string Recovery);

internal enum CssSyntaxTokenKind
{
    Ident, Function, AtKeyword, Hash, String, BadString, Url, BadUrl,
    Delim, Number, Percentage, Dimension, Whitespace, Cdo, Cdc,
    Colon, Semicolon, Comma, OpenSquare, CloseSquare,
    OpenParen, CloseParen, OpenBrace, CloseBrace, Eof
}

internal readonly record struct CssSyntaxToken(
    CssSyntaxTokenKind Kind, string Value, string Raw, CssSourceSpan Span,
    bool IsInteger = false, bool HashIsId = false, string? Unit = null);
