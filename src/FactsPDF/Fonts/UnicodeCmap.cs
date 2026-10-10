using static FactsPDF.SfntDirectory;

namespace FactsPDF;

/// <summary>A single consistently selected Unicode cmap; format 12 is preferred over 4.</summary>
internal sealed class UnicodeCmap
{
    private readonly byte[] bytes;
    private readonly int format;
    private readonly int count;

    internal UnicodeCmap(ReadOnlySpan<byte> table, int glyphCount, CancellationToken cancellation)
    {
        Need(U16(table, 0) == 0, "Invalid cmap version.");
        var records = U16(table, 2);
        Need(4L + records * 8L <= table.Length, "Truncated cmap records.");
        var bestOffset = -1; var bestScore = -1;
        for (var i = 0; i < records; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            var at = 4 + i * 8; var platform = U16(table, at); var encoding = U16(table, at + 2);
            var offset = U32(table, at + 4);
            Need(offset >= 4 + 8 * records && (long)offset + 2 <= table.Length, "Invalid cmap subtable offset.");
            if (!(platform == 0 || platform == 3 && encoding is 1 or 10)) continue;
            var candidate = U16(table, (int)offset);
            if (candidate is not (4 or 12)) continue;
            var score = (candidate == 12 ? 10 : 0) + (platform == 3 ? 1 : 0);
            if (score > bestScore) { bestScore = score; bestOffset = (int)offset; }
        }
        if (bestOffset < 0) throw new FactsPdfException("FPDF1502", "A Unicode cmap format 4 or 12 is required.");
        var sub = table[bestOffset..]; format = U16(sub, 0);
        var length = format == 12 ? U32(sub, 4) : U16(sub, 2);
        Need(length <= sub.Length && length >= (format == 12 ? 16 : 24), "Invalid cmap length.");
        bytes = sub[..(int)length].ToArray();
        if (format == 12)
        {
            Need(U16(bytes, 2) == 0, "Invalid cmap 12 reserved field.");
            var groups = U32(bytes, 12);
            Need(groups > 0 && 16L + groups * 12L <= bytes.Length, "Invalid cmap 12 group count.");
            count = (int)groups;
            long previous = -1;
            for (var i = 0; i < count; i++)
            {
                if ((i & 1023) == 0) cancellation.ThrowIfCancellationRequested();
                var at = 16 + i * 12; var start = U32(bytes, at); var end = U32(bytes, at + 4); var firstGlyph = U32(bytes, at + 8);
                Need(start > previous && start <= end && end <= 0x10ffff && !(start <= 0xdfff && end >= 0xd800), "Invalid or overlapping cmap 12 scalar range.");
                Need((long)firstGlyph + end - start < glyphCount, "cmap 12 references an absent glyph.");
                previous = end;
            }
        }
        else
        {
            var doubled = U16(bytes, 6); count = doubled / 2;
            Need(doubled > 0 && doubled % 2 == 0 && 16L + count * 8L <= bytes.Length, "Invalid cmap 4 segment count.");
            var previous = -1;
            for (var i = 0; i < count; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                var start = U16(bytes, 16 + count * 2 + i * 2); var end = U16(bytes, 14 + i * 2);
                Need(start > previous && start <= end, "Overlapping or unordered cmap 4 segments."); previous = end;
                var at = 16 + count * 6 + i * 2; var range = U16(bytes, at);
                if (range != 0) Need(range % 2 == 0 && at + range >= 16 + count * 8 && (long)at + range + 2L * (end - start) + 2 <= bytes.Length, "Invalid cmap 4 glyph array offset.");
                for (var scalar = (int)start; scalar <= end; scalar++)
                {
                    if ((scalar & 4095) == 0) cancellation.ThrowIfCancellationRequested();
                    Need(GlyphInSegment(i, scalar) < glyphCount, "cmap 4 references an absent glyph.");
                }
            }
            Need(previous == 65535 && U16(bytes, 14 + count * 2) == 0, "Missing cmap 4 sentinel or invalid reserved field.");
        }
    }

    internal ushort GlyphFor(int scalar)
    {
        if (scalar < 0 || scalar > 0x10ffff || scalar is >= 0xd800 and <= 0xdfff || format == 4 && scalar > 65535) return 0;
        var lo = 0; var hi = count - 1;
        while (lo <= hi)
        {
            var mid = lo + (hi - lo) / 2;
            var end = format == 12 ? U32(bytes, 20 + mid * 12) : U16(bytes, 14 + mid * 2);
            if (scalar > end) lo = mid + 1;
            else
            {
                var start = format == 12 ? U32(bytes, 16 + mid * 12) : U16(bytes, 16 + count * 2 + mid * 2);
                if (scalar < start) hi = mid - 1;
                else return format == 12 ? (ushort)(U32(bytes, 24 + mid * 12) + scalar - start) : GlyphInSegment(mid, scalar);
            }
        }
        return 0;
    }
    private ushort GlyphInSegment(int i, int scalar)
    {
        var delta = S16(bytes, 16 + count * 4 + i * 2);
        var at = 16 + count * 6 + i * 2; var range = U16(bytes, at);
        if (range == 0) return unchecked((ushort)(scalar + delta));
        var glyph = U16(bytes, at + range + 2 * (scalar - U16(bytes, 16 + count * 2 + i * 2)));
        return glyph == 0 ? (ushort)0 : unchecked((ushort)(glyph + delta));
    }
}
