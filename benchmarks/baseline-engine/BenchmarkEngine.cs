using FactsPDF;

// Adapter for engine revisions before the Pdf.Write() SDK (public PdfConverter/PdfOptions).
// The same-harness workflow copies this file next to the shared Program.cs in the baseline
// checkout; it is not compiled in the current tree.
internal sealed class BenchmarkEngine(IReadOnlyList<PdfFont> fonts, bool subset)
{
    private readonly PdfOptions options = new() { Fonts = fonts, SubsetFonts = subset };

    internal static PdfFont LoadFont(byte[] data) => PdfFont.LoadTrueType(data);

    internal (long BytesWritten, int PageCount) Render(string html, Stream output)
    {
        var result = PdfConverter.Convert(html, output, options);
        return (result.BytesWritten, result.PageCount);
    }
}
