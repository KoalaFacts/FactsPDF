using System.Text;
using static FactsPDF.SfntDirectory;

namespace FactsPDF;

/// <summary>Immutable explicitly supplied static TrueType font. No filesystem or network discovery.</summary>
public sealed class PdfFont
{
    public const int DefaultMaxBytes = 33_554_432;
    private readonly byte[] data;
    private readonly UnicodeCmap cmap;
    private readonly ushort[] advances;
    private readonly double scale;
    public string PostScriptName { get; }
    public int ByteLength => data.Length;
    internal ReadOnlySpan<byte> Data => data;
    internal double Ascent1000 { get; }
    internal double Descent1000 { get; }
    internal double CapHeight1000 { get; }
    internal double XMin1000 { get; }
    internal double YMin1000 { get; }
    internal double XMax1000 { get; }
    internal double YMax1000 { get; }
    internal double ItalicAngle { get; }

    private PdfFont(byte[] ownedBytes, CancellationToken cancellation)
    {
        data = ownedBytes;
        var sfnt = new SfntDirectory(data);
        var head = sfnt.Table("head", 54); var hhea = sfnt.Table("hhea", 36); var maxp = sfnt.Table("maxp", 32);
        Need(U32(head, 12) == 0x5f0f3cf5 && U16(head, 18) is >= 16 and <= 16384 && S16(head, 52) == 0, "Invalid head metrics.");
        Need(U32(maxp, 0) == 0x00010000 && U32(hhea, 0) == 0x00010000 && S16(hhea, 32) == 0, "Invalid TrueType metrics version.");
        var glyphs = U16(maxp, 4); var metrics = U16(hhea, 34);
        Need(glyphs > 0 && metrics > 0 && metrics <= glyphs, "Invalid glyph/metric count.");
        scale = 1000d / U16(head, 18);
        XMin1000 = S16(head, 36) * scale; YMin1000 = S16(head, 38) * scale;
        XMax1000 = S16(head, 40) * scale; YMax1000 = S16(head, 42) * scale;
        Need(XMin1000 <= XMax1000 && YMin1000 <= YMax1000, "Invalid font bounding box.");
        var hmtx = sfnt.Table("hmtx", metrics * 4 + (glyphs - metrics) * 2);
        advances = new ushort[glyphs];
        for (var i = 0; i < glyphs; i++) advances[i] = U16(hmtx, Math.Min(i, metrics - 1) * 4);
        var os2 = sfnt.Table("OS/2", 68); var version = U16(os2, 0); var embedding = U16(os2, 8);
        // Preview-and-print requires read-only document handling that this renderer does not implement.
        // Conservatively decline restricted, preview-only, bitmap-only and unknown permission bits.
        if ((embedding & 0x000e) is not (0 or 8) || (embedding & ~0x010e) != 0)
            throw new FactsPdfException("FPDF1505", "Font embedding permissions are restricted, preview-only, bitmap-only or unsupported. Supply an appropriately licensed installable/editable font.");
        var ascent = (int)S16(hhea, 4); var descent = (int)S16(hhea, 6);
        if (version >= 4 && os2.Length >= 78 && (U16(os2, 62) & 0x80) != 0)
        { ascent = S16(os2, 68); descent = S16(os2, 70); }
        Need(ascent > 0 && descent <= 0, "Invalid vertical font metrics.");
        Ascent1000 = ascent * scale; Descent1000 = descent * scale;
        CapHeight1000 = version >= 2 && os2.Length >= 90 && S16(os2, 88) > 0 ? S16(os2, 88) * scale : Ascent1000;
        if (sfnt.Contains("post")) ItalicAngle = unchecked((int)U32(sfnt.Table("post", 16), 4)) / 65536d;
        var longLoca = S16(head, 50); Need(longLoca is 0 or 1, "Unsupported loca representation.");
        var loca = sfnt.Table("loca", (glyphs + 1) * (longLoca == 1 ? 4 : 2)); var glyf = sfnt.Table("glyf", 0);
        long previous = 0;
        for (var i = 0; i <= glyphs; i++)
        {
            if ((i & 4095) == 0) cancellation.ThrowIfCancellationRequested();
            long offset = longLoca == 1 ? U32(loca, i * 4) : U16(loca, i * 2) * 2L;
            Need(offset >= previous && offset <= glyf.Length && (offset == previous || offset - previous >= 10 || i == 0), "Invalid loca/glyf range."); previous = offset;
        }
        cmap = new UnicodeCmap(sfnt.Table("cmap", 4), glyphs, cancellation);
        PostScriptName = ReadName(sfnt.Table("name", 6));
    }

    public static PdfFont LoadTrueType(ReadOnlySpan<byte> source, int maxBytes = DefaultMaxBytes,
        CancellationToken cancellationToken = default)
    {
        if (maxBytes < 1) throw new ArgumentOutOfRangeException(nameof(maxBytes));
        cancellationToken.ThrowIfCancellationRequested();
        if (source.Length > maxBytes) throw new FactsPdfException("FPDF1503", "Font exceeds its byte limit.");
        return new PdfFont(source.ToArray(), cancellationToken);
    }
    public static PdfFont LoadTrueType(Stream source, int maxBytes = DefaultMaxBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.CanRead) throw new ArgumentException("Font stream must be readable.", nameof(source));
        if (maxBytes < 1) throw new ArgumentOutOfRangeException(nameof(maxBytes));
        cancellationToken.ThrowIfCancellationRequested();
        using var buffer = new MemoryStream(Math.Min(maxBytes, 8192));
        var chunk = new byte[8192];
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var count = source.Read(chunk, 0, (int)Math.Min(chunk.Length, (long)maxBytes - buffer.Length + 1));
            if (count == 0) break;
            if (buffer.Length + count > maxBytes) throw new FactsPdfException("FPDF1503", "Font exceeds its byte limit.");
            buffer.Write(chunk, 0, count);
        }
        return new PdfFont(buffer.ToArray(), cancellationToken);
    }
    internal ushort GlyphFor(int scalar) => cmap.GlyphFor(scalar);
    internal double Width1000(ushort glyph) => glyph < advances.Length ? advances[glyph] * scale : throw new FactsPdfException("FPDF1501", "Invalid glyph index.");

    private static string ReadName(ReadOnlySpan<byte> name)
    {
        var count = U16(name, 2); var storage = U16(name, 4);
        Need(6L + count * 12L <= name.Length && storage >= 6 + count * 12 && storage <= name.Length, "Invalid font name records.");
        string? chosen = null; var best = -1;
        for (var i = 0; i < count; i++)
        {
            var at = 6 + i * 12; var platform = U16(name, at); var encoding = U16(name, at + 2);
            var length = U16(name, at + 8); var offset = storage + U16(name, at + 10);
            Need((long)offset + length <= name.Length, "Font name lies outside string storage.");
            if (U16(name, at + 6) != 6) continue;
            var unicode = platform == 0 || platform == 3 && encoding is 1 or 10;
            if (!unicode && !(platform == 1 && encoding == 0)) continue;
            Need(!unicode || length % 2 == 0, "Invalid UTF-16 font name length.");
            var text = unicode ? Encoding.BigEndianUnicode.GetString(name.Slice(offset, length)) : Encoding.ASCII.GetString(name.Slice(offset, length));
            if (text.Length is < 1 or > 63 || !text.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or '+')) continue;
            var score = (unicode ? 2 : 0) + (platform == 3 ? 1 : 0);
            if (score > best) { chosen = text; best = score; }
        }
        if (chosen is null) throw new FactsPdfException("FPDF1502", "A supported ASCII PostScript name is required.");
        return chosen;
    }
}
