using FactsPDF.CssSyntax;

namespace FactsPDF.Tests;

/// <summary>
/// Runs the selector engine over a parsed document without rendering it: which elements with an id a selector
/// matches, in document order. The expected sets in the selector tests were produced by Chrome's
/// querySelectorAll on the same markup.
/// </summary>
internal static class SelectorOracle
{
    /// <summary>The ids of the elements the selector matches, or null when the selector is invalid or unsupported.</summary>
    internal static string[]? Matched(string selector, string html, params (string? Prefix, string Uri)[] namespaces)
    {
        var table = new CssNamespaces();
        foreach (var (prefix, uri) in namespaces) table.Declare(prefix, uri);
        var limits = new CssSyntaxLimits();
        var parsed = CssSyntaxParser.ParseStylesheet(CssSourceText.Create(selector + "{}", 0, limits), limits);
        if (parsed.Rules.Count != 1 || parsed.Rules[0] is not CssQualifiedRuleNode rule) return null;
        var options = new PdfOptions { MaxCssMatchOperations = 50_000_000 };
        var budget = new CssBudget(options, CancellationToken.None);
        CssSelector[] selectors;
        try { selectors = CssSyntaxAdapter.CompileSelectors(rule.Prelude, budget, 0, nested: false, table); }
        catch (FactsPdfException) { return null; }
        // A top-level nesting selector stands for the root element, as :scope does in a stylesheet.
        if (selectors.Any(s => s.NestingCount > 0)) selectors = CssSelector.ResolveNesting(selectors, CssSelector.RootParent, budget, 0);

        var tokens = new List<HtmlToken>();
        CssStylesheets.Collect(html, options, tokens, CancellationToken.None);
        var positions = HtmlElementPositions.Build(tokens, null, CancellationToken.None);
        var found = new List<string>();
        for (var t = 0; t < tokens.Count; t++)
        {
            if (tokens[t].Kind != HtmlTokenKind.Start || !tokens[t].Attributes!.TryGetValue("id", out var id)) continue;
            var chain = new List<int>();
            for (var node = t; node >= 0; node = positions.Parent(node)) chain.Add(node);
            chain.Reverse();
            var path = chain.Select(node => positions.Element(node)).ToList();
            if (selectors.Any(s => s.Matches(path, budget, positions))) found.Add(id);
        }
        return found.ToArray();
    }
}
