namespace FactsPDF;

internal enum CssProperty
{
    FontSize, Color, LineHeight, TextAlign, MarginTop, MarginBottom, BreakBefore, BreakAfter,
    Width, PaddingTop, PaddingRight, PaddingBottom, PaddingLeft, BackgroundColor,
    BorderTopWidth, BorderRightWidth, BorderBottomWidth, BorderLeftWidth,
    BorderTopStyle, BorderRightStyle, BorderBottomStyle, BorderLeftStyle,
    BorderTopColor, BorderRightColor, BorderBottomColor, BorderLeftColor
}
internal sealed record CssDeclaration(CssProperty Property, string Name, string Value, bool Important, int Order, int Offset);

internal static class CssDeclarations
{
    internal static TextStyle Apply(TextStyle style, TextStyle parent, TextStyle initial, CssDeclaration declaration, bool paragraph)
    {
        var property = declaration.Property;
        if (!paragraph && property is CssProperty.MarginTop or CssProperty.MarginBottom or CssProperty.BreakBefore or CssProperty.BreakAfter)
            throw new FactsPdfException("FPDF1201", $"CSS property '{declaration.Name}' is only supported on paragraphs/headings.", declaration.Offset);
        if (declaration.Value is not ("inherit" or "initial" or "unset"))
            return InlineCss.Apply(style, declaration.Name + ":" + declaration.Value, paragraph, inline: false, declaration.Offset);
        var inherited = property is CssProperty.FontSize or CssProperty.Color or CssProperty.LineHeight or CssProperty.TextAlign;
        var source = declaration.Value == "inherit" || declaration.Value == "unset" && inherited ? parent : initial;
        return property switch
        {
            CssProperty.FontSize => style with { FontSize = source.FontSize },
            CssProperty.Color => style with { Color = source.Color },
            CssProperty.LineHeight => style with { LineHeight = source.LineHeight },
            CssProperty.TextAlign => style with { Alignment = source.Alignment },
            CssProperty.MarginTop => style with { MarginBefore = source.MarginBefore },
            CssProperty.MarginBottom => style with { MarginAfter = source.MarginAfter },
            CssProperty.BreakBefore => style with { BreakBefore = source.BreakBefore },
            CssProperty.BreakAfter => style with { BreakAfter = source.BreakAfter },
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
