namespace FactsPDF;

/// <summary>Immutable explicitly supplied image. Format is recognized from bytes; D1 supports bounded baseline JPEG.</summary>
public sealed class PdfImage
{
    private readonly byte[] data;
    private PdfImage(byte[] owned, JpegInfo info)
    { data = owned; PixelWidth = info.Width; PixelHeight = info.Height; Components = info.Components; }
    public int PixelWidth { get; }
    public int PixelHeight { get; }
    public int ByteLength => data.Length;
    internal int Components { get; }
    internal ReadOnlySpan<byte> Data => data;

    /// <summary>Copies explicit image bytes after checking the encoded limit, then validates their supported format.</summary>
    public static PdfImage LoadImage(ReadOnlySpan<byte> source, PdfImageLimits? limits = null,
        CancellationToken cancellationToken = default)
    {
        limits = Validate(limits); cancellationToken.ThrowIfCancellationRequested();
        if (source.Length > limits.MaxBytes) throw JpegErrors.Limit("Image exceeds MaxBytes.");
        return LoadOwned(source.ToArray(), limits, cancellationToken);
    }

    /// <summary>Reads at most MaxBytes+1 bytes without seeking; leaves the caller's readable stream open.</summary>
    public static PdfImage LoadImage(Stream source, PdfImageLimits? limits = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.CanRead) throw new ArgumentException("Image stream must be readable.", nameof(source));
        limits = Validate(limits); cancellationToken.ThrowIfCancellationRequested();
        using var buffer = new MemoryStream(Math.Min(8192, limits.MaxBytes));
        var chunk = new byte[8192];
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var count = source.Read(chunk, 0, (int)Math.Min(chunk.Length, (long)limits.MaxBytes - buffer.Length + 1));
            if (count == 0) break;
            if (count > limits.MaxBytes - buffer.Length) throw JpegErrors.Limit("Image exceeds MaxBytes.");
            buffer.Write(chunk, 0, count);
        }
        return LoadOwned(buffer.ToArray(), limits, cancellationToken);
    }

    private static PdfImageLimits Validate(PdfImageLimits? limits)
    {
        limits ??= new();
        if (limits.MaxBytes < 1 || limits.MaxDimension < 1 || limits.MaxPixels < 1
            || limits.MaxDecodedBytes < 1 || limits.MaxMcuBlocks < 1)
            throw new ArgumentOutOfRangeException(nameof(limits), "Every image limit must be positive.");
        return limits;
    }

    private static PdfImage LoadOwned(byte[] bytes, PdfImageLimits limits, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        // Content recognition only; a path extension or MIME label is never consulted.
        if (bytes.Length < 2 || bytes[0] == 255 && bytes[1] == 0)
            throw JpegErrors.Invalid("Empty or malformed image signature.");
        if (bytes[0] != 255 || bytes[1] != 216)
            throw JpegErrors.Unsupported("Unsupported image format. Supply a supported baseline JPEG image.");
        return new(bytes, JpegReader.Read(bytes, limits, cancellation));
    }
}
