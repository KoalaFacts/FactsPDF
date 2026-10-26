namespace FactsPDF;

internal sealed class JpegHuffmanTable
{
    private readonly int[] minimum = new int[17], maximum = new int[17], first = new int[17];
    private readonly byte[] values;

    internal JpegHuffmanTable(ReadOnlySpan<byte> lengths, ReadOnlySpan<byte> symbols, int kind)
    {
        var code = 0; var index = 0;
        for (var length = 1; length <= 16; length++)
        {
            var count = lengths[length - 1];
            JpegErrors.Need(code + count <= 1 << length, "Oversubscribed JPEG Huffman prefix tree.");
            JpegErrors.Need(count == 0 || code + count - 1 < (1 << length) - 1, "JPEG Huffman codes cannot be all ones.");
            minimum[length] = code; maximum[length] = count == 0 ? -1 : code + count - 1; first[length] = index;
            index += count; code = (code + count) << 1;
        }
        JpegErrors.Need(index > 0 && index == symbols.Length, "Invalid JPEG Huffman symbol count.");
        foreach (var value in symbols)
            JpegErrors.Need(kind == 0 ? value <= 11 : value is 0 or 0xf0 || (value & 15) is >= 1 and <= 10,
                "Invalid baseline JPEG coefficient category.");
        values = symbols.ToArray();
    }

    internal int Decode(ref JpegEntropyReader reader)
    {
        var code = 0;
        for (var length = 1; length <= 16; length++)
        {
            code = (code << 1) | reader.Bit();
            if (code >= minimum[length] && code <= maximum[length])
                return values[first[length] + code - minimum[length]];
        }
        throw JpegErrors.Invalid("Invalid JPEG entropy Huffman code.");
    }
}

internal ref struct JpegEntropyReader
{
    private readonly ReadOnlySpan<byte> data;
    private readonly CancellationToken cancellation;
    private readonly Action<int>? onBlockCheckpoint;
    private int position, current, remaining;

    internal JpegEntropyReader(ReadOnlySpan<byte> bytes, CancellationToken token,
        Action<int>? onBlockCheckpoint = null)
    {
        data = bytes; cancellation = token; this.onBlockCheckpoint = onBlockCheckpoint;
        position = current = remaining = 0;
    }

    internal void Validate(int mcuCount, JpegComponent[] components, JpegHuffmanTable[] dc,
        JpegHuffmanTable[] ac, int restartInterval)
    {
        cancellation.ThrowIfCancellationRequested();
        var blocks = 0; var restart = 0;
        for (var mcu = 0; mcu < mcuCount; mcu++)
        {
            for (var component = 0; component < components.Length; component++)
                for (var block = 0; block < components[component].Blocks; block++)
                {
                    if ((blocks++ & 255) == 0)
                    {
                        onBlockCheckpoint?.Invoke(blocks);
                        cancellation.ThrowIfCancellationRequested();
                    }
                    Skip(dc[component].Decode(ref this));
                    var coefficient = 1;
                    while (coefficient < 64)
                    {
                        var symbol = ac[component].Decode(ref this);
                        if (symbol == 0) break;
                        if (symbol == 0xf0)
                        {
                            coefficient += 16;
                            JpegErrors.Need(coefficient <= 64, "JPEG zero run exceeds block coefficients.");
                            continue;
                        }
                        coefficient += symbol >> 4;
                        JpegErrors.Need(coefficient < 64, "JPEG coefficient run exceeds block boundary.");
                        Skip(symbol & 15); coefficient++;
                    }
                }
            if (restartInterval != 0 && (mcu + 1) % restartInterval == 0 && mcu + 1 < mcuCount)
            { ExpectMarker(0xd0 + restart); restart = (restart + 1) & 7; }
        }
        ExpectMarker(0xd9);
        JpegErrors.Need(position == data.Length, "Trailing data after JPEG EOI is unsupported.");
    }

    private int Byte()
    {
        if ((position & 8191) == 0) cancellation.ThrowIfCancellationRequested();
        if (position == data.Length) throw JpegErrors.Invalid("Truncated JPEG entropy or marker.");
        return data[position++];
    }

    internal int Bit()
    {
        if (remaining == 0)
        {
            current = Byte();
            if (current == 255) JpegErrors.Need(Byte() == 0, "Unexpected marker or missing JPEG byte stuffing.");
            remaining = 8;
        }
        return (current >> --remaining) & 1;
    }

    private void Skip(int bits) { for (var i = 0; i < bits; i++) Bit(); }

    private void ExpectMarker(int expected)
    {
        var mask = (1 << remaining) - 1;
        JpegErrors.Need((current & mask) == mask, "JPEG entropy padding bits must be ones."); remaining = 0;
        JpegErrors.Need(Byte() == 255, "Extra or incomplete JPEG entropy data.");
        int marker; do { marker = Byte(); } while (marker == 255);
        JpegErrors.Need(marker == expected, "Missing, premature or out-of-order JPEG restart/EOI marker.");
    }
}
