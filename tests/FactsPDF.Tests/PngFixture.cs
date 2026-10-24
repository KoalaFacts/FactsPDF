using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace FactsPDF.Tests;

// Authored scanline bytes and expected pixels are supplied independently by each test.
internal static class PngFixture
{
    internal static readonly byte[] Signature = [137, 80, 78, 71, 13, 10, 26, 10];
    internal static byte[] Header(int width = 2, int height = 1, byte depth = 8, byte type = 6, byte interlace = 0)
    {
        var bytes = new byte[13]; BinaryPrimitives.WriteInt32BigEndian(bytes, width);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(4), height);
        bytes[8] = depth; bytes[9] = type; bytes[12] = interlace; return bytes;
    }
    internal static byte[] Chunk(string type, byte[] data)
    {
        var bytes = new byte[data.Length + 12]; BinaryPrimitives.WriteInt32BigEndian(bytes, data.Length);
        Encoding.ASCII.GetBytes(type).CopyTo(bytes, 4); data.CopyTo(bytes, 8);
        uint crc = uint.MaxValue;
        foreach (var b in bytes.AsSpan(4, data.Length + 4))
        { crc ^= b; for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320u : 0); }
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(bytes.Length - 4), ~crc); return bytes;
    }
    internal static byte[] Zlib(byte[] rows)
    {
        using var result = new MemoryStream();
        using (var z = new ZLibStream(result, CompressionLevel.Optimal, true)) z.Write(rows);
        return result.ToArray();
    }
    internal static byte[] File(params (string Type, byte[] Data)[] chunks)
        => [.. Signature, .. chunks.SelectMany(c => Chunk(c.Type, c.Data))];
    internal static byte[] Image(byte[] rows, int width = 2, int height = 1, byte depth = 8, byte type = 6,
        byte[]? palette = null, byte[]? transparency = null)
    {
        var chunks = new List<(string, byte[])> { ("IHDR", Header(width, height, depth, type)) };
        if (palette is not null) chunks.Add(("PLTE", palette));
        if (transparency is not null) chunks.Add(("tRNS", transparency));
        chunks.Add(("IDAT", Zlib(rows))); chunks.Add(("IEND", [])); return File([.. chunks]);
    }
    internal static byte[] Pdf(PdfImage image, PdfOptions? options = null, string html = "<img src='logo'>")
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, (options ?? new()) with { Images = new Dictionary<string, PdfImage> { ["logo"] = image } });
        return output.ToArray();
    }
    internal static List<(int Id, string Dictionary, byte[] Pixels)> ImageObjects(byte[] pdf)
    {
        var text = Encoding.Latin1.GetString(pdf); var result = new List<(int, string, byte[])>();
        foreach (Match m in Regex.Matches(text, @"(?m)^(\d+) 0 obj\n(<< /Type /XObject /Subtype /Image [^\n]+)\nstream\n"))
        {
            var length = int.Parse(Regex.Match(m.Groups[2].Value, @"/Length (\d+)").Groups[1].Value,
                System.Globalization.CultureInfo.InvariantCulture);
            var data = pdf.AsSpan(m.Index + m.Length, length).ToArray();
            if (m.Groups[2].Value.Contains("/FlateDecode", StringComparison.Ordinal))
            { using var input = new MemoryStream(data); using var z = new ZLibStream(input, CompressionMode.Decompress); using var raw = new MemoryStream(); z.CopyTo(raw); data = raw.ToArray(); }
            result.Add((int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), m.Groups[2].Value, data));
        }
        return result;
    }
}
