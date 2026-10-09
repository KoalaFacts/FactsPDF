using System.Globalization;
using System.Text;

namespace FactsPDF;

internal enum HtmlTokenKind { Text, Start, End, StyleText }
internal sealed record HtmlToken(HtmlTokenKind Kind, string Value, Dictionary<string, string>? Attributes,
    bool SelfClosing, int Offset);

/// <summary>Tokenizer for the explicitly documented static-HTML subset; not an HTML5 conformance claim.</summary>
internal static class HtmlTokens
{
    public static IEnumerable<HtmlToken> Read(string html, CancellationToken cancellation)
    {
        var i = 0;
        while (i < html.Length)
        {
            cancellation.ThrowIfCancellationRequested();
            var start = i;
            if (html[i] != '<')
            {
                var end = html.IndexOf('<', i);
                if (end < 0) end = html.Length;
                yield return new(HtmlTokenKind.Text, html[i..end], null, false, i);
                i = end;
                continue;
            }
            if (html.AsSpan(i).StartsWith("<!--", StringComparison.Ordinal))
            {
                var end = html.IndexOf("-->", i + 4, StringComparison.Ordinal);
                if (end < 0) throw Invalid("Unterminated comment.", i);
                i = end + 3;
                continue;
            }
            if (html.AsSpan(i).StartsWith("<!", StringComparison.Ordinal))
            {
                var end = html.IndexOf('>', i + 2);
                if (end < 0 || !html[(i + 2)..end].Trim().Equals("doctype html", StringComparison.OrdinalIgnoreCase))
                    throw Invalid("Only the HTML5 doctype is supported.", i);
                i = end + 1;
                continue;
            }
            i++;
            var closing = i < html.Length && html[i] == '/';
            if (closing) i++;
            var nameStart = i;
            while (i < html.Length && NameCharacter(html[i])) i++;
            if (i == nameStart || !char.IsAsciiLetter(html[nameStart])) throw Invalid("Expected an HTML tag name.", start);
            var name = html[nameStart..i].ToLowerInvariant();
            SkipSpace(html, ref i);
            if (closing)
            {
                if (i >= html.Length || html[i++] != '>') throw Invalid("Malformed end tag.", start);
                yield return new(HtmlTokenKind.End, name, null, false, start);
                continue;
            }
            var attrs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var selfClosing = false;
            while (i < html.Length && html[i] != '>')
            {
                cancellation.ThrowIfCancellationRequested();
                if (html[i] == '/' && i + 1 < html.Length && html[i + 1] == '>')
                { selfClosing = true; i++; break; }
                var attrStart = i;
                while (i < html.Length && NameCharacter(html[i])) i++;
                if (i == attrStart) throw Invalid("Malformed attribute.", i);
                var attribute = html[attrStart..i].ToLowerInvariant();
                SkipSpace(html, ref i);
                var value = "";
                if (i < html.Length && html[i] == '=')
                {
                    i++;
                    SkipSpace(html, ref i);
                    if (i >= html.Length) throw Invalid("Missing attribute value.", attrStart);
                    if (html[i] is '\'' or '"')
                    {
                        var quote = html[i++];
                        var end = html.IndexOf(quote, i);
                        if (end < 0) throw Invalid("Unterminated quoted attribute.", attrStart);
                        value = html[i..end];
                        i = end + 1;
                    }
                    else
                    {
                        var valueStart = i;
                        while (i < html.Length && !Space(html[i]) && html[i] != '>')
                        {
                            if (html[i] is '<' or '\'' or '"' or '=' or '`') throw Invalid("Invalid unquoted attribute.", i);
                            i++;
                        }
                        if (i == valueStart) throw Invalid("Empty unquoted attribute.", attrStart);
                        value = html[valueStart..i];
                    }
                }
                if (!attrs.TryAdd(attribute, Decode(value, attrStart))) throw Invalid("Duplicate attribute.", attrStart);
                SkipSpace(html, ref i);
            }
            if (i >= html.Length || html[i] != '>') throw Invalid("Unterminated start tag.", start);
            i++;
            yield return new(HtmlTokenKind.Start, name, attrs, selfClosing, start);
            if (name == "style" && !selfClosing)
            {
                // RAWTEXT does not decode entities or recognize markup inside CSS comments/strings.
                // The HTML end delimiter still wins even when it appears inside a CSS quote/comment.
                var end = i;
                while (true)
                {
                    cancellation.ThrowIfCancellationRequested();
                    end = html.IndexOf("</style", end, StringComparison.OrdinalIgnoreCase);
                    if (end < 0) throw Invalid("Unclosed style element.", start);
                    var after = end + 7;
                    if (after < html.Length && (Space(html[after]) || html[after] is '>' or '/')) break;
                    end = after;
                }
                yield return new(HtmlTokenKind.StyleText, html[i..end], null, false, i);
                i = end;
            }
        }
    }

    public static string Decode(string text, int offset)
    {
        if (!text.Contains('&')) return text;
        var result = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '&') { result.Append(text[i]); continue; }
            if (i + 1 >= text.Length || (!char.IsAsciiLetterOrDigit(text[i + 1]) && text[i + 1] != '#'))
            { result.Append('&'); continue; }
            var end = text.IndexOf(';', i + 1);
            if (end < 0 || end - i > 33) throw new FactsPdfException("FPDF1104", "Character references need a supported name and semicolon.", offset + i);
            var name = text[(i + 1)..end];
            var decoded = name switch
            {
                "amp" => "&", "lt" => "<", "gt" => ">", "quot" => "\"", "apos" => "'", "nbsp" => "\u00a0", _ => null
            };
            if (decoded is null && name.StartsWith('#'))
            {
                var hex = name.Length > 1 && name[1] is 'x' or 'X';
                var number = name.AsSpan(hex ? 2 : 1);
                if (int.TryParse(number, hex ? NumberStyles.AllowHexSpecifier : NumberStyles.None,
                    CultureInfo.InvariantCulture, out var scalar) && scalar > 0 && Rune.IsValid(scalar))
                    decoded = new Rune(scalar).ToString();
            }
            if (decoded is null) throw new FactsPdfException("FPDF1104", $"Unsupported character reference '&{name};'.", offset + i);
            result.Append(decoded);
            i = end;
        }
        return result.ToString();
    }

    public static bool Space(char ch) => ch is ' ' or '\t' or '\r' or '\n' or '\f';
    private static bool NameCharacter(char ch) => char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_' or ':';
    private static void SkipSpace(string html, ref int i) { while (i < html.Length && Space(html[i])) i++; }
    private static FactsPdfException Invalid(string message, int offset) => new("FPDF1101", message, offset);
}
