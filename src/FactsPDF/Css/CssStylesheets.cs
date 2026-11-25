using FactsPDF.CssSyntax;

namespace FactsPDF;

/// <summary>
/// A style attribute's declarations as the cascade sees them. A fresh compile (<see cref="FirstOrder"/>
/// 0) is used as it is; a replayed one shares an earlier compile's declarations, seen at
/// <see cref="FirstOrder"/> + index and at each declaration's offset plus <see cref="OffsetShift"/>.
/// </summary>
internal readonly record struct InlineStyle(CssDeclaration[]? Declarations, int OffsetShift, int FirstOrder);

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
    // Style attribute texts already compiled in this conversion, with the value offset they were
    // compiled at. Bounded so a page of distinct styles keeps no more than a small table.
    private const int MaxCompiledStyles = 256, MaxCompiledStyleLength = 1_024;
    private readonly Dictionary<string, (CssDeclaration[] Declarations, int ValueOffset)> compiledStyles = new(StringComparer.Ordinal);
    // The views Compute offers a replayed style attribute through, one per declaration position,
    // retargeted for every element (see CssDeclaration.Slot). Nothing keeps them past Compute:
    // the box cache stores the shared compiled declarations.
    private readonly List<CssDeclaration.Slot> inlineSlots = [];
    // Allocate for every accepted CSS property; new bounded components must
    // not index past the last legacy enum member in Native AOT or managed builds.
    private readonly Winner?[] winners = new Winner?[(int)CssProperty.TextAlignLast + 1];
    // Boxes computed earlier in this conversion, keyed by everything their box declarations read
    // (see Compute). Bounded so a page of distinct styles keeps no more than a small table.
    private const int MaxComputedBoxes = 256;
    private readonly Dictionary<int, ComputedBox> computedBoxes = [];
    private sealed record ComputedBox(string Element, BoxStyle ParentBox, double RootFontSize, double FontSize,
        Rgb Color, CssDeclaration[] Writes, BoxStyle Box);
    private readonly CssBudget budget;
    private readonly CssSyntaxLimits syntaxLimits;

    private CssStylesheets(PdfOptions options, CancellationToken token)
    {
        if (options.MaxCssCharacters < 1 || options.MaxCssSelectors < 1 || options.MaxCssDeclarations < 1 || options.MaxCssMatchOperations < 1)
            throw new ArgumentOutOfRangeException(nameof(options), "CSS budgets must be positive.");
        budget = new(options, token);
        syntaxLimits = new(options.MaxCssCharacters, options.MaxCssSyntaxNodes, options.MaxCssSyntaxDepth, Cancellation: token);
    }
    /// <summary>
    /// Compiles every style element and style attribute, and appends each HTML token it reads to
    /// <paramref name="tokens"/> so the document reader walks the same tokens instead of tokenizing
    /// the page again. The list is complete only when this method returns.
    /// </summary>
    internal static CssStylesheets Collect(string html, PdfOptions options, List<HtmlToken> tokens, CancellationToken token)
    {
        var sheet = new CssStylesheets(options, token); var elements = 0;
        var depth = new HtmlDocumentReader.OpenElements(options.MaxDepth);
        // Sized once instead of doubling past the pooled capacity (each doubling copies every
        // reference so far). Within the element limit, so an input that fails it early pays for
        // no more slots than it may hold.
        tokens.EnsureCapacity((int)Math.Min(EstimateTokens(html), 3L * options.MaxElements + 2));
        foreach (var item in HtmlTokens.Read(html, token))
        {
            token.ThrowIfCancellationRequested();
            tokens.Add(item);
            if (item.Kind == HtmlTokenKind.End) depth.Read(item);
            if (item.Kind == HtmlTokenKind.StyleText)
            {
                sheet.budget.Characters(item.Value.Length, item.Offset);
                sheet.ParseRules(sheet.ParseSource(item.Value, item.Offset, declarationsOnly: false).Rules);
            }
            else if (item.Kind == HtmlTokenKind.Start)
            {
                if (++elements > options.MaxElements) throw new FactsPdfException("FPDF1002", "Element limit exceeded.", item.Offset);
                depth.Read(item);
                if (item.Value == "style") ValidateStyleElement(item);
                else if (item is StyledHtmlToken styled)
                {
                    var css = styled.Attributes!["style"];
                    sheet.budget.Characters(css.Length, item.Offset);
                    styled.InlineDeclarations = sheet.CompileStyleAttribute(css, item.StyleValueOffset, item.StyleOriginOffsets);
                }
            }
        }
        return sheet;
    }
    /// <summary>
    /// Compiles one style attribute. A page usually repeats the same few style texts (every cell
    /// of a table column), so a text compiled before is not tokenized, parsed and compiled again:
    /// its declarations are replayed with this attribute's offsets. Parsing and compiling depend
    /// only on the text (the syntax limits are per source, and without character references the
    /// page offset of every code unit is the value start plus a function of the text), so the
    /// replay produces the same declarations, charges the same declaration budget in the same
    /// order and fails at the same offsets. Values with character references keep their origin
    /// map and are always compiled.
    /// </summary>
    private InlineStyle CompileStyleAttribute(string css, int valueOffset, int[]? originOffsets)
    {
        if (originOffsets is null && compiledStyles.TryGetValue(css, out var compiled))
        {
            var declarations = compiled.Declarations;
            if (declarations.Length == 0) return new InlineStyle(declarations, 0, 0);
            var shift = valueOffset - compiled.ValueOffset; var firstOrder = 0;
            for (var i = 0; i < declarations.Length; i++)
            {
                budget.Token.ThrowIfCancellationRequested();
                var order = budget.Declaration(declarations[i].Offset + shift);
                if (i == 0) firstOrder = order;
            }
            return new InlineStyle(declarations, shift, firstOrder);
        }
        var nodes = ParseSource(css, valueOffset, declarationsOnly: true, originOffsets).Declarations;
        var output = CssSyntaxAdapter.CompileKnownDeclarations(nodes, budget);
        if (originOffsets is null && compiledStyles.Count < MaxCompiledStyles && css.Length <= MaxCompiledStyleLength)
            compiledStyles.Add(css, (output, valueOffset));
        return new InlineStyle(output, 0, 0);
    }
    /// <summary>
    /// The tokens <see cref="HtmlTokens.Read"/> produces for a page where tags and text alternate:
    /// one per <c>&lt;</c> plus one per text run (leading, or after a <c>&gt;</c> that another tag
    /// does not follow at once). Only a capacity hint: a <c>&gt;</c> inside a comment, an attribute
    /// value or a stylesheet over-counts by one.
    /// </summary>
    internal static long EstimateTokens(ReadOnlySpan<char> html)
    {
        if (html.IsEmpty) return 0;
        long count = html.Count('<') + (html[0] == '<' ? 0 : 1);
        for (var at = html.IndexOf('>'); at >= 0;)
        {
            if (at + 1 < html.Length && html[at + 1] != '<') count++;
            var next = html[(at + 1)..].IndexOf('>');
            at = next < 0 ? -1 : at + 1 + next;
        }
        return count;
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
            // A stylesheet's top-level qualified rules each compile at least one selector, so the
            // tokenizer fails a rule flood with the selector budget before tokenizing the rest; each
            // declaration of such a rule compiles at least one counted declaration, so a declaration
            // flood fails with the declaration budget the same way.
            var result = declarationsOnly
                ? CssSyntaxParser.ParseDeclarations(source, syntaxLimits)
                : CssSyntaxParser.ParseStylesheet(source, syntaxLimits with
                {
                    MaxQualifiedRules = budget.SelectorsLeft,
                    MaxDeclarations = budget.DeclarationsLeft,
                });
            CssSyntaxAdapter.ThrowIfRecovered(result);
            return result;
        }
        catch (CssSyntaxLimitException error)
        {
            throw new FactsPdfException("FPDF1205", error.Message, error.Span.Start);
        }
    }

    private static void ValidateKeyframes(CssAtRuleNode at)
    {
        CssSyntaxComponent? name = null;
        var count = 0;
        for (var i = 0; i < at.Prelude.Count; i++)
            if (at.Prelude[i] is not CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Whitespace })
            { name ??= at.Prelude[i]; count++; }
        var validName = count == 1 && name is CssTokenComponent { Token: var token }
            && ((token.Kind == CssSyntaxTokenKind.String && token.Value.Length > 0)
                || (token.Kind == CssSyntaxTokenKind.Ident && token.Value.ToLowerInvariant()
                    is not ("none" or "inherit" or "initial" or "unset" or "revert" or "revert-layer" or "default")));
        if (at.Contents is null || !validName)
            throw new FactsPdfException("FPDF1204",
                "@keyframes needs one keyframes name and a block of keyframes.", at.Span.Start);
        for (var r = 0; r < at.Contents.Count; r++)
        {
            if (at.Contents[r] is not CssQualifiedRuleNode keyframe || !KeyframeSelector(keyframe.Prelude))
                throw new FactsPdfException("FPDF1204",
                    "@keyframes blocks contain only from, to or percentage keyframes.", at.Contents[r].Span.Start);
            for (var d = 0; d < keyframe.Contents.Count; d++)
                if (keyframe.Contents[d] is not CssDeclarationNode)
                    throw new FactsPdfException("FPDF1204",
                        "A keyframe contains only declarations.", keyframe.Contents[d].Span.Start);
        }
    }

    // from, to or a percentage from 0% to 100%, comma-separated.
    private static bool KeyframeSelector(IReadOnlyList<CssSyntaxComponent> prelude)
    {
        var expectSelector = true;
        for (var i = 0; i < prelude.Count; i++)
        {
            if (prelude[i] is not CssTokenComponent { Token: var token }) return false;
            if (token.Kind == CssSyntaxTokenKind.Whitespace) continue;
            if (expectSelector)
            {
                var ok = token.Kind switch
                {
                    CssSyntaxTokenKind.Ident => token.Value.ToLowerInvariant() is "from" or "to",
                    CssSyntaxTokenKind.Percentage => double.TryParse(token.Value,
                        System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture,
                        out var at) && at is >= 0 and <= 100,
                    _ => false
                };
                if (!ok) return false;
            }
            else if (token.Kind != CssSyntaxTokenKind.Comma) return false;
            expectSelector = !expectSelector;
        }
        return !expectSelector;
    }

    private void ParseRules(IReadOnlyList<CssRuleNode> rules)
    {
        foreach (var rule in rules)
        {
            budget.Token.ThrowIfCancellationRequested();
            if (rule is CssAtRuleNode at)
            {
                // Keyframes only drive on-screen animations; a written PDF page shows none of them,
                // so a well-formed @keyframes block is validated and dropped.
                if (at.Name.Equals("keyframes", StringComparison.OrdinalIgnoreCase)
                    || at.Name.Equals("-webkit-keyframes", StringComparison.OrdinalIgnoreCase))
                {
                    ValidateKeyframes(at);
                    continue;
                }
                // Bounded print-only conditionals: no device queries, nesting,
                // imports or ignored rules. Reuse the exact same selector,
                // declaration, cascade and resource budgets as top-level CSS.
                if (!at.Name.Equals("media", StringComparison.OrdinalIgnoreCase) || at.Contents is null)
                    throw new FactsPdfException("FPDF1204", "Only block-form @media print/all is supported.", rule.Span.Start);
                var prelude = at.Prelude
                    .Where(x => x is not CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Whitespace })
                    .ToArray();
                if (prelude.Length != 1 || prelude[0] is not CssTokenComponent
                    { Token.Kind: CssSyntaxTokenKind.Ident } medium ||
                    !(medium.Token.Value.Equals("print", StringComparison.OrdinalIgnoreCase)
                      || medium.Token.Value.Equals("all", StringComparison.OrdinalIgnoreCase)))
                    throw new FactsPdfException("FPDF1204", "Only a single print or all media type is supported.", rule.Span.Start);
                // A print stylesheet may carry @keyframes; ParseRules validates and drops them.
                if (at.Contents.Any(x => x is not CssQualifiedRuleNode && !(x is CssAtRuleNode nested
                        && (nested.Name.Equals("keyframes", StringComparison.OrdinalIgnoreCase)
                            || nested.Name.Equals("-webkit-keyframes", StringComparison.OrdinalIgnoreCase)))))
                    throw new FactsPdfException("FPDF1204",
                        "Nested media, other at-rules and loose declarations are unsupported.", rule.Span.Start);
                ParseRules(at.Contents.Cast<CssRuleNode>().ToArray());
                continue;
            }
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

    internal (TextStyle Text, BoxStyle Box) Compute(TextStyle defaults, bool defaultsOwned, TextStyle parent, TextStyle initial,
        double rootFontSize, BoxStyle parentBox, IReadOnlyList<CssElement> path,
        bool paragraph, bool block, int elementOffset, InlineStyle inlineDeclarations, bool allowStyling = true)
    {
        Array.Clear(winners); var element = path[^1];
        var cellParagraph = element.Name == "p" && path.Any(e => e.Name is "td" or "th");
        // Cell boxes inherit real first-line indent into anonymous text and flat paragraphs.
        var cellTextIndentHost = element.Name is "td" or "th";
        // These inherited text-flow controls can be authored on document or
        // ordinary report containers. Their computed state reaches the same
        // measured paragraph/heading renderer, not a separate box approximation.
        // Direct inline, list and table declarations remain unsupported.
        var reportTextFlowHost = element.Name is ("html" or "body" or "div" or "section" or "article" or "header" or "footer" or "main" or "nav" or "aside" or "blockquote" or "figure" or "figcaption" or "address" or "dl" or "dt" or "dd")
            && !path.Any(e => e.Name is "ol" or "ul" or "li" or "table" or "tr"
                or "td" or "th" or "thead" or "tbody" or "tfoot");
        void Offer(CssDeclaration declaration, bool isInline, CssSpecificity specificity)
        {
            budget.Work(declaration.Offset);
            // baseline is the initial value. Outside cells inline text (and a br or img) already
            // sits on its baseline and blocks ignore vertical-align, so resets such as
            // *{vertical-align:baseline} are validated and dropped, even on void elements.
            // A cell's baseline row alignment is not implemented (rejected below).
            if (declaration.Property == CssProperty.VerticalAlign && declaration.Value == "baseline"
                && element.Name is not ("td" or "th"))
                return;
            // A br has no width, height or children, so box-sizing (validated when parsed) changes
            // nothing there and *{box-sizing:border-box} does not fail on a line break.
            if (declaration.Property == CssProperty.BoxSizing && element.Name == "br")
                return;
            // <html> and <body> lay out with no margin of their own (the page margin options frame
            // the content), and <html> with no padding, so the resets body{margin:0},
            // html,body{margin:0;padding:0} and *{margin:0} change nothing there and are validated
            // and dropped. Inline text has no box here, so a zero margin or padding and a transparent
            // background (normalize.css a{background-color:transparent}, *{margin:0;padding:0}) are
            // dropped there too. Any value that would move or paint the content still fails below.
            if (RootZeroInset(element.Name, declaration) || InlineZeroBox(element.Name, declaration)
                || TablePartMargin(element.Name, declaration) || UnusedZeroInset(element.Name, declaration))
                return;
            if (!allowStyling) throw new FactsPdfException("FPDF1103", "Styling this void element is not supported.", elementOffset);
            // Only existing ordinary block-flow elements can be suppressed.
            // Lists/tables/images/inline/metadata have different layout and
            // resource contracts; their display declarations fail explicitly.
            if (declaration.Property == CssProperty.Display &&
                (element.Name is not ("p" or "pre" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6"
                    or "div" or "section" or "article" or "header" or "footer" or "main" or "nav" or "aside" or "blockquote" or "figure" or "figcaption" or "address" or "dl" or "dt" or "dd") ||
                 path.Any(e => e.Name is "ol" or "ul" or "li" or "table" or "tr"
                    or "td" or "th" or "thead" or "tbody" or "tfoot")))
                throw new FactsPdfException("FPDF1201",
                    "display is supported only on ordinary report blocks outside lists and tables.", declaration.Offset);
            // Outlines use report-panel paint geometry only, never table,
            // list, image or inline formatting contexts.
            if (declaration.Property is (CssProperty.OutlineWidth or CssProperty.OutlineStyle
                    or CssProperty.OutlineColor or CssProperty.OutlineOffset) &&
                (element.Name is not ("div" or "section" or "article" or "header" or "footer" or "main" or "nav" or "aside" or "blockquote" or "figure" or "figcaption" or "address" or "dl" or "dt" or "dd") ||
                 path.Any(e => e.Name is "ol" or "ul" or "li" or "table" or "tr"
                     or "td" or "th" or "thead" or "tbody" or "tfoot")))
            {
                // Resets such as a{outline:none}, img{outline:0} or button{outline:none}
                // remove an outline that is never painted outside report panels. Elsewhere
                // outline-style can only be none, so no outline exists to paint and every
                // already validated outline declaration that cannot make one is dropped.
                // A style that could paint (solid, dashed, dotted, inherit) still fails.
                if (declaration.Property != CssProperty.OutlineStyle
                    || declaration.Value is "none" or "initial" or "unset")
                    return;
                throw new FactsPdfException("FPDF1201",
                    "Outlines apply only to ordinary report panels outside lists and tables; elsewhere only outline:none (or outline-style:none) is accepted.",
                    declaration.Offset);
            }
            // Rounded paint is deliberately scoped to ordinary report boxes, not
            // table/list/image geometry or inline text. Unsupported matches fail
            // even if a later cascade winner would remove the radius.
            if (declaration.Property is (CssProperty.BorderRadius or CssProperty.BorderTopLeftRadius
                or CssProperty.BorderTopRightRadius or CssProperty.BorderBottomRightRadius
                or CssProperty.BorderBottomLeftRadius) &&
                (element.Name is not ("p" or "pre" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6"
                    or "div" or "section" or "article" or "header" or "footer" or "main" or "nav" or "aside" or "blockquote" or "figure" or "figcaption" or "address" or "dl" or "dt" or "dd") ||
                 path.Any(e => e.Name is "ol" or "ul" or "li" or "table" or "tr"
                     or "td" or "th" or "thead" or "tbody" or "tfoot")))
                throw new FactsPdfException("FPDF1201",
                    "border-radius is supported only on ordinary report boxes outside lists and tables.", declaration.Offset);
            // Panels consume their bounded min-height/height; direct JPEG/PNG
            // images additionally consume vertical limits in ImageLayout.Resolve.
            // Check every matching declaration, not only the cascade winner.
            if (declaration.Property is (CssProperty.MinHeight or CssProperty.Height or CssProperty.MaxHeight))
            {
                var imageHeight = element.Name == "img";
                var panelHeight = declaration.Property != CssProperty.MaxHeight
                    && element.Name is ("div" or "section" or "article" or "header" or "footer" or "main" or "nav" or "aside" or "blockquote" or "figure" or "figcaption" or "address" or "dl" or "dt" or "dd")
                    && !path.Any(e => e.Name is "ol" or "ul" or "li" or "table" or "tr"
                        or "td" or "th" or "thead" or "tbody" or "tfoot");
                if (!imageHeight && !panelHeight)
                    throw new FactsPdfException("FPDF1201",
                        "Height limits apply only to scoped report panels or direct images (max-height: images only).", declaration.Offset);
            }
            // A keep-together card must be an ordinary top-level report panel.
            // Validate losing declarations too; nested/table/list/inline usage
            // must never be accepted as an unimplemented decoration.
            if (declaration.Property == CssProperty.BreakInside &&
                (element.Name is not ("div" or "section" or "article" or "header" or "footer" or "main" or "nav" or "aside" or "blockquote" or "figure" or "figcaption" or "address" or "dl" or "dt" or "dd")
                 || path.Any(e => e.Name is "ol" or "ul" or "li" or "table" or "tr"
                     or "td" or "th" or "thead" or "tbody" or "tfoot")))
                throw new FactsPdfException("FPDF1201",
                    "break-inside is supported only on ordinary report panels outside lists/tables.", declaration.Offset);
            if ((declaration.Property is CssProperty.ListStyleType or CssProperty.ListStylePosition) &&
                element.Name is not ("ol" or "ul" or "li"))
                throw new FactsPdfException("FPDF1201",
                    "List marker properties are supported only on ol, ul and li.", declaration.Offset);
            if (declaration.Property == CssProperty.VerticalAlign)
            {
                if (element.Name is not ("td" or "th"))
                    throw new FactsPdfException("FPDF1201",
                        "vertical-align applies only to fixed-column table td/th cells (elsewhere only its initial value baseline is accepted).", declaration.Offset);
                if (declaration.Value == "baseline")
                    throw new FactsPdfException("FPDF1202",
                        "Table-cell vertical-align supports only top, middle and bottom.", declaration.Offset);
            }
            // A col takes only a track width; box-sizing changes nothing there (a col has no
            // padding or borders), so *{box-sizing:border-box} is accepted on it.
            if (element.Name == "col" && declaration.Property is not (CssProperty.Width or CssProperty.BoxSizing)
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
            // Fixed-column td/th cells resolve font-relative padding to a computed
            // length before TableLayout measures the track insets. Their own final
            // computed font size applies to em; html's root size applies to rem.
            // Tables use computed em/rem content widths and outside padding;
            // explicit col tracks use the inherited colgroup font size for em.
            // Both already reach TableLayout's strict track sums and geometry.
            // Row/group boxes and cell width constraints remain unsupported,
            // including losing declarations that would otherwise be ignored.
            if (declaration.Value.EndsWith("em", StringComparison.Ordinal)
                && declaration.Property is (CssProperty.Width or CssProperty.MinWidth or CssProperty.MaxWidth
                    or CssProperty.PaddingTop or CssProperty.PaddingRight or CssProperty.PaddingBottom or CssProperty.PaddingLeft)
                && (element.Name is ("tr" or "thead" or "tbody" or "tfoot")
                    || element.Name is ("td" or "th")
                        && declaration.Property is (CssProperty.Width or CssProperty.MinWidth or CssProperty.MaxWidth)))
                throw new FactsPdfException("FPDF1201",
                    "Relative em/rem box lengths are unsupported on table rows/groups and cell widths.", declaration.Offset);
            if (element.Name == "img" && declaration.Property is (CssProperty.TextDecoration or CssProperty.TextDecorationColor or CssProperty.TextDecorationStyle or CssProperty.TextDecorationThickness or CssProperty.TextUnderlineOffset))
                throw new FactsPdfException("FPDF1201", "Text decoration paint cannot be applied to standalone images.", declaration.Offset);
            if (declaration.Property == CssProperty.TextTransform && element.Name == "img")
                throw new FactsPdfException("FPDF1201", "text-transform cannot be applied to standalone images.", declaration.Offset);
            // Direct, explicitly bound images now consume these constraints through
            // their measured outer box, including direct image items in cells.
            // Table tracks and cell layouts remain separately controlled and
            // cannot silently accept sizing declarations they do not render.
            // box-sizing is supported on the table box itself (an explicit width then includes its
            // padding and borders); on columns, rows, groups and cells no width with padding or
            // borders is accepted, so it changes nothing there and universal resets pass.
            if (declaration.Property is CssProperty.MinWidth or CssProperty.MaxWidth &&
                (element.Name is "table" or "col" or "td" or "th" or "tr" or "thead" or "tbody" or "tfoot"))
                throw new FactsPdfException("FPDF1201", "Box sizing constraints are supported only on ordinary blocks and direct images.", declaration.Offset);
            if (cellParagraph && ((CssDeclarations.IsBoxProperty(declaration.Property) && !zeroCellInset
                    && declaration.Property != CssProperty.BoxSizing)
                || declaration.Property is CssProperty.BreakBefore or CssProperty.BreakAfter))
                throw new FactsPdfException("FPDF1201", $"CSS property '{declaration.Name}' is not supported on table cell paragraphs.", declaration.Offset);
            // Last-line alignment belongs to a paragraph's line box, never
            // a single inline run (including losing CSS declarations).
            if (declaration.Property == CssProperty.TextAlignLast &&
                HtmlDocumentReader.InlineTag(element.Name))
                throw new FactsPdfException("FPDF1201",
                    "text-align-last cannot be declared on inline text.", declaration.Offset);
            // Inline spans cannot independently align paragraph lines.
            // Preserve legacy left/right values, but reject logical start/end
            // just like justify: accepting them would only change one text run.
            // Do not key this off BoxTag/block: <html> inherits text alignment.
            if (declaration.Property == CssProperty.TextAlign
                && declaration.Value is ("justify" or "start" or "end")
                && HtmlDocumentReader.InlineTag(element.Name))
                throw new FactsPdfException("FPDF1201", "text-align:justify/start/end cannot be declared on inline text.", declaration.Offset);
            // Fail explicitly on unsupported applicability even if another declaration wins later.
            if (declaration.Property == CssProperty.BreakAfter && declaration.Value == "avoid" &&
                (!HeadingKeep.IsHeading(element.Name) || path.Any(e => e.Name is "ol" or "ul" or "li")))
                throw new FactsPdfException("FPDF1201", "break-after:avoid is supported only on headings outside lists.", declaration.Offset);
            // Report panels have real outside-the-border vertical margins.
            // Validate all matching declarations, even losing ones. Vertical
            // percentages and auto are not quietly reinterpreted as points;
            // Percentages are supported on report panels: their actual outside
            // spacing resolves against the containing block content WIDTH at
            // layout time, not the page height. Vertical auto still fails.
            // CSS-wide inheritance/reset is resolved for the winner below.
            var panelMargin = element.Name is ("div" or "section" or "article" or "header" or "footer" or "main" or "nav" or "aside" or "blockquote" or "figure" or "figcaption" or "address" or "dl" or "dt" or "dd" or "hr")
                && declaration.Property is (CssProperty.MarginTop or CssProperty.MarginBottom
                    or CssProperty.MarginLeft or CssProperty.MarginRight);
            if (panelMargin && declaration.Property is (CssProperty.MarginTop or CssProperty.MarginBottom)
                && declaration.Value.EndsWith('%') &&
                path.Any(e => e.Name is "ol" or "ul" or "li" or "table" or "tr" or "td" or "th"))
                throw new FactsPdfException("FPDF1201",
                    "Percentage report-panel margins are not supported inside lists or tables.", declaration.Offset);
            if (panelMargin && declaration.Property is (CssProperty.MarginTop or CssProperty.MarginBottom)
                && declaration.Value is not ("inherit" or "initial" or "unset"))
            {
                _ = CssBoxValues.Length(declaration.Value, declaration.Offset,
                    allowRem: true, allowEm: true);
            }
            // Vertical percentages on ordinary paragraphs use containing-block content width.
            // List and table flows have distinct geometry; reject even losing candidates.
            if (paragraph && declaration.Property is (CssProperty.MarginTop or CssProperty.MarginBottom)
                && declaration.Value.EndsWith('%') &&
                path.Any(e => e.Name is "ol" or "ul" or "li" or "table" or "tr" or "td" or "th"))
                throw new FactsPdfException("FPDF1201",
                    "Percentage paragraph margins are not supported inside lists or tables.", declaration.Offset);
            // Forced page breaks on a whole report section must open/close
            // at its box boundary, not be delayed until its first text leaf.
            // No break-inside, avoid for whole panels or list/table breaks.
            var panelPageBreak = declaration.Property is (CssProperty.BreakBefore or CssProperty.BreakAfter)
                && element.Name is ("div" or "section" or "article" or "header" or "footer" or "main" or "nav" or "aside" or "blockquote" or "figure" or "figcaption" or "address" or "dl" or "dt" or "dd")
                && !path.Any(e => e.Name is "ol" or "ul" or "li" or "table" or "tr"
                    or "td" or "th" or "thead" or "tbody" or "tfoot");
            if (panelPageBreak && declaration.Value == "avoid")
                throw new FactsPdfException("FPDF1202",
                    "Report-panel page breaks support auto/page, not avoid.", declaration.Offset);
            if (!paragraph && !panelMargin && !panelPageBreak && declaration.Property is (CssProperty.MarginTop or CssProperty.MarginBottom
                or CssProperty.MarginLeft or CssProperty.MarginRight or CssProperty.BreakBefore
                or CssProperty.BreakAfter or CssProperty.WhiteSpace or CssProperty.OverflowWrap or CssProperty.WordBreak or CssProperty.TextIndent)
                && !(reportTextFlowHost && declaration.Property is (CssProperty.WhiteSpace
                    or CssProperty.OverflowWrap or CssProperty.WordBreak or CssProperty.TextIndent))
                && !(cellTextIndentHost && declaration.Property is (CssProperty.WhiteSpace or CssProperty.OverflowWrap or CssProperty.WordBreak or CssProperty.TextIndent)))
                throw new FactsPdfException("FPDF1201", $"CSS property '{declaration.Name}' is only supported on paragraphs/headings or scoped report panels.", declaration.Offset);
            // box-sizing changes nothing on <html> (no width or height is accepted there) or on
            // non-replaced inline text (CSS Box Sizing: width and height do not apply), so the
            // universal reset *{box-sizing:border-box} is accepted there. It is still recorded on
            // the element's box so a child's box-sizing:inherit reads it, as in a browser.
            if (!block && CssDeclarations.IsBoxProperty(declaration.Property)
                && !(declaration.Property == CssProperty.BoxSizing && BoxSizingNoOpHost(element.Name)))
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
        foreach (var name in element.Classes.Names) if (classes.TryGetValue(name, out var classRules)) Match(classRules);
        if (inlineDeclarations.Declarations is { } inline)
        {
            if (inlineDeclarations.FirstOrder == 0) foreach (var d in inline) Offer(d, true, default);
            else
                for (var i = 0; i < inline.Length; i++)
                {
                    if (i == inlineSlots.Count) inlineSlots.Add(new CssDeclaration.Slot());
                    var d = inline[i];
                    Offer(inlineSlots[i].Retarget(d, inlineDeclarations.FirstOrder + i, d.Offset + inlineDeclarations.OffsetShift), true, default);
                }
        }
        // defaults is the shared parent style, a tag-defaults copy shared with earlier siblings, or
        // (defaultsOwned) a private copy the reader made for this element. Text declarations are
        // applied to one private copy, made on the first one if the reader did not make it; shared
        // styles are never assigned (see TextStyle).
        var style = defaults;
        var styleOwned = defaultsOwned;
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
            "blockquote" or "figure" => new BoxStyle
            {
                MarginTop = new CssLength(8),
                MarginBottom = new CssLength(8),
                MarginLeft = new CssLength(24),
                MarginRight = new CssLength(24)
            },
            // The HTML user-agent sheet gives dl the paragraph-like block margins and dd the
            // start indent; dt has no style beyond display:block (blockquote's 8/24 scale).
            "dl" => new BoxStyle
            {
                MarginTop = new CssLength(8),
                MarginBottom = new CssLength(8)
            },
            "dd" => new BoxStyle { MarginLeft = new CssLength(24) },
            _ => BoxStyle.Initial
        };
        // UA list defaults are decimal for ol and disc for ul. Other elements
        // propagate the inherited marker type, including li and nested panels.
        // Copy the box only when a marker setting actually differs.
        var markerKind = element.Name switch
        {
            "ol" => ListMarkerKind.Decimal,
            "ul" => ListMarkerKind.Disc,
            _ => parentBox.MarkerKind
        };
        // All of this element's declarations are applied to one private copy, made on the first
        // change; shared boxes (Initial, the parent's) are never assigned (see BoxStyle).
        var owned = false;
        BoxStyle Own()
        {
            if (!owned) { box = box with { }; owned = true; }
            return box;
        }
        if (box.MarkerKind != markerKind || box.MarkerInside != parentBox.MarkerInside)
        {
            Own().MarkerKind = markerKind;
            box.MarkerInside = parentBox.MarkerInside;
        }
        // An element whose box declarations, parent box, element name, root font size and computed
        // font size and colour match an earlier element's computes the same box, so it shares that
        // published (immutable) box instead of a private copy: every cell of a table column usually
        // repeats one style. FontSize and Color precede every box property in the enum, and no other
        // declaration changes them, so they are final at the first box declaration. A cached box was
        // stored only after its whole computation succeeded, so skipped box declarations cannot fail.
        var boxChecked = false; var boxShared = false; var boxKey = 0;
        for (var index = 0; index < winners.Length; index++)
        {
            if (winners[index] is not { } winner) continue;
            var declaration = winner.Declaration;
            if (WritesBox(element.Name, declaration.Property))
            {
                if (boxShared) continue;
                if (!boxChecked)
                {
                    boxChecked = true;
                    boxKey = BoxKey(element.Name, parentBox, rootFontSize, style.FontSize, style.Color, index);
                    if (computedBoxes.TryGetValue(boxKey, out var computed) && SameBoxInputs(computed, element.Name,
                        parentBox, rootFontSize, style.FontSize, style.Color, index))
                    {
                        box = computed.Box;
                        boxShared = true;
                        continue;
                    }
                }
            }
            if (declaration.Property == CssProperty.Display)
                Own().DisplayNone = declaration.Value == "none";
            else if (declaration.Property == CssProperty.ListStyleType)
                Own().MarkerKind = declaration.Value switch
                {
                    "inherit" or "unset" => parentBox.MarkerKind,
                    "initial" => ListMarkerKind.Disc,
                    _ => ListMarkerTypes.Parse(declaration.Value, declaration.Offset)
                };
            else if (declaration.Property == CssProperty.ListStylePosition)
                Own().MarkerInside = declaration.Value switch
                {
                    "inherit" or "unset" => parentBox.MarkerInside,
                    "initial" or "outside" => false,
                    "inside" => true,
                    _ => throw new InvalidOperationException("Unvalidated list marker position.")
                };
            else if (declaration.Property == CssProperty.VerticalAlign)
                Own().VerticalAlign = declaration.Value switch
                {
                    "inherit" => parentBox.VerticalAlign,
                    "initial" or "unset" or "top" => CellVerticalAlign.Top,
                    "middle" => CellVerticalAlign.Middle,
                    "bottom" => CellVerticalAlign.Bottom,
                    _ => throw new InvalidOperationException("Unvalidated table-cell vertical alignment.")
                };
            else if (element.Name is ("div" or "section" or "article" or "header" or "footer" or "main" or "nav" or "aside" or "blockquote" or "figure" or "figcaption" or "address" or "dl" or "dt" or "dd")
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
                if (declaration.Property == CssProperty.BreakBefore) Own().BreakBefore = value;
                else Own().BreakAfter = value;
            }
            else if (declaration.Property == CssProperty.BreakInside)
                Own().AvoidBreakInside = declaration.Value switch
                {
                    "avoid" => true,
                    "auto" or "initial" or "unset" => false,
                    "inherit" => parentBox.AvoidBreakInside,
                    _ => throw new InvalidOperationException("Unvalidated break-inside.")
                };
            else if (CssDeclarations.IsBoxProperty(declaration.Property))
                CssDeclarations.ApplyBox(Own(), parentBox, declaration, rootFontSize, style.FontSize,
                    style.Color);
            else if (element.Name is ("div" or "section" or "article" or "header" or "footer" or "main" or "nav" or "aside" or "blockquote" or "figure" or "figcaption" or "address" or "dl" or "dt" or "dd" or "hr")
                && declaration.Property is (CssProperty.MarginTop or CssProperty.MarginBottom))
                CssDeclarations.ApplyPanelVerticalMargin(Own(), parentBox, declaration,
                    rootFontSize, style.FontSize);
            else
            {
                // html font-size:rem refers to the initial size; other rem
                // text metrics use the newly computed root font on html.
                // FontSize precedes these properties in the fixed enum.
                var computedRoot = element.Name == "html"
                    && declaration.Property is (CssProperty.LineHeight
                        or CssProperty.TextIndent or CssProperty.LetterSpacing
                        or CssProperty.WordSpacing or CssProperty.TextDecorationThickness
                        or CssProperty.TextUnderlineOffset)
                    ? style.FontSize : rootFontSize;
                // Explicit paragraph/heading margin/break:inherit must read
                // the direct panel's computed BoxStyle. Neither property is
                // inherited by default; do not change anonymous text or the
                // normal reset of non-inherited paragraph declarations.
                var hasReportPanelParent = paragraph && path.Count > 1 &&
                    path[^2].Name is ("div" or "section" or "article" or "header" or "footer" or "main" or "nav" or "aside" or "blockquote" or "figure" or "figcaption" or "address" or "dl" or "dt" or "dd");
                var inheritedParent = hasReportPanelParent &&
                    declaration.Property is (CssProperty.MarginTop or CssProperty.MarginBottom)
                    ? parent with { MarginBefore = parentBox.MarginTop.IsPercent ? 0 : parentBox.MarginTop.Value,
                        MarginAfter = parentBox.MarginBottom.IsPercent ? 0 : parentBox.MarginBottom.Value,
                        PercentMarginBefore = parentBox.MarginTop.IsPercent ? parentBox.MarginTop : null,
                        PercentMarginAfter = parentBox.MarginBottom.IsPercent ? parentBox.MarginBottom : null }
                    : hasReportPanelParent &&
                        declaration.Property is (CssProperty.BreakBefore or CssProperty.BreakAfter)
                        ? parent with { BreakBefore = parentBox.BreakBefore,
                            BreakAfter = parentBox.BreakAfter }
                        : parent;
                if (!styleOwned) { style = style with { }; styleOwned = true; }
                CssDeclarations.Apply(style, inheritedParent, initial, declaration,
                    paragraph || reportTextFlowHost && declaration.Property is (CssProperty.WhiteSpace
                        or CssProperty.OverflowWrap or CssProperty.WordBreak or CssProperty.TextIndent)
                        || cellTextIndentHost && declaration.Property is (CssProperty.WhiteSpace or CssProperty.OverflowWrap or CssProperty.WordBreak or CssProperty.TextIndent), computedRoot);
            }
        }
        if (box.HasRoundedCorners && box.Outline.EffectiveWidth > 0)
            throw new FactsPdfException("FPDF1201",
                "Rectangular report outlines cannot be combined with rounded corners.",
                winners[(int)CssProperty.OutlineStyle]?.Declaration.Offset ?? elementOffset);
        if (box.HasRoundedCorners)
        {
            // Nonzero corner radii may not silently flatten asymmetric or
            // patterned borders. The uniform ring has four independent curved
            // corners and is actually painted;
            // non-rounded legacy box strokes retain exactly their old bytes.
            var edges = new[] { box.BorderTop, box.BorderRight, box.BorderBottom, box.BorderLeft };
            // currentcolor is stored as null, but paints with the final
            // computed text colour. Compare real ink, not raw nullable tokens.
            // A loop, not lambdas: a lambda capturing style would move style and every local the
            // local functions above capture into one closure object allocated for every element.
            var ink = style.Color;
            var firstInk = edges[0].Color ?? ink;
            var painted = false; var uneven = false;
            foreach (var edge in edges)
            {
                if (edge.EffectiveWidth > 0) painted = true;
                if (edge.EffectiveWidth <= 0 || edge.Pattern != BorderPattern.Solid
                    || edge.EffectiveWidth != edges[0].EffectiveWidth || (edge.Color ?? ink) != firstInk
                    || edge.Transparent != edges[0].Transparent) uneven = true;
            }
            if (painted && uneven)
                throw new FactsPdfException("FPDF1201",
                    "Rounded report boxes require an equally thick, single-colour solid border on all four sides.",
                    winners[(int)CssProperty.BorderTopLeftRadius]?.Declaration.Offset
                     ?? winners[(int)CssProperty.BorderTopRightRadius]?.Declaration.Offset
                     ?? winners[(int)CssProperty.BorderBottomRightRadius]?.Declaration.Offset
                     ?? winners[(int)CssProperty.BorderBottomLeftRadius]?.Declaration.Offset
                     ?? elementOffset);
        }
        if (boxChecked && !boxShared && owned && computedBoxes.Count < MaxComputedBoxes)
            computedBoxes[boxKey] = new ComputedBox(element.Name, parentBox, rootFontSize, style.FontSize, style.Color,
                BoxWrites(element.Name), box);
        return (style, box);
    }

    // A zero margin on <html> or <body>, or a zero padding on <html>: their used insets are already
    // zero. CSS-wide keywords compute to zero too (the initial value is 0 and html, the only parent
    // body can inherit from, never takes a non-zero inset).
    private static bool RootZeroInset(string element, CssDeclaration declaration)
    {
        var margin = declaration.Property is CssProperty.MarginTop or CssProperty.MarginRight
            or CssProperty.MarginBottom or CssProperty.MarginLeft;
        var padding = declaration.Property is CssProperty.PaddingTop or CssProperty.PaddingRight
            or CssProperty.PaddingBottom or CssProperty.PaddingLeft;
        if (!(element == "html" && (margin || padding) || element == "body" && margin)) return false;
        return declaration.Value is "inherit" or "initial" or "unset"
            || CssBoxValues.TryLength(declaration.Value, out var length, out _, rootFontSize: 16,
                allowRem: true, elementFontSize: 16, allowEm: true) && length.Value == 0;
    }

    // Inline text elements lay out no box of their own: a zero margin or padding leaves the text
    // where it is, and a transparent background (also background:none) paints nothing. initial and
    // unset compute to those values; inherit could carry a parent block's inset or fill, so it fails.
    private static bool InlineZeroBox(string element, CssDeclaration declaration)
    {
        if (!HtmlDocumentReader.InlineTag(element)) return false;
        if (declaration.Value is "initial" or "unset") return declaration.Property is CssProperty.BackgroundColor
            or CssProperty.MarginTop or CssProperty.MarginRight or CssProperty.MarginBottom or CssProperty.MarginLeft
            or CssProperty.PaddingTop or CssProperty.PaddingRight or CssProperty.PaddingBottom or CssProperty.PaddingLeft;
        if (declaration.Property == CssProperty.BackgroundColor) return declaration.Value == "transparent";
        return declaration.Property is CssProperty.MarginTop or CssProperty.MarginRight or CssProperty.MarginBottom
                or CssProperty.MarginLeft or CssProperty.PaddingTop or CssProperty.PaddingRight
                or CssProperty.PaddingBottom or CssProperty.PaddingLeft
            && CssBoxValues.TryLength(declaration.Value, out var length, out _, rootFontSize: 16,
                allowRem: true, elementFontSize: 16, allowEm: true) && length.Value == 0;
    }

    // A zero margin is the initial value on list items and on table rows, row groups, columns,
    // column groups and cells (where CSS 2.1 §8.3 margins do not apply at all), so *{margin:0}
    // changes nothing there and is dropped. Nonzero values still fail: a list item would need real
    // spacing, and the table parts keep rejecting declarations they do not render.
    private static bool TablePartMargin(string element, CssDeclaration declaration)
    {
        if (declaration.Property is not (CssProperty.MarginTop or CssProperty.MarginRight
            or CssProperty.MarginBottom or CssProperty.MarginLeft)) return false;
        var wide = declaration.Value is "initial" or "unset";
        var length = CssBoxValues.TryLength(declaration.Value, out var value, out _, rootFontSize: 16,
            allowRem: true, elementFontSize: 16, allowEm: true);
        return element is "li" or "tr" or "td" or "th" or "thead" or "tbody" or "tfoot" or "col" or "colgroup"
            && (wide || length && value.Value == 0);
    }

    // ul, ol and table lay out with a zero margin (their initial value, used as is); rows, row groups,
    // columns and column groups take no padding (CSS 2.1 §8.4); a br lays out no box at all. So the
    // rest of *{margin:0;padding:0} changes nothing there and is dropped. inherit could carry a
    // parent panel's inset, and nonzero values would need real geometry, so both still fail.
    private static bool UnusedZeroInset(string element, CssDeclaration declaration)
    {
        var margin = declaration.Property is CssProperty.MarginTop or CssProperty.MarginRight
            or CssProperty.MarginBottom or CssProperty.MarginLeft;
        var padding = declaration.Property is CssProperty.PaddingTop or CssProperty.PaddingRight
            or CssProperty.PaddingBottom or CssProperty.PaddingLeft;
        if (!(margin && element is "ul" or "ol" or "table" or "br"
            || padding && element is "tr" or "thead" or "tbody" or "tfoot" or "col" or "colgroup" or "br")) return false;
        return declaration.Value is "initial" or "unset"
            || CssBoxValues.TryLength(declaration.Value, out var length, out _, rootFontSize: 16,
                allowRem: true, elementFontSize: 16, allowEm: true) && length.Value == 0;
    }

    // Mirrors the branches of Compute that assign the element's box rather than its text style.
    private static bool BoxSizingNoOpHost(string element) => HtmlDocumentReader.InlineTag(element)
        || element is "html" or "tr" or "thead" or "tbody" or "tfoot" or "colgroup";

    private static bool WritesBox(string element, CssProperty property) =>
        property is CssProperty.Display or CssProperty.ListStyleType or CssProperty.ListStylePosition
            or CssProperty.VerticalAlign or CssProperty.BreakInside
        || element is ("div" or "section" or "article" or "header" or "footer" or "main" or "nav" or "aside" or "blockquote" or "figure" or "figcaption" or "address" or "dl" or "dt" or "dd")
            && property is (CssProperty.BreakBefore or CssProperty.BreakAfter)
        || CssDeclarations.IsBoxProperty(property)
        || element is ("div" or "section" or "article" or "header" or "footer" or "main" or "nav" or "aside" or "blockquote" or "figure" or "figcaption" or "address" or "dl" or "dt" or "dd" or "hr")
            && property is (CssProperty.MarginTop or CssProperty.MarginBottom);

    private int BoxKey(string element, BoxStyle parentBox, double rootFontSize, double fontSize, Rgb color, int first)
    {
        var hash = new HashCode();
        hash.Add(element);
        hash.Add(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(parentBox));
        hash.Add(rootFontSize);
        hash.Add(fontSize);
        hash.Add(color);
        for (var index = first; index < winners.Length; index++)
            if (winners[index] is { } winner && WritesBox(element, winner.Declaration.Property))
            {
                hash.Add(winner.Declaration.Property);
                hash.Add(winner.Declaration.Value);
            }
        return hash.ToHashCode();
    }

    private bool SameBoxInputs(ComputedBox computed, string element, BoxStyle parentBox, double rootFontSize,
        double fontSize, Rgb color, int first)
    {
        if (!string.Equals(computed.Element, element, StringComparison.Ordinal) || !ReferenceEquals(computed.ParentBox, parentBox)
            || !computed.RootFontSize.Equals(rootFontSize) || !computed.FontSize.Equals(fontSize) || computed.Color != color) return false;
        var writes = computed.Writes; var count = 0;
        for (var index = first; index < winners.Length; index++)
        {
            if (winners[index] is not { } winner || !WritesBox(element, winner.Declaration.Property)) continue;
            if (count == writes.Length || writes[count].Property != winner.Declaration.Property
                || !string.Equals(writes[count].Value, winner.Declaration.Value, StringComparison.Ordinal)) return false;
            count++;
        }
        return count == writes.Length;
    }

    private CssDeclaration[] BoxWrites(string element)
    {
        var count = 0;
        for (var index = 0; index < winners.Length; index++)
            if (winners[index] is { } winner && WritesBox(element, winner.Declaration.Property)) count++;
        var writes = new CssDeclaration[count]; count = 0;
        for (var index = 0; index < winners.Length; index++)
            if (winners[index] is { } winner && WritesBox(element, winner.Declaration.Property)) writes[count++] = winner.Declaration.Shared;
        return writes;
    }
}
