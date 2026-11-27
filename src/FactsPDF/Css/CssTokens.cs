using FactsPDF.CssSyntax;

namespace FactsPDF;

// Selector-only compatibility tokens. ALL CSS source lexing uses FactsPDF.CssSyntax.
internal enum CssTokenKind { Identifier, Number, Hash, String, Symbol, Function, End }

// A Function token keeps its name in Text and its unparsed arguments; selectors parse them
// themselves (An+B for :nth-child(), a compound list for :not()).
internal readonly record struct CssToken(CssTokenKind Kind, string Text, bool LeadingSpace, int Offset,
    IReadOnlyList<CssSyntaxComponent>? Arguments = null)
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
