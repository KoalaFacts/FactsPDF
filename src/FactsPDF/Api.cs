namespace FactsPDF;

/// <summary>Limits and page geometry for the experimental text renderer. Units are PDF points.</summary>
public sealed record PdfOptions
{
    public double PageWidth { get; init; } = 595.28;
    public double PageHeight { get; init; } = 841.89;
    public double Margin { get; init; } = 36;
    public double FontSize { get; init; } = 12;
    public int MaxInputCharacters { get; init; } = 1_000_000;
    public int MaxElements { get; init; } = 50_000;
    public int MaxDepth { get; init; } = 128;
    public int MaxPages { get; init; } = 1_000;
    public int MaxOutputBytes { get; init; } = 16_777_216;
}

public sealed record PdfConversionResult(int PageCount, long BytesWritten);

public sealed class FactsPdfException(string code, string message, int sourceOffset = -1)
    : Exception($"{code}: {message}")
{
    public string Code { get; } = code;
    public int SourceOffset { get; } = sourceOffset;
}
