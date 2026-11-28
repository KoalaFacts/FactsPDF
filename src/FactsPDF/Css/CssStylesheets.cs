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
    private sealed record Rule(CssSelector Selector, CssDeclaration[] Declarations, Layer? Layer = null);
    // Layer: where the declaration's cascade layer sorts (see LayerOrder), compared after importance and
    // the style attribute and before specificity, as the cascade orders them.
    private readonly record struct Winner(CssDeclaration Declaration, bool Inline, CssSpecificity Specificity, int Layer = 0, bool UserAgent = false)
    {
        internal bool Beats(Winner other)
        {
            var c = Declaration.Important.CompareTo(other.Declaration.Important); if (c != 0) return c > 0;
            c = Inline.CompareTo(other.Inline); if (c != 0) return c > 0;
            c = Layer.CompareTo(other.Layer); if (c != 0) return c > 0;
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
    // The cascade tables are pooled per thread like the token list: they are taken by a sheet and returned when the
    // document has been read (ReturnPooled), so a conversion allocates them only the first time.
    private readonly Winner?[] winners;
    [ThreadStatic] private static Winner?[]? pooledWinners, pooledUserAgentWinners;
    [ThreadStatic] private static CssStylesheets? lastCreated;

    /// <summary>Returns the calling thread's most recent sheet's cascade tables to the pool; call once its document is read.</summary>
    internal static void ReturnPooled()
    {
        if (lastCreated is not { } sheet) return;
        lastCreated = null;
        Array.Clear(sheet.winners); Array.Clear(sheet.userAgentWinners);
        pooledWinners = sheet.winners; pooledUserAgentWinners = sheet.userAgentWinners;
    }
    // The user-agent declarations that won for the element being computed (see UserAgentStyleSheet),
    // merged into winners for every property no author declaration (or a revert) decided.
    private readonly Winner?[] userAgentWinners;
    // Matching the user-agent rules is no author work: it never charges the conversion's CSS budgets.
    private readonly CssBudget userAgentBudget;
    // Text styles computed from nothing but user-agent declarations on a shared parent style, by parent,
    // element and root font size: siblings of one element name share one style, as they share the box.
    private const int MaxUserAgentTextStyles = 64;
    private readonly Dictionary<UserAgentTextKey, TextStyle> userAgentTextStyles = [];
    // Compared by the parent style's identity, not by its value: hashing a whole style per element would cost more than it saves.
    // Matched is the set of the element name's user-agent rules that matched (a bit per rule, in sheet order):
    // an attribute or ancestor condition (abbr[title]) makes elements of one name differ.
    private readonly record struct UserAgentTextKey(TextStyle Parent, string Element, double RootFontSize, ulong Matched) : IEquatable<UserAgentTextKey>
    {
        public bool Equals(UserAgentTextKey other) => ReferenceEquals(Parent, other.Parent)
            && string.Equals(Element, other.Element, StringComparison.Ordinal) && RootFontSize.Equals(other.RootFontSize)
            && Matched == other.Matched;
        public override int GetHashCode() => HashCode.Combine(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Parent), Element, RootFontSize, Matched);
    }

    /// <summary>The value of the text-align declaration that won the cascade for the element the
    /// last <see cref="Compute"/> call computed, or null when none applied to it.</summary>
    internal string? LastTextAlign => winners[(int)CssProperty.TextAlign]?.Declaration.Value;
    // Boxes computed earlier in this conversion, keyed by everything their box declarations read
    // (see Compute). Bounded so a page of distinct styles keeps no more than a small table.
    private const int MaxComputedBoxes = 256;
    private readonly Dictionary<int, ComputedBox> computedBoxes = [];
    private sealed record ComputedBox(string Element, BoxStyle ParentBox, double RootFontSize, double FontSize,
        Rgb Color, ListMarkerKind? ListTypeHint, CssDeclaration[] Writes, bool UserAgentVerticalAlign, BoxStyle Box);
    private readonly CssBudget budget;
    private readonly PdfOptions options;
    private readonly CssSyntaxLimits syntaxLimits;
    // What the element being computed gives em, rem and font-size percentages in math functions.
    // State only math, animations, layers and revert-layer need, allocated on first use.
    private sealed class Extras
    {
        internal double MathParentFontSize, MathElementFontSize, MathRootFontSize;
        internal (double Width, double Height)? Viewport;
        internal Dictionary<string, List<(Layer? Layer, CssAtRuleNode Rule)>>? KeyframesByName;
        internal Dictionary<(string Name, int Offset), CssDeclaration[]>? KeyframeStates;
        internal List<Winner>? Offered;
        internal Layer? LayerRoot;
        // The animation longhands' cascade winners, apart from the main table, which they would only enlarge.
        internal readonly Winner?[] Animation = new Winner?[(int)CssProperty.AnimationTimingFunction - (int)CssProperty.AnimationName + 1];
    }
    private Extras? extras;
    private Extras X => extras ??= new();
    // Whether a selector reads sibling positions or emptiness, and the script and template elements
    // collection dropped (each with the token index it preceded), which still count as siblings.
    private bool usesStructure;
    private List<(int Token, string Name, HtmlToken Source)>? dropped;

    /// <summary>The document's element positions, built by <see cref="TrackPositions"/> only when a
    /// selector needs them.</summary>
    internal HtmlElementPositions? Positions { get; private set; }

    /// <summary>Builds <see cref="Positions"/> from the collected tokens when a selector uses a
    /// structural pseudo-class.</summary>
    internal void TrackPositions(List<HtmlToken> tokens, CancellationToken cancellation)
    {
        if (usesStructure) Positions = HtmlElementPositions.Build(tokens, dropped, cancellation);
    }
    // Custom property and var() declarations an element matched, kept until every other declaration
    // has been offered: custom properties cascade by name, then each var() declaration is compiled
    // against the element's computed custom properties and offered at its own cascade position.
    private readonly record struct Deferral(CssDeclaration Shared, int Order, int Offset, bool Inline, CssSpecificity Specificity, int Layer = 0)
    {
        internal bool Beats(Deferral other)
        {
            var c = Shared.Important.CompareTo(other.Shared.Important); if (c != 0) return c > 0;
            c = Inline.CompareTo(other.Inline); if (c != 0) return c > 0;
            c = Layer.CompareTo(other.Layer); if (c != 0) return c > 0;
            c = Specificity.CompareTo(other.Specificity); return c != 0 ? c > 0 : Order >= other.Order;
        }
    }
    private readonly List<Deferral> customDeclarations = [], pendingDeclarations = [];
    // Every declaration offered to the element being computed, kept only when a stylesheet uses revert-layer.
    private List<Winner> offered => X.Offered ??= [];
    // The computed custom properties of each element on the current ancestor path, by depth.
    private readonly List<CssCustomEnvironment> environments = [];
    private readonly List<CssDeclaration.Slot> substitutionSlots = [];
    // var() declarations already compiled for a computed custom property set (and, for a style
    // attribute, its element). Bounded like the other per-conversion tables, by entries and by the
    // substituted syntax nodes the entries were compiled from (at most MaxCssSyntaxNodes in all).
    private const int MaxSubstitutions = 256;
    private readonly Dictionary<(CssDeclaration Declaration, CssCustomEnvironment Environment, string? Element), (CssDeclaration[] Declarations, FactsPdfException? Warning)> substitutions = [];
    private long substitutionNodes;
    /// <summary>The compiled <c>@page</c> rules, or null when the stylesheets have none.</summary>
    internal CssPageRules? PageRules { get; private set; }

    /// <summary>Whether any declaration sets a counter or prints one, so the reader tracks counters.</summary>
    internal bool UsesCounters => budget.UsesCounters;

    private CssStylesheets(PdfOptions options, CancellationToken token)
    {
        if (options.MaxCssCharacters < 1 || options.MaxCssSelectors < 1 || options.MaxCssDeclarations < 1 || options.MaxCssMatchOperations < 1 || options.MaxCssCustomProperties < 1)
            throw new ArgumentOutOfRangeException(nameof(options), "CSS budgets must be positive.");
        this.options = options;
        winners = pooledWinners ?? new Winner?[(int)CssProperty.TextOverflow + 1];
        userAgentWinners = pooledUserAgentWinners ?? new Winner?[UserAgentStyleSheet.Properties.Length];
        pooledWinners = pooledUserAgentWinners = null;
        lastCreated = this;
        budget = new(options, token);
        userAgentBudget = new(UserAgentStyleSheet.Unlimited, token);
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
        // no more slots than it may hold. Up to four more hold the html, head and body tags the HTML
        // parser implies (HtmlImpliedElements); a page that omits end tags grows the list as usual.
        tokens.EnsureCapacity((int)Math.Min(EstimateTokens(html), 3L * options.MaxElements + 2) + 4);
        // A template's contents are inert (HTML: a document fragment that is never rendered), so
        // its tokens, style elements and style attributes are counted against the element limit
        // and then dropped before any pass sees them; the page renders as if it were absent.
        var templates = 0; var templateOffset = -1; var insideTitle = false; string? insideScript = null; var insideColgroup = false;
        // A style element whose type is not CSS is not a stylesheet: non-strict mode ignores its text.
        var ignoredStyle = false;
        var shape = new HtmlImpliedElements();
        foreach (var item in HtmlTokens.Read(html, token))
        {
            token.ThrowIfCancellationRequested();
            if (item.Kind is HtmlTokenKind.Start or HtmlTokenKind.End && item.Value == "template")
            {
                if (item.Kind == HtmlTokenKind.End)
                {
                    if (templates == 0) throw new FactsPdfException("FPDF1101", "Unexpected end tag 'template'.", item.Offset);
                    templates--;
                    continue;
                }
                if (++elements > options.MaxElements) throw new FactsPdfException("FPDF1002", "Element limit exceeded.", item.Offset);
                // Title text is plain text in HTML, so markup there would show literally; reject it
                // like any other element inside title instead of silently changing the title.
                if (insideTitle) throw new FactsPdfException("FPDF1101", "template cannot be inside title.", item.Offset);
                // A declarative shadow root is rendered by browsers, so dropping it would lose content.
                if (item.Attributes!.ContainsKey("shadowrootmode"))
                    throw new FactsPdfException("FPDF1103", "Declarative shadow DOM (template shadowrootmode) is not supported.", item.Offset);
                if (templates++ == 0)
                {
                    templateOffset = item.Offset;
                    shape.Dropped(tokens, "template");
                    (sheet.dropped ??= []).Add((tokens.Count, "template", item));
                }
                continue;
            }
            if (templates > 0)
            {
                if (item.Kind == HtmlTokenKind.Start && ++elements > options.MaxElements)
                    throw new FactsPdfException("FPDF1002", "Element limit exceeded.", item.Offset);
                continue;
            }
            // FactsPDF runs no scripts, and a script element is never rendered (display:none in the
            // HTML user-agent sheet), so it is counted against the element limit and dropped with
            // its attributes; the tokenizer has already skipped its data (R5cj).
            // A noscript element is dropped the same way: with scripting enabled (as when a browser
            // prints a page) its content is raw text and the user-agent sheet gives it
            // display:none !important, so no author rule shows it.
            if (item.Kind is HtmlTokenKind.Start or HtmlTokenKind.End && item.Value is "script" or "noscript")
            {
                var raw = item.Value;
                if (item.Kind == HtmlTokenKind.End)
                {
                    if (insideScript != raw) throw new FactsPdfException("FPDF1101", $"Unexpected end tag '{raw}'.", item.Offset);
                    insideScript = null;
                    continue;
                }
                if (++elements > options.MaxElements) throw new FactsPdfException("FPDF1002", "Element limit exceeded.", item.Offset);
                if (insideTitle) throw new FactsPdfException("FPDF1101", $"{raw} cannot be inside title.", item.Offset);
                // In a colgroup, HTML closes the column group before a script, so the following col
                // would start a new column group; dropping the script would shift the columns.
                if (insideColgroup) throw new FactsPdfException("FPDF1101", $"{raw} cannot be inside colgroup.", item.Offset);
                insideScript = raw;
                shape.Dropped(tokens, raw);
                (sheet.dropped ??= []).Add((tokens.Count, raw, item));
                continue;
            }
            var appended = tokens.Count;
            var kept = shape.Add(tokens, item);
            // The end tags written out (the omitted ones the parser implies, then the token itself)
            // close elements in the early depth check as they do in the reader. The parser holds an
            // explicit </head> back until body content starts, but the early depth check closes head
            // there: it may only under-count the reader's depth, never exceed it.
            for (var k = appended; k < tokens.Count; k++)
                if (tokens[k] is { Kind: HtmlTokenKind.End } closing and not ImpliedHtmlToken) depth.Read(closing);
            if (!kept && item.Kind == HtmlTokenKind.End && item.Value == "head") depth.Read(item);
            if (item.Kind is HtmlTokenKind.Start or HtmlTokenKind.End && item.Value == "title")
                insideTitle = item.Kind == HtmlTokenKind.Start && !item.SelfClosing;
            else if (item.Kind is HtmlTokenKind.Start or HtmlTokenKind.End && item.Value is "colgroup" or "thead" or "tbody" or "tfoot" or "tr" or "table")
                insideColgroup = item.Kind == HtmlTokenKind.Start && !item.SelfClosing && item.Value == "colgroup";
            if (item.Kind == HtmlTokenKind.End && item.Value == "style") ignoredStyle = false;
            if (item.Kind == HtmlTokenKind.StyleText && ignoredStyle) ignoredStyle = false;
            else if (item.Kind == HtmlTokenKind.StyleText)
            {
                sheet.budget.Characters(item.Value.Length, item.Offset);
                sheet.ParseRules(sheet.ParseSource(item.Value, item.Offset, declarationsOnly: false).Rules);
            }
            else if (item.Kind == HtmlTokenKind.Start)
            {
                if (++elements > options.MaxElements) throw new FactsPdfException("FPDF1002", "Element limit exceeded.", item.Offset);
                if (kept) depth.Read(item);
                if (item.Value == "style") ignoredStyle = ValidateStyleElement(item, sheet.budget.Warnings);
                else if (item is StyledHtmlToken styled)
                {
                    var css = styled.Attributes!["style"];
                    sheet.budget.Characters(css.Length, item.Offset);
                    styled.InlineDeclarations = sheet.CompileStyleAttribute(css, item.Value, item.StyleValueOffset, item.StyleOriginOffsets);
                }
            }
        }
        if (templates > 0) throw new FactsPdfException("FPDF1101", "template element is not closed.", templateOffset);
        shape.Finish(tokens, html.Length);
        sheet.FinalizeLayers();
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
    /// map and are always compiled. A table-part display value depends on the element too, so the
    /// replay checks it against this element before charging its declaration, where compiling
    /// would have rejected it.
    /// </summary>
    private InlineStyle CompileStyleAttribute(string css, string element, int valueOffset, int[]? originOffsets)
    {
        if (originOffsets is null && compiledStyles.TryGetValue(css, out var compiled))
        {
            var declarations = compiled.Declarations;
            if (declarations.Length == 0) return new InlineStyle(declarations, 0, 0);
            var shift = valueOffset - compiled.ValueOffset; var firstOrder = 0;
            for (var i = 0; i < declarations.Length; i++)
            {
                budget.Token.ThrowIfCancellationRequested();
                if (TablePartDisplay(declarations[i]) && TablePartDisplayError(declarations[i].Value, element) is { } displayError)
                    throw new FactsPdfException("FPDF1202", displayError, declarations[i].Offset + shift);
                var order = budget.Declaration(declarations[i].Offset + shift);
                if (i == 0) firstOrder = order;
            }
            return new InlineStyle(declarations, shift, firstOrder);
        }
        var warned = budget.Warnings?.Total ?? 0;
        var nodes = ParseSource(css, valueOffset, declarationsOnly: true, originOffsets).Declarations;
        var output = CssSyntaxAdapter.CompileKnownDeclarations(nodes, budget, hostElement: element);
        // A text that produced non-strict warnings is compiled again for every element, so each
        // element's dropped declarations are reported at its own offsets.
        if (originOffsets is null && compiledStyles.Count < MaxCompiledStyles && css.Length <= MaxCompiledStyleLength
            && (budget.Warnings?.Total ?? 0) == warned)
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
    // Whether the style element is ignored. HTML processes a style element only when its type is
    // absent, empty or an ASCII case-insensitive match for text/css (no parameters, no white space;
    // Chrome 158 agrees), so any other type is not a stylesheet: strict mode fails FPDF1204 as it
    // always has, and non-strict mode ignores the whole element (with its other attributes) and warns.
    private static bool ValidateStyleElement(HtmlToken token, CssWarnings? warnings)
    {
        if (token.SelfClosing) throw new FactsPdfException("FPDF1101", "style requires an HTML end tag.", token.Offset);
        if (warnings is not null && token.Attributes!.TryGetValue("type", out var type) && !CssType(type))
        {
            warnings.Add("FPDF1204", "Only text/css style elements are supported.", token.Offset);
            return true;
        }
        foreach (var (name, value) in token.Attributes!)
        {
            if (name == "type")
            {
                if (!CssType(value))
                    throw new FactsPdfException("FPDF1204", "Only text/css style elements are supported.", token.Offset);
            }
            else if (name == "media")
            {
                var media = value.Trim().ToLowerInvariant();
                if (media is not ("" or "all" or "print")) throw new FactsPdfException("FPDF1204", "Only empty/all/print style media are supported.", token.Offset);
            }
            // dir="ltr" is accepted on every element, as in the document reader (R5ci).
            else if (!(HtmlGlobalAttributes.NoEffect(name)
                || name == "dir" && value.Equals("ltr", StringComparison.OrdinalIgnoreCase)))
                throw new FactsPdfException("FPDF1103", $"Attribute '{name}' is not supported on style.", token.Offset);
        }
        return false;
    }

    private static bool CssType(string type) => type.Length == 0 || type.Equals("text/css", StringComparison.OrdinalIgnoreCase);
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
            // Non-strict mode keeps the tree CSS Syntax error recovery produced (the parser drops
            // exactly the declaration, rule or at-rule a browser drops) and reports each error.
            if (budget.Warnings is { } warnings)
                foreach (var diagnostic in result.Diagnostics) warnings.Add("FPDF1203", diagnostic.Message, diagnostic.Span.Start);
            else CssSyntaxAdapter.ThrowIfRecovered(result);
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

    // A cascade layer: a name (empty for an anonymous layer) under its parent, with the layers declared
    // inside it in order of first appearance. Rank is the position of the layer's own declarations in
    // the final order (set by FinalizeLayers): a layer's sublayers rank below its own declarations and
    // every layer ranks below the unlayered declarations.
    private sealed class Layer(string name, Layer? parent)
    {
        internal string Name { get; } = name;
        internal Layer? Parent { get; } = parent;
        internal List<Layer> Children { get; } = [];
        internal int Rank { get; set; }
    }

    private Layer layerRoot => X.LayerRoot ??= new("", null);
    // The valid @keyframes rules by name (the last of a name wins), and the declarations of their 0% and
    // 100% keyframes compiled when an animation first shows one.
    private Dictionary<string, List<(Layer? Layer, CssAtRuleNode Rule)>> keyframes => X.KeyframesByName ??= new(StringComparer.Ordinal);
    private Dictionary<(string Name, int Offset), CssDeclaration[]> keyframeStates => X.KeyframeStates ??= [];
    private bool HasKeyframes => extras?.KeyframesByName is { Count: > 0 };
    private int layerCount;

    private Layer LayerAt(Layer? scope, string name)
    {
        var parent = scope ?? layerRoot;
        foreach (var child in parent.Children)
            if (child.Name.Length > 0 && string.Equals(child.Name, name, StringComparison.Ordinal)) return child;
        var created = new Layer(name, parent);
        parent.Children.Add(created);
        return created;
    }

    private Layer AnonymousLayer(Layer? scope)
    {
        var parent = scope ?? layerRoot;
        var created = new Layer("", parent);
        parent.Children.Add(created);
        return created;
    }

    // Layers rank in post order: the sublayers of a layer come before its own declarations, and
    // the layers of one level in the order they first appeared.
    private void FinalizeLayers()
    {
        if (extras?.LayerRoot is null) return;
        var next = 0;
        void Visit(Layer layer)
        {
            foreach (var child in layer.Children) Visit(child);
            layer.Rank = next++;
        }
        foreach (var layer in layerRoot.Children) Visit(layer);
        layerCount = next;
    }

    // Where a declaration of a layer sorts: normal declarations of a later layer win, and the unlayered
    // ones win over every layer; important declarations reverse that, so the earliest layer wins and an
    // unlayered important declaration loses to every layered one.
    private int LayerOrder(Layer? layer, bool important) => layer is null
        ? important ? int.MinValue : int.MaxValue
        : important ? layerCount - layer.Rank : layer.Rank;

    // A layer name: identifiers joined by dots, with no white space (CSS Cascade 5, <layer-name>).
    private static bool TryLayerName(CssSyntaxComponent[] prelude, ref int at, out string[] segments)
    {
        var parts = new List<string>();
        while (true)
        {
            if (at >= prelude.Length || prelude[at] is not CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.Ident } ident }) { segments = []; return false; }
            parts.Add(ident.Value);
            at++;
            if (at + 1 < prelude.Length && prelude[at] is CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.Delim, Value: "." } }
                && prelude[at + 1] is CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Ident }) { at++; continue; }
            segments = [.. parts];
            return true;
        }
    }

    // Handles @layer: a statement names layers (creating them in order), a block puts its rules in
    // one layer, and a block without a name opens an anonymous layer. Returns false for a malformed
    // rule, which a browser drops whole.
    private bool TryLayerRule(CssAtRuleNode at, Layer? scope, out List<Layer> statement, out Layer? block)
    {
        statement = [];
        block = null;
        var prelude = at.Prelude.Where(x => x is not CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Whitespace }).ToArray();
        var i = 0;
        if (at.Contents is null)
        {
            // @layer a, b.c;
            while (true)
            {
                if (!TryLayerName(prelude, ref i, out var segments)) return false;
                var layer = scope;
                foreach (var segment in segments) layer = LayerAt(layer, segment);
                statement.Add(layer!);
                if (i == prelude.Length) return true;
                if (prelude[i] is not CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Comma }) return false;
                i++;
            }
        }
        if (prelude.Length == 0) { block = AnonymousLayer(scope); return true; }
        if (!TryLayerName(prelude, ref i, out var name) || i != prelude.Length) return false;
        var target = scope;
        foreach (var segment in name) target = LayerAt(target, segment);
        block = target;
        return true;
    }

    private void RegisterKeyframes(CssAtRuleNode at, Layer? layer)
    {
        var name = at.Prelude.First(x => x is not CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Whitespace }) as CssTokenComponent;
        var key = name!.Token.Value;
        if (!keyframes.TryGetValue(key, out var rules)) keyframes.Add(key, rules = []);
        rules.Add((layer, at));
    }

    // The keyframes rule of a name that wins the cascade: those of a later layer (the unlayered ones last)
    // win, and among rules of one layer the last written.
    private CssAtRuleNode Keyframes(string name)
    {
        (Layer? Layer, CssAtRuleNode Rule) best = default!;
        var order = int.MinValue;
        foreach (var candidate in keyframes[name])
        {
            var candidateOrder = LayerOrder(candidate.Layer, false);
            if (candidateOrder >= order) { best = candidate; order = candidateOrder; }
        }
        return best.Rule!;
    }

    // The offsets a keyframe selector names: from and to are 0% and 100%.
    private static List<double> KeyframeOffsets(IReadOnlyList<CssSyntaxComponent> prelude)
    {
        var offsets = new List<double>();
        foreach (var item in prelude)
        {
            if (item is not CssTokenComponent { Token: var token }) continue;
            if (token.Kind == CssSyntaxTokenKind.Ident) offsets.Add(token.Value.Equals("from", StringComparison.OrdinalIgnoreCase) ? 0 : 100);
            else if (token.Kind == CssSyntaxTokenKind.Percentage
                && double.TryParse(token.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var percent))
                offsets.Add(percent);
        }
        return offsets;
    }

    // The declarations of the keyframes at one offset (0 or 100), in the order they are written, as the
    // animations origin lays them over the cascade. !important declarations in a keyframe are invalid, and
    // the animation and transition properties are not animated. A value that is the identity transform
    // changes nothing and is skipped; any other value the renderer cannot honour fails like the same
    // declaration in a style rule.
    private CssDeclaration[] KeyframeState(string name, int offset, string element)
    {
        if (keyframeStates.TryGetValue((name, offset), out var cached)) return cached;
        var nodes = new List<CssDeclarationNode>();
        foreach (var content in Keyframes(name).Contents!)
        {
            if (content is not CssQualifiedRuleNode rule || !KeyframeOffsets(rule.Prelude).Contains(offset)) continue;
            foreach (var item in rule.Contents)
            {
                if (item is not CssDeclarationNode declaration || declaration.Important) continue;
                var property = declaration.Name.ToLowerInvariant();
                if (property.StartsWith("animation", StringComparison.Ordinal) || property.StartsWith("-webkit-animation", StringComparison.Ordinal)
                    || property.StartsWith("transition", StringComparison.Ordinal) || property.StartsWith("-webkit-transition", StringComparison.Ordinal)
                    || CssTransforms.IsIdentity(property, declaration.Values)) continue;
                nodes.Add(declaration);
            }
        }
        CssDeclaration[] compiled;
        try { compiled = CssSyntaxAdapter.CompileKnownDeclarations(nodes, budget, hostElement: element, fixedOrder: 1, recover: false); }
        catch (FactsPdfException error) when (error.Code != "FPDF1205")
        {
            var reason = error.Message.StartsWith(error.Code + ": ", StringComparison.Ordinal) ? error.Message[(error.Code.Length + 2)..] : error.Message;
            throw new FactsPdfException(error.Code, $"@keyframes {name} at {(offset == 0 ? "from" : "to")}, shown by the animation at print time: {reason}", error.SourceOffset);
        }
        if (compiled.Any(d => d.Property is CssProperty.Pending or CssProperty.Custom))
            throw new FactsPdfException("FPDF1202", $"@keyframes {name} uses var() or math in a keyframe the animation shows; write the value out.", Keyframes(name).Span.Start);
        keyframeStates[(name, offset)] = compiled;
        return compiled;
    }

    // The keyframe declarations the element's animations show at time zero, in animation order (a later
    // animation overrides an earlier one for the same property).
    private List<CssDeclaration>? AnimationState(string element)
    {
        string Value(CssProperty property, string initial)
        {
            if (X.Animation[(int)property - (int)CssProperty.AnimationName] is not { } winner) return initial;
            var value = winner.Declaration.Value;
            if (value is "initial" or "unset" or "revert" or "revert-layer") return initial;
            if (value == "inherit")
                throw new FactsPdfException("FPDF1202", "inherit for an animation property is not supported: animations apply to the element itself.", winner.Declaration.Offset);
            return value;
        }
        var names = CssAnimations.Split(Value(CssProperty.AnimationName, "none"));
        var durations = CssAnimations.Split(Value(CssProperty.AnimationDuration, "0s"));
        var delays = CssAnimations.Split(Value(CssProperty.AnimationDelay, "0s"));
        var counts = CssAnimations.Split(Value(CssProperty.AnimationIterationCount, "1"));
        var directions = CssAnimations.Split(Value(CssProperty.AnimationDirection, "normal"));
        var fills = CssAnimations.Split(Value(CssProperty.AnimationFillMode, "none"));
        var easings = CssAnimations.Split(Value(CssProperty.AnimationTimingFunction, "ease"));
        List<CssDeclaration>? result = null;
        for (var i = 0; i < names.Length; i++)
        {
            var name = names[i];
            if (name == "none" || !keyframes.ContainsKey(name)) continue;
            var outcome = CssAnimations.StateAtZero(CssAnimations.Layer(durations, i), CssAnimations.Layer(delays, i), CssAnimations.Layer(counts, i),
                CssAnimations.Layer(directions, i), CssAnimations.Layer(fills, i), CssAnimations.Layer(easings, i));
            if (outcome == CssAnimations.Outcome.None) continue;
            if (outcome == CssAnimations.Outcome.Interpolated)
                throw new FactsPdfException("FPDF1202",
                    $"The animation {name} would print a state between its keyframes (a delay or iteration count that ends inside an iteration, or a step easing that jumps at the start): only the first and last keyframe states are supported.",
                    Keyframes(name).Span.Start);
            (result ??= []).AddRange(KeyframeState(name, outcome == CssAnimations.Outcome.From ? 0 : 100, element));
        }
        return result;
    }

    private void ParseRules(IReadOnlyList<CssRuleNode> rules)
        => ParseBlock(rules, parents: null, layer: null);

    // The rules of a stylesheet or of a conditional group rule (parents null), or the contents of a style
    // rule or of a group rule nested in one (parents: the selectors of the enclosing style rule, which the
    // declarations in the block apply to and the nested rules extend). Declarations a style rule holds
    // after a nested rule apply at their own position in the cascade, as the Nesting specification says.
    private void ParseBlock(IReadOnlyList<CssSyntaxContent> contents, CssSelector[]? parents, Layer? layer)
    {
        List<CssDeclarationNode>? declarations = null;
        foreach (var item in contents)
        {
            budget.Token.ThrowIfCancellationRequested();
            if (item is CssDeclarationNode declaration)
            {
                if (parents is null)
                    throw new FactsPdfException("FPDF1204",
                        "Nested media, other at-rules and loose declarations are unsupported.", item.Span.Start);
                (declarations ??= []).Add(declaration);
                continue;
            }
            if (declarations is { Count: > 0 }) { AddStyleRule(parents!, declarations, layer); declarations = null; }
            if (item is CssAtRuleNode at) ParseAtRule(at, parents, layer);
            else if (item is CssQualifiedRuleNode qualified) ParseStyleRule(qualified, parents, layer);
            else throw new FactsPdfException("FPDF1203", "Unsupported CSS rule syntax.", item.Span.Start);
        }
        if (declarations is { Count: > 0 }) AddStyleRule(parents!, declarations, layer);
    }

    private void ParseAtRule(CssAtRuleNode at, CssSelector[]? parents, Layer? layer)
    {
        var nested = parents is not null;
        // Keyframes only drive on-screen animations; a written PDF page shows none of them,
        // so a well-formed @keyframes block is validated and dropped.
        if (at.Name.Equals("keyframes", StringComparison.OrdinalIgnoreCase)
            || at.Name.Equals("-webkit-keyframes", StringComparison.OrdinalIgnoreCase))
        {
            // A malformed @keyframes rule (or keyframe) is dropped by a browser; every
            // keyframe is dropped here anyway, so the page is the same either way.
            try { ValidateKeyframes(at); RegisterKeyframes(at, layer); }
            catch (FactsPdfException error) when (budget.Warnings is { } warnings) { warnings.Add(error); }
            return;
        }
        // An unknown at-rule, and @media without a block, are invalid: a browser drops
        // the whole at-rule (to its semicolon or with its block).
        if (budget.Warnings is { } dropped && (CssSpecValidity.UnknownAtRule(at.Name)
            || at.Name.Equals("media", StringComparison.OrdinalIgnoreCase) && at.Contents is null))
        {
            dropped.Add(nested ? "FPDF1203" : "FPDF1204", nested ? "Nested CSS rules are not supported by the PDF renderer." : "Only block-form @media print/all is supported.", at.Span.Start);
            return;
        }
        // Paged-media page context: size, margins and the six top/bottom margin boxes.
        if (at.Name.Equals("page", StringComparison.OrdinalIgnoreCase) && !nested)
        {
            (PageRules ??= new()).Add(at, budget);
            return;
        }
        if (at.Name.Equals("layer", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryLayerRule(at, layer, out _, out var inner))
            {
                var error = new FactsPdfException("FPDF1204", "@layer takes a layer name (identifiers joined by dots), a comma-separated list of names, or a block.", at.Span.Start);
                if (budget.Warnings is { } warnings) { warnings.Add(error); return; }
                throw error;
            }
            if (inner is not null) ParseBlock(at.Contents!, parents, inner);
            return;
        }
        // Bounded print-only conditionals: no device queries, imports or ignored rules. They reuse the
        // exact same selector, declaration, cascade and resource budgets as top-level CSS, and nest in
        // one another and in style rules.
        if (!at.Name.Equals("media", StringComparison.OrdinalIgnoreCase) || at.Contents is null)
        {
            if (nested)
                throw new FactsPdfException("FPDF1203", "Nested CSS rules are not supported by the PDF renderer.", at.Span.Start);
            throw new FactsPdfException("FPDF1204", "Only block-form @media print/all is supported.", at.Span.Start);
        }
        var prelude = at.Prelude
            .Where(x => x is not CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Whitespace })
            .ToArray();
        if (prelude.Length != 1 || prelude[0] is not CssTokenComponent
            { Token.Kind: CssSyntaxTokenKind.Ident } medium ||
            !(medium.Token.Value.Equals("print", StringComparison.OrdinalIgnoreCase)
              || medium.Token.Value.Equals("all", StringComparison.OrdinalIgnoreCase)))
            throw new FactsPdfException("FPDF1204", "Only a single print or all media type is supported.", at.Span.Start);
        var contents = WithoutUnknownAtRules(at.Contents, "Nested media, other at-rules and loose declarations are unsupported.", "FPDF1204", at.Span.Start);
        // A print stylesheet may carry @keyframes, which is validated and dropped, @page, which compiles
        // like a top-level one, and nested @media and @layer.
        if (!nested && contents.Any(x => x is not CssQualifiedRuleNode && !(x is CssAtRuleNode inner
                && (inner.Name.Equals("keyframes", StringComparison.OrdinalIgnoreCase)
                    || inner.Name.Equals("-webkit-keyframes", StringComparison.OrdinalIgnoreCase)
                    || inner.Name.Equals("page", StringComparison.OrdinalIgnoreCase)
                    || inner.Name.Equals("media", StringComparison.OrdinalIgnoreCase)
                    || inner.Name.Equals("layer", StringComparison.OrdinalIgnoreCase)))))
            throw new FactsPdfException("FPDF1204",
                "Nested media, other at-rules and loose declarations are unsupported.", at.Span.Start);
        ParseBlock(contents, parents, layer);
    }

    private void ParseStyleRule(CssQualifiedRuleNode rule, CssSelector[]? parents, Layer? layer)
    {
        CssSelector[] selectors;
        try { selectors = CssSyntaxAdapter.CompileSelectors(rule.Prelude, budget, rule.Span.Start, nested: parents is not null); }
        // An invalid selector list makes a browser drop the whole style rule, declarations included.
        catch (FactsPdfException error) when (budget.Warnings is { } warnings && error.Code == "FPDF1203"
            && CssSpecValidity.InvalidSelectorList(rule.Prelude))
        {
            warnings.Add(error);
            return;
        }
        // Relative to the enclosing style rule (or, outside one, to the root element, as :scope is).
        if (parents is not null || selectors.Any(s => s.NestingCount > 0))
            selectors = CssSelector.ResolveNesting(selectors, parents ?? [CssSelector.RootParent], budget, rule.Span.Start);
        var contents = WithoutUnknownAtRules(rule.Contents, "Nested CSS rules are not supported by the PDF renderer.", "FPDF1203", rule.Span.Start);
        if (budget.Warnings is null && contents.Any(x => x is CssAtRuleNode nested && CssSpecValidity.UnknownAtRule(nested.Name)))
            throw new FactsPdfException("FPDF1203", "Nested CSS rules are not supported by the PDF renderer.", rule.Span.Start);
        ParseBlock(contents, selectors, layer);
    }

    private void AddStyleRule(CssSelector[] selectors, List<CssDeclarationNode> nodes, Layer? layer)
    {
        var declarations = CssSyntaxAdapter.CompileKnownDeclarations(nodes, budget, hostSelectors: selectors);
        foreach (var selector in selectors)
        {
            usesStructure |= selector.UsesStructure;
            if (selector.PseudoElement != CssPseudoElement.None) HasPseudoRules = true;
            var entry = new Rule(selector, declarations, layer);
            var last = selector.Rightmost;
            if (last.Ids.Length > 0) Add(ids, last.Ids[0], entry);
            else if (last.Classes.Length > 0) Add(classes, last.Classes[0], entry);
            else if (last.Type is not null) Add(types, last.Type, entry);
            else universal.Add(entry);
        }
    }

    // Non-strict mode drops unknown at-rules nested in a block, as a browser does, reporting the
    // error strict mode throws for the block; anything else in the block is left for the caller.
    private IReadOnlyList<CssSyntaxContent> WithoutUnknownAtRules(IReadOnlyList<CssSyntaxContent> contents,
        string message, string code, int offset)
    {
        if (budget.Warnings is not { } warnings || !contents.Any(x => x is CssAtRuleNode nested && CssSpecValidity.UnknownAtRule(nested.Name)))
            return contents;
        warnings.Add(code, message, offset);
        return contents.Where(x => !(x is CssAtRuleNode nested && CssSpecValidity.UnknownAtRule(nested.Name))).ToArray();
    }

    private static void Add(Dictionary<string, List<Rule>> index, string key, Rule rule)
    { if (!index.TryGetValue(key, out var list)) index.Add(key, list = []); list.Add(rule); }

    // An index loop: LINQ Any over the IReadOnlyList boxes a list enumerator per call on .NET 8.
    private static bool AnyElement(IReadOnlyList<CssElement> path, Func<CssElement, bool> predicate)
    {
        for (var i = 0; i < path.Count; i++) if (predicate(path[i])) return true;
        return false;
    }

    // A list (or an item of one) inside a table cell: block content the cell lays out as a fragment,
    // unlike the flat text of a cell paragraph. The nearest cell or list outward decides.
    private static bool InCellList(IReadOnlyList<CssElement> path)
    {
        var list = false;
        for (var i = path.Count - 1; i >= 0; i--)
        {
            if (path[i].Name is "ol" or "ul" or "menu") list = true;
            else if (path[i].Name is "td" or "th") return list;
        }
        return false;
    }

    /// <summary>Whether any rule selects a ::before or ::after pseudo-element.</summary>
    internal bool HasPseudoRules { get; private set; }

    /// <summary>
    /// The winning author <c>display</c> value of the element last on the path (rules and its style
    /// attribute), or null when no declaration sets it. Read before the element is computed, by an
    /// element whose kind of box depends on it: a custom element is inline unless CSS makes it a block.
    /// </summary>
    internal string? AuthorDisplay(IReadOnlyList<CssElement> path, InlineStyle inlineDeclarations)
    {
        Winner? display = null;
        var element = path[^1];
        void Scan(List<Rule> rules)
        {
            foreach (var rule in rules)
            {
                budget.Work(rule.Selector.Offset);
                if (rule.Selector.PseudoElement != CssPseudoElement.None || !rule.Selector.Matches(path, budget, Positions)) continue;
                foreach (var declaration in rule.Declarations)
                {
                    if (declaration.Property != CssProperty.Display) continue;
                    var candidate = new Winner(declaration, false, rule.Selector.Specificity);
                    if (display is not { } old || candidate.Beats(old)) display = candidate;
                }
            }
        }
        Scan(universal);
        if (types.TryGetValue(element.Name, out var typeRules)) Scan(typeRules);
        if (element.Id is not null && ids.TryGetValue(element.Id, out var idRules)) Scan(idRules);
        foreach (var name in element.Classes.Names) if (classes.TryGetValue(name, out var classRules)) Scan(classRules);
        if (inlineDeclarations.Declarations is { } inline)
            foreach (var declaration in inline)
            {
                if (declaration.Property != CssProperty.Display) continue;
                var candidate = new Winner(declaration, true, default);
                if (display is not { } old || candidate.Beats(old)) display = candidate;
            }
        return display?.Declaration.Value;
    }

    /// <summary>
    /// The winning display and content values of an element's ::before or ::after, read before the
    /// pseudo-element is computed: they decide whether a box is generated and of which kind.
    /// </summary>
    internal (string? Display, string? Content) PseudoKeys(IReadOnlyList<CssElement> path, CssPseudoElement pseudo)
    {
        Winner? display = null, content = null;
        var element = path[^1];
        void Scan(List<Rule> rules)
        {
            foreach (var rule in rules)
            {
                budget.Work(rule.Selector.Offset);
                if (rule.Selector.PseudoElement != pseudo || !rule.Selector.Matches(path, budget, Positions)) continue;
                foreach (var declaration in rule.Declarations)
                {
                    var candidate = new Winner(declaration, false, rule.Selector.Specificity, LayerOrder(rule.Layer, declaration.Important));
                    if (declaration.Property == CssProperty.Display)
                    { if (display is not { } old || candidate.Beats(old)) display = candidate; }
                    else if (declaration.Property == CssProperty.Content)
                    { if (content is not { } old || candidate.Beats(old)) content = candidate; }
                }
            }
        }
        Scan(universal);
        if (types.TryGetValue(element.Name, out var typeRules)) Scan(typeRules);
        if (element.Id is not null && ids.TryGetValue(element.Id, out var idRules)) Scan(idRules);
        foreach (var name in element.Classes.Names) if (classes.TryGetValue(name, out var classRules)) Scan(classRules);
        // all:initial and all:unset give the pseudo-element display:inline; revert gives its user-agent value
        // (inline too); inherit would take the originating element's display, which is not tracked here.
        var displayValue = display?.Declaration is { Name: "all" } fromAll
            ? fromAll.Value is "initial" or "unset" or "revert" or "revert-layer" ? "inline" : "inherit"
            : display?.Declaration.Value;
        return (displayValue, content?.Declaration.Value);
    }

    internal (TextStyle Text, BoxStyle Box) Compute(TextStyle defaults, bool defaultsOwned, TextStyle parent, TextStyle initial,
        double rootFontSize, BoxStyle parentBox, IReadOnlyList<CssElement> path,
        bool paragraph, bool block, int elementOffset, InlineStyle inlineDeclarations, bool allowStyling = true, bool hidden = false,
        ListMarkerKind? listTypeHint = null, CssPseudoElement pseudo = CssPseudoElement.None, string? pseudoName = null,
        string? uaName = null, CssDeclaration? widthHint = null, CssDeclaration? heightHint = null)
    {
        Array.Clear(winners); Array.Clear(userAgentWinners); if (extras is not null) Array.Clear(extras.Animation); var element = path[^1];
        if (budget.UsesRevertLayer) offered.Clear();
        // A pseudo-element is matched through its originating element but validated and computed
        // as the box it generates: an inline span or a block div. An element that lays out like
        // another (form, details and summary like a div, canvas and iframe like an svg) is
        // validated as that element; uaName is the one that keeps its own user-agent defaults
        // (a fieldset's border, a summary's marker, an iframe's inset border).
        var subject = pseudoName ?? element.Name;
        var uaSubject = uaName ?? subject;
        customDeclarations.Clear(); pendingDeclarations.Clear();
        var cellParagraph = subject == "p" && AnyElement(path, e => e.Name is "td" or "th") && !InCellList(path);
        // Cell boxes inherit real first-line indent into anonymous text and flat paragraphs.
        var cellTextIndentHost = subject is "td" or "th" or "caption";
        // These inherited text-flow controls can be authored on document or
        // ordinary report containers. Their computed state reaches the same
        // measured paragraph/heading renderer, not a separate box approximation.
        // Direct inline, list and table declarations remain unsupported.
        var reportTextFlowHost = subject is ("html" or "body" or "div" or "section" or "article" or "header" or "footer" or "main" or "nav" or "aside" or "hgroup" or "search" or "blockquote" or "figure" or "figcaption" or "address" or "dl" or "dt" or "dd")
            && !AnyElement(path, e => e.Name is "ol" or "ul" or "menu" or "li" or "table" or "tr"
                or "td" or "th" or "thead" or "tbody" or "tfoot");
        void Offer(CssDeclaration declaration, bool isInline, CssSpecificity specificity, int layer)
        {
            budget.Work(declaration.Offset);
            if (declaration.Property is CssProperty.Custom or CssProperty.Pending)
            {
                (declaration.Property == CssProperty.Custom ? customDeclarations : pendingDeclarations)
                    .Add(new(declaration.Shared, declaration.Order, declaration.Offset, isInline, specificity, layer));
                return;
            }
            if (declaration.Property >= CssProperty.AnimationName)
            {
                var slot = (int)declaration.Property - (int)CssProperty.AnimationName;
                var animated = new Winner(declaration, isInline, specificity, layer);
                if (budget.UsesRevertLayer) offered.Add(animated);
                if (X.Animation[slot] is not { } known || animated.Beats(known)) X.Animation[slot] = animated;
                return;
            }
            // revert and revert-layer roll the author origin back: the winner is discarded after the
            // cascade, so nothing the element would apply needs checking here.
            if (declaration.Value is "revert" or "revert-layer")
            {
                var reverting = new Winner(declaration, isInline, specificity, layer); var current = winners[(int)declaration.Property];
                if (budget.UsesRevertLayer) offered.Add(reverting);
                if (!current.HasValue || reverting.Beats(current.Value)) winners[(int)declaration.Property] = reverting;
                return;
            }
            // all:initial|inherit|unset gives display its initial value (inline) or the parent's. The report
            // renderer lays out inline text and replaced elements inline and report panels as blocks, so
            // the keyword is accepted where it leaves the element's display as it is and fails where it
            // would turn a block into inline text or the other way round.
            if (declaration.Name == "all" && declaration.Property == CssProperty.Display)
            {
                var effective = declaration.Value switch
                {
                    "initial" or "unset" => "inline",
                    _ => path.Count > 1 && parentBox.Flex.Display == BoxDisplay.Block && !parentBox.DisplayNone
                        && (HtmlDocumentReader.Container(path[^2].Name) || HtmlDocumentReader.ParagraphTag(path[^2].Name)) ? "block"
                        : path.Count > 1 && HtmlDocumentReader.InlineTag(path[^2].Name) ? "inline" : null
                };
                var blockHost = (HtmlDocumentReader.Container(subject) || HtmlDocumentReader.ParagraphTag(subject))
                    && !AnyElement(path, e => e.Name is "ol" or "ul" or "menu" or "li" or "table" or "tr" or "td" or "th" or "thead" or "tbody" or "tfoot");
                if (HtmlDocumentReader.InlineTag(subject) && effective == "inline") return;
                if (!(effective == "inline" && subject is "img" or "svg" || effective == "block" && (blockHost || subject is "img" or "svg")))
                    throw new FactsPdfException("FPDF1201",
                        $"all:{declaration.Value} would change the display of this element to {effective ?? "the parent's"}, which the report renderer cannot lay out; all supports revert and revert-layer everywhere, and initial, inherit and unset on inline text and on blocks that inherit block display.",
                        declaration.Offset);
                declaration = CssDeclaration.Create(CssProperty.Display, "display", effective!, declaration.Important, declaration.Order, declaration.Offset);
            }
            else if (declaration.Name == "all" && declaration.Value is not ("revert" or "revert-layer")
                && AllChangesNothing(declaration, subject, parent, parentBox))
                return;
            // object-fit and object-position fit an img's pixels into its content box. Every element
            // records them, since a child may inherit them, but only an img has replaced content to
            // place (an inline svg ignores them as well).
            if (declaration.Property is CssProperty.ObjectFit or CssProperty.ObjectPosition or CssProperty.TableLayout
                or CssProperty.CounterReset or CssProperty.CounterIncrement or CssProperty.CounterSet)
            {
                var fitCandidate = new Winner(declaration, isInline, specificity); var fitOld = winners[(int)declaration.Property];
                if (!fitOld.HasValue || fitCandidate.Beats(fitOld.Value)) winners[(int)declaration.Property] = fitCandidate;
                return;
            }
            // background-clip moves the edge the background colour is painted to on a report panel,
            // paragraph or heading outside lists and tables. Elsewhere the colour is painted to the border
            // box, which is the initial value; any other clip there would be silently ignored.
            if (declaration.Property == CssProperty.BackgroundClip && !ClipHost(subject, path))
            {
                // Inline text, br and column boxes paint no background here, so no clip changes anything.
                if (declaration.Value is "border-box" or "initial" or "unset" or "inherit"
                    || HtmlDocumentReader.InlineTag(subject) || subject is "br" or "col" or "colgroup") return;
                throw new FactsPdfException("FPDF1201",
                    "background-clip padding-box and content-box are supported on report panels, paragraphs and headings outside lists and tables; elsewhere only border-box.",
                    declaration.Offset);
            }
            // content generates boxes only for ::before and ::after; on an element it changes
            // nothing (as in a browser), so the validated value is dropped there.
            if (declaration.Property == CssProperty.Content && pseudo == CssPseudoElement.None) return;
            if (TablePartDisplay(declaration) && subject is not ("img" or "svg")) return;
            // baseline is the initial value. Outside cells inline text (and a br) already sits on
            // its baseline and blocks ignore vertical-align, so resets such as
            // *{vertical-align:baseline} are validated and dropped, even on void elements.
            // A cell's baseline row alignment is not implemented (rejected below). An img or svg
            // keeps it, since it can win over another alignment of the image.
            if (declaration.Property == CssProperty.VerticalAlign && declaration.Value == "baseline"
                && subject is not ("td" or "th" or "img" or "svg"))
                return;
            // A br has no width, height or children, so box-sizing (validated when parsed) changes
            // nothing there and *{box-sizing:border-box} does not fail on a line break.
            if (declaration.Property == CssProperty.BoxSizing && subject == "br")
                return;
            // <html> and <body> lay out with no margin of their own (the page margin options frame
            // the content), and <html> with no padding, so the resets body{margin:0},
            // html,body{margin:0;padding:0} and *{margin:0} change nothing there and are validated
            // and dropped. Inline text has no box here, so a zero margin or padding and a transparent
            // background (normalize.css a{background-color:transparent}, *{margin:0;padding:0}) are
            // dropped there too. Any value that would move or paint the content still fails below.
            if (RootZeroInset(subject, declaration) || InlineZeroBox(subject, declaration)
                || TablePartMargin(subject, declaration) || UnusedZeroInset(subject, declaration))
                return;
            // orphans/widows count a block's line boxes. Inline text, br, img, hr and col hold no
            // line box of their own and no block can nest in them (FPDF1101), so a value there
            // reaches no paragraph, as in a browser: validated and dropped (*{orphans:3} passes).
            if (declaration.Property is CssProperty.Orphans or CssProperty.Widows
                && (HtmlDocumentReader.InlineTag(element.Name) || element.Name is "br" or "img" or "hr" or "col"))
                return;
            if (!allowStyling) throw new FactsPdfException("FPDF1103", "Styling this void element is not supported.", elementOffset);
            // Only existing ordinary block-flow elements can be suppressed.
            // Lists/tables/images/inline/metadata have different layout and
            // resource contracts; their display declarations fail explicitly.
            // An img or svg is an inline replaced box by default and a block box with display:block;
            // inline-block lays a replaced element out exactly like inline. display:none fails.
            if (declaration.Property == CssProperty.Display && subject is "img" or "svg")
            {
                if (declaration.Value is not ("block" or "inline" or "inline-block"))
                    throw new FactsPdfException("FPDF1201",
                        "display on img and svg supports block, inline and inline-block.", declaration.Offset);
            }
            else if (declaration.Property == CssProperty.Display && declaration.Value is "flex" or "grid")
            {
                if (declaration.Value == "grid" && subject == "html")
                    throw new FactsPdfException("FPDF1201", "display:grid is not supported on html.", declaration.Offset);
                if (subject is not ("div" or "section" or "article" or "header" or "footer" or "main" or "nav"
                        or "aside" or "hgroup" or "search" or "blockquote" or "figure" or "figcaption" or "address"
                        or "dl" or "dt" or "dd" or "li" or "body" or "html")
                    || AnyElement(path, e => e.Name is "table" or "tr" or "td" or "th" or "thead" or "tbody" or "tfoot")
                        && !InCellList(path))
                    throw new FactsPdfException("FPDF1201",
                        "display:flex and display:grid are supported only on report panels, list items and body (flex also on html) outside tables, except on items of a list in a table cell.", declaration.Offset);
            }
            else if (declaration.Property == CssProperty.Display &&
                (subject is not ("p" or "pre" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6"
                    or "div" or "section" or "article" or "header" or "footer" or "main" or "nav" or "aside" or "hgroup" or "search" or "blockquote" or "figure" or "figcaption" or "address" or "dl" or "dt" or "dd") ||
                 AnyElement(path, e => e.Name is "ol" or "ul" or "menu" or "li" or "table" or "tr"
                    or "td" or "th" or "thead" or "tbody" or "tfoot")))
                throw new FactsPdfException("FPDF1201",
                    "display is supported only on ordinary report blocks outside lists and tables.", declaration.Offset);
            // Outlines use report-panel paint geometry only, never table,
            // list, image or inline formatting contexts.
            if (declaration.Property is (CssProperty.OutlineWidth or CssProperty.OutlineStyle
                    or CssProperty.OutlineColor or CssProperty.OutlineOffset) &&
                (subject is not ("div" or "section" or "article" or "header" or "footer" or "main" or "nav" or "aside" or "hgroup" or "search" or "blockquote" or "figure" or "figcaption" or "address" or "dl" or "dt" or "dd") ||
                 AnyElement(path, e => e.Name is "ol" or "ul" or "menu" or "li" or "table" or "tr"
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
                (subject is not ("p" or "pre" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6"
                    or "div" or "section" or "article" or "header" or "footer" or "main" or "nav" or "aside" or "hgroup" or "search" or "blockquote" or "figure" or "figcaption" or "address" or "dl" or "dt" or "dd") ||
                 AnyElement(path, e => e.Name is "ol" or "ul" or "menu" or "li" or "table" or "tr"
                     or "td" or "th" or "thead" or "tbody" or "tfoot")))
                throw new FactsPdfException("FPDF1201",
                    "border-radius is supported only on ordinary report boxes outside lists and tables.", declaration.Offset);
            // Panels consume their bounded min-height/height; direct JPEG/PNG
            // images additionally consume vertical limits in ImageLayout.Resolve.
            // Check every matching declaration, not only the cascade winner.
            if (declaration.Property is (CssProperty.MinHeight or CssProperty.Height or CssProperty.MaxHeight))
            {
                var imageHeight = subject is "img" or "svg";
                var panelHeight = declaration.Property != CssProperty.MaxHeight
                    && subject is ("div" or "section" or "article" or "header" or "footer" or "main" or "nav" or "aside" or "hgroup" or "search" or "blockquote" or "figure" or "figcaption" or "address" or "dl" or "dt" or "dd")
                    && !AnyElement(path, e => e.Name is "ol" or "ul" or "menu" or "li" or "table" or "tr"
                        or "td" or "th" or "thead" or "tbody" or "tfoot");
                if (!imageHeight && !panelHeight)
                    throw new FactsPdfException("FPDF1201",
                        "Height limits apply only to scoped report panels or direct images (max-height: images only).", declaration.Offset);
            }
            // break-inside:avoid moves a block box laid out by the page flow whole to the next page.
            // A table row and header group never split when they fit a fresh fragment, so tr and thead
            // keep avoid as a check in TableLayout. Cell content breaks by lines and units that never
            // move whole, and an inline box has no block extent to keep.
            if (declaration.Property == CssProperty.BreakInside && subject is not ("tr" or "thead"))
            {
                if (subject is not ("div" or "section" or "article" or "header" or "footer" or "main" or "nav" or "aside" or "hgroup" or "search" or "blockquote" or "figure" or "figcaption" or "address" or "dl" or "dt" or "dd"
                        or "p" or "pre" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6" or "ul" or "ol" or "menu" or "li" or "table"))
                    throw new FactsPdfException("FPDF1201",
                        "break-inside is supported on panels, paragraphs, headings, lists, list items, tables and table rows (tr, thead), not on this element.", declaration.Offset);
                if (AnyElement(path, e => e.Name is "td" or "th"))
                    throw new FactsPdfException("FPDF1201",
                        "break-inside is not supported inside table cells.", declaration.Offset);
            }
            if ((declaration.Property is CssProperty.ListStyleType or CssProperty.ListStylePosition) &&
                subject is not ("ol" or "ul" or "menu" or "li") && uaSubject != "summary")
                throw new FactsPdfException("FPDF1201",
                    "List marker properties are supported only on ol, ul, li and the first summary of a details.", declaration.Offset);
            if (declaration.Property == CssProperty.VerticalAlign && subject is not ("img" or "svg"))
            {
                if (subject is not ("td" or "th"))
                    throw new FactsPdfException("FPDF1201",
                        "vertical-align applies only to table td/th cells and inline img/svg (elsewhere only its initial value baseline is accepted).", declaration.Offset);
                if (declaration.Value is not ("top" or "middle" or "bottom" or "inherit" or "initial" or "unset"))
                    throw new FactsPdfException("FPDF1202",
                        "Table-cell vertical-align supports only top, middle and bottom.", declaration.Offset);
            }
            // A col takes only a track width; box-sizing changes nothing there (a col has no
            // padding or borders), so *{box-sizing:border-box} is accepted on it.
            // A td/th width takes part in automatic table layout only; TableLayout rejects it
            // in a table whose columns all have explicit widths.
            if (subject == "col" && declaration.Property is not (CssProperty.Width or CssProperty.BoxSizing))
                throw new FactsPdfException("FPDF1201", $"CSS property '{declaration.Name}' is not supported on this table element.", declaration.Offset);
            // hr has physical box geometry but no text. Its local font-size
            // can scale em lengths; colour supplies currentcolor borders.
            // Never pretend that text-only declarations rendered on a void rule.
            if (subject == "hr" &&
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
                && (subject is ("tr" or "thead" or "tbody" or "tfoot")
                    || subject is ("td" or "th")
                        && declaration.Property is (CssProperty.Width or CssProperty.MinWidth or CssProperty.MaxWidth)))
                throw new FactsPdfException("FPDF1201",
                    "Relative em/rem box lengths are unsupported on table rows/groups and cell widths.", declaration.Offset);
            if (subject is "img" or "svg" && declaration.Property is (CssProperty.TextDecoration or CssProperty.TextDecorationColor or CssProperty.TextDecorationStyle or CssProperty.TextDecorationThickness or CssProperty.TextUnderlineOffset))
                throw new FactsPdfException("FPDF1201", "Text decoration paint cannot be applied to standalone images.", declaration.Offset);
            if (declaration.Property == CssProperty.TextTransform && subject is "img" or "svg")
                throw new FactsPdfException("FPDF1201", "text-transform cannot be applied to standalone images.", declaration.Offset);
            // Direct, explicitly bound images now consume these constraints through
            // their measured outer box, including direct image items in cells.
            // Table tracks and cell layouts remain separately controlled and
            // cannot silently accept sizing declarations they do not render.
            // box-sizing is supported on the table box itself (an explicit width then includes its
            // padding and borders); on columns, rows, groups and cells no width with padding or
            // borders is accepted, so it changes nothing there and universal resets pass.
            // min-width and max-width also constrain a table's used width (TableLayout).
            if (declaration.Property is CssProperty.MinWidth or CssProperty.MaxWidth &&
                (subject is "col" or "td" or "th" or "tr" or "thead" or "tbody" or "tfoot" or "caption"))
                throw new FactsPdfException("FPDF1201", "Box sizing constraints are supported only on ordinary blocks, tables and direct images.", declaration.Offset);
            // A cell paragraph keeps its own padding (the other box properties still need a block box);
            // percentages would need the paragraph's containing width, which the flat cell does not track.
            var cellPadding = cellParagraph && declaration.Property is (CssProperty.PaddingTop
                or CssProperty.PaddingRight or CssProperty.PaddingBottom or CssProperty.PaddingLeft);
            if (cellPadding && declaration.Value.EndsWith('%'))
                throw new FactsPdfException("FPDF1201",
                    $"CSS property '{declaration.Name}' does not support percentages on table cell paragraphs.", declaration.Offset);
            if (cellParagraph && ((CssDeclarations.IsBoxProperty(declaration.Property) && !zeroCellInset && !cellPadding
                    && declaration.Property != CssProperty.BoxSizing)
                || declaration.Property is CssProperty.BreakBefore or CssProperty.BreakAfter))
                throw new FactsPdfException("FPDF1201", $"CSS property '{declaration.Name}' is not supported on table cell paragraphs.", declaration.Offset);
            // Last-line alignment belongs to a paragraph's line box, never
            // a single inline run (including losing CSS declarations).
            if (declaration.Property == CssProperty.TextAlignLast &&
                HtmlDocumentReader.InlineTag(subject))
                throw new FactsPdfException("FPDF1201",
                    "text-align-last cannot be declared on inline text.", declaration.Offset);
            // Inline spans cannot independently align paragraph lines.
            // Preserve legacy left/right values, but reject logical start/end
            // just like justify: accepting them would only change one text run.
            // Do not key this off BoxTag/block: <html> inherits text alignment.
            if (declaration.Property == CssProperty.TextAlign
                && declaration.Value is ("justify" or "start" or "end")
                && HtmlDocumentReader.InlineTag(subject))
                throw new FactsPdfException("FPDF1201", "text-align:justify/start/end cannot be declared on inline text.", declaration.Offset);
            // Fail explicitly on unsupported applicability even if another declaration wins later.
            if (declaration.Property == CssProperty.BreakAfter && declaration.Value == "avoid" &&
                (!HeadingKeep.IsHeading(subject) || AnyElement(path, e => e.Name is "ol" or "ul" or "menu" or "li")))
                throw new FactsPdfException("FPDF1201", "break-after:avoid is supported only on headings outside lists.", declaration.Offset);
            // Report panels have real outside-the-border vertical margins.
            // Validate all matching declarations, even losing ones. Vertical
            // percentages and auto are not quietly reinterpreted as points;
            // Percentages are supported on report panels: their actual outside
            // spacing resolves against the containing block content WIDTH at
            // layout time, not the page height. Vertical auto still fails.
            // CSS-wide inheritance/reset is resolved for the winner below.
            // Lists, list items and tables take them as well; a table has no horizontal margin
            // geometry, so only its zero horizontal margins (dropped above) are accepted.
            var panelMargin = (subject is ("div" or "section" or "article" or "header" or "footer" or "main" or "nav" or "aside" or "hgroup" or "search" or "blockquote" or "figure" or "figcaption" or "address" or "dl" or "dt" or "dd" or "hr"
                    or "ul" or "ol" or "menu" or "li" or "body")
                    || subject == "table" && declaration.Property is (CssProperty.MarginTop or CssProperty.MarginBottom))
                && declaration.Property is (CssProperty.MarginTop or CssProperty.MarginBottom
                    or CssProperty.MarginLeft or CssProperty.MarginRight);
            if (subject == "table" && declaration.Property is (CssProperty.MarginLeft or CssProperty.MarginRight))
                throw new FactsPdfException("FPDF1201",
                    $"CSS property '{declaration.Name}' is not supported on tables except as zero; only margin-top and margin-bottom apply to a table.",
                    declaration.Offset);
            if (panelMargin && declaration.Property is (CssProperty.MarginTop or CssProperty.MarginBottom)
                && declaration.Value.EndsWith('%') &&
                AnyElement(path, e => e.Name is "ol" or "ul" or "menu" or "li" or "table" or "tr" or "td" or "th"))
                throw new FactsPdfException("FPDF1201",
                    "Percentage vertical margins are not supported on lists, list items and tables or inside them.", declaration.Offset);
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
                AnyElement(path, e => e.Name is "ol" or "ul" or "menu" or "li" or "table" or "tr" or "td" or "th"))
                throw new FactsPdfException("FPDF1201",
                    "Percentage paragraph margins are not supported inside lists or tables.", declaration.Offset);
            // Forced page breaks on a whole report section must open/close
            // at its box boundary, not be delayed until its first text leaf.
            // No break-inside, avoid for whole panels or list/table breaks.
            var panelPageBreak = declaration.Property is (CssProperty.BreakBefore or CssProperty.BreakAfter)
                && subject is ("div" or "section" or "article" or "header" or "footer" or "main" or "nav" or "aside" or "hgroup" or "search" or "blockquote" or "figure" or "figcaption" or "address" or "dl" or "dt" or "dd")
                && !AnyElement(path, e => e.Name is "ol" or "ul" or "menu" or "li" or "table" or "tr"
                    or "td" or "th" or "thead" or "tbody" or "tfoot");
            if (panelPageBreak && declaration.Value == "avoid")
                throw new FactsPdfException("FPDF1202",
                    "Report-panel page breaks support auto/page, not avoid.", declaration.Offset);
            if (!paragraph && !panelMargin && !panelPageBreak && declaration.Property is (CssProperty.MarginTop or CssProperty.MarginBottom
                or CssProperty.MarginLeft or CssProperty.MarginRight or CssProperty.BreakBefore
                or CssProperty.BreakAfter or CssProperty.TextIndent)
                && !(reportTextFlowHost && declaration.Property is CssProperty.TextIndent)
                && !(cellTextIndentHost && declaration.Property is CssProperty.TextIndent))
                throw new FactsPdfException("FPDF1201", $"CSS property '{declaration.Name}' is only supported on paragraphs/headings or scoped report panels.", declaration.Offset);
            // box-sizing changes nothing on <html> (no width or height is accepted there) or on
            // non-replaced inline text (CSS Box Sizing: width and height do not apply), so the
            // universal reset *{box-sizing:border-box} is accepted there. It is still recorded on
            // the element's box so a child's box-sizing:inherit reads it, as in a browser.
            // html lays out at the full page width, so width:100% (or auto) changes nothing there; its
            // flex container properties are accepted with display:flex (see RootFlexNoOp).
            if (subject == "html" && (RootFlexNoOp(declaration)
                || declaration.Property == CssProperty.Width && declaration.Value is "100%" or "auto" or "initial" or "unset"))
                return;
            // gap at normal (or a CSS-wide keyword) places nothing on an element that is no flex or
            // grid container, as in a browser, so resets such as *{gap:normal} are dropped there.
            if (!block && declaration.Property is CssProperty.ColumnGap or CssProperty.RowGap
                && declaration.Value is "normal" or "initial" or "unset" or "inherit")
                return;
            // In the separated border model Chrome paints no border on a row or row group (only cells and the
            // table have borders), so the declaration is validated and dropped, as it is there.
            if (!parent.CollapseBorders && subject is "tr" or "thead" or "tbody" or "tfoot"
                && declaration.Property is >= CssProperty.BorderTopWidth and <= CssProperty.BorderLeftColor)
                return;
            // A row or row group background paints under its cells (TableMarkup gives it to
            // every cell without its own background); their other box properties still fail.
            if (!block && CssDeclarations.IsBoxProperty(declaration.Property)
                // overflow and text-overflow do nothing on an inline box, as in a browser; table parts need their own geometry.
                && !(declaration.Property is CssProperty.OverflowX or CssProperty.OverflowY or CssProperty.TextOverflow
                    && subject is not ("table" or "thead" or "tbody" or "tfoot" or "tr" or "td" or "th" or "caption" or "col" or "colgroup"))
                && !(declaration.Property == CssProperty.BoxSizing && BoxSizingNoOpHost(subject))
                // The root element's background is the page canvas (DocumentRoot.CanvasColor).
                && !(declaration.Property == CssProperty.BackgroundColor && subject == "html")
                && !(declaration.Property == CssProperty.BackgroundColor && subject is "tr" or "thead" or "tbody" or "tfoot")
                // In the collapsing border model row and row group borders take part in the
                // border conflict resolution (TableBorderCollapse); the table's computed
                // border-collapse reaches them through inheritance.
                && !(parent.CollapseBorders && subject is "tr" or "thead" or "tbody" or "tfoot"
                    && declaration.Property is >= CssProperty.BorderTopWidth and <= CssProperty.BorderLeftColor))
                throw new FactsPdfException("FPDF1201", $"CSS box property '{declaration.Name}' is only supported on block elements.", declaration.Offset);
            // A hidden or collapsed table part would still paint its cells' backgrounds and borders here, and a
            // collapsed row or column must leave the table layout; neither is implemented.
            if (declaration.Property == CssProperty.Visibility && declaration.Value is "hidden" or "collapse"
                && subject is "tr" or "td" or "th" or "thead" or "tbody" or "tfoot" or "col" or "colgroup" or "caption")
                throw new FactsPdfException("FPDF1201",
                    $"visibility:{declaration.Value} is not supported on table parts; hide the whole table or its content instead.", declaration.Offset);
            var candidate = new Winner(declaration, isInline, specificity, layer); var old = winners[(int)declaration.Property];
            if (budget.UsesRevertLayer) offered.Add(candidate);
            if (!old.HasValue || candidate.Beats(old.Value)) winners[(int)declaration.Property] = candidate;
        }
        void Match(List<Rule> rules)
        {
            foreach (var rule in rules)
            {
                budget.Work(rule.Selector.Offset);
                if (rule.Selector.PseudoElement != pseudo || !rule.Selector.Matches(path, budget, Positions)) continue;
                foreach (var declaration in rule.Declarations) Offer(declaration, false, rule.Selector.Specificity, LayerOrder(rule.Layer, declaration.Important));
            }
        }
        // The width and height attributes are presentational hints: author-level declarations of
        // specificity zero that precede every rule, so any author declaration wins.
        if (widthHint is not null) Offer(widthHint, false, default, LayerOrder(null, false));
        if (heightHint is not null) Offer(heightHint, false, default, LayerOrder(null, false));
        // The user-agent sheet is the lowest origin: its winners are kept apart and only fill in the
        // properties no author declaration decides. A pseudo-element generates no user-agent style here.
        ulong userAgentMatched = 0;
        if (pseudo == CssPseudoElement.None && UserAgentStyleSheet.For(element.Name) is { } userAgentRules)
            for (var ruleIndex = 0; ruleIndex < userAgentRules.Length; ruleIndex++)
            {
                var rule = userAgentRules[ruleIndex];
                if (!rule.Selector.Matches(path, userAgentBudget)) continue;
                userAgentMatched |= 1UL << ruleIndex;
                foreach (var declaration in rule.Declarations)
                {
                    var candidate = new Winner(declaration, false, rule.Selector.Specificity, UserAgent: true);
                    var slot = UserAgentStyleSheet.Slot(declaration.Property);
                    var current = userAgentWinners[slot];
                    if (!current.HasValue || candidate.Beats(current.Value)) userAgentWinners[slot] = candidate;
                }
            }
        Match(universal);
        if (types.TryGetValue(element.Name, out var typeRules)) Match(typeRules);
        if (element.Id is not null && ids.TryGetValue(element.Id, out var idRules)) Match(idRules);
        foreach (var name in element.Classes.Names) if (classes.TryGetValue(name, out var classRules)) Match(classRules);
        if (pseudo == CssPseudoElement.None && inlineDeclarations.Declarations is { } inline)
        {
            if (inlineDeclarations.FirstOrder == 0) foreach (var d in inline) Offer(d, true, default, LayerOrder(null, d.Important));
            else
                for (var i = 0; i < inline.Length; i++)
                {
                    if (i == inlineSlots.Count) inlineSlots.Add(new CssDeclaration.Slot());
                    var d = inline[i];
                    Offer(inlineSlots[i].Retarget(d, inlineDeclarations.FirstOrder + i, d.Offset + inlineDeclarations.OffsetShift), true, default, LayerOrder(null, d.Important));
                }
        }
        if (budget.UsesVariables)
        {
            var depth = path.Count - 1;
            var parentEnvironment = depth > 0 && depth - 1 < environments.Count ? environments[depth - 1] : CssCustomEnvironment.Empty;
            var environment = customDeclarations.Count == 0 ? parentEnvironment : ResolveCustomProperties(parentEnvironment);
            if (environments.Count > depth) environments.RemoveRange(depth, environments.Count - depth);
            while (environments.Count < depth) environments.Add(CssCustomEnvironment.Empty);
            environments.Add(environment);
            var used = 0;
            // font-size first: em and a percentage in its math refer to the parent's size, and every other
            // math value refers to this element's final font-size, known once its font-size declaration won.
            X.MathParentFontSize = parent.FontSize; X.MathRootFontSize = rootFontSize;
            X.MathElementFontSize = parent.FontSize;
            for (var pass = 0; pass < 2; pass++)
            {
                if (pass == 1) X.MathElementFontSize = ElementFontSize();
                foreach (var pending in pendingDeclarations)
                {
                    if ((pending.Shared.Name == "font-size") != (pass == 0)) continue;
                    foreach (var substituted in Substitute(pending, environment, element.Name))
                    {
                        if (used == substitutionSlots.Count) substitutionSlots.Add(new CssDeclaration.Slot());
                        Offer(substitutionSlots[used++].Retarget(substituted, pending.Order, pending.Offset), pending.Inline, pending.Specificity, pending.Layer);
                    }
                }
            }
        }

        // The best declaration of the same property and importance in a layer below the rolling one (the
        // style attribute sorts with the unlayered declarations), or none.
        Winner? RollBack(Winner rolling)
        {
            Winner? best = null;
            foreach (var candidate in offered)
            {
                if (candidate.Declaration.Property != rolling.Declaration.Property || candidate.Inline
                    || candidate.Declaration.Important != rolling.Declaration.Important
                    || candidate.Layer >= rolling.Layer) continue;
                if (best is not { } old || candidate.Beats(old)) best = candidate;
            }
            return best;
        }

        // Animations show their time-zero keyframe state: declarations of the animations origin, above every
        // normal declaration and below !important ones.
        if (HasKeyframes && extras?.Animation[0] is not null && AnimationState(element.Name) is { } shown)
            foreach (var declaration in shown) Offer(declaration, true, new CssSpecificity(int.MaxValue, 0, 0), int.MaxValue);

        // The element's computed font size from the font-size declaration that won so far.
        double ElementFontSize()
        {
            Winner? size = winners[(int)CssProperty.FontSize];
            // With no author font-size the user-agent one applies (a heading's 2em, small, big).
            if (size is null && UserAgentStyleSheet.Properties.Contains(CssProperty.FontSize))
                size = userAgentWinners[UserAgentStyleSheet.Slot(CssProperty.FontSize)];
            if (size is not { } chosen || chosen.Declaration.Value is "revert" or "revert-layer")
                return defaults.FontSize;
            return CssDeclarations.Apply(defaults with { }, parent, initial, chosen.Declaration, paragraph, rootFontSize).FontSize;
        }
        // A winning revert computes the property as if the author origin declared nothing for it: the
        // user-agent defaults below apply, or inheritance. Chrome 158 also discards the presentational
        // hints (the list type attribute, the hidden attribute's display:none) for such a property.
        // revert-layer rolls back to the declarations of the layers below the declaration's own, where
        // Chrome keeps those hints, so it only replaces the winner.
        var displayReverted = false;
        for (var index = 0; index < winners.Length; index++)
        {
            while (winners[index] is { Declaration.Value: "revert-layer" } rolling) winners[index] = RollBack(rolling);
            if (winners[index] is not { Declaration.Value: "revert" or "revert-layer" } reverted) continue;
            winners[index] = null;
            if (reverted.Declaration.Value == "revert-layer") continue;
            if (index == (int)CssProperty.Display) displayReverted = true;
            if (index == (int)CssProperty.ListStyleType) listTypeHint = null;
        }
        // The user-agent declarations decide every property the author origin left alone (or reverted).
        // The type attribute's marker is a presentational hint: below the author, above this sheet.
        var userAgentProperties = UserAgentStyleSheet.Properties;
        for (var slot = 0; slot < userAgentProperties.Length; slot++)
        {
            var index = (int)userAgentProperties[slot];
            if (winners[index] is null && userAgentWinners[slot] is { } userAgent
                && !(index == (int)CssProperty.ListStyleType && listTypeHint is not null))
                winners[index] = userAgent;
        }
        // defaults is the shared parent style, a tag-defaults copy shared with earlier siblings, or
        // (defaultsOwned) a private copy the reader made for this element. Text declarations are
        // applied to one private copy, made on the first one if the reader did not make it; shared
        // styles are never assigned (see TextStyle).
        var style = defaults;
        var styleOwned = defaultsOwned;
        // Every element starts from the CSS initial box: the user-agent sheet's declarations (the
        // quote and list insets, the table cell padding, the list marker) arrive below as the lowest
        // cascade origin, and author CSS overrides each side normally. CSS-wide initial resets to
        // this zero-margin ordinary box.
        var box = BoxStyle.Initial;
        // Other elements propagate the inherited marker type, including li and nested panels. Copy
        // the box only when a marker setting actually differs. The HTML type attribute is a
        // presentational hint: it replaces the user-agent default and loses to every author declaration.
        // A details' first summary starts with the disclosure triangle (list-style: inside disclosure-closed).
        var markerKind = listTypeHint ?? (uaSubject == "summary" ? ListMarkerKind.Disclosure : parentBox.MarkerKind);
        // All of this element's declarations are applied to one private copy, made on the first
        // change; shared boxes (Initial, the parent's) are never assigned (see BoxStyle).
        var owned = false;
        BoxStyle Own()
        {
            if (!owned) { box = box with { }; owned = true; }
            return box;
        }
        // A summary's marker is inside (list-style: inside disclosure-closed).
        var markerInside = uaSubject == "summary" || parentBox.MarkerInside;
        if (box.MarkerKind != markerKind || box.MarkerInside != markerInside)
        {
            Own().MarkerKind = markerKind;
            box.MarkerInside = markerInside;
        }
        // An element whose box declarations, parent box, element name, root font size and computed
        // font size and colour match an earlier element's computes the same box, so it shares that
        // published (immutable) box instead of a private copy: every cell of a table column usually
        // repeats one style. FontSize and Color precede every box property in the enum, and no other
        // declaration changes them, so they are final at the first box declaration. A cached box was
        // stored only after its whole computation succeeded, so skipped box declarations cannot fail.
        var boxChecked = false; var boxShared = false; var boxKey = 0;
        // An element whose text declarations all come from the user-agent sheet, on a shared parent
        // style, computes the same text style as an earlier sibling of its name: it shares that style
        // (the user-agent sheet gives every paragraph the same margins, every b the same weight).
        var userAgentTextOnly = !defaultsOwned; var userAgentTextStyle = false;
        for (var index = 0; index < winners.Length && userAgentTextOnly; index++)
            if (winners[index] is { } text && !WritesBox(subject, text.Declaration.Property)
                && text.Declaration.Property != CssProperty.Content && !text.UserAgent)
                userAgentTextOnly = false;
        var userAgentTextKey = new UserAgentTextKey(defaults, subject, rootFontSize, userAgentMatched);
        if (userAgentTextOnly && userAgentTextStyles.TryGetValue(userAgentTextKey, out var sharedText))
        {
            style = sharedText;
            userAgentTextStyle = true;
        }
        for (var index = 0; index < winners.Length; index++)
        {
            if (winners[index] is not { } winner) continue;
            var declaration = winner.Declaration;
            if (declaration.Property == CssProperty.Content || declaration.Property >= CssProperty.AnimationName) continue;
            if (userAgentTextStyle && !WritesBox(subject, declaration.Property)) continue;
            if (WritesBox(subject, declaration.Property))
            {
                if (boxShared) continue;
                if (!boxChecked)
                {
                    boxChecked = true;
                    boxKey = BoxKey(uaSubject, subject, parentBox, rootFontSize, style.FontSize, style.Color, listTypeHint, index);
                    if (computedBoxes.TryGetValue(boxKey, out var computed) && SameBoxInputs(computed, uaSubject, subject,
                        parentBox, rootFontSize, style.FontSize, style.Color, listTypeHint, index))
                    {
                        box = computed.Box;
                        boxShared = true;
                        continue;
                    }
                }
            }
            if (declaration.Property == CssProperty.Display && subject is "img" or "svg")
                Own().DisplayBlock = declaration.Value == "block";
            else if (declaration.Property == CssProperty.Display)
            {
                Own().DisplayNone = declaration.Value == "none";
                var display = declaration.Value switch { "flex" => BoxDisplay.Flex, "grid" => BoxDisplay.Grid, _ => BoxDisplay.Block };
                if (box.Flex.Display != display) box.Flex = box.Flex with { Display = display };
            }
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
            else if (declaration.Property == CssProperty.VerticalAlign && subject is "img" or "svg")
                Own().InlineAlign = declaration.Value is "inherit" ? parentBox.InlineAlign
                    : declaration.Value is "initial" or "unset" ? default
                    : InlineVerticalAlign.TryParse(declaration.Value, out var inlineAlign, style.FontSize, rootFontSize)
                        ? inlineAlign
                        : throw new InvalidOperationException("Unvalidated inline vertical alignment.");
            else if (declaration.Property == CssProperty.VerticalAlign)
                Own().VerticalAlign = declaration.Value switch
                {
                    "inherit" => parentBox.VerticalAlign,
                    "initial" or "unset" or "top" => CellVerticalAlign.Top,
                    "middle" => winner.UserAgent ? CellVerticalAlign.DefaultMiddle : CellVerticalAlign.Middle,
                    "bottom" => CellVerticalAlign.Bottom,
                    _ => throw new InvalidOperationException("Unvalidated table-cell vertical alignment.")
                };
            else if (subject is ("div" or "section" or "article" or "header" or "footer" or "main" or "nav" or "aside" or "hgroup" or "search" or "blockquote" or "figure" or "figcaption" or "address" or "dl" or "dt" or "dd")
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
            else if (declaration.Property == CssProperty.BackgroundClip)
            {
                // The background shorthand resets the clip on every element that sets one: skip the
                // private box copy when nothing changes.
                var clip = declaration.Value switch
                {
                    "inherit" => parentBox.BackgroundClip,
                    "padding-box" => BackgroundClip.PaddingBox,
                    "content-box" => BackgroundClip.ContentBox,
                    _ => BackgroundClip.BorderBox
                };
                if (box.BackgroundClip != clip) Own().BackgroundClip = clip;
            }
            else if (CssDeclarations.IsBoxProperty(declaration.Property))
                CssDeclarations.ApplyBox(Own(), parentBox, declaration, rootFontSize, style.FontSize,
                    style.Color);
            else if (subject is ("div" or "section" or "article" or "header" or "footer" or "main" or "nav" or "aside" or "hgroup" or "search" or "blockquote" or "figure" or "figcaption" or "address" or "dl" or "dt" or "dd" or "hr"
                    or "ul" or "ol" or "menu" or "li" or "table" or "body")
                && declaration.Property is (CssProperty.MarginTop or CssProperty.MarginBottom))
                CssDeclarations.ApplyPanelVerticalMargin(Own(), parentBox, declaration,
                    rootFontSize, style.FontSize);
            else
            {
                // html font-size:rem refers to the initial size; other rem
                // text metrics use the newly computed root font on html.
                // FontSize precedes these properties in the fixed enum.
                var computedRoot = subject == "html"
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
                    path[^2].Name is ("div" or "section" or "article" or "header" or "footer" or "main" or "nav" or "aside" or "hgroup" or "search" or "blockquote" or "figure" or "figcaption" or "address" or "dl" or "dt" or "dd");
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
                    paragraph || winner.UserAgent || reportTextFlowHost && declaration.Property is CssProperty.TextIndent
                        || cellTextIndentHost && declaration.Property is CssProperty.TextIndent, computedRoot);
            }
        }
        if (box.BackgroundClip == BackgroundClip.ContentBox && (box.PaddingTop.IsPercent || box.PaddingRight.IsPercent
                || box.PaddingBottom.IsPercent || box.PaddingLeft.IsPercent))
            throw new FactsPdfException("FPDF1201",
                "background-clip:content-box with a percentage padding is not supported.",
                winners[(int)CssProperty.BackgroundClip]?.Declaration.Offset ?? elementOffset);
        // A rounded content box shrinks every corner radius by the padding on both axes; that is a
        // circle only when the four paddings agree.
        if (box.BackgroundClip == BackgroundClip.ContentBox && box.HasRoundedCorners
            && (box.PaddingTop != box.PaddingRight || box.PaddingTop != box.PaddingBottom || box.PaddingTop != box.PaddingLeft))
            throw new FactsPdfException("FPDF1201",
                "background-clip:content-box on a rounded box requires equal padding on all four sides.",
                winners[(int)CssProperty.BackgroundClip]?.Declaration.Offset ?? elementOffset);
        if (userAgentTextOnly && !userAgentTextStyle && styleOwned && userAgentTextStyles.Count < MaxUserAgentTextStyles)
            userAgentTextStyles[userAgentTextKey] = style;
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
            computedBoxes[boxKey] = new ComputedBox(subject, parentBox, rootFontSize, style.FontSize, style.Color,
                listTypeHint, BoxWrites(subject), winners[(int)CssProperty.VerticalAlign] is { UserAgent: true }, box);
        // The fieldset's border is the sheet's 2px #f0f0f0 as a groove (two bands, the outer one darker,
        // as Chrome paints ThreeDFace), and the iframe's is its inset: the sheet gives the two tones as
        // border colours, which only fit black text and the sheet's own colours.
        if (uaSubject == "fieldset")
        {
            BorderEdge Groove(BorderEdge edge, CssProperty style) => winners[(int)style] is { UserAgent: true } && edge.Pattern == BorderPattern.Solid
                ? edge with { Pattern = BorderPattern.Groove } : edge;
            box = box with
            {
                BorderTop = Groove(box.BorderTop, CssProperty.BorderTopStyle), BorderRight = Groove(box.BorderRight, CssProperty.BorderRightStyle),
                BorderBottom = Groove(box.BorderBottom, CssProperty.BorderBottomStyle), BorderLeft = Groove(box.BorderLeft, CssProperty.BorderLeftStyle)
            };
            if (box.HasRoundedCorners && box.BorderTop.Pattern == BorderPattern.Groove)
                throw new FactsPdfException("FPDF1201",
                    "Rounded report boxes require an equally thick, single-colour solid border on all four sides.", elementOffset);
        }
        else if (uaSubject == "iframe-borderless" && winners[(int)CssProperty.BorderTopStyle] is { UserAgent: true })
            box = box with { BorderTop = BorderEdge.Initial, BorderRight = BorderEdge.Initial, BorderBottom = BorderEdge.Initial, BorderLeft = BorderEdge.Initial };
        else if (uaSubject == "iframe" && winners[(int)CssProperty.BorderTopStyle] is { UserAgent: true } && box.BorderTop.EffectiveWidth > 0
            && (style.Color != new Rgb(0, 0, 0) || winners[(int)CssProperty.BorderTopColor] is { UserAgent: false }))
            throw new FactsPdfException("FPDF1201",
                "An iframe's default inset border is drawn only with black text and no border-color; set border-style or border:none to draw it another way.",
                elementOffset);
        // The HTML hidden attribute is the user-agent rule [hidden]{display:none}, below every author
        // declaration, so an author display:block still shows the element. Applied after the box cache
        // is filled and on a fresh copy, so a cached or shared box never carries it to another element.
        if (hidden && !displayReverted && winners[(int)CssProperty.Display] is null && !box.DisplayNone)
            box = box with { DisplayNone = true };
        return (style, box);
    }

    // html{display:flex} only centres a body narrower than the page in a browser window; the
    // layout accepts it where the body fills the page width, where every flex container property
    // places nothing (one item, no free space, the line as tall as the item).
    private static bool ReportPanel(string subject) =>
        subject is "div" or "section" or "article" or "header" or "footer" or "main" or "nav" or "aside" or "hgroup" or "search"
            or "blockquote" or "figure" or "figcaption" or "address" or "dl" or "dt" or "dd";

    // all:initial|inherit|unset names every property, including those that do nothing on this element.
    // Returns true when the keyword leaves the element exactly as it is without the declaration, so the
    // property's own applicability checks need not run: the property does not show on this element
    // (list markers on anything but a list, box properties of inline text) or its value is already what
    // the keyword gives (an inherited property on inline text that unset or inherit keeps inheriting,
    // initial where the parent holds the initial value). It fails where the keyword would change what
    // the element shows and the renderer cannot (an inherited indent, the parent's padding or border on
    // inline text), and returns false for properties the cascade applies on the element as usual.
    private static bool AllChangesNothing(CssDeclaration declaration, string subject, TextStyle parent, BoxStyle parentBox)
    {
        var property = declaration.Property;
        var value = declaration.Value;
        // A list marker shows only on a list item, and a list item lies directly in a list.
        if (property is CssProperty.ListStyleType or CssProperty.ListStylePosition
            && subject is not ("ol" or "ul" or "menu" or "li")) return true;
        // Height limits apply to report panels and images only; elsewhere even auto and none are rejected.
        // initial and unset give those, and inherit gives the parent's limit, which must then be none.
        if (property is CssProperty.Height or CssProperty.MinHeight or CssProperty.MaxHeight
            && !(subject is "img" or "svg" || property != CssProperty.MaxHeight && ReportPanel(subject)))
        {
            var inherited = property switch
            {
                CssProperty.Height => parentBox.Height, CssProperty.MinHeight => parentBox.MinHeight, _ => parentBox.MaxHeight
            };
            if (value == "inherit" && inherited is not null)
                throw new FactsPdfException("FPDF1201", $"all:inherit would copy the parent's {declaration.Name} limit to an element that takes none.", declaration.Offset);
            return true;
        }
        // Outlines paint on report panels only; a parent's outline copied by inherit would show nowhere else.
        if (property is CssProperty.OutlineWidth or CssProperty.OutlineStyle or CssProperty.OutlineColor or CssProperty.OutlineOffset
            && !ReportPanel(subject))
        {
            if (value == "inherit" && parentBox.Outline.EffectiveWidth > 0)
                throw new FactsPdfException("FPDF1201", "all:inherit would copy the parent's outline to an element that cannot paint one.", declaration.Offset);
            return true;
        }
        // vertical-align places table cell content and inline images; it does nothing on a block or text.
        if (property == CssProperty.VerticalAlign && subject is not ("td" or "th" or "img" or "svg")
            && !HtmlDocumentReader.InlineTag(subject)) return true;
        if (!HtmlDocumentReader.InlineTag(subject)) return false;
        switch (property)
        {
            case CssProperty.FontSize or CssProperty.FontWeight or CssProperty.FontStyle or CssProperty.Color
                or CssProperty.LineHeight or CssProperty.TextDecoration or CssProperty.TextDecorationColor
                or CssProperty.TextDecorationStyle or CssProperty.TextDecorationThickness
                or CssProperty.TextUnderlineOffset or CssProperty.TextTransform or CssProperty.LetterSpacing
                or CssProperty.WordSpacing or CssProperty.Hyphens or CssProperty.Visibility:
                return false;
            case CssProperty.TextAlign or CssProperty.TextAlignLast or CssProperty.TextIndent or CssProperty.WhiteSpaceCollapse
                or CssProperty.TextWrapMode or CssProperty.TabSize or CssProperty.LineBreak or CssProperty.OverflowWrap or CssProperty.WordBreak or CssProperty.Orphans or CssProperty.Widows
                or CssProperty.BorderCollapse:
            {
                // Inherited text flow of the block the text sits in: the span keeps inheriting it.
                if (value is "unset" or "inherit") return true;
                var initial = property switch
                {
                    CssProperty.TextAlign => parent.Alignment == TextAlignment.Left,
                    CssProperty.TextAlignLast => parent.LastLineAlignment is null,
                    CssProperty.TextIndent => parent.FirstLineIndent.Value == 0,
                    CssProperty.WhiteSpaceCollapse => !parent.PreserveLineBreaks && !parent.PreserveSpaces && !parent.BreakSpaces,
                    CssProperty.TextWrapMode => !parent.NoWrap,
                    CssProperty.TabSize => parent.TabSize == 8,
                    CssProperty.LineBreak => parent.LineBreak == LineBreakStrictness.Normal,
                    CssProperty.OverflowWrap => !parent.BreakWords && !parent.BreakAnywhere,
                    CssProperty.WordBreak => !parent.BreakAll && !parent.BreakWordsByWordBreak && !parent.KeepAll,
                    CssProperty.Orphans => parent.Orphans == 0,
                    CssProperty.Widows => parent.Widows == 0,
                    _ => !parent.CollapseBorders
                };
                if (initial) return true;
                throw new FactsPdfException("FPDF1201",
                    $"all:initial on inline text would reset the text flow it inherits ({declaration.Name} resets it here), which the report renderer cannot apply to a span.",
                    declaration.Offset);
            }
            default:
                // Every other property is not inherited, so inline text starts at its initial value, which
                // initial and unset give. inherit would copy the parent's padding, border, margin sides,
                // background or outline onto the text, which the renderer cannot draw.
                if (value != "inherit") return true;
                if (parentBox.HasLayoutPaint || parentBox.Outline.EffectiveWidth > 0 || parentBox.HasItems
                    || parentBox.PaddingLeft.Value != 0 || parentBox.PaddingRight.Value != 0
                    || parentBox.PaddingTop.Value != 0 || parentBox.PaddingBottom.Value != 0
                    || parentBox.MarginLeft.Value != 0 || parentBox.MarginRight.Value != 0)
                    throw new FactsPdfException("FPDF1201",
                        "all:inherit would copy the parent's box properties (padding, borders, margins, background, outline or flex item properties) onto inline text, which the report renderer cannot draw.",
                        declaration.Offset);
                return true;
        }
    }

    // Elements whose background colour is painted by the report panel and paragraph box painter, which
    // clips it to the padding or content box.
    private static bool ClipHost(string subject, IReadOnlyList<CssElement> path) =>
        subject is ("p" or "pre" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6"
            or "div" or "section" or "article" or "header" or "footer" or "main" or "nav" or "aside" or "hgroup" or "search" or "blockquote" or "figure" or "figcaption" or "address" or "dl" or "dt" or "dd")
        && !AnyElement(path, e => e.Name is "ol" or "ul" or "menu" or "li" or "table" or "tr"
            or "td" or "th" or "thead" or "tbody" or "tfoot");

    private static bool RootFlexNoOp(CssDeclaration declaration) =>
        declaration.Property is CssProperty.JustifyContent or CssProperty.AlignItems or CssProperty.FlexDirection
            or CssProperty.FlexWrap or CssProperty.ColumnGap or CssProperty.RowGap;

    private CssCustomEnvironment ResolveCustomProperties(CssCustomEnvironment parent)
    {
        var winning = new Dictionary<string, Deferral>(StringComparer.Ordinal);
        foreach (var candidate in customDeclarations)
            if (!winning.TryGetValue(candidate.Shared.Name, out var old) || candidate.Beats(old))
                winning[candidate.Shared.Name] = candidate;
        var declared = new Dictionary<string, (CssCustomValue Value, int Offset)>(winning.Count, StringComparer.Ordinal);
        foreach (var (name, winner) in winning) declared.Add(name, ((CssCustomValue)winner.Shared.Payload!, winner.Offset));
        return CssVariables.Resolve(parent, declared, budget, syntaxLimits);
    }

    /// <summary>
    /// The declarations a var() declaration compiles to on an element with these computed custom
    /// properties. A reference without a value or fallback, a CSS-wide keyword produced by
    /// substitution, or a substituted value the property does not accept fails with the error the
    /// literal value would give (FPDF1202 for values), located at the var() declaration: the
    /// renderer fails rather than treating the declaration as unset as a browser would. Non-strict
    /// mode treats a declaration that is invalid at computed-value time (a reference without a value
    /// or fallback, or a substituted value that is invalid per the specifications) as unset, as a
    /// browser does, and reports the strict error as a warning; a substituted CSS-wide keyword or a
    /// valid value the renderer does not implement still fails.
    /// </summary>
    private CssDeclaration[] Substitute(Deferral pending, CssCustomEnvironment environment, string element)
    {
        var payload = (CssPendingValue)pending.Shared.Payload!;
        var key = (pending.Shared, environment, payload.Inline ? element : null);
        if (substitutions.TryGetValue(key, out var cached))
        {
            if (cached.Warning is not null) budget.Warnings?.Add(cached.Warning);
            return cached.Declarations;
        }
        var name = payload.Node.Name;
        var values = new List<CssSyntaxComponent>();
        var shape = new CssSubstitution();
        var cacheable = true;
        if (!CssVariables.Substitute(payload.Node.Values, values, ref shape, pending.Offset, 1, budget, syntaxLimits,
                (string reference, out CssVariableValue value) => environment.TryGet(reference, budget, pending.Offset, out value), out var missing))
        {
            var error = new FactsPdfException("FPDF1202",
                $"var({missing}) in '{name}' has no value and no fallback: the custom property is not defined on this element or its ancestors, is initial, is part of a reference cycle or failed its own substitution.",
                pending.Offset);
            return budget.Warnings is null ? throw error : Unset(pending, payload, element, key, error);
        }
        // Math functions and viewport lengths resolve against this element's font sizes and the page
        // area, so a declaration holding one is compiled for every element (never shared by environment).
        if (CssMath.Contains(values))
        {
            cacheable = false;
            values = CssMath.Resolve(values, MathContext(name), name);
        }
        // A substituted CSS-wide keyword applies as that keyword (Chrome 158), so it compiles like the
        // literal declaration below.
        CssDeclaration[] compiled;
        try
        {
            compiled = CssSyntaxAdapter.CompileKnownDeclarations([payload.Node with { Values = values.ToArray() }], budget,
                payload.HostSelectors, payload.Inline ? element : null, fixedOrder: 1, recover: false);
        }
        catch (FactsPdfException error) when (error.Code != "FPDF1205")
        {
            var reason = error.Message.StartsWith(error.Code + ": ", StringComparison.Ordinal)
                ? error.Message[(error.Code.Length + 2)..] : error.Message;
            var substituted = new FactsPdfException(error.Code,
                $"'{name}: {CssVariables.Text(values)}' after var() substitution: {reason}", pending.Offset);
            if (budget.Warnings is null || error.Code is not ("FPDF1201" or "FPDF1202")
                || !CssSpecValidity.InvalidDeclaration(name, CssSyntaxAdapter.CanonicalName(name.ToLowerInvariant()), values))
                throw substituted;
            return Unset(pending, payload, element, key, substituted);
        }
        if (cacheable) Cache(key, compiled, null, shape.Nodes);
        return compiled;
    }

    private CssMathContext MathContext(string property)
    {
        var (width, height) = Viewport();
        return property switch
        {
            "font-size" => new(X.MathParentFontSize, X.MathRootFontSize, width, height, X.MathParentFontSize),
            "line-height" => new(X.MathElementFontSize, X.MathRootFontSize, width, height, X.MathElementFontSize),
            _ => new(X.MathElementFontSize, X.MathRootFontSize, width, height, null)
        };
    }

    // The page area (page size less margins) that vw, vh, vmin and vmax are percentages of in print,
    // as Chrome resolves them against the @page size and margins.
    private (double Width, double Height) Viewport()
    {
        if (X.Viewport is { } known) return known;
        var page = PageRules is { } rules
            ? rules.Apply(options, new TextStyle(X.MathRootFontSize, 1.2, new Rgb(0, 0, 0), TextAlignment.Left))
            : options;
        var sides = page.SideMargins ?? new PageMargins(page.Margin, page.Margin, page.Margin, page.Margin);
        return (X.Viewport = (page.PageWidth - sides.Left - sides.Right, page.PageHeight - sides.Top - sides.Bottom)).Value;
    }

    private void Cache((CssDeclaration, CssCustomEnvironment, string?) key, CssDeclaration[] compiled, FactsPdfException? warning, long nodes)
    {
        if (substitutions.Count >= MaxSubstitutions || substitutionNodes + nodes > syntaxLimits.MaxNodes) return;
        substitutions[key] = (compiled, warning);
        substitutionNodes += nodes;
    }

    // A declaration invalid at computed-value time computes as unset (CSS Custom Properties 1,
    // section 3.1): it still wins the cascade, and its longhands take their inherited or initial value.
    private CssDeclaration[] Unset(Deferral pending, CssPendingValue payload, string element,
        (CssDeclaration, CssCustomEnvironment, string?) key, FactsPdfException warning)
    {
        var unset = new CssTokenComponent(new CssSyntaxToken(CssSyntaxTokenKind.Ident, "unset", "unset", new CssSourceSpan(pending.Offset, 0)));
        var compiled = CssSyntaxAdapter.CompileKnownDeclarations([payload.Node with { Values = [unset] }], budget,
            payload.HostSelectors, payload.Inline ? element : null, fixedOrder: 1, recover: false);
        budget.Warnings!.Add(warning);
        Cache(key, compiled, warning, 0);
        return compiled;
    }

    /// <summary>
    /// The author declarations matching an element inside an svg (the last of
    /// <paramref name="path"/>), by the source offset of the first one of each kind that reaches
    /// SVG rendering, or -1. SVG content takes no CSS box, font or text layout, so every other
    /// declaration a browser would apply there (the margin, padding and box-sizing of universal
    /// resets, fonts, text alignment) changes nothing and is ignored. color reaches the paint
    /// through currentColor, display can hide content, and width/height set a rect's geometry; a
    /// declaration whose value holds var() counts as the property it declares. Custom properties
    /// declared there fail: SVG paint attributes resolve var() against the svg element's own
    /// computed custom properties (<see cref="SvgColor"/>).
    /// </summary>
    internal (int Color, int Display, int Size) SvgContent(IReadOnlyList<CssElement> path, InlineStyle inline)
    {
        var element = path[^1];
        int color = -1, display = -1, size = -1;
        void Note(CssDeclaration declaration, int offset)
        {
            budget.Work(offset);
            var property = declaration.Property;
            if (property == CssProperty.Custom)
                throw new FactsPdfException("FPDF1201", "Custom properties declared on SVG content elements are not supported; declare them on the svg element or an ancestor.", offset);
            if (property == CssProperty.Pending && declaration.Payload is CssPendingValue pending)
                property = pending.Node.Name.ToLowerInvariant() switch
                {
                    "color" => CssProperty.Color,
                    "display" => CssProperty.Display,
                    "width" or "height" => CssProperty.Width,
                    _ => property,
                };
            switch (property)
            {
                case CssProperty.Color when color < 0: color = offset; break;
                case CssProperty.Display when display < 0: display = offset; break;
                case CssProperty.Width or CssProperty.Height when size < 0: size = offset; break;
            }
        }
        void Match(List<Rule> rules)
        {
            foreach (var rule in rules)
            {
                budget.Work(rule.Selector.Offset);
                if (!rule.Selector.Matches(path, budget, Positions)) continue;
                foreach (var declaration in rule.Declarations) Note(declaration, declaration.Offset);
            }
        }
        Match(universal);
        if (types.TryGetValue(element.Name, out var typeRules)) Match(typeRules);
        if (element.Id is not null && ids.TryGetValue(element.Id, out var idRules)) Match(idRules);
        foreach (var name in element.Classes.Names) if (classes.TryGetValue(name, out var classRules)) Match(classRules);
        if (inline.Declarations is { } declarations)
            foreach (var declaration in declarations) Note(declaration, declaration.Offset + inline.OffsetShift);
        return (color, display, size);
    }

    /// <summary>
    /// An SVG paint or color presentation attribute value read as a CSS colour, with the same
    /// syntax and limits as the color property: named colours, #rgb/#rrggbb (opaque alpha only),
    /// rgb()/rgba() and hsl()/hsla() with alpha exactly 1. CSS-wide keywords and !important are
    /// not attribute values. As in Chrome, var() is substituted from the computed custom
    /// properties of the svg element, which is at <paramref name="svgDepth"/> on the element path
    /// (SVG content declares none of its own; see <see cref="SvgContent"/>).
    /// </summary>
    internal Rgb SvgColor(string value, int offset, int svgDepth)
    {
        var nodes = ParseSource("color:" + value, offset, declarationsOnly: true).Declarations;
        var compiled = CssSyntaxAdapter.CompileKnownDeclarations(nodes, budget);
        if (compiled.Length == 1 && compiled[0].Property == CssProperty.Pending && !compiled[0].Important)
        {
            var environment = svgDepth < environments.Count ? environments[svgDepth] : CssCustomEnvironment.Empty;
            compiled = Substitute(new Deferral(compiled[0].Shared, compiled[0].Order, compiled[0].Offset, false, default),
                environment, "svg");
        }
        if (compiled.Length != 1 || compiled[0].Property != CssProperty.Color || compiled[0].Important
            || compiled[0].Value is "inherit" or "initial" or "unset" or "currentcolor")
            throw new FactsPdfException("FPDF1103", "SVG colour attributes take a single CSS colour value.", offset);
        return InlineCss.Color(compiled[0].Value, offset);
    }

    // A zero margin or padding on <html>: its used insets are already zero. CSS-wide keywords compute
    // to zero too (the initial value is 0). body has the user-agent margin of 8px, so a zero margin
    // there is a real declaration.
    private static bool RootZeroInset(string element, CssDeclaration declaration)
    {
        var margin = declaration.Property is CssProperty.MarginTop or CssProperty.MarginRight
            or CssProperty.MarginBottom or CssProperty.MarginLeft;
        var padding = declaration.Property is CssProperty.PaddingTop or CssProperty.PaddingRight
            or CssProperty.PaddingBottom or CssProperty.PaddingLeft;
        if (!(element == "html" && (margin || padding))) return false;
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
        // A colour with zero alpha (#rgb0, #rrggbb00, rgba(..., 0) lowered to hex) paints nothing either.
        if (declaration.Property == CssProperty.BackgroundColor)
            return declaration.Value == "transparent" || declaration.Value is ['#', .. var hex]
                && (hex.Length == 4 && hex[3] == '0' || hex.Length == 8 && hex.EndsWith("00", StringComparison.Ordinal));
        return declaration.Property is CssProperty.MarginTop or CssProperty.MarginRight or CssProperty.MarginBottom
                or CssProperty.MarginLeft or CssProperty.PaddingTop or CssProperty.PaddingRight
                or CssProperty.PaddingBottom or CssProperty.PaddingLeft
            && CssBoxValues.TryLength(declaration.Value, out var length, out _, rootFontSize: 16,
                allowRem: true, elementFontSize: 16, allowEm: true) && length.Value == 0;
    }

    // Report stylesheets restate the table-part display values. A value that names what the table
    // already lays out is accepted and dropped in the cascade: the single thead row repeats on every
    // fragment (table-header-group), tbody and tfoot rows are placed once in source order
    // (table-row-group), and rows and cells are table-row and table-cell. It is accepted only when
    // the declaration can reach nothing else: a style attribute of that element, or a rule whose
    // every selector names that element as its subject. Otherwise the syntax adapter rejects it while
    // parsing, with the message, location and order every other display value has.
    // table-footer-group would repeat the footer at the foot of every fragment, which tfoot rows
    // never do, so it fails.
    internal static string? TablePartDisplayError(string value, CssSelector[]? selectors, string? element)
    {
        if (element is not null) return TablePartDisplayError(value, element);
        if (selectors is not { Length: > 0 }) return UnsupportedDisplay;
        foreach (var selector in selectors)
        {
            var error = selector.Rightmost.Type is { } type ? TablePartDisplayError(value, type) : UnsupportedDisplay;
            if (error is not null) return error;
        }
        return null;
    }

    private const string UnsupportedDisplay = "Report display supports only block, flex, grid and none.";

    private static string? TablePartDisplayError(string value, string element)
    {
        var host = element.ToLowerInvariant();
        if ((host, value) is ("thead", "table-header-group") or ("tbody" or "tfoot", "table-row-group")
            or ("tr", "table-row") or ("td" or "th", "table-cell") or ("caption", "table-caption")
            // An img or svg is an inline replaced box already, and inline-block lays it out the same.
            or ("img" or "svg" or "canvas" or "video" or "iframe", "inline" or "inline-block"))
            return null;
        return (host, value) is ("tfoot", "table-footer-group")
            ? "tfoot rows render once after the body rows (table-row-group); a repeating table-footer-group is not supported."
            : UnsupportedDisplay;
    }

    private static bool TablePartDisplay(CssDeclaration declaration) =>
        declaration.Property == CssProperty.Display && declaration.Value is not ("none" or "block" or "flex" or "grid");

    // A zero margin is the initial value on table rows, row groups, columns, column groups and
    // cells (where CSS 2.1 section 8.3 margins do not apply at all), so *{margin:0} changes nothing
    // there and is dropped. Nonzero values still fail: the table parts keep rejecting declarations
    // they do not render. List items take real margins and are not dropped here.
    private static bool TablePartMargin(string element, CssDeclaration declaration)
    {
        if (declaration.Property is not (CssProperty.MarginTop or CssProperty.MarginRight
            or CssProperty.MarginBottom or CssProperty.MarginLeft)) return false;
        var wide = declaration.Value is "initial" or "unset";
        var length = CssBoxValues.TryLength(declaration.Value, out var value, out _, rootFontSize: 16,
            allowRem: true, elementFontSize: 16, allowEm: true);
        return element is "tr" or "td" or "th" or "thead" or "tbody" or "tfoot" or "col" or "colgroup" or "caption"
            && (wide || length && value.Value == 0);
    }

    // A table has no horizontal margin geometry (its zero horizontal margins change nothing) and an svg
    // takes none; rows, row groups, columns and column groups take no padding (CSS 2.1 section 8.4); a br
    // lays out no box at all. So the rest of *{margin:0;padding:0} changes nothing there and is dropped.
    // inherit could carry a parent panel's inset, and nonzero values would need real geometry, so
    // both still fail. Lists and the vertical margins of a table are real boxes and are not dropped.
    private static bool UnusedZeroInset(string element, CssDeclaration declaration)
    {
        var margin = declaration.Property is CssProperty.MarginTop or CssProperty.MarginRight
            or CssProperty.MarginBottom or CssProperty.MarginLeft;
        var padding = declaration.Property is CssProperty.PaddingTop or CssProperty.PaddingRight
            or CssProperty.PaddingBottom or CssProperty.PaddingLeft;
        var horizontal = declaration.Property is CssProperty.MarginLeft or CssProperty.MarginRight;
        if (!(margin && (element is "br" or "svg" || element == "table" && horizontal)
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
        || element is ("div" or "section" or "article" or "header" or "footer" or "main" or "nav" or "aside" or "hgroup" or "search" or "blockquote" or "figure" or "figcaption" or "address" or "dl" or "dt" or "dd")
            && property is (CssProperty.BreakBefore or CssProperty.BreakAfter)
        || CssDeclarations.IsBoxProperty(property)
        || element is ("div" or "section" or "article" or "header" or "footer" or "main" or "nav" or "aside" or "hgroup" or "search" or "blockquote" or "figure" or "figcaption" or "address" or "dl" or "dt" or "dd" or "hr"
                or "ul" or "ol" or "menu" or "li" or "table" or "body")
            && property is (CssProperty.MarginTop or CssProperty.MarginBottom);

    private int BoxKey(string element, string subject, BoxStyle parentBox, double rootFontSize, double fontSize, Rgb color,
        ListMarkerKind? listTypeHint, int first)
    {
        var hash = new HashCode();
        hash.Add(element);
        hash.Add(listTypeHint);
        hash.Add(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(parentBox));
        hash.Add(rootFontSize);
        hash.Add(fontSize);
        hash.Add(color);
        for (var index = first; index < winners.Length; index++)
            if (winners[index] is { } winner && WritesBox(subject, winner.Declaration.Property))
            {
                hash.Add(winner.Declaration.Property);
                hash.Add(winner.Declaration.Value);
                hash.Add(winner.UserAgent);
            }
        return hash.ToHashCode();
    }

    private bool SameBoxInputs(ComputedBox computed, string element, string subject, BoxStyle parentBox, double rootFontSize,
        double fontSize, Rgb color, ListMarkerKind? listTypeHint, int first)
    {
        if (!string.Equals(computed.Element, element, StringComparison.Ordinal) || !ReferenceEquals(computed.ParentBox, parentBox)
            || !computed.RootFontSize.Equals(rootFontSize) || !computed.FontSize.Equals(fontSize) || computed.Color != color
            || computed.ListTypeHint != listTypeHint
            || computed.UserAgentVerticalAlign != winners[(int)CssProperty.VerticalAlign] is { UserAgent: true }) return false;
        var writes = computed.Writes; var count = 0;
        for (var index = first; index < winners.Length; index++)
        {
            if (winners[index] is not { } winner || !WritesBox(subject, winner.Declaration.Property)) continue;
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
