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
            // Only opaque RGB functions are lowered to their equivalent hex
            // colour. All downstream cascade, box paint and PDF serializers
            // therefore use exactly the already audited solid-colour path.
            var values = node.Values.Select(CanonicalOpaqueRgb).ToArray();
            var terms = Terms(node with { Values = values });
            var valueTokens = values.OfType<CssTokenComponent>()
                .Where(x => x.Token.Kind != CssSyntaxTokenKind.Whitespace).ToArray();
            if (terms.Count == 0)
                throw new FactsPdfException("FPDF1202", "Empty CSS declaration.", node.Span.Start);

            void Emit(CssProperty target, string value, int offset = -1, string? targetName = null)
            {
                var sourceOffset = offset < 0 ? node.Span.Start : offset;
                var order = budget.Declaration(sourceOffset);
                output.Add(new(target, targetName ?? name, value, node.Important, order, sourceOffset));
            }
            if (name == "margin")
            {
                if (terms.Count > 4)
                    throw new FactsPdfException("FPDF1202", "Margin shorthand takes one to four values.", node.Span.Start);
                var parts = terms.ToArray();
                for (var i = 0; i < parts.Length; i++)
                {
                    // Normalize numeric zero with its original source location.
                    if (valueTokens[i].Token.Kind == CssSyntaxTokenKind.Number
                        && double.TryParse(parts[i], System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out var zero)
                        && double.IsFinite(zero) && zero == 0)
                        parts[i] = "0";
                }
                if (parts.Length == 1 && Wide(parts[0]))
                {
                    Emit(CssProperty.MarginTop, parts[0], valueTokens[0].Token.Span.Start, "margin-top");
                    Emit(CssProperty.MarginRight, parts[0], valueTokens[0].Token.Span.Start, "margin-right");
                    Emit(CssProperty.MarginBottom, parts[0], valueTokens[0].Token.Span.Start, "margin-bottom");
                    Emit(CssProperty.MarginLeft, parts[0], valueTokens[0].Token.Span.Start, "margin-left");
                    continue;
                }
                var sides = ExpandFour(parts);
                for (var side = 0; side < 4; side++)
                {
                    var source = parts.Length switch
                    {
                        1 => 0, 2 => side % 2,
                        3 => side == 3 ? 1 : side, _ => side
                    };
                    var offset = valueTokens[source].Token.Span.Start;
                    if (Wide(sides[side]))
                        throw new FactsPdfException("FPDF1202",
                            "A CSS-wide margin keyword must be the only value.", offset);
                    if (side % 2 == 0)
                        _ = InlineCss.Apply(ValidationStyle, "margin-top:" + sides[side],
                            paragraph: true, inline: false, offset);
                    else if (sides[side] != "auto")
                        _ = CssBoxValues.Length(sides[side], offset);
                }
                Emit(CssProperty.MarginTop, sides[0], valueTokens[0].Token.Span.Start, "margin-top");
                var rightSource = parts.Length == 1 ? 0 : 1;
                Emit(CssProperty.MarginRight, sides[1], valueTokens[rightSource].Token.Span.Start, "margin-right");
                var bottomSource = parts.Length switch { 1 => 0, 2 => 0, _ => 2 };
                Emit(CssProperty.MarginBottom, sides[2], valueTokens[bottomSource].Token.Span.Start, "margin-bottom");
                var leftSource = parts.Length == 4 ? 3 : rightSource;
                Emit(CssProperty.MarginLeft, sides[3], valueTokens[leftSource].Token.Span.Start, "margin-left");
                continue;
            }
            if (name is "padding" or "border-width" or "border-style" or "border-color")
            {
                if (terms.Count > 4) throw new FactsPdfException("FPDF1202", "Box shorthand takes one to four values.", node.Span.Start);
                var parts = terms.ToArray();
                if (!(parts.Length == 1 && Wide(parts[0])))
                    for (var i = 0; i < parts.Length; i++)
                        ValidateComponent(name, parts[i], valueTokens[i].Token.Span.Start);
                var sides = ExpandFour(parts);
                CssProperty[] properties = name switch
                {
                    "padding" => [CssProperty.PaddingTop, CssProperty.PaddingRight, CssProperty.PaddingBottom, CssProperty.PaddingLeft],
                    "border-width" => [CssProperty.BorderTopWidth, CssProperty.BorderRightWidth, CssProperty.BorderBottomWidth, CssProperty.BorderLeftWidth],
                    "border-style" => [CssProperty.BorderTopStyle, CssProperty.BorderRightStyle, CssProperty.BorderBottomStyle, CssProperty.BorderLeftStyle],
                    _ => [CssProperty.BorderTopColor, CssProperty.BorderRightColor, CssProperty.BorderBottomColor, CssProperty.BorderLeftColor]
                };
                for (var i = 0; i < 4; i++)
                {
                    // Expand in TRBL order but charge a generated longhand
                    // against the CSS token from which that side originated.
                    var sourceIndex = parts.Length switch
                    {
                        1 => 0,
                        2 => i % 2,
                        3 => i == 3 ? 1 : i,
                        _ => i
                    };
                    Emit(properties[i], sides[i], valueTokens[sourceIndex].Token.Span.Start);
                }
                continue;
            }
            if (name is "border" or "border-top" or "border-right" or "border-bottom" or "border-left")
            {
                if (terms.Count > 3) throw new FactsPdfException("FPDF1202", "Border shorthand takes up to three terms.", node.Span.Start);
                var words = terms.ToArray();
                string width = "medium", style = "none", color = "currentcolor";
                var initialOffset = valueTokens[0].Token.Span.Start;
                var widthOffset = initialOffset;
                var styleOffset = initialOffset;
                var colorOffset = initialOffset;
                if (words.Length == 1 && Wide(words[0]))
                    width = style = color = words[0];
                else
                {
                    bool hasWidth = false, hasStyle = false, hasColor = false;
                    for (var i = 0; i < words.Length; i++)
                    {
                        var word = words[i];
                        var offset = valueTokens[i].Token.Span.Start;
                        if (Wide(word)) throw new FactsPdfException("FPDF1202", "CSS-wide keyword must be the only shorthand value.", offset);
                        if (CssPaintValues.TryBorderWidth(word, out _))
                        {
                            if (hasWidth) throw new FactsPdfException("FPDF1202", "Duplicate border width.", offset);
                            width = word; hasWidth = true; widthOffset = offset;
                        }
                        else if (word is "solid" or "none")
                        {
                            if (hasStyle) throw new FactsPdfException("FPDF1202", "Duplicate border style.", offset);
                            style = word; hasStyle = true; styleOffset = offset;
                        }
                        else
                        {
                            _ = CssPaintValues.BorderColor(word, offset);
                            if (hasColor) throw new FactsPdfException("FPDF1202", "Duplicate border color.", offset);
                            color = word; hasColor = true; colorOffset = offset;
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
                    Emit((CssProperty)((int)CssProperty.BorderTopWidth + edge), width, widthOffset);
                    Emit((CssProperty)((int)CssProperty.BorderTopStyle + edge), style, styleOffset);
                    Emit((CssProperty)((int)CssProperty.BorderTopColor + edge), color, colorOffset);
                }
                continue;
            }
            if (terms.Count != 1)
                throw new FactsPdfException("FPDF1202", "Expected one supported CSS value.", node.Span.Start);
            var token = terms[0];
            var tokenOffset = valueTokens[0].Token.Span.Start;
            if (property == CssProperty.FontWeight)
            {
                var syntax = valueTokens[0].Token;
                if (syntax.Kind == CssSyntaxTokenKind.Number
                    && double.TryParse(syntax.Value, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var weight)
                    && weight is 400 or 700)
                    token = weight == 400 ? "400" : "700";
                else if (syntax.Kind != CssSyntaxTokenKind.Ident || !(Wide(token) || token is "normal" or "bold"))
                    throw new FactsPdfException("FPDF1202", "font-weight requires normal/bold or a number equal to 400/700.", tokenOffset);
            }
            if (property == CssProperty.FontStyle && (valueTokens[0].Token.Kind != CssSyntaxTokenKind.Ident
                || !(Wide(token) || token is "normal" or "italic")))
                throw new FactsPdfException("FPDF1202", "font-style requires normal or italic.", tokenOffset);
            if (!Wide(token)) ValidateComponent(name, token, tokenOffset);
            Emit(property, token, tokenOffset);
        }
        return output.ToArray();
    }

    private static bool Wide(string text) => text is "inherit" or "initial" or "unset";

    private static CssProperty Property(string name, int offset) => name switch
    {
        "font-size" => CssProperty.FontSize, "color" => CssProperty.Color,
        "font-weight" => CssProperty.FontWeight, "font-style" => CssProperty.FontStyle,
        "line-height" => CssProperty.LineHeight, "text-align" => CssProperty.TextAlign,
        "white-space" => CssProperty.WhiteSpace,
        "text-decoration" or "text-decoration-line" => CssProperty.TextDecoration,
        "margin" or "margin-top" => CssProperty.MarginTop, "margin-bottom" => CssProperty.MarginBottom,
        "margin-left" => CssProperty.MarginLeft, "margin-right" => CssProperty.MarginRight,
        "break-before" => CssProperty.BreakBefore, "break-after" => CssProperty.BreakAfter,
        "width" => CssProperty.Width, "box-sizing" => CssProperty.BoxSizing, "padding" or "padding-top" => CssProperty.PaddingTop,
        "padding-right" => CssProperty.PaddingRight, "padding-bottom" => CssProperty.PaddingBottom,
        "padding-left" => CssProperty.PaddingLeft,
        "background" or "background-color" => CssProperty.BackgroundColor,
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

    // The AST retains CSS functions rather than accepting arbitrary raw text.
    // Fail closed on alpha blending, percentages, arithmetic and nested functions:
    // none of them can be painted as a fully opaque PDF RGB colour.
    private static CssSyntaxComponent CanonicalOpaqueRgb(CssSyntaxComponent component)
    {
        if (component is not CssFunctionComponent function) return component;
        var name = function.Name.ToLowerInvariant();
        if (name is not ("rgb" or "rgba"))
            throw new FactsPdfException("FPDF1202", "Only opaque rgb()/rgba() integer colours are supported.", function.Span.Start);

        var tokens = new List<CssSyntaxToken>();
        var spaced = new List<bool>();
        var whitespace = false;
        foreach (var entry in function.Values)
        {
            if (entry is not CssTokenComponent item)
                throw new FactsPdfException("FPDF1202", "Nested colour functions are unsupported.", entry.Span.Start);
            if (item.Token.Kind == CssSyntaxTokenKind.Whitespace)
            {
                whitespace = true;
                continue;
            }
            tokens.Add(item.Token);
            spaced.Add(whitespace);
            whitespace = false;
        }

        // Legacy comma syntax and modern space-delimited rgb(), without alpha.
        // rgba() is deliberately limited to comma syntax and alpha exactly 1.
        var commaForm = tokens.Count == (name == "rgb" ? 5 : 7);
        if (commaForm)
            for (var i = 1; i < tokens.Count; i += 2)
                commaForm &= tokens[i].Kind == CssSyntaxTokenKind.Comma;
        var spaceForm = name == "rgb" && tokens.Count == 3 && spaced[1] && spaced[2];
        if (!commaForm && !spaceForm)
            throw new FactsPdfException("FPDF1202", "rgb() needs three integer channels; rgba() requires opaque alpha 1.", function.Span.Start);

        var channels = new byte[3];
        for (var i = 0; i < channels.Length; i++)
        {
            var token = tokens[commaForm ? 2 * i : i];
            if (token.Kind != CssSyntaxTokenKind.Number || !token.IsInteger ||
                !byte.TryParse(token.Value, System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out channels[i]))
                throw new FactsPdfException("FPDF1202", "RGB channels must be integers from 0 through 255.", token.Span.Start);
        }
        if (name == "rgba")
        {
            var alpha = tokens[6];
            if (alpha.Kind != CssSyntaxTokenKind.Number ||
                !IsExactlyOne(alpha.Value))
                throw new FactsPdfException("FPDF1202", "Alpha compositing is not supported; rgba() alpha must be 1.", alpha.Span.Start);
        }

        var hex = $"{channels[0]:x2}{channels[1]:x2}{channels[2]:x2}";
        return new CssTokenComponent(new CssSyntaxToken(
            CssSyntaxTokenKind.Hash, hex, "#" + hex, function.Span));
    }

    // Compare the decimal Number token without binary/decimal rounding. After
    // removing zero digits, exactly one significant digit (1) must remain;
    // its decimal position must be cancelled by the explicit exponent.
    private static bool IsExactlyOne(string value)
    {
        var number = value.AsSpan();
        if (number.Length > 0 && number[0] == '+') number = number[1..];
        if (number.Length == 0 || number[0] == '-') return false;
        var exponentIndex = number.IndexOfAny('e', 'E');
        var exponent = 0;
        if (exponentIndex >= 0)
        {
            if (!int.TryParse(number[(exponentIndex + 1)..],
                System.Globalization.NumberStyles.AllowLeadingSign,
                System.Globalization.CultureInfo.InvariantCulture, out exponent)) return false;
            number = number[..exponentIndex];
        }
        var decimalIndex = number.IndexOf('.');
        var integerDigits = decimalIndex < 0 ? number.Length : decimalIndex;
        var digitIndex = 0;
        var oneIndex = -1;
        foreach (var digit in number)
        {
            if (digit == '.') continue;
            if (digit != '0')
            {
                if (digit != '1' || oneIndex >= 0) return false;
                oneIndex = digitIndex;
            }
            digitIndex++;
        }
        return oneIndex >= 0 && exponent == oneIndex + 1 - integerDigits;
    }

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
        if (name == "font-size")
        {
            // Parse relative syntax even for a rule that does not match any
            // element. Actual parent-relative resolution happens after cascade.
            if (!InlineCss.RelativeFontSizeScale(token, offset).HasValue)
                _ = InlineCss.Apply(ValidationStyle, name + ":" + token,
                    paragraph: true, inline: false, offset);
        }
        else if (name == "line-height")
        {
            // A valid relative value may fit a computed 30pt element but not
            // the static 12pt validation style. Defer computed-height bounds
            // to the winning declaration, as for relative font-size.
            if (!InlineCss.RelativeLineHeightScale(token, offset).HasValue)
                _ = InlineCss.Apply(ValidationStyle, name + ":" + token,
                    paragraph: true, inline: false, offset);
        }
        else if (name == "box-sizing")
        {
            if (token is not ("content-box" or "border-box"))
                throw new FactsPdfException("FPDF1202", "box-sizing requires content-box or border-box.", offset);
        }
        else if (name == "width") { _ = CssBoxValues.Width(token, offset); }
        else if (name is "margin-left" or "margin-right")
        {
            if (token != "auto") _ = CssBoxValues.Length(token, offset);
        }
        else if (name is "padding" or "padding-top" or "padding-right" or "padding-bottom" or "padding-left")
            _ = CssBoxValues.Length(token, offset);
        else if (name is "background" or "background-color") _ = CssPaintValues.Background(token, offset);
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
