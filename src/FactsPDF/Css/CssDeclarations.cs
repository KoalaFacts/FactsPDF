namespace FactsPDF;

internal enum CssProperty { FontSize, Color, LineHeight, TextAlign, MarginTop, MarginBottom, BreakBefore, BreakAfter }
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
    private static FactsPdfException Invalid(string message, int offset) => new("FPDF1202", message, offset);
}
