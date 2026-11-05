namespace FactsPDF;

/// <summary>A single, bounded line of text in a page header or footer.</summary>
public sealed record PdfPageText
{
    /// <summary>Literal text with {page}, {pages}, {{ and }} substitutions.</summary>
    public string Template { get; init; } = "";
    /// <summary>Font size in points, from 1 to 144. The default is 10pt.</summary>
    public double FontSize { get; init; } = 10;
    /// <summary>Text color. The default is black.</summary>
    public PdfRgbColor Color { get; init; } = new(0, 0, 0);
    /// <summary>Use the bold font chain (<see cref="PdfOptions.BoldFonts"/>).</summary>
    public bool Bold { get; init; }
    /// <summary>Use the italic font chain (<see cref="PdfOptions.ItalicFonts"/>, or <see cref="PdfOptions.BoldItalicFonts"/> with <see cref="Bold"/>).</summary>
    public bool Italic { get; init; }
}

/// <summary>Opaque RGB color; each channel is 0–255.</summary>
/// <param name="R">Red channel.</param>
/// <param name="G">Green channel.</param>
/// <param name="B">Blue channel.</param>
public readonly record struct PdfRgbColor(byte R, byte G, byte B);

/// <summary>Six independently styled regions inside the existing page margins.</summary>
public sealed record PdfPageFurnitureRegions
{
    /// <summary>Left-aligned text in the top margin.</summary>
    public PdfPageText? HeaderLeft { get; init; }
    /// <summary>Centered text in the top margin.</summary>
    public PdfPageText? HeaderCenter { get; init; }
    /// <summary>Right-aligned text in the top margin.</summary>
    public PdfPageText? HeaderRight { get; init; }
    /// <summary>Left-aligned text in the bottom margin.</summary>
    public PdfPageText? FooterLeft { get; init; }
    /// <summary>Centered text in the bottom margin.</summary>
    public PdfPageText? FooterCenter { get; init; }
    /// <summary>Right-aligned text in the bottom margin.</summary>
    public PdfPageText? FooterRight { get; init; }
}

/// <summary>Optional formal page furniture; FirstPage replaces all six default fields.</summary>
public sealed record PdfPageFurniture
{
    /// <summary>Regions used on every page, or on every page after the first when <see cref="FirstPage"/> is set.</summary>
    public PdfPageFurnitureRegions Default { get; init; } = new();
    /// <summary>Optional regions for page 1 only; when set, they replace all six default regions on that page.</summary>
    public PdfPageFurnitureRegions? FirstPage { get; init; }
}
