using System.Globalization;
using System.Text;

namespace FactsPDF;

// The same identity-based registry, placement and binary writer serve both font paths.
internal sealed class PdfImageWriter
{
    private readonly Dictionary<PdfImage, int> indices = new(ReferenceEqualityComparer.Instance);
    private readonly List<PdfImage> images = [];
    internal int Count => images.Count;

    internal PdfImageWriter(List<LayoutPage> pages, CancellationToken cancellation)
    {
        foreach (var page in pages)
            foreach (var placed in page.Images)
            {
                cancellation.ThrowIfCancellationRequested();
                if (indices.TryAdd(placed.Image, images.Count)) images.Add(placed.Image);
            }
    }

    internal string Resources(LayoutPage page, int firstObject, int limit, CancellationToken cancellation)
    {
        if (page.Images.Count == 0) return "";
        var result = new StringBuilder(" /XObject <<"); var seen = new HashSet<int>();
        foreach (var placed in page.Images)
        {
            cancellation.ThrowIfCancellationRequested();
            var index = indices[placed.Image];
            if (seen.Add(index)) result.Append(FormattableString.Invariant($" /Im{index + 1} {firstObject + index} 0 R"));
            Check(result.Length, limit);
        }
        return result.Append(" >>").ToString();
    }

    internal void Append(StringBuilder content, LayoutPage page, PdfOptions options, CancellationToken cancellation)
    {
        foreach (var placed in page.Images)
        {
            cancellation.ThrowIfCancellationRequested();
            content.Append(FormattableString.Invariant($"q {N(placed.Width)} 0 0 {N(placed.Height)} {N(placed.X)} {N(options.PageHeight - placed.Top - placed.Height)} cm /Im{indices[placed.Image] + 1} Do Q\n"));
            Check(content.Length, options.MaxOutputBytes);
        }
    }

    internal void WriteObject(Stream output, int index, int id, int limit, CancellationToken cancellation)
    {
        var image = images[index];
        void WriteText(string text)
        {
            cancellation.ThrowIfCancellationRequested();
            Check(output.Position + text.Length, limit);
            output.Write(Encoding.ASCII.GetBytes(text));
        }
        WriteText(FormattableString.Invariant($"{id} 0 obj\n<< /Type /XObject /Subtype /Image /Width {image.PixelWidth} /Height {image.PixelHeight} /BitsPerComponent 8 /ColorSpace /Device{(image.Components == 1 ? "Gray" : "RGB")} /Filter /DCTDecode /DecodeParms << /ColorTransform {(image.Components == 1 ? 0 : 1)} >> /Length {image.ByteLength} >>\nstream\n"));
        Check(output.Position + image.ByteLength, limit);
        for (var offset = 0; offset < image.ByteLength;)
        {
            cancellation.ThrowIfCancellationRequested();
            var count = Math.Min(16_384, image.ByteLength - offset);
            output.Write(image.Data.Slice(offset, count)); offset += count;
        }
        WriteText("\nendstream\nendobj\n");
    }

    private static void Check(long length, int limit)
    {
        if (length > limit) throw new FactsPdfException("FPDF1401", "PDF image content exceeds MaxOutputBytes.");
    }
    private static string N(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);
}
