using System.Text.Json;
using FactsPDF;

// Verification-only public API host. Optional courier mode retains authored fixture metrics.
if (args.Length is < 3 or > 4 || args.Length == 4 && args[3] != "courier") return 2;
using var manifest = JsonDocument.Parse(File.ReadAllText(args[2]));
PdfFont[] Fonts(string role) => args.Length == 4 ? [] : manifest.RootElement.GetProperty("roles").GetProperty(role)
    .EnumerateArray().Select(path => PdfFont.FromBytes(File.ReadAllBytes(path.GetString()!))).ToArray();
var options = new PdfOptions { Fonts = Fonts("normal"), BoldFonts = Fonts("bold"),
    ItalicFonts = Fonts("italic"), BoldItalicFonts = Fonts("boldItalic"), SubsetFonts = true };
var html = File.ReadAllText(args[0]);
byte[] Convert(string input, PdfOptions settings)
{
    using var output = new MemoryStream(); PdfConverter.Convert(input, output, settings); return output.ToArray();
}
var pdf = Convert(html, options);
File.WriteAllBytes(args[1], pdf);
const string simple = "<table style='width:100pt;font-size:10pt;line-height:2'><colgroup><col style='width:100%'></colgroup><tbody><tr><td>A<br>B<br>C<br>D</td></tr></tbody></table>";
const string empty = "<table><colgroup><col style='width:100%'></colgroup><tbody><tr><td></td></tr></tbody></table>";
const string breakAfter = "<p style='break-after:page'>A</p>";
if (!Convert(breakAfter + empty, options with { MaxPages = 1 }).AsSpan().SequenceEqual(Convert(breakAfter, options)))
    throw new InvalidOperationException("Invisible table consumed a deferred break");
var errors = new List<object>();
// The former four-line height refusal is now a valid two-page body continuation.
using (var continued = new MemoryStream())
{
    var split = PdfConverter.Convert(simple, continued, options with { PageHeight = 99, Margin = 10 });
    if (split.PageCount != 2) throw new InvalidOperationException("Former height refusal did not continue on two pages");
}
foreach (var (name, input, settings, expected) in new[]
{
    ("shape", "<table></table>", options, "FPDF1101"),
    ("width", simple.Replace("width:100%", "width:99%"), options, "FPDF1302"),
    ("colspan", simple.Replace("<td>", "<td colspan='0'>"), options, "FPDF1103"),
    ("cell-css", simple.Replace("<td>", "<td style='width:100pt'>"), options, "FPDF1201"),
    ("missing-bold", simple.Replace("<tbody>", "<thead><tr><th>A</th></tr></thead><tbody>"), options with { BoldFonts = [] }, "FPDF1507"),
    ("atomic-line-height", simple.Replace("line-height:2", "line-height:8"), options with { PageHeight = 99, Margin = 10 }, "FPDF1302"),
    ("empty-leading", "<div style='padding-top:1pt;background-color:red'>" + empty + simple + "</div>", options with { PageHeight = 100, Margin = 10 }, "FPDF1302"),
    ("pages", "<p>A</p>" + simple, options with { PageHeight = 100, Margin = 10, MaxPages = 1 }, "FPDF1303"),
    ("commands", simple, options with { MaxDisplayCommands = 3 }, "FPDF1401"),
    ("bytes", html, options with { MaxOutputBytes = pdf.Length - 1 }, "FPDF1401")
})
{
    using var output = new MemoryStream(); output.Write("sentinel"u8); output.Position = 2;
    try { PdfConverter.Convert(input, output, settings); throw new InvalidOperationException("Expected failure: " + name); }
    catch (FactsPdfException error)
    {
        if (error.Code != expected || output.Position != 2 || !output.ToArray().AsSpan().SequenceEqual("sentinel"u8))
            throw new InvalidOperationException("Wrong diagnostic or changed output: " + name, error);
        errors.Add(new { name, code = error.Code, sourceOffset = error.SourceOffset, outputUnchanged = true });
    }
}
using (var output = new MemoryStream())
{
    output.Write("sentinel"u8); output.Position = 2;
    try { PdfConverter.Convert(html, output, options, new CancellationToken(true)); throw new InvalidOperationException("Expected cancellation"); }
    catch (OperationCanceledException)
    {
        if (output.Position != 2 || !output.ToArray().AsSpan().SequenceEqual("sentinel"u8)) throw new InvalidOperationException("Cancellation changed output");
        errors.Add(new { name = "cancelled", code = "cancelled", sourceOffset = -1, outputUnchanged = true });
    }
}
if (!Convert(html, options with { MaxOutputBytes = pdf.Length }).AsSpan().SequenceEqual(pdf)) throw new InvalidOperationException("Exact output boundary differs");
if (!Convert(html, options).AsSpan().SequenceEqual(pdf)) throw new InvalidOperationException("Success/failure/success differs");
Console.WriteLine(JsonSerializer.Serialize(new { bytes = pdf.Length, errors, exactOutputBoundary = true, recoveryByteEquality = true, emptyTablePreservesPagination = true }));
return 0;
