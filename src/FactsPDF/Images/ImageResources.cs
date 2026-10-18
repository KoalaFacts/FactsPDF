using System.Collections.ObjectModel;

namespace FactsPDF;

internal static class ImageResources
{
    internal static readonly IReadOnlyDictionary<string, PdfImage> Empty =
        new ReadOnlyDictionary<string, PdfImage>(new Dictionary<string, PdfImage>(StringComparer.Ordinal));

    internal static PdfOptions Snapshot(PdfOptions options, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (options.Images is null) throw new ArgumentException("Supply a non-null image resource map.", nameof(options));
        if (ReferenceEquals(options.Images, Empty)) return options;
        var images = new Dictionary<string, PdfImage>(StringComparer.Ordinal); long totalBytes = 0;
        foreach (var (name, image) in options.Images)
        {
            cancellation.ThrowIfCancellationRequested();
            if (images.Count == options.MaxImageResources) throw JpegErrors.Limit("Images exceed MaxImageResources.");
            if (!ValidName(name) || image is null)
                throw new ArgumentException("Image entries require a valid explicit name and non-null resource.", nameof(options));
            if (!images.TryAdd(name, image)) throw new ArgumentException("Image resource names must be unique.", nameof(options));
            if (image.ByteLength > options.MaxTotalImageBytes - totalBytes)
                throw JpegErrors.Limit("Images exceed MaxTotalImageBytes.");
            totalBytes += image.ByteLength;
        }
        return options with { Images = new ReadOnlyDictionary<string, PdfImage>(images) };
    }

    internal static bool ValidName(string? name)
    {
        if (name is null || name.Length is < 1 or > 128 || !char.IsAsciiLetterOrDigit(name[0])) return false;
        for (var i = 1; i < name.Length; i++)
            if (!char.IsAsciiLetterOrDigit(name[i]) && name[i] is not ('.' or '_' or '-')) return false;
        return true;
    }
}
