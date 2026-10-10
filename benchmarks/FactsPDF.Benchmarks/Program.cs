using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FactsPDF;

// A small reproducible baseline, not a statistically controlled performance certification.
if (args.Length < 2 || args[0] is not ("full" or "subset"))
{
    Console.Error.WriteLine("Usage: FactsPDF.Benchmarks <full|subset> <input.html> [font.ttf ...]");
    return 2;
}
var subset = args[0] == "subset";
var html = File.ReadAllText(args[1]);
var bytes = args.Skip(2).Select(File.ReadAllBytes).ToArray();
PdfFont[] Load() => bytes.Select(data => PdfFont.LoadTrueType(data)).ToArray();
var fonts = Load(); // Untimed warm-up; filesystem reads above are excluded from font-load measurements.
var loadSamples = new List<(double Ms, long Bytes)>();
for (var i = 0; i < 5; i++)
{
    var allocated = GC.GetAllocatedBytesForCurrentThread(); var start = Stopwatch.GetTimestamp();
    fonts = Load();
    loadSamples.Add((Stopwatch.GetElapsedTime(start).TotalMilliseconds, GC.GetAllocatedBytesForCurrentThread() - allocated));
}
var options = new PdfOptions { Fonts = fonts, SubsetFonts = subset };
long outputBytes = 0; int pages = 0;
void Render()
{
    using var output = new MemoryStream(); var result = PdfConverter.Convert(html, output, options);
    outputBytes = result.BytesWritten; pages = result.PageCount;
}
for (var i = 0; i < 3; i++) Render();
var renderSamples = new List<(double Ms, long Bytes)>();
for (var i = 0; i < 20; i++)
{
    var allocated = GC.GetAllocatedBytesForCurrentThread(); var start = Stopwatch.GetTimestamp(); Render();
    renderSamples.Add((Stopwatch.GetElapsedTime(start).TotalMilliseconds, GC.GetAllocatedBytesForCurrentThread() - allocated));
}
// Hash a separate untimed conversion, never the measured hot path. Both
// engine revisions use this identical harness and the same input bytes.
string outputHash;
using (var proof = new MemoryStream())
{
    var result = PdfConverter.Convert(html, proof, options);
    if (result.BytesWritten != outputBytes || result.PageCount != pages)
        throw new InvalidOperationException("Untimed proof differs from the measured output geometry.");
    outputHash = Convert.ToHexString(SHA256.HashData(
        proof.GetBuffer().AsSpan(0, checked((int)proof.Length)))).ToLowerInvariant();
}
var inputHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(html))).ToLowerInvariant();
using var writer = new Utf8JsonWriter(Console.OpenStandardOutput(), new JsonWriterOptions { Indented = true });
writer.WriteStartObject(); writer.WriteString("mode", args[0]); writer.WriteString("runtime", RuntimeInformation.FrameworkDescription);
writer.WriteString("os", RuntimeInformation.OSDescription); writer.WriteString("architecture", RuntimeInformation.ProcessArchitecture.ToString());
writer.WriteBoolean("dynamic_code_supported", System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported);
writer.WriteNumber("output_bytes", outputBytes); writer.WriteNumber("pages", pages);
writer.WriteString("input_sha256", inputHash); writer.WriteString("output_sha256", outputHash);
writer.WriteNumber("process_lifetime_peak_working_set_bytes", Process.GetCurrentProcess().PeakWorkingSet64);
writer.WriteString("measurement_scope", "Synchronous current-thread managed allocations; excludes native allocations. Font loading excludes file I/O. Conversion reuses loaded fonts (or ASCII/Courier when none supplied), includes output buffer allocation, has three untimed warm-ups. Input/PDF hashes use a separate untimed proof conversion. Peak working set includes setup, samples and proof/hash work before this reading, not just conversion.");
void Samples(string name, List<(double Ms, long Bytes)> samples)
{
    writer.WriteStartObject(name); writer.WriteNumber("samples", samples.Count);
    var times = samples.Select(s => s.Ms).Order().ToArray(); var allocations = samples.Select(s => s.Bytes).Order().ToArray();
    writer.WriteNumber("median_ms", (times[(times.Length - 1) / 2] + times[times.Length / 2]) / 2);
    writer.WriteNumber("median_managed_allocated_bytes", (allocations[(allocations.Length - 1) / 2] + allocations[allocations.Length / 2]) / 2);
    writer.WriteStartArray("raw");
    foreach (var sample in samples) { writer.WriteStartObject(); writer.WriteNumber("ms", sample.Ms); writer.WriteNumber("managed_allocated_bytes", sample.Bytes); writer.WriteEndObject(); }
    writer.WriteEndArray(); writer.WriteEndObject();
}
Samples("font_load", loadSamples); Samples("reused_font_conversion", renderSamples); writer.WriteEndObject(); writer.Flush();
return 0;
