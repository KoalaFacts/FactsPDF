using System.Text;

namespace FactsPDF.CommandLine;

/// <summary>CLI boundary: explicit file access, bounded UTF-8 input and atomic file replacement.</summary>
public static class CliApplication
{
    private const string Usage = "Usage: FactsPDF.Cli <input.html|-> <output.pdf|-> [--overwrite] [--subset-fonts] [--page-numbers] [--font path.ttf ...] [--font-bold path.ttf ...] [--font-italic path.ttf ...] [--font-bold-italic path.ttf ...]\n" +
        "Experimental inline-CSS renderer. Supply static TrueType fonts for simple Unicode/Chinese text.\n" +
        "--subset-fonts embeds only needed glyphs where permitted; default is full embedding.\n" +
        "--page-numbers adds normal 10pt decimal numbers in the existing bottom margin; default is off.\n" +
        "Repeat each font option for its independent ordered fallback chain (at most eight paths total). '-' reads stdin or writes binary PDF to stdout.\n" +
        "--image name=path supplies an explicit baseline JPEG resource (at most 16; no network downloads). Format is detected from content.\n";

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
            var paths = new List<string>(); var fontPaths = new List<(int Role, string Path)>(); var overwrite = false; var subsetFonts = false; var pageNumbers = false;
            var imagePaths = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                if (arg == "--overwrite" && !overwrite) overwrite = true;
                else if (arg == "--subset-fonts" && !subsetFonts) subsetFonts = true;
                else if (arg == "--page-numbers" && !pageNumbers) pageNumbers = true;
                else if (arg is "--font" or "--font-bold" or "--font-italic" or "--font-bold-italic")
                {
                    if (++i >= args.Length || string.IsNullOrWhiteSpace(args[i]) || args[i] == "-" || args[i].StartsWith("--", StringComparison.Ordinal))
                        throw new ArgumentException($"{arg} requires an explicit font file path.");
                    if (fontPaths.Count >= 8) throw new ArgumentException("At most eight font paths are allowed.");
                    var role = arg switch { "--font-bold" => 1, "--font-italic" => 2, "--font-bold-italic" => 3, _ => 0 };
                    fontPaths.Add((role, Path.GetFullPath(args[i])));
                }
                else if (arg == "--image")
                {
                    if (++i >= args.Length) throw new ArgumentException("--image requires name=path.");
                    var equal = args[i].IndexOf('=');
                    if (equal <= 0) throw new ArgumentException("--image requires name=path.");
                    var name = args[i][..equal]; var path = args[i][(equal + 1)..];
                    if (!ImageName(name) || string.IsNullOrWhiteSpace(path) || path == "-" || path.StartsWith("--", StringComparison.Ordinal))
                        throw new ArgumentException("--image requires a valid explicit name and file path.");
                    if (imagePaths.Count >= 16 || !imagePaths.TryAdd(name, Path.GetFullPath(path)))
                        throw new ArgumentException("At most 16 uniquely named images are allowed.");
                }
                else if (arg.StartsWith("--", StringComparison.Ordinal)) throw new ArgumentException("Unknown or repeated option.");
                else paths.Add(arg);
            }
            if (paths.Count != 2) throw new ArgumentException("Specify input and output paths.");
            var source = paths[0] == "-" ? null : Path.GetFullPath(paths[0]);
            var destination = paths[1] == "-" ? null : Path.GetFullPath(paths[1]);
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (destination is not null && (source is not null && string.Equals(source, destination, comparison)
                || fontPaths.Any(path => string.Equals(path.Path, destination, comparison))
                || imagePaths.Values.Any(path => string.Equals(path, destination, comparison))))
                throw new ArgumentException("Output must be different from the input HTML and every supplied font or image file.");
            if (destination is not null && File.Exists(destination) && !overwrite)
                throw new IOException("Destination exists. Use --overwrite to replace it after a successful conversion.");

            var options = new PdfOptions { SubsetFonts = subsetFonts, PageNumbers = pageNumbers }; var fonts = Enumerable.Range(0, 4).Select(_ => new List<PdfFont>()).ToArray(); long totalFontBytes = 0;
            foreach (var (role, path) in fontPaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var fontStream = File.OpenRead(path);
                var remaining = options.MaxTotalFontBytes - totalFontBytes;
                if (remaining <= 0) throw new FactsPdfException("FPDF1503", "Fonts exceed MaxTotalFontBytes.");
                var font = PdfFont.LoadTrueType(fontStream, (int)Math.Min(PdfFont.DefaultMaxBytes, remaining), cancellationToken);
                fonts[role].Add(font); totalFontBytes += font.ByteLength;
            }
            options = options with { Fonts = fonts[0].ToArray(), BoldFonts = fonts[1].ToArray(),
                ItalicFonts = fonts[2].ToArray(), BoldItalicFonts = fonts[3].ToArray() };
            var images = new Dictionary<string, PdfImage>(StringComparer.Ordinal); var totalImageBytes = 0;
            foreach (var (name, path) in imagePaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var remaining = options.MaxTotalImageBytes - totalImageBytes;
                if (remaining <= 0) throw new FactsPdfException("FPDF1603", "Images exceed MaxTotalImageBytes.");
                using var imageStream = File.OpenRead(path);
                var image = PdfImage.LoadImage(imageStream, new PdfImageLimits { MaxBytes = Math.Min(8_388_608, remaining) }, cancellationToken);
                images.Add(name, image); totalImageBytes += image.ByteLength;
            }
            if (images.Count > 0) options = options with { Images = images };
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

    // Keep CLI parameter errors distinct from the core's resource diagnostics.
    private static bool ImageName(string name) => name.Length is >= 1 and <= 128 && char.IsAsciiLetterOrDigit(name[0])
        && name.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-');

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
