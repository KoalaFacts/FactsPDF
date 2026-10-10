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
    private readonly Winner?[] winners = new Winner?[8];
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
                    var declarations = sheet.ParseSource(css, item.Offset, declarationsOnly: true).Declarations;
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
    private CssSyntaxResult ParseSource(string css, int offset, bool declarationsOnly)
    {
        CssSyntaxAdapter.EnforceStrictUnclosedComments(css, offset);
        try
        {
            var source = CssSourceText.Create(css, offset, syntaxLimits);
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
