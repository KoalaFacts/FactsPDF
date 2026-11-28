namespace FactsPDF;

/// <summary>
/// Images written inline as <c>data:</c> URLs in <c>img src</c>: <c>data:image/png;base64,...</c> and
/// <c>data:image/jpeg;base64,...</c>. Each distinct URL is decoded once per conversion through the same
/// bounded loader as supplied images, and counts against the same <see cref="PdfOptions.MaxImageResources"/>
/// and <see cref="PdfOptions.MaxTotalImageBytes"/> limits together with them.
/// </summary>
internal sealed class DataUriImages
{
    private readonly Dictionary<string, PdfImage> loaded = new(StringComparer.Ordinal);
    private readonly PdfOptions options;
    private readonly CancellationToken cancellation;
    private int count;
    private long bytes;

    internal DataUriImages(PdfOptions options, CancellationToken cancellation)
    {
        this.options = options;
        this.cancellation = cancellation;
        count = options.Images.Count;
        foreach (var image in options.Images.Values) bytes += image.ByteLength;
    }

    /// <summary>Whether an img src is a data URL.</summary>
    internal static bool IsDataUri(string src) => src.StartsWith("data:", StringComparison.OrdinalIgnoreCase);

    /// <summary>Decodes a data URL image, or fails FPDF1103 (a form that is not supported), FPDF1603 (a limit)
    /// or the image loader's own code.</summary>
    internal PdfImage Load(string src, int offset)
    {
        cancellation.ThrowIfCancellationRequested();
        if (loaded.TryGetValue(src, out var known)) return known;
        var comma = src.IndexOf(',', StringComparison.Ordinal);
        var header = comma < 0 ? ReadOnlySpan<char>.Empty : src.AsSpan(5, comma - 5);
        if (!(header.Equals("image/png;base64", StringComparison.OrdinalIgnoreCase)
            || header.Equals("image/jpeg;base64", StringComparison.OrdinalIgnoreCase)))
            throw new FactsPdfException("FPDF1103",
                "img data URLs must be data:image/png;base64,... or data:image/jpeg;base64,...; other media types, parameters and percent-encoded bodies are not supported.", offset);
        var body = src.AsSpan(comma + 1);
        // A data URL body is percent-decoded before base64 decoding; escapes are not supported.
        if (body.Contains('%'))
            throw new FactsPdfException("FPDF1103", "img data URLs with percent escapes in the base64 body are not supported.", offset);
        if (count >= options.MaxImageResources) throw new FactsPdfException("FPDF1603", "Images exceed MaxImageResources.", offset);
        var remaining = options.MaxTotalImageBytes - bytes;
        if ((long)body.Length / 4 * 3 > remaining + 3) throw new FactsPdfException("FPDF1603", "Images exceed MaxTotalImageBytes.", offset);
        var decoded = new byte[(body.Length + 3) / 4 * 3];
        // White space is skipped as the forgiving-base64 decode skips it; missing padding is not accepted.
        if (!Convert.TryFromBase64Chars(body, decoded, out var written))
            throw new FactsPdfException("FPDF1103", "img data URL holds invalid or unpadded base64.", offset);
        if (written > remaining) throw new FactsPdfException("FPDF1603", "Images exceed MaxTotalImageBytes.", offset);
        var image = PdfImage.FromBytes(decoded.AsSpan(0, written), new PdfImageLimits { MaxBytes = (int)Math.Max(1, remaining) }, cancellation);
        count++;
        bytes += image.ByteLength;
        loaded.Add(src, image);
        return image;
    }
}
