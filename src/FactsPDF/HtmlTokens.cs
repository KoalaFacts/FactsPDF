using System.Buffers;
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
    // The HTML void elements: their start tags never take an end tag, so a trailing slash is allowed.
    private static bool VoidName(string name) => name is "area" or "base" or "br" or "col" or "embed" or "hr"
        or "img" or "input" or "link" or "meta" or "param" or "source" or "track" or "wbr";

    public static IEnumerable<HtmlToken> Read(string html, CancellationToken cancellation)
    {
        var i = 0;
        var foreign = 0; // open svg elements: foreign content, where a self-closing slash closes the element
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
                if (name == "svg" && foreign > 0) foreign--;
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
                    else if (!AddAttribute(scratch, attribute, Decode(value, attribute: true)))
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
            // The HTML parser ignores the slash of a start tag that is neither void nor foreign
            // content: <div/> opens a div, which a later end tag closes.
            if (selfClosing && foreign == 0 && name != "svg" && !VoidName(name)) selfClosing = false;
            if (name == "svg" && !selfClosing) foreign++;
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
            else if (name is "noscript" or "iframe")
            {
                // With scripting enabled (as when a browser prints a page) the HTML parser reads
                // noscript as a raw text element, and iframe always: the content is data up to the
                // first "</noscript" or "</iframe" followed by a delimiter, never markup, and the
                // element's content is never rendered.
                var end = i;
                var close = "</" + name;
                while (true)
                {
                    cancellation.ThrowIfCancellationRequested();
                    end = html.IndexOf(close, end, StringComparison.OrdinalIgnoreCase);
                    if (end < 0) throw Invalid($"Unclosed {name} element.", start);
                    var after = end + close.Length;
                    if (after < html.Length && (Space(html[after]) || html[after] is '>' or '/')) break;
                    end = after;
                }
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
        // A reference never decodes to more code units than it is written with (see Decode).
        var characters = ArrayPool<char>.Shared.Rent(raw.Length);
        var origins = ArrayPool<int>.Shared.Rent(raw.Length + 1);
        try
        {
            var written = DecodeInto(raw, attribute: true, characters, origins, rawStart);
            origins[written] = rawStart + raw.Length;
            return (new string(characters, 0, written), origins.AsSpan(0, written + 1).ToArray());
        }
        finally
        {
            ArrayPool<char>.Shared.Return(characters);
            ArrayPool<int>.Shared.Return(origins);
        }
    }

    /// <summary>Decodes the character references of text (or, with <paramref name="attribute"/>, of an
    /// attribute value) exactly as the HTML tokenizer does: every named reference of the HTML
    /// Standard, the legacy names also without their semicolon, and numeric references with the
    /// tokenizer's replacements. Text that is not a reference, such as an unknown name, stays as written.</summary>
    public static string Decode(string text, bool attribute = false)
    {
        if (!text.Contains('&')) return text;
        // Every reference is at least as long as what it decodes to (the shortest, "&lt", is three
        // characters for one; a supplementary scalar is two UTF-16 units from at least five, as in
        // "&Afr;" or "&#65536"), so the decoded text fits in a buffer of the source length.
        char[]? rented = null;
        Span<char> buffer = text.Length <= 256 ? stackalloc char[256] : (rented = ArrayPool<char>.Shared.Rent(text.Length));
        try
        {
            var written = DecodeInto(text, attribute, buffer, default, 0);
            return new string(buffer[..written]);
        }
        finally
        {
            if (rented is not null) ArrayPool<char>.Shared.Return(rented);
        }
    }

    // Decodes `text` into `destination` (at least text.Length long) and returns the number of
    // characters written. When `origins` is not empty, origins[k] is originStart plus the index in
    // `text` where the reference (or plain character) that produced character k starts. Names are
    // matched as spans, so no name substring, numeric-reference string or builder is allocated.
    private static int DecodeInto(ReadOnlySpan<char> text, bool attribute, Span<char> destination, Span<int> origins, int originStart)
    {
        var written = 0;
        for (var i = 0; i < text.Length;)
        {
            var count = 1;
            var consumed = text[i] == '&' ? Reference(text[(i + 1)..], attribute, destination[written..], out count) : 0;
            if (consumed == 0)
            {
                destination[written] = text[i];
                count = consumed = 1;
            }
            else consumed++; // the ampersand
            if (!origins.IsEmpty)
                for (var k = 0; k < count; k++) origins[written + k] = originStart + i;
            written += count;
            i += consumed;
        }
        return written;
    }

    // The tokenizer's character reference state for the text after an ampersand: returns how many
    // characters of `rest` the reference consumes (0 when there is none, so the ampersand is text)
    // and writes what it stands for.
    private static int Reference(ReadOnlySpan<char> rest, bool attribute, Span<char> destination, out int written)
    {
        written = 0;
        if (rest.IsEmpty) return 0;
        if (rest[0] == '#') return NumericReference(rest, destination, out written);
        var length = HtmlCharacterReferences.Match(rest, out var value);
        if (length == 0) return 0;
        // Historical attribute values: a legacy name without its semicolon followed by '=' or an
        // ASCII alphanumeric is not a reference there ("?a=1&copy=2" keeps "&copy").
        if (attribute && rest[length - 1] != ';' && length < rest.Length
            && (rest[length] == '=' || char.IsAsciiLetterOrDigit(rest[length])))
            return 0;
        value.AsSpan().CopyTo(destination);
        written = value.Length;
        return length;
    }

    // "&#" followed by decimal digits, or "&#x"/"&#X" by hexadecimal ones, with an optional
    // semicolon; without a digit it is no reference. Zero, a surrogate or a value past U+10FFFF is
    // U+FFFD, and 0x80-0x9F take the windows-1252 characters (the numeric character reference end
    // state); any other value, a control or noncharacter included, is that scalar.
    private static int NumericReference(ReadOnlySpan<char> rest, Span<char> destination, out int written)
    {
        written = 0;
        var i = 1;
        var hex = i < rest.Length && rest[i] is 'x' or 'X';
        if (hex) i++;
        var digits = i;
        var value = 0;
        while (i < rest.Length && (hex ? char.IsAsciiHexDigit(rest[i]) : char.IsAsciiDigit(rest[i])))
        {
            // Saturates past the largest scalar, so a long run of digits never overflows.
            value = Math.Min(value * (hex ? 16 : 10) + HexDigit(rest[i]), 0x110000);
            i++;
        }
        if (i == digits) return 0;
        if (i < rest.Length && rest[i] == ';') i++;
        written = new Rune(NumericScalar(value)).EncodeToUtf16(destination);
        return i;
    }

    /// <summary>The scalar a numeric character reference with this value stands for.</summary>
    internal static int NumericScalar(int value) => value switch
    {
        0 or > 0x10FFFF or (>= 0xD800 and <= 0xDFFF) => 0xFFFD,
        >= 0x80 and <= 0x9F => HtmlEncoding.Windows1252High[value - 0x80],
        _ => value
    };

    private static int HexDigit(char ch) => ch <= '9' ? ch - '0' : (ch | 0x20) - 'a' + 10;

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
    private static readonly SpanStringSet KnownNames = new([
        "a", "abbr", "address", "article", "aside", "b", "base", "bdi", "blockquote", "body", "br", "caption", "cite", "code", "col", "colgroup", "data", "dd", "del", "dfn", "div",
        "dl", "dt", "em", "figcaption", "figure", "footer", "h1", "h2", "h3", "h4", "h5", "h6", "head", "header", "hgroup", "hr", "html", "i", "img",
        "ins", "kbd", "li", "link", "main", "menu", "meta", "nav", "noscript", "ol", "p", "pre", "q", "s", "samp", "script", "search", "section", "small", "span", "strong",
        "style", "sub", "sup", "table", "tbody", "td", "template", "tfoot", "th", "thead", "time", "title", "tr", "u", "ul", "var",
        "alt", "charset", "class", "colspan", "content", "datetime", "dir", "height", "hidden", "href", "id", "lang", "name", "reversed",
        "property", "rel", "rowspan", "src", "start", "target", "type", "value", "width",
    ]);

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
