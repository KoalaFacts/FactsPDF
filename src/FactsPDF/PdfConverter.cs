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
        return new(pageCount, length) { PdfUa = pdf.PdfUa };
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
        if (!options.Strict) options = options with { CssWarnings = new CssWarnings(options.MaxCssWarnings) };
        if ((options.HeaderText?.Length ?? 0) > 1024 || (options.FooterText?.Length ?? 0) > 1024)
            throw new FactsPdfException("FPDF1001", "Each header/footer field is limited to 1024 UTF-16 units.");
        var furnitureCharacters = PageFurnitureLayout.ValidateInput(options);
        if ((long)html.Length + (options.HeaderText?.Length ?? 0) + (options.FooterText?.Length ?? 0) + furnitureCharacters + metadataCharacters > options.MaxInputCharacters)
            throw new FactsPdfException("FPDF1001", "HTML and page text exceed MaxInputCharacters.");

        var document = HtmlDocumentReader.ReadTree(html, options, cancellationToken);
        try { return Render(document, options, cancellationToken, out pageCount); }
        finally { document.Structure?.Release(); }
    }

    private static PdfBuffer Render(DocumentRoot document, PdfOptions options, CancellationToken cancellationToken, out int pageCount)
    {
        if (document.Structure is { } structure)
        {
            // Tagged PDF takes the document title from the title element when none is set explicitly.
            if (options.Title is null && structure.Title is { } documentTitle)
                options = options with { Title = documentTitle };
            if (structure.FiguresWithoutAlt > 0)
                options.CssWarnings?.Add("FPDF1105", structure.FiguresWithoutAlt == 1
                    ? "An img or svg has no alt text, so its Figure has no description (use alt=\"\" for decoration); the PDF is not PDF/UA."
                    : $"{structure.FiguresWithoutAlt} img or svg elements have no alt text, so their Figures have no description (use alt=\"\" for decoration); the PDF is not PDF/UA.", -1);
        }
        // CSS @page sets the page geometry and margin boxes before any layout reads them.
        if (document.PageRules is { } pageRules)
        {
            options = pageRules.Apply(options, document.RootStyle);
            if (Problem(options) is { } problem)
                throw new FactsPdfException("FPDF1202", "@page produces invalid page options: " + problem.Message);
        }
        var pages = TextLayout.Layout(document, options, cancellationToken);
        // The root background is the canvas: Chrome prints it over each whole sheet, margins
        // included. Opaque white paints nothing visible on paper, so it adds no command.
        if (document.CanvasColor is { IsInvisible: false } canvas && canvas != new Rgb(1, 1, 1))
        {
            var sheet = BoxStyle.Initial with { BackgroundColor = canvas };
            foreach (var page in pages)
                page.PaintBoxes.Insert(0, new PaintedBox(0, 0, options.PageWidth, options.PageHeight, sheet, canvas, int.MinValue));
        }
        long displayCommands = 0;
        foreach (var page in pages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            displayCommands += (long)page.Runs.Count + page.Runs.Sum(run => TextDecorationCommands.Count(run.Style.Decoration))
                + page.Runs.Count(run => run.Style.LinkUri is not null)
                + page.Discs.Count;
            foreach (var image in page.Images) displayCommands += image.CommandCount;
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
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            options.WarningHandler?.Invoke(options.CssWarnings?.Report() ?? new PdfWarnings([], 0));
        }
        catch { pdf.Dispose(); throw; }
        pageCount = pages.Count;
        return pdf;
    }

    // ParamName is always the public "options" parameter; the message names the property.
    private static void Validate(PdfOptions options)
    {
        if (Problem(options) is { } problem)
            throw problem.Value is { } value
                ? new ArgumentOutOfRangeException(nameof(options), value, problem.Message)
                : new ArgumentOutOfRangeException(nameof(options), problem.Message);
    }

    // The same invariants hold for the writer's options and for the options CSS @page derives from them.
    private static (object? Value, string Message)? Problem(PdfOptions o)
    {
        if (!double.IsFinite(o.PageWidth) || o.PageWidth is < 72 or > 14_400)
            return (o.PageWidth, "PageWidth must be from 72 to 14400 points.");
        if (!double.IsFinite(o.PageHeight) || o.PageHeight is < 72 or > 14_400)
            return (o.PageHeight, "PageHeight must be from 72 to 14400 points.");
        if (!double.IsFinite(o.Margin) || o.Margin < 0 || 2 * o.Margin >= Math.Min(o.PageWidth, o.PageHeight))
            return (o.Margin, "Margin must be nonnegative and less than half the shorter page side.");
        if (o.SideMargins is { } sides && (
                !double.IsFinite(sides.Top) || !double.IsFinite(sides.Right) || !double.IsFinite(sides.Bottom) || !double.IsFinite(sides.Left)
                || sides.Top < 0 || sides.Right < 0 || sides.Bottom < 0 || sides.Left < 0
                || sides.Top + sides.Bottom >= o.PageHeight || sides.Left + sides.Right >= o.PageWidth))
            return (null, "Each margin must be nonnegative and the margins must leave a page area.");
        if (!double.IsFinite(o.FontSize) || o.FontSize is < 1 or > 144)
            return (o.FontSize, "FontSize must be from 1 to 144 points.");
        if (o.MaxInputCharacters < 1 || o.MaxElements < 1 || o.MaxDepth < 1 || o.MaxPages < 1 || o.MaxOutputBytes < 1 || o.MaxTotalFontBytes < 1 || o.MaxCssSyntaxNodes < 1 || o.MaxCssSyntaxDepth < 1 || o.MaxCssCustomProperties < 1 || o.MaxDisplayCommands < 1 || o.MaxCssWarnings < 1
            || o.MaxImageResources < 1 || o.MaxTotalImageBytes < 1)
            return (null, "All resource limits must be positive.");
        return null;
    }
}
