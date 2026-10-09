namespace FactsPDF;

internal enum CssTokenKind { Identifier, Number, Hash, String, Symbol, End }
internal readonly record struct CssToken(CssTokenKind Kind, string Text, bool LeadingSpace, int Offset)
{
    internal bool Is(string text) => Text == text;
}

/// <summary>A strict lexer for the documented subset. Comments are token boundaries, not whitespace.</summary>
internal static class CssTokens
{
    internal static List<CssToken> Read(string css, int sourceOffset, CancellationToken token)
    {
        var result = new List<CssToken>(); var i = 0; var space = false;
        while (i < css.Length)
        {
            token.ThrowIfCancellationRequested();
            if (HtmlTokens.Space(css[i])) { space = true; i++; continue; }
            if (css[i] == '/' && i + 1 < css.Length && css[i + 1] == '*')
            {
                var end = css.IndexOf("*/", i + 2, StringComparison.Ordinal);
                if (end < 0) throw Invalid("Unterminated CSS comment.", sourceOffset + i);
                i = end + 2; continue;
            }
            var start = i; var c = css[i]; CssTokenKind kind;
            if (c == '\\' || c > 127 || c == '\0') throw Invalid("CSS escapes and non-ASCII syntax are not supported in this increment.", sourceOffset + i);
            if (c is '\'' or '"')
            {
                kind = CssTokenKind.String; i++;
                while (i < css.Length && css[i] != c)
                {
                    token.ThrowIfCancellationRequested();
                    if (css[i] is '\r' or '\n' or '\f') throw Invalid("Newline in CSS string.", sourceOffset + i);
                    if (css[i] == '\\') i++;
                    i++;
                }
                if (i >= css.Length) throw Invalid("Unterminated CSS string.", sourceOffset + start);
                i++;
            }
            else if (NumberStart(css, i))
            {
                kind = CssTokenKind.Number;
                if (css[i] is '+' or '-') i++;
                while (i < css.Length && char.IsAsciiDigit(css[i])) i++;
                if (i + 1 < css.Length && css[i] == '.' && char.IsAsciiDigit(css[i + 1]))
                { i++; while (i < css.Length && char.IsAsciiDigit(css[i])) i++; }
                while (i < css.Length && NameChar(css[i])) i++;
                if (i < css.Length && css[i] == '%') i++;
            }
            else if (c == '#' && i + 1 < css.Length && NameChar(css[i + 1]))
            { kind = CssTokenKind.Hash; i++; while (i < css.Length && NameChar(css[i])) i++; }
            else if (IdentifierStart(css, i))
            { kind = CssTokenKind.Identifier; i++; while (i < css.Length && NameChar(css[i])) i++; }
            else { kind = CssTokenKind.Symbol; i++; }
            result.Add(new(kind, css[start..i], space, sourceOffset + start)); space = false;
        }
        result.Add(new(CssTokenKind.End, "", space, sourceOffset + css.Length)); return result;
    }
    internal static bool IdentifierStart(string s, int i)
        => i < s.Length && (char.IsAsciiLetter(s[i]) || s[i] == '_' || s[i] == '-' && i + 1 < s.Length && (char.IsAsciiLetter(s[i + 1]) || s[i + 1] is '_' or '-'));
    private static bool NameChar(char c) => char.IsAsciiLetterOrDigit(c) || c is '_' or '-';
    private static bool NumberStart(string s, int i)
    {
        if (i < s.Length && s[i] is '+' or '-') i++;
        return i < s.Length && (char.IsAsciiDigit(s[i]) || s[i] == '.' && i + 1 < s.Length && char.IsAsciiDigit(s[i + 1]));
    }
    internal static FactsPdfException Invalid(string message, int offset) => new("FPDF1203", message, offset);
}
