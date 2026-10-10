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
    /// <summary>Ordered explicit font fallback chain. Empty retains ASCII/Courier.</summary>
    public IReadOnlyList<PdfFont> Fonts { get; init; } = Array.Empty<PdfFont>();
    public int MaxTotalFontBytes { get; init; } = 67_108_864;
    /// <summary>Opt in to glyph subsetting where the supplied font permits it; otherwise embed fully.</summary>
    public bool SubsetFonts { get; init; }
    public int MaxCssCharacters { get; init; } = 262_144;
    public int MaxCssSelectors { get; init; } = 4_096;
    public int MaxCssDeclarations { get; init; } = 32_768;
    public int MaxCssMatchOperations { get; init; } = 5_000_000;
    public int MaxCssSyntaxNodes { get; init; } = 131_072;
    public int MaxCssSyntaxDepth { get; init; } = 64;
    /// <summary>Upper bound of text and primitive box paint operations, all pages.</summary>
    public int MaxDisplayCommands { get; init; } = 200_000;
}

public sealed record PdfConversionResult(int PageCount, long BytesWritten);

public sealed class FactsPdfException(string code, string message, int sourceOffset = -1)
    : Exception($"{code}: {message}")
{
    public string Code { get; } = code;
    public int SourceOffset { get; } = sourceOffset;
}
