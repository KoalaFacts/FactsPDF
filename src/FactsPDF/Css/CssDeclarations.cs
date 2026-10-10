namespace FactsPDF;

internal enum CssProperty { FontSize, Color, LineHeight, TextAlign, MarginTop, MarginBottom, BreakBefore, BreakAfter }
internal sealed record CssDeclaration(CssProperty Property, string Name, string Value, bool Important, int Order, int Offset);

internal static class CssDeclarations
{
    private static readonly TextStyle ValidationStyle = new(12, 1.2, new(0, 0, 0), TextAlignment.Left);

    internal static CssDeclaration[] Parse(List<CssToken> tokens, ref int i, bool block, CssBudget budget)
    {
        var result = new List<CssDeclaration>();
        while (true)
        {
            budget.Token.ThrowIfCancellationRequested();
            if (tokens[i].Kind == CssTokenKind.End)
            {
                if (block) throw CssTokens.Invalid("Unclosed CSS declaration block.", tokens[i].Offset);
                return result.ToArray();
            }
            if (tokens[i].Is("}") && block) { i++; return result.ToArray(); }
            if (tokens[i].Is(";")) { i++; continue; }
            if (tokens[i].Is("@")) throw new FactsPdfException("FPDF1204", "CSS at-rules are not supported.", tokens[i].Offset);
            var start = tokens[i];
            if (start.Kind != CssTokenKind.Identifier) throw Invalid("Expected a CSS property name.", start.Offset);
            var name = tokens[i++].Text.ToLowerInvariant();
            var property = name switch
            {
                "font-size" => CssProperty.FontSize, "color" => CssProperty.Color,
                "line-height" => CssProperty.LineHeight, "text-align" => CssProperty.TextAlign,
                "margin-top" => CssProperty.MarginTop, "margin-bottom" => CssProperty.MarginBottom,
                "break-before" => CssProperty.BreakBefore, "break-after" => CssProperty.BreakAfter,
                _ => throw new FactsPdfException("FPDF1201", $"CSS property '{name}' is not supported.", start.Offset)
            };
            if (!tokens[i].Is(":")) throw Invalid("Expected property: value.", tokens[i].Offset);
            i++; var valueToken = tokens[i];
            if (valueToken.Kind is not (CssTokenKind.Identifier or CssTokenKind.Number or CssTokenKind.Hash))
                throw Invalid("Expected one supported CSS value.", valueToken.Offset);
            var value = valueToken.Text.ToLowerInvariant(); i++; var important = false;
            if (tokens[i].Is("!"))
            {
                i++;
                if (tokens[i].Kind != CssTokenKind.Identifier || !tokens[i].Text.Equals("important", StringComparison.OrdinalIgnoreCase))
                    throw Invalid("Expected !important.", tokens[i].Offset);
                i++; important = true;
            }
            if (!(tokens[i].Is(";") || block && tokens[i].Is("}") || tokens[i].Kind == CssTokenKind.End))
                throw Invalid("Unsupported value tokens or missing declaration separator.", tokens[i].Offset);
            if (value is not ("inherit" or "initial" or "unset"))
                _ = InlineCss.Apply(ValidationStyle, name + ":" + value, paragraph: true, inline: false, valueToken.Offset);
            var order = budget.Declaration(start.Offset);
            result.Add(new(property, name, value, important, order, valueToken.Offset));
            if (tokens[i].Is(";")) i++;
        }
    }

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
