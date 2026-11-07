using System.Globalization;

namespace FactsPDF;

internal static class InlineCss
{
    // Assigns into style, which must be the caller's private copy (see TextStyle), and returns it.
    public static TextStyle Apply(TextStyle style, string css, bool paragraph, bool inline, int offset)
    {
        foreach (var declaration in css.Split(';'))
        {
            if (string.IsNullOrWhiteSpace(declaration)) continue;
            var colon = declaration.IndexOf(':');
            if (colon <= 0) throw Invalid("Expected property: value.", offset);
            ApplyTrimmed(style, declaration[..colon].Trim().ToLowerInvariant(),
                declaration[(colon + 1)..].Trim().ToLowerInvariant(), paragraph, inline, offset);
        }
        return style;
    }

    // Applies one already-separated declaration exactly as Apply(style, name + ":" + value, ...)
    // would, without building, splitting and re-slicing that string. Trim and ToLowerInvariant
    // return the same instance when nothing changes, so the usual lower-case, trimmed
    // stylesheet value costs no allocation here.
    public static TextStyle Apply(TextStyle style, string name, string value, bool paragraph, bool inline, int offset)
    {
        // A ';' in either part, or a ':' in the name, splits differently in the joined form.
        if (name.Contains(';') || value.Contains(';') || name.Contains(':'))
            return Apply(style, name + ":" + value, paragraph, inline, offset);
        if (name.Length == 0) throw Invalid("Expected property: value.", offset);
        ApplyTrimmed(style, name.Trim().ToLowerInvariant(), value.Trim().ToLowerInvariant(), paragraph, inline, offset);
        return style;
    }

    private static void ApplyTrimmed(TextStyle style, string name, string value, bool paragraph, bool inline, int offset)
    {
        if (value.Length == 0 || value.Contains('!')) throw Invalid("Empty values and !important are not supported.", offset);
        switch (name)
        {
            case "font-weight":
                // Relative weights operate on the engine's two provided faces:
                // bolder chooses bold and lighter chooses normal. At an edge
                // of this binary ladder the same role remains selected.
                // This is deliberately not intermediate CSS 100–900 matching.
                if (value is "bolder" or "lighter")
                {
                    style.Bold = value == "bolder";
                    break;
                }
                var weight = value switch { "normal" => 400, "bold" => 700, _ => Number(value, offset) };
                // The engine has only two explicit weight roles, 400 and 700.
                // Support the common hundred-step CSS values while resolving
                // 100–500 to the provided normal face and 600–900 to bold.
                // Do not synthesize intermediate font weights or infer faces.
                if (weight is < 100 or > 900 || weight % 100 != 0)
                    throw Invalid("font-weight accepts normal/bold or numeric hundreds from 100 through 900.", offset);
                style.Bold = weight >= 600;
                break;
            case "font-style":
                style.Italic = value switch
                {
                    "normal" => false, "italic" => true,
                    _ => throw Invalid("font-style supports only normal and italic.", offset)
                };
                break;
            case "font-size":
                var size = KeywordFontSize(value, style.FontSize) ?? Length(value, offset);
                if (size is < 1 or > 144) throw Invalid("font-size must be between 1pt and 144pt.", offset);
                style.FontSize = size;
                break;
            case "color":
                // The computed colour of currentColor is the inherited text
                // colour, not a previous losing declaration or a PDF alpha.
                // CSS winner selection leaves style.Color at the parent ink.
                style.Color = value == "currentcolor" ? style.Color : Color(value, offset);
                break;
            case "text-decoration" or "text-decoration-line":
                style.Decoration = ParseDecorationLines(value, offset);
                break;
            case "text-decoration-color":
                // Nullable means CSS currentColor, resolved at paint time
                // so an inline colour can still affect the line naturally.
                style.DecorationColor = value == "currentcolor" ? null : Color(value, offset);
                break;
            case "text-decoration-style":
                style.DecorationPattern = value switch
                {
                    "solid" => TextDecorationPattern.Solid,
                    "dashed" => TextDecorationPattern.Dashed,
                    "dotted" => TextDecorationPattern.Dotted,
                    _ => throw Invalid("Only solid, dashed and dotted decoration strokes are supported.", offset)
                };
                break;
            case "line-height":
                // Unitless leading remains inherited as a multiplier. A
                // percentage, em or fixed length instead computes to points
                // on this element and inherits that absolute value.
                if (value == "normal")
                {
                    style.LineHeight = 1.2;
                    style.AbsoluteLineHeight = null;
                    break;
                }
                var relativeScale = RelativeLineHeightScale(value, offset);
                var physical = value.EndsWith("pt", StringComparison.Ordinal)
                    || value.EndsWith("px", StringComparison.Ordinal)
                    || value.EndsWith("mm", StringComparison.Ordinal)
                    || value.EndsWith("cm", StringComparison.Ordinal)
                    || value.EndsWith("in", StringComparison.Ordinal)
                    || value.EndsWith("pc", StringComparison.Ordinal)
                    || value.EndsWith('q');
                if (relativeScale.HasValue || physical)
                {
                    var computed = physical ? Length(value, offset) : style.FontSize * relativeScale!.Value;
                    // Protect page progression and conversion budgets from
                    // zero/tiny or unbounded line boxes.
                    if (!double.IsFinite(computed) || computed is < 0.1 or > 1440)
                        throw Invalid("Computed line-height must be between 0.1pt and 1440pt.", offset);
                    style.AbsoluteLineHeight = computed;
                    break;
                }
                var factor = Number(value, offset);
                if (factor is < 1 or > 10) throw Invalid("Unitless line-height must be from 1 to 10.", offset);
                style.LineHeight = factor;
                style.AbsoluteLineHeight = null;
                break;
            case "letter-spacing":
                // A deliberately bounded fixed advance; no font metrics,
                // relative units or shaping are inferred at parse time.
                var tracking = value == "normal" ? 0 : Length(value, offset);
                if (!double.IsFinite(tracking) || tracking is < 0 or > 24)
                    throw Invalid("letter-spacing must be from 0pt to 24pt.", offset);
                style.LetterSpacing = tracking;
                break;
            case "word-spacing":
                // Only collapsed U+0020 space glyphs receive a fixed extra advance.
                var wordGap = value == "normal" ? 0 : Length(value, offset);
                if (!double.IsFinite(wordGap) || wordGap is < 0 or > 24)
                    throw Invalid("word-spacing must be from 0pt to 24pt.", offset);
                style.WordSpacing = wordGap;
                break;
            case "text-transform":
                style.Transform = value switch
                {
                    "none" => TextTransform.None,
                    "uppercase" => TextTransform.Uppercase,
                    "lowercase" => TextTransform.Lowercase,
                    "capitalize" => TextTransform.Capitalize,
                    _ => throw Invalid("text-transform supports only none, uppercase, lowercase and capitalize for ASCII letters.", offset)
                };
                break;
            case "white-space" when paragraph && !inline:
                if (value is not ("normal" or "nowrap" or "pre-line" or "pre-wrap" or "pre"))
                    throw Invalid("Only normal, nowrap, pre-line, bounded pre-wrap and no-soft-wrap pre are supported.", offset);
                style.NoWrap = value is "nowrap" or "pre";
                style.PreserveLineBreaks = value is "pre-line" or "pre-wrap" or "pre";
                style.PreserveSpaces = value is "pre-wrap" or "pre";
                break;
            case "overflow-wrap" when paragraph && !inline:
                // Emergency breaking applies only to the whole report
                // paragraph/heading, never a silently ignored inline span.
                style.BreakWords = value switch
                {
                    "normal" => false, "break-word" => true,
                    _ => throw Invalid("overflow-wrap supports only normal and break-word.", offset)
                };
                break;
            case "word-break" when paragraph && !inline:
                // Every already-supported simple glyph can end a line;
                // unlike emergency wrapping, this consumes remaining width.
                style.BreakAll = value switch
                {
                    "normal" => false, "break-all" => true,
                    _ => throw Invalid("word-break supports only normal and break-all.", offset)
                };
                break;
            case "text-indent" when paragraph && !inline:
                // Resolve percentages against this paragraph's actual text
                // content width at layout time, after margins/padding.
                style.FirstLineIndent = CssBoxValues.Length(value, offset);
                break;
            case "text-align-last" when !inline:
                style.LastLineAlignment = value switch
                {
                    "auto" => null,
                    "left" or "start" => TextAlignment.Left,
                    "center" => TextAlignment.Center,
                    "right" or "end" => TextAlignment.Right,
                    "justify" => TextAlignment.Justify,
                    _ => throw Invalid("text-align-last supports auto, left, center, right, LTR start/end and justify.", offset)
                };
                break;
            case "text-align" when !inline:
                style.Alignment = value switch
                {
                    "left" or "start" => TextAlignment.Left, "center" => TextAlignment.Center, "right" or "end" => TextAlignment.Right,
                    "justify" => TextAlignment.Justify,
                    _ => throw Invalid("Only left, center, right, LTR start/end and word-space justify alignment are supported.", offset)
                };
                break;
            case "margin-top" when paragraph: style.MarginBefore = Margin(value, offset); break;
            case "margin-bottom" when paragraph: style.MarginAfter = Margin(value, offset); break;
            case "break-before" when paragraph: style.BreakBefore = PageBreak(value, offset); break;
            case "break-after" when paragraph: style.BreakAfter = value != "avoid" && PageBreak(value, offset); style.KeepWithNext = value == "avoid"; break;
            default: throw new FactsPdfException("FPDF1201", $"CSS property '{name}' is not supported on this element.", offset);
        }
    }

    // Bounded, deterministic source-only keyword sizes for controlled report text.
    // Absolute keywords use the documented 12pt medium reference; relative
    // keywords use the *computed parent* size during stylesheet resolution.
    // Lines are an unordered CSS set. Keep deterministic flags, reject duplicates,
    // and never interpret 'none' combined with a visible line as an empty paint.
    internal static TextDecoration ParseDecorationLines(string value, int offset)
    {
        if (value == "none") return TextDecoration.None;
        var decoration = TextDecoration.None;
        var terms = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (terms.Length is < 1 or > 3)
            throw Invalid("Text decoration accepts one to three distinct visible line kinds.", offset);
        foreach (var term in terms)
        {
            var line = term switch
            {
                "underline" => TextDecoration.Underline,
                "line-through" => TextDecoration.LineThrough,
                "overline" => TextDecoration.Overline,
                _ => throw Invalid("Text decoration lines support underline, overline, line-through, or standalone none.", offset)
            };
            if ((decoration & line) != TextDecoration.None)
                throw Invalid("Duplicate text decoration line.", offset);
            decoration |= line;
        }
        return decoration;
    }

    internal static double? KeywordFontSize(string value, double parentFontSize) => value switch
    {
        "xx-small" => 7.2,
        "x-small" => 9,
        "small" => 10,
        "medium" => 12,
        "large" => 14.4,
        "x-large" => 18,
        "xx-large" => 24,
        "smaller" => parentFontSize * (5d / 6d),
        "larger" => parentFontSize * (6d / 5d),
        _ => null
    };

    // Unmatched rules are validated without assuming a synthetic computed root.
    internal static double? RootRelativeScale(string value, int offset, double maxFactor)
    {
        if (!value.EndsWith("rem", StringComparison.Ordinal)) return null;
        var factor = Number(value[..^3], offset);
        if (factor <= 0 || factor > maxFactor)
            throw Invalid("Root-relative rem requires a bounded positive finite multiplier.", offset);
        return factor;
    }

    // CSS relative font sizing is resolved only after the full cascade chooses
    // a declaration. Syntax validation here intentionally does not assume a
    // particular element or inherited font size.
    internal static double? RelativeFontSizeScale(string value, int offset)
    {
        var percent = value.EndsWith('%');
        var em = !percent && value.EndsWith("em", StringComparison.Ordinal);
        if (!percent && !em) return null;

        var numberText = percent ? value[..^1] : value[..^2];
        if (!double.TryParse(numberText,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var number) ||
            !double.IsFinite(number) || number <= 0)
            throw Invalid("Relative font-size requires a positive finite percentage or em length.", offset);
        var scale = percent ? number / 100 : number;
        // The minimum supported parent font size is 1pt; a larger factor can
        // never yield an allowed computed size even on the smallest parent.
        if (!double.IsFinite(scale) || scale > 144)
            throw Invalid("Relative font-size exceeds the supported 1–144pt computed range.", offset);
        return scale;
    }

    // Validate percentage/em syntax without assuming a 12pt parent. Unmatched
    // rules are checked here, but only the winning declaration has an actual
    // computed font size from which a bounded absolute height can be derived.
    internal static double? RelativeLineHeightScale(string value, int offset)
    {
        var percent = value.EndsWith('%');
        var em = !percent && value.EndsWith("em", StringComparison.Ordinal);
        if (!percent && !em) return null;
        var number = Number(percent ? value[..^1] : value[..^2], offset);
        var multiple = percent ? number / 100 : number;
        if (multiple <= 0 || multiple > 10)
            throw Invalid("Percentage/em line-height must be positive and at most 10 times the font-size.", offset);
        return multiple;
    }

    private static double Length(string value, int offset)
    {
        if (value == "0") return 0;
        if (value.EndsWith("pt", StringComparison.Ordinal)) return Number(value[..^2], offset);
        if (value.EndsWith("px", StringComparison.Ordinal)) return Number(value[..^2], offset) * 0.75;
        if (value.EndsWith("mm", StringComparison.Ordinal)) return Number(value[..^2], offset) * (72d / 25.4d);
        if (value.EndsWith("cm", StringComparison.Ordinal)) return Number(value[..^2], offset) * (72d / 2.54d);
        if (value.EndsWith("in", StringComparison.Ordinal)) return Number(value[..^2], offset) * 72d;
        if (value.EndsWith("pc", StringComparison.Ordinal)) return Number(value[..^2], offset) * 12d;
        if (value.EndsWith('q')) return Number(value[..^1], offset) * ((72d / 25.4d) / 4d);
        throw Invalid("This length requires pt, px, mm, cm, in, pc, or q units.", offset);
    }

    private static double Margin(string value, int offset)
    {
        var result = Length(value, offset);
        if (result is < 0 or > 14_400) throw Invalid("Margins must be between 0 and 14400pt.", offset);
        return result;
    }

    private static double Number(string value, int offset)
    {
        if (!double.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out var result) || !double.IsFinite(result))
            throw Invalid("Expected a finite decimal number.", offset);
        return result;
    }

    private static bool PageBreak(string value, int offset) => value switch
    {
        "page" => true, "auto" => false, _ => throw Invalid("Only auto/page breaks are supported.", offset)
    };

    internal static Rgb Color(string value, int offset)
    {
        value = CssNamedColors.Hex(value) ?? value;
        if (value.Length == 4 && value[0] == '#')
            value = $"#{value[1]}{value[1]}{value[2]}{value[2]}{value[3]}{value[3]}";
        if (value.Length != 7 || value[0] != '#' || !uint.TryParse(value.AsSpan(1),
            NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var rgb))
            throw Invalid("Use #rgb, #rrggbb or an opaque standard CSS named color.", offset);
        return new(((rgb >> 16) & 255) / 255d, ((rgb >> 8) & 255) / 255d, (rgb & 255) / 255d);
    }

    private static FactsPdfException Invalid(string text, int offset) => new("FPDF1202", text, offset);
}
