namespace FactsPDF;

public static class PdfConverter
{
    // TDD baseline: intentionally does not render; replaced by the implementation commit.
    public static PdfConversionResult Convert(string html, Stream output,
        PdfOptions? options = null, CancellationToken cancellationToken = default)
        => new(0, 0);
}
