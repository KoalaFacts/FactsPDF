using System.Buffers.Binary;
using System.Text;
using static FactsPDF.SfntDirectory;

namespace FactsPDF;

/// <summary>Assembles the small sfnt used only for PDF embedding; does not install fonts.</summary>
internal static class SubsetSfnt
{
    internal static void Put16(byte[] data, int at, int value) => BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(at, 2), unchecked((ushort)value));
    internal static void Put32(byte[] data, int at, uint value) => BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(at, 4), value);
    internal static uint Checksum(ReadOnlySpan<byte> data)
    {
        uint result = 0;
        for (var i = 0; i < data.Length; i += 4)
        {
            uint word = 0;
            for (var j = 0; j < 4; j++) word = (word << 8) | (i + j < data.Length ? data[i + j] : 0u);
            result = unchecked(result + word);
        }
        return result;
    }
    internal static byte[] Build(SortedDictionary<string, byte[]> tables, CancellationToken token)
    {
        long length = 12 + 16 * tables.Count;
        foreach (var value in tables.Values) length += ((long)value.Length + 3) & ~3L;
        if (length > 67_108_864) throw new FactsPdfException("FPDF1503", "Generated subset exceeds the 64 MiB sfnt limit.");
        var result = new byte[(int)length]; Put32(result, 0, 0x00010000); Put16(result, 4, tables.Count);
        var power = 1; var log = 0; while (power * 2 <= tables.Count) { power *= 2; log++; }
        Put16(result, 6, power * 16); Put16(result, 8, log); Put16(result, 10, tables.Count * 16 - power * 16);
        var record = 12; var offset = 12 + tables.Count * 16; var headOffset = -1;
        foreach (var (tag, bytes) in tables)
        {
            token.ThrowIfCancellationRequested(); Encoding.ASCII.GetBytes(tag).CopyTo(result, record);
            Put32(result, record + 4, Checksum(bytes)); Put32(result, record + 8, (uint)offset); Put32(result, record + 12, (uint)bytes.Length);
            bytes.CopyTo(result, offset); if (tag == "head") headOffset = offset;
            record += 16; offset += (bytes.Length + 3) & ~3;
        }
        if (headOffset < 0) throw new InvalidOperationException("Subset head table is required.");
        Put32(result, headOffset + 8, unchecked(0xb1b0afbau - Checksum(result)));
        return result;
    }

    internal static byte[] Cmap(SortedDictionary<int, ushort> characters)
    {
        // A zero mapping makes the glyph-zero-only subset readable without inventing visible text.
        var all = characters.Count == 0 ? new[] { new KeyValuePair<int, ushort>(0, 0) } : characters.ToArray();
        var full = new byte[16 + 12 * all.Length]; Put16(full, 0, 12); Put32(full, 4, (uint)full.Length); Put32(full, 12, (uint)all.Length);
        for (var i = 0; i < all.Length; i++)
        { Put32(full, 16 + i * 12, (uint)all[i].Key); Put32(full, 20 + i * 12, (uint)all[i].Key); Put32(full, 24 + i * 12, all[i].Value); }
        var bmp = all.Where(pair => pair.Key < 65535).ToArray(); byte[]? shortMap = null;
        // Format 4 has a 16-bit length. Format 12 remains authoritative when too many isolated BMP entries exist.
        if (bmp.Length <= 8188)
        {
            var n = bmp.Length + 1; shortMap = new byte[16 + 8 * n]; Put16(shortMap, 0, 4); Put16(shortMap, 2, shortMap.Length); Put16(shortMap, 6, n * 2);
            var power = 1; var log = 0; while (power * 2 <= n) { power *= 2; log++; }
            Put16(shortMap, 8, power * 2); Put16(shortMap, 10, log); Put16(shortMap, 12, n * 2 - power * 2);
            for (var i = 0; i < n; i++)
            {
                var scalar = i < bmp.Length ? bmp[i].Key : 65535; var glyph = i < bmp.Length ? bmp[i].Value : 0;
                Put16(shortMap, 14 + i * 2, scalar); Put16(shortMap, 16 + n * 2 + i * 2, scalar); Put16(shortMap, 16 + n * 4 + i * 2, glyph - scalar);
            }
        }
        var records = shortMap is null ? 1 : 2; var start = 4 + 8 * records;
        var result = new byte[start + (shortMap?.Length ?? 0) + full.Length]; Put16(result, 2, records);
        if (shortMap is not null)
        { Put16(result, 4, 3); Put16(result, 6, 1); Put32(result, 8, (uint)start); shortMap.CopyTo(result, start); }
        var rec = 4 + (records - 1) * 8; Put16(result, rec, 3); Put16(result, rec + 2, 10); Put32(result, rec + 4, (uint)(start + (shortMap?.Length ?? 0)));
        full.CopyTo(result, start + (shortMap?.Length ?? 0)); return result;
    }

    internal static byte[] Rename(ReadOnlySpan<byte> original, string name)
    {
        // Keep original records/string storage, including copyright, license text and language tags.
        var count = U16(original, 2); var storage = U16(original, 4);
        var replacements = new List<(int Record, byte[] Value)>(); var extra = 0;
        for (var i = 0; i < count; i++)
        {
            var at = 6 + i * 12; if (U16(original, at + 6) != 6) continue;
            var platform = U16(original, at); var encoding = U16(original, at + 2);
            byte[]? value = platform is 0 or 3 ? Encoding.BigEndianUnicode.GetBytes(name) : platform == 1 && encoding == 0 ? Encoding.ASCII.GetBytes(name) : null;
            if (value is null) continue;
            if ((long)original.Length + extra - storage > 65535) throw new FactsPdfException("FPDF1506", "Subset name storage exceeds 16-bit offsets.");
            replacements.Add((at, value)); extra += value.Length;
        }
        var result = new byte[checked(original.Length + extra)]; original.CopyTo(result); var offset = original.Length;
        foreach (var (record, value) in replacements)
        { Put16(result, record + 8, value.Length); Put16(result, record + 10, offset - storage); value.CopyTo(result, offset); offset += value.Length; }
        return result;
    }
}
