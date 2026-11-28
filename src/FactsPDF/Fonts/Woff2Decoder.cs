using System.Buffers.Binary;
using System.IO.Compression;

namespace FactsPDF;

/// <summary>
/// WOFF 2.0 (W3C Recommendation) to sfnt: one Brotli stream holds every table, glyf and loca may be
/// stored in the transformed form (separate contour, point, flag, coordinate, composite, bounding box
/// and instruction streams) and hmtx may omit left side bearings that equal the glyph's xMin. The
/// reconstruction follows section 5 of the specification; the output is an ordinary TrueType font.
/// </summary>
internal static class Woff2Decoder
{
    // Section 4.2: the known table tags, by the 6-bit index a directory entry may use instead of a tag.
    private static readonly string[] KnownTags =
    [
        "cmap", "head", "hhea", "hmtx", "maxp", "name", "OS/2", "post", "cvt ", "fpgm", "glyf", "loca", "prep",
        "CFF ", "VORG", "EBDT", "EBLC", "gasp", "hdmx", "kern", "LTSH", "PCLT", "VDMX", "vhea", "vmtx", "BASE",
        "GDEF", "GPOS", "GSUB", "EBSC", "JSTF", "MATH", "CBDT", "CBLC", "COLR", "CPAL", "SVG ", "sbix", "acnt",
        "avar", "bdat", "bloc", "bsln", "cvar", "fdsc", "feat", "fmtx", "fvar", "gvar", "hsty", "just", "lcar",
        "mort", "morx", "opbd", "prop", "trak", "Zapf", "Silf", "Glat", "Gloc", "Feat", "Sill",
    ];

    private static readonly uint Glyf = FontContainer.Tag("glyf"), Loca = FontContainer.Tag("loca"),
        Hmtx = FontContainer.Tag("hmtx"), Head = FontContainer.Tag("head"), Hhea = FontContainer.Tag("hhea"),
        Maxp = FontContainer.Tag("maxp");

    private sealed record Entry(uint Tag, uint OriginalLength, uint StreamLength, bool Transformed)
    {
        internal int Offset { get; set; }
    }

    internal static byte[] Decode(byte[] data, int maxBytes, CancellationToken cancellation)
    {
        const int header = 48;
        Need(data.Length >= header, "Truncated WOFF2 header.");
        var flavor = U32(data, 4);
        if (flavor == FontContainer.CollectionTag)
            throw new FactsPdfException("FPDF1502", "WOFF2 font collections are not supported; supply a single-face WOFF2 file.");
        Need(U32(data, 8) == data.Length, "WOFF2 length does not match the file.");
        var count = U16(data, 12);
        Need(count is > 0 and <= 256 && U16(data, 14) == 0, "Invalid WOFF2 table count or reserved field.");
        if (U32(data, 16) > (uint)maxBytes) throw new FactsPdfException("FPDF1503", "Decoded font exceeds its byte limit.");
        var compressedLength = U32(data, 20);
        var at = header;
        var entries = new List<Entry>(count);
        long streamTotal = 0;
        for (var i = 0; i < count; i++)
        {
            Need(at < data.Length, "Truncated WOFF2 table directory.");
            var flags = data[at++];
            uint tag;
            if ((flags & 63) == 63) { tag = U32(data, at); at += 4; }
            else tag = FontContainer.Tag(KnownTags[flags & 63]);
            var version = flags >> 6;
            var original = Base128(data, ref at);
            bool transformed;
            if (tag == Glyf || tag == Loca)
            {
                Need(version is 0 or 3, "Unsupported WOFF2 glyf/loca transform.");
                transformed = version == 0;
            }
            else
            {
                Need(version == 0 || tag == Hmtx && version == 1, "Unsupported WOFF2 table transform.");
                transformed = version != 0;
            }
            var stored = transformed ? Base128(data, ref at) : original;
            if (tag == Loca && transformed) Need(stored == 0, "A transformed WOFF2 loca table must be empty.");
            streamTotal += stored;
            Need(streamTotal <= maxBytes, "WOFF2 tables exceed the font byte limit.");
            entries.Add(new(tag, original, stored, transformed));
        }
        Need((long)at + compressedLength <= data.Length, "WOFF2 compressed data lies outside the file.");
        var stream = new byte[streamTotal];
        cancellation.ThrowIfCancellationRequested();
        Need(BrotliDecoder.TryDecompress(data.AsSpan(at, (int)compressedLength), stream, out var written) && written == stream.Length,
            "WOFF2 table data is not valid Brotli data of the declared length.");
        var offset = 0;
        foreach (var entry in entries) { entry.Offset = offset; offset += (int)entry.StreamLength; }

        Entry? Find(uint tag) => entries.Find(e => e.Tag == tag);
        var glyf = Find(Glyf); var loca = Find(Loca);
        Need((glyf is null) == (loca is null) && (glyf is null || glyf.Transformed == loca!.Transformed),
            "WOFF2 glyf and loca must be present and transformed together.");
        var tables = new List<(uint Tag, byte[] Bytes)>(count);
        short[]? xMins = null; var indexFormat = -1;
        if (glyf is { Transformed: true })
        {
            var (glyphs, locations, minima, format) = Glyphs(stream.AsSpan(glyf.Offset, (int)glyf.StreamLength), cancellation);
            tables.Add((Glyf, glyphs)); tables.Add((Loca, locations));
            xMins = minima; indexFormat = format;
        }
        foreach (var entry in entries)
        {
            cancellation.ThrowIfCancellationRequested();
            if (entry.Tag == Glyf && entry.Transformed || entry.Tag == Loca && entry.Transformed) continue;
            var bytes = stream.AsSpan(entry.Offset, (int)entry.StreamLength);
            if (entry.Tag == Hmtx && entry.Transformed)
            {
                var hhea = Find(Hhea); var maxp = Find(Maxp);
                Need(hhea is { Transformed: false } && maxp is { Transformed: false }, "A transformed WOFF2 hmtx needs hhea and maxp.");
                var metrics = U16(stream.AsSpan(hhea.Offset, (int)hhea.StreamLength), 34);
                var glyphCount = U16(stream.AsSpan(maxp.Offset, (int)maxp.StreamLength), 4);
                var table = Metrics(bytes, metrics, glyphCount, xMins);
                Need(table.Length == entry.OriginalLength, "Reconstructed WOFF2 hmtx length differs from the declared length.");
                tables.Add((Hmtx, table));
                continue;
            }
            var copy = bytes.ToArray();
            // The transformed glyf header records the loca format the reconstruction wrote.
            if (entry.Tag == Head && indexFormat >= 0)
            {
                Need(copy.Length >= 54, "Truncated WOFF2 head table.");
                BinaryPrimitives.WriteInt16BigEndian(copy.AsSpan(50), (short)indexFormat);
            }
            tables.Add((entry.Tag, copy));
        }
        return FontContainer.Assemble(flavor, tables, maxBytes);
    }

    // Section 5.1: the transformed glyf table back to glyf and loca, and every glyph's xMin for hmtx.
    private static (byte[] Glyf, byte[] Loca, short[] XMins, int IndexFormat) Glyphs(ReadOnlySpan<byte> t, CancellationToken cancellation)
    {
        Need(t.Length >= 36, "Truncated WOFF2 transformed glyf header.");
        var options = U16(t, 2); var glyphCount = U16(t, 4); var indexFormat = U16(t, 6);
        Need(indexFormat is 0 or 1, "Invalid WOFF2 loca format.");
        var sizes = new uint[7];
        long next = 36;
        for (var i = 0; i < 7; i++) { sizes[i] = U32(t, 8 + 4 * i); next += sizes[i]; }
        var overlapLength = (options & 1) != 0 ? (glyphCount + 7) >> 3 : 0;
        Need(next + overlapLength <= t.Length, "WOFF2 glyf streams exceed the table.");
        var starts = new int[7];
        var cursor = 36;
        for (var i = 0; i < 7; i++) { starts[i] = cursor; cursor += (int)sizes[i]; }
        var contours = new Reader(t, starts[0], (int)sizes[0]);
        var points = new Reader(t, starts[1], (int)sizes[1]);
        var flags = new Reader(t, starts[2], (int)sizes[2]);
        var coordinates = new Reader(t, starts[3], (int)sizes[3]);
        var composites = new Reader(t, starts[4], (int)sizes[4]);
        var boxBitmapLength = ((glyphCount + 31) >> 5) << 2;
        Need(sizes[5] >= boxBitmapLength, "Truncated WOFF2 bounding box bitmap.");
        var boxBitmap = t.Slice(starts[5], boxBitmapLength);
        var boxes = new Reader(t, starts[5] + boxBitmapLength, (int)sizes[5] - boxBitmapLength);
        var instructions = new Reader(t, starts[6], (int)sizes[6]);
        var overlap = overlapLength > 0 ? t.Slice(cursor, overlapLength) : default;

        using var glyf = new MemoryStream();
        var loca = new byte[(glyphCount + 1) * (indexFormat == 0 ? 2 : 4)];
        var xMins = new short[glyphCount];
        var endPoints = new List<int>();
        var xs = new List<int>(); var ys = new List<int>(); var onCurve = new List<bool>();
        for (var glyph = 0; glyph < glyphCount; glyph++)
        {
            if ((glyph & 255) == 0) cancellation.ThrowIfCancellationRequested();
            WriteLocation(loca, glyph, glyf.Length, indexFormat);
            var hasBox = (boxBitmap[glyph >> 3] & (0x80 >> (glyph & 7))) != 0;
            var contourCount = contours.S16();
            if (contourCount == 0)
            {
                Need(!hasBox, "An empty WOFF2 glyph has an explicit bounding box.");
                continue;
            }
            if (contourCount < 0)
            {
                Need(contourCount == -1 && hasBox, "Invalid WOFF2 composite glyph.");
                var (xMin, yMin, xMax, yMax) = (boxes.S16(), boxes.S16(), boxes.S16(), boxes.S16());
                var componentStart = composites.Position;
                var instructed = false;
                ushort componentFlags;
                do
                {
                    componentFlags = composites.U16();
                    composites.Skip(2 + ((componentFlags & 0x0001) != 0 ? 4 : 2)
                        + ((componentFlags & 0x0008) != 0 ? 2 : (componentFlags & 0x0040) != 0 ? 4 : (componentFlags & 0x0080) != 0 ? 8 : 0));
                    instructed |= (componentFlags & 0x0100) != 0;
                }
                while ((componentFlags & 0x0020) != 0);
                Write16(glyf, -1); Write16(glyf, xMin); Write16(glyf, yMin); Write16(glyf, xMax); Write16(glyf, yMax);
                glyf.Write(t.Slice(componentStart, composites.Position - componentStart));
                if (instructed)
                {
                    var length = coordinates.UShort255();
                    Write16(glyf, length);
                    glyf.Write(instructions.Bytes(length));
                }
                xMins[glyph] = xMin;
                Pad(glyf);
                continue;
            }
            endPoints.Clear(); xs.Clear(); ys.Clear(); onCurve.Clear();
            var total = 0;
            for (var c = 0; c < contourCount; c++)
            {
                total += points.UShort255();
                Need(total <= 65535, "A WOFF2 glyph has too many points.");
                endPoints.Add(total - 1);
            }
            Need(total > 0 && endPoints[0] >= 0, "A WOFF2 glyph contour has no points.");
            int x = 0, y = 0;
            for (var p = 0; p < total; p++)
            {
                var flag = flags.Byte();
                onCurve.Add((flag & 0x80) == 0);
                var (dx, dy) = Triplet(flag & 0x7f, ref coordinates);
                x += dx; y += dy;
                xs.Add(x); ys.Add(y);
            }
            var instructionLength = coordinates.UShort255();
            short boxXMin, boxYMin, boxXMax, boxYMax;
            if (hasBox) (boxXMin, boxYMin, boxXMax, boxYMax) = (boxes.S16(), boxes.S16(), boxes.S16(), boxes.S16());
            else
            {
                boxXMin = Clamp(xs.Min()); boxYMin = Clamp(ys.Min());
                boxXMax = Clamp(xs.Max()); boxYMax = Clamp(ys.Max());
            }
            Write16(glyf, contourCount); Write16(glyf, boxXMin); Write16(glyf, boxYMin); Write16(glyf, boxXMax); Write16(glyf, boxYMax);
            foreach (var end in endPoints) Write16(glyf, end);
            Write16(glyf, instructionLength);
            glyf.Write(instructions.Bytes(instructionLength));
            var overlapping = !overlap.IsEmpty && (overlap[glyph >> 3] & (0x80 >> (glyph & 7))) != 0;
            WriteSimpleOutline(glyf, xs, ys, onCurve, overlapping);
            xMins[glyph] = boxXMin;
            Pad(glyf);
        }
        WriteLocation(loca, glyphCount, glyf.Length, indexFormat);
        Need(indexFormat == 1 || glyf.Length <= 0x1FFFE, "Reconstructed WOFF2 glyf is too long for short loca offsets.");
        return (glyf.ToArray(), loca, xMins, indexFormat);
    }

    // The glyf simple-glyph flags and coordinates, each flag written out (no repeat runs).
    private static void WriteSimpleOutline(MemoryStream glyf, List<int> xs, List<int> ys, List<bool> onCurve, bool overlapping)
    {
        var count = xs.Count;
        var flagBytes = new byte[count];
        using var xBytes = new MemoryStream(); using var yBytes = new MemoryStream();
        int lastX = 0, lastY = 0;
        for (var i = 0; i < count; i++)
        {
            var flag = onCurve[i] ? 0x01 : 0x00;
            if (i == 0 && overlapping) flag |= 0x40;
            var dx = xs[i] - lastX; var dy = ys[i] - lastY;
            lastX = xs[i]; lastY = ys[i];
            if (dx == 0) flag |= 0x10;
            else if (dx is >= -255 and <= 255) { flag |= 0x02 | (dx > 0 ? 0x10 : 0); xBytes.WriteByte((byte)Math.Abs(dx)); }
            else { Need(dx is >= short.MinValue and <= short.MaxValue, "WOFF2 x coordinate delta out of range."); Write16(xBytes, dx); }
            if (dy == 0) flag |= 0x20;
            else if (dy is >= -255 and <= 255) { flag |= 0x04 | (dy > 0 ? 0x20 : 0); yBytes.WriteByte((byte)Math.Abs(dy)); }
            else { Need(dy is >= short.MinValue and <= short.MaxValue, "WOFF2 y coordinate delta out of range."); Write16(yBytes, dy); }
            flagBytes[i] = (byte)flag;
        }
        glyf.Write(flagBytes);
        xBytes.Position = 0; xBytes.CopyTo(glyf);
        yBytes.Position = 0; yBytes.CopyTo(glyf);
    }

    // Section 5.2: one point's coordinate deltas from its flag's triplet encoding.
    private static (int Dx, int Dy) Triplet(int flag, ref Reader data)
    {
        static int Sign(int flag, int value) => (flag & 1) != 0 ? value : -value;
        if (flag < 10) return (0, Sign(flag, ((flag & 14) << 7) + data.Byte()));
        if (flag < 20) return (Sign(flag, (((flag - 10) & 14) << 7) + data.Byte()), 0);
        if (flag < 84)
        {
            var b0 = flag - 20; var b1 = data.Byte();
            return (Sign(flag, 1 + (b0 & 0x30) + (b1 >> 4)), Sign(flag >> 1, 1 + ((b0 & 0x0c) << 2) + (b1 & 0x0f)));
        }
        if (flag < 120)
        {
            var b0 = flag - 84; var b1 = data.Byte(); var b2 = data.Byte();
            return (Sign(flag, 1 + ((b0 / 12) << 8) + b1), Sign(flag >> 1, 1 + (((b0 % 12) >> 2) << 8) + b2));
        }
        if (flag < 124)
        {
            var b1 = data.Byte(); var b2 = data.Byte(); var b3 = data.Byte();
            return (Sign(flag, (b1 << 4) + (b2 >> 4)), Sign(flag >> 1, ((b2 & 0x0f) << 8) + b3));
        }
        var c1 = data.Byte(); var c2 = data.Byte(); var c3 = data.Byte(); var c4 = data.Byte();
        return (Sign(flag, (c1 << 8) + c2), Sign(flag >> 1, (c3 << 8) + c4));
    }

    // Section 5.4: the transformed hmtx table, with omitted left side bearings taken from glyph xMin.
    private static byte[] Metrics(ReadOnlySpan<byte> t, int metricCount, int glyphCount, short[]? xMins)
    {
        Need(t.Length >= 1 && metricCount > 0 && metricCount <= glyphCount, "Invalid WOFF2 hmtx transform.");
        var flags = t[0];
        Need((flags & 0xFC) == 0 && (flags & 3) != 0, "Invalid WOFF2 hmtx transform flags.");
        Need(xMins is not null && xMins.Length == glyphCount, "A WOFF2 hmtx transform needs a transformed glyf table.");
        var data = new Reader(t, 1, t.Length - 1);
        var advances = new ushort[metricCount];
        for (var i = 0; i < metricCount; i++) advances[i] = data.U16();
        var bearings = new short[glyphCount];
        for (var i = 0; i < metricCount; i++) bearings[i] = (flags & 1) != 0 ? xMins![i] : data.S16();
        for (var i = metricCount; i < glyphCount; i++) bearings[i] = (flags & 2) != 0 ? xMins![i] : data.S16();
        Need(data.Remaining == 0, "Trailing data in a WOFF2 hmtx transform.");
        var table = new byte[metricCount * 4 + (glyphCount - metricCount) * 2];
        for (var i = 0; i < metricCount; i++)
        {
            BinaryPrimitives.WriteUInt16BigEndian(table.AsSpan(i * 4), advances[i]);
            BinaryPrimitives.WriteInt16BigEndian(table.AsSpan(i * 4 + 2), bearings[i]);
        }
        for (var i = metricCount; i < glyphCount; i++)
            BinaryPrimitives.WriteInt16BigEndian(table.AsSpan(metricCount * 4 + (i - metricCount) * 2), bearings[i]);
        return table;
    }

    private static void WriteLocation(byte[] loca, int glyph, long offset, int indexFormat)
    {
        if (indexFormat == 0)
        {
            Need(offset <= 0x1FFFE, "Reconstructed WOFF2 glyf is too long for short loca offsets.");
            BinaryPrimitives.WriteUInt16BigEndian(loca.AsSpan(glyph * 2), (ushort)(offset / 2));
        }
        else BinaryPrimitives.WriteUInt32BigEndian(loca.AsSpan(glyph * 4), (uint)offset);
    }

    private static void Pad(MemoryStream glyf) { while ((glyf.Length & 3) != 0) glyf.WriteByte(0); }

    private static short Clamp(int value) => (short)Math.Clamp(value, short.MinValue, short.MaxValue);

    private static void Write16(MemoryStream stream, int value)
    {
        stream.WriteByte((byte)(value >> 8));
        stream.WriteByte((byte)value);
    }

    // UIntBase128 (section 4.1): at most five bytes, no leading zero byte, no overflow.
    private static uint Base128(byte[] data, ref int at)
    {
        uint value = 0;
        for (var i = 0; i < 5; i++)
        {
            Need(at < data.Length, "Truncated WOFF2 UIntBase128.");
            var b = data[at++];
            Need(!(i == 0 && b == 0x80) && (value & 0xFE000000) == 0, "Invalid WOFF2 UIntBase128.");
            value = (value << 7) | (uint)(b & 0x7f);
            if ((b & 0x80) == 0) return value;
        }
        Need(false, "WOFF2 UIntBase128 is longer than five bytes.");
        return 0;
    }

    // A bounds-checked cursor over one stream of the transformed glyf table.
    private ref struct Reader
    {
        private readonly ReadOnlySpan<byte> data;
        private readonly int start;
        private int at;
        internal Reader(ReadOnlySpan<byte> table, int start, int length)
        {
            SfntDirectory.Need(start >= 0 && length >= 0 && (long)start + length <= table.Length, "WOFF2 stream lies outside its table.");
            data = table.Slice(start, length);
            this.start = start;
        }
        internal readonly int Position => start + at;
        internal readonly int Remaining => data.Length - at;
        internal byte Byte() { Need(at < data.Length, "Truncated WOFF2 glyf stream."); return data[at++]; }
        internal ushort U16() { Need(at + 2 <= data.Length, "Truncated WOFF2 glyf stream."); var v = BinaryPrimitives.ReadUInt16BigEndian(data[at..]); at += 2; return v; }
        internal short S16() => unchecked((short)U16());
        internal void Skip(int count) { Need(count >= 0 && at + count <= data.Length, "Truncated WOFF2 glyf stream."); at += count; }
        internal ReadOnlySpan<byte> Bytes(int count) { Need(at + count <= data.Length, "Truncated WOFF2 instruction stream."); var v = data.Slice(at, count); at += count; return v; }
        // 255UInt16 (section 6.1.1).
        internal int UShort255()
        {
            var code = Byte();
            return code switch { 253 => U16(), 255 => Byte() + 253, 254 => Byte() + 506, _ => code };
        }
    }

    private static ushort U16(ReadOnlySpan<byte> bytes, int at) => SfntDirectory.U16(bytes, at);
    private static uint U32(ReadOnlySpan<byte> bytes, int at) => SfntDirectory.U32(bytes, at);
    private static void Need(bool condition, string message) => SfntDirectory.Need(condition, message);
}
