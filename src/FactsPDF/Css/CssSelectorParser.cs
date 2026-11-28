using System.Collections.Frozen;
using FactsPDF.CssSyntax;

namespace FactsPDF;

/// <summary>The @namespace declarations of one style sheet, as its selectors see them: the default namespace
/// and the namespace prefixes, which are case-sensitive.</summary>
internal sealed class CssNamespaces
{
    internal const string XHtml = "http://www.w3.org/1999/xhtml";
    internal const string Svg = "http://www.w3.org/2000/svg";
    private const string XLink = "http://www.w3.org/1999/xlink";
    private const string Xml = "http://www.w3.org/XML/1998/namespace";
    private const string Xmlns = "http://www.w3.org/2000/xmlns/";

    private Dictionary<string, string>? prefixes;

    /// <summary>The default namespace URI, or null while no unprefixed @namespace rule was read.</summary>
    internal string? Default { get; private set; }

    internal void Declare(string? prefix, string uri)
    {
        if (prefix is null) Default = uri;
        else (prefixes ??= new(StringComparer.Ordinal))[prefix] = uri;
    }

    /// <summary>Reads an @namespace rule, <c>@namespace [prefix] (url(...) | "...");</c>, into the table. It is valid only
    /// while <paramref name="open"/> (no rule but @charset, @import and @layer statements came before it) and when
    /// it ends with a semicolon; otherwise a browser drops it and the table is left as it was.</summary>
    internal static bool TryDeclare(CssAtRuleNode at, bool open, CssNamespaces table)
    {
        if (!open || at.Contents is not null) return false;
        var parts = at.Prelude.Where(x => x is not CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Whitespace }).ToArray();
        var index = 0; string? prefix = null;
        if (parts.Length > 0 && parts[0] is CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Ident } name)
        { prefix = name.Token.Value; index = 1; }
        if (parts.Length != index + 1) return false;
        var uri = parts[index] switch
        {
            CssTokenComponent { Token.Kind: CssSyntaxTokenKind.String or CssSyntaxTokenKind.Url } token => token.Token.Value,
            CssFunctionComponent function when function.Name.Equals("url", StringComparison.OrdinalIgnoreCase)
                && function.Values.Where(v => v is not CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Whitespace }).ToArray()
                    is [CssTokenComponent { Token.Kind: CssSyntaxTokenKind.String } text] => text.Token.Value,
            _ => null
        };
        if (uri is null) return false;
        table.Declare(prefix, uri);
        return true;
    }

    internal bool TryPrefix(string prefix, out string uri)
    {
        uri = "";
        return prefixes is not null && prefixes.TryGetValue(prefix, out uri!);
    }

    /// <summary>The elements a namespace URI selects: the HTML and SVG namespaces hold the document's elements,
    /// any other (MathML, an arbitrary URI, the empty one) none.</summary>
    internal static CssNamespaceFilter ElementFilter(string uri)
        => uri == XHtml ? CssNamespaceFilter.XHtml : uri == Svg ? CssNamespaceFilter.Svg : CssNamespaceFilter.Never;

    internal static CssAttributeNamespace AttributeNamespace(string uri)
        => uri == XLink ? CssAttributeNamespace.Xlink : uri == Xml ? CssAttributeNamespace.Xml
            : uri == Xmlns ? CssAttributeNamespace.Xmlns : CssAttributeNamespace.Never;
}

/// <summary>
/// Compiles a selector list from CSS Syntax components per Selectors Level 4 as Chrome parses it: complex
/// selectors with the four combinators, namespace prefixes, escaped identifiers, attribute selectors, the
/// pseudo-classes of a printed page, forgiving :is() and :where() lists, relative :has() lists, :not() with
/// selector lists, :nth-child(An+B of S) and the nesting selector. A selector Chrome rejects throws
/// FPDF1203 marked invalid (see <see cref="IsInvalid"/>): a non-strict conversion drops the whole rule, as
/// the browser does. A valid selector the renderer cannot honour (a pseudo-element other than ::before and
/// ::after) throws FPDF1203 unmarked and always fails.
/// </summary>
internal static class CssSelectorParser
{
    private const string InvalidMarker = "css-selector-invalid";
    private const int MaxDepth = 32, MaxListLength = 32, MaxCompounds = 256, MaxCompoundTerms = 32, MaxCompoundsPerSelector = 32;

    /// <summary>Whether the error says Chrome rejects the selector (the rule is dropped when non-strict) rather
    /// than that a valid selector is unsupported.</summary>
    internal static bool IsInvalid(FactsPdfException error) => error.Data.Contains(InvalidMarker);

    internal static FactsPdfException Invalid(string message, int offset)
    {
        var error = new FactsPdfException("FPDF1203", message, offset);
        error.Data[InvalidMarker] = true;
        return error;
    }

    private static FactsPdfException Unsupported(string message, int offset) => new("FPDF1203", message, offset);

    internal static CssSelector[] Compile(IReadOnlyList<CssSyntaxComponent> prelude, CssBudget budget, int offset,
        bool nested, CssNamespaces? namespaces)
    {
        var parser = new Parser(budget, namespaces, nested, offset);
        var list = parser.List(prelude, 0, prelude.Count, ListKind.Top, 0, false);
        if (list.Count == 0) throw Invalid("Expected a selector.", offset);
        return list.ToArray();
    }

    private enum ListKind { Top, Forgiving, Unforgiving, Relative }

    // ASCII case-insensitive keyword test; no other case mapping applies.
    private static bool Keyword(string text, string lower)
    {
        if (text.Length != lower.Length) return false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c is >= 'A' and <= 'Z') c = (char)(c + 32);
            if (c != lower[i]) return false;
        }
        return true;
    }

    // HTML documents match element and attribute names after lower-casing their ASCII letters only.
    private static string LowerAscii(string text)
    {
        foreach (var ch in text)
            if (ch is >= 'A' and <= 'Z')
                return string.Create(text.Length, text, static (buffer, source) =>
                {
                    for (var i = 0; i < source.Length; i++) buffer[i] = source[i] is >= 'A' and <= 'Z' ? (char)(source[i] + 32) : source[i];
                });
        return text;
    }

    private static bool IsSpace(CssSyntaxComponent item) => item is CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Whitespace };
    private static bool IsComma(CssSyntaxComponent item) => item is CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Comma };
    private static bool IsDelim(CssSyntaxComponent item, string value)
        => item is CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.Delim } token } && token.Value == value;
    private static bool IsIdent(CssSyntaxComponent item) => item is CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Ident };
    private static bool IsColon(CssSyntaxComponent item) => item is CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Colon };
    private static string IdentValue(CssSyntaxComponent item) => ((CssTokenComponent)item).Token.Value;
    private static bool IsNameOrStar(CssSyntaxComponent item) => IsIdent(item) || IsDelim(item, "*");

    private static CssCombinator? CombinatorAt(IReadOnlyList<CssSyntaxComponent> items, int i)
        => IsDelim(items[i], ">") ? CssCombinator.Child
            : IsDelim(items[i], "+") ? CssCombinator.NextSibling
            : IsDelim(items[i], "~") ? CssCombinator.SubsequentSibling : null;

    private static bool SkipSpace(IReadOnlyList<CssSyntaxComponent> items, ref int i, int end)
    {
        var any = false;
        while (i < end && IsSpace(items[i])) { i++; any = true; }
        return any;
    }

    // Pseudo-classes no printed page can satisfy: user actions, focus, links already visited, the URL fragment
    // target, fullscreen and modal state, open popovers, dialogs and details, media playback, interaction-driven
    // form validity, autofill, the shadow tree and custom element states, view transitions, and the states of
    // scrollbar parts. Every element the reader accepts is a plain content element, so the form states are in
    // the list too (see CssElementState).
    private static readonly FrozenSet<string> NeverMatching = new[]
    {
        "visited", "hover", "active", "focus", "focus-visible", "focus-within", "target", "target-current",
        "target-before", "target-after", "fullscreen", "-webkit-full-screen", "modal", "popover-open", "open",
        "playing", "paused", "muted", "seeking", "buffering", "stalled", "volume-locked", "picture-in-picture",
        "xr-overlay", "current", "past", "future", "user-valid", "user-invalid", "autofill", "-webkit-autofill",
        "host", "-webkit-drag", "active-view-transition", "window-inactive", "interest-source", "interest-target",
        "horizontal", "vertical", "decrement", "increment", "start", "end", "double-button", "single-button",
        "no-button", "corner-present",
        "enabled", "disabled", "checked", "required", "optional", "valid", "invalid", "default", "indeterminate",
        "in-range", "out-of-range", "placeholder-shown",
    }.ToFrozenSet(StringComparer.Ordinal);

    // The pseudo-elements Chrome 158 accepts without arguments (checked with CSSStyleSheet.replaceSync); every
    // vendor-prefixed -webkit- name is accepted too. ::before and ::after are the renderer's generated boxes and
    // ::marker, ::first-line and ::first-letter paint on a printed page, which the renderer does not model: they
    // are valid and fail as unsupported. Every other one (selection, placeholder, backdrop, scrollbars, spelling
    // and grammar errors, view transitions, shadow parts and the like) paints nothing in a printed static
    // document, so its rules never match.
    private static readonly FrozenSet<string> PlainPseudoElements = new[]
    {
        "after", "before", "backdrop", "file-selector-button", "first-letter", "first-line", "grammar-error", "marker",
        "placeholder", "selection", "spelling-error", "target-text", "view-transition", "details-content", "picker-icon",
        "checkmark", "column", "scroll-marker", "scroll-marker-group", "search-text",
    }.ToFrozenSet(StringComparer.Ordinal);

    // Pseudo-elements after which Chrome accepts user-action and similar pseudo-classes (every other one takes
    // only :is() and :where()).
    private static readonly FrozenSet<string> PseudoClassFriendlyElements = new[]
    {
        "file-selector-button", "details-content", "scroll-marker", "scroll-marker-group", "part", "scroll-button",
    }.ToFrozenSet(StringComparer.Ordinal);

    // The attributes whose values HTML compares ASCII case-insensitively on an HTML element.
    private static readonly FrozenSet<string> CaseInsensitiveValues = new[]
    {
        "accept", "accept-charset", "align", "alink", "axis", "bgcolor", "charset", "checked", "clear", "codetype",
        "color", "compact", "declare", "defer", "dir", "direction", "disabled", "enctype", "face", "frame", "hreflang",
        "http-equiv", "lang", "language", "link", "media", "method", "multiple", "nohref", "noresize", "noshade",
        "nowrap", "readonly", "rel", "rev", "rules", "scope", "scrolling", "selected", "shape", "target", "text",
        "type", "valign", "valuetype", "vlink",
    }.ToFrozenSet(StringComparer.Ordinal);

    private sealed class Parser(CssBudget budget, CssNamespaces? namespaces, bool nested, int offset)
    {
        private int compounds;
        // Set by Compound: a pseudo-element ended the compound (and so the selector); the name of a valid pseudo-element
        // the renderer paints but does not model.
        private bool afterPseudoElement;
        private string? unsupportedPseudoElement;
        private bool chainablePseudoElement;

        // A list of complex selectors separated by commas, in items[start..end).
        internal List<CssSelector> List(IReadOnlyList<CssSyntaxComponent> items, int start, int end, ListKind kind, int depth, bool inHas)
        {
            var result = new List<CssSelector>();
            var from = start;
            for (var i = start; i <= end; i++)
            {
                budget.Token.ThrowIfCancellationRequested();
                if (i < end && !IsComma(items[i])) continue;
                var segmentStart = from; var segmentEnd = i;
                from = i + 1;
                SkipSpace(items, ref segmentStart, segmentEnd);
                while (segmentEnd > segmentStart && IsSpace(items[segmentEnd - 1])) segmentEnd--;
                if (segmentStart == segmentEnd)
                {
                    // A forgiving list ignores an empty selector; every other list, and a rule's own, rejects it.
                    if (kind == ListKind.Forgiving) continue;
                    throw Invalid("Expected a selector before and after every comma.", offset);
                }
                if (depth == 0) { budget.Selector(items[segmentStart].Span.Start); compounds = 0; }
                else CssBudget.Check(result.Count < MaxListLength, "A functional pseudo-class may contain at most 32 selectors.", items[segmentStart].Span.Start);
                if (kind == ListKind.Forgiving)
                {
                    // An invalid selector of a forgiving list is dropped alone; the rest of the list still counts.
                    try { result.Add(Complex(items, segmentStart, segmentEnd, kind, depth, inHas)); }
                    catch (FactsPdfException error) when (IsInvalid(error)) { }
                }
                else result.Add(Complex(items, segmentStart, segmentEnd, kind, depth, inHas));
            }
            return result;
        }

        private CssSelector Complex(IReadOnlyList<CssSyntaxComponent> items, int start, int end, ListKind kind, int depth, bool inHas)
        {
            budget.Token.ThrowIfCancellationRequested();
            var top = depth == 0;
            var relative = kind == ListKind.Relative;
            var at = items[start].Span.Start;
            var parts = new List<CssCompound>();
            var combinator = CssCombinator.Descendant;
            var i = start;
            if (relative) parts.Add(new(null, [], [], [], [], CssCombinator.Descendant, Anchor: true));
            if (CombinatorAt(items, i) is { } lead)
            {
                if (!relative && !(top && nested)) throw Invalid("A selector cannot begin with a combinator.", at);
                // A nested rule's selector that begins with a combinator relates the rule's own element (an implied &).
                if (!relative) parts.Add(new(null, [], [], [], [], CssCombinator.Descendant, Nesting: true, Namespace: DefaultFilter));
                combinator = lead;
                i++;
                SkipSpace(items, ref i, end);
                if (i >= end) throw Invalid("A combinator needs a selector after it.", at);
            }
            var pseudoElement = CssPseudoElement.None;
            unsupportedPseudoElement = null;
            while (true)
            {
                CssBudget.Check(parts.Count < MaxCompoundsPerSelector + 2, "A selector may contain at most 32 compounds.", at);
                CssBudget.Check(++compounds <= MaxCompounds, "A selector, with the arguments of its functional pseudo-classes, may contain at most 256 compounds.", at);
                var compound = Compound(items, ref i, end, combinator, kind, depth, inHas, ref pseudoElement);
                parts.Add(compound);
                if (afterPseudoElement)
                {
                    SkipSpace(items, ref i, end);
                    if (i < end) throw Invalid("A pseudo-element must end its selector.", items[i].Span.Start);
                    break;
                }
                var space = SkipSpace(items, ref i, end);
                if (i >= end) break;
                if (CombinatorAt(items, i) is { } next)
                {
                    combinator = next;
                    i++;
                    SkipSpace(items, ref i, end);
                    if (i >= end) throw Invalid("A combinator needs a selector after it.", at);
                }
                else if (space) combinator = CssCombinator.Descendant;
                else throw Invalid("Unsupported or malformed selector/combinator.", items[i].Span.Start);
            }
            // A valid pseudo-element the renderer paints but does not model fails, once the selector is known to be valid.
            if (unsupportedPseudoElement is { } unsupported)
                throw Unsupported($"The ::{unsupported} pseudo-element is not supported (only ::before and ::after).", at);
            var selector = new CssSelector(parts.ToArray(), at, pseudoElement);
            // A relative selector without a nesting selector is a descendant of the parent rule's element.
            if (top && nested && !relative && selector.NestingCount == 0)
            {
                parts.Insert(0, new(null, [], [], [], [], CssCombinator.Descendant, Nesting: true, Namespace: DefaultFilter));
                selector = new CssSelector(parts.ToArray(), at, pseudoElement);
            }
            return selector;
        }

        private CssNamespaceFilter DefaultFilter
            => namespaces?.Default is { } uri ? CssNamespaces.ElementFilter(uri) : CssNamespaceFilter.Any;

        private CssCompound Compound(IReadOnlyList<CssSyntaxComponent> items, ref int i, int end, CssCombinator combinator,
            ListKind kind, int depth, bool inHas, ref CssPseudoElement pseudoElement)
        {
            string? type = null;
            var filter = DefaultFilter;
            var terms = 0;
            var ids = new List<string>(); var classes = new List<string>();
            var attributes = new List<CssAttributeMatch>(); List<CssPseudoClass>? pseudos = null;
            var nesting = false;
            var at = items[i].Span.Start;
            afterPseudoElement = false;
            var friendly = false;
            // A type or universal selector, with an optional namespace prefix: name, ns|name, *|name or |name.
            if (IsDelim(items[i], "|") && i + 1 < end && IsNameOrStar(items[i + 1]))
            {
                filter = CssNamespaceFilter.Never;
                i++;
                type = TypeName(items[i++]);
                terms++;
            }
            else if (IsNameOrStar(items[i]))
            {
                if (i + 2 < end && IsDelim(items[i + 1], "|") && IsNameOrStar(items[i + 2]))
                {
                    filter = items[i] is CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Ident } prefix
                        ? CssNamespaces.ElementFilter(ResolvePrefix(prefix))
                        : CssNamespaceFilter.Any;
                    i += 2;
                    type = TypeName(items[i++]);
                }
                else type = TypeName(items[i++]);
                terms++;
            }
            while (i < end)
            {
                var item = items[i];
                if (IsSpace(item)) break;
                if (afterPseudoElement)
                {
                    // Only :is() and :where() (and, after some pseudo-elements, user-action states) may follow one.
                    if (IsColon(item) && i + 1 < end && AfterPseudoElement(items, ref i, end, friendly, depth, inHas))
                    {
                        (pseudos ??= []).Add(new(CssPseudoKind.Never));
                        continue;
                    }
                    break;
                }
                if (item is CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.Hash } hash })
                {
                    if (!hash.HashIsId) throw Invalid("An ID selector must be an identifier.", item.Span.Start);
                    ids.Add(hash.Value); i++;
                }
                else if (IsDelim(item, "."))
                {
                    i++;
                    if (i >= end || !IsIdent(items[i])) throw Invalid("Expected a class identifier adjacent to '.'.", item.Span.Start);
                    classes.Add(IdentValue(items[i++]));
                }
                else if (item is CssBlockComponent { Opening: CssSyntaxTokenKind.OpenSquare } block)
                {
                    attributes.Add(Attribute(block)); i++;
                }
                else if (IsDelim(item, "&"))
                {
                    nesting = true; i++;
                }
                else if (IsColon(item))
                {
                    i++;
                    if (i >= end) throw Invalid("Expected a pseudo-class name after ':'.", item.Span.Start);
                    if (IsColon(items[i]))
                    {
                        i++;
                        pseudoElement = PseudoElement(items, ref i, end, double_: true, kind, depth, inHas, item.Span.Start, out friendly);
                        if (pseudoElement == CssPseudoElement.None) (pseudos ??= []).Add(new(CssPseudoKind.Never));
                        afterPseudoElement = true;
                        terms++;
                        continue;
                    }
                    // ::before and ::after, and the legacy :before, :after, :first-line and :first-letter, are pseudo-elements.
                    var written = items[i] is CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Ident } word ? LowerAscii(word.Token.Value) : null;
                    if (written is "before" or "after" or "first-line" or "first-letter")
                    {
                        pseudoElement = PseudoElement(items, ref i, end, double_: false, kind, depth, inHas, item.Span.Start, out friendly);
                        if (pseudoElement == CssPseudoElement.None) (pseudos ??= []).Add(new(CssPseudoKind.Never));
                        afterPseudoElement = true;
                        terms++;
                        continue;
                    }
                    (pseudos ??= []).Add(PseudoClass(items, ref i, depth, inHas, item.Span.Start));
                }
                else break;
                CssBudget.Check(++terms <= MaxCompoundTerms, "A compound selector may contain at most 32 simple terms.", at);
            }
            if (terms == 0 && !nesting) throw Invalid("Expected a selector.", at);
            return new(type, ids.ToArray(), classes.ToArray(), attributes.ToArray(), pseudos?.ToArray() ?? [], combinator, nesting, filter);
        }

        private static string? TypeName(CssSyntaxComponent item)
            => IsDelim(item, "*") ? null : LowerAscii(IdentValue(item));

        private string ResolvePrefix(CssSyntaxComponent prefix)
        {
            var name = IdentValue(prefix);
            if (namespaces is not null && namespaces.TryPrefix(name, out var uri)) return uri;
            throw Invalid($"The namespace prefix '{name}' is not declared by an @namespace rule.", prefix.Span.Start);
        }

        // The pseudo-element at items[i] (after its colons). ::before and ::after, also written with one colon, are the
        // renderer's generated boxes (returned); ::marker, ::first-line and ::first-letter are remembered as unsupported;
        // every other valid pseudo-element paints nothing on a printed page (None: the selector never matches).
        private CssPseudoElement PseudoElement(IReadOnlyList<CssSyntaxComponent> items, ref int i, int end, bool double_,
            ListKind kind, int depth, bool inHas, int colon, out bool friendly)
        {
            friendly = false;
            if (i >= end) throw Invalid("Expected a pseudo-element name.", colon);
            var item = items[i++];
            string name;
            var function = false;
            if (item is CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Ident } ident) name = LowerAscii(ident.Token.Value);
            else if (item is CssFunctionComponent call) { name = LowerAscii(call.Name); function = true; }
            else throw Invalid("Expected a pseudo-element name.", colon);
            // A pseudo-element belongs to the rule's own selector, never to the arguments of a functional pseudo-class.
            if (depth > 0 || kind == ListKind.Relative) throw Invalid("A pseudo-element is not allowed here.", colon);
            if (!function && name is "before" or "after")
                return name == "before" ? CssPseudoElement.Before : CssPseudoElement.After;
            if (function)
            {
                if (!ValidFunctionalPseudoElement(name, (CssFunctionComponent)item, depth, inHas))
                    throw Invalid($"Unknown or malformed pseudo-element ::{name}().", colon);
            }
            else if (!(name.StartsWith("-webkit-", StringComparison.Ordinal) || PlainPseudoElements.Contains(name)))
                throw Invalid($"Unknown pseudo-element ::{name}.", colon);
            // The legacy single-colon spelling exists only for ::before, ::after, ::first-line and ::first-letter.
            if (!double_ && !(name is "first-line" or "first-letter")) throw Invalid("Unknown pseudo-class.", colon);
            friendly = name.StartsWith("-webkit-", StringComparison.Ordinal) || PseudoClassFriendlyElements.Contains(name);
            chainablePseudoElement = name is "slotted" or "part" or "details-content";
            if (name is "marker" or "first-line" or "first-letter") unsupportedPseudoElement = name;
            return CssPseudoElement.None;
        }

        // The arguments Chrome 158 accepts for a functional pseudo-element: ::cue() and ::slotted() take one compound
        // selector, ::part() one or more identifiers, ::highlight() and the view-transition ones an identifier or *,
        // ::scroll-button() only *.
        private bool ValidFunctionalPseudoElement(string name, CssFunctionComponent call, int depth, bool inHas)
        {
            var values = call.Values.Where(v => !IsSpace(v)).ToArray();
            switch (name)
            {
                case "cue" or "slotted":
                    var selectors = List(call.Values, 0, call.Values.Count, ListKind.Unforgiving, depth + 1, inHas);
                    return selectors.Count == 1 && selectors[0].Parts.Length == 1;
                case "part":
                    return values.Length > 0 && values.All(IsIdent);
                case "highlight" or "view-transition-group" or "view-transition-image-pair" or "view-transition-new" or "view-transition-old":
                    return values.Length == 1 && IsNameOrStar(values[0]);
                case "scroll-button":
                    return values.Length == 1 && IsDelim(values[0], "*");
                default:
                    return false;
            }
        }

        // Consumes a pseudo-class that may follow a pseudo-element: :is() and :where() (whose arguments cannot match
        // inside a pseudo-element, so the compound never matches), and a user-action state after the pseudo-elements
        // that take them. i is at the colon; it is left there when nothing is consumed.
        private bool AfterPseudoElement(IReadOnlyList<CssSyntaxComponent> items, ref int i, int end, bool friendly, int depth, bool inHas)
        {
            var next = items[i + 1];
            // ::slotted(), ::part() and ::details-content may be followed by ::before or ::after.
            if (chainablePseudoElement && IsColon(next) && i + 2 < end
                && items[i + 2] is CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Ident } generated
                && LowerAscii(generated.Token.Value) is "before" or "after")
            {
                chainablePseudoElement = false;
                i += 3;
                return true;
            }
            if (next is CssFunctionComponent function && LowerAscii(function.Name) is "is" or "where")
            {
                CssBudget.Check(depth < MaxDepth, "Functional pseudo-classes may nest at most 32 levels.", function.Span.Start);
                _ = List(function.Values, 0, function.Values.Count, ListKind.Forgiving, depth + 1, inHas);
                i += 2;
                return true;
            }
            if (!friendly || next is not CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Ident } ident
                || !(NeverMatching.Contains(LowerAscii(ident.Token.Value)) || LowerAscii(ident.Token.Value) is "link" or "any-link" or "read-only" or "read-write" or "defined"))
                return false;
            i += 2;
            return true;
        }

        // A pseudo-class: items[i] is its name (an identifier, or a function holding its arguments).
        private CssPseudoClass PseudoClass(IReadOnlyList<CssSyntaxComponent> items, ref int i, int depth, bool inHas, int colon)
        {
            var item = items[i++];
            if (item is CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Ident } ident)
            {
                var name = LowerAscii(ident.Token.Value);
                return name switch
                {
                    "first-child" => new(CssPseudoKind.NthChild, 0, 1),
                    "last-child" => new(CssPseudoKind.NthLastChild, 0, 1),
                    "only-child" => new(CssPseudoKind.OnlyChild),
                    "first-of-type" => new(CssPseudoKind.NthOfType, 0, 1),
                    "last-of-type" => new(CssPseudoKind.NthLastOfType, 0, 1),
                    "only-of-type" => new(CssPseudoKind.OnlyOfType),
                    "empty" => new(CssPseudoKind.Empty),
                    "root" or "scope" => new(CssPseudoKind.Root),
                    "link" or "any-link" or "-webkit-any-link" => new(CssPseudoKind.Link),
                    "read-only" => new(CssPseudoKind.ReadOnly),
                    "read-write" => new(CssPseudoKind.ReadWrite),
                    "defined" => new(CssPseudoKind.Always),
                    _ when NeverMatching.Contains(name) => new(CssPseudoKind.Never),
                    _ => throw Invalid($"Unknown pseudo-class ':{ident.Token.Value}'.", colon)
                };
            }
            if (item is not CssFunctionComponent function)
                throw Invalid("Expected a pseudo-class name after ':'.", colon);
            var arguments = function.Values;
            var fname = LowerAscii(function.Name);
            CssBudget.Check(depth < MaxDepth, "Functional pseudo-classes may nest at most 32 levels.", function.Span.Start);
            switch (fname)
            {
                case "is" or "where":
                    return new(fname == "is" ? CssPseudoKind.Is : CssPseudoKind.Where,
                        Selectors: List(arguments, 0, arguments.Count, ListKind.Forgiving, depth + 1, inHas).ToArray());
                case "not" or "-webkit-any":
                    var listed = Required(List(arguments, 0, arguments.Count, ListKind.Unforgiving, depth + 1, inHas), function);
                    // The legacy :-webkit-any() takes compound selectors only.
                    if (fname != "not" && listed.Any(selector => selector.Parts.Length != 1))
                        throw Invalid(":-webkit-any() takes compound selectors.", function.Span.Start);
                    return new(fname == "not" ? CssPseudoKind.Not : CssPseudoKind.Is, Selectors: listed);
                case "has":
                    if (inHas) throw Invalid(":has() cannot be nested in :has().", function.Span.Start);
                    return new(CssPseudoKind.Has,
                        Selectors: Required(List(arguments, 0, arguments.Count, ListKind.Relative, depth + 1, true), function));
                case "lang":
                    return new(CssPseudoKind.Lang, Text: SingleIdent(arguments, function));
                case "dir":
                    return new(Keyword(SingleIdent(arguments, function), "ltr") ? CssPseudoKind.Always : CssPseudoKind.Never);
                case "state":
                    SingleIdent(arguments, function);
                    return new(CssPseudoKind.Never);
                case "active-view-transition-type":
                    // Comma-separated identifiers; no view transition runs on a printed page.
                    if (!arguments.Any(a => IsIdent(a)) || arguments.Any(a => !(IsIdent(a) || IsSpace(a) || IsComma(a))))
                        throw Invalid(":active-view-transition-type() takes identifiers.", function.Span.Start);
                    return new(CssPseudoKind.Never);
                case "host" or "host-context":
                    // The arguments must be a selector (checked, never matched: a document has no shadow host).
                    var hostArgument = Required(List(arguments, 0, arguments.Count, ListKind.Unforgiving, depth + 1, inHas), function);
                    if (hostArgument.Length != 1 || fname == "host" && hostArgument[0].Parts.Length != 1)
                        throw Invalid($":{fname}() takes one selector.", function.Span.Start);
                    return new(CssPseudoKind.Never);
                case "nth-child" or "nth-last-child":
                    return Nth(function, fname == "nth-child" ? CssPseudoKind.NthChild : CssPseudoKind.NthLastChild, true, depth, inHas);
                case "nth-of-type" or "nth-last-of-type":
                    return Nth(function, fname == "nth-of-type" ? CssPseudoKind.NthOfType : CssPseudoKind.NthLastOfType, false, depth, inHas);
                default:
                    throw Invalid($"Unknown pseudo-class ':{function.Name}()'.", colon);
            }
        }

        private static CssSelector[] Required(List<CssSelector> list, CssFunctionComponent function)
            => list.Count > 0 ? list.ToArray() : throw Invalid($":{function.Name}() requires a selector.", function.Span.Start);

        // :lang() and :dir() take exactly one identifier (Chrome 158 accepts no list, string or wildcard).
        private static string SingleIdent(IReadOnlyList<CssSyntaxComponent> arguments, CssFunctionComponent function)
        {
            string? value = null;
            foreach (var argument in arguments)
            {
                if (IsSpace(argument)) continue;
                if (value is not null || !IsIdent(argument)) throw Invalid($":{function.Name}() takes one identifier.", function.Span.Start);
                value = IdentValue(argument);
            }
            return value ?? throw Invalid($":{function.Name}() takes one identifier.", function.Span.Start);
        }

        private CssPseudoClass Nth(CssFunctionComponent function, CssPseudoKind kind, bool allowOf, int depth, bool inHas)
        {
            var arguments = function.Values;
            // "of S": the first lower-case "of" identifier ends the An+B part; S is an ordinary selector list.
            var of = -1;
            for (var k = 0; k < arguments.Count; k++)
                if (arguments[k] is CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.Ident, Value: "of" } }) { of = k; break; }
            if (of >= 0 && !allowOf) throw Invalid($":{function.Name}() has no \"of S\" form.", function.Span.Start);
            var (a, b) = ParseNth(arguments, of >= 0 ? of : arguments.Count, function.Span.Start);
            if (of < 0) return new(kind, a, b);
            var selectors = Required(List(arguments, of + 1, arguments.Count, ListKind.Unforgiving, depth + 1, inHas), function);
            return new(kind, a, b, selectors);
        }

        // [name], [name=value] and the other operators, with an optional namespace prefix on the name and an i flag.
        private CssAttributeMatch Attribute(CssBlockComponent block)
        {
            var values = block.Values;
            var at = block.Span.Start;
            var i = 0; var end = values.Count;
            while (end > 0 && IsSpace(values[end - 1])) end--;
            SkipSpace(values, ref i, end);
            if (i >= end) throw Invalid("Expected an attribute name.", at);
            var attributeNamespace = CssAttributeNamespace.None;
            // [name], [ns|name], [*|name] and [|name]; a lone | followed by = is the |= operator instead.
            bool NamespaceBar(int k) => IsDelim(values[k], "|") && k + 1 < end && IsIdent(values[k + 1]);
            if (NamespaceBar(i))
            {
                i++;
            }
            else if (IsDelim(values[i], "*") && i + 1 < end && NamespaceBar(i + 1))
            {
                attributeNamespace = CssAttributeNamespace.Any;
                i += 2;
            }
            else if (IsIdent(values[i]) && i + 1 < end && NamespaceBar(i + 1))
            {
                attributeNamespace = CssNamespaces.AttributeNamespace(ResolvePrefix(values[i]));
                i += 2;
            }
            if (i >= end || !IsIdent(values[i])) throw Invalid("Expected an attribute name.", at);
            var name = LowerAscii(IdentValue(values[i++]));
            SkipSpace(values, ref i, end);
            var op = CssAttributeOperator.Presence;
            string? expected = null;
            var ignoreCase = false;
            if (i < end)
            {
                if (IsDelim(values[i], "="))
                {
                    op = CssAttributeOperator.Equals; i++;
                }
                else
                {
                    op = values[i] switch
                    {
                        _ when IsDelim(values[i], "^") => CssAttributeOperator.StartsWith,
                        _ when IsDelim(values[i], "$") => CssAttributeOperator.EndsWith,
                        _ when IsDelim(values[i], "*") => CssAttributeOperator.Contains,
                        _ when IsDelim(values[i], "~") => CssAttributeOperator.Includes,
                        _ when IsDelim(values[i], "|") => CssAttributeOperator.Dash,
                        _ => throw Invalid("Malformed attribute selector.", at)
                    };
                    // CSS Syntax supplies separate delim tokens for ^=, $=, *=, ~= and |=; no whitespace may separate them.
                    if (i + 1 >= end || !IsDelim(values[i + 1], "=")) throw Invalid("Expected an adjacent '=' in the attribute operator.", values[i].Span.Start);
                    i += 2;
                }
                SkipSpace(values, ref i, end);
                if (i >= end || !(IsIdent(values[i]) || values[i] is CssTokenComponent { Token.Kind: CssSyntaxTokenKind.String }))
                    throw Invalid("Attribute comparison requires an identifier or quoted string.", at);
                expected = ((CssTokenComponent)values[i++]).Token.Value;
                SkipSpace(values, ref i, end);
                if (i < end)
                {
                    // Chrome 158 knows the i flag only; the s flag makes the selector invalid.
                    if (!IsIdent(values[i]) || !Keyword(IdentValue(values[i]), "i"))
                        throw Invalid("Only the i attribute-value flag is supported.", values[i].Span.Start);
                    ignoreCase = true; i++;
                    SkipSpace(values, ref i, end);
                    if (i < end) throw Invalid("Malformed attribute selector.", values[i].Span.Start);
                }
            }
            return new(name, op, expected, ignoreCase, attributeNamespace,
                attributeNamespace == CssAttributeNamespace.None && CaseInsensitiveValues.Contains(name));
        }
    }

    // The An+B microsyntax of CSS Syntax 3: odd, even, B, An and An+B with the sign and
    // whitespace rules of its token grammar, over the first `count` components of the argument.
    private static (long A, long B) ParseNth(IReadOnlyList<CssSyntaxComponent> arguments, int count, int offset)
    {
        const string Malformed = "Malformed An+B argument.";
        Span<int> found = stackalloc int[4];
        Span<bool> spaced = stackalloc bool[4];
        var tokens = 0; var space = false;
        for (var k = 0; k < count; k++)
        {
            if (arguments[k] is not CssTokenComponent { Token: var token })
                throw Invalid(Malformed, arguments[k].Span.Start);
            if (token.Kind == CssSyntaxTokenKind.Whitespace) { space = true; continue; }
            if (tokens == found.Length) throw Invalid(Malformed, token.Span.Start);
            found[tokens] = k; spaced[tokens++] = space; space = false;
        }
        if (tokens == 0) throw Invalid("An+B argument is empty.", offset);
        var first = TokenAt(arguments, found[0]);
        long a; string form; var next = 1;
        switch (first.Kind)
        {
            case CssSyntaxTokenKind.Ident when tokens == 1 && Keyword(first.Value, "odd"): return (2, 1);
            case CssSyntaxTokenKind.Ident when tokens == 1 && Keyword(first.Value, "even"): return (2, 0);
            case CssSyntaxTokenKind.Ident:
                a = first.Value.StartsWith('-') ? -1 : 1;
                form = a < 0 ? first.Value[1..] : first.Value;
                break;
            case CssSyntaxTokenKind.Delim when first.Value == "+" && tokens > 1 && !spaced[1]
                && TokenAt(arguments, found[1]) is { Kind: CssSyntaxTokenKind.Ident } plus && !plus.Value.StartsWith('-'):
                a = 1; form = plus.Value; next = 2;
                break;
            case CssSyntaxTokenKind.Number when first.IsInteger && tokens == 1:
                return (0, Integer(first.Value, first.Span.Start));
            case CssSyntaxTokenKind.Dimension when first.IsInteger:
                a = Integer(first.Value, first.Span.Start); form = first.Unit!;
                break;
            default: throw Invalid(Malformed, first.Span.Start);
        }
        if (form.Length == 0 || form[0] is not ('n' or 'N')) throw Invalid(Malformed, first.Span.Start);
        var rest = tokens - next;
        if (form.Length == 1)
        {
            if (rest == 0) return (a, 0);
            var b = TokenAt(arguments, found[next]);
            if (rest == 1 && IsInteger(b, signed: true)) return (a, Integer(b.Value, b.Span.Start));
            if (rest == 2 && b.Kind == CssSyntaxTokenKind.Delim && b.Value is "+" or "-"
                && TokenAt(arguments, found[next + 1]) is var magnitude && IsInteger(magnitude, signed: false))
            {
                var value = Integer(magnitude.Value, magnitude.Span.Start);
                return (a, b.Value == "-" ? -value : value);
            }
            throw Invalid(Malformed, b.Span.Start);
        }
        if (form[1] != '-') throw Invalid(Malformed, first.Span.Start);
        if (form.Length == 2)
        {
            if (rest == 1 && TokenAt(arguments, found[next]) is var b && IsInteger(b, signed: false))
                return (a, -Integer(b.Value, b.Span.Start));
            throw Invalid(Malformed, first.Span.Start);
        }
        for (var k = 2; k < form.Length; k++)
            if (!char.IsAsciiDigit(form[k])) throw Invalid(Malformed, first.Span.Start);
        if (rest != 0) throw Invalid(Malformed, TokenAt(arguments, found[next]).Span.Start);
        return (a, -Integer(form[2..], first.Span.Start));
    }

    private static CssSyntaxToken TokenAt(IReadOnlyList<CssSyntaxComponent> arguments, int index)
        => ((CssTokenComponent)arguments[index]).Token;

    private static bool IsInteger(CssSyntaxToken token, bool signed)
        => token.Kind == CssSyntaxTokenKind.Number && token.IsInteger && (token.Value[0] is '+' or '-') == signed;

    // An optionally signed run of ASCII digits; reading stops as soon as the value passes the bound.
    private static long Integer(string text, int offset)
    {
        var negative = text[0] == '-';
        var k = text[0] is '+' or '-' ? 1 : 0;
        long value = 0;
        for (; k < text.Length; k++)
        {
            if (!char.IsAsciiDigit(text[k])) throw Invalid("Malformed An+B argument.", offset);
            value = value * 10 + (text[k] - '0');
            if (value > CssSelector.MaxNthValue)
                throw Unsupported("An+B values beyond ±1000000000 are not supported.", offset);
        }
        return negative ? -value : value;
    }
}
