using System.Globalization;
using System.Text;

namespace FactsPDF;

/// <summary>
/// Shared ASCII/Unicode PDF painter. Solid borders are filled rectangles,
/// with independent side widths/colors. All boxes are painted before text.
/// Coordinates are PDF points; layout's top-origin becomes PDF bottom-origin.
/// </summary>
internal static class PdfPaintSerializer
{
    internal static void Append(StringBuilder output, IReadOnlyList<PaintedBox> boxes,
        double pageHeight, int maxOutputBytes, CancellationToken cancellation)
    {
        foreach (var box in boxes.OrderBy(x => x.Order))
        {
            cancellation.ThrowIfCancellationRequested();
            var s = box.Style;
            var x = box.X;
            var y = box.Top;
            var width = box.Width;
            var height = box.Height;
            if (!double.IsFinite(x + y + width + height) || width < 0 || height < 0)
                throw new InvalidOperationException("Invalid paint rectangle.");
            if (s.BackgroundColor.HasValue)
                Rect(output, s.BackgroundColor.Value, x, y, width, height, pageHeight, maxOutputBytes);

            var top = s.BorderTop.EffectiveWidth;
            var right = s.BorderRight.EffectiveWidth;
            var bottom = s.BorderBottom.EffectiveWidth;
            var left = s.BorderLeft.EffectiveWidth;
            if (top > 0)
                Rect(output, s.BorderTop.Color ?? box.TextColor, x, y, width, top, pageHeight, maxOutputBytes);
            if (bottom > 0)
                Rect(output, s.BorderBottom.Color ?? box.TextColor,
                    x, y + height - bottom, width, bottom, pageHeight, maxOutputBytes);
            var middleTop = y + top;
            var middleHeight = Math.Max(0, height - top - bottom);
            if (left > 0)
                Rect(output, s.BorderLeft.Color ?? box.TextColor,
                    x, middleTop, left, middleHeight, pageHeight, maxOutputBytes);
            if (right > 0)
                Rect(output, s.BorderRight.Color ?? box.TextColor,
                    x + width - right, middleTop, right, middleHeight, pageHeight, maxOutputBytes);
        }
    }

    private static void Rect(StringBuilder output, Rgb color, double x, double top,
        double width, double height, double pageHeight, int maxOutputBytes)
    {
        if (width <= 0 || height <= 0) return;
        var y = pageHeight - top - height;
        output.Append("q ").Append(N(color.R)).Append(' ').Append(N(color.G)).Append(' ')
            .Append(N(color.B)).Append(" rg ").Append(N(x)).Append(' ').Append(N(y))
            .Append(' ').Append(N(width)).Append(' ').Append(N(height))
            .Append(" re f Q\n");
        // Bound the temporary PDF content stream as rectangles are appended.
        // A command can exceed the limit by at most its own small encoding;
        // never build an unbounded string before the serializer rejects it.
        if (output.Length > maxOutputBytes)
            throw new FactsPdfException("FPDF1401", "PDF paint content exceeds MaxOutputBytes.");
    }

    private static string N(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
