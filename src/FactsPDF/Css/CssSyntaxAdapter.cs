using FactsPDF.CssSyntax;

namespace FactsPDF;

/// <summary>Strict semantic bridge: a syntactically valid CSS construct need not be renderable.</summary>
internal static class CssSyntaxAdapter
{
    private static readonly string[] DecorationLineOrder = ["underline", "line-through", "overline"];

    // Shared, never written: the box and border shorthands index these instead of building a
    // side or edge array per declaration.
    private static readonly CssProperty[] PaddingSides =
        [CssProperty.PaddingTop, CssProperty.PaddingRight, CssProperty.PaddingBottom, CssProperty.PaddingLeft];
    private static readonly CssProperty[] BorderWidthSides =
        [CssProperty.BorderTopWidth, CssProperty.BorderRightWidth, CssProperty.BorderBottomWidth, CssProperty.BorderLeftWidth];
    private static readonly CssProperty[] BorderStyleSides =
        [CssProperty.BorderTopStyle, CssProperty.BorderRightStyle, CssProperty.BorderBottomStyle, CssProperty.BorderLeftStyle];
    private static readonly CssProperty[] BorderColorSides =
        [CssProperty.BorderTopColor, CssProperty.BorderRightColor, CssProperty.BorderBottomColor, CssProperty.BorderLeftColor];
    private static readonly int[] TopEdge = [0], RightEdge = [1], BottomEdge = [2], LeftEdge = [3];
    private static readonly int[] InlineEdges = [3, 1], BlockEdges = [0, 2], AllEdges = [0, 1, 2, 3];

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

    // hostSelectors are the selectors of the rule that holds the declarations, hostElement the
    // element whose style attribute holds them; a table-part display value is checked against them.
    // In non-strict mode (budget.Warnings set, recover true) a declaration that is invalid per the
    // CSS specifications is dropped as a browser drops it, and the error strict mode throws becomes a
    // warning; a value holding a bad string, bad URL or unmatched closer is dropped silently, since
    // its syntax error was already reported. Values after var() substitution pass recover false:
    // the cascade decides what an invalid substitution means.
    internal static CssDeclaration[] CompileKnownDeclarations(
        IReadOnlyList<CssDeclarationNode> nodes, CssBudget budget,
        CssSelector[]? hostSelectors = null, string? hostElement = null, int fixedOrder = 0, bool recover = true)
    {
        // One pooled scratch list per thread: shorthands expand into several declarations
        // (`border` into twelve), so a list sized to the nodes grew by doubling per declaration
        // block; the result is still one exact-size array. Taken out of its slot while in use.
        var output = pooledDeclarations ?? new List<CssDeclaration>(16);
        pooledDeclarations = null;
        var warnings = recover ? budget.Warnings : null;
        // An index loop: foreach over the IReadOnlyList would box an array enumerator per call.
        for (var n = 0; n < nodes.Count; n++)
        {
            var node = nodes[n];
            budget.Token.ThrowIfCancellationRequested();
            if (warnings is not null && CssSpecValidity.HasInvalidTokens(node.Values)) continue;
            var mark = output.Count;
            try
            {
                // A custom property (--name, case-sensitive) holds any value; only its var() syntax and
                // CSS-wide keyword are checked here. The cascade resolves it for each element.
                if (CssVariables.IsCustomPropertyName(node.Name))
                {
                    CssVariables.ValidateReferences(node.Values);
                    var keyword = CssVariables.Keyword(node.Values);
                    var values = node.Values as CssSyntaxComponent[] ?? [.. node.Values];
                    budget.UsesVariables = true;
                    output.Add(CssDeclaration.Create(CssProperty.Custom, node.Name, CssVariables.Text(values), node.Important,
                        budget.Declaration(node.Span.Start), node.Span.Start, new CssCustomValue(values, keyword)));
                    budget.CustomProperty(node.Name, node.Span.Start);
                    continue;
                }
                var originalName = node.Name.ToLowerInvariant();
                // Legacy print stylesheets use page-break-before/after:always.
                // Normalize only these known aliases into the existing cascade
                // slots; page/auto retain the same actual layout semantics.
                var legacyPageBreak = originalName is "page-break-before" or "page-break-after";
                var name = CanonicalName(originalName);
                // Fail unsupported properties first even when the value contains
                // functions or multiple tokens.
                var initialOnly = InitialOnlyValue(name);
                var property = initialOnly is null && !IsInteractionOnly(name) ? Property(name, node.Span.Start) : default;
                // A supported property whose value holds var() can be checked only after substitution,
                // for each element it applies to (CSS Custom Properties: valid at parse time when the
                // var() syntax is). It keeps one cascade order for every declaration it expands into.
                if (CssVariables.ContainsVar(node.Values))
                {
                    CssVariables.ValidateReferences(node.Values);
                    budget.UsesVariables = true;
                    output.Add(CssDeclaration.Create(CssProperty.Pending, name, CssVariables.Text(node.Values), node.Important,
                        budget.Declaration(node.Span.Start), node.Span.Start, new CssPendingValue(node, hostSelectors, hostElement is not null)));
                    continue;
                }
                // Properties the report renderer does not implement, declared at the one value it
                // already lays out (position: static, float: none, opacity: 1, ...): the declaration
                // changes nothing, so it is validated and dropped. Every other value, including any
                // function such as rotate() or url(), fails with a message naming the one value.
                if (initialOnly is not null)
                {
                    ValidateInitialOnly(name, initialOnly, node);
                    continue;
                }
                // Interaction-only properties (cursor, pointer-events, user-select, resize) change
                // how a screen reacts to a pointer, never what is painted, and print-color-adjust
                // only lets a printer drop colours, which this renderer never does; text-rendering and
                // the font-smoothing switches only steer a screen rasteriser; so every keyword
                // value is validated and dropped and any other value fails with FPDF1202.
                if (IsInteractionOnly(name))
                {
                    ValidateInteractionOnly(name, node);
                    continue;
                }
                // RGB and HSL functions are lowered to their equivalent hex colour
                // (#rrggbb, or #rrggbbaa when translucent), so the cascade, box paint
                // and PDF serializers share one colour path.
                var canonical = CanonicalValues(node);
                var terms = Terms(canonical);
                var valueTokens = NonWhitespaceTokens(canonical.Values);
                if (terms.Count == 0)
                    throw new FactsPdfException("FPDF1202", "Empty CSS declaration.", node.Span.Start);

                void Emit(CssProperty target, string value, int offset = -1, string? targetName = null)
                {
                    var sourceOffset = offset < 0 ? node.Span.Start : offset;
                    var order = fixedOrder != 0 ? fixedOrder : budget.Declaration(sourceOffset);
                    output.Add(CssDeclaration.Create(target, targetName ?? name, value, node.Important, order, sourceOffset));
                }
                // Logical border component declarations use the existing *physical*
                // per-side width/style/colour cascade slots. The pair forms accept
                // one or two values (start/end); individual sides exactly one.
                // Only horizontal LTR normal-flow print layouts are supported.
                if (name is "border-inline-width" or "border-inline-style" or "border-inline-color"
                    or "border-block-width" or "border-block-style" or "border-block-color"
                    or "border-inline-start-width" or "border-inline-start-style" or "border-inline-start-color"
                    or "border-inline-end-width" or "border-inline-end-style" or "border-inline-end-color"
                    or "border-block-start-width" or "border-block-start-style" or "border-block-start-color"
                    or "border-block-end-width" or "border-block-end-style" or "border-block-end-color")
                {
                    var component = name[(name.LastIndexOf('-') + 1)..];
                    var logicalAxis = name[..^(component.Length + 1)];
                    string[] physicalSides = logicalAxis switch
                    {
                        "border-inline" => ["border-left", "border-right"],
                        "border-block" => ["border-top", "border-bottom"],
                        "border-inline-start" => ["border-left"],
                        "border-inline-end" => ["border-right"],
                        "border-block-start" => ["border-top"],
                        "border-block-end" => ["border-bottom"],
                        _ => throw new InvalidOperationException("Unrecognized logical border component.")
                    };
                    if (terms.Count > physicalSides.Length)
                        throw new FactsPdfException("FPDF1202",
                            "Logical border component expects one or two values for a pair, one for a side.",
                            node.Span.Start);
                    var parts = terms.ToArray();
                    for (var i = 0; i < parts.Length; i++)
                    {
                        var source = valueTokens[i].Token;
                        if (source.Kind == CssSyntaxTokenKind.Number &&
                            CssBoxValues.IsExactlyUnitlessZero(parts[i]))
                            parts[i] = "0";
                        if (Wide(parts[i]) && parts.Length != 1)
                            throw new FactsPdfException("FPDF1202",
                                "A CSS-wide logical border component must be the only value.", source.Span.Start);
                        if (!Wide(parts[i]))
                            ValidateComponent(physicalSides[i] + "-" + component, parts[i], source.Span.Start);
                    }
                    for (var i = 0; i < physicalSides.Length; i++)
                    {
                        var sourceIndex = Math.Min(i, parts.Length - 1);
                        var sourceOffset = valueTokens[sourceIndex].Token.Span.Start;
                        var physical = physicalSides[i] + "-" + component;
                        Emit(Property(physical, sourceOffset), parts[sourceIndex], sourceOffset, physical);
                    }
                    continue;
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
                    if (!(terms.Count == 1 && Wide(terms[0])))
                        for (var i = 0; i < terms.Count; i++)
                            ValidateComponent(name, terms[i], valueTokens[i].Token.Span.Start);
                    var properties = name switch
                    {
                        "padding" => PaddingSides,
                        "border-width" => BorderWidthSides,
                        "border-style" => BorderStyleSides,
                        _ => BorderColorSides
                    };
                    for (var i = 0; i < 4; i++)
                    {
                        // Expand in TRBL order but charge a generated longhand
                        // against the CSS token from which that side originated.
                        var sourceIndex = SideSource(terms.Count, i);
                        Emit(properties[i], terms[sourceIndex], valueTokens[sourceIndex].Token.Span.Start);
                    }
                    continue;
                }
                // Expand the outline shorthand into three separately cascading
                // components. Omitted terms reset to initial values, not the
                // previously winning outline longhand.
                if (name == "outline")
                {
                    if (terms.Count > 3)
                        throw new FactsPdfException("FPDF1202",
                            "Outline accepts up to three width/style/colour terms.", node.Span.Start);
                    var at = valueTokens[0].Token.Span.Start;
                    if (terms.Count == 1 && Wide(terms[0]))
                    {
                        Emit(CssProperty.OutlineWidth, terms[0], at, "outline-width");
                        Emit(CssProperty.OutlineStyle, terms[0], at, "outline-style");
                        Emit(CssProperty.OutlineColor, terms[0], at, "outline-color");
                        continue;
                    }
                    var width = "medium"; var style = "none"; var color = "currentcolor";
                    var widthAt = at; var styleAt = at; var colorAt = at;
                    var seenWidth = false; var seenStyle = false; var seenColor = false;
                    for (var i = 0; i < terms.Count; i++)
                    {
                        var word = terms[i];
                        var tokenAt = valueTokens[i].Token.Span.Start;
                        if (Wide(word))
                            throw new FactsPdfException("FPDF1202",
                                "CSS-wide outline value must stand alone.", tokenAt);
                        if (CssPaintValues.TryBorderWidth(word, out _))
                        {
                            if (seenWidth) throw new FactsPdfException("FPDF1202", "Duplicate outline width.", tokenAt);
                            _ = CssPaintValues.OutlineWidth(word, tokenAt);
                            width = word; widthAt = tokenAt; seenWidth = true;
                        }
                        else if (word is "solid" or "none" or "dashed" or "dotted")
                        {
                            if (seenStyle) throw new FactsPdfException("FPDF1202", "Duplicate outline style.", tokenAt);
                            style = word; styleAt = tokenAt; seenStyle = true;
                        }
                        else
                        {
                            if (seenColor) throw new FactsPdfException("FPDF1202", "Duplicate outline colour.", tokenAt);
                            _ = CssPaintValues.BorderColor(word, tokenAt);
                            color = word; colorAt = tokenAt; seenColor = true;
                        }
                    }
                    Emit(CssProperty.OutlineWidth, width, widthAt, "outline-width");
                    Emit(CssProperty.OutlineStyle, style, styleAt, "outline-style");
                    Emit(CssProperty.OutlineColor, color, colorAt, "outline-color");
                    continue;
                }
                // Only horizontal LTR logical borders are mapped to physical PDF edges.
                // This is a shorthand family, not directional writing-mode support.
                // Lower each affected edge to the three existing independent
                // width/style/colour cascade slots before PDF layout and paint.
                if (name is "border" or "border-top" or "border-right" or "border-bottom" or "border-left"
                    or "border-inline" or "border-block" or "border-inline-start" or "border-inline-end"
                    or "border-block-start" or "border-block-end")
                {
                    if (terms.Count > 3) throw new FactsPdfException("FPDF1202", "Border shorthand takes up to three terms.", node.Span.Start);
                    var words = terms;
                    string width = "medium", style = "none", color = "currentcolor";
                    var initialOffset = valueTokens[0].Token.Span.Start;
                    var widthOffset = initialOffset;
                    var styleOffset = initialOffset;
                    var colorOffset = initialOffset;
                    if (words.Count == 1 && Wide(words[0]))
                        width = style = color = words[0];
                    else
                    {
                        bool hasWidth = false, hasStyle = false, hasColor = false;
                        for (var i = 0; i < words.Count; i++)
                        {
                            var word = words[i];
                            var offset = valueTokens[i].Token.Span.Start;
                            if (Wide(word)) throw new FactsPdfException("FPDF1202", "CSS-wide keyword must be the only shorthand value.", offset);
                            if (CssPaintValues.TryBorderWidth(word, out _, allowRelative: true))
                            {
                                if (hasWidth) throw new FactsPdfException("FPDF1202", "Duplicate border width.", offset);
                                width = word; hasWidth = true; widthOffset = offset;
                            }
                            else if (word is "solid" or "none" or "hidden" or "dashed" or "dotted")
                            {
                                if (hasStyle) throw new FactsPdfException("FPDF1202", "Duplicate border style.", offset);
                                style = word; hasStyle = true; styleOffset = offset;
                            }
                            else
                            {
                                _ = CssPaintValues.EdgeColor(word, offset);
                                if (hasColor) throw new FactsPdfException("FPDF1202", "Duplicate border color.", offset);
                                color = word; hasColor = true; colorOffset = offset;
                            }
                        }
                    }
                    var edges = name switch
                    {
                        "border-left" or "border-inline-start" => LeftEdge,
                        "border-right" or "border-inline-end" => RightEdge,
                        "border-top" or "border-block-start" => TopEdge,
                        "border-bottom" or "border-block-end" => BottomEdge,
                        "border-inline" => InlineEdges,
                        "border-block" => BlockEdges,
                        _ => AllEdges
                    };
                    foreach (var edge in edges)
                    {
                        Emit((CssProperty)((int)CssProperty.BorderTopWidth + edge), width, widthOffset);
                        Emit((CssProperty)((int)CssProperty.BorderTopStyle + edge), style, styleOffset);
                        Emit((CssProperty)((int)CssProperty.BorderTopColor + edge), color, colorOffset);
                    }
                    continue;
                }
                // A bounded text-decoration-line set can paint any nonempty subset
                // of underline/overline/line-through, or standalone none. Parse and
                // validate even unmatched selectors before entering the cascade.
                if (name == "text-decoration-line")
                {
                    if (terms.Count > 3 || valueTokens.Length != terms.Count ||
                        valueTokens.Any(t => t.Token.Kind != CssSyntaxTokenKind.Ident))
                        throw new FactsPdfException("FPDF1202",
                            "text-decoration-line accepts up to three distinct line keywords.", node.Span.Start);
                    var at = valueTokens[0].Token.Span.Start;
                    var lines = string.Join(" ", terms);
                    if (!(terms.Count == 1 && Wide(terms[0])))
                        _ = InlineCss.ParseDecorationLines(lines, at);
                    Emit(CssProperty.TextDecoration, lines, at, "text-decoration-line");
                    continue;
                }
                // Bounded text-decoration shorthand: a nonempty line set and
                // optional colour, bounded thickness and solid/dashed/dotted stroke. Lower all
                // four longhands through the normal CSS winner/cascade pipeline,
                // so a later !important longhand can override one component and a
                // new shorthand resets omitted components rather than ignoring them.
                if (name == "text-decoration")
                {
                    if (terms.Count > 6)
                        throw new FactsPdfException("FPDF1202",
                            "Text decoration accepts up to three lines, one stroke, thickness and colour.", node.Span.Start);
                    if (terms.Count == 1 && Wide(terms[0]))
                    {
                        var wideOffset = valueTokens[0].Token.Span.Start;
                        Emit(CssProperty.TextDecoration, terms[0], wideOffset, "text-decoration-line");
                        Emit(CssProperty.TextDecorationStyle, terms[0], wideOffset, "text-decoration-style");
                        Emit(CssProperty.TextDecorationColor, terms[0], wideOffset, "text-decoration-color");
                        Emit(CssProperty.TextDecorationThickness, terms[0], wideOffset, "text-decoration-thickness");
                        continue;
                    }
                    var lines = new List<string>(3);
                    var stroke = "solid";
                    var ink = "currentcolor";
                    var thickness = "auto";
                    var strokeSeen = false;
                    var inkSeen = false;
                    var thicknessSeen = false;
                    var lineOffset = node.Span.Start;
                    var strokeOffset = node.Span.Start;
                    var inkOffset = node.Span.Start;
                    var thicknessOffset = node.Span.Start;
                    for (var i = 0; i < terms.Count; i++)
                    {
                        var word = terms[i];
                        var at = valueTokens[i].Token.Span.Start;
                        if (Wide(word))
                            throw new FactsPdfException("FPDF1202",
                                "CSS-wide decoration values must stand alone.", at);
                        if (word is "none" or "underline" or "line-through" or "overline")
                        {
                            if (lines.Contains(word))
                                throw new FactsPdfException("FPDF1202",
                                    "Duplicate text decoration line.", at);
                            if (lines.Count == 0) lineOffset = at;
                            lines.Add(word);
                        }
                        else if (word is "solid" or "dashed" or "dotted")
                        {
                            if (strokeSeen)
                                throw new FactsPdfException("FPDF1202", "Duplicate decoration stroke.", at);
                            stroke = word; strokeSeen = true; strokeOffset = at;
                        }
                        else if (word == "auto" || valueTokens[i].Token.Kind is CssSyntaxTokenKind.Dimension
                            or CssSyntaxTokenKind.Number or CssSyntaxTokenKind.Percentage)
                        {
                            if (thicknessSeen)
                                throw new FactsPdfException("FPDF1202", "Duplicate decoration thickness.", at);
                            ValidateComponent("text-decoration-thickness", word, at);
                            thickness = word; thicknessSeen = true; thicknessOffset = at;
                        }
                        else
                        {
                            if (inkSeen)
                                throw new FactsPdfException("FPDF1202", "Duplicate decoration colour.", at);
                            if (word != "currentcolor") _ = InlineCss.Color(word, at);
                            ink = word; inkSeen = true; inkOffset = at;
                        }
                    }
                    if (lines.Count == 0 || lines.Contains("none") && terms.Count > 1)
                        throw new FactsPdfException("FPDF1202",
                            "Text decoration shorthand requires visible lines or standalone none.", node.Span.Start);
                    var orderedLines = lines.Contains("none") ? "none" : string.Join(" ",
                        DecorationLineOrder.Where(lines.Contains));
                    _ = InlineCss.ParseDecorationLines(orderedLines, lineOffset);
                    Emit(CssProperty.TextDecoration, orderedLines, lineOffset, "text-decoration-line");
                    Emit(CssProperty.TextDecorationStyle, stroke, strokeOffset, "text-decoration-style");
                    Emit(CssProperty.TextDecorationColor, ink, inkOffset, "text-decoration-color");
                    Emit(CssProperty.TextDecorationThickness, thickness, thicknessOffset, "text-decoration-thickness");
                    continue;
                }
                // List shorthands reset BOTH inherited marker type and position.
                // Support only marker type plus optional inside/outside; never
                // silently accept a URL, counter or arbitrary list-style image.
                if (name == "list-style")
                {
                    if (terms.Count is < 1 or > 2 || valueTokens.Length != terms.Count ||
                        valueTokens.Any(t => t.Token.Kind != CssSyntaxTokenKind.Ident))
                        throw new FactsPdfException("FPDF1202",
                            "list-style accepts one marker type and optional inside/outside.", node.Span.Start);
                    if (terms.Count == 1 && Wide(terms[0]))
                    {
                        Emit(CssProperty.ListStyleType, terms[0], valueTokens[0].Token.Span.Start, "list-style-type");
                        Emit(CssProperty.ListStylePosition, terms[0], valueTokens[0].Token.Span.Start, "list-style-position");
                        continue;
                    }
                    var kind = "disc";
                    var position = "outside";
                    var kindSeen = false;
                    var positionSeen = false;
                    for (var i = 0; i < terms.Count; i++)
                    {
                        var word = terms[i];
                        var at = valueTokens[i].Token.Span.Start;
                        if (Wide(word))
                            throw new FactsPdfException("FPDF1202",
                                "CSS-wide list-style keywords must stand alone.", at);
                        if (word is "inside" or "outside")
                        {
                            if (positionSeen) throw new FactsPdfException("FPDF1202", "Duplicate list marker position.", at);
                            position = word; positionSeen = true;
                        }
                        else
                        {
                            if (kindSeen) throw new FactsPdfException("FPDF1202", "Duplicate list marker type.", at);
                            _ = ListMarkerTypes.Parse(word, at);
                            kind = word; kindSeen = true;
                        }
                    }
                    Emit(CssProperty.ListStyleType, kind, node.Span.Start, "list-style-type");
                    Emit(CssProperty.ListStylePosition, position, node.Span.Start, "list-style-position");
                    continue;
                }
                if (terms.Count != 1)
                    throw new FactsPdfException("FPDF1202", "Expected one supported CSS value.", node.Span.Start);
                var token = terms[0];
                var tokenOffset = valueTokens[0].Token.Span.Start;
                if (legacyPageBreak && token == "always") token = "page";
                // The background shorthand's "none" resets the image layer and leaves
                // the initial colour, transparent: exactly the existing no-fill paint.
                // background-color has no "none" value and still rejects it.
                if (name == "background" && token == "none" &&
                    valueTokens[0].Token.Kind == CssSyntaxTokenKind.Ident) token = "transparent";
                if (property == CssProperty.Display)
                {
                    // The report renderer supports suppression, not CSS display
                    // formatting contexts. CSS-wide initial/unset compute to
                    // inline and are deliberately rejected rather than faked.
                    // A table-part keyword passes only where every host the declarations
                    // can reach is the table part whose rendering it names; the cascade
                    // then drops it (CssStylesheets.TablePartDisplayError).
                    var displayError = valueTokens[0].Token.Kind != CssSyntaxTokenKind.Ident
                        ? "Report display supports only block and none."
                        : token is "none" or "block" or "revert" or "revert-layer" ? null
                        : CssStylesheets.TablePartDisplayError(token, hostSelectors, hostElement);
                    if (displayError is not null)
                        throw new FactsPdfException("FPDF1202", displayError, tokenOffset);
                    Emit(property, token, tokenOffset);
                    continue;
                }
                if (property == CssProperty.FontWeight)
                {
                    var syntax = valueTokens[0].Token;
                    if (syntax.Kind == CssSyntaxTokenKind.Number
                        && TryNumericFontWeight(syntax.Value, out var weight))
                        token = weight.ToString("0", System.Globalization.CultureInfo.InvariantCulture);
                    else if (syntax.Kind != CssSyntaxTokenKind.Ident || !(Wide(token) || token is "normal" or "bold" or "bolder" or "lighter"))
                        throw new FactsPdfException("FPDF1202",
                            "font-weight requires normal/bold/bolder/lighter or numeric hundreds from 100 through 900.", tokenOffset);
                }
                if (property == CssProperty.FontStyle && (valueTokens[0].Token.Kind != CssSyntaxTokenKind.Ident
                    || !(Wide(token) || token is "normal" or "italic")))
                    throw new FactsPdfException("FPDF1202", "font-style requires normal or italic.", tokenOffset);
                if ((property is CssProperty.ListStyleType or CssProperty.ListStylePosition) && valueTokens[0].Token.Kind != CssSyntaxTokenKind.Ident)
                    throw new FactsPdfException("FPDF1202", "list-style-type requires a supported identifier.", tokenOffset);
                if (!Wide(token)) ValidateComponent(name, token, tokenOffset);
                Emit(property, token, tokenOffset);
            }
            catch (FactsPdfException error) when (warnings is not null && error.Code is "FPDF1201" or "FPDF1202"
                && CssSpecValidity.InvalidDeclaration(node.Name, CanonicalName(node.Name.ToLowerInvariant()), node.Values))
            {
                output.RemoveRange(mark, output.Count - mark);
                warnings.Add(error);
            }
        }
        var compiled = output.ToArray();
        output.Clear(); // drop the references before the list waits in its slot
        if (output.Capacity <= MaxPooledDeclarations) pooledDeclarations = output;
        return compiled;
    }

    /// <summary>The property a legacy, prefixed or alias name shares its cascade slot, values and errors with.</summary>
    internal static string CanonicalName(string name) => name switch
    {
        "page-break-before" => "break-before",
        "page-break-after" => "break-after",
        "page-break-inside" => "break-inside",
        // CSS Text: word-wrap is the legacy name of overflow-wrap and
        // shares its cascade slot, values and applicability.
        "word-wrap" => "overflow-wrap",
        // print-color-adjust is still mostly written with Chromium's prefix.
        // color-adjust is the deprecated name of print-color-adjust (Firefox, older print CSS);
        // background-color-adjust is a non-standard name print resets write next to both.
        "-webkit-print-color-adjust" or "color-adjust" or "background-color-adjust" => "print-color-adjust",
        // Tailwind preflight and modern-normalize write tab-size with the Gecko prefix first.
        "-moz-tab-size" => "tab-size",
        // text-size-adjust is almost always written with the WebKit prefix (normalize.css).
        // normalize.css v3/v4 also writes the -ms- spelling (and some resets -moz-).
        "-webkit-text-size-adjust" or "-ms-text-size-adjust" or "-moz-text-size-adjust" => "text-size-adjust",
        // appearance resets are still written with both engine prefixes.
        "-webkit-appearance" or "-moz-appearance" => "appearance",
        "-webkit-backface-visibility" => "backface-visibility",
        // Resets and component libraries still disable selection with all three prefixes.
        "-webkit-user-select" or "-moz-user-select" or "-ms-user-select" => "user-select",
        // Bootstrap 3 and older resets write box-sizing with both engine prefixes first.
        "-webkit-box-sizing" or "-moz-box-sizing" => "box-sizing",
        // Bootstrap 3, Font Awesome 4 and older themes write the engine-prefixed spellings of
        // border-radius, box-shadow, transform and filter before (or instead of) the standard
        // name; they share its cascade slot, values, applicability and errors.
        "-webkit-border-radius" or "-moz-border-radius" => "border-radius",
        "-webkit-box-shadow" or "-moz-box-shadow" => "box-shadow",
        "-webkit-transform" or "-moz-transform" or "-ms-transform" or "-o-transform" => "transform",
        "-webkit-filter" => "filter",
        "-webkit-transform-origin" => "transform-origin",
        "-webkit-clip-path" => "clip-path",
        "-webkit-backdrop-filter" => "backdrop-filter",
        // normalize.css 7 and Bootstrap 4's reboot write -webkit-text-decoration-skip:objects on a.
        "-webkit-text-decoration-skip" => "text-decoration-skip",
        // Bootstrap 4's reboot and Safari-era stylesheets write the text-decoration shorthand
        // and longhands with the WebKit prefix (abbr{-webkit-text-decoration:underline dotted}).
        "-webkit-text-decoration" or "-webkit-text-decoration-line" or "-webkit-text-decoration-color"
            or "-webkit-text-decoration-style" => name[8..],
        // Older stylesheets still write font-feature-settings with both engine prefixes.
        "-webkit-font-feature-settings" or "-moz-font-feature-settings" => "font-feature-settings",
        // Older stylesheets (Bootstrap 3 and many themes) still write -webkit-transition.
        "-webkit-transition" or "-webkit-transition-property" or "-webkit-transition-duration"
            or "-webkit-transition-timing-function" or "-webkit-transition-delay"
            or "-webkit-animation" or "-webkit-animation-name" or "-webkit-animation-duration"
            or "-webkit-animation-timing-function" or "-webkit-animation-delay"
            or "-webkit-animation-iteration-count" or "-webkit-animation-direction"
            or "-webkit-animation-fill-mode" or "-webkit-animation-play-state" => name[8..],
        _ => name
    };

    // Keeps the pooled array small; a larger declaration block allocates its own list.
    private const int MaxPooledDeclarations = 1024;
    [ThreadStatic] private static List<CssDeclaration>? pooledDeclarations;

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

    // revert and revert-layer (no cascade layers exist, so it reverts like revert) roll back the
    // author origin; the cascade discards such a winner (CssStylesheets.Compute).
    private static bool Wide(string text) => text is "inherit" or "initial" or "unset" or "revert" or "revert-layer";

    // The only value each property accepts: the initial value, which the report renderer's
    // normal flow already produces (no positioning, floats, clipping, hiding, stacking,
    // transparency, shadows, transforms, filters or background images). CSS-wide keywords
    // also compute to it, since nothing else can be declared.
    private static string? InitialOnlyValue(string name) => name switch
    {
        "position" => "static",
        "float" or "clear" => "none",
        "overflow" or "overflow-x" or "overflow-y" or "visibility" => "visible",
        "z-index" => "auto",
        "opacity" => "1",
        "box-shadow" or "text-shadow" or "transform" or "filter" or "background-image" => "none",
        // Compositing and clipping resets: nothing is blended, isolated or clipped.
        "clip-path" or "backdrop-filter" => "none",
        "mix-blend-mode" => "normal",
        "isolation" => "auto",
        // Font tuning resets (Tailwind preflight writes font-feature-settings and
        // font-variation-settings as normal on html): the renderer applies no OpenType
        // features, variation axes, optical sizing, aspect adjustment or width selection
        // beyond the font as given, which is exactly what these initial values ask for.
        "font-feature-settings" or "font-variation-settings" or "font-variant-ligatures"
            or "font-variant-numeric" or "font-stretch" => "normal",
        // The remaining font-variant longhands and the font-variant shorthand at normal
        // (email and print templates reset them) ask for no small caps, East Asian forms,
        // alternates or sub/superscript glyphs, which the renderer never substitutes.
        "font-variant" or "font-variant-caps" or "font-variant-east-asian"
            or "font-variant-alternates" or "font-variant-position" => "normal",
        // Font language, emoji and colour-palette selection at normal (the font's own default)
        // and no emphasis marks: the renderer applies no language-specific glyph forms, emoji
        // presentation switches or palettes, and draws no East Asian emphasis marks.
        "font-language-override" or "font-variant-emoji" or "font-palette" => "normal",
        "text-emphasis" or "text-emphasis-style" => "none",
        "font-optical-sizing" => "auto",
        "font-size-adjust" => "none",
        // Writing-direction resets at their initial value: the renderer lays out only
        // left-to-right horizontal text (characters that need bidi fail FPDF1305), never
        // reorders, rotates or combines glyphs, so ltr, normal, horizontal-tb, mixed and none
        // ask for exactly that; rtl, isolate, vertical-rl and the rest still fail FPDF1202.
        "direction" => "ltr",
        "unicode-bidi" => "normal",
        "writing-mode" => "horizontal-tb",
        "text-orientation" => "mixed",
        "text-combine-upright" => "none",
        // Multi-column resets at their initial value: the renderer lays every box out in one
        // column, so no column count or width, the normal gap, no spanning, balanced fill and no
        // column rule describe exactly that; column-count:2 and the rest still fail FPDF1202.
        "column-count" or "column-width" or "columns" => "auto",
        // gap and row-gap at normal: no flex, grid or multi-column container exists, so no gap
        // is ever inserted between rows or columns; gap:0 and row-gap:1em still fail FPDF1202.
        "column-gap" or "row-gap" or "gap" => "normal",
        "column-span" or "column-rule" or "column-rule-style" => "none",
        "column-fill" => "balance",
        // Overflow and fragment resets: no box clips its overflow (overflow stays visible), so
        // text-overflow:clip and line-clamp:none ask for nothing, and a box split across pages
        // already paints as slices, which is what box-decoration-break:slice describes.
        "text-overflow" => "clip",
        "line-clamp" or "-webkit-line-clamp" => "none",
        "box-decoration-break" or "-webkit-box-decoration-break" => "slice",
        // Transform, masking and float-shape resets: like transform:none, the individual
        // transforms and perspective at none and transform-style:flat move nothing; no mask or
        // float shape is ever applied, so none asks for exactly what is drawn.
        "rotate" or "scale" or "translate" or "perspective" => "none",
        "transform-style" => "flat",
        "mask" or "mask-image" or "-webkit-mask" or "-webkit-mask-image" => "none",
        "shape-outside" => "none",
        // Typography resets: no hanging punctuation, no hyphenation character choice (nothing
        // is hyphenated automatically), no CJK spacing trim beyond the font and underlines at
        // the renderer's own position are exactly what is drawn.
        "hanging-punctuation" => "none",
        "hyphenate-character" or "text-underline-position" => "auto",
        "text-spacing-trim" => "normal",
        // Background and container resets: no background image is ever painted (only
        // background-image:none is accepted), so it neither stays fixed across pages, nor blends,
        // nor needs an origin box; no container query is evaluated, so no element is a container.
        "background-attachment" => "scroll",
        "background-blend-mode" => "normal",
        "background-origin" => "padding-box",
        "container-type" => "normal",
        "container" => "none",
        // Aspect-ratio and mask longhand resets: images always keep their pixel ratio and other
        // boxes have none, which is aspect-ratio:auto; with no mask ever applied (only mask:none
        // is accepted), the mask longhands at their initial values describe exactly that.
        "aspect-ratio" or "mask-size" => "auto",
        "mask-repeat" => "repeat",
        "mask-origin" or "mask-clip" => "border-box",
        "mask-mode" => "match-source",
        "mask-composite" => "add",
        "mask-type" => "luminance",
        // Animation timeline and view-transition resets: no animation ever runs (animations are
        // validated and dropped) and the page is never scrolled or navigated, so the default
        // document timeline, no named scroll or view timeline and no view-transition name are
        // exactly what is drawn.
        "animation-composition" => "replace",
        "animation-timeline" or "view-timeline-inset" => "auto",
        "animation-range" or "animation-range-start" or "animation-range-end" => "normal",
        "scroll-timeline" or "scroll-timeline-name" or "view-timeline" or "view-timeline-name"
            or "timeline-scope" or "view-transition-name" or "view-transition-class" => "none",
        "scroll-timeline-axis" or "view-timeline-axis" => "block",
        // Anchor positioning and top-layer resets: every box stays in normal flow (only
        // position:static is accepted) and nothing is promoted to a top layer, so no anchor name,
        // anchor scope, position area or fallback list and overlay:none describe what is drawn.
        "anchor-name" or "anchor-scope" or "position-area" or "position-try"
            or "position-try-fallbacks" or "overlay" => "none",
        "position-try-order" => "normal",
        // Screen and UI resets: no size is ever interpolated (nothing animates), focus and
        // reading order follow the document, form fields keep their fixed sizing, colours are
        // never mapped to a high-dynamic-range display and no text caret is drawn.
        "interpolate-size" => "numeric-only",
        "reading-flow" => "normal",
        "field-sizing" => "fixed",
        "dynamic-range-limit" => "no-limit",
        "caret" or "caret-animation" or "caret-shape" => "auto",
        // Text box and paint resets: lines break greedily (text-wrap-style:auto), line boxes
        // keep the font's full ascent and descent (no text-box trimming), glyphs are filled
        // without a stroke to reorder and no MathML layout is applied.
        "text-wrap-style" or "text-box-edge" => "auto",
        "text-box-trim" => "none",
        "text-box" or "paint-order" or "math-style" or "math-shift" => "normal",
        // Intrinsic-size and transform-box resets: size containment never applies (only
        // contain:none and content-visibility:visible are accepted), so no fallback intrinsic
        // size is used, and with only transform:none accepted the transform box is never consulted.
        "contain-intrinsic-size" or "contain-intrinsic-width" or "contain-intrinsic-height"
            or "contain-intrinsic-block-size" or "contain-intrinsic-inline-size" => "none",
        "transform-box" => "view-box",
        // Box offsets and border images: with only position:static accepted the offsets never
        // move a box, and with no border image every border is painted from its style, width
        // and colour, which is what auto and none ask for.
        "top" or "right" or "bottom" or "left" or "inset" or "inset-block" or "inset-inline"
            or "inset-block-start" or "inset-block-end" or "inset-inline-start"
            or "inset-inline-end" => "auto",
        "border-image" or "border-image-source" => "none",
        // Legacy and containment resets: zoom:normal (or 1/100%, below) scales nothing;
        // contain:none and content-visibility:visible skip nothing; line breaking, justification
        // and image interpolation at auto are already the renderer's own choice.
        "zoom" => "normal",
        "contain" => "none",
        "content-visibility" => "visible",
        "line-break" or "text-justify" or "image-rendering" => "auto",
        // Empty cells already paint their background and borders (empty-cells:show), and a
        // caption is always placed above its table (caption-side:bottom is not implemented).
        "empty-cells" => "show",
        "caption-side" => "top",
        // Motion path, drop cap and emphasis colour resets: no box is ever placed along a path
        // (only transform:none and position:static are accepted), no first letter is enlarged
        // into a drop cap, and no emphasis mark is drawn (only text-emphasis:none is accepted),
        // so the initial values describe exactly what is painted.
        "offset-path" => "none",
        "offset-rotate" or "offset-anchor" => "auto",
        "initial-letter" => "normal",
        "text-emphasis-color" => "currentcolor",
        _ => null
    };

    private static void ValidateInitialOnly(string name, string initial, CssDeclarationNode node)
    {
        // Exactly one component, a token; checked on the raw values so a function such as
        // rotate() or url() gets this message rather than a colour or box-value one.
        CssSyntaxComponent? value = null;
        var count = 0;
        for (var i = 0; i < node.Values.Count; i++)
            if (node.Values[i] is not CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Whitespace })
            { value ??= node.Values[i]; count++; }
        if (count == 1 && value is CssTokenComponent { Token: var token })
        {
            if (token.Kind == CssSyntaxTokenKind.Ident)
            {
                var term = token.Value.ToLowerInvariant();
                if (Wide(term) || term == initial) return;
            }
            if (name is "opacity" or "zoom" && OpaqueAlpha(token)) return;
        }
        throw new FactsPdfException("FPDF1202",
            $"{name} supports only {initial}: the report renderer does not implement other values.",
            value?.Span.Start ?? node.Span.Start);
    }

    private static bool IsInteractionOnly(string name) =>
        name is "cursor" or "pointer-events" or "user-select" or "resize" or "print-color-adjust"
            or "text-rendering" or "-webkit-font-smoothing" or "-moz-osx-font-smoothing"
            or "text-size-adjust" or "appearance" or "backface-visibility" or "touch-action"
            or "will-change" or "transform-origin" or "transition" or "transition-property" or "transition-duration"
            or "transition-timing-function" or "transition-delay" or "transition-behavior"
            or "animation" or "animation-name" or "animation-duration" or "animation-timing-function"
            or "animation-delay" or "animation-iteration-count" or "animation-direction"
            or "animation-fill-mode" or "animation-play-state"
            or "scroll-behavior" or "overscroll-behavior" or "overscroll-behavior-x"
            or "overscroll-behavior-y" or "overscroll-behavior-inline" or "overscroll-behavior-block"
            or "overflow-anchor" or "-webkit-overflow-scrolling" or "scrollbar-width"
            or "caret-color" or "accent-color" or "-webkit-tap-highlight-color" or "scrollbar-color"
            or "scrollbar-gutter" or "color-scheme" or "forced-color-adjust"
            or "-webkit-user-drag" or "-webkit-touch-callout" or "-ms-overflow-style"
            // Tab stops never reach a page: collapsed tabs become spaces and preserved tabs fail
            // (FPDF1305), so the tab width is validated and dropped.
            or "tab-size"
            // Aural rendering only (CSS Speech); icon-font stylesheets write speak:none or never.
            or "speak"
            // Hints the renderer already satisfies: it synthesizes no bold or italic face and
            // never breaks an underline around descenders (see the keyword arms).
            or "font-synthesis" or "font-synthesis-weight" or "font-synthesis-style"
            or "font-synthesis-small-caps" or "font-synthesis-position"
            or "text-decoration-skip-ink" or "text-decoration-skip"
            or "font-kerning"
            // Quotation marks are only inserted by q elements and open-quote/close-quote content,
            // and neither is accepted, so no quote is ever drawn (see the keyword arm and pairs).
            or "quotes"
            // Fonts are never discovered: the supplied font chain (or the standard Courier
            // fallback) is the only installed font, so every family list resolves to it, as a
            // browser falls back when none of the named families is installed (see FamilyList).
            or "font-family"
        // Cell spacing at its initial value: separated cell boxes already touch (zero spacing),
        // so one or two zero lengths are validated and dropped; any other spacing fails.
        || name == "border-spacing"
        // Scroll-into-view offsets (docs and utility stylesheets write scroll-margin-top on
        // anchored headings): a PDF page is never scrolled, so the offsets are validated and
        // dropped.
        || ScrollOffsetSides(name) > 0
        // Scroll snapping only steers where a scroll container comes to rest.
        || name is "scroll-snap-type" or "scroll-snap-align" or "scroll-snap-stop";

    // The keyword values of each interaction-only property (CSS UI 4; pointer-events also
    // takes the SVG keywords). cursor images (url(...) with a fallback) are not accepted.
    private static bool InteractionKeyword(string name, string term) => name switch
    {
        "cursor" => term is "auto" or "default" or "none" or "context-menu" or "help" or "pointer"
            or "progress" or "wait" or "cell" or "crosshair" or "text" or "vertical-text" or "alias"
            or "copy" or "move" or "no-drop" or "not-allowed" or "grab" or "grabbing" or "e-resize"
            or "n-resize" or "ne-resize" or "nw-resize" or "s-resize" or "se-resize" or "sw-resize"
            or "w-resize" or "ew-resize" or "ns-resize" or "nesw-resize" or "nwse-resize"
            or "col-resize" or "row-resize" or "all-scroll" or "zoom-in" or "zoom-out",
        "pointer-events" => term is "auto" or "none" or "visiblepainted" or "visiblefill"
            or "visiblestroke" or "visible" or "painted" or "fill" or "stroke" or "all",
        "user-select" => term is "auto" or "text" or "none" or "contain" or "all",
        "resize" => term is "none" or "both" or "horizontal" or "vertical" or "block" or "inline",
        // economy only permits a user agent to adjust colours; backgrounds and colours are
        // always painted exactly as declared, which both keywords allow.
        "print-color-adjust" => term is "exact" or "economy",
        // Screen rasteriser hints (SVG 1.1 text-rendering; the WebKit and Gecko macOS
        // font-smoothing switches). A PDF stores glyph outlines and the viewer chooses
        // anti-aliasing and hinting, so no keyword changes the file.
        "text-rendering" => term is "auto" or "optimizespeed" or "optimizelegibility" or "geometricprecision",
        "-webkit-font-smoothing" => term is "auto" or "none" or "antialiased" or "subpixel-antialiased",
        "-moz-osx-font-smoothing" => term is "auto" or "grayscale",
        // Mobile browsers' automatic font inflation; a PDF page is never inflated.
        "text-size-adjust" => term is "auto" or "none",
        // Native form-control styling (CSS UI 4 keywords and compat values). The renderer
        // draws no native widgets, so every element already paints as appearance:none.
        "appearance" => term is "none" or "auto" or "base" or "menulist-button" or "textfield"
            or "searchfield" or "textarea" or "push-button" or "slider-horizontal" or "checkbox"
            or "radio" or "square-button" or "menulist" or "listbox" or "meter"
            or "progress-bar" or "button",
        // Only a 3D-transformed element can show its back face; transform accepts only none.
        "backface-visibility" => term is "visible" or "hidden",
        // Which touch gestures a screen handles; a PDF page has no touch input.
        "touch-action" => term is "auto" or "none" or "manipulation" or "pan-x" or "pan-y"
            or "pan-left" or "pan-right" or "pan-up" or "pan-down" or "pinch-zoom",
        // Scroll-container behaviour (CSSOM View, Overscroll Behavior, Scroll Anchoring,
        // Scrollbars Styling, WebKit momentum scrolling). A PDF page never scrolls: overflow
        // accepts only visible, so no box is a scroll container and no keyword changes the page.
        // The overscroll-behavior shorthand takes one keyword here, like touch-action.
        "scroll-behavior" => term is "auto" or "smooth",
        "overscroll-behavior" or "overscroll-behavior-x" or "overscroll-behavior-y"
            or "overscroll-behavior-inline" or "overscroll-behavior-block" => term is "auto" or "contain" or "none",
        "overflow-anchor" => term is "auto" or "none",
        "-webkit-overflow-scrolling" => term is "auto" or "touch",
        "scrollbar-width" => term is "auto" or "thin" or "none",
        // Screen-only UI colours and scrollbar space: the text caret, native form-control
        // accents, the WebKit tap flash and scrollbar paint. A PDF page has no caret, native
        // control, tap or scrollbar, so auto and any colour (validated, never painted) are dropped.
        "caret-color" or "accent-color" or "scrollbar-color" => term == "auto",
        "scrollbar-gutter" => term is "auto" or "stable",
        // Screen colour-scheme hints: color-scheme lets a browser pick dark UA defaults and
        // forced-color-adjust opts out of a forced high-contrast palette. A PDF page has no
        // preferred or forced scheme and paints every colour exactly as declared.
        "color-scheme" => term is "normal" or "light" or "dark",
        "forced-color-adjust" => term is "auto" or "none" or "preserve-parent-color",
        // WebKit drag-and-drop and the iOS long-press callout, and the legacy Edge/IE scrollbar
        // style: a PDF page has no pointer drag, touch callout or scrollbar.
        "-webkit-user-drag" => term is "auto" or "element" or "none",
        "-webkit-touch-callout" => term is "default" or "none",
        "-ms-overflow-style" => term is "auto" or "none" or "scrollbar" or "-ms-autohiding-scrollbar",
        // CSS Speech keywords plus the CSS 2 aural values icon fonts still write; a PDF page is
        // not spoken by the renderer.
        "speak" => term is "auto" or "never" or "always" or "none" or "normal" or "spell-out",
        // Only the values the renderer already honours: skip-ink auto leaves ink skipping to the
        // renderer (which does none) and none forbids it; font-synthesis:none forbids synthesized
        // faces, and none are made. text-decoration-skip-ink:all and the synthesis keywords that
        // ask for faux faces still fail.
        "text-decoration-skip-ink" => term is "auto" or "none",
        "font-synthesis" => term == "none",
        // The longhands of font-synthesis (CSS Fonts 4): none forbids one kind of synthesized
        // face, and no bold, italic, small-caps or super/subscript face is ever synthesized;
        // auto asks for synthesis and still fails, as for the shorthand.
        "font-synthesis-weight" or "font-synthesis-style" or "font-synthesis-small-caps"
            or "font-synthesis-position" => term == "none",
        // No glyph pair is ever kerned (no kern or GPOS data is read), so auto (the renderer's
        // choice) and none (never kern) are already honoured; normal asks for kerning and fails.
        "font-kerning" => term is "auto" or "none",
        "quotes" => term is "auto" or "none" or "match-parent",
        // Legacy text-decoration-skip, which Chrome does not implement: an inline image never
        // carries its parent's decoration (objects), and Chrome paints none and auto the same way.
        // ink, spaces, edges and box-decoration ask for gaps the renderer never draws, so they fail.
        "text-decoration-skip" => term is "none" or "auto" or "objects",
        "scroll-snap-type" => term is "none" or "x" or "y" or "block" or "inline" or "both",
        "scroll-snap-align" => term is "none" or "start" or "end" or "center",
        "scroll-snap-stop" => term is "normal" or "always",
        _ => false
    };

    private static bool OnlyStrings(CssDeclarationNode node)
    {
        for (var i = 0; i < node.Values.Count; i++)
            if (node.Values[i] is not CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Whitespace or CssSyntaxTokenKind.String })
                return false;
        return true;
    }

    private static void ValidateInteractionOnly(string name, CssDeclarationNode node)
    {
        CssSyntaxComponent? value = null;
        var count = 0;
        for (var i = 0; i < node.Values.Count; i++)
            if (node.Values[i] is not CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Whitespace })
            { value ??= node.Values[i]; count++; }
        if (count == 1 && value is CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.Ident } token })
        {
            var term = token.Value.ToLowerInvariant();
            if (Wide(term) || InteractionKeyword(name, term)) return;
        }
        // will-change is a compositor hint: auto, or a comma list of animateable features
        // (transform, opacity, scroll-position, contents, ...). A static page never changes.
        if (name == "will-change" && WillChangeList(node)) return;
        if (name == "font-family" && FamilyList(node)) return;
        // transform-origin only moves the point a transform pivots around; the only accepted
        // transform is none, so no box is ever moved and a well-formed origin changes nothing.
        if (name == "transform-origin" && TransformOrigin(node)) return;
        // Transitions only animate a change of computed value on screen; a written PDF page
        // is never restyled, so a well-formed transition list is validated and dropped.
        if (IsTransition(name) && TransitionList(name, node)) return;
        // Animations only run on screen; a written PDF page shows no keyframe, so a well-formed
        // animation list is validated and dropped (@keyframes rules are dropped likewise).
        if (IsAnimation(name) && AnimationList(name, node)) return;
        if (name is "caret-color" or "accent-color" or "-webkit-tap-highlight-color" or "scrollbar-color"
            && UiColours(node, name == "scrollbar-color" ? 2 : 1)) return;
        // color-scheme also takes a list of scheme names with one leading or trailing "only".
        if (name == "color-scheme" && ColorSchemeList(node)) return;
        // scrollbar-gutter also takes stable both-edges (either order).
        if (name == "scrollbar-gutter" && ScrollbarGutterBothEdges(node)) return;
        // scroll-margin* take one to four lengths (one or two for the axis pairs, one for a side);
        // scroll-padding* take auto or non-negative lengths and percentages.
        if (ScrollOffsetSides(name) is > 0 and var sides && count >= 1 && count <= sides && ScrollOffsets(name, node)) return;
        // scroll-snap-type also takes an axis then mandatory or proximity; scroll-snap-align
        // also takes a block then an inline alignment.
        if (count == 2 && name is "scroll-snap-type" or "scroll-snap-align" && ScrollSnapPair(name, node)) return;
        if (name == "border-spacing" && count is 1 or 2 && ZeroLengths(node)) return;
        // quotes also takes one or more pairs of strings (open and close marks per level).
        if (name == "quotes" && count >= 2 && count % 2 == 0 && OnlyStrings(node)) return;
        // tab-size takes a non-negative number of spaces or a non-negative length.
        if (count == 1 && name == "tab-size" && value is CssTokenComponent { Token: var tab } && TabSize(tab)) return;
        // text-size-adjust also takes a non-negative percentage (text-size-adjust:100%).
        if (count == 1 && name == "text-size-adjust"
            && value is CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.Percentage } percentage }
            && double.TryParse(percentage.Value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var amount)
            && amount >= 0 && double.IsFinite(amount))
            return;
        throw new FactsPdfException("FPDF1202",
            $"{name} supports only {(name is "caret-color" or "accent-color" ? "auto or a colour" : name == "-webkit-tap-highlight-color" ? "a colour" : name == "scrollbar-color" ? "auto or two colours (thumb and track)" : name == "scrollbar-gutter" ? "auto, stable or stable both-edges" : name == "color-scheme" ? "normal, or scheme names such as light dark with an optional only" : name == "text-size-adjust" ? "auto, none or a non-negative percentage" : name == "tab-size" ? "a non-negative number or length" : ScrollOffsetSides(name) > 0 ? ScrollOffsetForm(name) : name == "scroll-snap-type" ? "none, or an axis with an optional mandatory or proximity" : name == "scroll-snap-align" ? "one or two of none, start, end or center" : name == "text-decoration-skip-ink" ? "auto or none" : name == "text-decoration-skip" ? "none, auto or objects" : name.StartsWith("font-synthesis", StringComparison.Ordinal) ? "none" : name == "font-kerning" ? "auto or none" : name == "quotes" ? "auto, none, match-parent or pairs of strings" : name == "font-family" ? "a comma-separated list of family names (strings or identifiers) and generic families" : name == "border-spacing" ? "0 (one or two zero lengths)" : name == "will-change" ? "auto or a comma-separated list of property names" : name == "transform-origin" ? "one or two positions (left, center, right, top, bottom, a length or a percentage) and an optional length" : IsTransition(name) ? TransitionForm(name) : IsAnimation(name) ? AnimationForm(name) : "a single keyword")}: it does not change the PDF page, so the declaration is validated and dropped.",
            value?.Span.Start ?? node.Span.Start);
    }

    private static bool ScrollSnapPair(string name, CssDeclarationNode node)
    {
        string? first = null;
        for (var i = 0; i < node.Values.Count; i++)
        {
            if (node.Values[i] is CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Whitespace }) continue;
            if (node.Values[i] is not CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.Ident } token }) return false;
            var term = token.Value.ToLowerInvariant();
            if (first is null) { first = term; continue; }
            return name == "scroll-snap-type"
                ? first is "x" or "y" or "block" or "inline" or "both" && term is "mandatory" or "proximity"
                : first is "none" or "start" or "end" or "center" && term is "none" or "start" or "end" or "center";
        }
        return false;
    }

    // How many components a scroll-margin or scroll-padding name takes (0: not one of them).
    private static int ScrollOffsetSides(string name)
    {
        if (!name.StartsWith("scroll-margin", StringComparison.Ordinal)
            && !name.StartsWith("scroll-padding", StringComparison.Ordinal)) return 0;
        var side = name[(name[7] == 'm' ? "scroll-margin".Length : "scroll-padding".Length)..];
        return side switch
        {
            "" => 4,
            "-inline" or "-block" => 2,
            "-top" or "-right" or "-bottom" or "-left" or "-inline-start" or "-inline-end"
                or "-block-start" or "-block-end" => 1,
            _ => 0
        };
    }

    private static string ScrollOffsetForm(string name)
    {
        var sides = ScrollOffsetSides(name);
        var many = sides switch { 1 => "one", 2 => "one or two", _ => "one to four" };
        return name[7] == 'm'
            ? $"{many} length{(sides == 1 ? "" : "s")}"
            : $"{many} of auto or a non-negative length or percentage";
    }

    // Each component of a scroll-margin (any length or zero) or scroll-padding (auto, zero, a
    // non-negative length or percentage) declaration; the caller has checked the count.
    private static bool ScrollOffsets(string name, CssDeclarationNode node)
    {
        var padding = name[7] == 'p';
        for (var i = 0; i < node.Values.Count; i++)
        {
            if (node.Values[i] is CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Whitespace }) continue;
            if (node.Values[i] is not CssTokenComponent { Token: var token }) return false;
            if (padding && token.Kind == CssSyntaxTokenKind.Ident && token.Value.Equals("auto", StringComparison.OrdinalIgnoreCase))
                continue;
            var length = token.Kind == CssSyntaxTokenKind.Dimension
                && token.Unit?.ToLowerInvariant() is "px" or "pt" or "pc" or "in" or "cm" or "mm" or "q"
                    or "em" or "rem" or "ex" or "ch" or "vh" or "vw" or "vmin" or "vmax";
            var kind = token.Kind == CssSyntaxTokenKind.Number || length
                || padding && token.Kind == CssSyntaxTokenKind.Percentage;
            if (!kind || !double.TryParse(token.Value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var amount) || !double.IsFinite(amount))
                return false;
            // A bare number is only allowed as zero; padding is never negative.
            if (token.Kind == CssSyntaxTokenKind.Number && !ZeroNumber(token.Value) || padding && amount < 0) return false;
        }
        return true;
    }

    // One or two positions (a horizontal then a vertical, or keywords in either order) and an
    // optional third length (the z offset).
    private static bool TransformOrigin(CssDeclarationNode node)
    {
        var count = 0;
        var first = 0;
        for (var i = 0; i < node.Values.Count; i++)
        {
            if (node.Values[i] is CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Whitespace }) continue;
            if (node.Values[i] is not CssTokenComponent { Token: var token } || ++count > 3) return false;
            // 1 horizontal keyword, 2 vertical keyword, 3 center, 4 length, 5 percentage.
            var kind = OriginComponent(token);
            if (kind == 0) return false;
            if (count == 3) { if (kind != 4) return false; continue; }
            if (count == 1) { first = kind; continue; }
            // After a vertical keyword the second is horizontal (a keyword or center); after
            // center anything goes; otherwise the second is vertical.
            if (first == 2 ? kind is 2 or 4 or 5 : first != 3 && kind == 1) return false;
        }
        return count > 0;
    }

    private static int OriginComponent(CssSyntaxToken token)
    {
        if (token.Kind == CssSyntaxTokenKind.Ident)
            return token.Value.ToLowerInvariant() switch
            {
                "left" or "right" => 1,
                "top" or "bottom" => 2,
                "center" => 3,
                _ => 0,
            };
        var length = token.Kind == CssSyntaxTokenKind.Dimension
            && token.Unit?.ToLowerInvariant() is "px" or "pt" or "pc" or "in" or "cm" or "mm" or "q"
                or "em" or "rem" or "ex" or "ch" or "vh" or "vw" or "vmin" or "vmax";
        if (!length && token.Kind is not (CssSyntaxTokenKind.Number or CssSyntaxTokenKind.Percentage)) return 0;
        if (!double.TryParse(token.Value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var amount) || !double.IsFinite(amount))
            return 0;
        // A bare number is only allowed as zero (a length).
        if (token.Kind == CssSyntaxTokenKind.Number) return ZeroNumber(token.Value) ? 4 : 0;
        return length ? 4 : 5;
    }

    // Every component is a zero length: a bare zero or a zero with a length unit.
    private static bool ZeroLengths(CssDeclarationNode node)
    {
        for (var i = 0; i < node.Values.Count; i++)
        {
            if (node.Values[i] is CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Whitespace }) continue;
            if (node.Values[i] is not CssTokenComponent { Token: var token }) return false;
            var length = token.Kind == CssSyntaxTokenKind.Number
                || token.Kind == CssSyntaxTokenKind.Dimension
                    && token.Unit?.ToLowerInvariant() is "px" or "pt" or "pc" or "in" or "cm" or "mm" or "q"
                        or "em" or "rem" or "ex" or "ch" or "vh" or "vw" or "vmin" or "vmax";
            if (!length || !ZeroNumber(token.Value)) return false;
        }
        return true;
    }

    // A bare number is zero only when its mantissa has no nonzero digit: 1e-999 parses to 0 but
    // is not zero, so it must not pass as the unitless zero length.
    private static bool ZeroNumber(string text)
    {
        for (var i = 0; i < text.Length && text[i] is not ('e' or 'E'); i++)
            if (text[i] is >= '1' and <= '9') return false;
        return true;
    }

    private static bool TabSize(CssSyntaxToken token) =>
        (token.Kind == CssSyntaxTokenKind.Number
            || token.Kind == CssSyntaxTokenKind.Dimension
                && token.Unit?.ToLowerInvariant() is "px" or "pt" or "pc" or "in" or "cm" or "mm" or "q"
                    or "em" or "rem" or "ex" or "ch")
        && double.TryParse(token.Value, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var size)
        && double.IsFinite(size) && size >= 0;

    // color-scheme: normal, or one or more scheme names (light, dark or custom identifiers)
    // with at most one "only" at the start or end; normal, only and CSS-wide keywords cannot
    // be scheme names.
    private static bool ColorSchemeList(CssDeclarationNode node)
    {
        var count = 0; var onlyAt = -1; var bad = false;
        for (var i = 0; i < node.Values.Count; i++)
        {
            if (node.Values[i] is CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Whitespace }) continue;
            if (node.Values[i] is not CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.Ident } token }) return false;
            var term = token.Value.ToLowerInvariant();
            if (term == "only")
            {
                if (onlyAt >= 0) return false;
                onlyAt = count;
            }
            else if (term is "normal" or "default" || Wide(term)) bad = true;
            count++;
        }
        var names = onlyAt >= 0 ? count - 1 : count;
        return !bad && names > 0 && (onlyAt < 0 || onlyAt == 0 || onlyAt == count - 1);
    }

    // Exactly `count` colours: a named colour, transparent, currentColor, #rgb, #rgba, #rrggbb,
    // #rrggbbaa or an rgb()/rgba()/hsl()/hsla() function of numbers, percentages, angles, commas
    // and "/". Alpha is allowed because the colour is validated and dropped, never painted.
    private static bool UiColours(CssDeclarationNode node, int count)
    {
        var colours = 0;
        for (var i = 0; i < node.Values.Count; i++)
        {
            var component = node.Values[i];
            if (component is CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Whitespace }) continue;
            if (++colours > count || !UiColour(component)) return false;
        }
        return colours == count;
    }

    private static bool UiColour(CssSyntaxComponent component)
    {
        if (component is CssTokenComponent { Token: var token })
        {
            if (token.Kind == CssSyntaxTokenKind.Ident)
            {
                var term = token.Value.ToLowerInvariant();
                return term is "transparent" or "currentcolor" || CssNamedColors.Hex(term) is not null;
            }
            if (token.Kind != CssSyntaxTokenKind.Hash || token.Value.Length is not (3 or 4 or 6 or 8)) return false;
            for (var i = 0; i < token.Value.Length; i++)
                if (!char.IsAsciiHexDigit(token.Value[i])) return false;
            return true;
        }
        if (component is not CssFunctionComponent function
            || function.Name.ToLowerInvariant() is not ("rgb" or "rgba" or "hsl" or "hsla")) return false;
        var channels = 0;
        for (var i = 0; i < function.Values.Count; i++)
        {
            if (function.Values[i] is not CssTokenComponent { Token: var item }) return false;
            switch (item.Kind)
            {
                case CssSyntaxTokenKind.Whitespace or CssSyntaxTokenKind.Comma: break;
                case CssSyntaxTokenKind.Delim when item.Value == "/": break;
                case CssSyntaxTokenKind.Number or CssSyntaxTokenKind.Percentage or CssSyntaxTokenKind.Dimension:
                    channels++;
                    break;
                default: return false;
            }
        }
        return channels is 3 or 4;
    }

    private static bool ScrollbarGutterBothEdges(CssDeclarationNode node)
    {
        string? first = null, second = null;
        for (var i = 0; i < node.Values.Count; i++)
        {
            if (node.Values[i] is not CssTokenComponent { Token: var token }) return false;
            if (token.Kind == CssSyntaxTokenKind.Whitespace) continue;
            if (token.Kind != CssSyntaxTokenKind.Ident || second is not null) return false;
            if (first is null) first = token.Value.ToLowerInvariant();
            else second = token.Value.ToLowerInvariant();
        }
        return (first, second) is ("stable", "both-edges") or ("both-edges", "stable");
    }

    // CSS Fonts 4 <family-name>#: each entry is one string or a sequence of identifiers (a
    // generic family such as sans-serif is a single identifier); entries are comma-separated and
    // none may be empty, a CSS-wide keyword or the reserved default.
    private static bool FamilyList(CssDeclarationNode node)
    {
        var entries = 0;
        var inEntry = false;
        var stringEntry = false;
        for (var i = 0; i < node.Values.Count; i++)
        {
            if (node.Values[i] is not CssTokenComponent { Token: var token }) return false;
            switch (token.Kind)
            {
                case CssSyntaxTokenKind.Whitespace:
                    continue;
                case CssSyntaxTokenKind.Comma:
                    if (!inEntry) return false;
                    inEntry = false;
                    continue;
                case CssSyntaxTokenKind.String:
                    if (inEntry || token.Value.Length == 0) return false;
                    inEntry = stringEntry = true;
                    entries++;
                    continue;
                case CssSyntaxTokenKind.Ident:
                    var term = token.Value.ToLowerInvariant();
                    if (Wide(term) || term == "default") return false;
                    if (inEntry && stringEntry) return false;
                    if (!inEntry) { entries++; inEntry = true; stringEntry = false; }
                    continue;
                default:
                    return false;
            }
        }
        return entries > 0 && inEntry;
    }

    private static bool WillChangeList(CssDeclarationNode node)
    {
        var expectIdent = true;
        var idents = 0;
        for (var i = 0; i < node.Values.Count; i++)
        {
            if (node.Values[i] is not CssTokenComponent { Token: var token }) return false;
            if (token.Kind == CssSyntaxTokenKind.Whitespace) continue;
            if (expectIdent)
            {
                if (token.Kind != CssSyntaxTokenKind.Ident) return false;
                var term = token.Value.ToLowerInvariant();
                // auto stands alone; CSS-wide keywords, none and will-change are excluded idents.
                if (term is "will-change" or "none" || Wide(term) || (term == "auto" && idents > 0)) return false;
                if (idents > 0 && FirstIdent(node) == "auto") return false;
                idents++;
            }
            else if (token.Kind != CssSyntaxTokenKind.Comma) return false;
            expectIdent = !expectIdent;
        }
        return idents > 0 && !expectIdent;
    }

    private static bool IsTransition(string name) => name.StartsWith("transition", StringComparison.Ordinal);

    private static bool IsAnimation(string name) => name.StartsWith("animation", StringComparison.Ordinal);

    private static string AnimationForm(string name) => name switch
    {
        "animation" => "none or a comma-separated list of animations (name, times, easing, count, direction, fill mode, play state)",
        "animation-name" => "a comma-separated list of none or keyframes names",
        "animation-duration" => "a comma-separated list of auto or non-negative times",
        "animation-delay" => "a comma-separated list of times",
        "animation-timing-function" => "a comma-separated list of easing functions",
        "animation-iteration-count" => "a comma-separated list of infinite or non-negative numbers",
        "animation-direction" => "a comma-separated list of normal, reverse, alternate or alternate-reverse",
        "animation-fill-mode" => "a comma-separated list of none, forwards, backwards or both",
        _ => "a comma-separated list of running or paused"
    };

    // CSS Animations 1: each comma-separated layer of a longhand is one value; a shorthand layer
    // has at most one each of name, easing, iteration count, direction, fill mode and play state
    // and two times (duration then delay). CSS-wide keywords are accepted only as the whole value.
    private static bool AnimationList(string name, CssDeclarationNode node)
    {
        int components = 0, names = 0, times = 0, easings = 0, counts = 0, directions = 0, fills = 0, states = 0;
        for (var i = 0; i <= node.Values.Count; i++)
        {
            var component = i < node.Values.Count ? node.Values[i] : null;
            if (component is CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Whitespace }) continue;
            if (component is null or CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Comma })
            {
                if (components == 0 || (name != "animation" && components != 1)) return false;
                if (component is null) break;
                components = names = times = easings = counts = directions = fills = states = 0;
                continue;
            }
            components++;
            if (component is CssFunctionComponent function)
            {
                if (name is not ("animation" or "animation-timing-function") || !EasingFunction(function)
                    || ++easings > 1) return false;
                continue;
            }
            if (component is not CssTokenComponent { Token: var token }) return false;
            switch (token.Kind)
            {
                case CssSyntaxTokenKind.Dimension:
                    if (!(name switch
                        {
                            "animation" => Time(token, nonNegative: times == 0),
                            "animation-duration" => Time(token, nonNegative: true),
                            "animation-delay" => Time(token, nonNegative: false),
                            _ => false
                        }) || ++times > 2) return false;
                    continue;
                case CssSyntaxTokenKind.Number:
                    if (name is not ("animation" or "animation-iteration-count")
                        || !double.TryParse(token.Value, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out var iterations)
                        || !double.IsFinite(iterations) || iterations < 0 || ++counts > 1) return false;
                    continue;
                case CssSyntaxTokenKind.String:
                    // A keyframes name may be a string (animation-name:"slide").
                    if (name is not ("animation" or "animation-name") || token.Value.Length == 0 || ++names > 1)
                        return false;
                    continue;
                case CssSyntaxTokenKind.Ident:
                    break;
                default:
                    return false;
            }
            var term = token.Value.ToLowerInvariant();
            if (Wide(term) || term is "revert" or "revert-layer" or "default") return false;
            // auto is the duration keyword (CSS Animations 2); in the shorthand it takes the duration slot.
            if (term == "auto" && name == "animation")
            {
                if (times > 0) return false;
                times = 1;
                continue;
            }
            var isEasing = EasingKeyword(term);
            var isCount = term == "infinite";
            var isDirection = term is "normal" or "reverse" or "alternate" or "alternate-reverse";
            var isFill = term is "none" or "forwards" or "backwards" or "both";
            var isState = term is "running" or "paused";
            var ok = name switch
            {
                "animation" => true,
                "animation-name" => true,
                "animation-duration" => term == "auto",
                "animation-timing-function" => isEasing,
                "animation-iteration-count" => isCount,
                "animation-direction" => isDirection,
                "animation-fill-mode" => isFill,
                "animation-play-state" => isState,
                _ => false
            };
            if (!ok) return false;
            if (name != "animation") continue;
            // In the shorthand a keyword is taken by the first slot it can fill; the name is
            // whatever identifier is left (CSS Animations 1, 'animation' shorthand).
            if (isEasing && easings == 0) easings++;
            else if (isCount && counts == 0) counts++;
            else if (isDirection && directions == 0) directions++;
            else if (isFill && fills == 0) fills++;
            else if (isState && states == 0) states++;
            else if (++names > 1) return false;
        }
        return true;
    }

    private static string TransitionForm(string name) => name switch
    {
        "transition" => "none or a comma-separated list of transitions (property, times, easing)",
        "transition-property" => "none or a comma-separated list of property names",
        "transition-duration" => "a comma-separated list of non-negative times",
        "transition-delay" => "a comma-separated list of times",
        "transition-timing-function" => "a comma-separated list of easing functions",
        _ => "a comma-separated list of normal or allow-discrete"
    };

    private static bool EasingKeyword(string term) =>
        term is "ease" or "linear" or "ease-in" or "ease-out" or "ease-in-out" or "step-start" or "step-end";

    // cubic-bezier(x1, y1, x2, y2) with x1 and x2 in [0, 1], or steps(n[, position]) with n >= 1.
    private static bool EasingFunction(CssFunctionComponent function)
    {
        var steps = function.Name.Equals("steps", StringComparison.OrdinalIgnoreCase);
        if (!steps && !function.Name.Equals("cubic-bezier", StringComparison.OrdinalIgnoreCase)) return false;
        CssSyntaxToken first = default, second = default;
        var count = 0;
        var expectValue = true;
        for (var i = 0; i < function.Values.Count; i++)
        {
            if (function.Values[i] is not CssTokenComponent { Token: var token }) return false;
            if (token.Kind == CssSyntaxTokenKind.Whitespace) continue;
            if (expectValue == (token.Kind == CssSyntaxTokenKind.Comma)) return false;
            expectValue = !expectValue;
            if (token.Kind == CssSyntaxTokenKind.Comma) continue;
            count++;
            if (count == 1) first = token;
            else if (count == 2) second = token;
            // cubic-bezier: four finite numbers, the x values (1st and 3rd) within [0, 1].
            if (!steps && (token.Kind != CssSyntaxTokenKind.Number || !double.TryParse(token.Value,
                    System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture,
                    out var number) || !double.IsFinite(number) || (count % 2 == 1 && number is < 0 or > 1)))
                return false;
        }
        if (expectValue) return false;
        if (!steps) return count == 4;
        // steps(n[, position]): n an integer >= 1; jump-none needs at least two steps.
        if (count is < 1 or > 2 || first.Kind != CssSyntaxTokenKind.Number || !first.IsInteger
            || !double.TryParse(first.Value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var stepCount) || stepCount < 1) return false;
        if (count == 1) return true;
        var position = second.Kind == CssSyntaxTokenKind.Ident ? second.Value.ToLowerInvariant() : "";
        return position is "jump-start" or "jump-end" or "jump-both" or "start" or "end"
            || (position == "jump-none" && stepCount >= 2);
    }

    private static bool Time(CssSyntaxToken token, bool nonNegative) =>
        token.Kind == CssSyntaxTokenKind.Dimension
        && (token.Unit?.ToLowerInvariant() is "s" or "ms")
        && double.TryParse(token.Value, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var time)
        && double.IsFinite(time) && (!nonNegative || time >= 0);

    // CSS Transitions 1/2: each comma-separated layer of a longhand is one value; a shorthand
    // layer has at most one property name, two times (duration then delay), one easing and one
    // behavior. none stands alone. CSS-wide keywords are accepted only as the whole value.
    private static bool TransitionList(string name, CssDeclarationNode node)
    {
        var layers = 1;
        int components = 0, properties = 0, times = 0, easings = 0, behaviors = 0;
        var sawNone = false;
        for (var i = 0; i <= node.Values.Count; i++)
        {
            var component = i < node.Values.Count ? node.Values[i] : null;
            if (component is CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Whitespace }) continue;
            if (component is null or CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Comma })
            {
                if (components == 0 || (name != "transition" && components != 1)) return false;
                if (component is null) break;
                layers++;
                components = properties = times = easings = behaviors = 0;
                continue;
            }
            components++;
            if (component is CssFunctionComponent function)
            {
                if (name is not ("transition" or "transition-timing-function") || !EasingFunction(function)
                    || ++easings > 1) return false;
                continue;
            }
            if (component is not CssTokenComponent { Token: var token }) return false;
            if (token.Kind == CssSyntaxTokenKind.Dimension)
            {
                var allowed = name switch
                {
                    "transition" => Time(token, nonNegative: times == 0),
                    "transition-duration" => Time(token, nonNegative: true),
                    "transition-delay" => Time(token, nonNegative: false),
                    _ => false
                };
                if (!allowed || ++times > 2) return false;
                continue;
            }
            if (token.Kind != CssSyntaxTokenKind.Ident) return false;
            var term = token.Value.ToLowerInvariant();
            // CSS-wide keywords stand alone (handled before this list), and default is reserved.
            if (Wide(term) || term is "revert" or "revert-layer" or "default") return false;
            var isEasing = EasingKeyword(term);
            var isBehavior = term is "normal" or "allow-discrete";
            var ok = name switch
            {
                "transition-timing-function" => isEasing,
                "transition-behavior" => isBehavior,
                "transition-property" or "transition" => true,
                _ => false
            };
            if (!ok) return false;
            if (name == "transition" && isEasing) { if (++easings > 1) return false; }
            else if (name == "transition" && isBehavior) { if (++behaviors > 1) return false; }
            else if (name is "transition" or "transition-property")
            {
                if (++properties > 1) return false;
                if (term == "none") sawNone = true;
            }
        }
        // none is valid only as the single transitioned property of a single layer.
        return !sawNone || layers == 1;
    }

    private static string? FirstIdent(CssDeclarationNode node)
    {
        for (var i = 0; i < node.Values.Count; i++)
            if (node.Values[i] is CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.Ident } token })
                return token.Value.ToLowerInvariant();
        return null;
    }

    // opacity: 1 or 100% (or any spelling of them, such as 1.0 or 100.00%).
    private static bool OpaqueAlpha(CssSyntaxToken token) =>
        (token.Kind == CssSyntaxTokenKind.Number || token.Kind == CssSyntaxTokenKind.Percentage)
        && double.TryParse(token.Value, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var alpha)
        && alpha == (token.Kind == CssSyntaxTokenKind.Number ? 1 : 100);

    private static CssProperty Property(string name, int offset) => name switch
    {
        "font-size" => CssProperty.FontSize, "color" => CssProperty.Color,
        "font-weight" => CssProperty.FontWeight, "font-style" => CssProperty.FontStyle,
        "line-height" => CssProperty.LineHeight, "text-align" => CssProperty.TextAlign,
        "text-align-last" => CssProperty.TextAlignLast,
        "border-collapse" => CssProperty.BorderCollapse,
        "vertical-align" => CssProperty.VerticalAlign,
        "white-space" => CssProperty.WhiteSpace, "overflow-wrap" => CssProperty.OverflowWrap,
        "word-break" => CssProperty.WordBreak, "text-indent" => CssProperty.TextIndent,
        "letter-spacing" => CssProperty.LetterSpacing, "word-spacing" => CssProperty.WordSpacing,
        "text-transform" => CssProperty.TextTransform,
        "display" => CssProperty.Display,
        "list-style-type" or "list-style" => CssProperty.ListStyleType,
        "list-style-position" => CssProperty.ListStylePosition,
        "text-decoration" or "text-decoration-line" => CssProperty.TextDecoration,
        "text-decoration-color" => CssProperty.TextDecorationColor,
        "text-decoration-style" => CssProperty.TextDecorationStyle,
        "text-decoration-thickness" => CssProperty.TextDecorationThickness,
        "text-underline-offset" => CssProperty.TextUnderlineOffset,
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
        "break-inside" => CssProperty.BreakInside,
        "width" => CssProperty.Width, "min-width" => CssProperty.MinWidth, "max-width" => CssProperty.MaxWidth,
        "min-height" => CssProperty.MinHeight, "height" => CssProperty.Height,
        "max-height" => CssProperty.MaxHeight,
        "box-sizing" => CssProperty.BoxSizing,
        "outline" or "outline-width" => CssProperty.OutlineWidth,
        "outline-style" => CssProperty.OutlineStyle,
        "outline-color" => CssProperty.OutlineColor,
        "outline-offset" => CssProperty.OutlineOffset,
        "padding" or "padding-top" or "padding-block" or "padding-block-start" => CssProperty.PaddingTop,
        "padding-right" or "padding-inline-end" => CssProperty.PaddingRight,
        "padding-bottom" or "padding-block-end" => CssProperty.PaddingBottom,
        "padding-left" or "padding-inline" or "padding-inline-start" => CssProperty.PaddingLeft,
        "background" or "background-color" => CssProperty.BackgroundColor,
        "border-inline-width" or "border-inline-start-width" => CssProperty.BorderLeftWidth,
        "border-inline-end-width" => CssProperty.BorderRightWidth,
        "border-block-width" or "border-block-start-width" => CssProperty.BorderTopWidth,
        "border-block-end-width" => CssProperty.BorderBottomWidth,
        "border-inline-style" or "border-inline-start-style" => CssProperty.BorderLeftStyle,
        "border-inline-end-style" => CssProperty.BorderRightStyle,
        "border-block-style" or "border-block-start-style" => CssProperty.BorderTopStyle,
        "border-block-end-style" => CssProperty.BorderBottomStyle,
        "border-inline-color" or "border-inline-start-color" => CssProperty.BorderLeftColor,
        "border-inline-end-color" => CssProperty.BorderRightColor,
        "border-block-color" or "border-block-start-color" => CssProperty.BorderTopColor,
        "border-block-end-color" => CssProperty.BorderBottomColor,
        "border" or "border-top" or "border-width" or "border-top-width"
            or "border-inline" or "border-block" or "border-inline-start" or "border-inline-end"
            or "border-block-start" or "border-block-end" => CssProperty.BorderTopWidth,
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
    // Fail closed on unsupported units, arithmetic and nested functions: only bounded
    // rgb()/hsl() colours (with optional alpha) can be painted as PDF RGB.
    private static CssSyntaxComponent CanonicalColor(CssSyntaxComponent component)
    {
        if (component is not CssFunctionComponent function) return component;
        var name = function.Name.ToLowerInvariant();
        if (name is not ("rgb" or "rgba" or "hsl" or "hsla"))
            throw new FactsPdfException("FPDF1202",
                "Only rgb()/rgba() and bounded hsl()/hsla() colour functions are supported.", function.Span.Start);

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

        // CSS Color 4 makes rgba()/hsla() aliases of rgb()/hsl(). Both accept the
        // legacy comma form (three channels, optional comma alpha) and the modern
        // space form (three space-separated channels, optional "/ alpha"). Alpha
        // is clamped to [0, 1] and painted with PDF constant alpha.
        var commaForm = tokens.Count is 5 or 7;
        if (commaForm)
            for (var i = 1; i < tokens.Count; i += 2)
                commaForm &= tokens[i].Kind == CssSyntaxTokenKind.Comma;
        var spaceForm = !commaForm && tokens.Count is 3 or 5 && spaced[1] && spaced[2] &&
            (tokens.Count == 3 || tokens[3] is { Kind: CssSyntaxTokenKind.Delim, Value: "/" });
        if (!commaForm && !spaceForm)
            throw new FactsPdfException("FPDF1202", name is "hsl" or "hsla"
                    ? "hsl() needs hue and two percentages, comma- or space-separated, with optional alpha."
                    : "rgb() needs three same-unit channels, comma- or space-separated, with optional alpha.",
                function.Span.Start);
        CssSyntaxToken Channel(int index) => tokens[commaForm ? 2 * index : index];
        var alphaByte = tokens.Count > 3 + (commaForm ? 2 : 0) ? AlphaByte(tokens[^1]) : 255;
        // A translucent colour lowers to #rrggbbaa; an opaque one keeps the exact #rrggbb token.
        CssTokenComponent Lowered(string hex)
        {
            if (alphaByte != 255) hex += alphaByte.ToString("x2", System.Globalization.CultureInfo.InvariantCulture);
            return new CssTokenComponent(new CssSyntaxToken(CssSyntaxTokenKind.Hash, hex, "#" + hex, function.Span));
        }

        // Independently authored HSL report colours share the existing RGB
        // PDF paint path. The bounded subset accepts numbers or deg hue in
        // [-3600,3600] and saturation/lightness percentages. No colour-space or
        // blending fallback.
        if (name is "hsl" or "hsla")
        {
            var hueToken = Channel(0);
            // Degrees per unit (CSS Values <angle>); a plain number is degrees.
            var degreesPerUnit = hueToken.Kind == CssSyntaxTokenKind.Number ? 1m
                : hueToken.Kind != CssSyntaxTokenKind.Dimension ? 0m
                : hueToken.Unit?.ToLowerInvariant() switch
                {
                    "deg" => 1m,
                    "grad" => 0.9m,
                    "turn" => 360m,
                    "rad" => 180m / 3.1415926535897932384626433833m,
                    _ => 0m
                };
            if (degreesPerUnit == 0m)
                throw new FactsPdfException("FPDF1202",
                    "HSL hue must be a number or an angle in deg, grad, rad or turn.", hueToken.Span.Start);
            // Use decimal throughout HSL channel calculation. Binary double
            // cancellation at 5% / 95% turns exact .5 byte midpoints into
            // 25.499999... and silently changes the promised 8-bit PDF ink.
            if (!decimal.TryParse(hueToken.Value,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var hue) ||
                Math.Abs(hue) > 3600m / degreesPerUnit ||
                (hue *= degreesPerUnit) is < -3600m or > 3600m)
                throw new FactsPdfException("FPDF1202",
                    "HSL hue must be finite and between -3600 and 3600 degrees.", hueToken.Span.Start);
            decimal Percent(int index)
            {
                var channel = Channel(index);
                if (channel.Kind != CssSyntaxTokenKind.Percentage ||
                    !decimal.TryParse(channel.Value, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var value) ||
                    value is < 0m or > 100m)
                    throw new FactsPdfException("FPDF1202",
                        "HSL saturation and lightness must be percentages from 0% to 100%.",
                        channel.Span.Start);
                return value / 100m;
            }
            var saturation = Percent(1);
            var lightness = Percent(2);
            var normalizedHue = ((hue % 360m) + 360m) % 360m;
            var chroma = (1m - Math.Abs(2m * lightness - 1m)) * saturation;
            var sector = normalizedHue / 60m;
            var x = chroma * (1m - Math.Abs(sector % 2m - 1m));
            var m = lightness - chroma / 2m;
            (decimal red, decimal green, decimal blue) = sector switch
            {
                < 1m => (chroma, x, 0m),
                < 2m => (x, chroma, 0m),
                < 3m => (0m, chroma, x),
                < 4m => (0m, x, chroma),
                < 5m => (x, 0m, chroma),
                _ => (chroma, 0m, x)
            };
            static byte Byte(decimal channel) =>
                (byte)decimal.Round(channel * 255m, 0, MidpointRounding.AwayFromZero);
            return Lowered($"{Byte(red + m):x2}{Byte(green + m):x2}{Byte(blue + m):x2}");
        }

        // Accept uniform percent-channel RGB in controlled print colours.
        // Preserve integer-channel behaviour, but never mix number and percent
        // syntax; decimal rounding gives deterministic exact byte midpoints.
        var channels = new byte[3];
        var percentage = Channel(0).Kind == CssSyntaxTokenKind.Percentage;
        for (var i = 0; i < channels.Length; i++)
        {
            var token = Channel(i);
            if (percentage)
            {
                if (token.Kind != CssSyntaxTokenKind.Percentage ||
                    !decimal.TryParse(token.Value, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var value) ||
                    value is < 0m or > 100m)
                    throw new FactsPdfException("FPDF1202",
                        "RGB percentage channels must be uniformly from 0% to 100%.", token.Span.Start);
                channels[i] = (byte)decimal.Round(value * 255m / 100m, 0,
                    MidpointRounding.AwayFromZero);
            }
            else if (token.Kind != CssSyntaxTokenKind.Number || !token.IsInteger ||
                !byte.TryParse(token.Value, System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out channels[i]))
                throw new FactsPdfException("FPDF1202",
                    "RGB channels must be all integers 0–255 or all percentages 0–100%.", token.Span.Start);
        }
        return Lowered($"{channels[0]:x2}{channels[1]:x2}{channels[2]:x2}");
    }

    // CSS clamps colour alpha (a number or a percentage) to [0, 1]. Chrome paints it as one
    // byte, rounded in single precision exactly as Skia's SkPaint::getAlpha does, so the same
    // byte drives the PDF /ca and /CA values.
    private static int AlphaByte(CssSyntaxToken alpha)
    {
        if (alpha.Kind is not (CssSyntaxTokenKind.Number or CssSyntaxTokenKind.Percentage) ||
            !double.TryParse(alpha.Value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var value) || double.IsNaN(value))
            throw new FactsPdfException("FPDF1202",
                "Colour alpha must be a number or a percentage.", alpha.Span.Start);
        if (alpha.Kind == CssSyntaxTokenKind.Percentage) value /= 100;
        var clamped = (float)Math.Clamp(value, 0d, 1d);
        return (int)MathF.Floor(clamped * 255f + 0.5f);
    }

    // The declaration with colour functions lowered (see CanonicalColor); the node
    // itself when it has no function value.
    private static CssDeclarationNode CanonicalValues(CssDeclarationNode node)
    {
        var values = node.Values;
        for (var i = 0; i < values.Count; i++)
        {
            if (values[i] is not CssFunctionComponent) continue;
            var lowered = new CssSyntaxComponent[values.Count];
            for (var j = 0; j < lowered.Length; j++) lowered[j] = CanonicalColor(values[j]);
            return node with { Values = lowered };
        }
        return node;
    }

    private static CssTokenComponent[] NonWhitespaceTokens(IReadOnlyList<CssSyntaxComponent> values)
    {
        var count = 0;
        for (var i = 0; i < values.Count; i++)
            if (values[i] is CssTokenComponent { Token.Kind: not CssSyntaxTokenKind.Whitespace }) count++;
        if (count == 0) return [];
        var tokens = new CssTokenComponent[count];
        count = 0;
        for (var i = 0; i < values.Count; i++)
            if (values[i] is CssTokenComponent { Token.Kind: not CssSyntaxTokenKind.Whitespace } token) tokens[count++] = token;
        return tokens;
    }

    private static bool NoEscapes(CssSyntaxToken token) => !token.Raw.Contains('\\');

    private static List<string> Terms(CssDeclarationNode node)
    {
        // Sized to the non-whitespace components (each becomes one term or throws), and walked by
        // index so no array enumerator is boxed per declaration.
        var values = node.Values;
        var capacity = 0;
        for (var i = 0; i < values.Count; i++)
            if (values[i] is not CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Whitespace }) capacity++;
        var terms = new List<string>(capacity);
        var whitespace = true;
        for (var i = 0; i < values.Count; i++)
        {
            var component = values[i];
            if (component is CssTokenComponent x && x.Token.Kind == CssSyntaxTokenKind.Whitespace)
            { whitespace = true; continue; }
            if (!whitespace) throw new FactsPdfException("FPDF1202", "CSS terms require whitespace separators.", component.Span.Start);
            if (component is not CssTokenComponent t)
                throw new FactsPdfException("FPDF1202", "CSS functions/blocks are unsupported in box values.", component.Span.Start);
            var token = t.Token;
            var value = token.Kind switch
            {
                CssSyntaxTokenKind.Ident or CssSyntaxTokenKind.Number => token.Value,
                // Without escapes the source text is exactly the joined value, so reuse it.
                CssSyntaxTokenKind.Dimension => NoEscapes(token) ? token.Raw : token.Value + token.Unit,
                CssSyntaxTokenKind.Percentage => NoEscapes(token) ? token.Raw : token.Value + "%",
                CssSyntaxTokenKind.Hash => NoEscapes(token) ? token.Raw : "#" + token.Value,
                _ => throw new FactsPdfException("FPDF1202", "Unsupported CSS value token.", token.Span.Start)
            };
            terms.Add(value.ToLowerInvariant());
            whitespace = false;
        }
        return terms;
    }

    // The term a one-to-four value shorthand gives side i (TRBL), as CSS expands it.
    private static int SideSource(int count, int side) => count switch
    {
        1 => 0,
        2 => side % 2,
        3 => side == 3 ? 1 : side,
        _ => side
    };

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
            if (!InlineCss.KeywordFontSize(token, ValidationStyle.FontSize).HasValue
                && !InlineCss.RootRelativeScale(token, offset, 144).HasValue
                && !InlineCss.RelativeFontSizeScale(token, offset).HasValue)
                _ = InlineCss.Apply(ValidationStyle with { }, name, token,
                    paragraph: true, inline: false, offset);
        }
        else if (name == "line-height")
        {
            // A valid relative value may fit a computed 30pt element but not
            // the static 12pt validation style. Defer computed-height bounds
            // to the winning declaration, as for relative font-size.
            if (!InlineCss.RootRelativeScale(token, offset, 1440).HasValue
                && !InlineCss.RelativeLineHeightScale(token, offset).HasValue)
                _ = InlineCss.Apply(ValidationStyle with { }, name, token,
                    paragraph: true, inline: false, offset);
        }
        else if (name is ("text-indent" or "letter-spacing" or "word-spacing")
            && token.EndsWith("em", StringComparison.Ordinal))
        {
            // Check syntax even for an unmatched selector, but resolve the
            // final font and bounded glyph advance only for the CSS winner.
            _ = CssBoxValues.Length(token, offset, allowRem: true, allowEm: true);
        }
        else if (name == "text-decoration-thickness")
        {
            if (token != "auto")
            {
                var length = CssBoxValues.Length(token, offset, allowRem: true, allowEm: true);
                // Relative widths may change when a selector wins the cascade.
                // Absolute out-of-range values fail even in unmatched rules.
                var relative = token.EndsWith("em", StringComparison.Ordinal);
                // Never silently accept an unmatched relative length that cannot
                // become valid at any supported 1–144pt font size.
                if (length.IsPercent ||
                    (relative && (length.Value <= 0 || length.Value > 12 || length.Value * 144 < 0.2)) ||
                    (!relative && length.Value is < 0.2 or > 12))
                    throw new FactsPdfException("FPDF1202",
                        "Text-decoration thickness requires auto or a bounded positive length (0.2pt to 12pt after resolving em/rem).", offset);
            }
        }
        else if (name == "text-underline-offset")
        {
            if (token != "auto")
            {
                var length = CssBoxValues.Length(token, offset, allowRem: true, allowEm: true);
                // Validate unmatched rules as well as winners. Relative lengths
                // will be range-checked again against the computed font.
                if (length.IsPercent || length.Value > 24)
                    throw new FactsPdfException("FPDF1202",
                        "Text-underline-offset requires auto or a nonnegative 0–24pt physical/em/rem length.", offset);
            }
        }
        else if (name == "list-style-type")
            _ = ListMarkerTypes.Parse(token, offset);
        else if (name == "list-style-position")
        {
            if (token is not ("inside" or "outside"))
                throw new FactsPdfException("FPDF1202",
                    "list-style-position supports only inside or outside.", offset);
        }
        else if (name == "break-inside")
        {
            if (token is not ("auto" or "avoid"))
                throw new FactsPdfException("FPDF1202",
                    "Report break-inside accepts only auto or avoid.", offset);
        }
        else if (name == "vertical-align")
        {
            // baseline passes here; the cascade drops it outside cells and img/svg and rejects it
            // on td/th. text-top, text-bottom and lengths pass for inline img/svg; the cascade
            // rejects them on td/th.
            if (!InlineVerticalAlign.TryParse(token, out _))
                throw new FactsPdfException("FPDF1202",
                    "vertical-align supports baseline, middle, top, bottom, text-top, text-bottom, lengths and percentages (table cells: top, middle and bottom).", offset);
        }
        else if (name == "outline-width") _ = CssPaintValues.OutlineWidth(token, offset);
        else if (name == "outline-offset") _ = CssPaintValues.OutlineOffset(token, offset);
        else if (name == "outline-style") _ = CssPaintValues.BorderStyle(token, offset);
        else if (name == "outline-color") _ = CssPaintValues.BorderColor(token, offset);
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
        else if (name == "max-height")
        {
            if (token != "none")
            {
                var length = CssBoxValues.Length(token, offset, allowRem: true, allowEm: true);
                if (length.IsPercent)
                    throw new FactsPdfException("FPDF1202",
                        "Image max-height requires a bounded physical length or none.", offset);
            }
        }
        else if (name == "height")
        {
            if (token != "auto")
            {
                var length = CssBoxValues.Length(token, offset, allowRem: true, allowEm: true);
                if (length.IsPercent)
                    throw new FactsPdfException("FPDF1202",
                        "Height accepts only auto or a bounded physical length (em/rem are panel-only).", offset);
            }
        }
        else if (name is "margin-left" or "margin-right")
        {
            if (token != "auto") _ = CssBoxValues.Length(token, offset, allowRem: true, allowEm: true);
        }
        else if (name is "margin-top" or "margin-bottom")
            _ = CssBoxValues.Length(token, offset, allowRem: true, allowEm: true);
        else if (name is "padding" or "padding-top" or "padding-right" or "padding-bottom" or "padding-left")
            _ = CssBoxValues.Length(token, offset, allowRem: true, allowEm: true);
        else if (name is "background" or "background-color") _ = CssPaintValues.Background(token, offset);
        else if (name == "border-collapse")
        {
            if (token is not ("separate" or "collapse"))
                throw new FactsPdfException("FPDF1202", "border-collapse supports only separate and collapse.", offset);
        }
        else if (name.Contains("border", StringComparison.Ordinal))
        {
            if (name.EndsWith("width", StringComparison.Ordinal))
                _ = CssPaintValues.BorderWidth(token, offset, allowRelative: true);
            else if (name.EndsWith("style", StringComparison.Ordinal)) _ = CssPaintValues.EdgeStyle(token, offset);
            else if (name.EndsWith("color", StringComparison.Ordinal)) _ = CssPaintValues.EdgeColor(token, offset);
            else throw new InvalidOperationException("Shorthand validation should be handled separately.");
        }
        else
            _ = InlineCss.Apply(ValidationStyle with { }, name, token,
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
            if (item is CssBlockComponent { Opening: CssSyntaxTokenKind.OpenSquare } square)
            {
                // CSS Syntax groups square brackets into a simple block.
                // Flatten only atomic attribute terms, preserving decoded strings.
                tokens.Add(new(CssTokenKind.Symbol, "[", space, square.Span.Start));
                var insideSpace = false;
                foreach (var component in square.Values)
                {
                    if (component is not CssTokenComponent value)
                        throw new FactsPdfException("FPDF1203",
                            "Nested attribute selector components are unsupported.", component.Span.Start);
                    var attributeToken = value.Token;
                    if (attributeToken.Kind == CssSyntaxTokenKind.Whitespace) { insideSpace = true; continue; }
                    var attributeKind = attributeToken.Kind switch
                    {
                        CssSyntaxTokenKind.Ident => CssTokenKind.Identifier,
                        CssSyntaxTokenKind.String => CssTokenKind.String,
                        CssSyntaxTokenKind.Delim => CssTokenKind.Symbol,
                        _ => throw new FactsPdfException("FPDF1203",
                            "Unsupported attribute selector token.", attributeToken.Span.Start)
                    };
                    tokens.Add(new(attributeKind, attributeToken.Kind is CssSyntaxTokenKind.Ident or CssSyntaxTokenKind.String
                        ? attributeToken.Value : attributeToken.Raw, insideSpace, attributeToken.Span.Start));
                    insideSpace = false;
                }
                tokens.Add(new(CssTokenKind.Symbol, "]", false,
                    square.Span.Start + square.Span.Length - 1));
                space = false;
                continue;
            }
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
