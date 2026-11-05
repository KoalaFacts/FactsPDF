namespace FactsPDF;

// Engine options. Internal: the public API is the immutable Pdf.Write() builder, which produces these.
internal sealed record PdfOptions
{
    /// <summary>Page width in points, from 72 to 14,400. The default is A4 width (595.28pt).</summary>
    public double PageWidth { get; init; } = 595.28;
    /// <summary>Page height in points, from 72 to 14,400. The default is A4 height (841.89pt).</summary>
    public double PageHeight { get; init; } = 841.89;
    /// <summary>Margin on all four sides in points. Must be nonnegative and less than half the shorter page side. The default is 36pt.</summary>
    public double Margin { get; init; } = 36;
    /// <summary>Optional PDF Info document title; null omits the field.</summary>
    public string? Title { get; init; }
    /// <summary>Optional PDF Info author (visible to PDF recipients).</summary>
    public string? Author { get; init; }
    /// <summary>Optional PDF Info subject.</summary>
    public string? Subject { get; init; }
    /// <summary>Optional PDF Info keywords.</summary>
    public string? Keywords { get; init; }
    /// <summary>Root font size in points, from 1 to 144. The default is 12pt.</summary>
    public double FontSize { get; init; } = 12;
    /// <summary>Opt in to centered decimal page numbers in the existing bottom margin, using normal 10pt text.</summary>
    public bool PageNumbers { get; init; }
    /// <summary>Optional six-region styled page furniture; mutually exclusive with legacy margin text.</summary>
    public PdfPageFurniture? PageFurniture { get; init; }
    /// <summary>Optional literal single-line header, normal 10pt text in the existing top margin; at most 1024 UTF-16 units.</summary>
    public string? HeaderText { get; init; }
    /// <summary>Optional literal single-line footer; PageNumbers appends " | N". At most 1024 UTF-16 units.</summary>
    public string? FooterText { get; init; }
    /// <summary>Maximum UTF-16 units of HTML plus header, footer, furniture and metadata text (FPDF1001).</summary>
    public int MaxInputCharacters { get; init; } = 1_000_000;
    /// <summary>Maximum number of HTML elements (FPDF1002).</summary>
    public int MaxElements { get; init; } = 50_000;
    /// <summary>Maximum element nesting depth (FPDF1003).</summary>
    public int MaxDepth { get; init; } = 128;
    /// <summary>Maximum number of output pages.</summary>
    public int MaxPages { get; init; } = 1_000;
    /// <summary>Maximum size of the generated PDF in bytes (FPDF1401).</summary>
    public int MaxOutputBytes { get; init; } = 16_777_216;
    /// <summary>Ordered explicit font fallback chain. Empty retains ASCII/Courier.</summary>
    public IReadOnlyList<PdfFont> Fonts { get; init; } = Array.Empty<PdfFont>();
    /// <summary>Ordered explicit fallback for bold text. No synthetic weight or cross-style fallback.</summary>
    public IReadOnlyList<PdfFont> BoldFonts { get; init; } = Array.Empty<PdfFont>();
    /// <summary>Ordered explicit fallback for italic text. No synthetic slant or cross-style fallback.</summary>
    public IReadOnlyList<PdfFont> ItalicFonts { get; init; } = Array.Empty<PdfFont>();
    /// <summary>Ordered explicit fallback for text that is both bold and italic.</summary>
    public IReadOnlyList<PdfFont> BoldItalicFonts { get; init; } = Array.Empty<PdfFont>();
    /// <summary>Maximum combined size in bytes of all distinct supplied fonts (FPDF1503).</summary>
    public int MaxTotalFontBytes { get; init; } = 67_108_864;
    /// <summary>Explicit ordinal resource names used by standalone and image-only table-cell img elements; no implicit file or network resolution.</summary>
    public IReadOnlyDictionary<string, PdfImage> Images { get; init; } = ImageResources.Empty;
    /// <summary>Maximum number of entries in <see cref="Images"/>.</summary>
    public int MaxImageResources { get; init; } = 16;
    /// <summary>Maximum combined encoded size in bytes of all supplied images.</summary>
    public int MaxTotalImageBytes { get; init; } = 33_554_432;
    /// <summary>Opt in to glyph subsetting where the supplied font permits it; otherwise embed fully.</summary>
    public bool SubsetFonts { get; init; }
    /// <summary>Maximum UTF-16 units of CSS across embedded style elements and style attributes.</summary>
    public int MaxCssCharacters { get; init; } = 262_144;
    /// <summary>Maximum number of CSS selectors.</summary>
    public int MaxCssSelectors { get; init; } = 4_096;
    /// <summary>Maximum number of CSS declarations.</summary>
    public int MaxCssDeclarations { get; init; } = 32_768;
    /// <summary>Work budget for matching selectors against elements and cascading values.</summary>
    public int MaxCssMatchOperations { get; init; } = 5_000_000;
    /// <summary>Maximum number of CSS syntax nodes (tokens, blocks and components) parsed.</summary>
    public int MaxCssSyntaxNodes { get; init; } = 131_072;
    /// <summary>Maximum nesting depth of CSS blocks and functions.</summary>
    public int MaxCssSyntaxDepth { get; init; } = 64;
    /// <summary>Upper bound of text and primitive box paint operations, all pages.</summary>
    public int MaxDisplayCommands { get; init; } = 200_000;
}

/// <summary>Outcome of a successful <see cref="PdfWriter"/> write.</summary>
/// <param name="PageCount">Number of pages in the generated PDF.</param>
/// <param name="BytesWritten">Number of bytes written to the output.</param>
public sealed record PdfWriteResult(int PageCount, long BytesWritten);

/// <summary>Input that FactsPDF cannot render exactly, or that exceeds a resource limit.</summary>
/// <remarks>Nothing is written to the output stream when this is thrown. Branch on <see cref="Code"/>,
/// not on the message text, which may change between releases.</remarks>
/// <param name="code">Stable error code, such as <c>FPDF1101</c>.</param>
/// <param name="message">Human-readable explanation.</param>
/// <param name="sourceOffset">Zero-based UTF-16 offset into the HTML, or -1 when not available.</param>
public sealed class FactsPdfException(string code, string message, int sourceOffset = -1)
    : Exception($"{code}: {message}")
{
    /// <summary>Stable error code in the form <c>FPDF####</c>; see the error code table in docs/development.md.</summary>
    public string Code { get; } = code;
    /// <summary>Zero-based UTF-16 offset into the HTML near the cause, or -1 when not available. Inline CSS errors point at their element.</summary>
    public int SourceOffset { get; } = sourceOffset;
}
