using FactsPDF;

// The only engine-API surface the benchmark harness touches. Program.cs and the csproj are
// identical across compared engine revisions; a revision whose engine API differs supplies its own thin adapter.
internal sealed class BenchmarkEngine(IReadOnlyList<PdfFont> fonts, bool subset)
{
    // The comparison is of layout and resource cost, so the PDF is untagged, as every earlier revision wrote it.
    private readonly PdfWriter writer = Pdf.Write().WithTaggedPdf(false).WithFonts(f =>
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
