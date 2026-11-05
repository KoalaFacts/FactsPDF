using System.Buffers;
using System.Collections.ObjectModel;

namespace FactsPDF;

/// <summary>Entry point for writing PDF documents from controlled HTML and CSS.</summary>
/// <example>
/// <code>
/// var writer = Pdf.Write()
///     .WithFonts(f => f.Regular(latin, chinese).Bold(latinBold))
///     .WithPage(p => p.A4().Margin(15, PdfUnit.Millimetre))
///     .WithPageNumbers();
/// byte[] pdf = writer.ToBytes(html);
/// </code>
/// </example>
public static class Pdf
{
    /// <summary>Starts from the defaults: A4 portrait, 36pt margins, built-in Courier for ASCII text and no page text.</summary>
    /// <returns>An immutable, thread-safe writer. Configure it once and reuse it for every document.</returns>
    public static PdfWriter Write() => PdfWriter.Default;
}

/// <summary>An immutable, thread-safe PDF writer configuration.</summary>
/// <remarks>Each <c>With</c> method returns a new writer and leaves the original unchanged, so a configured
/// writer can be shared across requests and threads. Rendering never reads files or the network: fonts and
/// images are only those you supply. Every output method renders the complete PDF in memory first, so a
/// failed conversion writes nothing.</remarks>
public sealed class PdfWriter
{
    internal static readonly PdfWriter Default = new(new PdfOptions());

    private PdfWriter(PdfOptions options) => Options = options;

    internal PdfOptions Options { get; }

    /// <summary>Sets the font fallback chains, base font size and subsetting.</summary>
    /// <param name="configure">Configures the fonts, starting from this writer's current font settings.</param>
    public PdfWriter WithFonts(Action<PdfFontSetup> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var setup = new PdfFontSetup(Options);
        configure(setup);
        return new(setup.Options);
    }

    /// <summary>Binds an image resource that <c>&lt;img src="name"&gt;</c> refers to; the same name replaces the earlier image.</summary>
    /// <param name="name">Case-sensitive resource name: an ASCII letter or digit, then letters, digits, <c>.</c>, <c>_</c> or <c>-</c>; at most 128 characters.</param>
    /// <param name="image">The image, loaded with <see cref="PdfImage.FromFile"/>, <see cref="PdfImage.FromBytes"/> or <see cref="PdfImage.FromStream"/>.</param>
    public PdfWriter WithImage(string name, PdfImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (!ImageResources.ValidName(name))
            throw new ArgumentException("Use 1–128 ASCII letters, digits, '.', '_' or '-', starting with a letter or digit.", nameof(name));
        var images = new Dictionary<string, PdfImage>(Options.Images, StringComparer.Ordinal) { [name] = image };
        return new(Options with { Images = new ReadOnlyDictionary<string, PdfImage>(images) });
    }

    /// <summary>Sets the paper size, orientation and margins.</summary>
    /// <param name="configure">Configures the page, starting from this writer's current page settings.</param>
    public PdfWriter WithPage(Action<PdfPageSetup> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var setup = new PdfPageSetup(Options);
        configure(setup);
        return new(setup.Build());
    }

    /// <summary>Sets the PDF document properties shown by PDF viewers.</summary>
    /// <param name="configure">Configures the properties, starting from this writer's current values.</param>
    public PdfWriter WithMetadata(Action<PdfMetadataSetup> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var setup = new PdfMetadataSetup(Options);
        configure(setup);
        return new(setup.Options);
    }

    /// <summary>Prints one line of literal text, centred in the top margin of every page (normal 10pt text).</summary>
    /// <param name="text">Single-line text of at most 1024 UTF-16 units.</param>
    /// <remarks>Cannot be combined with <see cref="WithPageFurniture(Action{PdfPageFurnitureSetup})"/>.</remarks>
    public PdfWriter WithHeaderText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new(Options with { HeaderText = text });
    }

    /// <summary>Prints one line of literal text, centred in the bottom margin of every page (normal 10pt text).</summary>
    /// <param name="text">Single-line text of at most 1024 UTF-16 units.</param>
    /// <remarks>With <see cref="WithPageNumbers"/>, the page number follows as <c>text | N</c>.
    /// Cannot be combined with <see cref="WithPageFurniture(Action{PdfPageFurnitureSetup})"/>.</remarks>
    public PdfWriter WithFooterText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new(Options with { FooterText = text });
    }

    /// <summary>Prints the page number centred in the bottom margin, after any footer text.</summary>
    /// <remarks>Cannot be combined with <see cref="WithPageFurniture(Action{PdfPageFurnitureSetup})"/>; use a
    /// <c>{page}</c> template there instead.</remarks>
    public PdfWriter WithPageNumbers() => new(Options with { PageNumbers = true });

    /// <summary>Sets styled left, centre and right header and footer text, with <c>{page}</c> and <c>{pages}</c> placeholders.</summary>
    /// <param name="configure">Configures the six regions and an optional first-page variant.</param>
    /// <remarks>Replaces any earlier page furniture. Cannot be combined with <see cref="WithHeaderText"/>,
    /// <see cref="WithFooterText"/> or <see cref="WithPageNumbers"/>.</remarks>
    public PdfWriter WithPageFurniture(Action<PdfPageFurnitureSetup> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var setup = new PdfPageFurnitureSetup();
        configure(setup);
        return WithPageFurniture(setup.Build());
    }

    /// <summary>Sets page furniture from an existing description, such as one read from configuration.</summary>
    /// <param name="furniture">The header and footer regions.</param>
    public PdfWriter WithPageFurniture(PdfPageFurniture furniture)
    {
        ArgumentNullException.ThrowIfNull(furniture);
        return new(Options with { PageFurniture = furniture });
    }

    /// <summary>Adjusts the resource limits that bound input size, work and output.</summary>
    /// <param name="configure">Configures the limits, starting from this writer's current values.</param>
    public PdfWriter WithLimits(Action<PdfLimitsSetup> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var setup = new PdfLimitsSetup(Options);
        configure(setup);
        return new(setup.Options);
    }

    /// <summary>Renders <paramref name="html"/> and returns the PDF bytes.</summary>
    /// <param name="html">The complete HTML document.</param>
    /// <param name="cancellationToken">Cancels the conversion.</param>
    /// <returns>The complete PDF.</returns>
    /// <exception cref="FactsPdfException">The input uses an unsupported feature, is malformed, or exceeds a limit.</exception>
    /// <exception cref="ArgumentException">The configuration is invalid.</exception>
    public byte[] ToBytes(string html, CancellationToken cancellationToken = default)
    {
        using var pdf = PdfConverter.Render(html, Options, cancellationToken, out _);
        return pdf.ToArray();
    }

    /// <summary>Renders <paramref name="html"/> and writes the PDF to <paramref name="output"/>, which is not closed.</summary>
    /// <param name="html">The complete HTML document.</param>
    /// <param name="output">A writable stream. Nothing is written unless rendering succeeds.</param>
    /// <param name="cancellationToken">Cancels the conversion.</param>
    /// <returns>The page count and number of bytes written.</returns>
    /// <exception cref="FactsPdfException">The input uses an unsupported feature, is malformed, or exceeds a limit.</exception>
    /// <exception cref="ArgumentException">The configuration is invalid or <paramref name="output"/> is not writable.</exception>
    public PdfWriteResult ToStream(string html, Stream output, CancellationToken cancellationToken = default)
        => PdfConverter.Convert(html, output, Options, cancellationToken);

    /// <summary>Renders <paramref name="html"/> and writes the PDF to <paramref name="output"/> asynchronously, for
    /// streams such as an ASP.NET Core response body that reject synchronous writes. The stream is not closed.</summary>
    /// <param name="html">The complete HTML document.</param>
    /// <param name="output">A writable stream. Nothing is written unless rendering succeeds.</param>
    /// <param name="cancellationToken">Cancels the conversion and the write.</param>
    /// <returns>The page count and number of bytes written.</returns>
    /// <exception cref="FactsPdfException">The input uses an unsupported feature, is malformed, or exceeds a limit.</exception>
    /// <exception cref="ArgumentException">The configuration is invalid or <paramref name="output"/> is not writable.</exception>
    public async Task<PdfWriteResult> ToStreamAsync(string html, Stream output, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (!output.CanWrite) throw new ArgumentException("Output must be writable.", nameof(output));
        using var pdf = PdfConverter.Render(html, Options, cancellationToken, out var pageCount);
        await output.WriteAsync(pdf.WrittenMemory, cancellationToken).ConfigureAwait(false);
        return new(pageCount, pdf.Length);
    }

    /// <summary>Renders <paramref name="html"/> into <paramref name="output"/>, for pipelines such as an
    /// ASP.NET Core <c>PipeWriter</c>. Nothing is written unless rendering succeeds.</summary>
    /// <param name="html">The complete HTML document.</param>
    /// <param name="output">The buffer writer that receives the PDF bytes.</param>
    /// <param name="cancellationToken">Cancels the conversion.</param>
    /// <returns>The page count and number of bytes written.</returns>
    /// <exception cref="FactsPdfException">The input uses an unsupported feature, is malformed, or exceeds a limit.</exception>
    public PdfWriteResult ToBufferWriter(string html, IBufferWriter<byte> output, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(output);
        using var pdf = PdfConverter.Render(html, Options, cancellationToken, out var pageCount);
        var data = pdf.WrittenSpan;
        output.Write(data);
        return new(pageCount, data.Length);
    }

    /// <summary>Renders <paramref name="html"/> and saves the PDF to <paramref name="path"/>, replacing any existing file.</summary>
    /// <param name="html">The complete HTML document.</param>
    /// <param name="path">Destination file. The PDF is written to a temporary file in the same directory and moved
    /// into place only after it is complete, so a failure never leaves a partial or damaged file.</param>
    /// <param name="cancellationToken">Cancels the conversion.</param>
    /// <returns>The page count and number of bytes written.</returns>
    /// <exception cref="FactsPdfException">The input uses an unsupported feature, is malformed, or exceeds a limit.</exception>
    /// <exception cref="IOException">The file cannot be written.</exception>
    public PdfWriteResult ToFile(string html, string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        using var pdf = PdfConverter.Render(html, Options, cancellationToken, out var pageCount);
        var length = checked((int)pdf.Length);
        ReplaceFile(path, temporary =>
        {
            using var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            file.Write(pdf.WrittenSpan);
            file.Flush(flushToDisk: true);
            cancellationToken.ThrowIfCancellationRequested();
        });
        return new(pageCount, length);
    }

    /// <summary>Renders <paramref name="html"/> and saves the PDF to <paramref name="path"/> asynchronously, replacing any existing file.</summary>
    /// <param name="html">The complete HTML document.</param>
    /// <param name="path">Destination file, replaced atomically as in <see cref="ToFile"/>.</param>
    /// <param name="cancellationToken">Cancels the conversion and the write.</param>
    /// <returns>The page count and number of bytes written.</returns>
    /// <exception cref="FactsPdfException">The input uses an unsupported feature, is malformed, or exceeds a limit.</exception>
    /// <exception cref="IOException">The file cannot be written.</exception>
    public async Task<PdfWriteResult> ToFileAsync(string html, string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        using var pdf = PdfConverter.Render(html, Options, cancellationToken, out var pageCount);
        var length = checked((int)pdf.Length);
        var (full, temporary) = TemporaryFor(path);
        try
        {
            await using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
            {
                await file.WriteAsync(pdf.WrittenMemory, cancellationToken).ConfigureAwait(false);
                await file.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, full, overwrite: true);
        }
        catch
        {
            DeleteQuietly(temporary);
            throw;
        }
        return new(pageCount, length);
    }

    private static void ReplaceFile(string path, Action<string> write)
    {
        var (full, temporary) = TemporaryFor(path);
        try
        {
            write(temporary);
            File.Move(temporary, full, overwrite: true);
        }
        catch
        {
            DeleteQuietly(temporary);
            throw;
        }
    }

    private static (string Full, string Temporary) TemporaryFor(string path)
    {
        var full = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(full) ?? throw new ArgumentException("Path must name a file.", nameof(path));
        return (full, Path.Combine(directory, $".{Path.GetFileName(full)}.{Guid.NewGuid():N}.tmp"));
    }

    // Best effort: the original failure is what the caller needs to see.
    private static void DeleteQuietly(string temporary)
    {
        try { File.Delete(temporary); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}

/// <summary>Length units accepted by <see cref="PdfPageSetup"/>.</summary>
public enum PdfUnit
{
    /// <summary>PDF point, 1/72 inch.</summary>
    Point,
    /// <summary>CSS pixel, 1/96 inch (0.75pt).</summary>
    Pixel,
    /// <summary>Millimetre.</summary>
    Millimetre,
    /// <summary>Centimetre.</summary>
    Centimetre,
    /// <summary>Inch.</summary>
    Inch,
}

/// <summary>Configures font fallback chains for <see cref="PdfWriter.WithFonts"/>.</summary>
/// <remarks>Without fonts, only ASCII text in built-in Courier is available. Each style has its own ordered
/// fallback chain; styles are never synthesised from another chain.</remarks>
public sealed class PdfFontSetup
{
    internal PdfFontSetup(PdfOptions options) => Options = options;

    internal PdfOptions Options { get; private set; }

    /// <summary>Sets the ordered fallback chain for normal text.</summary>
    /// <param name="fonts">Fonts tried in order for each character.</param>
    public PdfFontSetup Regular(params PdfFont[] fonts) { Options = Options with { Fonts = Chain(fonts) }; return this; }

    /// <summary>Sets the ordered fallback chain for bold text.</summary>
    /// <param name="fonts">Fonts tried in order for each character.</param>
    public PdfFontSetup Bold(params PdfFont[] fonts) { Options = Options with { BoldFonts = Chain(fonts) }; return this; }

    /// <summary>Sets the ordered fallback chain for italic text.</summary>
    /// <param name="fonts">Fonts tried in order for each character.</param>
    public PdfFontSetup Italic(params PdfFont[] fonts) { Options = Options with { ItalicFonts = Chain(fonts) }; return this; }

    /// <summary>Sets the ordered fallback chain for text that is both bold and italic.</summary>
    /// <param name="fonts">Fonts tried in order for each character.</param>
    public PdfFontSetup BoldItalic(params PdfFont[] fonts) { Options = Options with { BoldItalicFonts = Chain(fonts) }; return this; }

    /// <summary>Sets the root font size in points, from 1 to 144. The default is 12pt.</summary>
    /// <param name="points">The size in points.</param>
    public PdfFontSetup Size(double points)
    {
        if (!double.IsFinite(points) || points is < 1 or > 144)
            throw new ArgumentOutOfRangeException(nameof(points), points, "Font size must be from 1 to 144 points.");
        Options = Options with { FontSize = points };
        return this;
    }

    /// <summary>Embeds only the glyphs the document uses, where the font's embedding permissions allow it.</summary>
    public PdfFontSetup Subset() { Options = Options with { SubsetFonts = true }; return this; }

    private static PdfFont[] Chain(PdfFont[] fonts)
    {
        ArgumentNullException.ThrowIfNull(fonts);
        foreach (var font in fonts) ArgumentNullException.ThrowIfNull(font, nameof(fonts));
        return [.. fonts];
    }
}

/// <summary>Configures paper size, orientation and margins for <see cref="PdfWriter.WithPage"/>.</summary>
public sealed class PdfPageSetup
{
    private PdfOptions options;
    private bool? landscape;

    internal PdfPageSetup(PdfOptions options) => this.options = options;

    /// <summary>ISO A4, 210 × 297 mm. This is the default.</summary>
    public PdfPageSetup A4() => Size(595.28, 841.89, PdfUnit.Point);

    /// <summary>US Letter, 8.5 × 11 in.</summary>
    public PdfPageSetup Letter() => Size(612, 792, PdfUnit.Point);

    /// <summary>Sets a custom paper size; each side must be from 1 to 200 inches.</summary>
    /// <param name="width">Paper width.</param>
    /// <param name="height">Paper height.</param>
    /// <param name="unit">Unit of <paramref name="width"/> and <paramref name="height"/>.</param>
    public PdfPageSetup Size(double width, double height, PdfUnit unit = PdfUnit.Point)
    {
        var w = Points(width, unit, nameof(width)); var h = Points(height, unit, nameof(height));
        if (w is < 72 or > 14_400) throw new ArgumentOutOfRangeException(nameof(width), width, "Page width must be from 1 to 200 inches.");
        if (h is < 72 or > 14_400) throw new ArgumentOutOfRangeException(nameof(height), height, "Page height must be from 1 to 200 inches.");
        options = options with { PageWidth = w, PageHeight = h };
        return this;
    }

    /// <summary>Turns the paper so its longer side is horizontal, whatever order the size was set in.</summary>
    public PdfPageSetup Landscape() { landscape = true; return this; }

    /// <summary>Turns the paper so its longer side is vertical, whatever order the size was set in.</summary>
    public PdfPageSetup Portrait() { landscape = false; return this; }

    /// <summary>Sets the margin on all four sides; it must be less than half the shorter paper side. The default is 36pt.</summary>
    /// <param name="value">Margin size.</param>
    /// <param name="unit">Unit of <paramref name="value"/>.</param>
    public PdfPageSetup Margin(double value, PdfUnit unit = PdfUnit.Point)
    {
        options = options with { Margin = Points(value, unit, nameof(value), allowZero: true) };
        return this;
    }

    internal PdfOptions Build()
    {
        var (w, h) = (options.PageWidth, options.PageHeight);
        if (landscape == true && w < h || landscape == false && w > h) (w, h) = (h, w);
        if (2 * options.Margin >= Math.Min(w, h))
            throw new ArgumentOutOfRangeException(nameof(Margin), options.Margin, "Margin must be less than half the shorter page side.");
        return options with { PageWidth = w, PageHeight = h };
    }

    private static double Points(double value, PdfUnit unit, string name, bool allowZero = false)
    {
        if (!double.IsFinite(value) || value < 0 || (!allowZero && value == 0))
            throw new ArgumentOutOfRangeException(name, value, allowZero ? "Must be zero or positive." : "Must be positive.");
        return unit switch
        {
            PdfUnit.Point => value,
            PdfUnit.Pixel => value * 0.75,
            PdfUnit.Millimetre => value * 72 / 25.4,
            PdfUnit.Centimetre => value * 72 / 2.54,
            PdfUnit.Inch => value * 72,
            _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unknown unit."),
        };
    }
}

/// <summary>Configures PDF document properties for <see cref="PdfWriter.WithMetadata"/>.</summary>
public sealed class PdfMetadataSetup
{
    internal PdfMetadataSetup(PdfOptions options) => Options = options;

    internal PdfOptions Options { get; private set; }

    /// <summary>Sets the document title.</summary>
    /// <param name="title">Visible to anyone who receives the PDF.</param>
    public PdfMetadataSetup Title(string title) { Options = Options with { Title = title }; return this; }

    /// <summary>Sets the document author.</summary>
    /// <param name="author">Visible to anyone who receives the PDF.</param>
    public PdfMetadataSetup Author(string author) { Options = Options with { Author = author }; return this; }

    /// <summary>Sets the document subject.</summary>
    /// <param name="subject">Visible to anyone who receives the PDF.</param>
    public PdfMetadataSetup Subject(string subject) { Options = Options with { Subject = subject }; return this; }

    /// <summary>Sets the document keywords.</summary>
    /// <param name="keywords">Visible to anyone who receives the PDF.</param>
    public PdfMetadataSetup Keywords(string keywords) { Options = Options with { Keywords = keywords }; return this; }
}

/// <summary>Configures the resource limits for <see cref="PdfWriter.WithLimits"/>. Every limit must be positive.</summary>
/// <remarks>Input beyond a limit fails with a <see cref="FactsPdfException"/> instead of consuming unbounded time or memory.</remarks>
public sealed class PdfLimitsSetup
{
    internal PdfLimitsSetup(PdfOptions options) => Options = options;

    internal PdfOptions Options { get; private set; }

    /// <summary>Maximum UTF-16 units of HTML plus page text (FPDF1001). Default 1,000,000.</summary>
    /// <param name="value">A positive limit.</param>
    public PdfLimitsSetup MaxInputCharacters(int value) => Set(value, o => o with { MaxInputCharacters = value });

    /// <summary>Maximum number of HTML elements (FPDF1002). Default 50,000.</summary>
    /// <param name="value">A positive limit.</param>
    public PdfLimitsSetup MaxElements(int value) => Set(value, o => o with { MaxElements = value });

    /// <summary>Maximum element nesting depth (FPDF1003). Default 128.</summary>
    /// <param name="value">A positive limit.</param>
    public PdfLimitsSetup MaxDepth(int value) => Set(value, o => o with { MaxDepth = value });

    /// <summary>Maximum number of pages (FPDF1303). Default 1,000.</summary>
    /// <param name="value">A positive limit.</param>
    public PdfLimitsSetup MaxPages(int value) => Set(value, o => o with { MaxPages = value });

    /// <summary>Maximum PDF size in bytes (FPDF1401). Default 16 MiB.</summary>
    /// <param name="value">A positive limit.</param>
    public PdfLimitsSetup MaxOutputBytes(int value) => Set(value, o => o with { MaxOutputBytes = value });

    /// <summary>Maximum combined size of all supplied fonts in bytes (FPDF1503). Default 64 MiB.</summary>
    /// <param name="value">A positive limit.</param>
    public PdfLimitsSetup MaxTotalFontBytes(int value) => Set(value, o => o with { MaxTotalFontBytes = value });

    /// <summary>Maximum number of bound images. Default 16.</summary>
    /// <param name="value">A positive limit.</param>
    public PdfLimitsSetup MaxImageResources(int value) => Set(value, o => o with { MaxImageResources = value });

    /// <summary>Maximum combined encoded size of all bound images in bytes. Default 32 MiB.</summary>
    /// <param name="value">A positive limit.</param>
    public PdfLimitsSetup MaxTotalImageBytes(int value) => Set(value, o => o with { MaxTotalImageBytes = value });

    /// <summary>Maximum UTF-16 units of CSS in style elements and attributes. Default 262,144.</summary>
    /// <param name="value">A positive limit.</param>
    public PdfLimitsSetup MaxCssCharacters(int value) => Set(value, o => o with { MaxCssCharacters = value });

    /// <summary>Maximum number of CSS selectors. Default 4,096.</summary>
    /// <param name="value">A positive limit.</param>
    public PdfLimitsSetup MaxCssSelectors(int value) => Set(value, o => o with { MaxCssSelectors = value });

    /// <summary>Maximum number of CSS declarations. Default 32,768.</summary>
    /// <param name="value">A positive limit.</param>
    public PdfLimitsSetup MaxCssDeclarations(int value) => Set(value, o => o with { MaxCssDeclarations = value });

    /// <summary>Work budget for selector matching and cascading (FPDF1205). Default 5,000,000.</summary>
    /// <param name="value">A positive limit.</param>
    public PdfLimitsSetup MaxCssMatchOperations(int value) => Set(value, o => o with { MaxCssMatchOperations = value });

    /// <summary>Maximum number of CSS syntax nodes. Default 131,072.</summary>
    /// <param name="value">A positive limit.</param>
    public PdfLimitsSetup MaxCssSyntaxNodes(int value) => Set(value, o => o with { MaxCssSyntaxNodes = value });

    /// <summary>Maximum nesting depth of CSS blocks and functions. Default 64.</summary>
    /// <param name="value">A positive limit.</param>
    public PdfLimitsSetup MaxCssSyntaxDepth(int value) => Set(value, o => o with { MaxCssSyntaxDepth = value });

    /// <summary>Maximum text and box paint operations across all pages (FPDF1401). Default 200,000.</summary>
    /// <param name="value">A positive limit.</param>
    public PdfLimitsSetup MaxDisplayCommands(int value) => Set(value, o => o with { MaxDisplayCommands = value });

    private PdfLimitsSetup Set(int value, Func<PdfOptions, PdfOptions> apply)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        Options = apply(Options);
        return this;
    }
}

/// <summary>Configures header and footer regions for <see cref="PdfWriter.WithPageFurniture(Action{PdfPageFurnitureSetup})"/>.</summary>
/// <remarks>Templates are literal single-line text with <c>{page}</c>, <c>{pages}</c>, <c>{{</c> and <c>}}</c> substitutions.</remarks>
public sealed class PdfPageFurnitureSetup
{
    private readonly PdfPageRegionsSetup pages = new();
    private PdfPageRegionsSetup? firstPage;

    internal PdfPageFurnitureSetup() { }

    /// <summary>Left-aligned text in the top margin.</summary>
    /// <param name="template">Text with optional <c>{page}</c> and <c>{pages}</c> placeholders.</param>
    /// <param name="style">Optional size, colour and weight; the default is normal 10pt black.</param>
    public PdfPageFurnitureSetup HeaderLeft(string template, Action<PdfPageTextStyle>? style = null) { pages.HeaderLeft(template, style); return this; }

    /// <summary>Centred text in the top margin.</summary>
    /// <param name="template">Text with optional <c>{page}</c> and <c>{pages}</c> placeholders.</param>
    /// <param name="style">Optional size, colour and weight; the default is normal 10pt black.</param>
    public PdfPageFurnitureSetup HeaderCenter(string template, Action<PdfPageTextStyle>? style = null) { pages.HeaderCenter(template, style); return this; }

    /// <summary>Right-aligned text in the top margin.</summary>
    /// <param name="template">Text with optional <c>{page}</c> and <c>{pages}</c> placeholders.</param>
    /// <param name="style">Optional size, colour and weight; the default is normal 10pt black.</param>
    public PdfPageFurnitureSetup HeaderRight(string template, Action<PdfPageTextStyle>? style = null) { pages.HeaderRight(template, style); return this; }

    /// <summary>Left-aligned text in the bottom margin.</summary>
    /// <param name="template">Text with optional <c>{page}</c> and <c>{pages}</c> placeholders.</param>
    /// <param name="style">Optional size, colour and weight; the default is normal 10pt black.</param>
    public PdfPageFurnitureSetup FooterLeft(string template, Action<PdfPageTextStyle>? style = null) { pages.FooterLeft(template, style); return this; }

    /// <summary>Centred text in the bottom margin.</summary>
    /// <param name="template">Text with optional <c>{page}</c> and <c>{pages}</c> placeholders.</param>
    /// <param name="style">Optional size, colour and weight; the default is normal 10pt black.</param>
    public PdfPageFurnitureSetup FooterCenter(string template, Action<PdfPageTextStyle>? style = null) { pages.FooterCenter(template, style); return this; }

    /// <summary>Right-aligned text in the bottom margin.</summary>
    /// <param name="template">Text with optional <c>{page}</c> and <c>{pages}</c> placeholders.</param>
    /// <param name="style">Optional size, colour and weight; the default is normal 10pt black.</param>
    public PdfPageFurnitureSetup FooterRight(string template, Action<PdfPageTextStyle>? style = null) { pages.FooterRight(template, style); return this; }

    /// <summary>Uses different regions on page 1, such as a cover page; they replace all six regions on that page.</summary>
    /// <param name="configure">Configures the first-page regions; regions left unset are blank on page 1.</param>
    public PdfPageFurnitureSetup FirstPage(Action<PdfPageRegionsSetup> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        firstPage = new PdfPageRegionsSetup();
        configure(firstPage);
        return this;
    }

    internal PdfPageFurniture Build() => new() { Default = pages.Build(), FirstPage = firstPage?.Build() };
}

/// <summary>Configures the six header and footer regions of one page variant.</summary>
public sealed class PdfPageRegionsSetup
{
    private PdfPageFurnitureRegions regions = new();

    internal PdfPageRegionsSetup() { }

    /// <summary>Left-aligned text in the top margin.</summary>
    /// <param name="template">Text with optional <c>{page}</c> and <c>{pages}</c> placeholders.</param>
    /// <param name="style">Optional size, colour and weight; the default is normal 10pt black.</param>
    public PdfPageRegionsSetup HeaderLeft(string template, Action<PdfPageTextStyle>? style = null) { regions = regions with { HeaderLeft = Text(template, style) }; return this; }

    /// <summary>Centred text in the top margin.</summary>
    /// <param name="template">Text with optional <c>{page}</c> and <c>{pages}</c> placeholders.</param>
    /// <param name="style">Optional size, colour and weight; the default is normal 10pt black.</param>
    public PdfPageRegionsSetup HeaderCenter(string template, Action<PdfPageTextStyle>? style = null) { regions = regions with { HeaderCenter = Text(template, style) }; return this; }

    /// <summary>Right-aligned text in the top margin.</summary>
    /// <param name="template">Text with optional <c>{page}</c> and <c>{pages}</c> placeholders.</param>
    /// <param name="style">Optional size, colour and weight; the default is normal 10pt black.</param>
    public PdfPageRegionsSetup HeaderRight(string template, Action<PdfPageTextStyle>? style = null) { regions = regions with { HeaderRight = Text(template, style) }; return this; }

    /// <summary>Left-aligned text in the bottom margin.</summary>
    /// <param name="template">Text with optional <c>{page}</c> and <c>{pages}</c> placeholders.</param>
    /// <param name="style">Optional size, colour and weight; the default is normal 10pt black.</param>
    public PdfPageRegionsSetup FooterLeft(string template, Action<PdfPageTextStyle>? style = null) { regions = regions with { FooterLeft = Text(template, style) }; return this; }

    /// <summary>Centred text in the bottom margin.</summary>
    /// <param name="template">Text with optional <c>{page}</c> and <c>{pages}</c> placeholders.</param>
    /// <param name="style">Optional size, colour and weight; the default is normal 10pt black.</param>
    public PdfPageRegionsSetup FooterCenter(string template, Action<PdfPageTextStyle>? style = null) { regions = regions with { FooterCenter = Text(template, style) }; return this; }

    /// <summary>Right-aligned text in the bottom margin.</summary>
    /// <param name="template">Text with optional <c>{page}</c> and <c>{pages}</c> placeholders.</param>
    /// <param name="style">Optional size, colour and weight; the default is normal 10pt black.</param>
    public PdfPageRegionsSetup FooterRight(string template, Action<PdfPageTextStyle>? style = null) { regions = regions with { FooterRight = Text(template, style) }; return this; }

    internal PdfPageFurnitureRegions Build() => regions;

    private static PdfPageText Text(string template, Action<PdfPageTextStyle>? style)
    {
        ArgumentNullException.ThrowIfNull(template);
        var text = new PdfPageTextStyle(new PdfPageText { Template = template });
        style?.Invoke(text);
        return text.Text;
    }
}

/// <summary>Styles one header or footer region.</summary>
public sealed class PdfPageTextStyle
{
    internal PdfPageTextStyle(PdfPageText text) => Text = text;

    internal PdfPageText Text { get; private set; }

    /// <summary>Font size in points, from 1 to 144. The default is 10pt.</summary>
    /// <param name="points">The size in points.</param>
    public PdfPageTextStyle Size(double points)
    {
        if (!double.IsFinite(points) || points is < 1 or > 144)
            throw new ArgumentOutOfRangeException(nameof(points), points, "Font size must be from 1 to 144 points.");
        Text = Text with { FontSize = points };
        return this;
    }

    /// <summary>Opaque text colour. The default is black.</summary>
    /// <param name="red">Red channel, 0–255.</param>
    /// <param name="green">Green channel, 0–255.</param>
    /// <param name="blue">Blue channel, 0–255.</param>
    public PdfPageTextStyle Color(byte red, byte green, byte blue) { Text = Text with { Color = new PdfRgbColor(red, green, blue) }; return this; }

    /// <summary>Uses the bold font chain.</summary>
    public PdfPageTextStyle Bold() { Text = Text with { Bold = true }; return this; }

    /// <summary>Uses the italic font chain, or the bold-italic chain together with <see cref="Bold"/>.</summary>
    public PdfPageTextStyle Italic() { Text = Text with { Italic = true }; return this; }
}
