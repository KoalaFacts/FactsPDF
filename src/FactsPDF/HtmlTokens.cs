using System.Globalization;
using System.Text;

namespace FactsPDF;

internal enum HtmlTokenKind { Text, Start, End, StyleText }
internal record HtmlToken(HtmlTokenKind Kind, string Value, bool SelfClosing, int Offset)
{
    // Attributes: null for text, end and style-text tokens; a start tag without attributes shares
    // one empty map. Only a start tag with attributes is an AttributedHtmlToken that stores its
    // map, so every other token is 8 bytes smaller.
    public virtual Dictionary<string, string>? Attributes => Kind == HtmlTokenKind.Start ? HtmlTokens.NoAttributes : null;

    // StyleValueOffset: where the style attribute's raw value starts in the page (-1 without one).
    // StyleOriginOffsets: the page offset of every decoded code unit plus its end, kept only when
    // the value contains '&'; otherwise code unit i comes from StyleValueOffset + i.
    // Only a start tag with a style attribute is a StyledHtmlToken that stores them.
    public virtual int StyleValueOffset => -1;
    public virtual int[]? StyleOriginOffsets => null;
}

internal record AttributedHtmlToken(string Value, Dictionary<string, string> AttributeMap, bool SelfClosing, int Offset)
    : HtmlToken(HtmlTokenKind.Start, Value, SelfClosing, Offset)
{
    public override Dictionary<string, string>? Attributes => AttributeMap;
}

internal sealed record StyledHtmlToken(string Value, Dictionary<string, string> AttributeMap,
    bool SelfClosing, int Offset, int StyleStart, int[]? StyleOrigins)
    : AttributedHtmlToken(Value, AttributeMap, SelfClosing, Offset)
{
    public override int StyleValueOffset => StyleStart;
    public override int[]? StyleOriginOffsets => StyleOrigins;
}

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
                yield return new(HtmlTokenKind.Text, html[i..end], false, i);
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
            var name = LowerName(html, nameStart, i);
            SkipSpace(html, ref i);
            if (closing)
            {
                if (i >= html.Length || html[i++] != '>') throw Invalid("Malformed end tag.", start);
                yield return new(HtmlTokenKind.End, name, false, start);
                continue;
            }
            Dictionary<string, string>? attrs = null;
            var styleValueOffset = -1;
            int[]? styleOriginOffsets = null;
            var selfClosing = false;
            while (i < html.Length && html[i] != '>')
            {
                cancellation.ThrowIfCancellationRequested();
                if (html[i] == '/' && i + 1 < html.Length && html[i + 1] == '>')
                { selfClosing = true; i++; break; }
                var attrStart = i;
                while (i < html.Length && NameCharacter(html[i])) i++;
                if (i == attrStart) throw Invalid("Malformed attribute.", i);
                var attribute = LowerName(html, attrStart, i);
                SkipSpace(html, ref i);
                var value = "";
                var rawValueStart = attrStart;
                if (i < html.Length && html[i] == '=')
                {
                    i++;
                    SkipSpace(html, ref i);
                    if (i >= html.Length) throw Invalid("Missing attribute value.", attrStart);
                    if (html[i] is '\'' or '"')
                    {
                        var quote = html[i++];
                        rawValueStart = i;
                        var end = html.IndexOf(quote, i);
                        if (end < 0) throw Invalid("Unterminated quoted attribute.", attrStart);
                        value = html[i..end];
                        i = end + 1;
                    }
                    else
                    {
                        var valueStart = i;
                        rawValueStart = valueStart;
                        while (i < html.Length && !Space(html[i]) && html[i] != '>')
                        {
                            if (html[i] is '<' or '\'' or '"' or '=' or '`') throw Invalid("Invalid unquoted attribute.", i);
                            i++;
                        }
                        if (i == valueStart) throw Invalid("Empty unquoted attribute.", attrStart);
                        value = html[valueStart..i];
                    }
                }
                if (attribute == "style")
                {
                    // Without '&' the value is its own decoding, so its origins are its own positions.
                    var hasReferences = value.Contains('&');
                    var decoded = value;
                    int[]? offsets = null;
                    if (hasReferences) (decoded, offsets) = DecodeWithOriginOffsets(value, rawValueStart);
                    if (!(attrs ??= NewAttributes()).TryAdd(attribute, decoded)) throw Invalid("Duplicate attribute.", attrStart);
                    styleValueOffset = rawValueStart;
                    styleOriginOffsets = offsets;
                }
                else if (!(attrs ??= NewAttributes()).TryAdd(attribute, Decode(value, attrStart)))
                    throw Invalid("Duplicate attribute.", attrStart);
                SkipSpace(html, ref i);
            }
            if (i >= html.Length || html[i] != '>') throw Invalid("Unterminated start tag.", start);
            i++;
            yield return styleValueOffset >= 0
                ? new StyledHtmlToken(name, attrs!, selfClosing, start, styleValueOffset, styleOriginOffsets)
                : attrs is null
                    ? new HtmlToken(HtmlTokenKind.Start, name, selfClosing, start)
                    : new AttributedHtmlToken(name, attrs, selfClosing, start);
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
                yield return new(HtmlTokenKind.StyleText, html[i..end], false, i);
                i = end;
            }
        }
    }

    /// <summary>Decode one style attribute while retaining original HTML boundaries for CSS diagnostics.</summary>
    private static (string Decoded, int[] OriginalOffsets) DecodeWithOriginOffsets(string raw, int rawStart)
    {
        var decoded = Decode(raw, rawStart);
        var positions = new int[decoded.Length + 1];
        var input = 0; var output = 0;
        while (input < raw.Length)
        {
            if (raw[input] == '&' && input + 1 < raw.Length &&
                (char.IsAsciiLetterOrDigit(raw[input + 1]) || raw[input + 1] == '#'))
            {
                var end = raw.IndexOf(';', input + 1);
                if (end >= 0 && end - input <= 33)
                {
                    var entity = Decode(raw[input..(end + 1)], rawStart + input);
                    for (var j = 0; j < entity.Length; j++)
                        positions[output++] = rawStart + input;
                    input = end + 1;
                    continue;
                }
            }
            positions[output++] = rawStart + input;
            input++;
        }
        if (output != decoded.Length) throw new InvalidOperationException("HTML attribute decode offset map is inconsistent.");
        positions[output] = rawStart + input;
        return (decoded, positions);
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

    // Start tags without attributes share this instance; token consumers only read attributes.
    internal static readonly Dictionary<string, string> NoAttributes = NewAttributes();

    private static Dictionary<string, string> NewAttributes() => new(StringComparer.OrdinalIgnoreCase);

    // Lower-case spellings of the tag and attribute names the reader and the CSS layer use. A name
    // found here is returned without a substring or lower-casing copy; any other name falls back to
    // the copy. Names are ASCII (NameCharacter), so the ignore-case match equals ToLowerInvariant.
    private static readonly HashSet<string>.AlternateLookup<ReadOnlySpan<char>> KnownNames = new HashSet<string>([
        "a", "article", "b", "blockquote", "body", "br", "caption", "code", "col", "colgroup", "dd", "del", "div",
        "dl", "dt", "em", "footer", "h1", "h2", "h3", "h4", "h5", "h6", "head", "header", "hr", "html", "i", "img",
        "ins", "li", "link", "main", "meta", "nav", "ol", "p", "pre", "s", "section", "small", "span", "strong",
        "style", "sub", "sup", "table", "tbody", "td", "tfoot", "th", "thead", "title", "tr", "u", "ul",
        "alt", "charset", "class", "colspan", "content", "dir", "height", "href", "id", "lang", "name", "reversed",
        "rowspan", "src", "start", "type", "value", "width",
    ], StringComparer.OrdinalIgnoreCase).GetAlternateLookup<ReadOnlySpan<char>>();

    private static string LowerName(string html, int start, int end) =>
        KnownNames.TryGetValue(html.AsSpan(start, end - start), out var known) ? known : html[start..end].ToLowerInvariant();

    public static bool Space(char ch) => ch is ' ' or '\t' or '\r' or '\n' or '\f';
    private static bool NameCharacter(char ch) => char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_' or ':';
    private static void SkipSpace(string html, ref int i) { while (i < html.Length && Space(html[i])) i++; }
    private static FactsPdfException Invalid(string message, int offset) => new("FPDF1101", message, offset);
}
