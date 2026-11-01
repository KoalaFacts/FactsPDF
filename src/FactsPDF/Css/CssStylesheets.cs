using FactsPDF.CssSyntax;

namespace FactsPDF;

/// <summary>Per-conversion compiled rules. No static mutable cache, network access or public DOM dependency.</summary>
internal sealed class CssStylesheets
{
    private sealed record Rule(CssSelector Selector, CssDeclaration[] Declarations);
    private readonly record struct Winner(CssDeclaration Declaration, bool Inline, CssSpecificity Specificity)
    {
        internal bool Beats(Winner other)
        {
            var c = Declaration.Important.CompareTo(other.Declaration.Important); if (c != 0) return c > 0;
            c = Inline.CompareTo(other.Inline); if (c != 0) return c > 0;
            c = Specificity.CompareTo(other.Specificity); return c != 0 ? c > 0 : Declaration.Order >= other.Declaration.Order;
        }
    }
    private readonly Dictionary<string, List<Rule>> ids = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<Rule>> classes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<Rule>> types = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Rule> universal = [];
    private readonly Dictionary<int, CssDeclaration[]> inline = [];
    private readonly Winner?[] winners = new Winner?[(int)CssProperty.BorderBottomLeftRadius + 1];
    private readonly CssBudget budget;
    private readonly CssSyntaxLimits syntaxLimits;

    private CssStylesheets(PdfOptions options, CancellationToken token)
    {
        if (options.MaxCssCharacters < 1 || options.MaxCssSelectors < 1 || options.MaxCssDeclarations < 1 || options.MaxCssMatchOperations < 1)
            throw new ArgumentOutOfRangeException(nameof(options), "CSS budgets must be positive.");
        budget = new(options, token);
        syntaxLimits = new(options.MaxCssCharacters, options.MaxCssSyntaxNodes, options.MaxCssSyntaxDepth, token);
    }
    internal static CssStylesheets Collect(string html, PdfOptions options, CancellationToken token)
    {
        var sheet = new CssStylesheets(options, token); var elements = 0;
        foreach (var item in HtmlTokens.Read(html, token))
        {
            token.ThrowIfCancellationRequested();
            if (item.Kind == HtmlTokenKind.StyleText)
            {
                sheet.budget.Characters(item.Value.Length, item.Offset);
                sheet.ParseRules(sheet.ParseSource(item.Value, item.Offset, declarationsOnly: false).Rules);
            }
            else if (item.Kind == HtmlTokenKind.Start)
            {
                if (++elements > options.MaxElements) throw new FactsPdfException("FPDF1002", "Element limit exceeded.", item.Offset);
                if (item.Value == "style") ValidateStyleElement(item);
                else if (item.Attributes!.TryGetValue("style", out var css))
                {
                    sheet.budget.Characters(css.Length, item.Offset);
                    var origins = item.AttributeSourceOffsets is not null &&
                        item.AttributeSourceOffsets.TryGetValue("style", out var found) ? found : null;
                    var declarations = sheet.ParseSource(css, item.Offset, declarationsOnly: true, origins).Declarations;
                    sheet.inline.Add(item.Offset, CssSyntaxAdapter.CompileKnownDeclarations(declarations, sheet.budget));
                }
            }
        }
        return sheet;
    }
    private static void ValidateStyleElement(HtmlToken token)
    {
        if (token.SelfClosing) throw new FactsPdfException("FPDF1101", "style requires an HTML end tag.", token.Offset);
        foreach (var (name, value) in token.Attributes!)
        {
            if (name == "type")
            {
                if (value.Length != 0 && !value.Equals("text/css", StringComparison.OrdinalIgnoreCase))
                    throw new FactsPdfException("FPDF1204", "Only text/css style elements are supported.", token.Offset);
            }
            else if (name == "media")
            {
                var media = value.Trim().ToLowerInvariant();
                if (media is not ("" or "all" or "print")) throw new FactsPdfException("FPDF1204", "Only empty/all/print style media are supported.", token.Offset);
            }
            else if (!(name is "id" or "class" or "lang" or "title" || name.StartsWith("data-", StringComparison.Ordinal)))
                throw new FactsPdfException("FPDF1103", $"Attribute '{name}' is not supported on style.", token.Offset);
        }
    }
    private CssSyntaxResult ParseSource(string css, int offset, bool declarationsOnly,
        IReadOnlyList<int>? originalOffsets = null)
    {
        // The standard tokenizer records malformed comments with original
        // UTF-16 spans, including decoded HTML style-attribute origins.
        try
        {
            var source = CssSourceText.Create(css, offset, syntaxLimits, originalOffsets);
            var result = declarationsOnly
                ? CssSyntaxParser.ParseDeclarations(source, syntaxLimits)
                : CssSyntaxParser.ParseStylesheet(source, syntaxLimits);
            CssSyntaxAdapter.ThrowIfRecovered(result);
            return result;
        }
        catch (CssSyntaxLimitException error)
        {
            throw new FactsPdfException("FPDF1205", error.Message, error.Span.Start);
        }
    }

    private void ParseRules(IReadOnlyList<CssRuleNode> rules)
    {
        foreach (var rule in rules)
        {
            budget.Token.ThrowIfCancellationRequested();
            if (rule is CssAtRuleNode)
                throw new FactsPdfException("FPDF1204", "CSS at-rules and imports are not supported.", rule.Span.Start);
            if (rule is not CssQualifiedRuleNode qualified)
                throw new FactsPdfException("FPDF1203", "Unsupported CSS rule syntax.", rule.Span.Start);
            var selectors = CssSyntaxAdapter.CompileSelectors(qualified.Prelude, budget, rule.Span.Start);
            if (qualified.Contents.Any(x => x is not CssDeclarationNode))
                throw new FactsPdfException("FPDF1203", "Nested CSS rules are not supported by the PDF renderer.", rule.Span.Start);
            var declarations = CssSyntaxAdapter.CompileKnownDeclarations(
                qualified.Contents.OfType<CssDeclarationNode>().ToArray(), budget);
            foreach (var selector in selectors)
            {
                var entry = new Rule(selector, declarations);
                var last = selector.Rightmost;
                if (last.Ids.Length > 0) Add(ids, last.Ids[0], entry);
                else if (last.Classes.Length > 0) Add(classes, last.Classes[0], entry);
                else if (last.Type is not null) Add(types, last.Type, entry);
                else universal.Add(entry);
            }
        }
    }

    private static void Add(Dictionary<string, List<Rule>> index, string key, Rule rule)
    { if (!index.TryGetValue(key, out var list)) index.Add(key, list = []); list.Add(rule); }

    internal (TextStyle Text, BoxStyle Box) Compute(TextStyle defaults, TextStyle parent, TextStyle initial,
        double rootFontSize, BoxStyle parentBox, IReadOnlyList<CssElement> path,
        bool paragraph, bool block, int elementOffset, bool allowStyling = true)
    {
        Array.Clear(winners); var element = path[^1];
        var cellParagraph = element.Name == "p" && path.Any(e => e.Name is "td" or "th");
        void Offer(CssDeclaration declaration, bool isInline, CssSpecificity specificity)
        {
            budget.Work(declaration.Offset);
            if (!allowStyling) throw new FactsPdfException("FPDF1103", "Styling this void element is not supported.", elementOffset);
            // Only existing ordinary block-flow elements can be suppressed.
            // Lists/tables/images/inline/metadata have different layout and
            // resource contracts; their display declarations fail explicitly.
            if (declaration.Property == CssProperty.Display &&
                (element.Name is not ("p" or "pre" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6"
                    or "div" or "section" or "article" or "blockquote") ||
                 path.Any(e => e.Name is "ol" or "ul" or "li" or "table" or "tr"
                    or "td" or "th" or "thead" or "tbody" or "tfoot")))
                throw new FactsPdfException("FPDF1201",
                    "display is supported only on ordinary report blocks outside lists and tables.", declaration.Offset);
            // Rounded paint is deliberately scoped to ordinary report boxes, not
            // table/list/image geometry or inline text. Unsupported matches fail
            // even if a later cascade winner would remove the radius.
            if (declaration.Property is (CssProperty.BorderRadius or CssProperty.BorderTopLeftRadius
                or CssProperty.BorderTopRightRadius or CssProperty.BorderBottomRightRadius
                or CssProperty.BorderBottomLeftRadius) &&
                (element.Name is not ("p" or "pre" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6"
                    or "div" or "section" or "article" or "blockquote") ||
                 path.Any(e => e.Name is "ol" or "ul" or "li" or "table" or "tr"
                     or "td" or "th" or "thead" or "tbody" or "tfoot")))
                throw new FactsPdfException("FPDF1201",
                    "border-radius is supported only on ordinary report boxes outside lists and tables.", declaration.Offset);
            // Minimum heights consume real vertical layout and are confined
            // to ordinary report panels, including declarations that lose cascade.
            if (declaration.Property == CssProperty.MinHeight &&
                (element.Name is not ("div" or "section" or "article" or "blockquote") ||
                 path.Any(e => e.Name is "ol" or "ul" or "li" or "table" or "tr"
                     or "td" or "th" or "thead" or "tbody" or "tfoot")))
                throw new FactsPdfException("FPDF1201",
                    "min-height is supported only on ordinary report panels outside lists and tables.", declaration.Offset);
            if (declaration.Property == CssProperty.ListStyleType &&
                element.Name is not ("ol" or "ul" or "li"))
                throw new FactsPdfException("FPDF1201",
                    "list-style-type is supported only on ol, ul and li.", declaration.Offset);
            if (declaration.Property == CssProperty.VerticalAlign && element.Name is not ("td" or "th"))
                throw new FactsPdfException("FPDF1201",
                    "vertical-align applies only to fixed-column table td/th cells.", declaration.Offset);
            if (element.Name == "col" && declaration.Property != CssProperty.Width
                || element.Name is "td" or "th" && declaration.Property == CssProperty.Width)
                throw new FactsPdfException("FPDF1201", $"CSS property '{declaration.Name}' is not supported on this table element.", declaration.Offset);
            // hr has physical box geometry but no text. Its local font-size
            // can scale em lengths; colour supplies currentcolor borders.
            // Never pretend that text-only declarations rendered on a void rule.
            if (element.Name == "hr" &&
                declaration.Property is not (CssProperty.FontSize or CssProperty.Color
                    or CssProperty.MarginTop or CssProperty.MarginBottom
                    or CssProperty.MarginLeft or CssProperty.MarginRight) &&
                !CssDeclarations.IsBoxProperty(declaration.Property))
                throw new FactsPdfException("FPDF1201",
                    $"CSS property '{declaration.Name}' does not apply to horizontal rules.", declaration.Offset);
            // Flat table cell paragraphs do not have independent horizontal box
            // geometry. Preserve existing explicit zero sides, but auto can
            // consume real space and is not a zero-valued cell margin.
            if (cellParagraph && declaration.Property is (CssProperty.MarginTop or CssProperty.MarginBottom
                    or CssProperty.MarginLeft or CssProperty.MarginRight)
                && declaration.Value.EndsWith("em", StringComparison.Ordinal))
                throw new FactsPdfException("FPDF1201",
                    "Relative em/rem margins are not supported inside table cells.", declaration.Offset);
            // Only flat table-cell paragraphs need zero-inset recognition.
            // Do not parse the general block's margin through this legacy
            // absolute-only helper before its relative-value winner can resolve.
            var zeroCellInset = cellParagraph
                && declaration.Property is (CssProperty.MarginLeft or CssProperty.MarginRight)
                && (declaration.Value is "inherit" or "initial" or "unset"
                    || declaration.Value != "auto"
                        && CssBoxValues.Length(declaration.Value, declaration.Offset).Value == 0);
            // Images and tables use separate width contracts and cannot honour
            // block border-box sizing. Never silently discard a declaration.
            // Table/standalone-image lengths have separate sizing and resource
            // contracts. Do not silently introduce rem/em into those paths.
            if (declaration.Value.EndsWith("em", StringComparison.Ordinal)
                && declaration.Property is (CssProperty.Width or CssProperty.MinWidth or CssProperty.MaxWidth
                    or CssProperty.PaddingTop or CssProperty.PaddingRight or CssProperty.PaddingBottom or CssProperty.PaddingLeft)
                && element.Name is ("img" or "table" or "col" or "td" or "th"
                    or "tr" or "thead" or "tbody" or "tfoot"))
                throw new FactsPdfException("FPDF1201",
                    "Relative em/rem report box lengths are supported only on ordinary block boxes.", declaration.Offset);
            if (element.Name == "img" && declaration.Property is (CssProperty.TextDecoration or CssProperty.TextDecorationColor or CssProperty.TextDecorationStyle))
                throw new FactsPdfException("FPDF1201", "Text decoration paint cannot be applied to standalone images.", declaration.Offset);
            if (declaration.Property == CssProperty.TextTransform && element.Name == "img")
                throw new FactsPdfException("FPDF1201", "text-transform cannot be applied to standalone images.", declaration.Offset);
            if (declaration.Property is CssProperty.BoxSizing or CssProperty.MinWidth or CssProperty.MaxWidth &&
                (element.Name is "img" or "table" or "col" or "td" or "th" or "tr" or "thead" or "tbody" or "tfoot"))
                throw new FactsPdfException("FPDF1201", "Box sizing constraints are supported only on ordinary block boxes.", declaration.Offset);
            if (cellParagraph && declaration.Property == CssProperty.TextIndent)
                throw new FactsPdfException("FPDF1201", "text-indent is not supported inside table cells.", declaration.Offset);
            if (cellParagraph && ((CssDeclarations.IsBoxProperty(declaration.Property) && !zeroCellInset)
                || declaration.Property is CssProperty.BreakBefore or CssProperty.BreakAfter))
                throw new FactsPdfException("FPDF1201", $"CSS property '{declaration.Name}' is not supported on table cell paragraphs.", declaration.Offset);
            // Inline spans cannot independently align paragraph lines.
            // Preserve legacy left/right values, but reject logical start/end
            // just like justify: accepting them would only change one text run.
            // Do not key this off BoxTag/block: <html> inherits text alignment.
            if (declaration.Property == CssProperty.TextAlign
                && declaration.Value is ("justify" or "start" or "end")
                && (element.Name is "span" or "b" or "strong" or "i" or "em"))
                throw new FactsPdfException("FPDF1201", "text-align:justify/start/end cannot be declared on inline text.", declaration.Offset);
            // Fail explicitly on unsupported applicability even if another declaration wins later.
            if (declaration.Property == CssProperty.BreakAfter && declaration.Value == "avoid" &&
                (!HeadingKeep.IsHeading(element.Name) || path.Any(e => e.Name is "ol" or "ul" or "li")))
                throw new FactsPdfException("FPDF1201", "break-after:avoid is supported only on headings outside lists.", declaration.Offset);
            // Report panels have real outside-the-border vertical margins.
            // Validate all matching declarations, even losing ones. Vertical
            // percentages and auto are not quietly reinterpreted as points;
            // CSS-wide inheritance/reset is resolved for the winner below.
            var panelMargin = element.Name is ("div" or "section" or "article" or "blockquote" or "hr")
                && declaration.Property is (CssProperty.MarginTop or CssProperty.MarginBottom
                    or CssProperty.MarginLeft or CssProperty.MarginRight);
            if (panelMargin && declaration.Property is (CssProperty.MarginTop or CssProperty.MarginBottom)
                && declaration.Value is not ("inherit" or "initial" or "unset"))
            {
                var value = CssBoxValues.Length(declaration.Value, declaration.Offset,
                    allowRem: true, allowEm: true);
                if (value.IsPercent)
                    throw new FactsPdfException("FPDF1202",
                        "Report-panel vertical margins do not support percentages.", declaration.Offset);
            }
            // Forced page breaks on a whole report section must open/close
            // at its box boundary, not be delayed until its first text leaf.
            // No break-inside, avoid for whole panels or list/table breaks.
            var panelPageBreak = declaration.Property is (CssProperty.BreakBefore or CssProperty.BreakAfter)
                && element.Name is ("div" or "section" or "article" or "blockquote")
                && !path.Any(e => e.Name is "ol" or "ul" or "li" or "table" or "tr"
                    or "td" or "th" or "thead" or "tbody" or "tfoot");
            if (panelPageBreak && declaration.Value == "avoid")
                throw new FactsPdfException("FPDF1202",
                    "Report-panel page breaks support auto/page, not avoid.", declaration.Offset);
            if (!paragraph && !panelMargin && !panelPageBreak && declaration.Property is (CssProperty.MarginTop or CssProperty.MarginBottom
                or CssProperty.MarginLeft or CssProperty.MarginRight or CssProperty.BreakBefore
                or CssProperty.BreakAfter or CssProperty.WhiteSpace or CssProperty.OverflowWrap or CssProperty.TextIndent))
                throw new FactsPdfException("FPDF1201", $"CSS property '{declaration.Name}' is only supported on paragraphs/headings or scoped report panels.", declaration.Offset);
            if (!block && CssDeclarations.IsBoxProperty(declaration.Property))
                throw new FactsPdfException("FPDF1201", $"CSS box property '{declaration.Name}' is only supported on block elements.", declaration.Offset);
            var candidate = new Winner(declaration, isInline, specificity); var old = winners[(int)declaration.Property];
            if (!old.HasValue || candidate.Beats(old.Value)) winners[(int)declaration.Property] = candidate;
        }
        void Match(List<Rule> rules)
        {
            foreach (var rule in rules)
            {
                budget.Work(rule.Selector.Offset);
                if (!rule.Selector.Matches(path, budget)) continue;
                foreach (var declaration in rule.Declarations) Offer(declaration, false, rule.Selector.Specificity);
            }
        }
        Match(universal);
        if (types.TryGetValue(element.Name, out var typeRules)) Match(typeRules);
        if (element.Id is not null && ids.TryGetValue(element.Id, out var idRules)) Match(idRules);
        foreach (var name in element.Classes) if (classes.TryGetValue(name, out var classRules)) Match(classRules);
        if (inline.TryGetValue(elementOffset, out var declarations)) foreach (var d in declarations) Offer(d, true, default);
        var style = defaults;
        // Bounded report defaults. A quote is a real inset block rather
        // than an ignored tag; author CSS overrides each side normally.
        // CSS-wide initial resets to the zero-margin ordinary box initial.
        var box = element.Name switch
        {
            "hr" => new BoxStyle
            {
                BorderTop = new BorderEdge(1, true, null),
                MarginTop = new CssLength(8),
                MarginBottom = new CssLength(8)
            },
            "blockquote" => new BoxStyle
            {
                MarginTop = new CssLength(8),
                MarginBottom = new CssLength(8),
                MarginLeft = new CssLength(24),
                MarginRight = new CssLength(24)
            },
            _ => new BoxStyle()
        };
        // UA list defaults are decimal for ol and disc for ul. Other elements
        // propagate the inherited marker type, including li and nested panels.
        box = box with { MarkerKind = element.Name switch
        {
            "ol" => ListMarkerKind.Decimal,
            "ul" => ListMarkerKind.Disc,
            _ => parentBox.MarkerKind
        } };
        foreach (var winner in winners)
        {
            if (!winner.HasValue) continue;
            var declaration = winner.Value.Declaration;
            if (declaration.Property == CssProperty.Display)
                box = box with { DisplayNone = declaration.Value == "none" };
            else if (declaration.Property == CssProperty.ListStyleType)
                box = box with { MarkerKind = declaration.Value switch
                {
                    "inherit" or "unset" => parentBox.MarkerKind,
                    "initial" => ListMarkerKind.Disc,
                    _ => ListMarkerTypes.Parse(declaration.Value, declaration.Offset)
                } };
            else if (declaration.Property == CssProperty.VerticalAlign)
                box = box with { VerticalAlign = declaration.Value switch
                {
                    "inherit" => parentBox.VerticalAlign,
                    "initial" or "unset" or "top" => CellVerticalAlign.Top,
                    "middle" => CellVerticalAlign.Middle,
                    "bottom" => CellVerticalAlign.Bottom,
                    _ => throw new InvalidOperationException("Unvalidated table-cell vertical alignment.")
                } };
            else if (element.Name is ("div" or "section" or "article" or "blockquote")
                && declaration.Property is (CssProperty.BreakBefore or CssProperty.BreakAfter))
            {
                var value = declaration.Value switch
                {
                    "page" => true,
                    "auto" or "initial" or "unset" => false,
                    "inherit" => declaration.Property == CssProperty.BreakBefore
                        ? parentBox.BreakBefore : parentBox.BreakAfter,
                    _ => throw new InvalidOperationException("Unvalidated report-panel page break.")
                };
                box = declaration.Property == CssProperty.BreakBefore
                    ? box with { BreakBefore = value }
                    : box with { BreakAfter = value };
            }
            else if (CssDeclarations.IsBoxProperty(declaration.Property))
                box = CssDeclarations.ApplyBox(box, parentBox, declaration, rootFontSize, style.FontSize);
            else if (element.Name is ("div" or "section" or "article" or "blockquote" or "hr")
                && declaration.Property is (CssProperty.MarginTop or CssProperty.MarginBottom))
                box = CssDeclarations.ApplyPanelVerticalMargin(box, parentBox, declaration,
                    rootFontSize, style.FontSize);
            else
            {
                // html font-size:rem refers to the initial size; other rem
                // text metrics use the newly computed root font on html.
                // FontSize precedes these properties in the fixed enum.
                var computedRoot = element.Name == "html"
                    && declaration.Property is (CssProperty.LineHeight
                        or CssProperty.LetterSpacing or CssProperty.WordSpacing)
                    ? style.FontSize : rootFontSize;
                // Explicit paragraph/heading margin:inherit must read the direct
                // panel's computed outside margin. Do not alter anonymous text
                // or the default (non-inherited) paragraph margins.
                var marginParent = paragraph && path.Count > 1 &&
                    path[^2].Name is ("div" or "section" or "article" or "blockquote") &&
                    declaration.Property is (CssProperty.MarginTop or CssProperty.MarginBottom)
                    ? parent with { MarginBefore = parentBox.MarginTop.Value,
                        MarginAfter = parentBox.MarginBottom.Value }
                    : parent;
                style = CssDeclarations.Apply(style, marginParent, initial, declaration,
                    paragraph, computedRoot);
            }
        }
        if (box.HasRoundedCorners)
        {
            // Nonzero corner radii may not silently flatten asymmetric or
            // patterned borders. The uniform ring has four independent curved
            // corners and is actually painted;
            // non-rounded legacy box strokes retain exactly their old bytes.
            var edges = new[] { box.BorderTop, box.BorderRight, box.BorderBottom, box.BorderLeft };
            // currentcolor is stored as null, but paints with the final
            // computed text colour. Compare real ink, not raw nullable tokens.
            var firstInk = edges[0].Color ?? style.Color;
            if (edges.Any(e => e.EffectiveWidth > 0) &&
                (edges.Any(e => e.EffectiveWidth <= 0 || e.Pattern != BorderPattern.Solid) ||
                 edges.Any(e => e.EffectiveWidth != edges[0].EffectiveWidth ||
                     (e.Color ?? style.Color) != firstInk)))
                throw new FactsPdfException("FPDF1201",
                    "Rounded report boxes require an equally thick, single-colour solid border on all four sides.",
                    winners[(int)CssProperty.BorderTopLeftRadius]?.Declaration.Offset
                     ?? winners[(int)CssProperty.BorderTopRightRadius]?.Declaration.Offset
                     ?? winners[(int)CssProperty.BorderBottomRightRadius]?.Declaration.Offset
                     ?? winners[(int)CssProperty.BorderBottomLeftRadius]?.Declaration.Offset
                     ?? elementOffset);
        }
        return (style, box);
    }
}
