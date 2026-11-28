using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace FactsPDF;

/// <summary>
/// Turns the font containers Chrome loads (a plain sfnt, a TrueType/OpenType collection, WOFF and
/// WOFF2) into one standalone sfnt, so the rest of the engine (validation, measuring, subsetting and
/// embedding) only ever sees a single-face font program. Outlines are not interpreted here.
/// </summary>
internal static class FontContainer
{
    internal const uint TrueTypeTag = 0x00010000, OpenTypeCffTag = 0x4F54544F, CollectionTag = 0x74746366,
        WoffTag = 0x774F4646, Woff2Tag = 0x774F4632;

    /// <summary>The number of faces in <paramref name="data"/>: the collection's count, or 1 for every other format.</summary>
    internal static int FaceCount(ReadOnlySpan<byte> data)
    {
        Need(data.Length >= 12, "Truncated font header.");
        if (U32(data, 0) != CollectionTag) return 1;
        var count = U32(data, 8);
        Need(count is > 0 and <= MaxCollectionFaces && 12L + 4L * count <= data.Length, "Invalid font collection header.");
        return (int)count;
    }

    private const int MaxCollectionFaces = 4096;

    /// <summary>The sfnt bytes of face <paramref name="faceIndex"/>. Plain sfnt input is returned as it is.</summary>
    internal static byte[] Decode(byte[] data, int faceIndex, int maxBytes, CancellationToken cancellation)
    {
        Need(data.Length >= 12, "Truncated font header.");
        var tag = U32(data, 0);
        if (tag == CollectionTag) return CollectionFace(data, faceIndex, maxBytes);
        if (faceIndex != 0)
            throw new FactsPdfException("FPDF1502", $"Face {faceIndex} was requested from a font file that holds one face.");
        return tag switch
        {
            WoffTag => Woff(data, maxBytes, cancellation),
            Woff2Tag => Woff2Decoder.Decode(data, maxBytes, cancellation),
            _ => data,
        };
    }

    // A collection (ttcf) shares tables between faces; the requested face's tables are copied into
    // a font of their own.
    private static byte[] CollectionFace(byte[] data, int faceIndex, int maxBytes)
    {
        var count = FaceCount(data);
        if (faceIndex < 0 || faceIndex >= count)
            throw new FactsPdfException("FPDF1502", $"The font collection holds {count} face(s); face {faceIndex} does not exist.");
        var at = U32(data, 12 + 4 * faceIndex);
        Need(at <= int.MaxValue - 12 && at + 12L <= data.Length, "Font collection face lies outside the file.");
        var face = (int)at;
        var flavor = U32(data, face);
        var tables = U16(data, face + 4);
        Need(tables is > 0 and <= 256 && face + 12L + 16L * tables <= data.Length, "Invalid font collection face directory.");
        var copied = new List<(uint Tag, byte[] Bytes)>(tables);
        for (var i = 0; i < tables; i++)
        {
            var record = face + 12 + 16 * i;
            var offset = U32(data, record + 8); var length = U32(data, record + 12);
            Need((long)offset + length <= data.Length, "Font collection table lies outside the file.");
            copied.Add((U32(data, record), data.AsSpan((int)offset, (int)length).ToArray()));
        }
        return Assemble(flavor, copied, maxBytes);
    }

    // WOFF 1.0 (W3C): a header, a table directory, and each table zlib-compressed unless that would
    // not make it smaller.
    private static byte[] Woff(byte[] data, int maxBytes, CancellationToken cancellation)
    {
        const int header = 44;
        Need(data.Length >= header, "Truncated WOFF header.");
        var flavor = U32(data, 4);
        if (flavor == CollectionTag) throw new FactsPdfException("FPDF1502", "WOFF font collections are not supported.");
        Need(U32(data, 8) == data.Length, "WOFF length does not match the file.");
        var count = U16(data, 12);
        Need(U16(data, 14) == 0, "WOFF reserved field must be zero.");
        Need(count is > 0 and <= 256 && header + 20L * count <= data.Length, "Invalid WOFF table directory.");
        if (U32(data, 16) > (uint)maxBytes) throw new FactsPdfException("FPDF1503", "Decoded font exceeds its byte limit.");
        var tables = new List<(uint Tag, byte[] Bytes)>(count);
        long total = 0;
        for (var i = 0; i < count; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            var record = header + 20 * i;
            var offset = U32(data, record + 4); var compressed = U32(data, record + 8); var original = U32(data, record + 12);
            Need((long)offset + compressed <= data.Length && compressed <= original, "WOFF table lies outside the file.");
            total += original;
            if (total > maxBytes) throw new FactsPdfException("FPDF1503", "Decoded font exceeds its byte limit.");
            var bytes = new byte[original];
            if (compressed == original) data.AsSpan((int)offset, (int)original).CopyTo(bytes);
            else Inflate(data.AsSpan((int)offset, (int)compressed), bytes, cancellation);
            tables.Add((U32(data, record), bytes));
        }
        return Assemble(flavor, tables, maxBytes);
    }

    private static void Inflate(ReadOnlySpan<byte> source, byte[] destination, CancellationToken cancellation)
    {
        using var input = new MemoryStream(source.ToArray(), writable: false);
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
        var filled = 0;
        try
        {
            while (filled < destination.Length)
            {
                cancellation.ThrowIfCancellationRequested();
                var read = zlib.Read(destination, filled, destination.Length - filled);
                if (read == 0) break;
                filled += read;
            }
            Need(filled == destination.Length && zlib.Read(new byte[1], 0, 1) == 0, "WOFF table does not inflate to its declared length.");
        }
        catch (InvalidDataException) { Need(false, "WOFF table is not valid zlib data."); }
    }

    /// <summary>Writes tables (in any order) as one sfnt: a directory sorted by tag and 4-byte aligned table data.</summary>
    internal static byte[] Assemble(uint flavor, List<(uint Tag, byte[] Bytes)> tables, int maxBytes)
    {
        tables.Sort((a, b) => a.Tag.CompareTo(b.Tag));
        for (var i = 1; i < tables.Count; i++) Need(tables[i - 1].Tag != tables[i].Tag, "Duplicate font table.");
        long size = 12 + 16L * tables.Count;
        foreach (var (_, bytes) in tables) size += (bytes.Length + 3L) & ~3L;
        if (size > maxBytes) throw new FactsPdfException("FPDF1503", "Decoded font exceeds its byte limit.");
        var font = new byte[(int)size];
        var power = 1; var log = 0;
        while (power * 2 <= tables.Count) { power *= 2; log++; }
        BinaryPrimitives.WriteUInt32BigEndian(font, flavor);
        BinaryPrimitives.WriteUInt16BigEndian(font.AsSpan(4), (ushort)tables.Count);
        BinaryPrimitives.WriteUInt16BigEndian(font.AsSpan(6), (ushort)(power * 16));
        BinaryPrimitives.WriteUInt16BigEndian(font.AsSpan(8), (ushort)log);
        BinaryPrimitives.WriteUInt16BigEndian(font.AsSpan(10), (ushort)(tables.Count * 16 - power * 16));
        var offset = 12 + 16 * tables.Count;
        for (var i = 0; i < tables.Count; i++)
        {
            var (tag, bytes) = tables[i];
            var record = font.AsSpan(12 + 16 * i);
            BinaryPrimitives.WriteUInt32BigEndian(record, tag);
            BinaryPrimitives.WriteUInt32BigEndian(record[4..], Checksum(bytes));
            BinaryPrimitives.WriteUInt32BigEndian(record[8..], (uint)offset);
            BinaryPrimitives.WriteUInt32BigEndian(record[12..], (uint)bytes.Length);
            bytes.CopyTo(font, offset);
            offset += (bytes.Length + 3) & ~3;
        }
        return font;
    }

    private static uint Checksum(byte[] bytes)
    {
        uint sum = 0;
        for (var at = 0; at < bytes.Length; at += 4)
        {
            uint word = 0;
            for (var i = 0; i < 4; i++) word = (word << 8) | (at + i < bytes.Length ? bytes[at + i] : 0u);
            sum = unchecked(sum + word);
        }
        return sum;
    }

    internal static uint Tag(string tag) => BinaryPrimitives.ReadUInt32BigEndian(Encoding.ASCII.GetBytes(tag));

    internal static ushort U16(ReadOnlySpan<byte> bytes, int at) => SfntDirectory.U16(bytes, at);
    internal static uint U32(ReadOnlySpan<byte> bytes, int at) => SfntDirectory.U32(bytes, at);
    internal static void Need(bool condition, string message) => SfntDirectory.Need(condition, message);
}
