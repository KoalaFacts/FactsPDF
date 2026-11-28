using FactsPDF.CssSyntax;

namespace FactsPDF;

/// <summary>
/// The user-agent stylesheet: the subset of Chrome's html.css (the HTML Standard's rendering
/// section) for the elements FactsPDF supports. It is parsed and compiled once, with the engine's own
/// CSS parser, on first use, and is the lowest origin of the cascade: every author rule, style
/// attribute and presentational hint wins over it, and a <c>revert</c> that wins computes the
/// property as this sheet declares it (see <see cref="CssStylesheets"/>).
/// </summary>
/// <remarks>
/// Not in the sheet, because the document model gives it to every element by construction: the
/// <c>display</c> of each element (blocks, list items, table parts, inline text), the centring of
/// <c>th</c> and <c>caption</c> (the user-agent <c>-internal-center</c>, which defers to an inherited
/// alignment, see <see cref="HtmlDocumentReader"/>), <c>quotes</c> on <c>q</c> and the metadata
/// elements <c>head</c>, <c>title</c>, <c>script</c> and <c>template</c> that render nothing.
/// </remarks>
internal static class UserAgentStyleSheet
{
    internal sealed record Rule(CssSelector Selector, CssDeclaration[] Declarations);

    private const string Source = """
        body{margin:8px}
        p,pre,dl{margin-top:1em;margin-bottom:1em}
        pre{white-space:pre}
        h1{font-size:2em;font-weight:bold;margin-top:.67em;margin-bottom:.67em}
        h2{font-size:1.5em;font-weight:bold;margin-top:.83em;margin-bottom:.83em}
        h3{font-size:1.17em;font-weight:bold;margin-top:1em;margin-bottom:1em}
        h4{font-weight:bold;margin-top:1.33em;margin-bottom:1.33em}
        h5{font-size:.83em;font-weight:bold;margin-top:1.67em;margin-bottom:1.67em}
        h6{font-size:.67em;font-weight:bold;margin-top:2.33em;margin-bottom:2.33em}
        blockquote,figure{margin:1em 40px}
        dd{margin-left:40px}
        address,i,cite,em,var,dfn{font-style:italic}
        b,strong,th{font-weight:bold}
        small{font-size:smaller}
        sub{vertical-align:sub;font-size:smaller}
        sup{vertical-align:super;font-size:smaller}
        mark{background-color:yellow;color:black}
        big{font-size:larger}
        u,ins{text-decoration:underline}
        s,del{text-decoration:line-through}
        a{color:#0000ee;text-decoration:underline}
        abbr[title]{text-decoration-line:underline;text-decoration-style:dotted}
        ul,menu{list-style-type:disc}
        ol{list-style-type:decimal}
        ul,ol,menu{margin-top:1em;margin-bottom:1em;padding-left:40px}
        ul ul,ol ul,menu ul,ul menu,ol menu,menu menu{list-style-type:circle}
        ul ul,ul ol,ul menu,ol ul,ol ol,ol menu,menu ul,menu ol,menu menu{margin-top:0;margin-bottom:0}
        hr{color:gray;margin-top:.5em;margin-bottom:.5em;border:1px solid;border-color:#9a9a9a #eee #eee #9a9a9a}
        table{border-collapse:separate;box-sizing:border-box;text-indent:initial}
        td,th{padding:1px;vertical-align:inherit}
        thead,tbody,tfoot,tr{vertical-align:middle}
        """;

    // The rules by the type their selector ends in. Built once by the first conversion that needs it;
    // the compiled rules are immutable and shared by every conversion and thread.
    private static readonly Lazy<Compiled> compiled = new(Compile, LazyThreadSafetyMode.ExecutionAndPublication);

    private sealed record Compiled(Dictionary<string, Rule[]> Rules, CssProperty[] Properties, Dictionary<CssProperty, int> Slots);

    /// <summary>The properties the sheet declares, so a conversion keeps one slot for each rather than one per property.</summary>
    internal static CssProperty[] Properties => compiled.Value.Properties;

    /// <summary>The index of a declared property in <see cref="Properties"/>.</summary>
    internal static int Slot(CssProperty property) => compiled.Value.Slots[property];

    /// <summary>The options a conversion's user-agent matching budget is built from: it never fails.</summary>
    internal static readonly PdfOptions Unlimited = new()
    {
        MaxCssSelectors = int.MaxValue,
        MaxCssDeclarations = int.MaxValue,
        MaxCssMatchOperations = int.MaxValue,
    };

    /// <summary>The rules whose selector ends in <paramref name="element"/>, in source order, or null.</summary>
    internal static Rule[]? For(string element) => compiled.Value.Rules.GetValueOrDefault(element);

    private static Compiled Compile()
    {
        var budget = new CssBudget(Unlimited, default);
        var limits = new CssSyntaxLimits();
        var result = CssSyntaxParser.ParseStylesheet(CssSourceText.Create(Source, 0, limits, null), limits);
        if (result.Diagnostics.Count > 0)
            throw new InvalidOperationException("The user-agent stylesheet is malformed: " + result.Diagnostics[0].Message);
        var byType = new Dictionary<string, List<Rule>>(StringComparer.Ordinal);
        foreach (var node in result.Rules)
        {
            var qualified = (CssQualifiedRuleNode)node;
            var selectors = CssSyntaxAdapter.CompileSelectors(qualified.Prelude, budget, node.Span.Start);
            var declarations = CssSyntaxAdapter.CompileKnownDeclarations(
                [.. qualified.Contents.OfType<CssDeclarationNode>()], budget, hostSelectors: selectors);
            foreach (var selector in selectors)
            {
                var type = selector.Rightmost.Type
                    ?? throw new InvalidOperationException("Every user-agent rule selects a type.");
                if (!byType.TryGetValue(type, out var list)) byType.Add(type, list = []);
                list.Add(new Rule(selector, declarations));
            }
        }
        var properties = byType.Values.SelectMany(list => list).SelectMany(rule => rule.Declarations)
            .Select(declaration => declaration.Property).Distinct().Order().ToArray();
        return new(byType.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.Ordinal), properties,
            properties.Select((property, index) => (property, index)).ToDictionary(pair => pair.property, pair => pair.index));
    }
}
