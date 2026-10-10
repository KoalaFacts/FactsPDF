using FactsPDF.CssSyntax;

namespace FactsPDF;

/// <summary>Strict semantic bridge: a syntactically valid CSS construct need not be renderable.</summary>
internal static class CssSyntaxAdapter
{
    private static readonly TextStyle ValidationStyle =
        new(12, 1.2, new(0, 0, 0), TextAlignment.Left);

    internal static void ThrowIfRecovered(CssSyntaxResult result)
    {
        if (result.Diagnostics.Count > 0)
        {
            var diagnostic = result.Diagnostics[0];
            throw new FactsPdfException("FPDF1203", diagnostic.Message, diagnostic.Span.Start);
        }
    }

    internal static void EnforceStrictUnclosedComments(string css, int baseOffset)
    {
        var quote = '\0';
        for (var i = 0; i < css.Length;)
        {
            if (quote != '\0')
            {
                if (css[i] == '\\' && i + 1 < css.Length) { i += 2; continue; }
                if (css[i] == quote) quote = '\0';
                i++; continue;
            }
            if (css[i] is '\'' or '"') { quote = css[i++]; continue; }
            if (css[i] == '/' && i + 1 < css.Length && css[i + 1] == '*')
            {
                var end = css.IndexOf("*/", i + 2, StringComparison.Ordinal);
                if (end < 0)
                    throw new FactsPdfException("FPDF1203", "Unterminated CSS comment.", baseOffset + i);
                i = end + 2; continue;
            }
            i++;
        }
    }

    internal static CssDeclaration[] CompileKnownDeclarations(
        IReadOnlyList<CssDeclarationNode> nodes, CssBudget budget)
    {
        var output = new List<CssDeclaration>(nodes.Count);
        foreach (var node in nodes)
        {
            budget.Token.ThrowIfCancellationRequested();
            var name = node.Name.ToLowerInvariant();
            if (name == "padding")
            {
                // CSS box shorthands expand into independently cascading longhands.
                // Charge all four derived declarations to the existing work budget.
                var tokens = new List<CssTokenComponent>(4);
                var separated = true;
                foreach (var value in node.Values)
                {
                    if (value is CssTokenComponent whitespace && whitespace.Token.Kind == CssSyntaxTokenKind.Whitespace)
                    { separated = true; continue; }
                    if (value is not CssTokenComponent term || (tokens.Count > 0 && !separated))
                        throw new FactsPdfException("FPDF1202", "Padding values must be whitespace-separated lengths.", value.Span.Start);
                    tokens.Add(term);
                    separated = false;
                }
                if (tokens.Count is < 1 or > 4)
                    throw new FactsPdfException("FPDF1202", "Padding takes one to four lengths.", node.Span.Start);
                var values = tokens.Select(t => TokenValue(t.Token)).ToArray();
                if (values.Length != 1 || values[0] is not ("inherit" or "initial" or "unset"))
                    foreach (var value in values) _ = CssBoxValues.Length(value, node.Span.Start);
                var expanded = values.Length switch
                {
                    1 => new[] { values[0], values[0], values[0], values[0] },
                    2 => new[] { values[0], values[1], values[0], values[1] },
                    3 => new[] { values[0], values[1], values[2], values[1] },
                    _ => values
                };
                var properties = new[] { CssProperty.PaddingTop, CssProperty.PaddingRight,
                    CssProperty.PaddingBottom, CssProperty.PaddingLeft };
                for (var i = 0; i < 4; i++)
                {
                    var order = budget.Declaration(tokens[Math.Min(i, tokens.Count - 1)].Token.Span.Start);
                    output.Add(new(properties[i], name, expanded[i], node.Important, order, node.Span.Start));
                }
                continue;
            }

            var property = name switch
            {
                "font-size" => CssProperty.FontSize,
                "color" => CssProperty.Color,
                "line-height" => CssProperty.LineHeight,
                "text-align" => CssProperty.TextAlign,
                "margin-top" => CssProperty.MarginTop,
                "margin-bottom" => CssProperty.MarginBottom,
                "break-before" => CssProperty.BreakBefore,
                "break-after" => CssProperty.BreakAfter,
                "width" => CssProperty.Width,
                "padding-top" => CssProperty.PaddingTop,
                "padding-right" => CssProperty.PaddingRight,
                "padding-bottom" => CssProperty.PaddingBottom,
                "padding-left" => CssProperty.PaddingLeft,
                _ => throw new FactsPdfException("FPDF1201", $"CSS property '{name}' is not supported.", node.Span.Start)
            };
            var meaningful = node.Values.Where(x => x is not CssTokenComponent t ||
                t.Token.Kind != CssSyntaxTokenKind.Whitespace).ToArray();
            if (meaningful.Length != 1 || meaningful[0] is not CssTokenComponent component)
                throw new FactsPdfException("FPDF1202", "Expected one supported CSS value.", node.Span.Start);
            var valueText = TokenValue(component.Token);
            if (valueText is not ("inherit" or "initial" or "unset"))
            {
                if (property == CssProperty.Width)
                    _ = CssBoxValues.Width(valueText, component.Token.Span.Start);
                else if (CssDeclarations.IsBoxProperty(property))
                    _ = CssBoxValues.Length(valueText, component.Token.Span.Start);
                else
                    _ = InlineCss.Apply(ValidationStyle, name + ":" + valueText,
                        paragraph: true, inline: false, component.Token.Span.Start);
            }
            var declarationOrder = budget.Declaration(node.Span.Start);
            output.Add(new(property, name, valueText, node.Important, declarationOrder, component.Token.Span.Start));
        }
        return output.ToArray();
    }

    private static string TokenValue(CssSyntaxToken token)
    {
        var value = token.Kind switch
        {
            CssSyntaxTokenKind.Ident => token.Value,
            CssSyntaxTokenKind.Number => token.Value,
            CssSyntaxTokenKind.Dimension => token.Value + token.Unit,
            CssSyntaxTokenKind.Percentage => token.Value + "%",
            CssSyntaxTokenKind.Hash => "#" + token.Value,
            _ => throw new FactsPdfException("FPDF1202", "CSS value is not supported.", token.Span.Start)
        };
        return value.ToLowerInvariant();
    }

    internal static CssSelector[] CompileSelectors(
        IReadOnlyList<CssSyntaxComponent> prelude, CssBudget budget, int offset)
    {
        var tokens = new List<CssToken>();
        var space = false;
        foreach (var item in prelude)
        {
            budget.Token.ThrowIfCancellationRequested();
            if (item is not CssTokenComponent entry)
                throw new FactsPdfException("FPDF1203", "Selector functions and nested components are not supported.", item.Span.Start);
            var t = entry.Token;
            if (t.Kind == CssSyntaxTokenKind.Whitespace) { space = true; continue; }
            var kind = t.Kind switch
            {
                CssSyntaxTokenKind.Ident => CssTokenKind.Identifier,
                CssSyntaxTokenKind.Hash => CssTokenKind.Hash,
                CssSyntaxTokenKind.Number or CssSyntaxTokenKind.Dimension or CssSyntaxTokenKind.Percentage
                    => CssTokenKind.Number,
                CssSyntaxTokenKind.String => CssTokenKind.String,
                _ => CssTokenKind.Symbol
            };
            var text = t.Kind switch
            {
                CssSyntaxTokenKind.Hash => "#" + t.Value,
                CssSyntaxTokenKind.Ident => t.Value,
                _ => t.Raw
            };
            tokens.Add(new(kind, text, space, t.Span.Start));
            space = false;
        }
        tokens.Add(new(CssTokenKind.Symbol, "{", space, offset));
        var i = 0;
        var selectors = new List<CssSelector> { CssSelector.Parse(tokens, ref i, budget) };
        while (tokens[i].Is(","))
        {
            i++;
            selectors.Add(CssSelector.Parse(tokens, ref i, budget));
        }
        if (!tokens[i].Is("{"))
            throw new FactsPdfException("FPDF1203", "Invalid CSS selector list.", tokens[i].Offset);
        return selectors.ToArray();
    }
}
