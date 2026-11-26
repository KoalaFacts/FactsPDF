using System.Buffers;
using System.Globalization;
using System.Text;

namespace FactsPDF;

// One byte, so it packs beside SelfClosing: a plain token is 32 bytes instead of 40.
internal enum HtmlTokenKind : byte { Text, Start, End, StyleText }
internal record HtmlToken(HtmlTokenKind Kind, string Value, bool SelfClosing, int Offset)
{
    // Attributes: null for text, end and style-text tokens; a start tag without attributes shares
    // one empty map. Only a start tag with attributes is an AttributedHtmlToken that stores its
    // map, so every other token is 8 bytes smaller.
    public virtual HtmlAttributes? Attributes => Kind == HtmlTokenKind.Start ? HtmlAttributes.None : null;

    // StyleValueOffset: where the style attribute's raw value starts in the page (-1 without one).
    // StyleOriginOffsets: the page offset of every decoded code unit plus its end, kept only when
    // the value contains '&'; otherwise code unit i comes from StyleValueOffset + i.
    // Only a start tag with a style attribute is a StyledHtmlToken that stores them.
    public virtual int StyleValueOffset => -1;
    public virtual int[]? StyleOriginOffsets => null;
}

internal record AttributedHtmlToken(string Value, HtmlAttributes AttributeMap, bool SelfClosing, int Offset)
    : HtmlToken(HtmlTokenKind.Start, Value, SelfClosing, Offset)
{
    public override HtmlAttributes? Attributes => AttributeMap;
}

internal sealed record StyledHtmlToken(string Value, HtmlAttributes AttributeMap,
    bool SelfClosing, int Offset, int StyleStart, int[]? StyleOrigins)
    : AttributedHtmlToken(Value, AttributeMap, SelfClosing, Offset)
{
    public override int StyleValueOffset => StyleStart;
    public override int[]? StyleOriginOffsets => StyleOrigins;

    // The style attribute's compiled declarations, set once by CssStylesheets.Collect, so the
    // cascade reads them from the token instead of a per-conversion map keyed by tag offset.
    internal InlineStyle InlineDeclarations { get; set; }
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
            // Attributes collect in one per-thread scratch list and are copied into an exact-size
            // array at the end; the finally below empties it (and the name set) whether the tag
            // succeeded or failed, so nothing from this tag outlives it.
            var scratch = attributeScratch ??= [];
            var styleValueOffset = -1;
            int[]? styleOriginOffsets = null;
            var selfClosing = false;
            HtmlAttributes? attrs = null;
            try
            {
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
                        if (!AddAttribute(scratch, attribute, decoded)) throw Invalid("Duplicate attribute.", attrStart);
                        styleValueOffset = rawValueStart;
                        styleOriginOffsets = offsets;
                    }
                    else if (!AddAttribute(scratch, attribute, Decode(value, attrStart)))
                        throw Invalid("Duplicate attribute.", attrStart);
                    SkipSpace(html, ref i);
                }
                if (i >= html.Length || html[i] != '>') throw Invalid("Unterminated start tag.", start);
                i++;
                if (scratch.Count > 0) attrs = new(scratch.ToArray());
            }
            finally
            {
                ReleaseAttributeScratch(scratch);
            }
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
            else if (name == "script")
            {
                // Script data ends at the first "</script" followed by a delimiter, as in browsers,
                // which also ignore a self-closing slash on script. FactsPDF runs no scripts, so the
                // data is skipped and never becomes a token. "<!--" followed by "<script" would put
                // a browser in the double-escaped state, where "</script>" does not end the element;
                // that case fails instead of guessing where the element ends.
                var end = i;
                while (true)
                {
                    cancellation.ThrowIfCancellationRequested();
                    end = html.IndexOf("</script", end, StringComparison.OrdinalIgnoreCase);
                    if (end < 0) throw Invalid("Unclosed script element.", start);
                    var after = end + 8;
                    if (after < html.Length && (Space(html[after]) || html[after] is '>' or '/')) break;
                    end = after;
                }
                var data = html.AsSpan(i, end - i);
                var comment = data.IndexOf("<!--", StringComparison.Ordinal);
                if (comment >= 0 && data[comment..].Contains("<script", StringComparison.OrdinalIgnoreCase))
                    throw Invalid("A nested '<script' after '<!--' inside script data is not supported.", i + comment);
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
        Span<char> entity = stackalloc char[34]; // a reference is at most 34 characters (see Decode)
        while (input < raw.Length)
        {
            if (raw[input] == '&' && input + 1 < raw.Length &&
                (char.IsAsciiLetterOrDigit(raw[input + 1]) || raw[input + 1] == '#'))
            {
                var end = raw.IndexOf(';', input + 1);
                if (end >= 0 && end - input <= 33)
                {
                    var entityLength = DecodeInto(raw.AsSpan(input, end + 1 - input), rawStart + input, entity);
                    for (var j = 0; j < entityLength; j++)
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
        // Every reference is at least as long as what it decodes to (the shortest, "&#1;", is four
        // characters for one; a supplementary scalar is two UTF-16 units from at least eight), so
        // the decoded text fits in a buffer of the source length.
        char[]? rented = null;
        Span<char> buffer = text.Length <= 256 ? stackalloc char[256] : (rented = ArrayPool<char>.Shared.Rent(text.Length));
        try
        {
            var written = DecodeInto(text, offset, buffer);
            return new string(buffer[..written]);
        }
        finally
        {
            if (rented is not null) ArrayPool<char>.Shared.Return(rented);
        }
    }

    // Decodes character references in `text` into `destination` (at least text.Length long) and
    // returns the number of characters written; reference names are matched as spans, so no
    // name substring, numeric-reference string or builder is allocated.
    private static int DecodeInto(ReadOnlySpan<char> text, int offset, Span<char> destination)
    {
        var written = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '&') { destination[written++] = text[i]; continue; }
            if (i + 1 >= text.Length || (!char.IsAsciiLetterOrDigit(text[i + 1]) && text[i + 1] != '#'))
            { destination[written++] = '&'; continue; }
            var length = text[(i + 1)..].IndexOf(';');
            var end = length < 0 ? -1 : i + 1 + length;
            if (end < 0 || end - i > 33) throw new FactsPdfException("FPDF1104", "Character references need a supported name and semicolon.", offset + i);
            var name = text[(i + 1)..end];
            var named = name switch
            {
                "amp" => '&', "lt" => '<', "gt" => '>', "quot" => '"', "apos" => '\'', "nbsp" => '\u00a0', _ => '\0'
            };
            if (named != '\0') destination[written++] = named;
            else if (name.StartsWith('#') && NumericReference(name) is { } rune)
                written += rune.EncodeToUtf16(destination[written..]);
            else
                throw new FactsPdfException("FPDF1104", $"Unsupported character reference '&{name};'.", offset + i);
            i = end;
        }
        return written;
    }

    private static Rune? NumericReference(ReadOnlySpan<char> name)
    {
        var hex = name.Length > 1 && name[1] is 'x' or 'X';
        var number = name[(hex ? 2 : 1)..];
        return int.TryParse(number, hex ? NumberStyles.AllowHexSpecifier : NumberStyles.None,
            CultureInfo.InvariantCulture, out var scalar) && scalar > 0 && Rune.IsValid(scalar)
            ? new Rune(scalar) : null;
    }

    private const int MaxPooledAttributes = 1_024;
    [ThreadStatic] private static List<KeyValuePair<string, string>>? attributeScratch;
    [ThreadStatic] private static HashSet<string>? attributeNames;

    // Empties the scratch list and, when this tag used it, the name set; either is dropped when it
    // grew past MaxPooledAttributes, so one huge tag does not stay pinned to the thread.
    private static void ReleaseAttributeScratch(List<KeyValuePair<string, string>> scratch)
    {
        if (scratch.Count > HtmlAttributes.IndexedAbove && attributeNames is { } names)
        {
            if (scratch.Count > MaxPooledAttributes) attributeNames = null;
            else names.Clear();
        }
        scratch.Clear();
        if (scratch.Capacity > MaxPooledAttributes) attributeScratch = null;
    }

    // Adds an attribute unless its name is already present. Up to HtmlAttributes.IndexedAbove names
    // are compared directly; past that a per-thread set keeps a long attribute list linear.
    private static bool AddAttribute(List<KeyValuePair<string, string>> scratch, string name, string value)
    {
        if (scratch.Count < HtmlAttributes.IndexedAbove)
        {
            for (var i = 0; i < scratch.Count; i++)
                if (string.Equals(scratch[i].Key, name, StringComparison.OrdinalIgnoreCase)) return false;
        }
        else
        {
            var names = attributeNames ??= new(StringComparer.OrdinalIgnoreCase);
            if (scratch.Count == HtmlAttributes.IndexedAbove)
            {
                names.Clear();
                for (var i = 0; i < scratch.Count; i++) names.Add(scratch[i].Key);
            }
            if (!names.Add(name)) return false;
        }
        scratch.Add(new(name, value));
        return true;
    }

    // Lower-case spellings of the tag and attribute names the reader and the CSS layer use. A name
    // found here is returned without a substring or lower-casing copy; any other name falls back to
    // the copy. Names are ASCII (NameCharacter), so the ignore-case match equals ToLowerInvariant.
    private static readonly HashSet<string>.AlternateLookup<ReadOnlySpan<char>> KnownNames = new HashSet<string>([
        "a", "abbr", "address", "article", "aside", "b", "bdi", "blockquote", "body", "br", "caption", "cite", "code", "col", "colgroup", "data", "dd", "del", "dfn", "div",
        "dl", "dt", "em", "figcaption", "figure", "footer", "h1", "h2", "h3", "h4", "h5", "h6", "head", "header", "hgroup", "hr", "html", "i", "img",
        "ins", "kbd", "li", "link", "main", "menu", "meta", "nav", "ol", "p", "pre", "q", "s", "samp", "script", "search", "section", "small", "span", "strong",
        "style", "sub", "sup", "table", "tbody", "td", "template", "tfoot", "th", "thead", "time", "title", "tr", "u", "ul", "var",
        "alt", "charset", "class", "colspan", "content", "datetime", "dir", "height", "hidden", "href", "id", "lang", "name", "reversed",
        "property", "rel", "rowspan", "src", "start", "type", "value", "width",
    ], StringComparer.OrdinalIgnoreCase).GetAlternateLookup<ReadOnlySpan<char>>();

    private static string LowerName(string html, int start, int end) =>
        KnownNames.TryGetValue(html.AsSpan(start, end - start), out var known) ? known : html[start..end].ToLowerInvariant();

    public static bool Space(char ch) => ch is ' ' or '\t' or '\r' or '\n' or '\f';

    /// <summary>Whether every character is HTML whitespace (true for an empty string).</summary>
    /// <remarks>A span loop: <c>text.All(Space)</c> boxes a string enumerator per call.</remarks>
    public static bool AllSpace(string text)
    {
        foreach (var ch in text.AsSpan())
            if (!Space(ch)) return false;
        return true;
    }
    private static bool NameCharacter(char ch) => char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_' or ':';
    private static void SkipSpace(string html, ref int i) { while (i < html.Length && Space(html[i])) i++; }
    private static FactsPdfException Invalid(string message, int offset) => new("FPDF1101", message, offset);
}
