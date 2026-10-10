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
        WriteRules(writer, parsed.Rules);
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

static void WriteRules(Utf8JsonWriter writer, IEnumerable<CssRuleNode> rules)
{
    writer.WriteStartArray();
    foreach (var rule in rules)
    {
        writer.WriteStartObject();
        if (rule is CssQualifiedRuleNode qualified)
        {
            writer.WriteString("kind", "style");
            writer.WritePropertyName("declarations");
            WriteDeclarations(writer, qualified.Contents.OfType<CssDeclarationNode>());
            writer.WritePropertyName("children");
            WriteRules(writer, qualified.Contents.OfType<CssRuleNode>());
        }
        else if (rule is CssAtRuleNode at)
        {
            writer.WriteString("kind", "@" + at.Name.ToLowerInvariant());
            writer.WritePropertyName("declarations");
            WriteDeclarations(writer, at.Contents?.OfType<CssDeclarationNode>() ?? []);
            writer.WritePropertyName("children");
            WriteRules(writer, at.Contents?.OfType<CssRuleNode>() ?? []);
        }
        else throw new InvalidOperationException("Unknown CSS syntax rule node.");
        writer.WriteEndObject();
    }
    writer.WriteEndArray();
}
