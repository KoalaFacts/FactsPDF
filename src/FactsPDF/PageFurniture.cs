namespace FactsPDF;

/// <summary>A single, bounded line of text in a page header or footer.</summary>
public sealed record PdfPageText
{
    /// <summary>Literal text with {page}, {pages}, {{ and }} substitutions.</summary>
    public string Template { get; init; } = "";
    public double FontSize { get; init; } = 10;
    public PdfRgbColor Color { get; init; } = new(0, 0, 0);
    public bool Bold { get; init; }
    public bool Italic { get; init; }
}

/// <summary>Opaque RGB color; each channel is 0–255.</summary>
public readonly record struct PdfRgbColor(byte R, byte G, byte B);

/// <summary>Six independently styled regions inside the existing page margins.</summary>
public sealed record PdfPageFurnitureRegions
{
    public PdfPageText? HeaderLeft { get; init; }
    public PdfPageText? HeaderCenter { get; init; }
    public PdfPageText? HeaderRight { get; init; }
    public PdfPageText? FooterLeft { get; init; }
    public PdfPageText? FooterCenter { get; init; }
    public PdfPageText? FooterRight { get; init; }
}

/// <summary>Optional formal page furniture; FirstPage replaces all six default fields.</summary>
public sealed record PdfPageFurniture
{
    public PdfPageFurnitureRegions Default { get; init; } = new();
    public PdfPageFurnitureRegions? FirstPage { get; init; }
}
