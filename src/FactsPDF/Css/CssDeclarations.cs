namespace FactsPDF;

internal enum CssProperty
{
    FontSize, FontWeight, FontStyle, Color, LineHeight, TextAlign, MarginTop, MarginBottom, BreakBefore, BreakAfter, TextDecoration,
    WhiteSpace, OverflowWrap, TextIndent, LetterSpacing, WordSpacing, TextTransform, Width, PaddingTop, PaddingRight, PaddingBottom, PaddingLeft, BackgroundColor,
    BorderTopWidth, BorderRightWidth, BorderBottomWidth, BorderLeftWidth,
    BorderTopStyle, BorderRightStyle, BorderBottomStyle, BorderLeftStyle,
    BorderTopColor, BorderRightColor, BorderBottomColor, BorderLeftColor,
    MarginRight, MarginLeft, BoxSizing, MinWidth, MaxWidth, Display, ListStyleType, VerticalAlign
}
internal sealed record CssDeclaration(CssProperty Property, string Name, string Value, bool Important, int Order, int Offset);

internal static class CssDeclarations
{
    internal static TextStyle Apply(TextStyle style, TextStyle parent, TextStyle initial, CssDeclaration declaration, bool paragraph, double rootFontSize)
    {
        var property = declaration.Property;
        if (!paragraph && property is CssProperty.MarginTop or CssProperty.MarginBottom or CssProperty.MarginLeft or CssProperty.MarginRight or CssProperty.BreakBefore or CssProperty.BreakAfter or CssProperty.WhiteSpace or CssProperty.OverflowWrap or CssProperty.TextIndent)
            throw new FactsPdfException("FPDF1201", $"CSS property '{declaration.Name}' is only supported on paragraphs/headings.", declaration.Offset);
        if (declaration.Value is not ("inherit" or "initial" or "unset"))
        {
            if (property == CssProperty.FontSize)
            {
                var rem = InlineCss.RootRelativeScale(declaration.Value, declaration.Offset, 144);
                if (rem.HasValue)
                {
                    var computed = rootFontSize * rem.Value;
                    if (!double.IsFinite(computed) || computed is < 1 or > 144)
                        throw new FactsPdfException("FPDF1202",
                            "Computed root-relative font-size must be between 1pt and 144pt.", declaration.Offset);
                    return style with { FontSize = computed };
                }
                var relative = InlineCss.RelativeFontSizeScale(declaration.Value, declaration.Offset);
                if (relative.HasValue)
                {
                    // Relative to the actual computed parent, never the UA
                    // heading size multiplier or a previous losing declaration.
                    var computed = parent.FontSize * relative.Value;
                    if (!double.IsFinite(computed) || computed is < 1 or > 144)
                        throw new FactsPdfException("FPDF1202",
                            "Computed relative font-size must be between 1pt and 144pt.", declaration.Offset);
                    return style with { FontSize = computed };
                }
            }
            if (property == CssProperty.LineHeight)
            {
                var rem = InlineCss.RootRelativeScale(declaration.Value, declaration.Offset, 1440);
                if (rem.HasValue)
                {
                    var computed = rootFontSize * rem.Value;
                    if (!double.IsFinite(computed) || computed is < 0.1 or > 1440)
                        throw new FactsPdfException("FPDF1202",
                            "Computed root-relative line-height must be between 0.1pt and 1440pt.", declaration.Offset);
                    return style with { AbsoluteLineHeight = computed };
                }
            }
            if (property is CssProperty.LetterSpacing or CssProperty.WordSpacing or CssProperty.TextIndent
                && declaration.Value.EndsWith("em", StringComparison.Ordinal))
            {
                // Compute the winner after font-size; inherit fixed-point metrics.
                var value = CssBoxValues.Length(declaration.Value, declaration.Offset,
                    rootFontSize, allowRem: true, elementFontSize: style.FontSize, allowEm: true).Value;
                if (property is CssProperty.LetterSpacing or CssProperty.WordSpacing && value > 24)
                    throw new FactsPdfException("FPDF1202",
                        "Computed letter/word spacing must be from 0pt to 24pt.", declaration.Offset);
                return property switch
                {
                    CssProperty.LetterSpacing => style with { LetterSpacing = value },
                    CssProperty.WordSpacing => style with { WordSpacing = value },
                    CssProperty.TextIndent => style with { FirstLineIndent = new CssLength(value) },
                    _ => throw new InvalidOperationException()
                };
            }
            if ((property is CssProperty.MarginTop or CssProperty.MarginBottom)
                && declaration.Value.EndsWith("em", StringComparison.Ordinal))
            {
                // Compute only the winning margin: rem from the root,
                // em from this element's final font-size, both as points.
                var margin = CssBoxValues.Length(declaration.Value, declaration.Offset,
                    rootFontSize, allowRem: true, elementFontSize: style.FontSize, allowEm: true).Value;
                return property == CssProperty.MarginTop
                    ? style with { MarginBefore = margin }
                    : style with { MarginAfter = margin };
            }
            return InlineCss.Apply(style, declaration.Name + ":" + declaration.Value, paragraph, inline: false, declaration.Offset);
        }
        var inherited = property is CssProperty.FontSize or CssProperty.FontWeight or CssProperty.FontStyle or CssProperty.Color or CssProperty.LineHeight or CssProperty.TextAlign or CssProperty.WhiteSpace or CssProperty.OverflowWrap or CssProperty.TextIndent or CssProperty.LetterSpacing or CssProperty.WordSpacing or CssProperty.TextTransform;
        var source = declaration.Value == "inherit" || declaration.Value == "unset" && inherited ? parent : initial;
        return property switch
        {
            CssProperty.FontSize => style with { FontSize = source.FontSize },
            CssProperty.FontWeight => style with { Bold = source.Bold },
            CssProperty.FontStyle => style with { Italic = source.Italic },
            CssProperty.Color => style with { Color = source.Color },
            CssProperty.LineHeight => style with { LineHeight = source.LineHeight, AbsoluteLineHeight = source.AbsoluteLineHeight },
            CssProperty.TextAlign => style with { Alignment = source.Alignment },
            CssProperty.TextDecoration => style with { Decoration = source.Decoration },
            CssProperty.WhiteSpace => style with { NoWrap = source.NoWrap, PreserveLineBreaks = source.PreserveLineBreaks, PreserveSpaces = source.PreserveSpaces },
            CssProperty.OverflowWrap => style with { BreakWords = source.BreakWords },
            CssProperty.TextIndent => style with { FirstLineIndent = source.FirstLineIndent },
            CssProperty.LetterSpacing => style with { LetterSpacing = source.LetterSpacing },
            CssProperty.WordSpacing => style with { WordSpacing = source.WordSpacing },
            CssProperty.TextTransform => style with { Transform = source.Transform },
            CssProperty.MarginTop => style with { MarginBefore = source.MarginBefore },
            CssProperty.MarginBottom => style with { MarginAfter = source.MarginAfter },
            CssProperty.BreakBefore => style with { BreakBefore = source.BreakBefore },
            CssProperty.BreakAfter => style with { BreakAfter = source.BreakAfter, KeepWithNext = source.KeepWithNext },
            _ => throw new InvalidOperationException("Unrecognized CSS property.")
        };
    }
    internal static bool IsBoxProperty(CssProperty property) => property >= CssProperty.Width && property <= CssProperty.MaxWidth;

    // Vertical panel margins have a distinct box-level meaning: they sit
    // outside decorations. Paragraphs/headings retain TextStyle margins.
    internal static BoxStyle ApplyPanelVerticalMargin(BoxStyle current, BoxStyle parent,
        CssDeclaration declaration, double rootFontSize, double elementFontSize)
    {
        var wide = declaration.Value is "inherit" or "initial" or "unset";
        var inherited = declaration.Value == "inherit" ? parent : new BoxStyle();
        var length = wide ? default : CssBoxValues.Length(declaration.Value, declaration.Offset,
            rootFontSize, allowRem: true, elementFontSize: elementFontSize, allowEm: true);
        if (length.IsPercent)
            throw new FactsPdfException("FPDF1202",
                "Report-panel vertical margins do not support percentages.", declaration.Offset);
        return declaration.Property switch
        {
            CssProperty.MarginTop => current with { MarginTop = wide ? inherited.MarginTop : length },
            CssProperty.MarginBottom => current with { MarginBottom = wide ? inherited.MarginBottom : length },
            _ => throw new InvalidOperationException("Expected vertical panel margin.")
        };
    }

    internal static BoxStyle ApplyBox(BoxStyle current, BoxStyle parent, CssDeclaration declaration,
        double rootFontSize, double elementFontSize)
    {
        var property = declaration.Property;
        if (!IsBoxProperty(property)) throw new InvalidOperationException("Expected a box property.");
        var value = declaration.Value;
        var wide = value is "inherit" or "initial" or "unset";
        var from = value == "inherit" ? parent : new BoxStyle();

        if (property == CssProperty.BoxSizing)
            return current with { BorderBoxSizing = wide ? from.BorderBoxSizing : value == "border-box" };
        if (property == CssProperty.Width)
            return current with { Width = wide ? from.Width : CssBoxValues.Width(value, declaration.Offset, rootFontSize, elementFontSize) };
        if (property == CssProperty.MinWidth)
            return current with { MinWidth = wide ? from.MinWidth : value == "auto" ? null : CssBoxValues.Length(value, declaration.Offset, rootFontSize, allowRem: true,
                elementFontSize: elementFontSize, allowEm: true) };
        if (property == CssProperty.MaxWidth)
            return current with { MaxWidth = wide ? from.MaxWidth : value == "none" ? null : CssBoxValues.Length(value, declaration.Offset, rootFontSize, allowRem: true,
                elementFontSize: elementFontSize, allowEm: true) };
        if (property >= CssProperty.PaddingTop && property <= CssProperty.PaddingLeft)
        {
            var length = wide ? default : CssBoxValues.Length(value, declaration.Offset, rootFontSize, allowRem: true,
                elementFontSize: elementFontSize, allowEm: true);
            return property switch
            {
                CssProperty.PaddingTop => current with { PaddingTop = wide ? from.PaddingTop : length },
                CssProperty.PaddingRight => current with { PaddingRight = wide ? from.PaddingRight : length },
                CssProperty.PaddingBottom => current with { PaddingBottom = wide ? from.PaddingBottom : length },
                CssProperty.PaddingLeft => current with { PaddingLeft = wide ? from.PaddingLeft : length },
                _ => throw new InvalidOperationException()
            };
        }
        if (property is CssProperty.MarginLeft or CssProperty.MarginRight)
        {
            var auto = !wide && value == "auto";
            var length = wide || auto ? default : CssBoxValues.Length(value, declaration.Offset,
                rootFontSize, allowRem: true, elementFontSize: elementFontSize, allowEm: true);
            return property == CssProperty.MarginLeft
                ? current with
                {
                    MarginLeft = wide ? from.MarginLeft : length,
                    MarginLeftAuto = wide ? from.MarginLeftAuto : auto
                }
                : current with
                {
                    MarginRight = wide ? from.MarginRight : length,
                    MarginRightAuto = wide ? from.MarginRightAuto : auto
                };
        }
        if (property == CssProperty.BackgroundColor)
            return current with { BackgroundColor = wide ? from.BackgroundColor :
                CssPaintValues.Background(value, declaration.Offset) };

        var index = (int)property - (int)CssProperty.BorderTopWidth;
        var side = index % 4;
        var component = index / 4;
        BorderEdge old = side switch
        {
            0 => current.BorderTop, 1 => current.BorderRight, 2 => current.BorderBottom,
            3 => current.BorderLeft, _ => throw new InvalidOperationException()
        };
        BorderEdge inherited = side switch
        {
            0 => from.BorderTop, 1 => from.BorderRight, 2 => from.BorderBottom,
            3 => from.BorderLeft, _ => throw new InvalidOperationException()
        };
        var changed = component switch
        {
            0 => old with { Width = wide ? inherited.Width : CssPaintValues.BorderWidth(value, declaration.Offset) },
            1 => old with
            {
                Solid = wide ? inherited.Solid : value != "none",
                Pattern = wide ? inherited.Pattern : CssPaintValues.BorderStyle(value, declaration.Offset)
            },
            2 => old with { Color = wide ? inherited.Color : CssPaintValues.BorderColor(value, declaration.Offset) },
            _ => throw new InvalidOperationException("Unrecognized border property.")
        };
        return side switch
        {
            0 => current with { BorderTop = changed },
            1 => current with { BorderRight = changed },
            2 => current with { BorderBottom = changed },
            3 => current with { BorderLeft = changed },
            _ => throw new InvalidOperationException()
        };
    }
}
