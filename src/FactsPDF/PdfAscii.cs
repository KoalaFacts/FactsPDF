using System.Text;

namespace FactsPDF;

// Writes page content streams built in a StringBuilder straight into the PDF
// output, chunk by chunk, without the intermediate string and byte[] copies.
internal static class PdfAscii
{
    private const int ChunkBytes = 4096;

    /// <summary>The bytes <see cref="Write"/> produces for <paramref name="text"/>.</summary>
    internal static int ByteCount(StringBuilder text)
    {
        var count = 0;
        foreach (var chunk in text.GetChunks())
            for (var at = 0; at < chunk.Length; at += ChunkBytes)
                count += Encoding.ASCII.GetByteCount(chunk.Span.Slice(at, Math.Min(ChunkBytes, chunk.Length - at)));
        return count;
    }

    /// <summary>Encodes <paramref name="text"/> as ASCII into <paramref name="output"/>.</summary>
    internal static void Write(Stream output, StringBuilder text, CancellationToken cancellation)
    {
        Span<byte> bytes = stackalloc byte[ChunkBytes];
        foreach (var chunk in text.GetChunks())
            for (var at = 0; at < chunk.Length; at += ChunkBytes)
            {
                cancellation.ThrowIfCancellationRequested();
                var written = Encoding.ASCII.GetBytes(chunk.Span.Slice(at, Math.Min(ChunkBytes, chunk.Length - at)), bytes);
                output.Write(bytes[..written]);
            }
    }

    /// <summary>
    /// Appends the upper-case hex of the ASCII bytes of <paramref name="text"/>, as
    /// <c>Convert.ToHexString(Encoding.ASCII.GetBytes(text))</c> does, without allocating.
    /// </summary>
    internal static void AppendHex(StringBuilder output, ReadOnlySpan<char> text)
    {
        if (!Ascii.IsValid(text))
        {
            output.Append(Convert.ToHexString(Encoding.ASCII.GetBytes(text.ToArray())));
            return;
        }
        Span<char> pair = stackalloc char[2];
        foreach (var c in text)
        {
            pair[0] = HexDigit(c >> 4);
            pair[1] = HexDigit(c & 0xF);
            output.Append(pair);
        }
    }

    private static char HexDigit(int value) => (char)(value < 10 ? '0' + value : 'A' + value - 10);
}
