namespace FactsPDF;

// The engine entry point behind the public Pdf.Write() builder. Rendering always completes
// in memory before any byte reaches a caller's output, so every output path is atomic up to
// the final copy.
internal static class PdfConverter
{
    internal static PdfWriteResult Convert(string html, Stream output,
        PdfOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (!output.CanWrite) throw new ArgumentException("Output must be writable.", nameof(output));
        using var pdf = Render(html, options, cancellationToken, out var pageCount);
        var data = pdf.WrittenSpan; var length = data.Length;
        for (var offset = 0; offset < length; offset += Math.Min(16_384, length - offset))
        {
            cancellationToken.ThrowIfCancellationRequested();
            output.Write(data.Slice(offset, Math.Min(16_384, length - offset)));
        }
        return new(pageCount, length);
    }

    // Returns the complete PDF in a pooled buffer; read it with WrittenSpan/WrittenMemory, then dispose it.
    internal static PdfBuffer Render(string html, PdfOptions? options, CancellationToken cancellationToken, out int pageCount)
    {
        ArgumentNullException.ThrowIfNull(html);
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
        var pdf = pages.Any(p => p.Runs.Any(r => r.Font is not null))
            ? UnicodePdfSerializer.Build(pages, options, cancellationToken)
            : PdfSerializer.Build(pages, options, cancellationToken);
        try { cancellationToken.ThrowIfCancellationRequested(); }
        catch { pdf.Dispose(); throw; }
        pageCount = pages.Count;
        return pdf;
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
