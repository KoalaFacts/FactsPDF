namespace FactsPDF;

/// <summary>Converts controlled HTML and CSS to PDF without a browser.</summary>
/// <remarks>The supported subset is listed at https://koalafacts.github.io/FactsPDF/. Conversion is deterministic,
/// performs no file or network access, and is safe to call concurrently.</remarks>
public static class PdfConverter
{
    /// <summary>Renders an HTML document to PDF and writes it to <paramref name="output"/>.</summary>
    /// <param name="html">The complete HTML document.</param>
    /// <param name="output">Writable destination, which is not closed. Any failure before the finished PDF is copied leaves it untouched;
    /// an I/O failure or cancellation during that final copy may leave a partial prefix. Write to a temporary file and move it
    /// into place, as the CLI does, when the destination must never hold a partial PDF.</param>
    /// <param name="options">Geometry, fonts, images and limits; null uses the defaults.</param>
    /// <param name="cancellationToken">Cancels the conversion.</param>
    /// <returns>The page count and number of bytes written.</returns>
    /// <exception cref="FactsPdfException">The input uses an unsupported feature, is malformed, or exceeds a limit.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An option is outside its valid range.</exception>
    /// <exception cref="ArgumentException"><paramref name="output"/> is not writable.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="html"/> or <paramref name="output"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    public static PdfConversionResult Convert(string html, Stream output,
        PdfOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(html);
        ArgumentNullException.ThrowIfNull(output);
        if (!output.CanWrite) throw new ArgumentException("Output must be writable.", nameof(output));
        options ??= new PdfOptions();
        cancellationToken.ThrowIfCancellationRequested();
        Validate(options);
        var metadataCharacters = PdfDocumentMetadata.Validate(options);
        PageFurnitureLayout.Validate(options);
        options = FontRoles.Snapshot(options);
        options = ImageResources.Snapshot(options, cancellationToken);
        if ((options.HeaderText?.Length ?? 0) > 1024 || (options.FooterText?.Length ?? 0) > 1024)
            throw new FactsPdfException("FPDF1001", "Each header/footer field is limited to 1024 UTF-16 units.");
        var furnitureCharacters = PageFurnitureLayout.ValidateInput(options);
        if ((long)html.Length + (options.HeaderText?.Length ?? 0) + (options.FooterText?.Length ?? 0) + furnitureCharacters + metadataCharacters > options.MaxInputCharacters)
            throw new FactsPdfException("FPDF1001", "HTML and page text exceed MaxInputCharacters.");

        var document = HtmlDocumentReader.ReadTree(html, options, cancellationToken);
        var pages = TextLayout.Layout(document, options, cancellationToken);
        long displayCommands = 0;
        foreach (var page in pages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            displayCommands += (long)page.Runs.Count + page.Runs.Sum(run => TextDecorationCommands.Count(run.Style.Decoration))
                + page.Runs.Count(run => run.Style.LinkUri is not null)
                + page.Discs.Count + page.Images.Count;
            foreach (var box in page.PaintBoxes) displayCommands += box.CommandCount;
            if (displayCommands > options.MaxDisplayCommands)
                throw new FactsPdfException("FPDF1401", "PDF display command limit exceeded.");
        }
        if (options.PageFurniture is not null)
            PageFurnitureLayout.Append(pages, options, displayCommands, cancellationToken);
        else if (options.PageNumbers || options.HeaderText is not null || options.FooterText is not null)
            PageMarginLayout.Append(pages, options, displayCommands, cancellationToken);
        using var pdf = pages.Any(p => p.Runs.Any(r => r.Font is not null))
            ? UnicodePdfSerializer.Build(pages, options, cancellationToken)
            : PdfSerializer.Build(pages, options, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var data = pdf.GetBuffer(); var length = checked((int)pdf.Length);
        for (var offset = 0; offset < length; offset += Math.Min(16_384, length - offset))
        {
            cancellationToken.ThrowIfCancellationRequested();
            output.Write(data.AsSpan(offset, Math.Min(16_384, length - offset)));
        }
        return new(pages.Count, length);
    }

    // ParamName is always the public "options" parameter; the message names the property.
    private static void Validate(PdfOptions options)
    {
        var o = options;
        if (!double.IsFinite(o.PageWidth) || o.PageWidth is < 72 or > 14_400)
            throw new ArgumentOutOfRangeException(nameof(options), o.PageWidth, "PageWidth must be from 72 to 14400 points.");
        if (!double.IsFinite(o.PageHeight) || o.PageHeight is < 72 or > 14_400)
            throw new ArgumentOutOfRangeException(nameof(options), o.PageHeight, "PageHeight must be from 72 to 14400 points.");
        if (!double.IsFinite(o.Margin) || o.Margin < 0 || 2 * o.Margin >= Math.Min(o.PageWidth, o.PageHeight))
            throw new ArgumentOutOfRangeException(nameof(options), o.Margin, "Margin must be nonnegative and less than half the shorter page side.");
        if (!double.IsFinite(o.FontSize) || o.FontSize is < 1 or > 144)
            throw new ArgumentOutOfRangeException(nameof(options), o.FontSize, "FontSize must be from 1 to 144 points.");
        if (o.MaxInputCharacters < 1 || o.MaxElements < 1 || o.MaxDepth < 1 || o.MaxPages < 1 || o.MaxOutputBytes < 1 || o.MaxTotalFontBytes < 1 || o.MaxCssSyntaxNodes < 1 || o.MaxCssSyntaxDepth < 1 || o.MaxDisplayCommands < 1
            || o.MaxImageResources < 1 || o.MaxTotalImageBytes < 1)
            throw new ArgumentOutOfRangeException(nameof(options), "All resource limits must be positive.");
    }
}
