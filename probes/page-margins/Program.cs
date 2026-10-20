using System.Text.Json;
using FactsPDF;

// Verification-only public API host, independent of CLI option parsing.
// input output manifest courier|fonts subset|full on|off header footer
if (args.Length != 8 || args[3] is not ("courier" or "fonts") ||
    args[4] is not ("subset" or "full") || args[5] is not ("on" or "off")) return 2;
try
{
    using var manifest = JsonDocument.Parse(File.ReadAllText(args[2]));
    PdfFont[] Load(string role) => args[3] == "courier" ? [] : manifest.RootElement.GetProperty("roles")
        .GetProperty(role).EnumerateArray().Select(path => PdfFont.LoadTrueType(File.ReadAllBytes(path.GetString()!))).ToArray();
    var options = new PdfOptions { Fonts = Load("normal"), BoldFonts = Load("bold"), ItalicFonts = Load("italic"),
        BoldItalicFonts = Load("boldItalic"), SubsetFonts = args[4] == "subset", PageNumbers = args[5] == "on",
        HeaderText = args[6], FooterText = args[7] };
    using var output = new MemoryStream();
    var result = PdfConverter.Convert(File.ReadAllText(args[0]), output, options);
    File.WriteAllBytes(args[1], output.ToArray());
    Console.WriteLine(JsonSerializer.Serialize(new { pages = result.PageCount, bytes = result.BytesWritten,
        dynamicCodeSupported = System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported }));
    return 0;
}
catch (FactsPdfException ex) { Console.Error.WriteLine(ex.Message); return 3; }
