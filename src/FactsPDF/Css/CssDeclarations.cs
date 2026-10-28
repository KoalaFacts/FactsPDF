namespace FactsPDF;

internal enum CssProperty
{
    FontSize, FontWeight, FontStyle, Color, LineHeight, TextAlign, MarginTop, MarginBottom, BreakBefore, BreakAfter, TextDecoration,
    WhiteSpace, TextIndent, LetterSpacing, WordSpacing, TextTransform, Width, PaddingTop, PaddingRight, PaddingBottom, PaddingLeft, BackgroundColor,
    BorderTopWidth, BorderRightWidth, BorderBottomWidth, BorderLeftWidth,
    BorderTopStyle, BorderRightStyle, BorderBottomStyle, BorderLeftStyle,
    BorderTopColor, BorderRightColor, BorderBottomColor, BorderLeftColor,
    MarginRight, MarginLeft, BoxSizing
}
internal sealed record CssDeclaration(CssProperty Property, string Name, string Value, bool Important, int Order, int Offset);

internal static class CssDeclarations
{
    internal static TextStyle Apply(TextStyle style, TextStyle parent, TextStyle initial, CssDeclaration declaration, bool paragraph)
    {
        var property = declaration.Property;
        if (!paragraph && property is CssProperty.MarginTop or CssProperty.MarginBottom or CssProperty.MarginLeft or CssProperty.MarginRight or CssProperty.BreakBefore or CssProperty.BreakAfter or CssProperty.WhiteSpace or CssProperty.TextIndent)
            throw new FactsPdfException("FPDF1201", $"CSS property '{declaration.Name}' is only supported on paragraphs/headings.", declaration.Offset);
        if (declaration.Value is not ("inherit" or "initial" or "unset"))
        {
            if (property == CssProperty.FontSize)
            {
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
            return InlineCss.Apply(style, declaration.Name + ":" + declaration.Value, paragraph, inline: false, declaration.Offset);
        }
        var inherited = property is CssProperty.FontSize or CssProperty.FontWeight or CssProperty.FontStyle or CssProperty.Color or CssProperty.LineHeight or CssProperty.TextAlign or CssProperty.WhiteSpace or CssProperty.TextIndent or CssProperty.LetterSpacing or CssProperty.WordSpacing or CssProperty.TextTransform;
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
            CssProperty.WhiteSpace => style with { NoWrap = source.NoWrap },
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
    internal static bool IsBoxProperty(CssProperty property) => property >= CssProperty.Width;

    internal static BoxStyle ApplyBox(BoxStyle current, BoxStyle parent, CssDeclaration declaration)
    {
        var property = declaration.Property;
        if (!IsBoxProperty(property)) throw new InvalidOperationException("Expected a box property.");
        var value = declaration.Value;
        var wide = value is "inherit" or "initial" or "unset";
        var from = value == "inherit" ? parent : new BoxStyle();

        if (property == CssProperty.BoxSizing)
            return current with { BorderBoxSizing = wide ? from.BorderBoxSizing : value == "border-box" };
        if (property == CssProperty.Width)
            return current with { Width = wide ? from.Width : CssBoxValues.Width(value, declaration.Offset) };
        if (property >= CssProperty.PaddingTop && property <= CssProperty.PaddingLeft)
        {
            var length = wide ? default : CssBoxValues.Length(value, declaration.Offset);
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
            var length = wide || auto ? default : CssBoxValues.Length(value, declaration.Offset);
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
            1 => old with { Solid = wide ? inherited.Solid : CssPaintValues.BorderStyle(value, declaration.Offset) },
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
