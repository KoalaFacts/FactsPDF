using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using FactsPDF.CssSyntax;

// Independent syntax-only sample; no HTML renderer, PDF I/O, system fonts or network calls.
const int ruleCount = 96;
const int samples = 30;
var css = new StringBuilder();
for (var i = 0; i < ruleCount; i++)
{
    css.Append(".report .card-").Append(i)
        .Append(" > p.note {color:#123456; font-size:12pt; ")
        .Append("width:calc(100% - 2px); --tag:var(--x, red); }\n");
}
var input = css.ToString();
var limits = new CssSyntaxLimits();

int Parse()
{
    var source = CssSourceText.Create(input, 0, limits);
    var syntax = CssSyntaxParser.ParseStylesheet(source, limits);
    if (syntax.Diagnostics.Count != 0 || syntax.Rules.Count != ruleCount)
        throw new InvalidOperationException("Benchmark fixture did not parse correctly.");
    return syntax.Rules.Count;
}
for (var i = 0; i < 10; i++) _ = Parse();
var observed = new (double Milliseconds, long Bytes)[samples];
for (var i = 0; i < samples; i++)
{
    var allocated = GC.GetAllocatedBytesForCurrentThread();
    var start = Stopwatch.GetTimestamp();
    _ = Parse();
    observed[i] = (Stopwatch.GetElapsedTime(start).TotalMilliseconds,
        GC.GetAllocatedBytesForCurrentThread() - allocated);
}
var times = observed.Select(s => s.Milliseconds).Order().ToArray();
var allocations = observed.Select(s => s.Bytes).Order().ToArray();
using var writer = new Utf8JsonWriter(Console.OpenStandardOutput(), new JsonWriterOptions { Indented = true });
writer.WriteStartObject();
writer.WriteString("measurement", "CSS Syntax preprocessing + tokenization + stylesheet AST");
writer.WriteString("runtime", RuntimeInformation.FrameworkDescription);
writer.WriteString("os", RuntimeInformation.OSDescription);
writer.WriteString("arch", RuntimeInformation.ProcessArchitecture.ToString());
writer.WriteBoolean("dynamic_code_supported", RuntimeFeature.IsDynamicCodeSupported);
writer.WriteNumber("rules", ruleCount);
writer.WriteNumber("css_utf16_characters", input.Length);
writer.WriteNumber("warmups", 10);
writer.WriteNumber("samples", samples);
writer.WriteNumber("median_ms", (times[14] + times[15]) / 2);
writer.WriteNumber("median_current_thread_allocated_bytes", (allocations[14] + allocations[15]) / 2);
writer.WriteNumber("peak_process_working_set_bytes", Process.GetCurrentProcess().PeakWorkingSet64);
writer.WriteString("scope", "Per iteration includes CSS string preprocessing, tokenization and AST. Excludes HTML, cascade, layout and PDF. Current-thread allocation is cumulative allocated bytes, not peak memory. Peak working set covers process lifetime. Shared CI runner, warm JIT/AOT code and OS caches; not production performance.");
writer.WriteStartArray("raw");
foreach (var sample in observed)
{
    writer.WriteStartObject();
    writer.WriteNumber("ms", sample.Milliseconds);
    writer.WriteNumber("current_thread_allocated_bytes", sample.Bytes);
    writer.WriteEndObject();
}
writer.WriteEndArray();
writer.WriteEndObject();
writer.Flush();
