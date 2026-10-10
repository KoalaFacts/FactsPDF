namespace FactsPDF;

// Selector-only compatibility tokens. ALL CSS source lexing uses FactsPDF.CssSyntax.
internal enum CssTokenKind { Identifier, Number, Hash, String, Symbol, End }

internal readonly record struct CssToken(CssTokenKind Kind, string Text, bool LeadingSpace, int Offset)
{
    internal bool Is(string text) => Text == text;
}

internal static class CssTokens
{
    internal static bool IdentifierStart(string text, int i)
        => i < text.Length && (char.IsAsciiLetter(text[i]) ||
             text[i] == '_' || text[i] == '-' && i + 1 < text.Length &&
             (char.IsAsciiLetter(text[i + 1]) || text[i + 1] is '_' or '-'));

    internal static FactsPdfException Invalid(string message, int offset)
        => new("FPDF1203", message, offset);
}
