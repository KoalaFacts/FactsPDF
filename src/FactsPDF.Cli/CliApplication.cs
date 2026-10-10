using System.Text;

namespace FactsPDF.CommandLine;

/// <summary>CLI boundary: explicit file access, bounded UTF-8 input and atomic file replacement.</summary>
public static class CliApplication
{
    private const string Usage = "Usage: FactsPDF.Cli <input.html|-> <output.pdf|-> [--overwrite] [--subset-fonts] [--font path.ttf ...]\n" +
        "Experimental inline-CSS renderer. Supply static TrueType fonts for simple Unicode/Chinese text.\n" +
        "--subset-fonts embeds only needed glyphs where permitted; default is full embedding.\n" +
        "Repeat --font for an ordered fallback chain (at most eight). '-' reads stdin or writes binary PDF to stdout.\n";

    public static int Run(string[] args, Stream input, Stream output, TextWriter error,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args); ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output); ArgumentNullException.ThrowIfNull(error);
        string? temporary = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (args.Length == 1 && args[0] is "--help" or "-h") { output.Write(Encoding.UTF8.GetBytes(Usage)); return 0; }
            var paths = new List<string>(); var fontPaths = new List<string>(); var overwrite = false; var subsetFonts = false;
            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                if (arg == "--overwrite" && !overwrite) overwrite = true;
                else if (arg == "--subset-fonts" && !subsetFonts) subsetFonts = true;
                else if (arg == "--font")
                {
                    if (++i >= args.Length || string.IsNullOrWhiteSpace(args[i]) || args[i] == "-" || args[i].StartsWith("--", StringComparison.Ordinal))
                        throw new ArgumentException("--font requires an explicit font file path.");
                    if (fontPaths.Count >= 8) throw new ArgumentException("At most eight font paths are allowed.");
                    fontPaths.Add(Path.GetFullPath(args[i]));
                }
                else if (arg.StartsWith("--", StringComparison.Ordinal)) throw new ArgumentException("Unknown or repeated option.");
                else paths.Add(arg);
            }
            if (paths.Count != 2) throw new ArgumentException("Specify input and output paths.");
            var source = paths[0] == "-" ? null : Path.GetFullPath(paths[0]);
            var destination = paths[1] == "-" ? null : Path.GetFullPath(paths[1]);
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (destination is not null && (source is not null && string.Equals(source, destination, comparison)
                || fontPaths.Any(path => string.Equals(path, destination, comparison))))
                throw new ArgumentException("Output must be different from the input HTML and every supplied font file.");
            if (destination is not null && File.Exists(destination) && !overwrite)
                throw new IOException("Destination exists. Use --overwrite to replace it after a successful conversion.");

            var options = new PdfOptions { SubsetFonts = subsetFonts }; var fonts = new List<PdfFont>(); long totalFontBytes = 0;
            foreach (var path in fontPaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var fontStream = File.OpenRead(path);
                var remaining = options.MaxTotalFontBytes - totalFontBytes;
                if (remaining <= 0) throw new FactsPdfException("FPDF1503", "Fonts exceed MaxTotalFontBytes.");
                var font = PdfFont.LoadTrueType(fontStream, (int)Math.Min(PdfFont.DefaultMaxBytes, remaining), cancellationToken);
                fonts.Add(font); totalFontBytes += font.ByteLength;
            }
            options = options with { Fonts = fonts.ToArray() };
            string html;
            if (source is null) html = ReadInput(input, options.MaxInputCharacters, cancellationToken);
            else { using var file = File.OpenRead(source); html = ReadInput(file, options.MaxInputCharacters, cancellationToken); }
            PdfConversionResult result;
            if (destination is null) result = PdfConverter.Convert(html, output, options, cancellationToken);
            else
            {
                temporary = Path.Combine(Path.GetDirectoryName(destination)!, $".factspdf-{Guid.NewGuid():N}.tmp");
                using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { result = PdfConverter.Convert(html, file, options, cancellationToken); file.Flush(flushToDisk: true); }
                cancellationToken.ThrowIfCancellationRequested(); File.Move(temporary, destination, overwrite); temporary = null;
            }
            error.WriteLine($"Wrote {result.PageCount} page(s), {result.BytesWritten} bytes."); return 0;
        }
        catch (OperationCanceledException) { error.WriteLine("Conversion cancelled."); return 130; }
        catch (FactsPdfException ex) { error.WriteLine(ex.Message); return 3; }
        catch (DecoderFallbackException) { error.WriteLine("Input must be valid UTF-8."); return 3; }
        catch (ArgumentException ex) { error.WriteLine(ex.Message); error.Write(Usage); return 2; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        { error.WriteLine(ex.Message); return 4; }
        finally
        {
            if (temporary is not null)
            {
                try { File.Delete(temporary); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                { error.WriteLine("Could not remove temporary file: " + temporary); }
            }
        }
    }

    private static string ReadInput(Stream input, int limit, CancellationToken cancellation)
    {
        using var reader = new StreamReader(input, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: false, bufferSize: 4096, leaveOpen: true);
        var text = new StringBuilder(); var buffer = new char[4096]; var first = true;
        while (true)
        {
            cancellation.ThrowIfCancellationRequested(); var count = reader.Read(buffer, 0, buffer.Length);
            if (count == 0) break;
            var start = first && buffer[0] == '\uFEFF' ? 1 : 0; first = false;
            if (text.Length + count - start > limit) throw new FactsPdfException("FPDF1001", "HTML exceeds MaxInputCharacters.");
            text.Append(buffer, start, count - start);
        }
        return text.ToString();
    }
}
