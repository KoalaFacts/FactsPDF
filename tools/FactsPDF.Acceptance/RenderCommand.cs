using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace FactsPDF.Acceptance;

internal static class RenderCommand
{
    internal static int Run(Settings s)
    {
        var token = new CancellationToken(s.PreCancelled);
        using var output = new MemoryStream();
        if (s.Probe) output.Write("keep"u8);
        var row = Program.RuntimeInfo();
        var started = Stopwatch.GetTimestamp();
        string? temporary = null;
        try
        {
            token.ThrowIfCancellationRequested();
            var html = File.ReadAllText(s.Input, new UTF8Encoding(false, true));
            if (s.InvalidScalar.HasValue) html = "<p>A" + (char)s.InvalidScalar.Value + "B</p>";
            var options = s.LoadFonts(token);
            var result = PdfConverter.Convert(html, output, options, token);
            token.ThrowIfCancellationRequested();
            if (File.Exists(s.Output) && !s.Probe) throw new IOException("Output already exists");
            temporary = Path.Combine(Path.GetDirectoryName(s.Output)!, ".acceptance-" + Guid.NewGuid().ToString("N") + ".tmp");
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                output.Position = 0; output.CopyTo(file); file.Flush(true);
            }
            token.ThrowIfCancellationRequested();
            File.Move(temporary, s.Output, s.Probe); temporary = null;
            row["status"] = "success"; row["pages"] = result.PageCount;
            row["pdf_bytes"] = output.Length;
            row["pdf_sha256"] = Convert.ToHexString(SHA256.HashData(output.GetBuffer().AsSpan(0, checked((int)output.Length)))).ToLowerInvariant();
            row["elapsed_ms"] = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            JsonOutput.Write(row); return 0;
        }
        catch (FactsPdfException ex)
        {
            row["status"] = "error"; row["code"] = ex.Code; row["source_offset"] = ex.SourceOffset;
            row["output_unchanged"] = s.Probe && output.ToArray().AsSpan().SequenceEqual("keep"u8);
            row["elapsed_ms"] = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            JsonOutput.Write(row); return 3;
        }
        catch (OperationCanceledException)
        {
            row["status"] = "cancelled"; row["code"] = null;
            row["output_unchanged"] = s.Probe && output.ToArray().AsSpan().SequenceEqual("keep"u8);
            JsonOutput.Write(row); return 130;
        }
        finally { if (temporary is not null) File.Delete(temporary); }
    }
}
