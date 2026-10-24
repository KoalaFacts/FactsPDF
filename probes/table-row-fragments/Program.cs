using System.Security.Cryptography;
using System.Text.Json;
using FactsPDF;

// Verification-only public API entry point. Expectations come from authored JSON,
// never from reflection, internal page records, or implementation calculations.
if (args.Length != 2) return 2;
var fixtures = Path.GetFullPath(args[0]); var directory = Path.GetFullPath(args[1]);
Directory.CreateDirectory(directory);
using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(fixtures, "table-row-fragments.json")));
var images = new Dictionary<string, PdfImage>(StringComparer.Ordinal)
{
    ["sample"] = PdfImage.LoadImage(File.ReadAllBytes(Path.Combine(fixtures, "sample.png")))
};
PdfOptions Options(JsonElement item)
{
    var options = new PdfOptions { Images = images };
    foreach (var setting in item.GetProperty("options").EnumerateObject())
        options = setting.Name switch
        {
            "PageWidth" => options with { PageWidth = setting.Value.GetDouble() },
            "PageHeight" => options with { PageHeight = setting.Value.GetDouble() },
            "Margin" => options with { Margin = setting.Value.GetDouble() },
            _ => throw new Exception("Unimplemented public probe option: " + setting.Name)
        };
    return options;
}
byte[] Render(string html, PdfOptions options, int pages)
{
    using var output = new MemoryStream();
    var result = PdfConverter.Convert(html, output, options);
    if (result.PageCount != pages) throw new Exception($"Expected {pages} pages, received {result.PageCount}");
    return output.ToArray();
}
var evidence = new List<object>();
void Failure(string name, string html, PdfOptions options, string expected)
{
    using var output = new MemoryStream(); output.Write("sentinel"u8); output.Position = 2;
    try { PdfConverter.Convert(html, output, options); throw new Exception("Expected failure: " + name); }
    catch (FactsPdfException error)
    {
        if (error.Code != expected || output.Position != 2 || !output.ToArray().AsSpan().SequenceEqual("sentinel"u8))
            throw new Exception("Wrong diagnostic or changed destination: " + name, error);
        evidence.Add(new { name, code = error.Code, sourceOffset = error.SourceOffset, outputUnchanged = true, position = 2 });
    }
}
foreach (var item in manifest.RootElement.GetProperty("cases").EnumerateArray())
{
    var name = item.GetProperty("name").GetString()!;
    // Include A4 API output as a second public entry-point and exact A1 accounting witness.
    var html = item.GetProperty("html").GetString()!; var options = Options(item);
    var pages = item.GetProperty("pages").GetInt32(); var bytes = Render(html, options, pages);
    File.WriteAllBytes(Path.Combine(directory, name + "-api.pdf"), bytes);
    if (item.GetProperty("commands").ValueKind == JsonValueKind.Number)
    {
        var limit = item.GetProperty("commands").GetInt32();
        if (!Render(html, options with { MaxDisplayCommands = limit }, pages).AsSpan().SequenceEqual(bytes))
            throw new Exception("Exact display boundary changed bytes: " + name);
        Failure(name + "-commands-minus-one", html, options with { MaxDisplayCommands = limit - 1 }, "FPDF1401");
    }
    if (!Render(html, options with { MaxPages = pages }, pages).AsSpan().SequenceEqual(bytes))
        throw new Exception("Exact page boundary changed bytes: " + name);
    Failure(name + "-pages-minus-one", html, options with { MaxPages = pages - 1 }, "FPDF1303");
    if (!Render(html, options with { MaxOutputBytes = bytes.Length }, pages).AsSpan().SequenceEqual(bytes))
        throw new Exception("Exact byte boundary changed bytes: " + name);
    Failure(name + "-bytes-minus-one", html, options with { MaxOutputBytes = bytes.Length - 1 }, "FPDF1401");
    using (var output = new MemoryStream())
    {
        output.Write("sentinel"u8); output.Position = 2;
        try { PdfConverter.Convert(html, output, options, new CancellationToken(true)); throw new Exception("Expected cancellation"); }
        catch (OperationCanceledException)
        {
            if (output.Position != 2 || !output.ToArray().AsSpan().SequenceEqual("sentinel"u8))
                throw new Exception("Cancellation changed destination: " + name);
        }
    }
    if (!Render(html, options, pages).AsSpan().SequenceEqual(bytes)) throw new Exception("Failure/recovery changed bytes: " + name);
    evidence.Add(new { name, pages, bytes = bytes.Length, exactCommands = item.GetProperty("commands"),
        exactPages = true, exactOutputBoundary = true, cancellationPreserved = true, recoveryByteEquality = true,
        sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)) });
}
foreach (var item in manifest.RootElement.GetProperty("errors").EnumerateArray())
{
    Failure(item.GetProperty("name").GetString()!, item.GetProperty("html").GetString()!, Options(item), item.GetProperty("code").GetString()!);
    using var recovery = new MemoryStream(); PdfConverter.Convert("<p>RECOVERY</p>", recovery);
    using var control = new MemoryStream(); PdfConverter.Convert("<p>RECOVERY</p>", control);
    if (!recovery.ToArray().AsSpan().SequenceEqual(control.ToArray())) throw new Exception("Negative changed same-entry-point recovery");
}
File.WriteAllText(Path.Combine(directory, "api-results.json"), JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine(JsonSerializer.Serialize(new { cases = evidence.Count, success = true }));
return 0;
