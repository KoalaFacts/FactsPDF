using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace FactsPDF;

internal sealed record PngInfo(int Width, int Height, int Components, byte[] Color, byte[]? Alpha);

// Static noninterlaced PNG, expanded into unassociated eight-bit PDF color/mask planes.
internal static class PngReader
{
    internal static ReadOnlySpan<byte> Signature => [137, 80, 78, 71, 13, 10, 26, 10];
    private static readonly uint[] CrcTable = MakeCrcTable();

    internal static PngInfo Read(byte[] bytes, PdfImageLimits limits, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (!bytes.AsSpan().StartsWith(Signature)) throw Invalid("Malformed PNG signature.");
        var width = 0; var height = 0; var depth = 0; var type = 0;
        byte[]? palette = null; byte[]? transparency = null;
        var header = false; var data = false; var dataEnded = false; var end = false; var srgb = false;
        using var compressed = new MemoryStream();
        for (var at = 8; at < bytes.Length;)
        {
            cancellation.ThrowIfCancellationRequested();
            if (bytes.Length - at < 12) throw Invalid("Truncated PNG chunk.");
            var size = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(at));
            if (size > int.MaxValue || size > bytes.Length - at - 12) throw Invalid("PNG chunk exceeds its input range.");
            var length = (int)size; var kind = bytes.AsSpan(at + 4, 4); var payload = bytes.AsSpan(at + 8, length);
            foreach (var b in kind)
                if (!(b >= 'A' && b <= 'Z' || b >= 'a' && b <= 'z')) throw Invalid("Invalid PNG chunk type.");
            if ((kind[2] & 32) != 0) throw Invalid("Invalid reserved PNG chunk type bit.");
            if (Crc(bytes.AsSpan(at + 4, length + 4), cancellation) != BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(at + length + 8)))
                throw Invalid("PNG chunk CRC mismatch.");
            var name = Encoding.ASCII.GetString(kind);
            if (!header && name != "IHDR") throw Invalid("PNG must begin with IHDR.");
            if (data && name != "IDAT") dataEnded = true;
            switch (name)
            {
                case "IHDR":
                    if (header || length != 13) throw Invalid("Invalid or duplicate PNG IHDR.");
                    header = true;
                    var w = BinaryPrimitives.ReadUInt32BigEndian(payload); var h = BinaryPrimitives.ReadUInt32BigEndian(payload[4..]);
                    if (w == 0 || h == 0 || w > int.MaxValue || h > int.MaxValue) throw Invalid("Invalid PNG dimensions.");
                    width = (int)w; height = (int)h; depth = payload[8]; type = payload[9];
                    if (!(type switch { 0 => depth is 1 or 2 or 4 or 8 or 16, 2 or 4 or 6 => depth is 8 or 16, 3 => depth is 1 or 2 or 4 or 8, _ => false }))
                        throw Invalid("Invalid PNG color type and bit depth.");
                    if (payload[10] != 0 || payload[11] != 0 || payload[12] > 1) throw Invalid("Invalid PNG compression, filter or interlace method.");
                    if (depth == 16 || payload[12] == 1) throw Unsupported("16-bit and Adam7 PNG images are not supported.");
                    if (width > limits.MaxDimension || height > limits.MaxDimension || (long)width * height > limits.MaxPixels)
                        throw Limit("PNG dimensions or pixels exceed image limits.");
                    break;
                case "PLTE":
                    if (palette is not null || transparency is not null || data || type is 0 or 4
                        || length == 0 || length % 3 != 0 || length > 768 || type == 3 && length / 3 > 1 << depth)
                        throw Invalid("Invalid PNG palette or palette order.");
                    palette = payload.ToArray(); break;
                case "tRNS":
                    if (transparency is not null || data) throw Invalid("Invalid PNG transparency order.");
                    if (type == 0)
                    {
                        if (length != 2 || BinaryPrimitives.ReadUInt16BigEndian(payload) >= 1 << depth)
                            throw Invalid("Invalid grayscale PNG transparency key.");
                    }
                    else if (type == 2)
                    {
                        if (length != 6 || BinaryPrimitives.ReadUInt16BigEndian(payload) > 255
                            || BinaryPrimitives.ReadUInt16BigEndian(payload[2..]) > 255 || BinaryPrimitives.ReadUInt16BigEndian(payload[4..]) > 255)
                            throw Invalid("Invalid RGB PNG transparency key.");
                    }
                    else if (type != 3 || palette is null || length == 0 || length > palette.Length / 3)
                        throw Invalid("Invalid PNG palette transparency or alpha color type.");
                    transparency = payload.ToArray(); break;
                case "IDAT":
                    if (dataEnded || type == 3 && palette is null) throw Invalid("Invalid PNG image data order.");
                    if (!data) CheckDecoded(width, height, type, transparency is not null, limits);
                    data = true;
                    for (var offset = 0; offset < payload.Length;)
                    {
                        cancellation.ThrowIfCancellationRequested(); var count = Math.Min(16384, payload.Length - offset);
                        compressed.Write(payload.Slice(offset, count)); offset += count;
                    }
                    break;
                case "IEND":
                    if (!data || length != 0 || at + 12 != bytes.Length) throw Invalid("Invalid PNG end or trailing file data.");
                    end = true; break;
                case "sRGB":
                    if (srgb || palette is not null || data || length != 1 || payload[0] > 3) throw Invalid("Invalid PNG sRGB metadata.");
                    srgb = true; break;
                case "acTL": case "fcTL": case "fdAT":
                    throw Unsupported("Animated PNG is not supported.");
                case "iCCP": case "cICP": case "mDCV": case "cLLI":
                    throw Unsupported("ICC and HDR PNG color transforms are not supported.");
                default:
                    if ((kind[0] & 32) == 0) throw Unsupported("Unknown critical PNG chunk.");
                    // Other ancillary metadata is ignored after framing/type/CRC validation.
                    break;
            }
            at += length + 12;
        }
        if (!end) throw Invalid("Missing PNG IEND.");
        return Decode(compressed.ToArray(), width, height, depth, type, palette, transparency, limits, cancellation);
    }

    private static void CheckDecoded(int width, int height, int type, bool transparency, PdfImageLimits limits)
    {
        var components = type is 0 or 4 ? 1 : 3;
        var alpha = type is 4 or 6 || transparency;
        var decoded = (long)width * height * (components + (alpha ? 1 : 0));
        if (decoded > limits.MaxDecodedBytes || decoded > int.MaxValue) throw Limit("Expanded PNG exceeds MaxDecodedBytes.");
    }

    private static PngInfo Decode(byte[] compressed, int width, int height, int depth, int type,
        byte[]? palette, byte[]? transparency, PdfImageLimits limits, CancellationToken cancellation)
    {
        var channels = type switch { 0 or 3 => 1, 2 => 3, 4 => 2, _ => 4 };
        var components = type is 0 or 4 ? 1 : 3; var hasAlpha = type is 4 or 6 || transparency is not null;
        var pixels = (long)width * height;
        CheckDecoded(width, height, type, transparency is not null, limits);
        // Scanlines are streamed, with two bounded rows rather than an untrusted inflated buffer.
        var rowLength = checked((int)(((long)width * channels * depth + 7) / 8));
        var bpp = Math.Max(1, channels * depth / 8);
        var previous = new byte[rowLength]; var row = new byte[rowLength];
        var color = new byte[checked((int)(pixels * components))];
        var alpha = hasAlpha ? new byte[(int)pixels] : null;
        try
        {
            using var input = new CompleteInput(compressed, cancellation);
            using var zlib = new ZLibStream(input, CompressionMode.Decompress);
            for (var y = 0; y < height; y++)
            {
                cancellation.ThrowIfCancellationRequested();
                var filter = zlib.ReadByte();
                if (filter is < 0 or > 4) throw Invalid("Invalid PNG scanline filter or missing row.");
                var offset = 0;
                while (offset < row.Length)
                {
                    cancellation.ThrowIfCancellationRequested();
                    var n = zlib.Read(row.AsSpan(offset, Math.Min(16384, row.Length - offset)));
                    if (n == 0) throw Invalid("PNG decompressed scanline data is truncated.");
                    offset += n;
                }
                for (var i = 0; i < row.Length; i++)
                {
                    if ((i & 16383) == 0) cancellation.ThrowIfCancellationRequested();
                    var left = i >= bpp ? row[i - bpp] : 0; var up = previous[i]; var upperLeft = i >= bpp ? previous[i - bpp] : 0;
                    var prediction = filter switch { 0 => 0, 1 => left, 2 => up, 3 => (left + up) / 2, _ => Paeth(left, up, upperLeft) };
                    row[i] = unchecked((byte)(row[i] + prediction));
                }
                for (var x = 0; x < width; x++)
                {
                    if ((x & 4095) == 0) cancellation.ThrowIfCancellationRequested();
                    var pixel = y * width + x; var target = pixel * components; var source = x * channels;
                    if (type is 0 or 3)
                    {
                        var sample = depth == 8 ? row[x] : row[x / (8 / depth)] >> (8 - depth - x % (8 / depth) * depth) & ((1 << depth) - 1);
                        if (type == 0)
                        {
                            color[target] = (byte)(sample * 255 / ((1 << depth) - 1));
                            if (alpha is not null) alpha[pixel] = (byte)(sample == BinaryPrimitives.ReadUInt16BigEndian(transparency!) ? 0 : 255);
                        }
                        else
                        {
                            if (sample >= palette!.Length / 3) throw Invalid("PNG palette index is outside PLTE.");
                            palette.AsSpan(sample * 3, 3).CopyTo(color.AsSpan(target, 3));
                            if (alpha is not null) alpha[pixel] = sample < transparency!.Length ? transparency[sample] : (byte)255;
                        }
                    }
                    else
                    {
                        row.AsSpan(source, components).CopyTo(color.AsSpan(target, components));
                        if (alpha is not null)
                            alpha[pixel] = type is 4 or 6 ? row[source + components]
                                : (byte)(row[source] == transparency![1] && row[source + 1] == transparency[3] && row[source + 2] == transparency[5] ? 0 : 255);
                    }
                }
                (previous, row) = (row, previous);
            }
            // This read forces the inflater through its end marker and Adler checksum, and
            // catches excess pixels. Unused bytes after the zlib end in IDAT are PNG-permitted.
            if (zlib.ReadByte() != -1) throw Invalid("PNG has excess decompressed scanline data.");
        }
        catch (InvalidDataException) { throw Invalid("Invalid, corrupt or truncated PNG zlib stream."); }
        return new(width, height, components, Compress(color, cancellation), alpha is null ? null : Compress(alpha, cancellation));
    }

    // BCL inflation can return EOF for incomplete input. Throwing at encoded EOF makes
    // missing deflate end/checksum observable while allowing buffered reads for throughput.
    private sealed class CompleteInput : MemoryStream
    {
        private readonly byte[] encoded;
        private readonly CancellationToken cancellation;
        internal CompleteInput(byte[] bytes, CancellationToken cancellation) : base(bytes, false)
        { encoded = bytes; this.cancellation = cancellation; }
        public override int Read(Span<byte> buffer)
        {
            cancellation.ThrowIfCancellationRequested();
            if (buffer.Length != 0 && Position == Length) throw new InvalidDataException("Incomplete PNG zlib stream.");
            var count = Math.Min(buffer.Length, encoded.Length - (int)Position);
            encoded.AsSpan((int)Position, count).CopyTo(buffer); Position += count; return count;
        }
        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
    }
    private static byte[] Compress(byte[] plane, CancellationToken cancellation)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.Optimal, true))
            for (var at = 0; at < plane.Length;)
            {
                cancellation.ThrowIfCancellationRequested(); var count = Math.Min(16384, plane.Length - at);
                zlib.Write(plane.AsSpan(at, count)); at += count;
            }
        cancellation.ThrowIfCancellationRequested(); return output.ToArray();
    }
    private static int Paeth(int left, int up, int upperLeft)
    {
        var p = left + up - upperLeft; var a = Math.Abs(p - left); var b = Math.Abs(p - up); var c = Math.Abs(p - upperLeft);
        return a <= b && a <= c ? left : b <= c ? up : upperLeft;
    }
    private static uint Crc(ReadOnlySpan<byte> bytes, CancellationToken cancellation)
    {
        var crc = uint.MaxValue;
        for (var i = 0; i < bytes.Length; i++)
        { if ((i & 16383) == 0) cancellation.ThrowIfCancellationRequested(); crc = CrcTable[(crc ^ bytes[i]) & 255] ^ (crc >> 8); }
        return ~crc;
    }
    private static uint[] MakeCrcTable()
    {
        var table = new uint[256];
        for (var i = 0; i < table.Length; i++)
        { var c = (uint)i; for (var j = 0; j < 8; j++) c = (c >> 1) ^ ((c & 1) != 0 ? 0xedb88320u : 0); table[i] = c; }
        return table;
    }
    private static FactsPdfException Invalid(string message) => new("FPDF1601", message);
    private static FactsPdfException Unsupported(string message) => new("FPDF1602", message);
    private static FactsPdfException Limit(string message) => new("FPDF1603", message);
}
