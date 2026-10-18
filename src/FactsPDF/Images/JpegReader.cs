using System.Buffers.Binary;

namespace FactsPDF;

internal static class JpegErrors
{
    internal static FactsPdfException Invalid(string message) => new("FPDF1601", message);
    internal static FactsPdfException Unsupported(string message) => new("FPDF1602", message);
    internal static FactsPdfException Limit(string message) => new("FPDF1603", message);
    internal static void Need(bool condition, string message)
    { if (!condition) throw Invalid(message); }
}

internal readonly record struct JpegInfo(int Width, int Height, int Components);
internal sealed record JpegComponent(int Id, int Blocks, int Quantizer);

/// <summary>Bounded single-frame baseline JFIF structural and entropy validation, without IDCT or pixel buffers.</summary>
internal ref struct JpegReader
{
    private readonly ReadOnlySpan<byte> data;
    private readonly PdfImageLimits limits;
    private readonly CancellationToken cancellation;
    private readonly bool[] quantizers;
    private readonly JpegHuffmanTable?[,] huffman;
    private JpegComponent[] components;
    private int position, segmentCount, metadataBytes, restartInterval, mcuCount;
    private JpegInfo info;
    private bool hasFrame, hasExif;

    private JpegReader(ReadOnlySpan<byte> bytes, PdfImageLimits bounds, CancellationToken token)
    {
        data = bytes; limits = bounds; cancellation = token;
        quantizers = new bool[4]; huffman = new JpegHuffmanTable?[2, 2]; components = [];
        position = 2; segmentCount = metadataBytes = restartInterval = mcuCount = 0;
        info = default; hasFrame = hasExif = false;
    }

    internal static JpegInfo Read(ReadOnlySpan<byte> bytes, PdfImageLimits limits, CancellationToken cancellation)
    {
        var reader = new JpegReader(bytes, limits, cancellation);
        return reader.Read();
    }

    private JpegInfo Read()
    {
        while (true)
        {
            cancellation.ThrowIfCancellationRequested();
            var marker = Marker();
            JpegErrors.Need(marker is not (0xd8 or 0xd9) && marker is not (>= 0xd0 and <= 0xd7), "Unexpected standalone JPEG marker.");
            if (marker == 1) throw JpegErrors.Unsupported("JPEG TEM markers are unsupported.");
            var payload = Segment();
            if (segmentCount == 1 && marker != 0xe0)
                throw JpegErrors.Unsupported("Images require an explicit JFIF APP0 color contract.");
            if (marker is >= 0xe0 and <= 0xef or 0xfe)
            {
                if (payload.Length > 65_536 - metadataBytes) throw JpegErrors.Limit("Image metadata exceeds 65536 bytes.");
                metadataBytes += payload.Length;
            }
            switch (marker)
            {
                case 0xe0:
                    if (segmentCount != 1) throw JpegErrors.Unsupported("Additional APP0/JFIF extension segments are unsupported.");
                    Jfif(payload); break;
                case 0xe1:
                    if (hasExif || !payload.StartsWith("Exif\0\0"u8))
                        throw JpegErrors.Unsupported("Only one Exif APP1 segment is supported.");
                    hasExif = true; JpegExif.Validate(payload[6..], cancellation); break;
                case 0xfe: break;
                case 0xdb: Quantizers(payload); break;
                case 0xc4: Huffman(payload); break;
                case 0xc0: Frame(payload); break;
                case 0xdd:
                    JpegErrors.Need(payload.Length == 2, "Invalid JPEG restart interval length.");
                    restartInterval = U16(payload); break;
                case 0xda:
                    Scan(payload); return info;
                default:
                    throw JpegErrors.Unsupported("JPEG coding process, color profile or marker is outside the supported baseline JFIF subset.");
            }
        }
    }

    private byte Byte()
    {
        if ((position & 8191) == 0) cancellation.ThrowIfCancellationRequested();
        if (position == data.Length) throw JpegErrors.Invalid("Truncated JPEG marker or header.");
        return data[position++];
    }

    private int Marker()
    {
        JpegErrors.Need(Byte() == 255, "Expected a JPEG marker.");
        int marker; do { marker = Byte(); } while (marker == 255);
        JpegErrors.Need(marker != 0, "Stuffed data is not a JPEG header marker.");
        return marker;
    }

    private ReadOnlySpan<byte> Segment()
    {
        if (++segmentCount > 256) throw JpegErrors.Limit("Image exceeds 256 marker segments.");
        var length = (Byte() << 8) | Byte();
        JpegErrors.Need(length >= 2 && length - 2 <= data.Length - position, "Truncated or invalid JPEG segment length.");
        var payload = data.Slice(position, length - 2); position += length - 2; return payload;
    }

    private static int U16(ReadOnlySpan<byte> bytes) => BinaryPrimitives.ReadUInt16BigEndian(bytes);

    private static void Jfif(ReadOnlySpan<byte> p)
    {
        if (!p.StartsWith("JFIF\0"u8)) throw JpegErrors.Unsupported("Image requires JFIF APP0 metadata.");
        JpegErrors.Need(p.Length >= 14, "Truncated JFIF metadata.");
        if (p[5] != 1 || p[6] > 2 || p[7] > 2 || p[12] != 0 || p[13] != 0)
            throw JpegErrors.Unsupported("JFIF version, density units or thumbnail is unsupported.");
        JpegErrors.Need(p.Length == 14 && U16(p[8..]) > 0 && U16(p[10..]) > 0, "Invalid JFIF size or density.");
        if (U16(p[8..]) != U16(p[10..])) throw JpegErrors.Unsupported("Non-square JFIF pixels are unsupported.");
    }

    private void Quantizers(ReadOnlySpan<byte> p)
    {
        JpegErrors.Need(!p.IsEmpty, "Empty JPEG quantization segment.");
        while (!p.IsEmpty)
        {
            var spec = p[0]; p = p[1..]; var id = spec & 15;
            if ((spec >> 4) != 0) throw JpegErrors.Unsupported("Only 8-bit JPEG quantizers are supported.");
            JpegErrors.Need(id < 4 && p.Length >= 64, "Invalid JPEG quantizer table or length.");
            if (quantizers[id]) throw JpegErrors.Unsupported("JPEG quantizer redefinition is unsupported.");
            for (var i = 0; i < 64; i++) JpegErrors.Need(p[i] != 0, "JPEG quantizers must be nonzero.");
            quantizers[id] = true; p = p[64..];
        }
    }

    private void Huffman(ReadOnlySpan<byte> p)
    {
        JpegErrors.Need(!p.IsEmpty, "Empty JPEG Huffman segment.");
        while (!p.IsEmpty)
        {
            JpegErrors.Need(p.Length >= 17, "Truncated JPEG Huffman table.");
            var kind = p[0] >> 4; var id = p[0] & 15;
            JpegErrors.Need(kind <= 1, "Invalid JPEG Huffman table class.");
            if (id > 1) throw JpegErrors.Unsupported("Only baseline Huffman table identifiers 0 and 1 are supported.");
            if (huffman[kind, id] is not null) throw JpegErrors.Unsupported("JPEG Huffman redefinition is unsupported.");
            var count = 0; for (var i = 1; i <= 16; i++) count += p[i];
            JpegErrors.Need(count is > 0 and <= 256 && count <= p.Length - 17, "Invalid JPEG Huffman symbol count.");
            huffman[kind, id] = new(p.Slice(1, 16), p.Slice(17, count), kind);
            p = p[(17 + count)..];
        }
    }

    private void Frame(ReadOnlySpan<byte> p)
    {
        if (hasFrame) throw JpegErrors.Unsupported("Multiple JPEG frames are unsupported.");
        JpegErrors.Need(p.Length >= 6, "Truncated JPEG frame.");
        if (p[0] != 8) throw JpegErrors.Unsupported("Only 8-bit baseline JPEG is supported.");
        var height = U16(p[1..]); var width = U16(p[3..]); var count = (int)p[5];
        JpegErrors.Need(width > 0 && height > 0, "JPEG dimensions must be nonzero.");
        if (count is not (1 or 3)) throw JpegErrors.Unsupported("Only grayscale or JFIF YCbCr images are supported.");
        JpegErrors.Need(p.Length == 6 + 3 * count, "Invalid JPEG frame component length.");
        var seen = new bool[256]; var sumBlocks = 0; var horizontal = 1; var vertical = 1;
        components = new JpegComponent[count];
        for (var i = 0; i < count; i++)
        {
            var at = 6 + i * 3; var id = p[at]; var sampling = p[at + 1]; var q = p[at + 2];
            JpegErrors.Need(!seen[id], "Duplicate JPEG component identifier."); seen[id] = true;
            if (id != i + 1) throw JpegErrors.Unsupported("JFIF components must be ordered Y, Cb, Cr.");
            if (i == 0 && count == 3)
            {
                if (sampling is not (0x11 or 0x21 or 0x22)) throw JpegErrors.Unsupported("Unsupported JPEG chroma sampling.");
                horizontal = sampling >> 4; vertical = sampling & 15;
            }
            else if (sampling != 0x11) throw JpegErrors.Unsupported("Unsupported JPEG component sampling.");
            JpegErrors.Need(q < 4, "Invalid JPEG quantization table identifier.");
            var blocks = (sampling >> 4) * (sampling & 15); sumBlocks += blocks;
            components[i] = new(id, blocks, q);
        }
        var pixels = checked((long)width * height);
        var samples = checked(pixels * count);
        var mcus = checked(((width + 8L * horizontal - 1) / (8 * horizontal))
            * ((height + 8L * vertical - 1) / (8 * vertical)));
        if (width > limits.MaxDimension || height > limits.MaxDimension || pixels > limits.MaxPixels
            || samples > limits.MaxDecodedBytes || checked(mcus * sumBlocks) > limits.MaxMcuBlocks)
            throw JpegErrors.Limit("Image exceeds dimension, pixel, decoded-byte or MCU-block limits.");
        info = new(width, height, count); mcuCount = checked((int)mcus); hasFrame = true;
    }

    private void Scan(ReadOnlySpan<byte> p)
    {
        JpegErrors.Need(hasFrame && p.Length >= 1, "JPEG scan requires a frame.");
        if (p[0] != components.Length) throw JpegErrors.Unsupported("JPEG requires one complete interleaved scan.");
        JpegErrors.Need(p.Length == 1 + 2 * components.Length + 3, "Invalid JPEG scan header length.");
        JpegErrors.Need(p[^3] == 0 && p[^2] == 63 && p[^1] == 0, "Invalid baseline JPEG spectral or approximation parameters.");
        var dc = new JpegHuffmanTable[components.Length]; var ac = new JpegHuffmanTable[components.Length];
        for (var i = 0; i < components.Length; i++)
        {
            if (p[1 + 2 * i] != components[i].Id) throw JpegErrors.Unsupported("JPEG scan component order is unsupported.");
            var d = p[2 + 2 * i] >> 4; var a = p[2 + 2 * i] & 15;
            if (d > 1 || a > 1) throw JpegErrors.Unsupported("Unsupported baseline JPEG Huffman table reference.");
            JpegErrors.Need(quantizers[components[i].Quantizer] && huffman[0, d] is not null
                && huffman[1, a] is not null, "JPEG scan references an undefined table.");
            dc[i] = huffman[0, d]!; ac[i] = huffman[1, a]!;
        }
        var entropy = new JpegEntropyReader(data[position..], cancellation);
        entropy.Validate(mcuCount, components, dc, ac, restartInterval);
    }
}
