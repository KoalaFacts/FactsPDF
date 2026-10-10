namespace FactsPDF.CssSyntax;

/// <summary>CSS Syntax Level 3 tokenizer. Numeric and delimiter subset; other token families follow in TDD increments.</summary>
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
                else if (i < text.Length && (char.IsAsciiLetter(text[i]) || text[i] is '_' or '-'))
                {
                    var unitStart = i++;
                    while (i < text.Length && (char.IsAsciiLetterOrDigit(text[i]) || text[i] is '_' or '-')) i++;
                    Add(CssSyntaxTokenKind.Dimension, start, i, numeric, integer, text[unitStart..i]);
                }
                else Add(CssSyntaxTokenKind.Number, start, i, numeric, integer);
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
