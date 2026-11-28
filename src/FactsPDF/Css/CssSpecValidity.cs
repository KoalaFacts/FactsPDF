using System.Collections.Frozen;
using System.Globalization;
using FactsPDF.CssSyntax;

namespace FactsPDF;

/// <summary>
/// Decides, for non-strict mode, whether CSS the renderer rejected is invalid per the CSS
/// specifications, so a browser drops it during parsing (CSS Syntax 3 and CSS Cascade error
/// handling), or may be valid CSS the renderer does not implement. Every check answers "proven
/// invalid" or "not proven": a grammar here accepts a superset of what the specifications and
/// Google Chrome accept (Chrome extensions, legacy -webkit- values, quirks-mode unitless lengths and
/// hashless colours, and any known function treated as matching any value type), so a declaration,
/// selector or at-rule it reports invalid is dropped by every browser, and anything it cannot prove
/// invalid keeps failing as in strict mode. Properties without a grammar here are never proven
/// invalid by value.
/// </summary>
internal static class CssSpecValidity
{
    // Values with more components than this are not examined (not proven invalid), which bounds
    // the work a hostile declaration can cause.
    private const int MaxTerms = 256;

    /// <summary>Whether a declaration is invalid per the specifications: an unknown property name, a
    /// malformed var(), a value holding a bad string, bad URL or unmatched closing bracket, a CSS-wide
    /// keyword combined with other components, an empty value, or a value its property's grammar
    /// cannot match.</summary>
    /// <param name="writtenName">The property name as written (any case).</param>
    /// <param name="canonicalName">The name after the renderer's alias normalization (lower case).</param>
    /// <param name="values">The declaration's value components, without <c>!important</c>.</param>
    internal static bool InvalidDeclaration(string writtenName, string canonicalName, IReadOnlyList<CssSyntaxComponent> values)
    {
        if (HasInvalidTokens(values) || HasMalformedVar(values)) return true;
        if (CssVariables.IsCustomPropertyName(writtenName)) return false;
        if (!CssKnownProperties.Contains(writtenName.ToLowerInvariant())) return true;
        if (CssVariables.ContainsVar(values)) return false;
        var terms = new List<Term>();
        if (!Terms(values, terms)) return false;
        if (terms.Count == 0) return true;
        if (canonicalName == "font") return InvalidFont(values);
        var wide = 0;
        foreach (var term in terms) if (term.Kind == TermKind.Ident && WideKeyword(term.Text)) wide++;
        // Chrome reads a family list holding a CSS-wide keyword as one family name.
        if (wide > 0 && canonicalName != "font-family") return terms.Count > 1;
        if (!Grammars.TryGetValue(canonicalName, out var grammar)) return false;
        return !Matches(grammar, terms);
    }

    // The font shorthand is invalid when its structure does not parse or when a part is not a valid value of
    // its longhand (checked with that longhand's grammar, so the two agree except where noted). System font keywords
    // and CSS-wide keywords are valid.
    private static bool InvalidFont(IReadOnlyList<CssSyntaxComponent> values)
    {
        var split = CssFontShorthand.Parse(values);
        if (split.Form == CssFontShorthand.Form.Invalid) return true;
        if (split.Form != CssFontShorthand.Form.Longhands) return false;
        var family = CssFontShorthand.Slice(values, split.Family);
        return InvalidDeclaration("font-size", "font-size", CssFontShorthand.Slice(values, split.Size))
            || split.LineHeight.Present && InvalidDeclaration("line-height", "line-height", CssFontShorthand.Slice(values, split.LineHeight))
            || InvalidDeclaration("font-family", "font-family", family);
    }

    /// <summary>Whether component values hold a bad string, a bad URL or an unmatched closing
    /// parenthesis, bracket or brace at any depth: no property or custom property accepts them.</summary>
    internal static bool HasInvalidTokens(IReadOnlyList<CssSyntaxComponent> values)
    {
        for (var i = 0; i < values.Count; i++)
        {
            switch (values[i])
            {
                case CssTokenComponent { Token.Kind: CssSyntaxTokenKind.BadString or CssSyntaxTokenKind.BadUrl
                    or CssSyntaxTokenKind.CloseParen or CssSyntaxTokenKind.CloseSquare or CssSyntaxTokenKind.CloseBrace }:
                    return true;
                case CssFunctionComponent function when HasInvalidTokens(function.Values): return true;
                case CssBlockComponent block when HasInvalidTokens(block.Values): return true;
            }
        }
        return false;
    }

    private static bool HasMalformedVar(IReadOnlyList<CssSyntaxComponent> values)
    {
        try { CssVariables.ValidateReferences(values); return false; }
        catch (FactsPdfException) { return true; }
    }

    /// <summary>Whether an at-rule name is unknown to the specifications and to Chrome; such a rule
    /// (with its block, or up to its semicolon) is dropped by every browser.</summary>
    internal static bool UnknownAtRule(string name) => !KnownAtRules.Contains(name.ToLowerInvariant());

    private static readonly FrozenSet<string> KnownAtRules = new[]
    {
        "charset", "import", "namespace", "media", "supports", "font-face", "font-feature-values",
        "font-palette-values", "counter-style", "keyframes", "-webkit-keyframes", "page", "layer",
        "container", "property", "scope", "starting-style", "view-transition", "position-try",
        "custom-media", "custom-selector", "color-profile", "function", "mixin", "apply", "contents",
        "when", "else", "document", "viewport", "-ms-viewport", "-webkit-viewport", "stylistic",
        "historical-forms", "styleset", "character-variant", "swash", "ornaments", "annotation",
        "top-left-corner", "top-left", "top-center", "top-right", "top-right-corner", "bottom-left-corner",
        "bottom-left", "bottom-center", "bottom-right", "bottom-right-corner", "left-top", "left-middle",
        "left-bottom", "right-top", "right-middle", "right-bottom", "footnote", "route", "nest",
    }.ToFrozenSet(StringComparer.Ordinal);

    private static bool WideKeyword(string lower) =>
        lower is "inherit" or "initial" or "unset" or "revert" or "revert-layer";

    // ---- Selectors ----

    private enum Verdict { Valid, Unknown, Invalid }

    /// <summary>Whether a style rule's selector list is invalid per Selectors 4: an empty selector,
    /// a leading, trailing or doubled combinator, a token that cannot start a simple selector, an ID
    /// that is not an identifier, a malformed attribute selector, an unknown pseudo-class or
    /// pseudo-element, or a selector after a pseudo-element. A browser drops the whole rule.</summary>
    internal static bool InvalidSelectorList(IReadOnlyList<CssSyntaxComponent> prelude)
    {
        var start = 0;
        for (var i = 0; i <= prelude.Count; i++)
        {
            if (i < prelude.Count && prelude[i] is not CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Comma }) continue;
            if (ComplexSelector(prelude, start, i) == Verdict.Invalid) return true;
            start = i + 1;
        }
        return false;
    }

    private static Verdict ComplexSelector(IReadOnlyList<CssSyntaxComponent> items, int start, int end)
    {
        var result = Verdict.Valid;
        var compounds = 0; var inCompound = false; var combinator = false; var afterPseudoElement = false;
        for (var i = start; i < end; i++)
        {
            var item = items[i];
            if (item is CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Whitespace }) { inCompound = false; continue; }
            if (item is CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.Delim, Value: ">" or "+" or "~" } })
            {
                if (compounds == 0 || combinator) return Verdict.Invalid;
                combinator = true; inCompound = false;
                continue;
            }
            var startsCompound = !inCompound;
            if (startsCompound)
            {
                // A compound after a pseudo-element is a descendant of it: never valid.
                if (afterPseudoElement) return Verdict.Invalid;
                compounds++; combinator = false; inCompound = true;
            }
            switch (item)
            {
                case CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.Delim, Value: "|" or "&" } }:
                    // Namespace prefixes and the nesting selector are not examined here.
                    return Verdict.Unknown;
                case CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.Delim, Value: "*" } }:
                case CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Ident }:
                    if (!startsCompound) return Verdict.Invalid;
                    break;
                case CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.Delim, Value: "." } }:
                    if (afterPseudoElement) return Verdict.Invalid;
                    if (i + 1 >= end || items[i + 1] is not CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Ident })
                        return Verdict.Invalid;
                    i++;
                    break;
                case CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.Hash } hash }:
                    if (afterPseudoElement || !hash.HashIsId) return Verdict.Invalid;
                    break;
                case CssBlockComponent { Opening: CssSyntaxTokenKind.OpenSquare } attribute:
                    if (afterPseudoElement) return Verdict.Invalid;
                    var verdict = AttributeSelector(attribute.Values);
                    if (verdict == Verdict.Invalid) return verdict;
                    if (verdict == Verdict.Unknown) result = verdict;
                    break;
                case CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Colon }:
                    if (i + 1 >= end) return Verdict.Invalid;
                    var next = items[++i];
                    if (next is CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Colon })
                    {
                        if (i + 1 >= end) return Verdict.Invalid;
                        var element = items[++i];
                        var name = element switch
                        {
                            CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.Ident } token } => token.Value.ToLowerInvariant(),
                            CssFunctionComponent function => function.Name.ToLowerInvariant(),
                            _ => null
                        };
                        if (name is null) return Verdict.Invalid;
                        if (afterPseudoElement) result = Verdict.Unknown;
                        if (name.StartsWith("-webkit-", StringComparison.Ordinal) || name.StartsWith("-internal-", StringComparison.Ordinal))
                            result = Verdict.Unknown;
                        else if (element is CssFunctionComponent)
                        {
                            if (!FunctionalPseudoElements.Contains(name)) return Verdict.Invalid;
                            result = Verdict.Unknown;
                        }
                        else if (!PseudoElements.Contains(name)) return Verdict.Invalid;
                        afterPseudoElement = true;
                        break;
                    }
                    var pseudo = next switch
                    {
                        CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.Ident } token } => token.Value.ToLowerInvariant(),
                        CssFunctionComponent function => function.Name.ToLowerInvariant(),
                        _ => null
                    };
                    if (pseudo is null) return Verdict.Invalid;
                    // Selectors 4 allows some pseudo-classes after a pseudo-element; not examined.
                    if (afterPseudoElement) result = Verdict.Unknown;
                    if (pseudo.StartsWith("-webkit-", StringComparison.Ordinal) || pseudo.StartsWith("-internal-", StringComparison.Ordinal))
                        result = Verdict.Unknown;
                    else if (next is CssFunctionComponent)
                    {
                        if (!FunctionalPseudoClasses.Contains(pseudo)) return Verdict.Invalid;
                        // Arguments (forgiving lists, An+B) are not examined.
                        result = Verdict.Unknown;
                    }
                    else if (pseudo is "before" or "after" or "first-line" or "first-letter") afterPseudoElement = true;
                    else if (!PseudoClasses.Contains(pseudo)) return Verdict.Invalid;
                    break;
                default:
                    // Numbers, strings, URLs, other delimiters, (), {} and functions cannot start or
                    // continue a compound selector.
                    return Verdict.Invalid;
            }
        }
        return compounds == 0 || combinator ? Verdict.Invalid : result;
    }

    private static Verdict AttributeSelector(IReadOnlyList<CssSyntaxComponent> values)
    {
        var parts = new List<CssSyntaxComponent>();
        foreach (var value in values)
            if (value is not CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Whitespace }) parts.Add(value);
        // A namespace prefix (ns|attr, *|attr, |attr) is not examined; '|=' is a matcher.
        for (var p = 0; p < parts.Count; p++)
            if (parts[p] is CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.Delim, Value: "|" } } && !AdjacentEquals(parts, p))
                return Verdict.Unknown;
        var i = 0;
        if (i >= parts.Count || parts[i] is not CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Ident }) return Verdict.Invalid;
        i++;
        if (i == parts.Count) return Verdict.Valid;
        if (parts[i] is not CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.Delim } matcher }) return Verdict.Invalid;
        if (matcher.Value is "~" or "|" or "^" or "$" or "*")
        {
            if (!AdjacentEquals(parts, i)) return Verdict.Invalid;
            i++;
        }
        else if (matcher.Value != "=") return Verdict.Invalid;
        i++;
        if (i >= parts.Count || parts[i] is not CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Ident or CssSyntaxTokenKind.String })
            return Verdict.Invalid;
        i++;
        if (i < parts.Count)
        {
            if (parts[i] is not CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.Ident } modifier }
                || modifier.Value.ToLowerInvariant() is not ("i" or "s"))
                return Verdict.Invalid;
            i++;
        }
        return i == parts.Count ? Verdict.Valid : Verdict.Invalid;
    }

    private static bool AdjacentEquals(List<CssSyntaxComponent> parts, int index) =>
        index >= 0 && index + 1 < parts.Count && parts[index + 1] is CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.Delim, Value: "=" } equals }
        && equals.Span.Start == parts[index].Span.Start + parts[index].Span.Length;

    // Selectors 4/5, CSS UI, Fullscreen, Shadow DOM, Media and Chrome (checked with
    // CSS.supports("selector(...)") on Chrome 158). Page-only :first, :left and :right are absent.
    private static readonly FrozenSet<string> PseudoClasses = new[]
    {
        "active", "any-link", "autofill", "blank", "buffering", "checked", "closed", "current", "default",
        "defined", "disabled", "empty", "enabled", "first-child", "first-of-type", "focus", "focus-visible",
        "focus-within", "fullscreen", "future", "host", "hover", "in-range", "indeterminate", "invalid",
        "last-child", "last-of-type", "link", "local-link", "modal", "muted", "only-child", "only-of-type",
        "open", "optional", "out-of-range", "past", "paused", "picture-in-picture", "placeholder-shown",
        "playing", "popover-open", "read-only", "read-write", "required", "root", "scope", "seeking",
        "stalled", "target", "target-within", "user-invalid", "user-valid", "valid", "visited",
        "volume-locked", "xr-overlay", "horizontal", "vertical", "decrement", "increment", "start", "end",
        "double-button", "single-button", "no-button", "corner-present", "window-inactive",
        "active-view-transition", "target-current", "target-before", "target-after", "has-slotted",
        "heading", "user-error", "focus-ring",
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> FunctionalPseudoClasses = new[]
    {
        "dir", "has", "host", "host-context", "is", "lang", "not", "nth-child", "nth-last-child",
        "nth-of-type", "nth-last-of-type", "nth-col", "nth-last-col", "state", "where", "current",
        "active-view-transition-type", "heading", "matches", "any",
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> PseudoElements = new[]
    {
        "after", "before", "backdrop", "cue", "cue-region", "file-selector-button", "first-letter",
        "first-line", "grammar-error", "marker", "placeholder", "selection", "spelling-error",
        "target-text", "view-transition", "details-content", "picker-icon", "checkmark", "column",
        "scroll-marker", "scroll-marker-group", "search-text", "postfix", "prefix", "footnote-call",
        "footnote-marker", "scroll-button",
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> FunctionalPseudoElements = new[]
    {
        "cue", "cue-region", "highlight", "part", "slotted", "view-transition-group",
        "view-transition-image-pair", "view-transition-new", "view-transition-old", "picker",
        "scroll-button", "nth-fragment", "column",
    }.ToFrozenSet(StringComparer.Ordinal);

    // ---- Declaration values ----

    private enum TermKind { Ident, Number, Percentage, Dimension, Hash, String, Url, Comma, Slash, Function, Other }

    private readonly record struct Term(TermKind Kind, string Text, double Number = 0, bool Integer = false, string Unit = "");

    // False when the value cannot be examined (too many components); a known function becomes a
    // Function term that matches any value type, an unknown one matches nothing.
    private static bool Terms(IReadOnlyList<CssSyntaxComponent> values, List<Term> terms)
    {
        foreach (var value in values)
        {
            if (terms.Count > MaxTerms) return false;
            switch (value)
            {
                case CssTokenComponent { Token: var token }:
                    switch (token.Kind)
                    {
                        case CssSyntaxTokenKind.Whitespace: break;
                        case CssSyntaxTokenKind.Ident: terms.Add(new(TermKind.Ident, token.Value.ToLowerInvariant())); break;
                        case CssSyntaxTokenKind.Number:
                            terms.Add(new(TermKind.Number, token.Raw, Parse(token.Value), token.IsInteger)); break;
                        case CssSyntaxTokenKind.Percentage: terms.Add(new(TermKind.Percentage, token.Raw, Parse(token.Value))); break;
                        case CssSyntaxTokenKind.Dimension:
                            terms.Add(new(TermKind.Dimension, token.Raw, Parse(token.Value), token.IsInteger,
                                (token.Unit ?? "").ToLowerInvariant())); break;
                        case CssSyntaxTokenKind.Hash: terms.Add(new(TermKind.Hash, token.Value)); break;
                        case CssSyntaxTokenKind.String: terms.Add(new(TermKind.String, token.Value)); break;
                        case CssSyntaxTokenKind.Url: terms.Add(new(TermKind.Url, token.Value)); break;
                        case CssSyntaxTokenKind.Comma: terms.Add(new(TermKind.Comma, ",")); break;
                        case CssSyntaxTokenKind.Delim when token.Value == "/": terms.Add(new(TermKind.Slash, "/")); break;
                        default: terms.Add(new(TermKind.Other, token.Raw)); break;
                    }
                    break;
                case CssFunctionComponent function:
                    terms.Add(new(KnownFunctions.Contains(function.Name.ToLowerInvariant()) ? TermKind.Function : TermKind.Other,
                        function.Name.ToLowerInvariant()));
                    break;
                default: terms.Add(new(TermKind.Other, "")); break;
            }
        }
        return terms.Count <= MaxTerms;
    }

    private static double Parse(string text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : double.NaN;

    // Functional notations of CSS Values, Color, Images, Fonts, Transforms, Filters, Shapes, Easing,
    // Anchor Positioning and Chrome's prefixed spellings. Any of them may stand for a value of any type.
    private static readonly FrozenSet<string> KnownFunctions = new[]
    {
        "calc", "min", "max", "clamp", "round", "mod", "rem", "abs", "sign", "sin", "cos", "tan", "asin",
        "acos", "atan", "atan2", "pow", "sqrt", "hypot", "log", "exp", "progress", "calc-size", "random",
        "sibling-index", "sibling-count", "if", "attr", "env", "var", "anchor", "anchor-size", "-webkit-calc",
        "rgb", "rgba", "hsl", "hsla", "hwb", "lab", "lch", "oklab", "oklch", "color", "color-mix",
        "light-dark", "contrast-color", "device-cmyk", "url", "src", "image", "image-set", "-webkit-image-set",
        "cross-fade", "-webkit-cross-fade", "element", "paint", "linear-gradient", "radial-gradient",
        "conic-gradient", "repeating-linear-gradient", "repeating-radial-gradient", "repeating-conic-gradient",
        "-webkit-linear-gradient", "-webkit-radial-gradient", "-webkit-repeating-linear-gradient",
        "-webkit-repeating-radial-gradient", "-webkit-gradient", "fit-content", "minmax", "repeat",
        "counter", "counters", "symbols", "format", "local", "tech", "leader", "target-counter",
        "target-counters", "target-text", "content", "string", "cubic-bezier", "steps", "linear",
        "fade", "path", "polygon", "circle", "ellipse", "inset", "xywh", "rect", "shape", "ray",
        "matrix", "matrix3d", "translate", "translatex", "translatey", "translatez", "translate3d",
        "scale", "scalex", "scaley", "scalez", "scale3d", "rotate", "rotatex", "rotatey", "rotatez",
        "rotate3d", "skew", "skewx", "skewy", "perspective", "blur", "brightness", "contrast",
        "drop-shadow", "grayscale", "hue-rotate", "invert", "opacity", "saturate", "sepia", "scroll",
        "view", "stylistic", "styleset", "character-variant", "swash", "ornaments", "annotation",
        "first-valid", "palette-mix", "superellipse", "type", "running",
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> LengthUnits = new[]
    {
        "px", "pt", "pc", "in", "cm", "mm", "q", "em", "rem", "ex", "rex", "cap", "rcap", "ch", "rch", "ic",
        "ric", "lh", "rlh", "vw", "vh", "vi", "vb", "vmin", "vmax", "svw", "svh", "svi", "svb", "svmin",
        "svmax", "lvw", "lvh", "lvi", "lvb", "lvmin", "lvmax", "dvw", "dvh", "dvi", "dvb", "dvmin", "dvmax",
        "cqw", "cqh", "cqi", "cqb", "cqmin", "cqmax",
    }.ToFrozenSet(StringComparer.Ordinal);

    // System colours (CSS Color 4, including deprecated ones) and the keywords that are colours.
    private static readonly FrozenSet<string> ColorKeywords = new[]
    {
        "transparent", "currentcolor", "canvas", "canvastext", "linktext", "visitedtext", "activetext",
        "buttonface", "buttontext", "buttonborder", "field", "fieldtext", "highlight", "highlighttext",
        "selecteditem", "selecteditemtext", "mark", "marktext", "graytext", "accentcolor", "accentcolortext",
        "activeborder", "activecaption", "appworkspace", "background", "buttonhighlight", "buttonshadow",
        "captiontext", "inactiveborder", "inactivecaption", "inactivecaptiontext", "infobackground",
        "infotext", "menu", "menutext", "scrollbar", "threeddarkshadow", "threedface", "threedhighlight",
        "threedlightshadow", "threedshadow", "window", "windowframe", "windowtext",
    }.ToFrozenSet(StringComparer.Ordinal);

    private static bool Hex(string text, bool lengths36Only)
    {
        if (!(text.Length is 3 or 6 || !lengths36Only && text.Length is 4 or 8)) return false;
        foreach (var c in text) if (!char.IsAsciiHexDigit(c)) return false;
        return true;
    }

    private static bool InRange(double value, double min, double max) => !double.IsNaN(value) && value >= min && value <= max;

    private static bool TypeMatches(Term term, string type, double min, double max)
    {
        // A known function may compute to any type (calc(), var()-free attr(), colour and image
        // functions): treating it as a match keeps every verdict a superset of the real grammar.
        if (term.Kind == TermKind.Function) return true;
        switch (type)
        {
            case "length": return Length(term, min, max);
            case "percentage": return term.Kind == TermKind.Percentage && InRange(term.Number, min, max);
            case "length-percentage":
                return Length(term, min, max) || term.Kind == TermKind.Percentage && InRange(term.Number, min, max);
            case "number": return term.Kind == TermKind.Number && InRange(term.Number, min, max);
            case "integer": return term.Kind == TermKind.Number && term.Integer && InRange(term.Number, min, max);
            case "string": return term.Kind == TermKind.String;
            case "url": return term.Kind == TermKind.Url;
            case "ident": return term.Kind == TermKind.Ident;
            case "custom-ident": return term.Kind == TermKind.Ident && !WideKeyword(term.Text) && term.Text != "default";
            case "time": return term.Kind == TermKind.Dimension && term.Unit is "s" or "ms";
            case "angle":
                return term.Kind == TermKind.Dimension && term.Unit is "deg" or "grad" or "rad" or "turn"
                    || term.Kind == TermKind.Number && term.Number == 0;
            case "color":
                return term.Kind switch
                {
                    TermKind.Ident => CssNamedColors.Hex(term.Text) is not null || ColorKeywords.Contains(term.Text)
                        || term.Text.StartsWith("-webkit-", StringComparison.Ordinal)
                        || term.Text.StartsWith("-internal-", StringComparison.Ordinal)
                        // Quirks mode accepts hashless hex colours.
                        || Hex(term.Text, lengths36Only: true),
                    TermKind.Hash => Hex(term.Text, lengths36Only: false),
                    TermKind.Number or TermKind.Dimension => Hex(term.Text, lengths36Only: true),
                    _ => false
                };
            default: throw new InvalidOperationException("Unknown grammar type " + type);
        }
    }

    private static bool Length(Term term, double min, double max) =>
        term.Kind == TermKind.Dimension && LengthUnits.Contains(term.Unit) && InRange(term.Number, min, max)
        // Zero needs no unit; quirks mode also accepts a unitless number as a length in px.
        || term.Kind == TermKind.Number && (term.Number == 0 || InRange(term.Number, min, max));

    // ---- Grammar engine: a subset of the CSS value definition syntax ----

    private abstract record Node;
    private sealed record Keyword(string Word) : Node;
    private sealed record Literal(TermKind Kind) : Node;
    private sealed record TypeRef(string Type, double Min, double Max) : Node;
    private sealed record Sequence(Node[] Items) : Node;
    private sealed record Choice(Node[] Items) : Node;
    private sealed record AnyOrder(Node[] Items, bool All) : Node;
    private sealed record Repeat(Node Item, int Min, int Max, bool Commas) : Node;

    private static bool Matches(Node grammar, List<Term> terms)
    {
        foreach (var end in Ends(grammar, terms, 0)) if (end == terms.Count) return true;
        return false;
    }

    private static HashSet<int> Ends(Node node, List<Term> terms, int at)
    {
        var result = new HashSet<int>();
        switch (node)
        {
            case Keyword keyword:
                if (at < terms.Count && terms[at].Kind == TermKind.Ident && terms[at].Text == keyword.Word) result.Add(at + 1);
                break;
            case Literal literal:
                if (at < terms.Count && terms[at].Kind == literal.Kind) result.Add(at + 1);
                break;
            case TypeRef type:
                if (at < terms.Count && TypeMatches(terms[at], type.Type, type.Min, type.Max)) result.Add(at + 1);
                break;
            case Sequence sequence:
                result.Add(at);
                foreach (var item in sequence.Items)
                {
                    var next = new HashSet<int>();
                    foreach (var position in result) next.UnionWith(Ends(item, terms, position));
                    result = next;
                    if (result.Count == 0) break;
                }
                break;
            case Choice choice:
                foreach (var item in choice.Items) result.UnionWith(Ends(item, terms, at));
                break;
            case AnyOrder any:
            {
                var full = (1 << any.Items.Length) - 1;
                var seen = new HashSet<(int, int)>();
                var pending = new Stack<(int Position, int Mask)>();
                pending.Push((at, 0));
                while (pending.Count > 0)
                {
                    var (position, mask) = pending.Pop();
                    if (!seen.Add((position, mask))) continue;
                    if (any.All ? mask == full : mask != 0) result.Add(position);
                    for (var i = 0; i < any.Items.Length; i++)
                    {
                        if ((mask & (1 << i)) != 0) continue;
                        foreach (var end in Ends(any.Items[i], terms, position)) pending.Push((end, mask | (1 << i)));
                    }
                }
                break;
            }
            case Repeat repeat:
            {
                if (repeat.Min == 0) result.Add(at);
                var frontier = new HashSet<int> { at };
                for (var count = 1; count <= repeat.Max && count <= terms.Count + 1 && frontier.Count > 0; count++)
                {
                    var next = new HashSet<int>();
                    foreach (var position in frontier)
                    {
                        var from = position;
                        if (repeat.Commas && count > 1)
                        {
                            if (from >= terms.Count || terms[from].Kind != TermKind.Comma) continue;
                            from++;
                        }
                        foreach (var end in Ends(repeat.Item, terms, from)) if (end > position || count == 1) next.Add(end);
                    }
                    if (count >= repeat.Min) result.UnionWith(next);
                    frontier = next;
                }
                break;
            }
        }
        return result;
    }

    private sealed class GrammarReader(string text)
    {
        private int at;

        internal Node Read()
        {
            var node = ReadChoice();
            Skip();
            if (at != text.Length) throw new InvalidOperationException("Grammar syntax error at " + at + ": " + text);
            return node;
        }

        private void Skip() { while (at < text.Length && text[at] == ' ') at++; }

        private bool Take(string symbol)
        {
            Skip();
            if (string.CompareOrdinal(text, at, symbol, 0, symbol.Length) != 0) return false;
            // '|' must not be read as the first half of '||'.
            if (symbol == "|" && at + 1 < text.Length && text[at + 1] == '|') return false;
            at += symbol.Length;
            return true;
        }

        private Node ReadChoice()
        {
            var items = new List<Node> { ReadOr() };
            while (Take("|")) items.Add(ReadOr());
            return items.Count == 1 ? items[0] : new Choice([.. items]);
        }

        private Node ReadOr()
        {
            var items = new List<Node> { ReadAnd() };
            while (Take("||")) items.Add(ReadAnd());
            return items.Count == 1 ? items[0] : new AnyOrder([.. items], All: false);
        }

        private Node ReadAnd()
        {
            var items = new List<Node> { ReadSequence() };
            while (Take("&&")) items.Add(ReadSequence());
            return items.Count == 1 ? items[0] : new AnyOrder([.. items], All: true);
        }

        private Node ReadSequence()
        {
            var items = new List<Node>();
            while (true)
            {
                Skip();
                if (at >= text.Length || text[at] == ']' || text[at] == '|' || text[at] == '&') break;
                items.Add(ReadMultiplied());
            }
            if (items.Count == 0) throw new InvalidOperationException("Empty grammar sequence: " + text);
            return items.Count == 1 ? items[0] : new Sequence([.. items]);
        }

        private Node ReadMultiplied()
        {
            var node = ReadAtom();
            while (at < text.Length)
            {
                var c = text[at];
                if (c == '?') { at++; node = new Repeat(node, 0, 1, false); }
                else if (c == '*') { at++; node = new Repeat(node, 0, int.MaxValue, false); }
                else if (c == '+') { at++; node = new Repeat(node, 1, int.MaxValue, false); }
                else if (c == '#') { at++; node = new Repeat(node, 1, int.MaxValue, true); }
                else if (c == '{')
                {
                    var close = text.IndexOf('}', at);
                    var range = text[(at + 1)..close].Split(',');
                    var min = int.Parse(range[0], CultureInfo.InvariantCulture);
                    var max = range.Length == 1 ? min : range[1].Length == 0 ? int.MaxValue : int.Parse(range[1], CultureInfo.InvariantCulture);
                    node = new Repeat(node, min, max, false);
                    at = close + 1;
                }
                else break;
            }
            return node;
        }

        private Node ReadAtom()
        {
            Skip();
            if (Take("["))
            {
                var inner = ReadChoice();
                if (!Take("]")) throw new InvalidOperationException("Unclosed grammar group: " + text);
                return inner;
            }
            if (Take(",")) return new Literal(TermKind.Comma);
            if (Take("/")) return new Literal(TermKind.Slash);
            if (Take("<"))
            {
                var close = text.IndexOf('>', at);
                var body = text[at..close];
                at = close + 1;
                var space = body.IndexOf(' ');
                if (space < 0) return new TypeRef(body, double.NegativeInfinity, double.PositiveInfinity);
                var bounds = body[(space + 1)..].Trim('[', ']').Split(',');
                return new TypeRef(body[..space], Bound(bounds[0], double.NegativeInfinity), Bound(bounds[1], double.PositiveInfinity));
            }
            var start = at;
            while (at < text.Length && (char.IsAsciiLetterOrDigit(text[at]) || text[at] == '-')) at++;
            if (at == start) throw new InvalidOperationException("Grammar syntax error at " + at + ": " + text);
            return new Keyword(text[start..at]);
        }

        private static double Bound(string text, double open) =>
            text.Length == 0 ? open : double.Parse(text, CultureInfo.InvariantCulture);
    }

    private const string LineWidth = "<length [0,]> | thin | medium | thick";
    private const string LineStyle = "none | hidden | dotted | dashed | solid | double | groove | ridge | inset | outset";
    private const string Size = "auto | none | <length-percentage [0,]> | min-content | max-content | fit-content | stretch | contain"
        + " | -webkit-fill-available | -webkit-min-content | -webkit-max-content | -webkit-fit-content | intrinsic | min-intrinsic";
    private const string Margin = "<length-percentage> | auto";
    private const string Padding = "<length-percentage [0,]>";
    private const string Inset = "<length-percentage> | auto";
    private const string Breaks = "auto | avoid | always | all | avoid-page | page | left | right | recto | verso | avoid-column | column | avoid-region | region";
    private const string BlendModes = "normal | multiply | screen | overlay | darken | lighten | color-dodge | color-burn | hard-light"
        + " | soft-light | difference | exclusion | hue | saturation | color | luminosity | plus-darker | plus-lighter";
    private const string Overflow = "visible | hidden | clip | scroll | auto | overlay | -webkit-paged-x | -webkit-paged-y";
    private const string DecorationLines = "underline | overline | line-through | blink | spelling-error | grammar-error";
    private const string DecorationStyles = "solid | double | dotted | dashed | wavy";
    private const string Cursors = "auto | default | none | context-menu | help | pointer | progress | wait | cell | crosshair | text"
        + " | vertical-text | alias | copy | move | no-drop | not-allowed | grab | grabbing | e-resize | n-resize | ne-resize"
        + " | nw-resize | s-resize | se-resize | sw-resize | w-resize | ew-resize | ns-resize | nesw-resize | nwse-resize"
        + " | col-resize | row-resize | all-scroll | zoom-in | zoom-out | -webkit-grab | -webkit-grabbing | -webkit-zoom-in"
        + " | -webkit-zoom-out | hand";

    // Superset grammars per canonical (alias-normalized) property name. A property that is absent
    // is never proven invalid by its value.
    private static readonly FrozenDictionary<string, Node> Grammars = Build(new Dictionary<string, string>
    {
        ["color"] = "<color>",
        ["background-color"] = "<color>",
        ["font-size"] = "xx-small | x-small | small | medium | large | x-large | xx-large | xxx-large | -webkit-xxx-large | larger | smaller | math | <length-percentage [0,]>",
        ["font-weight"] = "normal | bold | bolder | lighter | <number [1,1000]>",
        ["font-style"] = "normal | italic | oblique <angle>{0,2} | left | right",
        ["line-height"] = "normal | <number [0,]> | <length-percentage [0,]>",
        ["text-align"] = "start | end | left | right | center | justify | match-parent | justify-all | -webkit-left | -webkit-right | -webkit-center | -webkit-match-parent | -webkit-auto | <string>",
        ["text-align-last"] = "auto | start | end | left | right | center | justify | match-parent",
        ["vertical-align"] = "[baseline | sub | super | text-top | text-bottom | middle | top | bottom | center | alphabetic | ideographic | central | mathematical | text-before-edge | text-after-edge | before-edge | after-edge | hanging | first | last | -webkit-baseline-middle | <length-percentage>]{1,3}",
        ["white-space"] = "[normal | pre | nowrap | pre-wrap | break-spaces | pre-line | collapse | preserve | preserve-breaks | preserve-spaces | wrap | discard | discard-before | discard-after | discard-inner | balance | stable | pretty]{1,4}",
        ["overflow-wrap"] = "normal | break-word | anywhere",
        ["word-break"] = "normal | break-all | keep-all | break-word | auto-phrase | manual",
        ["text-indent"] = "[<length-percentage> | hanging | each-line]{1,3}",
        ["letter-spacing"] = "normal | <length-percentage>",
        ["word-spacing"] = "normal | <length-percentage>",
        ["text-transform"] = "none | [capitalize | uppercase | lowercase | full-width | full-size-kana | math-auto]{1,3}",
        ["display"] = "[block | inline | run-in | flow | flow-root | table | flex | grid | ruby | math | list-item | grid-lanes | masonry]{1,3}"
            + " | table-row-group | table-header-group | table-footer-group | table-row | table-cell | table-column-group | table-column"
            + " | table-caption | ruby-base | ruby-text | ruby-base-container | ruby-text-container | contents | none | inline-block"
            + " | inline-table | inline-flex | inline-grid | inline-list-item | inline-grid-lanes | inline-masonry | -webkit-box"
            + " | -webkit-inline-box | -webkit-flex | -webkit-inline-flex",
        ["list-style-type"] = "<ident> | <string>",
        ["list-style-position"] = "inside | outside",
        ["list-style"] = "[<ident> | <string> | <url>]{1,3}",
        ["text-decoration-line"] = $"none | [{DecorationLines}]{{1,6}}",
        ["text-decoration-style"] = DecorationStyles,
        ["text-decoration-color"] = "<color>",
        ["text-decoration-thickness"] = "auto | from-font | <length-percentage>",
        ["text-underline-offset"] = "auto | <length-percentage>",
        ["text-decoration"] = $"[none | {DecorationLines} | {DecorationStyles} | <color> | auto | from-font | <length-percentage>]{{1,9}}",
        ["margin-top"] = Margin, ["margin-right"] = Margin, ["margin-bottom"] = Margin, ["margin-left"] = Margin,
        ["margin-inline-start"] = Margin, ["margin-inline-end"] = Margin, ["margin-block-start"] = Margin, ["margin-block-end"] = Margin,
        ["margin"] = $"[{Margin}]{{1,4}}", ["margin-inline"] = $"[{Margin}]{{1,2}}", ["margin-block"] = $"[{Margin}]{{1,2}}",
        ["padding-top"] = Padding, ["padding-right"] = Padding, ["padding-bottom"] = Padding, ["padding-left"] = Padding,
        ["padding-inline-start"] = Padding, ["padding-inline-end"] = Padding, ["padding-block-start"] = Padding, ["padding-block-end"] = Padding,
        ["padding"] = $"[{Padding}]{{1,4}}", ["padding-inline"] = $"[{Padding}]{{1,2}}", ["padding-block"] = $"[{Padding}]{{1,2}}",
        ["width"] = Size, ["height"] = Size, ["min-width"] = Size, ["min-height"] = Size, ["max-width"] = Size, ["max-height"] = Size,
        ["box-sizing"] = "content-box | border-box",
        ["border-top-width"] = LineWidth, ["border-right-width"] = LineWidth, ["border-bottom-width"] = LineWidth, ["border-left-width"] = LineWidth,
        ["border-inline-start-width"] = LineWidth, ["border-inline-end-width"] = LineWidth,
        ["border-block-start-width"] = LineWidth, ["border-block-end-width"] = LineWidth,
        ["border-width"] = $"[{LineWidth}]{{1,4}}", ["border-inline-width"] = $"[{LineWidth}]{{1,2}}", ["border-block-width"] = $"[{LineWidth}]{{1,2}}",
        ["border-top-style"] = LineStyle, ["border-right-style"] = LineStyle, ["border-bottom-style"] = LineStyle, ["border-left-style"] = LineStyle,
        ["border-inline-start-style"] = LineStyle, ["border-inline-end-style"] = LineStyle,
        ["border-block-start-style"] = LineStyle, ["border-block-end-style"] = LineStyle,
        ["border-style"] = $"[{LineStyle}]{{1,4}}", ["border-inline-style"] = $"[{LineStyle}]{{1,2}}", ["border-block-style"] = $"[{LineStyle}]{{1,2}}",
        ["border-top-color"] = "<color>", ["border-right-color"] = "<color>", ["border-bottom-color"] = "<color>", ["border-left-color"] = "<color>",
        ["border-inline-start-color"] = "<color>", ["border-inline-end-color"] = "<color>",
        ["border-block-start-color"] = "<color>", ["border-block-end-color"] = "<color>",
        ["border-color"] = "<color>{1,4}", ["border-inline-color"] = "<color>{1,2}", ["border-block-color"] = "<color>{1,2}",
        // Chrome 158 also accepts a trailing comma after border, border-inline and border-block.
        ["border"] = $"[[{LineWidth}] || [{LineStyle}] || <color>] ,?",
        ["border-top"] = $"[{LineWidth}] || [{LineStyle}] || <color>",
        ["border-right"] = $"[{LineWidth}] || [{LineStyle}] || <color>",
        ["border-bottom"] = $"[{LineWidth}] || [{LineStyle}] || <color>",
        ["border-left"] = $"[{LineWidth}] || [{LineStyle}] || <color>",
        ["border-inline"] = $"[[{LineWidth}] || [{LineStyle}] || <color>] ,?",
        ["border-block"] = $"[[{LineWidth}] || [{LineStyle}] || <color>] ,?",
        ["border-inline-start"] = $"[{LineWidth}] || [{LineStyle}] || <color>",
        ["border-inline-end"] = $"[{LineWidth}] || [{LineStyle}] || <color>",
        ["border-block-start"] = $"[{LineWidth}] || [{LineStyle}] || <color>",
        ["border-block-end"] = $"[{LineWidth}] || [{LineStyle}] || <color>",
        ["border-radius"] = "<length-percentage [0,]>{1,4} [/ <length-percentage [0,]>{1,4}]?",
        ["border-top-left-radius"] = "<length-percentage [0,]>{1,2}",
        ["border-top-right-radius"] = "<length-percentage [0,]>{1,2}",
        ["border-bottom-right-radius"] = "<length-percentage [0,]>{1,2}",
        ["border-bottom-left-radius"] = "<length-percentage [0,]>{1,2}",
        ["outline-width"] = LineWidth,
        ["outline-style"] = "auto | " + LineStyle,
        ["outline-color"] = "auto | invert | <color>",
        ["outline-offset"] = "<length>",
        ["outline"] = $"[auto | invert | <color>] || [auto | {LineStyle}] || [{LineWidth}]",
        ["break-before"] = Breaks, ["break-after"] = Breaks,
        ["break-inside"] = "auto | avoid | avoid-page | avoid-column | avoid-region",
        ["position"] = "static | relative | absolute | sticky | fixed | -webkit-sticky",
        ["float"] = "left | right | none | inline-start | inline-end | top | bottom | start | end | snap-block | snap-inline | footnote",
        ["clear"] = "none | left | right | both | inline-start | inline-end | top | bottom | start | end | all | block-start | block-end",
        ["overflow-x"] = Overflow, ["overflow-y"] = Overflow, ["overflow"] = $"[{Overflow}]{{1,2}}",
        ["visibility"] = "visible | hidden | collapse",
        ["z-index"] = "auto | <integer>",
        ["opacity"] = "<number> | <percentage>",
        ["isolation"] = "auto | isolate",
        ["mix-blend-mode"] = BlendModes,
        ["background-blend-mode"] = $"[{BlendModes}]#",
        ["direction"] = "ltr | rtl",
        ["unicode-bidi"] = "normal | embed | isolate | bidi-override | isolate-override | plaintext | -webkit-isolate | -webkit-isolate-override | -webkit-plaintext",
        ["writing-mode"] = "horizontal-tb | vertical-rl | vertical-lr | sideways-rl | sideways-lr | lr | lr-tb | rl | rl-tb | tb | tb-rl",
        ["text-orientation"] = "mixed | upright | sideways | sideways-right | vertical-right",
        ["column-count"] = "auto | <integer [1,]>",
        ["column-width"] = "auto | <length [0,]>",
        ["columns"] = "[auto | <integer [1,]> | <length [0,]>]{1,2} [/ <length-percentage [0,]> | / auto]?",
        ["column-gap"] = "normal | <length-percentage [0,]>",
        ["row-gap"] = "normal | <length-percentage [0,]>",
        ["gap"] = "[normal | <length-percentage [0,]>]{1,2}",
        ["column-span"] = "none | all | <integer [1,]>",
        ["column-fill"] = "auto | balance | balance-all",
        ["column-rule-style"] = LineStyle,
        ["transform-style"] = "flat | preserve-3d",
        ["empty-cells"] = "show | hide",
        ["caption-side"] = "top | bottom | inline-start | inline-end | block-start | block-end | left | right | top-outside | bottom-outside",
        ["border-collapse"] = "separate | collapse",
        ["border-spacing"] = "<length [0,]>{1,2}",
        ["text-overflow"] = "[clip | ellipsis | fade | <string>]{1,2}",
        ["box-decoration-break"] = "slice | clone",
        ["backface-visibility"] = "visible | hidden",
        ["content-visibility"] = "visible | auto | hidden",
        ["font-optical-sizing"] = "auto | none",
        ["font-kerning"] = "auto | normal | none",
        ["font-stretch"] = "normal | ultra-condensed | extra-condensed | condensed | semi-condensed | semi-expanded | expanded | extra-expanded | ultra-expanded | <percentage [0,]>",
        ["font-variant-caps"] = "normal | small-caps | all-small-caps | petite-caps | all-petite-caps | unicase | titling-caps",
        ["font-variant-position"] = "normal | sub | super",
        ["text-decoration-skip-ink"] = "auto | none | all",
        ["font-synthesis"] = "none | [weight | style | small-caps | position]{1,4}",
        ["font-synthesis-weight"] = "auto | none", ["font-synthesis-style"] = "auto | none",
        ["font-synthesis-small-caps"] = "auto | none", ["font-synthesis-position"] = "auto | none",
        ["print-color-adjust"] = "exact | economy",
        ["scroll-behavior"] = "auto | smooth",
        ["overscroll-behavior"] = "[auto | contain | none]{1,2}",
        ["overscroll-behavior-x"] = "auto | contain | none", ["overscroll-behavior-y"] = "auto | contain | none",
        ["overscroll-behavior-inline"] = "auto | contain | none", ["overscroll-behavior-block"] = "auto | contain | none",
        ["overflow-anchor"] = "auto | none",
        ["scrollbar-width"] = "auto | thin | none",
        ["scrollbar-gutter"] = "auto | stable | stable both-edges | both-edges stable",
        ["pointer-events"] = "auto | none | visiblepainted | visiblefill | visiblestroke | visible | painted | fill | stroke | all | bounding-box",
        ["user-select"] = "auto | text | none | contain | all | element",
        ["resize"] = "none | both | horizontal | vertical | block | inline | auto",
        ["touch-action"] = "auto | none | manipulation | [pan-x | pan-left | pan-right | pan-y | pan-up | pan-down | pinch-zoom]{1,3}",
        ["text-rendering"] = "auto | optimizespeed | optimizelegibility | geometricprecision | optimizequality",
        ["-webkit-font-smoothing"] = "auto | none | antialiased | subpixel-antialiased",
        ["tab-size"] = "<number [0,]> | <length [0,]>",
        ["cursor"] = $"[[<url> | <string>] [<number> <number>]? ,]* [{Cursors}]",
        ["color-scheme"] = "<ident>+",
        ["forced-color-adjust"] = "auto | none | preserve-parent-color",
        ["caret-color"] = "auto | <color>",
        ["accent-color"] = "auto | <color>",
        ["scrollbar-color"] = "auto | <color>{2}",
        ["-webkit-tap-highlight-color"] = "<color>",
        ["appearance"] = "<ident>",
        ["font-family"] = "[<string> | <ident>+]#",
        ["quotes"] = "auto | none | match-parent | [<string> <string>]+",
        ["will-change"] = "<ident>#",
        ["transform-origin"] = "[<length-percentage> | left | center | right | top | bottom]{1,3}",
        ["zoom"] = "normal | reset | <number [0,]> | <percentage [0,]>",
        ["contain"] = "none | strict | content | [size | inline-size | layout | style | paint]{1,4}",
        ["line-break"] = "auto | loose | normal | strict | anywhere | after-white-space",
        ["text-justify"] = "auto | none | inter-word | inter-character | distribute",
        ["image-rendering"] = "auto | smooth | high-quality | crisp-edges | pixelated | optimizespeed | optimizequality | -webkit-optimize-contrast",
        ["hanging-punctuation"] = "none | [first | force-end | allow-end | last]{1,3}",
        ["background-attachment"] = "[scroll | fixed | local]#",
        ["background-origin"] = "[border-box | padding-box | content-box]#",
        ["background-clip"] = "[border-box | padding-box | content-box | text | border-area | -webkit-text]#",
        ["background-repeat"] = "[repeat-x | repeat-y | [repeat | space | round | no-repeat]{1,2}]#",
        ["background-size"] = "[cover | contain | [<length-percentage [0,]> | auto]{1,2}]#",
        ["background-position"] = "[[left | center | right | top | bottom | <length-percentage>] | [left | center | right | <length-percentage>] [top | center | bottom | <length-percentage>] | [center | [left | right] <length-percentage>?] && [center | [top | bottom] <length-percentage>?]]#",
        ["top"] = Inset, ["right"] = Inset, ["bottom"] = Inset, ["left"] = Inset,
        ["inset-block-start"] = Inset, ["inset-block-end"] = Inset, ["inset-inline-start"] = Inset, ["inset-inline-end"] = Inset,
        ["inset"] = $"[{Inset}]{{1,4}}", ["inset-block"] = $"[{Inset}]{{1,2}}", ["inset-inline"] = $"[{Inset}]{{1,2}}",
        ["text-emphasis-color"] = "<color>",
        ["text-size-adjust"] = "auto | none | <percentage [0,]>",
        ["-webkit-user-drag"] = "auto | element | none",
        ["scroll-snap-type"] = "none | [x | y | block | inline | both] [mandatory | proximity]?",
        ["scroll-snap-align"] = "[none | start | end | center]{1,2}",
        ["scroll-snap-stop"] = "normal | always",
        ["field-sizing"] = "fixed | content",
        ["interpolate-size"] = "numeric-only | allow-keywords",
        ["transform-box"] = "content-box | border-box | fill-box | stroke-box | view-box",
        ["text-box-trim"] = "none | trim-start | trim-end | trim-both",
        ["mask-type"] = "luminance | alpha",
        ["text-wrap-style"] = "auto | balance | stable | pretty | avoid-orphans",
        ["hyphenate-character"] = "auto | <string>",
        ["paint-order"] = "normal | [fill | stroke | markers]{1,3}",
        ["math-style"] = "normal | compact",
        ["math-shift"] = "normal | compact",
        ["overlay"] = "none | auto",
        ["caret-animation"] = "auto | manual",
        ["caret-shape"] = "auto | bar | block | underscore",
    });

    private static FrozenDictionary<string, Node> Build(Dictionary<string, string> definitions)
    {
        var grammars = new Dictionary<string, Node>(definitions.Count, StringComparer.Ordinal);
        foreach (var (name, definition) in definitions) grammars.Add(name, new GrammarReader(definition).Read());
        return grammars.ToFrozenDictionary(StringComparer.Ordinal);
    }
}
