namespace FactsPDF;

/// <summary>Independent bounds for explicitly supplied image bytes and validation work.</summary>
public sealed record PdfImageLimits
{
    /// <summary>Maximum encoded image size in bytes (8 MiB by default).</summary>
    public int MaxBytes { get; init; } = 8_388_608;
    /// <summary>Maximum width or height in pixels.</summary>
    public int MaxDimension { get; init; } = 8_192;
    /// <summary>Maximum width × height in pixels.</summary>
    public int MaxPixels { get; init; } = 16_777_216;
    /// <summary>Maximum decoded sample bytes, including any PNG alpha channel.</summary>
    public int MaxDecodedBytes { get; init; } = 50_331_648;
    /// <summary>Maximum JPEG minimum coded units (MCUs) validated per image.</summary>
    public int MaxMcuBlocks { get; init; } = 1_048_576;
}
