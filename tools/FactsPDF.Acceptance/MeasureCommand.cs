using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace FactsPDF.Acceptance;

internal static class MeasureCommand
{
    internal static int Run(Settings s)
    {
        // HTML and font loading are explicitly outside the warm conversion interval.
        var html = File.ReadAllText(s.Input, new UTF8Encoding(false, true));
        var fontStart = Stopwatch.GetTimestamp();
        var fontBefore = GC.GetAllocatedBytesForCurrentThread();
        var options = s.LoadFonts(default);
        var fontAllocated = GC.GetAllocatedBytesForCurrentThread() - fontBefore;
        var fontElapsed = Stopwatch.GetElapsedTime(fontStart).TotalMilliseconds;
        var samples = new List<object?>();
        try
        {
            for (var i = -s.Warmups; i < s.Samples; i++)
            {
                var started = Stopwatch.GetTimestamp();
                var before = GC.GetAllocatedBytesForCurrentThread();
                using var output = new MemoryStream();
                var result = PdfConverter.Convert(html, output, options);
                var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                // Hashing, sample construction and JSON serialization are not timed/charged.
                var hash = Convert.ToHexString(SHA256.HashData(output.GetBuffer().AsSpan(0, checked((int)output.Length)))).ToLowerInvariant();
                if (i >= 0) samples.Add(new Dictionary<string, object?>
                {
                    ["sample_index"] = i, ["elapsed_ms"] = elapsed,
                    ["managed_allocated_bytes"] = allocated, ["pdf_bytes"] = result.BytesWritten,
                    ["pdf_sha256"] = hash, ["pages"] = result.PageCount,
                    ["measurement_scope"] = "warm-convert-and-output-buffer-current-thread"
                });
            }
            long? peak = null;
            try { using var process = Process.GetCurrentProcess(); var value = process.PeakWorkingSet64; if (value > 0) peak = value; }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or PlatformNotSupportedException) { }
            var row = Program.RuntimeInfo();
            row["status"] = "measured"; row["samples"] = samples; row["warmups"] = s.Warmups;
            row["font_load_elapsed_ms"] = fontElapsed;
            row["font_load_managed_allocated_bytes"] = fontAllocated;
            row["font_load_scope"] = "file-read-and-explicit-font-load-current-thread";
            row["warm_process_peak_bytes"] = peak;
            row["peak_scope"] = "whole-process-lifetime-not-per-conversion";
            JsonOutput.Write(row); return 0;
        }
        catch (FactsPdfException ex)
        {
            JsonOutput.Write(new Dictionary<string, object?> { ["status"] = "measurement-failed", ["code"] = ex.Code });
            return 3;
        }
    }
}
