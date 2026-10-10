using System.Text;

namespace FactsPDF.CssSyntax;

/// <summary>CSS Syntax tokenizer with Unicode identifiers and recovery diagnostics.</summary>
internal static class CssSyntaxTokenizer
{
    internal static IReadOnlyList<CssSyntaxToken> Tokenize(CssSourceText source, CssSyntaxLimits limits)
        => TokenizeWithDiagnostics(source, limits).Tokens;

    internal static CssSyntaxTokenizationResult TokenizeWithDiagnostics(
        CssSourceText source, CssSyntaxLimits limits)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(limits);
        limits.Cancellation.ThrowIfCancellationRequested();
        var text = source.Normalized;
        var result = new List<CssSyntaxToken>();
        var diagnostics = new List<CssSyntaxDiagnostic>();

        void Add(CssSyntaxTokenKind kind, int start, int end, string? value = null,
            bool integer = false, string? unit = null, bool terminated = true)
        {
            limits.Cancellation.ThrowIfCancellationRequested();
            var raw = text[start..end];
            result.Add(new CssSyntaxToken(
                kind, value ?? raw, raw, source.Span(start, end - start),
                integer, false, unit, terminated));
        }

        for (var i = 0; i < text.Length;)
        {
            limits.Cancellation.ThrowIfCancellationRequested();
            if (i + 1 < text.Length && text[i] == '/' && text[i + 1] == '*')
            {
                var close = text.IndexOf("*/", i + 2, StringComparison.Ordinal);
                if (close < 0)
                {
                    diagnostics.Add(new("CSS_SYNTAX", "Unterminated CSS comment.",
                        source.Span(i, text.Length - i), "eof"));
                    i = text.Length;
                }
                else i = close + 2;
                continue;
            }
            if (text.AsSpan(i).StartsWith("<!--", StringComparison.Ordinal))
            {
                Add(CssSyntaxTokenKind.Cdo, i, i + 4); i += 4; continue;
            }
            if (text.AsSpan(i).StartsWith("-->", StringComparison.Ordinal))
            {
                Add(CssSyntaxTokenKind.Cdc, i, i + 3); i += 3; continue;
            }
            if (text[i] is ' ' or '\t' or '\n')
            {
                var start = i;
                do { i++; } while (i < text.Length && text[i] is ' ' or '\t' or '\n');
                Add(CssSyntaxTokenKind.Whitespace, start, i, " ");
                continue;
            }
            if (text[i] is '\'' or '"')
            {
                var start = i;
                var parsed = ConsumeString(text, ref i, limits.Cancellation);
                Add(parsed.Kind, start, i, parsed.Value, terminated: parsed.Terminated);
                continue;
            }
            if (text[i] == '#' && i + 1 < text.Length &&
                (IsNameCharAt(text, i + 1) || IsValidEscape(text, i + 1)))
            {
                var start = i++;
                var isId = StartsIdentifier(text, i);
                var name = ConsumeName(text, ref i, limits.Cancellation);
                result.Add(new CssSyntaxToken(
                    CssSyntaxTokenKind.Hash, name, text[start..i],
                    source.Span(start, i - start), false, isId));
                continue;
            }
            if (text[i] == '@' && StartsIdentifier(text, i + 1))
            {
                var start = i++;
                var name = ConsumeName(text, ref i, limits.Cancellation);
                Add(CssSyntaxTokenKind.AtKeyword, start, i, name);
                continue;
            }
            if (StartsNumber(text, i))
            {
                var start = i;
                if (text[i] is '+' or '-') i++;
                while (i < text.Length && char.IsAsciiDigit(text[i])) i++;
                var integer = true;
                if (i + 1 < text.Length && text[i] == '.' && char.IsAsciiDigit(text[i + 1]))
                {
                    integer = false; i += 2;
                    while (i < text.Length && char.IsAsciiDigit(text[i])) i++;
                }
                if (i < text.Length && text[i] is 'e' or 'E')
                {
                    var j = i + 1;
                    if (j < text.Length && text[j] is '+' or '-') j++;
                    if (j < text.Length && char.IsAsciiDigit(text[j]))
                    {
                        integer = false; i = j + 1;
                        while (i < text.Length && char.IsAsciiDigit(text[i])) i++;
                    }
                }
                var numericEnd = i;
                var numeric = text[start..numericEnd];
                if (i < text.Length && text[i] == '%')
                {
                    i++; Add(CssSyntaxTokenKind.Percentage, start, i, numeric, integer);
                }
                else if (StartsIdentifier(text, i))
                {
                    var unit = ConsumeName(text, ref i, limits.Cancellation);
                    Add(CssSyntaxTokenKind.Dimension, start, i, numeric, integer, unit);
                }
                else Add(CssSyntaxTokenKind.Number, start, i, numeric, integer);
                continue;
            }
            if (StartsIdentifier(text, i))
            {
                var start = i;
                var ident = ConsumeName(text, ref i, limits.Cancellation);
                if (i < text.Length && text[i] == '(')
                {
                    i++;
                    var lookahead = i;
                    while (lookahead < text.Length && IsWhitespace(text[lookahead])) lookahead++;
                    if (ident.Equals("url", StringComparison.OrdinalIgnoreCase) &&
                        !(lookahead < text.Length && text[lookahead] is '\'' or '"'))
                    {
                        var parsed = ConsumeUrl(text, ref i, limits.Cancellation);
                        Add(parsed.Kind, start, i, parsed.Value, terminated: parsed.Terminated);
                    }
                    else Add(CssSyntaxTokenKind.Function, start, i, ident);
                }
                else Add(CssSyntaxTokenKind.Ident, start, i, ident);
                continue;
            }

            var at = i++;
            var kind = text[at] switch
            {
                '{' => CssSyntaxTokenKind.OpenBrace,
                '}' => CssSyntaxTokenKind.CloseBrace,
                '(' => CssSyntaxTokenKind.OpenParen,
                ')' => CssSyntaxTokenKind.CloseParen,
                '[' => CssSyntaxTokenKind.OpenSquare,
                ']' => CssSyntaxTokenKind.CloseSquare,
                ':' => CssSyntaxTokenKind.Colon,
                ';' => CssSyntaxTokenKind.Semicolon,
                ',' => CssSyntaxTokenKind.Comma,
                _ => CssSyntaxTokenKind.Delim
            };
            Add(kind, at, i);
        }
        result.Add(new CssSyntaxToken(
            CssSyntaxTokenKind.Eof, "", "",
            source.Span(text.Length, 0)));
        return new CssSyntaxTokenizationResult(result.ToArray(), diagnostics.ToArray());
    }

    private static bool IsWhitespace(char ch) => ch is ' ' or '\t' or '\n';

    private static (CssSyntaxTokenKind Kind, string Value, bool Terminated) ConsumeString(
        string text, ref int at, CancellationToken cancellation)
    {
        var quote = text[at++];
        var value = new StringBuilder();
        while (at < text.Length)
        {
            if ((at & 1023) == 0) cancellation.ThrowIfCancellationRequested();
            var c = text[at];
            if (c == quote)
            {
                at++;
                return (CssSyntaxTokenKind.String, value.ToString(), true);
            }
            if (c == '\n') return (CssSyntaxTokenKind.BadString, value.ToString(), false);
            if (c == '\\')
            {
                if (at + 1 < text.Length && text[at + 1] == '\n') { at += 2; continue; }
                if (IsValidEscape(text, at)) { value.Append(ConsumeEscape(text, ref at)); continue; }
            }
            value.Append(c);
            at++;
        }
        return (CssSyntaxTokenKind.String, value.ToString(), false);
    }

    private static (CssSyntaxTokenKind Kind, string Value, bool Terminated) ConsumeUrl(
        string text, ref int at, CancellationToken cancellation)
    {
        while (at < text.Length && IsWhitespace(text[at])) at++;
        var value = new StringBuilder();
        while (at < text.Length)
        {
            if ((at & 1023) == 0) cancellation.ThrowIfCancellationRequested();
            var c = text[at];
            if (c == ')') { at++; return (CssSyntaxTokenKind.Url, value.ToString(), true); }
            if (IsWhitespace(c))
            {
                while (at < text.Length && IsWhitespace(text[at])) at++;
                if (at == text.Length) return (CssSyntaxTokenKind.Url, value.ToString(), false);
                if (text[at] == ')') { at++; return (CssSyntaxTokenKind.Url, value.ToString(), true); }
                ConsumeBadUrlRemnants(text, ref at, cancellation);
                return (CssSyntaxTokenKind.BadUrl, value.ToString(), false);
            }
            if (c is '"' or '\'' or '(' || IsNonPrintable(c))
            {
                ConsumeBadUrlRemnants(text, ref at, cancellation);
                return (CssSyntaxTokenKind.BadUrl, value.ToString(), false);
            }
            if (c == '\\')
            {
                if (IsValidEscape(text, at)) { value.Append(ConsumeEscape(text, ref at)); continue; }
                ConsumeBadUrlRemnants(text, ref at, cancellation);
                return (CssSyntaxTokenKind.BadUrl, value.ToString(), false);
            }
            value.Append(c); at++;
        }
        return (CssSyntaxTokenKind.Url, value.ToString(), false);
    }

    private static void ConsumeBadUrlRemnants(string text, ref int at, CancellationToken cancellation)
    {
        while (at < text.Length)
        {
            if ((at & 1023) == 0) cancellation.ThrowIfCancellationRequested();
            if (text[at] == ')') { at++; return; }
            if (IsValidEscape(text, at)) { _ = ConsumeEscape(text, ref at); continue; }
            at++;
        }
    }

    private static bool IsNonPrintable(char c)
        => c <= '\u0008' || c == '\u000B' || c is >= '\u000E' and <= '\u001F' || c == '\u007F';

    // 2026 CSS Syntax Level 3's restricted non-ASCII ident-code-point
    // ranges. In particular bidi controls U+202E, U+2060, U+0080 and
    // soft hyphen U+00AD are NOT unescaped identifier constituents.
    private static bool IsNonAsciiIdent(int cp)
        => cp == 0x00B7 ||
           cp is >= 0x00C0 and <= 0x00D6 or >= 0x00D8 and <= 0x00F6
              or >= 0x00F8 and <= 0x037D or >= 0x037F and <= 0x1FFF
              or >= 0x200C and <= 0x200D or >= 0x203F and <= 0x2040
              or >= 0x2070 and <= 0x218F or >= 0x2C00 and <= 0x2FEF
              or >= 0x3001 and <= 0xD7FF or >= 0xF900 and <= 0xFDCF
              or >= 0xFDF0 and <= 0xFFFD or >= 0x10000 and <= 0x10FFFF;

    private static bool IsNameStartAt(string text, int at)
    {
        if (at >= text.Length) return false;
        var c = text[at];
        if (char.IsHighSurrogate(c) && at + 1 < text.Length &&
            char.IsLowSurrogate(text[at + 1]))
            return IsNonAsciiIdent(char.ConvertToUtf32(c, text[at + 1]));
        return char.IsAsciiLetter(c) || c == '_' || IsNonAsciiIdent(c);
    }

    private static bool IsNameCharAt(string text, int at)
        => IsNameStartAt(text, at) ||
           at < text.Length && (char.IsAsciiDigit(text[at]) || text[at] == '-');

    private static bool IsValidEscape(string text, int at)
        => at < text.Length && text[at] == '\\' &&
           (at + 1 == text.Length || text[at + 1] != '\n');

    private static bool StartsIdentifier(string text, int at)
    {
        if (at >= text.Length) return false;
        if (text[at] == '-')
        {
            if (at + 1 >= text.Length) return false;
            return IsNameStartAt(text, at + 1) || text[at + 1] == '-' ||
                   IsValidEscape(text, at + 1);
        }
        return IsNameStartAt(text, at) || IsValidEscape(text, at);
    }

    private static string ConsumeName(string text, ref int at, CancellationToken cancellation)
    {
        var builder = new StringBuilder();
        while (at < text.Length)
        {
            if ((at & 1023) == 0) cancellation.ThrowIfCancellationRequested();
            if (IsNameCharAt(text, at))
            {
                if (char.IsHighSurrogate(text[at]) && at + 1 < text.Length &&
                    char.IsLowSurrogate(text[at + 1]))
                {
                    builder.Append(text, at, 2);
                    at += 2;
                }
                else builder.Append(text[at++]);
            }
            else if (IsValidEscape(text, at))
            {
                builder.Append(ConsumeEscape(text, ref at));
            }
            else break;
        }
        return builder.ToString();
    }

    private static string ConsumeEscape(string text, ref int at)
    {
        at++; // consume reverse solidus
        if (at >= text.Length) return "\uFFFD";
        if (!char.IsAsciiHexDigit(text[at])) return text[at++].ToString();

        var scalar = 0;
        var length = 0;
        while (at < text.Length && length < 6 && char.IsAsciiHexDigit(text[at]))
        {
            var c = text[at++];
            scalar = scalar * 16 + (c is >= '0' and <= '9' ? c - '0'
                : c is >= 'a' and <= 'f' ? c - 'a' + 10 : c - 'A' + 10);
            length++;
        }
        if (at < text.Length && text[at] is ' ' or '\t' or '\n') at++;
        return scalar == 0 || !Rune.IsValid(scalar) ? "\uFFFD" : char.ConvertFromUtf32(scalar);
    }

    private static bool StartsNumber(string text, int at)
    {
        if (at >= text.Length) return false;
        if (text[at] is '+' or '-')
        {
            if (at + 1 < text.Length && char.IsAsciiDigit(text[at + 1])) return true;
            return at + 2 < text.Length && text[at + 1] == '.' && char.IsAsciiDigit(text[at + 2]);
        }
        return char.IsAsciiDigit(text[at]) ||
               (text[at] == '.' && at + 1 < text.Length && char.IsAsciiDigit(text[at + 1]));
    }
}
