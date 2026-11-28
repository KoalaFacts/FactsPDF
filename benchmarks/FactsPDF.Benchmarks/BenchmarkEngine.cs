using FactsPDF;

// The only engine-API surface the benchmark harness touches. Program.cs and the csproj are
// identical across compared engine revisions; a revision whose engine API differs supplies its own thin adapter.
internal sealed class BenchmarkEngine(IReadOnlyList<PdfFont> fonts, bool subset)
{
    private readonly PdfWriter writer = Pdf.Write().WithFonts(f =>
    {
        f.Regular([.. fonts]);
        if (subset) f.Subset();
    });

    internal static PdfFont LoadFont(byte[] data) => PdfFont.FromBytes(data);

    internal (long BytesWritten, int PageCount) Render(string html, Stream output)
    {
        var result = writer.ToStream(html, output);
        return (result.BytesWritten, result.PageCount);
    }
}
