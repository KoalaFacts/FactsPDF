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
        // Open svg elements: a title inside one is an svg element with ordinary content, not the
        // document's RCDATA title.
        var svgDepth = 0;
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
                if (name == "svg" && svgDepth > 0) svgDepth--;
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
                    while (i < html.Length && AttributeNameCharacter(html[i])) i++;
                    if (i == attrStart) throw Invalid("Malformed attribute.", i);
                    var attribute = LowerAttributeName(html, attrStart, i);
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
                        // A repeated attribute is a parse error the HTML parser recovers from by keeping
                        // the first, so a repeated style attribute leaves the first one's offsets.
                        if (AddAttribute(scratch, attribute, decoded))
                        {
                            styleValueOffset = rawValueStart;
                            styleOriginOffsets = offsets;
                        }
                    }
                    else
                    {
                        // The value of a repeated attribute is still decoded, so an invalid character
                        // reference in it fails as it would elsewhere.
                        _ = AddAttribute(scratch, attribute, Decode(value, attrStart));
                    }
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
            var rcdataTitle = name == "title" && svgDepth == 0;
            if (rcdataTitle) selfClosing = false; // the slash is ignored: title still opens its text
            if (name == "svg" && !selfClosing) svgDepth++;
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
            else if (rcdataTitle)
            {
                // RCDATA: markup inside title is literal text (character references still decode,
                // in the collecting pass), and the element ends at the first "</title" followed by
                // a delimiter. The self-closing slash is ignored, as in browsers.
                var end = i;
                while (true)
                {
                    cancellation.ThrowIfCancellationRequested();
                    end = html.IndexOf("</title", end, StringComparison.OrdinalIgnoreCase);
                    if (end < 0) throw Invalid("Unclosed title element.", start);
                    var after = end + 7;
                    if (after < html.Length && (Space(html[after]) || html[after] is '>' or '/')) break;
                    end = after;
                }
                if (end > i) yield return new(HtmlTokenKind.Text, html[i..end], false, i);
                i = end;
            }
            else if (name == "script")
            {
                // FactsPDF runs no scripts, so the data is skipped and never becomes a token. The
                // data ends where a browser's tokenizer ends it, which ignores a self-closing slash
                // on script and follows the escaped and double-escaped states.
                i = ScriptDataEnd(html, i, start, cancellation);
            }
        }
    }

    // The offset of the "</script" that ends the script data starting at `from`, following the
    // HTML tokenizer's script data states: "<!--" enters the escaped state, where a "<script"
    // (the double-escaped state) hides the next "</script" from the end-tag check, and "-->"
    // returns to the plain state. A "</script" in the double-escaped state only leaves it.
    private static int ScriptDataEnd(string html, int from, int elementStart, CancellationToken cancellation)
    {
        const int Plain = 0, Escaped = 1, DoubleEscaped = 2;
        var state = Plain; var dashes = 0;
        for (var i = from; i < html.Length;)
        {
            if ((i & 0xFFF) == 0) cancellation.ThrowIfCancellationRequested();
            var ch = html[i];
            if (ch == '<')
            {
                dashes = 0;
                if (state == Plain && html.AsSpan(i).StartsWith("<!--", StringComparison.Ordinal))
                {
                    state = Escaped; dashes = 2; i += 4;
                }
                else if (TagNameAt(html, i + 2, "script") && html[i + 1] == '/')
                {
                    if (state != DoubleEscaped) return i;
                    state = Escaped; i += 8;
                }
                else if (state == Escaped && TagNameAt(html, i + 1, "script"))
                {
                    state = DoubleEscaped; i += 7;
                }
                else i++;
            }
            else
            {
                if (ch == '-') dashes++;
                else if (ch == '>' && dashes >= 2 && state != Plain) { state = Plain; dashes = 0; }
                else dashes = 0;
                i++;
            }
        }
        throw Invalid("Unclosed script element.", elementStart);
    }

    // Whether `name` starts at `at` (ASCII case-insensitively) and is followed by a tag delimiter.
    private static bool TagNameAt(string html, int at, string name)
        => at + name.Length < html.Length
            && string.Compare(html, at, name, 0, name.Length, StringComparison.OrdinalIgnoreCase) == 0
            && (Space(html[at + name.Length]) || html[at + name.Length] is '>' or '/');

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
        if (!int.TryParse(number, hex ? NumberStyles.AllowHexSpecifier : NumberStyles.None,
                CultureInfo.InvariantCulture, out var scalar) || scalar <= 0 || !Rune.IsValid(scalar)) return null;
        // A reference to a C1 control reads as the windows-1252 character at that code (&#x85; is an
        // ellipsis), the way the HTML parser decodes it.
        if (scalar is >= 0x80 and <= 0x9F) scalar = Windows1252[scalar - 0x80];
        return new Rune(scalar);
    }

    private static readonly int[] Windows1252 =
    [
        0x20AC, 0x0081, 0x201A, 0x0192, 0x201E, 0x2026, 0x2020, 0x2021, 0x02C6, 0x2030, 0x0160, 0x2039, 0x0152, 0x008D, 0x017D, 0x008F,
        0x0090, 0x2018, 0x2019, 0x201C, 0x201D, 0x2022, 0x2013, 0x2014, 0x02DC, 0x2122, 0x0161, 0x203A, 0x0153, 0x009D, 0x017E, 0x0178
    ];

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
        "ins", "kbd", "li", "link", "main", "menu", "meta", "nav", "ol", "p", "pre", "q", "s", "samp", "script", "search", "section", "small", "span", "strong",
        "style", "sub", "sup", "table", "tbody", "td", "template", "tfoot", "th", "thead", "time", "title", "tr", "u", "ul", "var",
        "alt", "charset", "class", "colspan", "content", "datetime", "dir", "height", "hidden", "href", "id", "lang", "name", "reversed",
        "property", "rel", "rowspan", "src", "start", "target", "type", "value", "width",
    ]);

    private static string LowerName(string html, int start, int end) =>
        KnownNames.TryGetValue(html.AsSpan(start, end - start), out var known) ? known : html[start..end].ToLowerInvariant();

    // An attribute name is any run of characters except white space, controls, quotes, "<", ">", "/" and "="
    // (HTML), so data-* names such as data-a.b or data-café are names too. Tag names stay ASCII.
    private static bool AttributeNameCharacter(char ch)
        => ch > ' ' && ch is not ('"' or '\'' or '<' or '>' or '/' or '=') && !char.IsControl(ch) && !char.IsWhiteSpace(ch);

    // HTML lower-cases only ASCII letters in an attribute name.
    private static string LowerAttributeName(string html, int start, int end)
    {
        if (KnownNames.TryGetValue(html.AsSpan(start, end - start), out var known)) return known;
        var name = html[start..end];
        foreach (var ch in name)
            if (char.IsAsciiLetterUpper(ch)) return string.Create(name.Length, name, static (span, source) =>
            {
                for (var i = 0; i < span.Length; i++) span[i] = char.IsAsciiLetterUpper(source[i]) ? (char)(source[i] | 0x20) : source[i];
            });
        return name;
    }

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
