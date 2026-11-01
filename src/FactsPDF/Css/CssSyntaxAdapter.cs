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
            var originalName = node.Name.ToLowerInvariant();
            // Legacy print stylesheets use page-break-before/after:always.
            // Normalize only these known aliases into the existing cascade
            // slots; page/auto retain the same actual layout semantics.
            var legacyPageBreak = originalName is "page-break-before" or "page-break-after";
            var name = originalName switch
            {
                "page-break-before" => "break-before",
                "page-break-after" => "break-after",
                _ => originalName
            };
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
            // Eight LTR logical *longhands* name the same physical sides as
            // the existing two-sided logical shorthands. Lowering before
            // cascade preserves physical/logical declaration ordering and
            // !important, including individual inherit/initial/unset sides.
            // No RTL or vertical writing is accepted by this renderer.
            if (name is "margin-inline-start" or "margin-inline-end"
                or "margin-block-start" or "margin-block-end"
                or "padding-inline-start" or "padding-inline-end"
                or "padding-block-start" or "padding-block-end")
            {
                if (terms.Count != 1)
                    throw new FactsPdfException("FPDF1202",
                        "Logical side longhands require exactly one value.", node.Span.Start);
                var physical = name switch
                {
                    "margin-inline-start" => "margin-left",
                    "margin-inline-end" => "margin-right",
                    "margin-block-start" => "margin-top",
                    "margin-block-end" => "margin-bottom",
                    "padding-inline-start" => "padding-left",
                    "padding-inline-end" => "padding-right",
                    "padding-block-start" => "padding-top",
                    _ => "padding-bottom"
                };
                var sourceToken = valueTokens[0].Token;
                var value = terms[0];
                if (sourceToken.Kind == CssSyntaxTokenKind.Number)
                {
                    if (!CssBoxValues.IsExactlyUnitlessZero(value))
                        throw new FactsPdfException("FPDF1202",
                            "Logical box lengths require a supported unit or exact numeric zero.",
                            sourceToken.Span.Start);
                    value = "0";
                }
                if (!Wide(value)) ValidateComponent(physical, value, sourceToken.Span.Start);
                Emit(Property(physical, sourceToken.Span.Start), value,
                    sourceToken.Span.Start, physical);
                continue;
            }
            // CSS four-corner order is top-left, top-right, bottom-right,
            // bottom-left. Validate *all* components, including unmatched
            // selectors; preserve one cascade winner for the full shorthand.
            // Elliptical slash syntax remains rejected. Lower all four corners
            // before winner selection so each longhand competes with its
            // shorthand side under the normal cascade and !important.
            if (name == "border-radius")
            {
                if (terms.Count > 4)
                    throw new FactsPdfException("FPDF1202",
                        "border-radius takes one to four physical corner lengths.", node.Span.Start);
                var parts = terms.ToArray();
                for (var i = 0; i < parts.Length; i++)
                {
                    var source = valueTokens[i].Token;
                    if (source.Kind == CssSyntaxTokenKind.Number &&
                        CssBoxValues.IsExactlyUnitlessZero(parts[i]))
                        parts[i] = "0";
                    if (Wide(parts[i]))
                    {
                        if (parts.Length != 1)
                            throw new FactsPdfException("FPDF1202",
                                "CSS-wide radius keyword must be the only value.", source.Span.Start);
                    }
                    else _ = CssPaintValues.BorderRadius(parts[i], source.Span.Start);
                }
                var corners = ExpandFour(parts);
                var targets = new[] { CssProperty.BorderTopLeftRadius, CssProperty.BorderTopRightRadius,
                    CssProperty.BorderBottomRightRadius, CssProperty.BorderBottomLeftRadius };
                var names = new[] { "border-top-left-radius", "border-top-right-radius",
                    "border-bottom-right-radius", "border-bottom-left-radius" };
                for (var i = 0; i < corners.Length; i++)
                    Emit(targets[i], corners[i], node.Span.Start, names[i]);
                continue;
            }
            if (name is "border-top-left-radius" or "border-top-right-radius"
                or "border-bottom-right-radius" or "border-bottom-left-radius")
            {
                if (terms.Count != 1)
                    throw new FactsPdfException("FPDF1202",
                        "A corner-radius longhand requires exactly one physical length.", node.Span.Start);
                var source = valueTokens[0].Token;
                var radius = terms[0];
                if (source.Kind == CssSyntaxTokenKind.Number && CssBoxValues.IsExactlyUnitlessZero(radius))
                    radius = "0";
                if (!Wide(radius)) _ = CssPaintValues.BorderRadius(radius, source.Span.Start);
                Emit(property, radius, source.Span.Start);
                continue;
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
                        && CssBoxValues.IsExactlyUnitlessZero(parts[i]))
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
                        ValidateComponent("margin-top", sides[side], offset);
                    else if (sides[side] != "auto")
                        _ = CssBoxValues.Length(sides[side], offset, allowRem: true, allowEm: true);
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

            // Logical spacing is intentionally limited to horizontal LTR
            // documents in normal flow. Expand into physical longhands here
            // so the *same* per-side cascade, box geometry, page fragmentation
            // and explicit failure behavior are used by the renderer.
            if (name is "margin-inline" or "margin-block" or "padding-inline" or "padding-block")
            {
                if (terms.Count > 2)
                    throw new FactsPdfException("FPDF1202",
                        "Logical spacing shorthand takes one or two values.", node.Span.Start);
                string[] longhands = name switch
                {
                    "margin-inline" => ["margin-left", "margin-right"],
                    "margin-block" => ["margin-top", "margin-bottom"],
                    "padding-inline" => ["padding-left", "padding-right"],
                    _ => ["padding-top", "padding-bottom"]
                };
                var parts = terms.ToArray();
                for (var i = 0; i < parts.Length; i++)
                {
                    // Like the existing physical margin shorthand, retain
                    // the source offset but normalize exactly zero-valued
                    // unitless Number tokens before checking the physical side.
                    if (valueTokens[i].Token.Kind == CssSyntaxTokenKind.Number
                        && CssBoxValues.IsExactlyUnitlessZero(parts[i]))
                        parts[i] = "0";
                }
                var first = parts[0];
                var second = parts.Length == 1 ? first : parts[1];
                var firstOffset = valueTokens[0].Token.Span.Start;
                var secondOffset = valueTokens[terms.Count - 1].Token.Span.Start;
                if (terms.Count == 1 && Wide(first))
                {
                    Emit(Property(longhands[0], firstOffset), first, firstOffset, longhands[0]);
                    Emit(Property(longhands[1], firstOffset), first, firstOffset, longhands[1]);
                    continue;
                }
                if (Wide(first) || Wide(second))
                    throw new FactsPdfException("FPDF1202",
                        "A CSS-wide logical spacing keyword must be the only value.",
                        Wide(first) ? firstOffset : secondOffset);
                ValidateComponent(longhands[0], first, firstOffset);
                ValidateComponent(longhands[1], second, secondOffset);
                Emit(Property(longhands[0], firstOffset), first, firstOffset, longhands[0]);
                Emit(Property(longhands[1], secondOffset), second, secondOffset, longhands[1]);
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
                        else if (word is "solid" or "none" or "dashed" or "dotted")
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
            // Bounded text-decoration shorthand: exactly one line kind and
            // optional opaque colour and solid/dashed/dotted stroke. Lower all
            // three longhands through the normal CSS winner/cascade pipeline,
            // so a later !important longhand can override one component and a
            // new shorthand resets omitted components rather than ignoring them.
            if (name == "text-decoration")
            {
                if (terms.Count > 3)
                    throw new FactsPdfException("FPDF1202",
                        "Text decoration accepts one line kind, a stroke and an opaque colour.", node.Span.Start);
                if (terms.Count == 1 && Wide(terms[0]))
                {
                    var wideOffset = valueTokens[0].Token.Span.Start;
                    Emit(CssProperty.TextDecoration, terms[0], wideOffset, "text-decoration-line");
                    Emit(CssProperty.TextDecorationStyle, terms[0], wideOffset, "text-decoration-style");
                    Emit(CssProperty.TextDecorationColor, terms[0], wideOffset, "text-decoration-color");
                    continue;
                }
                string? line = null;
                var stroke = "solid";
                var ink = "currentcolor";
                var strokeSeen = false;
                var inkSeen = false;
                var lineOffset = node.Span.Start;
                var strokeOffset = node.Span.Start;
                var inkOffset = node.Span.Start;
                for (var i = 0; i < terms.Count; i++)
                {
                    var word = terms[i];
                    var at = valueTokens[i].Token.Span.Start;
                    if (Wide(word))
                        throw new FactsPdfException("FPDF1202",
                            "CSS-wide decoration values must stand alone.", at);
                    if (word is "none" or "underline" or "line-through" or "overline")
                    {
                        if (line is not null)
                            throw new FactsPdfException("FPDF1202",
                                "Only one text decoration line is supported.", at);
                        line = word; lineOffset = at;
                    }
                    else if (word is "solid" or "dashed" or "dotted")
                    {
                        if (strokeSeen)
                            throw new FactsPdfException("FPDF1202", "Duplicate decoration stroke.", at);
                        stroke = word; strokeSeen = true; strokeOffset = at;
                    }
                    else
                    {
                        if (inkSeen)
                            throw new FactsPdfException("FPDF1202", "Duplicate decoration colour.", at);
                        if (word != "currentcolor") _ = InlineCss.Color(word, at);
                        ink = word; inkSeen = true; inkOffset = at;
                    }
                }
                if (line is null || line == "none" && terms.Count > 1)
                    throw new FactsPdfException("FPDF1202",
                        "Text decoration shorthand requires one visible line or standalone none.", node.Span.Start);
                Emit(CssProperty.TextDecoration, line, lineOffset, "text-decoration-line");
                Emit(CssProperty.TextDecorationStyle, stroke, strokeOffset, "text-decoration-style");
                Emit(CssProperty.TextDecorationColor, ink, inkOffset, "text-decoration-color");
                continue;
            }
            // Restricted list-style shorthand: exactly one supported marker
            // type or one CSS-wide keyword. Position/images are not implemented,
            // so accepting or dropping any other term would falsely render it.
            // Lower to the existing list-style-type cascade and PDF marker path.
            if (name == "list-style")
            {
                if (terms.Count != 1 || valueTokens.Count() != 1
                    || valueTokens[0].Token.Kind != CssSyntaxTokenKind.Ident)
                    throw new FactsPdfException("FPDF1202",
                        "list-style accepts exactly one supported marker type.", node.Span.Start);
                var kind = terms[0];
                var kindOffset = valueTokens[0].Token.Span.Start;
                if (!Wide(kind)) _ = ListMarkerTypes.Parse(kind, kindOffset);
                Emit(CssProperty.ListStyleType, kind, kindOffset);
                continue;
            }
            if (terms.Count != 1)
                throw new FactsPdfException("FPDF1202", "Expected one supported CSS value.", node.Span.Start);
            var token = terms[0];
            var tokenOffset = valueTokens[0].Token.Span.Start;
            if (legacyPageBreak && token == "always") token = "page";
            if (property == CssProperty.Display)
            {
                // The report renderer supports suppression, not CSS display
                // formatting contexts. CSS-wide initial/unset compute to
                // inline and are deliberately rejected rather than faked.
                if (valueTokens[0].Token.Kind != CssSyntaxTokenKind.Ident ||
                    token is not ("none" or "block"))
                    throw new FactsPdfException("FPDF1202",
                        "Report display supports only block and none.", tokenOffset);
                Emit(property, token, tokenOffset);
                continue;
            }
            if (property == CssProperty.FontWeight)
            {
                var syntax = valueTokens[0].Token;
                if (syntax.Kind == CssSyntaxTokenKind.Number
                    && TryNumericFontWeight(syntax.Value, out var weight))
                    token = weight.ToString("0", System.Globalization.CultureInfo.InvariantCulture);
                else if (syntax.Kind != CssSyntaxTokenKind.Ident || !(Wide(token) || token is "normal" or "bold"))
                    throw new FactsPdfException("FPDF1202",
                        "font-weight requires normal/bold or numeric hundreds from 100 through 900.", tokenOffset);
            }
            if (property == CssProperty.FontStyle && (valueTokens[0].Token.Kind != CssSyntaxTokenKind.Ident
                || !(Wide(token) || token is "normal" or "italic")))
                throw new FactsPdfException("FPDF1202", "font-style requires normal or italic.", tokenOffset);
            if (property == CssProperty.ListStyleType && valueTokens[0].Token.Kind != CssSyntaxTokenKind.Ident)
                throw new FactsPdfException("FPDF1202", "list-style-type requires a supported identifier.", tokenOffset);
            if (!Wide(token)) ValidateComponent(name, token, tokenOffset);
            Emit(property, token, tokenOffset);
        }
        return output.ToArray();
    }

    // A supported weight is exactly one nonzero decimal digit times 10^2.
    // Inspect every mantissa digit: floating-point parsing can round an
    // unsupported fraction (or a value above 900) into a supported weight.
    private static bool TryNumericFontWeight(string value, out int weight)
    {
        weight = 0;
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
        var significantIndex = -1;
        foreach (var digit in number)
        {
            if (digit == '.') continue;
            if (digit != '0')
            {
                if (digit is < '1' or > '9' || significantIndex >= 0) return false;
                significantIndex = digitIndex;
                weight = (digit - '0') * 100;
            }
            digitIndex++;
        }
        return significantIndex >= 0 && (long)exponent + integerDigits - significantIndex - 1 == 2;
    }

    private static bool Wide(string text) => text is "inherit" or "initial" or "unset";

    private static CssProperty Property(string name, int offset) => name switch
    {
        "font-size" => CssProperty.FontSize, "color" => CssProperty.Color,
        "font-weight" => CssProperty.FontWeight, "font-style" => CssProperty.FontStyle,
        "line-height" => CssProperty.LineHeight, "text-align" => CssProperty.TextAlign,
        "vertical-align" => CssProperty.VerticalAlign,
        "white-space" => CssProperty.WhiteSpace, "overflow-wrap" => CssProperty.OverflowWrap, "text-indent" => CssProperty.TextIndent,
        "letter-spacing" => CssProperty.LetterSpacing, "word-spacing" => CssProperty.WordSpacing,
        "text-transform" => CssProperty.TextTransform,
        "display" => CssProperty.Display,
        "list-style-type" or "list-style" => CssProperty.ListStyleType,
        "text-decoration" or "text-decoration-line" => CssProperty.TextDecoration,
        "text-decoration-color" => CssProperty.TextDecorationColor,
        "text-decoration-style" => CssProperty.TextDecorationStyle,
        "border-radius" => CssProperty.BorderRadius,
        "border-top-left-radius" => CssProperty.BorderTopLeftRadius,
        "border-top-right-radius" => CssProperty.BorderTopRightRadius,
        "border-bottom-right-radius" => CssProperty.BorderBottomRightRadius,
        "border-bottom-left-radius" => CssProperty.BorderBottomLeftRadius,
        "margin" or "margin-top" or "margin-block" or "margin-block-start" => CssProperty.MarginTop,
        "margin-bottom" or "margin-block-end" => CssProperty.MarginBottom,
        "margin-left" or "margin-inline" or "margin-inline-start" => CssProperty.MarginLeft,
        "margin-right" or "margin-inline-end" => CssProperty.MarginRight,
        "break-before" => CssProperty.BreakBefore, "break-after" => CssProperty.BreakAfter,
        "width" => CssProperty.Width, "min-width" => CssProperty.MinWidth, "max-width" => CssProperty.MaxWidth,
        "min-height" => CssProperty.MinHeight,
        "box-sizing" => CssProperty.BoxSizing,
        "padding" or "padding-top" or "padding-block" or "padding-block-start" => CssProperty.PaddingTop,
        "padding-right" or "padding-inline-end" => CssProperty.PaddingRight,
        "padding-bottom" or "padding-block-end" => CssProperty.PaddingBottom,
        "padding-left" or "padding-inline" or "padding-inline-start" => CssProperty.PaddingLeft,
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
            if (!InlineCss.RootRelativeScale(token, offset, 144).HasValue
                && !InlineCss.RelativeFontSizeScale(token, offset).HasValue)
                _ = InlineCss.Apply(ValidationStyle, name + ":" + token,
                    paragraph: true, inline: false, offset);
        }
        else if (name == "line-height")
        {
            // A valid relative value may fit a computed 30pt element but not
            // the static 12pt validation style. Defer computed-height bounds
            // to the winning declaration, as for relative font-size.
            if (!InlineCss.RootRelativeScale(token, offset, 1440).HasValue
                && !InlineCss.RelativeLineHeightScale(token, offset).HasValue)
                _ = InlineCss.Apply(ValidationStyle, name + ":" + token,
                    paragraph: true, inline: false, offset);
        }
        else if (name is ("text-indent" or "letter-spacing" or "word-spacing")
            && token.EndsWith("em", StringComparison.Ordinal))
        {
            // Check syntax even for an unmatched selector, but resolve the
            // final font and bounded glyph advance only for the CSS winner.
            _ = CssBoxValues.Length(token, offset, allowRem: true, allowEm: true);
        }
        else if (name == "list-style-type")
            _ = ListMarkerTypes.Parse(token, offset);
        else if (name == "vertical-align")
        {
            if (token is not ("top" or "middle" or "bottom"))
                throw new FactsPdfException("FPDF1202",
                    "Table-cell vertical-align supports only top, middle and bottom.", offset);
        }
        else if (name == "border-radius")
            _ = CssPaintValues.BorderRadius(token, offset);
        else if (name == "box-sizing")
        {
            if (token is not ("content-box" or "border-box"))
                throw new FactsPdfException("FPDF1202", "box-sizing requires content-box or border-box.", offset);
        }
        else if (name == "width") { _ = CssBoxValues.Width(token, offset); }
        else if (name == "min-width")
        {
            if (token != "auto") _ = CssBoxValues.Length(token, offset, allowRem: true, allowEm: true);
        }
        else if (name == "max-width")
        {
            if (token != "none") _ = CssBoxValues.Length(token, offset, allowRem: true, allowEm: true);
        }
        else if (name == "min-height")
        {
            var length = CssBoxValues.Length(token, offset, allowRem: true, allowEm: true);
            if (length.IsPercent)
                throw new FactsPdfException("FPDF1202",
                    "Report panel min-height requires a nonnegative physical, em or rem length.", offset);
        }
        else if (name is "margin-left" or "margin-right")
        {
            if (token != "auto") _ = CssBoxValues.Length(token, offset, allowRem: true, allowEm: true);
        }
        else if ((name is "margin-top" or "margin-bottom")
            && token.EndsWith("em", StringComparison.Ordinal))
            _ = CssBoxValues.Length(token, offset, allowRem: true, allowEm: true);
        else if (name is "padding" or "padding-top" or "padding-right" or "padding-bottom" or "padding-left")
            _ = CssBoxValues.Length(token, offset, allowRem: true, allowEm: true);
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
