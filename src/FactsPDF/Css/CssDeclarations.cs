namespace FactsPDF;

internal enum CssProperty
{
    FontSize, Color, LineHeight, TextAlign, MarginTop, MarginBottom, BreakBefore, BreakAfter,
    Width, PaddingTop, PaddingRight, PaddingBottom, PaddingLeft
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
        if (declaration.Value is "inherit" or "initial" or "unset")
        {
            var from = declaration.Value == "inherit" ? parent : new BoxStyle();
            return property switch
            {
                CssProperty.Width => current with { Width = from.Width },
                CssProperty.PaddingTop => current with { PaddingTop = from.PaddingTop },
                CssProperty.PaddingRight => current with { PaddingRight = from.PaddingRight },
                CssProperty.PaddingBottom => current with { PaddingBottom = from.PaddingBottom },
                CssProperty.PaddingLeft => current with { PaddingLeft = from.PaddingLeft },
                _ => throw new InvalidOperationException("Unrecognized box property.")
            };
        }
        if (property == CssProperty.Width)
            return current with { Width = CssBoxValues.Width(declaration.Value, declaration.Offset) };
        var length = CssBoxValues.Length(declaration.Value, declaration.Offset);
        return property switch
        {
            CssProperty.PaddingTop => current with { PaddingTop = length },
            CssProperty.PaddingRight => current with { PaddingRight = length },
            CssProperty.PaddingBottom => current with { PaddingBottom = length },
            CssProperty.PaddingLeft => current with { PaddingLeft = length },
            _ => throw new InvalidOperationException("Unrecognized box property.")
        };
    }
}
