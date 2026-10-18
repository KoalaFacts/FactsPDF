namespace FactsPDF;

/// <summary>Independent bounds for explicitly supplied image bytes and validation work.</summary>
public sealed record PdfImageLimits
{
    public int MaxBytes { get; init; } = 8_388_608;
    public int MaxDimension { get; init; } = 8_192;
    public int MaxPixels { get; init; } = 16_777_216;
    public int MaxDecodedBytes { get; init; } = 50_331_648;
    public int MaxMcuBlocks { get; init; } = 1_048_576;
}
