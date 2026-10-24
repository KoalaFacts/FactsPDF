using System.Text.Json;
using FactsPDF;

// Direct public API example. Inputs/resources are explicit; no CLI code is used.
// input output font-manifest jpeg on|off [negative-mode]
if (args.Length is < 5 or > 6 || args[4] is not ("on" or "off")) return 2;
using var manifest = JsonDocument.Parse(File.ReadAllText(args[2]));
PdfFont[] Load(string role) => manifest.RootElement.GetProperty("roles").GetProperty(role)
    .EnumerateArray().Select(path => PdfFont.LoadTrueType(File.ReadAllBytes(path.GetString()!))).ToArray();
var options = new PdfOptions { Fonts = Load("normal"), BoldFonts = Load("bold"), ItalicFonts = Load("italic"),
    BoldItalicFonts = Load("boldItalic"), SubsetFonts = true,
    Images = new Dictionary<string, PdfImage> { ["quality-chart"] = PdfImage.LoadImage(File.ReadAllBytes(args[3])) },
    PageNumbers = args[4] == "on", HeaderText = args[4] == "on" ? "FactsPDF | Quality review" : null,
    FooterText = args[4] == "on" ? "Internal copy" : null };
var html = File.ReadAllText(args[0]);
var mode = args.Length == 6 ? args[5] : "positive";
// tall-row input is authored by the oracle with one 80pt/line-height:10 atomic line.
// An accumulated multiline body can now continue, so it is no longer a refusal.
var code = mode switch { "missing-image" => "FPDF1604", "missing-bold" => "FPDF1507",
    "wide-row" or "tall-row" or "long-footer" => "FPDF1302", "oversize-footer" => "FPDF1001",
    "page-limit" => "FPDF1303", "output-limit" => "FPDF1401", "display-limit" => "FPDF1401", "positive" => null,
    _ => throw new ArgumentException("Unknown negative mode") };
options = mode switch { "missing-image" => options with { Images = new Dictionary<string, PdfImage>() },
    "missing-bold" => options with { BoldFonts = [] }, "long-footer" => options with { FooterText = new string('W', 100) },
    "oversize-footer" => options with { FooterText = new string('W', 1025) },
    "page-limit" => options with { MaxPages = 1 }, "output-limit" => options with { MaxOutputBytes = 1 },
    "display-limit" => options with { MaxDisplayCommands = 1 }, _ => options };
using var output = new MemoryStream();
if (code is not null) { output.Write("sentinel"u8); output.Position = 2; }
try
{
    var result = PdfConverter.Convert(html, output, options);
    if (code is not null) throw new Exception("Expected " + code);
    File.WriteAllBytes(args[1], output.ToArray());
    Console.WriteLine(JsonSerializer.Serialize(new { pages = result.PageCount, bytes = result.BytesWritten,
        dynamicCodeSupported = System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported }));
}
catch (FactsPdfException error) when (code is not null)
{
    if (error.Code != code || output.Position != 2 || !output.ToArray().AsSpan().SequenceEqual("sentinel"u8))
        throw new Exception("Wrong diagnostic or modified caller stream", error);
    Console.WriteLine(JsonSerializer.Serialize(new { mode, code, outputUnchanged = true, positionUnchanged = true }));
}
return 0;
