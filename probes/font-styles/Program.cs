using System.Text.Json;
using FactsPDF;

// Verification host calls the public .NET API directly, independently of CLI parsing.
// Arguments: input.html output.pdf font-manifest.json subset|full [latin-only|courier]
if (args.Length is < 4 or > 5 || args[3] is not ("subset" or "full")
    || args.Length == 5 && args[4] is not ("latin-only" or "courier")) return 2;
using var manifest = JsonDocument.Parse(File.ReadAllText(args[2]));
var roles = manifest.RootElement.GetProperty("roles");
PdfFont[] Load(string role) => args.ElementAtOrDefault(4) == "courier" && role != "italic" ? []
    : roles.GetProperty(role).EnumerateArray().Take(args.ElementAtOrDefault(4) == "latin-only" ? 1 : 8)
        .Select(path => PdfFont.LoadTrueType(File.ReadAllBytes(path.GetString()!))).ToArray();
var options = new PdfOptions { Fonts = Load("normal"), BoldFonts = Load("bold"),
    ItalicFonts = Load("italic"), BoldItalicFonts = Load("boldItalic"), SubsetFonts = args[3] == "subset" };
using var output = new MemoryStream();
var result = PdfConverter.Convert(File.ReadAllText(args[0]), output, options);
File.WriteAllBytes(args[1], output.ToArray());
Console.WriteLine($"{{\"pages\":{result.PageCount},\"bytes\":{result.BytesWritten},\"dynamicCodeSupported\":{System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported.ToString().ToLowerInvariant()}}}");
return 0;
