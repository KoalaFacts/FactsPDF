using System.Security.Cryptography;
using System.Text.Json;
using FactsPDF;

// Verification-only direct public API host; all paths are supplied explicitly.
if (args.Length != 3) return 2;
var fixtures = Path.GetFullPath(args[0]); var outputDirectory = Path.GetFullPath(args[1]);
Directory.CreateDirectory(outputDirectory);
using var contract = JsonDocument.Parse(File.ReadAllText(Path.Combine(fixtures, "acceptance-cases.json")));
using var manifest = JsonDocument.Parse(File.ReadAllText(args[2]));
var fonts = manifest.RootElement.GetProperty("roles").GetProperty("normal").EnumerateArray()
    .Select(path => PdfFont.LoadTrueType(File.ReadAllBytes(path.GetString()!))).ToArray();
var evidence = new List<object>();
foreach (var item in contract.RootElement.GetProperty("cases").EnumerateArray())
{
    var id = item.GetProperty("id").GetString()!;
    var html = item.TryGetProperty("html", out var inline) ? inline.GetString()!
        : File.ReadAllText(Path.Combine(fixtures, item.GetProperty("html_file").GetString()!));
    var images = item.GetProperty("resources").EnumerateObject().ToDictionary(p => p.Name,
        p => PdfImage.LoadImage(File.ReadAllBytes(Path.Combine(fixtures, p.Value.GetString()!))), StringComparer.Ordinal);
    var o = new PdfOptions { Images = images, Fonts = item.TryGetProperty("fonts", out _) ? fonts : [], SubsetFonts = true };
    if (item.TryGetProperty("options", out var settings))
        foreach (var setting in settings.EnumerateObject())
            o = setting.Name switch
            {
                "PageHeight" => o with { PageHeight = setting.Value.GetDouble() },
                "Margin" => o with { Margin = setting.Value.GetDouble() },
                "MaxDisplayCommands" => o with { MaxDisplayCommands = setting.Value.GetInt32() },
                "MaxOutputBytes" => o with { MaxOutputBytes = setting.Value.GetInt32() },
                "MaxImageResources" => o with { MaxImageResources = setting.Value.GetInt32() },
                _ => throw new Exception("Unimplemented acceptance option: " + setting.Name)
            };
    using var output = new MemoryStream(); var expect = item.GetProperty("expect");
    if (expect.TryGetProperty("code", out var code))
    {
        output.Write("sentinel"u8); output.Position = 2;
        try { PdfConverter.Convert(html, output, o); throw new Exception("Expected failure: " + id); }
        catch (FactsPdfException error)
        {
            if (error.Code != code.GetString() || output.Position != 2 || !output.ToArray().AsSpan().SequenceEqual("sentinel"u8)
                || expect.TryGetProperty("source_offset", out var offset) && error.SourceOffset != offset.GetInt32())
                throw new Exception("Wrong diagnostic or changed output: " + id, error);
            evidence.Add(new { id, code = error.Code, sourceOffset = error.SourceOffset, outputUnchanged = true });
        }
    }
    else
    {
        var result = PdfConverter.Convert(html, output, o); var bytes = output.ToArray();
        if (result.PageCount != expect.GetProperty("page_count").GetInt32()) throw new Exception("Page count: " + id);
        File.WriteAllBytes(Path.Combine(outputDirectory, id + "-api.pdf"), bytes);
        using var second = new MemoryStream(); PdfConverter.Convert(html, second, o);
        if (!second.ToArray().AsSpan().SequenceEqual(bytes)) throw new Exception("Nondeterministic: " + id);
        evidence.Add(new { id, pages = result.PageCount, bytes = bytes.Length, sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)) });
    }
}
var image = PdfImage.LoadImage(File.ReadAllBytes(Path.Combine(fixtures, "rgb-420.jpg")));
using (var output = new MemoryStream())
{
    output.Write("sentinel"u8); output.Position = 2;
    var images = Enumerable.Range(0, 17).ToDictionary(i => "image" + i, _ => image);
    try { PdfConverter.Convert("<p>ASCII</p>", output, new() { Images = images }); throw new Exception("Expected resource count failure"); }
    catch (FactsPdfException error) when (error.Code == "FPDF1603")
    {
        if (output.Position != 2 || !output.ToArray().AsSpan().SequenceEqual("sentinel"u8)) throw new Exception("Resource failure changed output");
        evidence.Add(new { id = "D1-default-image-count", code = error.Code, outputUnchanged = true });
    }
}
var huge = new byte[8_388_609];
File.ReadAllBytes(Path.Combine(fixtures, "rgb-420.jpg")).CopyTo(huge, 0);
try { PdfImage.LoadImage(huge); throw new Exception("Expected byte limit failure"); }
catch (FactsPdfException error) when (error.Code == "FPDF1603")
{ evidence.Add(new { id = "D1-default-byte-cap", code = error.Code }); }
File.WriteAllText(Path.Combine(outputDirectory, "api-results.json"), JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine(JsonSerializer.Serialize(new { cases = evidence.Count, success = true }));
return 0;
