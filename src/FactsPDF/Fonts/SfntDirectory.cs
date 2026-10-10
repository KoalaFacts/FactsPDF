using System.Buffers.Binary;
using System.Text;

namespace FactsPDF;

/// <summary>Bounds-checked sfnt table directory, not an outline-program sanitizer.</summary>
internal sealed class SfntDirectory
{
    private readonly byte[] data;
    private readonly Dictionary<string, (int Offset, int Length)> tables = new(StringComparer.Ordinal);

    internal SfntDirectory(byte[] data)
    {
        this.data = data;
        Need(data.Length >= 12, "Truncated sfnt header.");
        if (U32(data, 0) != 0x00010000)
            throw new FactsPdfException("FPDF1502", "Only static TrueType sfnt fonts with glyf outlines are supported (not TTC, CFF, WOFF or WOFF2).");
        var count = U16(data, 4);
        Need(count is > 0 and <= 256 && 12L + 16L * count <= data.Length, "Invalid sfnt directory.");
        var ranges = new List<(int Start, int End)>();
        for (var i = 0; i < count; i++)
        {
            var at = 12 + i * 16;
            var tag = Encoding.ASCII.GetString(data, at, 4);
            var offset = U32(data, at + 8); var length = U32(data, at + 12);
            Need(offset >= 12 + 16 * count && (long)offset + length <= data.Length, "Table lies outside font data.");
            Need(tables.TryAdd(tag, ((int)offset, (int)length)), "Duplicate sfnt table.");
            if (length > 0) ranges.Add(((int)offset, checked((int)((long)offset + length))));
        }
        ranges.Sort((a, b) => a.Start.CompareTo(b.Start));
        for (var i = 1; i < ranges.Count; i++) Need(ranges[i - 1].End <= ranges[i].Start, "Overlapping sfnt tables.");
        foreach (var tag in new[] { "fvar", "gvar", "CFF ", "CFF2", "COLR", "CBDT", "sbix", "SVG " })
            if (tables.ContainsKey(tag)) throw new FactsPdfException("FPDF1502", "Variable, CFF and color fonts are not supported in this increment.");
    }

    internal ReadOnlySpan<byte> Table(string tag, int minimum)
    {
        Need(tables.TryGetValue(tag, out var t) && t.Length >= minimum, $"Missing or truncated {tag} table.");
        return data.AsSpan(t.Offset, t.Length);
    }
    internal bool Contains(string tag) => tables.ContainsKey(tag);
    internal static ushort U16(ReadOnlySpan<byte> bytes, int at)
    { Need(at >= 0 && (long)at + 2 <= bytes.Length, "Truncated 16-bit font field."); return BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(at, 2)); }
    internal static short S16(ReadOnlySpan<byte> bytes, int at) => unchecked((short)U16(bytes, at));
    internal static uint U32(ReadOnlySpan<byte> bytes, int at)
    { Need(at >= 0 && (long)at + 4 <= bytes.Length, "Truncated 32-bit font field."); return BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(at, 4)); }
    internal static void Need(bool condition, string message)
    { if (!condition) throw new FactsPdfException("FPDF1501", message); }
}
