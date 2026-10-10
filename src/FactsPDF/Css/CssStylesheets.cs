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
    private readonly Winner?[] winners = new Winner?[8];
    private readonly CssBudget budget;

    private CssStylesheets(PdfOptions options, CancellationToken token)
    {
        if (options.MaxCssCharacters < 1 || options.MaxCssSelectors < 1 || options.MaxCssDeclarations < 1 || options.MaxCssMatchOperations < 1)
            throw new ArgumentOutOfRangeException(nameof(options), "CSS budgets must be positive.");
        budget = new(options, token);
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
                sheet.ParseRules(CssTokens.Read(item.Value, item.Offset, token));
            }
            else if (item.Kind == HtmlTokenKind.Start)
            {
                if (++elements > options.MaxElements) throw new FactsPdfException("FPDF1002", "Element limit exceeded.", item.Offset);
                if (item.Value == "style") ValidateStyleElement(item);
                else if (item.Attributes!.TryGetValue("style", out var css))
                {
                    sheet.budget.Characters(css.Length, item.Offset); var i = 0;
                    sheet.inline.Add(item.Offset, CssDeclarations.Parse(CssTokens.Read(css, item.Offset, token), ref i, false, sheet.budget));
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
    private void ParseRules(List<CssToken> tokens)
    {
        var i = 0;
        while (tokens[i].Kind != CssTokenKind.End)
        {
            budget.Token.ThrowIfCancellationRequested();
            if (tokens[i].Is("@")) throw new FactsPdfException("FPDF1204", "CSS at-rules and imports are not supported.", tokens[i].Offset);
            var selectors = new List<CssSelector> { CssSelector.Parse(tokens, ref i, budget) };
            while (tokens[i].Is(",")) { i++; selectors.Add(CssSelector.Parse(tokens, ref i, budget)); }
            if (!tokens[i].Is("{")) throw CssTokens.Invalid("Expected a CSS declaration block.", tokens[i].Offset);
            i++; var declarations = CssDeclarations.Parse(tokens, ref i, true, budget);
            foreach (var selector in selectors)
            {
                var rule = new Rule(selector, declarations); var last = selector.Rightmost;
                if (last.Ids.Length > 0) Add(ids, last.Ids[0], rule);
                else if (last.Classes.Length > 0) Add(classes, last.Classes[0], rule);
                else if (last.Type is not null) Add(types, last.Type, rule);
                else universal.Add(rule);
            }
        }
    }
    private static void Add(Dictionary<string, List<Rule>> index, string key, Rule rule)
    { if (!index.TryGetValue(key, out var list)) index.Add(key, list = []); list.Add(rule); }

    internal TextStyle Compute(TextStyle defaults, TextStyle parent, TextStyle initial, IReadOnlyList<CssElement> path,
        bool paragraph, int elementOffset, bool allowStyling = true)
    {
        Array.Clear(winners); var element = path[^1];
        void Offer(CssDeclaration declaration, bool isInline, CssSpecificity specificity)
        {
            budget.Work(declaration.Offset);
            if (!allowStyling) throw new FactsPdfException("FPDF1103", "Styling this void element is not supported.", elementOffset);
            // Fail explicitly on unsupported applicability even if another declaration wins later.
            if (!paragraph && declaration.Property is CssProperty.MarginTop or CssProperty.MarginBottom or CssProperty.BreakBefore or CssProperty.BreakAfter)
                throw new FactsPdfException("FPDF1201", $"CSS property '{declaration.Name}' is only supported on paragraphs/headings.", declaration.Offset);
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
        foreach (var winner in winners)
            if (winner.HasValue) style = CssDeclarations.Apply(style, parent, initial, winner.Value.Declaration, paragraph);
        return style;
    }
}
