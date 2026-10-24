namespace FactsPDF;

/// <summary>Limits and page geometry for the experimental text renderer. Units are PDF points.</summary>
public sealed record PdfOptions
{
    public double PageWidth { get; init; } = 595.28;
    public double PageHeight { get; init; } = 841.89;
    public double Margin { get; init; } = 36;
    public double FontSize { get; init; } = 12;
    /// <summary>Opt in to centered decimal page numbers in the existing bottom margin, using normal 10pt text.</summary>
    public bool PageNumbers { get; init; }
    /// <summary>Optional six-region styled page furniture; mutually exclusive with legacy margin text.</summary>
    public PdfPageFurniture? PageFurniture { get; init; }
    /// <summary>Optional literal single-line header, normal 10pt text in the existing top margin; at most 1024 UTF-16 units.</summary>
    public string? HeaderText { get; init; }
    /// <summary>Optional literal single-line footer; PageNumbers appends " | N". At most 1024 UTF-16 units.</summary>
    public string? FooterText { get; init; }
    public int MaxInputCharacters { get; init; } = 1_000_000;
    public int MaxElements { get; init; } = 50_000;
    public int MaxDepth { get; init; } = 128;
    public int MaxPages { get; init; } = 1_000;
    public int MaxOutputBytes { get; init; } = 16_777_216;
    /// <summary>Ordered explicit font fallback chain. Empty retains ASCII/Courier.</summary>
    public IReadOnlyList<PdfFont> Fonts { get; init; } = Array.Empty<PdfFont>();
    /// <summary>Ordered explicit fallback for bold text. No synthetic weight or cross-style fallback.</summary>
    public IReadOnlyList<PdfFont> BoldFonts { get; init; } = Array.Empty<PdfFont>();
    /// <summary>Ordered explicit fallback for italic text. No synthetic slant or cross-style fallback.</summary>
    public IReadOnlyList<PdfFont> ItalicFonts { get; init; } = Array.Empty<PdfFont>();
    /// <summary>Ordered explicit fallback for text that is both bold and italic.</summary>
    public IReadOnlyList<PdfFont> BoldItalicFonts { get; init; } = Array.Empty<PdfFont>();
    public int MaxTotalFontBytes { get; init; } = 67_108_864;
    /// <summary>Explicit ordinal resource names used by standalone and image-only table-cell img elements; no implicit file or network resolution.</summary>
    public IReadOnlyDictionary<string, PdfImage> Images { get; init; } = ImageResources.Empty;
    public int MaxImageResources { get; init; } = 16;
    public int MaxTotalImageBytes { get; init; } = 33_554_432;
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
