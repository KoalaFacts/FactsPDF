using System.Text.Json;
using FactsPDF.CssSyntax;

// Test-only projection of FactsPDF's independent CSS syntax tree.
// Never interpret, fetch or render a CSS resource.
if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: FactsPDF.SyntaxProbe <fixture.json>");
    return 2;
}

var source = JsonDocument.Parse(File.ReadAllText(args[0]));
var results = new List<ProbeResult>();
var limits = new CssSyntaxLimits();
foreach (var item in source.RootElement.EnumerateArray())
{
    var id = item.GetProperty("id").GetString()!;
    var context = item.GetProperty("context").GetString()!;
    var css = item.GetProperty("css").GetString()!;
    var text = CssSourceText.Create(css, 0, limits);
    if (context == "stylesheet")
    {
        var parsed = CssSyntaxParser.ParseStylesheet(text, limits);
        results.Add(new ProbeResult(id, new SheetProjection(ProjectRules(parsed.Rules))));
    }
    else if (context == "inline")
    {
        var parsed = CssSyntaxParser.ParseDeclarations(text, limits);
        results.Add(new ProbeResult(id, new InlineProjection(ProjectDeclarations(parsed.Declarations))));
    }
    else throw new InvalidOperationException("Unsupported fixture context " + context);
}
Console.WriteLine(JsonSerializer.Serialize(results, new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = true
}));
return 0;

static DeclProjection[] ProjectDeclarations(IEnumerable<CssDeclarationNode> declarations)
    => declarations.Select(d => new DeclProjection(d.Name, d.Important)).ToArray();

static RuleProjection[] ProjectRules(IEnumerable<CssRuleNode> rules)
    => rules.Select(rule =>
    {
        if (rule is CssQualifiedRuleNode q)
            return new RuleProjection("style",
                ProjectDeclarations(q.Contents.OfType<CssDeclarationNode>()),
                ProjectRules(q.Contents.OfType<CssRuleNode>()));
        if (rule is CssAtRuleNode a)
            return new RuleProjection("@" + a.Name.ToLowerInvariant(),
                ProjectDeclarations(a.Contents?.OfType<CssDeclarationNode>() ?? []),
                ProjectRules(a.Contents?.OfType<CssRuleNode>() ?? []));
        throw new InvalidOperationException("Unknown CSS syntax node.");
    }).ToArray();

internal sealed record DeclProjection(string Name, bool Important);
internal sealed record RuleProjection(string Kind, DeclProjection[] Declarations, RuleProjection[] Children);
internal sealed record InlineProjection(DeclProjection[] Declarations);
internal sealed record SheetProjection(RuleProjection[] Rules);
internal sealed record ProbeResult(string Id, object Data);
