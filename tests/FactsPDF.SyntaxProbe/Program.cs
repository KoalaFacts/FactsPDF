using FactsPDF;
using System.Text.Json;
using FactsPDF.CssSyntax;

// Test-only, AOT-safe structural projection. The browser oracle independently
// projects real Chrome CSSOM to the exact same JSON shape.
if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: FactsPDF.SyntaxProbe <fixture.json>");
    return 2;
}

using var source = JsonDocument.Parse(File.ReadAllText(args[0]));
using var writer = new Utf8JsonWriter(Console.OpenStandardOutput(),
    new JsonWriterOptions { Indented = true });
writer.WriteStartArray();
var limits = new CssSyntaxLimits();
foreach (var item in source.RootElement.EnumerateArray())
{
    var id = item.GetProperty("id").GetString()!;
    var context = item.GetProperty("context").GetString()!;
    var css = item.GetProperty("css").GetString()!;
    var text = CssSourceText.Create(css, 0, limits);
    writer.WriteStartObject();
    writer.WriteString("id", id);
    writer.WriteStartObject("data");
    if (context == "stylesheet")
    {
        var parsed = CssSyntaxParser.ParseStylesheet(text, limits);
        writer.WritePropertyName("rules");
        WriteStylesheet(writer, parsed.Rules);
    }
    else if (context == "inline")
    {
        var parsed = CssSyntaxParser.ParseDeclarations(text, limits);
        writer.WritePropertyName("declarations");
        WriteDeclarations(writer, parsed.Declarations);
    }
    else throw new InvalidOperationException("Unsupported fixture context " + context);
    writer.WriteEndObject();
    writer.WriteEndObject();
}
writer.WriteEndArray();
writer.Flush();
return 0;

static void WriteDeclarations(Utf8JsonWriter writer, IEnumerable<CssDeclarationNode> declarations)
{
    writer.WriteStartArray();
    foreach (var d in declarations)
    {
        writer.WriteStartObject();
        writer.WriteString("name", d.Name);
        writer.WriteBoolean("important", d.Important);
        writer.WriteEndObject();
    }
    writer.WriteEndArray();
}

// A stylesheet as the browser's CSSOM holds it: a rule with an invalid selector list, a late or malformed
// @namespace rule and an unknown at-rule are dropped, so they are left out here too (the selector grammar
// and the @namespace placement rule are the engine's own, the ones its stylesheet compiler applies).
static void WriteStylesheet(Utf8JsonWriter writer, IEnumerable<CssRuleNode> rules)
{
    var namespaces = new CssNamespaces();
    var open = true;
    var kept = new List<CssRuleNode>();
    foreach (var rule in rules)
    {
        if (rule is CssAtRuleNode { Name: var name } at && name.Equals("namespace", StringComparison.OrdinalIgnoreCase))
        {
            if (CssNamespaces.TryDeclare(at, open, namespaces)) kept.Add(at);
            continue;
        }
        if (open && !(rule is CssAtRuleNode preamble && (preamble.Name.Equals("charset", StringComparison.OrdinalIgnoreCase)
                || preamble.Name.Equals("import", StringComparison.OrdinalIgnoreCase)
                || preamble.Name.Equals("layer", StringComparison.OrdinalIgnoreCase) && preamble.Contents is null)))
            open = false;
        kept.Add(rule);
    }
    WriteRules(writer, kept, nested: false, namespaces, top: true);
}

// A top-level @namespace rule reaches here only when it was valid; @charset is no rule and @import is not
// allowed in the constructed style sheet the browser projection reads.
static bool Dropped(CssRuleNode rule, bool nested, CssNamespaces namespaces, bool top)
{
    if (rule is CssAtRuleNode at)
        return CssSpecValidity.UnknownAtRule(at.Name) || at.Name.Equals("charset", StringComparison.OrdinalIgnoreCase)
            || at.Name.Equals("import", StringComparison.OrdinalIgnoreCase)
            || !top && at.Name.Equals("namespace", StringComparison.OrdinalIgnoreCase);
    var qualified = (CssQualifiedRuleNode)rule;
    var budget = new CssBudget(new PdfOptions(), CancellationToken.None);
    try { CssSelectorParser.Compile(qualified.Prelude, budget, qualified.Span.Start, nested, namespaces); }
    catch (FactsPdfException error) { return CssSelectorParser.IsInvalid(error); }
    return false;
}

static void WriteRules(Utf8JsonWriter writer, IEnumerable<CssRuleNode> rules, bool nested, CssNamespaces namespaces, bool top = false)
{
    writer.WriteStartArray();
    foreach (var rule in rules)
    {
        if (Dropped(rule, nested, namespaces, top)) continue;
        writer.WriteStartObject();
        if (rule is CssQualifiedRuleNode qualified)
        {
            writer.WriteString("kind", "style");
            writer.WritePropertyName("declarations");
            WriteDeclarations(writer, qualified.Contents.OfType<CssDeclarationNode>());
            writer.WritePropertyName("children");
            WriteRules(writer, qualified.Contents.OfType<CssRuleNode>(), nested: true, namespaces);
        }
        else if (rule is CssAtRuleNode at)
        {
            writer.WriteString("kind", "@" + at.Name.ToLowerInvariant());
            writer.WritePropertyName("declarations");
            WriteDeclarations(writer, at.Contents?.OfType<CssDeclarationNode>() ?? []);
            writer.WritePropertyName("children");
            WriteRules(writer, at.Contents?.OfType<CssRuleNode>() ?? [], nested, namespaces);
        }
        else throw new InvalidOperationException("Unknown CSS syntax rule node.");
        writer.WriteEndObject();
    }
    writer.WriteEndArray();
}
