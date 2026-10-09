namespace FactsPDF;

/// <summary>Experimental, bounded HTML text conversion; see docs/development.md for the supported subset.</summary>
public static class PdfConverter
{
    public static PdfConversionResult Convert(string html, Stream output,
        PdfOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(html);
        ArgumentNullException.ThrowIfNull(output);
        if (!output.CanWrite) throw new ArgumentException("Output must be writable.", nameof(output));
        options ??= new PdfOptions();
        Validate(options);
        cancellationToken.ThrowIfCancellationRequested();
        if (html.Length > options.MaxInputCharacters)
            throw new FactsPdfException("FPDF1001", "HTML exceeds MaxInputCharacters.");

        var paragraphs = HtmlDocumentReader.Read(html, options, cancellationToken);
        var pages = TextLayout.Layout(paragraphs, options, cancellationToken);
        using var pdf = PdfSerializer.Build(pages, options, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        // Content is fully validated before touching the caller's stream.
        var data = pdf.GetBuffer();
        var length = checked((int)pdf.Length);
        for (var offset = 0; offset < length; offset += Math.Min(16_384, length - offset))
        {
            cancellationToken.ThrowIfCancellationRequested();
            output.Write(data.AsSpan(offset, Math.Min(16_384, length - offset)));
        }
        return new(pages.Count, length);
    }

    private static void Validate(PdfOptions o)
    {
        if (!double.IsFinite(o.PageWidth) || o.PageWidth is < 72 or > 14_400)
            throw new ArgumentOutOfRangeException(nameof(o.PageWidth));
        if (!double.IsFinite(o.PageHeight) || o.PageHeight is < 72 or > 14_400)
            throw new ArgumentOutOfRangeException(nameof(o.PageHeight));
        if (!double.IsFinite(o.Margin) || o.Margin < 0 || 2 * o.Margin >= Math.Min(o.PageWidth, o.PageHeight))
            throw new ArgumentOutOfRangeException(nameof(o.Margin));
        if (!double.IsFinite(o.FontSize) || o.FontSize is < 1 or > 144)
            throw new ArgumentOutOfRangeException(nameof(o.FontSize));
        if (o.MaxInputCharacters < 1 || o.MaxElements < 1 || o.MaxDepth < 1 || o.MaxPages < 1 || o.MaxOutputBytes < 1)
            throw new ArgumentOutOfRangeException(nameof(o), "All resource limits must be positive.");
    }
}
