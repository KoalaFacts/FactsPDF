using System.Globalization;

namespace FactsPDF;

internal static class InlineCss
{
    public static TextStyle Apply(TextStyle style, string css, bool paragraph, bool inline, int offset)
    {
        foreach (var declaration in css.Split(';'))
        {
            if (string.IsNullOrWhiteSpace(declaration)) continue;
            var colon = declaration.IndexOf(':');
            if (colon <= 0) throw Invalid("Expected property: value.", offset);
            var name = declaration[..colon].Trim().ToLowerInvariant();
            var value = declaration[(colon + 1)..].Trim().ToLowerInvariant();
            if (value.Length == 0 || value.Contains('!')) throw Invalid("Empty values and !important are not supported.", offset);
            switch (name)
            {
                case "font-weight":
                    var weight = value switch { "normal" => 400, "bold" => 700, _ => Number(value, offset) };
                    // The engine has only two explicit weight roles, 400 and 700.
                    // Support the common hundred-step CSS values while resolving
                    // 100–500 to the provided normal face and 600–900 to bold.
                    // Do not synthesize intermediate font weights or infer faces.
                    if (weight is < 100 or > 900 || weight % 100 != 0)
                        throw Invalid("font-weight accepts normal/bold or numeric hundreds from 100 through 900.", offset);
                    style = style with { Bold = weight >= 600 };
                    break;
                case "font-style":
                    style = style with { Italic = value switch
                    {
                        "normal" => false, "italic" => true,
                        _ => throw Invalid("font-style supports only normal and italic.", offset)
                    } };
                    break;
                case "font-size":
                    var size = Length(value, offset);
                    if (size is < 1 or > 144) throw Invalid("font-size must be between 1pt and 144pt.", offset);
                    style = style with { FontSize = size };
                    break;
                case "color": style = style with { Color = Color(value, offset) }; break;
                case "text-decoration" or "text-decoration-line":
                    style = style with { Decoration = value switch
                    {
                        "none" => TextDecoration.None,
                        "underline" => TextDecoration.Underline,
                        "line-through" => TextDecoration.LineThrough,
                        _ => throw Invalid("Only none, underline and line-through text decorations are supported.", offset)
                    } };
                    break;
                case "line-height":
                    // Unitless leading remains inherited as a multiplier. A
                    // percentage, em or fixed length instead computes to points
                    // on this element and inherits that absolute value.
                    if (value == "normal")
                    {
                        style = style with { LineHeight = 1.2, AbsoluteLineHeight = null };
                        break;
                    }
                    var relativeScale = RelativeLineHeightScale(value, offset);
                    var physical = value.EndsWith("pt", StringComparison.Ordinal)
                        || value.EndsWith("px", StringComparison.Ordinal)
                        || value.EndsWith("mm", StringComparison.Ordinal)
                        || value.EndsWith("cm", StringComparison.Ordinal)
                        || value.EndsWith("in", StringComparison.Ordinal);
                    if (relativeScale.HasValue || physical)
                    {
                        var computed = physical ? Length(value, offset) : style.FontSize * relativeScale!.Value;
                        // Protect page progression and conversion budgets from
                        // zero/tiny or unbounded line boxes.
                        if (!double.IsFinite(computed) || computed is < 0.1 or > 1440)
                            throw Invalid("Computed line-height must be between 0.1pt and 1440pt.", offset);
                        style = style with { AbsoluteLineHeight = computed };
                        break;
                    }
                    var factor = Number(value, offset);
                    if (factor is < 1 or > 10) throw Invalid("Unitless line-height must be from 1 to 10.", offset);
                    style = style with { LineHeight = factor, AbsoluteLineHeight = null };
                    break;
                case "letter-spacing":
                    // A deliberately bounded fixed advance; no font metrics,
                    // relative units or shaping are inferred at parse time.
                    var tracking = value == "normal" ? 0 : Length(value, offset);
                    if (!double.IsFinite(tracking) || tracking is < 0 or > 24)
                        throw Invalid("letter-spacing must be from 0pt to 24pt.", offset);
                    style = style with { LetterSpacing = tracking };
                    break;
                case "word-spacing":
                    // Only collapsed U+0020 space glyphs receive a fixed extra advance.
                    var wordGap = value == "normal" ? 0 : Length(value, offset);
                    if (!double.IsFinite(wordGap) || wordGap is < 0 or > 24)
                        throw Invalid("word-spacing must be from 0pt to 24pt.", offset);
                    style = style with { WordSpacing = wordGap };
                    break;
                case "text-transform":
                    style = style with { Transform = value switch
                    {
                        "none" => TextTransform.None,
                        "uppercase" => TextTransform.Uppercase,
                        "lowercase" => TextTransform.Lowercase,
                        "capitalize" => TextTransform.Capitalize,
                        _ => throw Invalid("text-transform supports only none, uppercase, lowercase and capitalize for ASCII letters.", offset)
                    } };
                    break;
                case "white-space" when paragraph && !inline:
                    style = style with { NoWrap = value switch
                    {
                        "normal" => false, "nowrap" => true,
                        _ => throw Invalid("Only collapsing normal and nowrap white-space are supported.", offset)
                    } };
                    break;
                case "overflow-wrap" when paragraph && !inline:
                    // Emergency breaking applies only to the whole report
                    // paragraph/heading, never a silently ignored inline span.
                    style = style with { BreakWords = value switch
                    {
                        "normal" => false, "break-word" => true,
                        _ => throw Invalid("overflow-wrap supports only normal and break-word.", offset)
                    } };
                    break;
                case "text-indent" when paragraph && !inline:
                    // Resolve percentages against this paragraph's actual text
                    // content width at layout time, after margins/padding.
                    style = style with { FirstLineIndent = CssBoxValues.Length(value, offset) };
                    break;
                case "text-align" when !inline:
                    style = style with { Alignment = value switch
                    {
                        "left" => TextAlignment.Left, "center" => TextAlignment.Center, "right" => TextAlignment.Right,
                        "justify" => TextAlignment.Justify,
                        _ => throw Invalid("Only left, center, right and word-space justify alignment are supported.", offset)
                    } };
                    break;
                case "margin-top" when paragraph: style = style with { MarginBefore = Margin(value, offset) }; break;
                case "margin-bottom" when paragraph: style = style with { MarginAfter = Margin(value, offset) }; break;
                case "break-before" when paragraph: style = style with { BreakBefore = PageBreak(value, offset) }; break;
                case "break-after" when paragraph: style = style with { BreakAfter = value != "avoid" && PageBreak(value, offset), KeepWithNext = value == "avoid" }; break;
                default: throw new FactsPdfException("FPDF1201", $"CSS property '{name}' is not supported on this element.", offset);
            }
        }
        return style;
    }

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
        throw Invalid("This length requires pt, px, mm, cm, or in units.", offset);
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
        value = value switch
        {
            "black" => "#000000", "white" => "#ffffff", "red" => "#ff0000",
            "green" => "#008000", "blue" => "#0000ff", "gray" or "grey" => "#808080", _ => value
        };
        if (value.Length == 4 && value[0] == '#')
            value = $"#{value[1]}{value[1]}{value[2]}{value[2]}{value[3]}{value[3]}";
        if (value.Length != 7 || value[0] != '#' || !uint.TryParse(value.AsSpan(1),
            NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var rgb))
            throw Invalid("Use #rgb, #rrggbb or a documented named color.", offset);
        return new(((rgb >> 16) & 255) / 255d, ((rgb >> 8) & 255) / 255d, (rgb & 255) / 255d);
    }

    private static FactsPdfException Invalid(string text, int offset) => new("FPDF1202", text, offset);
}
