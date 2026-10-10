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
            // Fail unsupported properties first even when the value contains
            // functions or multiple tokens.
            var property = Property(name, node.Span.Start);
            var terms = Terms(node);
            if (terms.Count == 0)
                throw new FactsPdfException("FPDF1202", "Empty CSS declaration.", node.Span.Start);

            void Emit(CssProperty target, string value)
            {
                var order = budget.Declaration(node.Span.Start);
                output.Add(new(target, name, value, node.Important, order, node.Span.Start));
            }
            if (name is "padding" or "border-width" or "border-style" or "border-color")
            {
                if (terms.Count > 4) throw new FactsPdfException("FPDF1202", "Box shorthand takes one to four values.", node.Span.Start);
                var parts = terms.ToArray();
                if (!(parts.Length == 1 && Wide(parts[0])))
                    foreach (var v in parts) ValidateComponent(name, v, node.Span.Start);
                var sides = ExpandFour(parts);
                CssProperty[] properties = name switch
                {
                    "padding" => [CssProperty.PaddingTop, CssProperty.PaddingRight, CssProperty.PaddingBottom, CssProperty.PaddingLeft],
                    "border-width" => [CssProperty.BorderTopWidth, CssProperty.BorderRightWidth, CssProperty.BorderBottomWidth, CssProperty.BorderLeftWidth],
                    "border-style" => [CssProperty.BorderTopStyle, CssProperty.BorderRightStyle, CssProperty.BorderBottomStyle, CssProperty.BorderLeftStyle],
                    _ => [CssProperty.BorderTopColor, CssProperty.BorderRightColor, CssProperty.BorderBottomColor, CssProperty.BorderLeftColor]
                };
                for (var i = 0; i < 4; i++) Emit(properties[i], sides[i]);
                continue;
            }
            if (name is "border" or "border-top" or "border-right" or "border-bottom" or "border-left")
            {
                if (terms.Count > 3) throw new FactsPdfException("FPDF1202", "Border shorthand takes up to three terms.", node.Span.Start);
                var words = terms.ToArray();
                string width = "medium", style = "none", color = "currentcolor";
                if (words.Length == 1 && Wide(words[0]))
                    width = style = color = words[0];
                else
                {
                    bool hasWidth = false, hasStyle = false, hasColor = false;
                    foreach (var word in words)
                    {
                        if (Wide(word)) throw new FactsPdfException("FPDF1202", "CSS-wide keyword must be the only shorthand value.", node.Span.Start);
                        if (CssPaintValues.TryBorderWidth(word, out _))
                        {
                            if (hasWidth) throw new FactsPdfException("FPDF1202", "Duplicate border width.", node.Span.Start);
                            width = word; hasWidth = true;
                        }
                        else if (word is "solid" or "none")
                        {
                            if (hasStyle) throw new FactsPdfException("FPDF1202", "Duplicate border style.", node.Span.Start);
                            style = word; hasStyle = true;
                        }
                        else
                        {
                            _ = CssPaintValues.BorderColor(word, node.Span.Start);
                            if (hasColor) throw new FactsPdfException("FPDF1202", "Duplicate border color.", node.Span.Start);
                            color = word; hasColor = true;
                        }
                    }
                }
                var edges = name switch
                {
                    "border-top" => new[] { 0 },
                    "border-right" => new[] { 1 },
                    "border-bottom" => new[] { 2 },
                    "border-left" => new[] { 3 },
                    _ => new[] { 0, 1, 2, 3 }
                };
                foreach (var edge in edges)
                {
                    Emit((CssProperty)((int)CssProperty.BorderTopWidth + edge), width);
                    Emit((CssProperty)((int)CssProperty.BorderTopStyle + edge), style);
                    Emit((CssProperty)((int)CssProperty.BorderTopColor + edge), color);
                }
                continue;
            }
            if (terms.Count != 1)
                throw new FactsPdfException("FPDF1202", "Expected one supported CSS value.", node.Span.Start);
            var token = terms[0];
            if (!Wide(token)) ValidateComponent(name, token, node.Span.Start);
            Emit(property, token);
        }
        return output.ToArray();
    }

    private static bool Wide(string text) => text is "inherit" or "initial" or "unset";

    private static CssProperty Property(string name, int offset) => name switch
    {
        "font-size" => CssProperty.FontSize, "color" => CssProperty.Color,
        "line-height" => CssProperty.LineHeight, "text-align" => CssProperty.TextAlign,
        "margin-top" => CssProperty.MarginTop, "margin-bottom" => CssProperty.MarginBottom,
        "break-before" => CssProperty.BreakBefore, "break-after" => CssProperty.BreakAfter,
        "width" => CssProperty.Width, "padding" or "padding-top" => CssProperty.PaddingTop,
        "padding-right" => CssProperty.PaddingRight, "padding-bottom" => CssProperty.PaddingBottom,
        "padding-left" => CssProperty.PaddingLeft,
        "background-color" => CssProperty.BackgroundColor,
        "border" or "border-top" or "border-width" or "border-top-width" => CssProperty.BorderTopWidth,
        "border-right" or "border-right-width" => CssProperty.BorderRightWidth,
        "border-bottom" or "border-bottom-width" => CssProperty.BorderBottomWidth,
        "border-left" or "border-left-width" => CssProperty.BorderLeftWidth,
        "border-style" or "border-top-style" => CssProperty.BorderTopStyle,
        "border-right-style" => CssProperty.BorderRightStyle,
        "border-bottom-style" => CssProperty.BorderBottomStyle,
        "border-left-style" => CssProperty.BorderLeftStyle,
        "border-color" or "border-top-color" => CssProperty.BorderTopColor,
        "border-right-color" => CssProperty.BorderRightColor,
        "border-bottom-color" => CssProperty.BorderBottomColor,
        "border-left-color" => CssProperty.BorderLeftColor,
        _ => throw new FactsPdfException("FPDF1201", $"CSS property '{name}' is not supported.", offset)
    };

    private static List<string> Terms(CssDeclarationNode node)
    {
        var terms = new List<string>();
        var whitespace = true;
        foreach (var component in node.Values)
        {
            if (component is CssTokenComponent x && x.Token.Kind == CssSyntaxTokenKind.Whitespace)
            { whitespace = true; continue; }
            if (!whitespace) throw new FactsPdfException("FPDF1202", "CSS terms require whitespace separators.", component.Span.Start);
            if (component is not CssTokenComponent t)
                throw new FactsPdfException("FPDF1202", "CSS functions/blocks are unsupported in box values.", component.Span.Start);
            var token = t.Token;
            var value = token.Kind switch
            {
                CssSyntaxTokenKind.Ident or CssSyntaxTokenKind.Number => token.Value,
                CssSyntaxTokenKind.Dimension => token.Value + token.Unit,
                CssSyntaxTokenKind.Percentage => token.Value + "%",
                CssSyntaxTokenKind.Hash => "#" + token.Value,
                _ => throw new FactsPdfException("FPDF1202", "Unsupported CSS value token.", token.Span.Start)
            };
            terms.Add(value.ToLowerInvariant());
            whitespace = false;
        }
        return terms;
    }

    private static string[] ExpandFour(string[] parts) => parts.Length switch
    {
        1 => [parts[0], parts[0], parts[0], parts[0]],
        2 => [parts[0], parts[1], parts[0], parts[1]],
        3 => [parts[0], parts[1], parts[2], parts[1]],
        4 => parts,
        _ => throw new InvalidOperationException("Invalid shorthand arity.")
    };

    private static void ValidateComponent(string name, string token, int offset)
    {
        if (name == "width") { _ = CssBoxValues.Width(token, offset); }
        else if (name is "padding" or "padding-top" or "padding-right" or "padding-bottom" or "padding-left")
            _ = CssBoxValues.Length(token, offset);
        else if (name == "background-color") _ = CssPaintValues.Background(token, offset);
        else if (name.Contains("border", StringComparison.Ordinal))
        {
            if (name.EndsWith("width", StringComparison.Ordinal)) _ = CssPaintValues.BorderWidth(token, offset);
            else if (name.EndsWith("style", StringComparison.Ordinal)) _ = CssPaintValues.BorderStyle(token, offset);
            else if (name.EndsWith("color", StringComparison.Ordinal)) _ = CssPaintValues.BorderColor(token, offset);
            else throw new InvalidOperationException("Shorthand validation should be handled separately.");
        }
        else
            _ = InlineCss.Apply(ValidationStyle, name + ":" + token,
                paragraph: true, inline: false, offset);
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
