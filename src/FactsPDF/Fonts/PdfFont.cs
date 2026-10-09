namespace FactsPDF;

/// <summary>Immutable explicitly supplied font. This commit is the deliberate test-first baseline.</summary>
public sealed class PdfFont
{
    public const int DefaultMaxBytes = 33_554_432;
    public string PostScriptName => "";
    public int ByteLength => 0;
    private PdfFont() { }
    public static PdfFont LoadTrueType(ReadOnlySpan<byte> source, int maxBytes = DefaultMaxBytes,
        CancellationToken cancellationToken = default)
        => throw new FactsPdfException("FPDF1501", "Font loading not implemented in the test baseline.");
    public static PdfFont LoadTrueType(Stream source, int maxBytes = DefaultMaxBytes,
        CancellationToken cancellationToken = default)
        => throw new FactsPdfException("FPDF1501", "Font loading not implemented in the test baseline.");
    internal ushort GlyphFor(int scalar) => 0;
    internal double Width1000(ushort glyph) => 0;
}
