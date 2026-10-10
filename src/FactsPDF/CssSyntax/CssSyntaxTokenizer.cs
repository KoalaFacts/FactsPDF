using System.Text;

namespace FactsPDF.CssSyntax;

/// <summary>Incremental standards-based CSS tokenizer; URL/string token families follow next.</summary>
internal static class CssSyntaxTokenizer
{
    internal static IReadOnlyList<CssSyntaxToken> Tokenize(CssSourceText source, CssSyntaxLimits limits)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(limits);
        limits.Cancellation.ThrowIfCancellationRequested();
        var text = source.Normalized;
        var result = new List<CssSyntaxToken>();

        void Add(CssSyntaxTokenKind kind, int start, int end, string? value = null,
            bool integer = false, string? unit = null)
        {
            limits.Cancellation.ThrowIfCancellationRequested();
            var raw = text[start..end];
            result.Add(new CssSyntaxToken(
                kind, value ?? raw, raw, source.Span(start, end - start),
                integer, false, unit));
        }

        for (var i = 0; i < text.Length;)
        {
            limits.Cancellation.ThrowIfCancellationRequested();
            if (i + 1 < text.Length && text[i] == '/' && text[i + 1] == '*')
            {
                var close = text.IndexOf("*/", i + 2, StringComparison.Ordinal);
                i = close < 0 ? text.Length : close + 2;
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
                Add(parsed.Kind, start, i, parsed.Value);
                continue;
            }
            if (text[i] == '#' && i + 1 < text.Length &&
                (IsNameChar(text[i + 1]) || IsValidEscape(text, i + 1)))
            {
                var start = i++;
                var isId = StartsIdentifier(text, i);
                var name = ConsumeName(text, ref i);
                result.Add(new CssSyntaxToken(
                    CssSyntaxTokenKind.Hash, name, text[start..i],
                    source.Span(start, i - start), false, isId));
                continue;
            }
            if (text[i] == '@' && StartsIdentifier(text, i + 1))
            {
                var start = i++;
                var name = ConsumeName(text, ref i);
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
                    var unit = ConsumeName(text, ref i);
                    Add(CssSyntaxTokenKind.Dimension, start, i, numeric, integer, unit);
                }
                else Add(CssSyntaxTokenKind.Number, start, i, numeric, integer);
                continue;
            }
            if (StartsIdentifier(text, i))
            {
                var start = i;
                var ident = ConsumeName(text, ref i);
                if (i < text.Length && text[i] == '(')
                {
                    i++;
                    var lookahead = i;
                    while (lookahead < text.Length && IsWhitespace(text[lookahead])) lookahead++;
                    if (ident.Equals("url", StringComparison.OrdinalIgnoreCase) &&
                        !(lookahead < text.Length && text[lookahead] is '\'' or '"'))
                    {
                        var parsed = ConsumeUrl(text, ref i, limits.Cancellation);
                        Add(parsed.Kind, start, i, parsed.Value);
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
        return result;
    }

    private static bool IsWhitespace(char ch) => ch is ' ' or '\t' or '\n';

    private static (CssSyntaxTokenKind Kind, string Value) ConsumeString(
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
                return (CssSyntaxTokenKind.String, value.ToString());
            }
            if (c == '\n') return (CssSyntaxTokenKind.BadString, value.ToString());
            if (c == '\\')
            {
                if (at + 1 < text.Length && text[at + 1] == '\n') { at += 2; continue; }
                if (IsValidEscape(text, at)) { value.Append(ConsumeEscape(text, ref at)); continue; }
            }
            value.Append(c);
            at++;
        }
        return (CssSyntaxTokenKind.String, value.ToString());
    }

    private static (CssSyntaxTokenKind Kind, string Value) ConsumeUrl(
        string text, ref int at, CancellationToken cancellation)
    {
        while (at < text.Length && IsWhitespace(text[at])) at++;
        var value = new StringBuilder();
        while (at < text.Length)
        {
            if ((at & 1023) == 0) cancellation.ThrowIfCancellationRequested();
            var c = text[at];
            if (c == ')') { at++; return (CssSyntaxTokenKind.Url, value.ToString()); }
            if (IsWhitespace(c))
            {
                while (at < text.Length && IsWhitespace(text[at])) at++;
                if (at == text.Length) return (CssSyntaxTokenKind.Url, value.ToString());
                if (text[at] == ')') { at++; return (CssSyntaxTokenKind.Url, value.ToString()); }
                ConsumeBadUrlRemnants(text, ref at, cancellation);
                return (CssSyntaxTokenKind.BadUrl, value.ToString());
            }
            if (c is '"' or '\'' or '(' || IsNonPrintable(c))
            {
                ConsumeBadUrlRemnants(text, ref at, cancellation);
                return (CssSyntaxTokenKind.BadUrl, value.ToString());
            }
            if (c == '\\')
            {
                if (IsValidEscape(text, at)) { value.Append(ConsumeEscape(text, ref at)); continue; }
                ConsumeBadUrlRemnants(text, ref at, cancellation);
                return (CssSyntaxTokenKind.BadUrl, value.ToString());
            }
            value.Append(c); at++;
        }
        return (CssSyntaxTokenKind.Url, value.ToString());
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

    private static bool IsNameStart(char c)
        => char.IsAsciiLetter(c) || c == '_' || c >= 0x0080;

    private static bool IsNameChar(char c)
        => IsNameStart(c) || char.IsAsciiDigit(c) || c == '-';

    private static bool IsValidEscape(string text, int at)
        => at < text.Length && text[at] == '\\' &&
           (at + 1 == text.Length || text[at + 1] != '\n');

    private static bool StartsIdentifier(string text, int at)
    {
        if (at >= text.Length) return false;
        if (text[at] == '-')
        {
            if (at + 1 >= text.Length) return false;
            var next = text[at + 1];
            return IsNameStart(next) || next == '-' || IsValidEscape(text, at + 1);
        }
        return IsNameStart(text[at]) || IsValidEscape(text, at);
    }

    private static string ConsumeName(string text, ref int at)
    {
        var builder = new StringBuilder();
        while (at < text.Length)
        {
            if (IsNameChar(text[at]))
            {
                builder.Append(text[at++]);
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
