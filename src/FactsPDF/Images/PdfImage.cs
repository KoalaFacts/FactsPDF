namespace FactsPDF;

/// <summary>Immutable explicitly supplied image. Format is recognized from bytes; supports bounded baseline JPEG and static noninterlaced PNG.</summary>
public sealed class PdfImage
{
    private readonly byte[] data;
    private readonly int byteLength;
    private readonly byte[]? alpha;
    private PdfImage(byte[] owned, JpegInfo info)
    { data = owned; byteLength = owned.Length; PixelWidth = info.Width; PixelHeight = info.Height; Components = info.Components; }
    private PdfImage(int encodedLength, PngInfo info)
    { data = info.Color; alpha = info.Alpha; byteLength = encodedLength; IsPng = true; PixelWidth = info.Width; PixelHeight = info.Height; Components = info.Components; }
    /// <summary>Width in pixels.</summary>
    public int PixelWidth { get; }
    /// <summary>Height in pixels.</summary>
    public int PixelHeight { get; }
    /// <summary>Original encoded input size, before any PNG expansion or recompression.</summary>
    public int ByteLength => byteLength;
    internal bool IsPng { get; }
    internal bool HasAlpha => alpha is not null;
    internal ReadOnlySpan<byte> Alpha => alpha;
    internal int Components { get; }
    internal ReadOnlySpan<byte> Data => data;

    /// <summary>Copies image bytes after checking the encoded limit, then validates their supported format.</summary>
    /// <param name="source">Baseline JPEG or static, non-interlaced PNG bytes.</param>
    /// <param name="limits">Decoding limits; null uses the defaults.</param>
    /// <param name="cancellationToken">Cancels validation.</param>
    /// <returns>An immutable image that can be shared across conversions and threads.</returns>
    /// <exception cref="FactsPdfException">The bytes are not a supported image or exceed a limit.</exception>
    public static PdfImage FromBytes(ReadOnlySpan<byte> source, PdfImageLimits? limits = null,
        CancellationToken cancellationToken = default)
    {
        limits = Validate(limits); cancellationToken.ThrowIfCancellationRequested();
        if (source.Length > limits.MaxBytes) throw JpegErrors.Limit("Image exceeds MaxBytes.");
        return LoadOwned(source.ToArray(), limits, cancellationToken);
    }

    /// <summary>Reads at most MaxBytes+1 bytes without seeking, then validates them; the stream is not closed.</summary>
    /// <param name="source">Readable stream positioned at the image data.</param>
    /// <param name="limits">Decoding limits; null uses the defaults.</param>
    /// <param name="cancellationToken">Cancels reading and validation.</param>
    /// <returns>An immutable image that can be shared across conversions and threads.</returns>
    /// <exception cref="FactsPdfException">The data is not a supported image or exceeds a limit.</exception>
    public static PdfImage FromStream(Stream source, PdfImageLimits? limits = null,
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

    /// <summary>Reads and validates a baseline JPEG or static, non-interlaced PNG file.</summary>
    /// <param name="path">Path of the image file; only this file is read.</param>
    /// <param name="limits">Decoding limits; null uses the defaults.</param>
    /// <param name="cancellationToken">Cancels reading and validation.</param>
    /// <returns>An immutable image that can be shared across conversions and threads.</returns>
    /// <exception cref="FactsPdfException">The file is not a supported image or exceeds a limit.</exception>
    public static PdfImage FromFile(string path, PdfImageLimits? limits = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1, FileOptions.SequentialScan);
        return FromStream(file, limits, cancellationToken);
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
        if (bytes.AsSpan().StartsWith(PngReader.Signature))
            return new(bytes.Length, PngReader.Read(bytes, limits, cancellation));
        if (bytes.Length < 2 || bytes[0] == 255 && bytes[1] == 0)
            throw JpegErrors.Invalid("Empty or malformed image signature.");
        if (bytes[0] != 255 || bytes[1] != 216)
            throw JpegErrors.Unsupported("Unsupported image format. Supply a supported baseline JPEG or static PNG image.");
        return new(bytes, JpegReader.Read(bytes, limits, cancellation));
    }
}
