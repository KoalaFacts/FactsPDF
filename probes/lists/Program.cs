using System.Text.Json;
using FactsPDF;

// Verification-only direct public API host: input.html output.pdf font-manifest.json.
if (args.Length != 3) return 2;
using var manifest = JsonDocument.Parse(File.ReadAllText(args[2]));
var fonts = manifest.RootElement.GetProperty("roles").GetProperty("normal").EnumerateArray()
    .Select(path => PdfFont.LoadTrueType(File.ReadAllBytes(path.GetString()!))).ToArray();
var options = new PdfOptions { Fonts = fonts, SubsetFonts = true };
var html = File.ReadAllText(args[0]);
byte[] Convert(string input, PdfOptions settings)
{
    using var output = new MemoryStream();
    PdfConverter.Convert(input, output, settings);
    return output.ToArray();
}
var pdf = Convert(html, options);
File.WriteAllBytes(args[1], pdf);
var errors = new List<object>();
foreach (var (name, input, settings, expected) in new[]
{
    ("third-level", "<ol><li><ul><li><ol></ol></li></ul></li></ol>", options, "FPDF1101"),
    ("orphan", "<li>A</li>", options, "FPDF1101"),
    ("attribute", "<ol start='2'><li>A</li></ol>", options, "FPDF1103"),
    ("list-css", "<ul style='list-style-type:square'><li>A</li></ul>", options, "FPDF1201"),
    ("marker-style", "<ol><li style='font-weight:bold'><p style='font-weight:normal'>A</p></li></ol>", options, "FPDF1507"),
    ("gutter", "<ol style='width:10pt'><li>A</li></ol>", options, "FPDF1302"),
    ("pages", html, options with { MaxPages = 1 }, "FPDF1303"),
    ("commands", "<ul><li>A</li><li>B</li></ul>", options with { MaxDisplayCommands = 3 }, "FPDF1401"),
    ("bytes", "<ol><li>A</li></ol>", options with { MaxOutputBytes = 100 }, "FPDF1401")
})
{
    using var output = new MemoryStream(); output.Write("sentinel"u8); output.Position = 2;
    try { PdfConverter.Convert(input, output, settings); throw new Exception("Expected failure: " + name); }
    catch (FactsPdfException error)
    {
        if (error.Code != expected || output.Position != 2 || !output.ToArray().AsSpan().SequenceEqual("sentinel"u8))
            throw new Exception("Wrong diagnostic or changed output: " + name, error);
        errors.Add(new { name, code = error.Code, sourceOffset = error.SourceOffset, outputUnchanged = true });
    }
}
using (var output = new MemoryStream())
{
    output.Write("sentinel"u8); output.Position = 2;
    try { PdfConverter.Convert(html, output, options, new CancellationToken(true)); throw new Exception("Expected cancellation"); }
    catch (OperationCanceledException)
    {
        if (output.Position != 2 || !output.ToArray().AsSpan().SequenceEqual("sentinel"u8)) throw new Exception("Cancellation changed output");
        errors.Add(new { name = "cancelled", code = "cancelled", sourceOffset = -1, outputUnchanged = true });
    }
}
if (!Convert(html, options).AsSpan().SequenceEqual(pdf)) throw new Exception("Success/failure/success differs");
Console.WriteLine(JsonSerializer.Serialize(new { bytes = pdf.Length, errors, recoveryByteEquality = true }));
return 0;
