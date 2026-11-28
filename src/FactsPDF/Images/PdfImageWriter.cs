using System.Text;

namespace FactsPDF;

// The same identity-based registry, placement and binary writer serve both font paths.
internal sealed class PdfImageWriter
{
    private const string Number = "0.######";
    private readonly Dictionary<PdfImage, int> indices = new(ReferenceEqualityComparer.Instance);
    private readonly List<(PdfImage Image, bool Mask)> images = [];
    internal int ObjectCount => images.Count;

    internal PdfImageWriter(List<LayoutPage> pages, CancellationToken cancellation)
    {
        foreach (var page in pages)
            foreach (var placed in page.Images)
            {
                cancellation.ThrowIfCancellationRequested();
                if (placed.Image is { } image && indices.TryAdd(image, images.Count))
                {
                    images.Add((image, false));
                    if (image.HasAlpha) images.Add((image, true));
                }
            }
    }

    /// <summary>
    /// Appends the page's <c>/XObject</c> resource entries (nothing for a page without images). The
    /// byte limit applies to the entries alone, as when they were built as a separate string.
    /// </summary>
    internal void AppendResources(StringBuilder output, LayoutPage page, int firstObject, int limit, CancellationToken cancellation,
        List<int>? forms = null)
    {
        // An inline svg is painted in the content stream itself and needs no XObject.
        if (!page.Images.Exists(placed => placed.Image is not null))
        {
            // The transparency groups of opacity below 1 (PdfEffects) are XObjects too.
            if (forms is not { Count: > 0 }) return;
            output.Append(" /XObject <<");
            PdfEffects.AppendFormEntries(output, forms);
            output.Append(" >>");
            return;
        }
        var start = output.Length;
        output.Append(" /XObject <<"); var seen = new HashSet<int>();
        foreach (var placed in page.Images)
        {
            cancellation.ThrowIfCancellationRequested();
            if (placed.Image is null) continue;
            var index = indices[placed.Image];
            if (seen.Add(index)) output.Append(" /Im").Append(index + 1).Append(' ').Append(firstObject + index).Append(" 0 R");
            Check(output.Length - start, limit);
        }
        if (forms is { Count: > 0 }) PdfEffects.AppendFormEntries(output, forms);
        output.Append(" >>");
    }

    internal void Append(StringBuilder content, LayoutPage page, PdfOptions options, CancellationToken cancellation,
        ref PdfAlphaStates alpha)
    {
        var clipping = page.Clips.Count == 0 ? null : new PdfClipping(page, PdfClipping.Kind.Images, options.PageHeight);
        var imageIndex = 0;
        foreach (var placed in page.Images)
        {
            clipping?.Enter(content, imageIndex++);
            cancellation.ThrowIfCancellationRequested();
            if (placed.Decoration is { } decoration)
                PdfPaintSerializer.Append(content,
                    [decoration with { X = placed.X + decoration.X, Top = placed.Top + decoration.Top }],
                    options.PageHeight, options.MaxOutputBytes, cancellation, ref alpha);
            if (placed.Vector is { } vector)
            {
                SvgPainter.Append(content, vector, placed, options, ref alpha);
                continue;
            }
            var (imageX, imageTop, imageWidth, imageHeight) = (placed.X, placed.Top, placed.Width, placed.Height);
            content.Append("q ");
            if (placed.Placement is { } placement)
            {
                // object-fit: the pixels are scaled and positioned inside the content box, which clips them.
                content.AppendInvariant(placed.X, Number).Append(' ').AppendInvariant(options.PageHeight - placed.Top - placed.Height, Number)
                    .Append(' ').AppendInvariant(placed.Width, Number).Append(' ').AppendInvariant(placed.Height, Number).Append(" re W n ");
                (imageX, imageTop, imageWidth, imageHeight) = Fit(placed, placement);
            }
            content.AppendInvariant(imageWidth, Number).Append(" 0 0 ").AppendInvariant(imageHeight, Number).Append(' ')
                .AppendInvariant(imageX, Number).Append(' ').AppendInvariant(options.PageHeight - imageTop - imageHeight, Number)
                .Append(" cm /Im").Append(indices[placed.Image!] + 1).Append(" Do Q\n");
            Check(content.Length, options.MaxOutputBytes);
        }
        clipping?.Finish(content);
    }

    // The used rectangle of the image's pixels inside the content box for object-fit and object-position:
    // the natural size is the pixel size at 0.75pt per pixel, scaled by the fit and placed by the position.
    private static (double X, double Top, double Width, double Height) Fit(PlacedImage placed, ObjectPlacement placement)
    {
        var naturalWidth = placed.Image!.PixelWidth * 0.75;
        var naturalHeight = placed.Image.PixelHeight * 0.75;
        var contain = Math.Min(placed.Width / naturalWidth, placed.Height / naturalHeight);
        var scale = placement.Fit switch
        {
            ObjectFit.Contain => contain,
            ObjectFit.Cover => Math.Max(placed.Width / naturalWidth, placed.Height / naturalHeight),
            ObjectFit.None => 1,
            ObjectFit.ScaleDown => Math.Min(1, contain),
            _ => throw new InvalidOperationException("Fill needs no placement.")
        };
        var width = naturalWidth * scale;
        var height = naturalHeight * scale;
        return (placed.X + placement.X.Resolve(placed.Width - width), placed.Top + placement.Y.Resolve(placed.Height - height), width, height);
    }

    internal void WriteObject(Stream output, int index, int id, int limit, CancellationToken cancellation)
    {
        var (image, mask) = images[index];
        var bytes = mask ? image.Alpha : image.Data;
        void WriteText(string text)
        {
            cancellation.ThrowIfCancellationRequested();
            Check(output.Position + text.Length, limit);
            output.Write(Encoding.ASCII.GetBytes(text));
        }
        if (!image.IsPng)
            WriteText(FormattableString.Invariant($"{id} 0 obj\n<< /Type /XObject /Subtype /Image /Width {image.PixelWidth} /Height {image.PixelHeight} /BitsPerComponent 8 /ColorSpace /Device{(image.Components == 1 ? "Gray" : "RGB")} /Filter /DCTDecode /DecodeParms << /ColorTransform {(image.Components == 1 ? 0 : 1)} >> /Length {image.ByteLength} >>\nstream\n"));
        else
        {
            var softMask = !mask && image.HasAlpha ? FormattableString.Invariant($" /SMask {id + 1} 0 R") : "";
            WriteText(FormattableString.Invariant($"{id} 0 obj\n<< /Type /XObject /Subtype /Image /Width {image.PixelWidth} /Height {image.PixelHeight} /BitsPerComponent 8 /ColorSpace /Device{(mask || image.Components == 1 ? "Gray" : "RGB")} /Filter /FlateDecode{softMask} /Length {bytes.Length} >>\nstream\n"));
        }
        Check(output.Position + bytes.Length, limit);
        for (var offset = 0; offset < bytes.Length;)
        {
            cancellation.ThrowIfCancellationRequested();
            var count = Math.Min(16_384, bytes.Length - offset);
            output.Write(bytes.Slice(offset, count)); offset += count;
        }
        WriteText("\nendstream\nendobj\n");
    }

    private static void Check(long length, int limit)
    {
        if (length > limit) throw new FactsPdfException("FPDF1401", "PDF image content exceeds MaxOutputBytes.");
    }
}
