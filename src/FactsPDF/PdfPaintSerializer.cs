using System.Globalization;
using System.Text;

namespace FactsPDF;

/// <summary>
/// Shared ASCII/Unicode PDF painter. Legacy solid borders are filled
/// rectangles; bounded dashed/dotted borders are measured PDF strokes.
/// All boxes are painted before text.
/// Coordinates are PDF points; layout's top-origin becomes PDF bottom-origin.
/// </summary>
internal static class PdfPaintSerializer
{
    internal static void AppendDiscs(StringBuilder output, IReadOnlyList<PlacedDisc> discs,
        int maxOutputBytes, CancellationToken cancellation)
    {
        foreach (var disc in discs)
        {
            cancellation.ThrowIfCancellationRequested();
            var x = disc.X; var y = disc.Y; var r = disc.Radius;
            if (!double.IsFinite(x + y + r) || r <= 0)
                throw new InvalidOperationException("Invalid list disc geometry.");
            // Every vector marker consumes exactly one retained paint command.
            // Keep the legacy disc byte path identical for unstyled reports.
            var color = disc.Color;
            if (disc.Kind == ListMarkerKind.Square)
            {
                output.Append($"q {N(color.R)} {N(color.G)} {N(color.B)} rg {N(x - r)} {N(y - r)} {N(2 * r)} {N(2 * r)} re f Q\n");
            }
            else
            {
                if (disc.Kind is not (ListMarkerKind.Disc or ListMarkerKind.Circle))
                    throw new InvalidOperationException("Invalid vector list marker kind.");
                // A stroked outline, not a white fill: the open centre leaves
                // existing painted backgrounds visible. Inset the curve so
                // the full stroke stays within the measured marker width.
                var hollow = disc.Kind == ListMarkerKind.Circle;
                var radius = hollow ? r * 0.8 : r;
                var k = radius * 0.5522847498307936;
                if (hollow)
                    output.Append($"q {N(color.R)} {N(color.G)} {N(color.B)} RG {N(r * 0.25)} w {N(x + radius)} {N(y)} m ");
                else
                    output.Append($"q {N(color.R)} {N(color.G)} {N(color.B)} rg {N(x + r)} {N(y)} m ");
                output.Append($"{N(x + radius)} {N(y + k)} {N(x + k)} {N(y + radius)} {N(x)} {N(y + radius)} c ");
                output.Append($"{N(x - k)} {N(y + radius)} {N(x - radius)} {N(y + k)} {N(x - radius)} {N(y)} c ");
                output.Append($"{N(x - radius)} {N(y - k)} {N(x - k)} {N(y - radius)} {N(x)} {N(y - radius)} c ");
                output.Append($"{N(x + k)} {N(y - radius)} {N(x + radius)} {N(y - k)} {N(x + radius)} {N(y)} c");
                output.Append(hollow ? " S Q\n" : " f Q\n");
            }
            if (output.Length > maxOutputBytes) throw new FactsPdfException("FPDF1401", "PDF content exceeds MaxOutputBytes.");
        }
    }

    // Decorations follow the already wrapped, font-resolved glyph advances.
    // One opaque filled rectangle is emitted for each decorated placed run,
    // keeping Courier and embedded TrueType geometry consistent.
    internal static void AppendTextDecoration(StringBuilder output, PlacedText run,
        int maxOutputBytes)
    {
        if (run.Style.Decoration == TextDecoration.None || run.Width <= 0) return;
        var size = run.Style.FontSize;
        var thickness = Math.Max(0.3, size * 0.06);
        var centre = run.Style.Decoration switch
        {
            TextDecoration.Underline => run.Baseline - 0.12 * size,
            TextDecoration.LineThrough => run.Baseline + 0.29 * size,
            TextDecoration.Overline => run.Baseline + 0.78 * size,
            _ => throw new InvalidOperationException("Unsupported text decoration.")
        };
        var y = centre - thickness / 2;
        if (!double.IsFinite(run.X + y + run.Width + thickness) || run.Width < 0)
            throw new InvalidOperationException("Invalid text decoration geometry.");
        var c = run.Style.DecorationColor ?? run.Style.Color;
        if (run.Style.DecorationPattern == TextDecorationPattern.Solid)
        {
            // Keep the legacy opaque rectangle path byte-for-byte identical
            // when neither new paint longhand is supplied.
            output.Append("q ").Append(N(c.R)).Append(' ').Append(N(c.G)).Append(' ')
                .Append(N(c.B)).Append(" rg ").Append(N(run.X)).Append(' ').Append(N(y))
                .Append(' ').Append(N(run.Width)).Append(' ').Append(N(thickness))
                .Append(" re f Q\n");
        }
        else
        {
            // A single PDF stroked path is charged as one decoration command.
            // Clip the dash marks/round caps inside the measured glyph advance:
            // no extension into neighbouring cells or outside the PDF panel.
            if (run.Width < 0.001)
                throw new FactsPdfException("FPDF1302", "Text decoration is too short to paint.");
            output.Append("q ").Append(N(c.R)).Append(' ').Append(N(c.G)).Append(' ')
                .Append(N(c.B)).Append(" RG ").Append(N(thickness)).Append(" w ");
            if (run.Style.DecorationPattern == TextDecorationPattern.Dashed)
                output.Append('[').Append(N(3 * thickness)).Append(' ')
                    .Append(N(3 * thickness)).Append("] 0 d ");
            else if (run.Style.DecorationPattern == TextDecorationPattern.Dotted)
                output.Append("1 J [0.001 ").Append(N(2 * thickness)).Append("] 0 d ");
            else
                throw new InvalidOperationException("Unsupported text decoration stroke.");
            output.Append(N(run.X)).Append(' ').Append(N(y)).Append(' ')
                .Append(N(run.Width)).Append(' ').Append(N(thickness))
                .Append(" re W n ").Append(N(run.X)).Append(' ').Append(N(centre))
                .Append(" m ").Append(N(run.X + run.Width)).Append(' ').Append(N(centre))
                .Append(" l S Q\n");
        }
        if (output.Length > maxOutputBytes)
            throw new FactsPdfException("FPDF1401", "PDF text decoration exceeds MaxOutputBytes.");
    }

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

            // Slice does not repeat horizontal borders on intermediate pages.
            // Short first/last fragments clip the border to their own bounds;
            // these exact heights are also used by PaintedBox.CommandCount.
            var top = box.TopBorderHeight;
            var right = s.BorderRight.EffectiveWidth;
            var bottom = box.BottomBorderHeight;
            var left = s.BorderLeft.EffectiveWidth;
            if (top > 0)
                Border(output, s.BorderTop, box.TextColor, x, y, width, top,
                    horizontal: true, pageHeight, maxOutputBytes);
            if (bottom > 0)
                Border(output, s.BorderBottom, box.TextColor, x, y + height - bottom, width, bottom,
                    horizontal: true, pageHeight, maxOutputBytes);
            var middleTop = y + top;
            var middleHeight = Math.Max(0, height - top - bottom);
            if (left > 0)
                Border(output, s.BorderLeft, box.TextColor, x, middleTop, left, middleHeight,
                    horizontal: false, pageHeight, maxOutputBytes);
            if (right > 0)
                Border(output, s.BorderRight, box.TextColor, x + width - right, middleTop, right, middleHeight,
                    horizontal: false, pageHeight, maxOutputBytes);
        }
    }

    // A stroked edge replaces exactly one solid-border rectangle in the
    // display-command budget. Each stroke is independently enclosed in q/Q,
    // so PDF dash state never leaks into later text, images or panel borders.
    // Stroke centre lines are inset by half the visible width, including the
    // round caps on dotted strokes. Top/bottom are omitted on middle fragments;
    // side patterns restart on each retained page fragment (no CSS slice clone).
    private static void Border(StringBuilder output, BorderEdge edge, Rgb textColor,
        double x, double top, double width, double height, bool horizontal,
        double pageHeight, int maxOutputBytes)
    {
        if (width <= 0 || height <= 0 || edge.EffectiveWidth <= 0) return;
        var color = edge.Color ?? textColor;
        if (edge.Pattern == BorderPattern.Solid)
        {
            Rect(output, color, x, top, width, height, pageHeight, maxOutputBytes);
            return;
        }
        var stroke = horizontal ? height : width;
        // Reject genuinely sub-millipoint widths, but tolerate the roundoff
        // introduced when page coordinates are subtracted for a 0.001pt edge.
        // Both values serialize to the same visible 0.001pt PDF linewidth.
        if (stroke < 0.001 - 1e-10)
            throw new FactsPdfException("FPDF1302", "Patterned border is too thin to paint.");
        // Border strokes span the *full* measured edge and are clipped to the
        // edge rectangle. An inset centreline can disappear after N() rounds
        // two nearby endpoints to the same PDF coordinate (even when the
        // edge is longer than its thickness). Clipping also contains the
        // round end caps of dotted strokes on every page fragment.
        // The solid branch above deliberately retains its original PDF bytes.
        var x1 = horizontal ? x : x + width / 2;
        var x2 = horizontal ? x + width : x1;
        var y1 = horizontal ? pageHeight - top - height / 2 : pageHeight - top;
        var y2 = horizontal ? y1 : pageHeight - top - height;
        if (!double.IsFinite(x1 + x2 + y1 + y2 + stroke))
            throw new InvalidOperationException("Invalid patterned border geometry.");
        if (N(x1) == N(x2) && N(y1) == N(y2))
            throw new FactsPdfException("FPDF1302", "Patterned border edge is too short to paint.");
        output.Append("q ").Append(N(color.R)).Append(' ').Append(N(color.G)).Append(' ')
            .Append(N(color.B)).Append(" RG ").Append(N(stroke)).Append(" w ");
        if (edge.Pattern == BorderPattern.Dashed)
            output.Append('[').Append(N(3 * stroke)).Append(' ').Append(N(3 * stroke)).Append("] 0 d ");
        else if (edge.Pattern == BorderPattern.Dotted)
            output.Append("1 J [0.001 ").Append(N(2 * stroke)).Append("] 0 d ");
        else
            throw new InvalidOperationException("Invalid patterned border kind.");
        // One clip per stroke, inside the q/Q state pair. This prevents both
        // dash fragments and round caps from painting outside the measured box.
        output.Append(N(x)).Append(' ').Append(N(pageHeight - top - height))
            .Append(' ').Append(N(width)).Append(' ').Append(N(height))
            .Append(" re W n ");
        output.Append(N(x1)).Append(' ').Append(N(y1)).Append(" m ")
            .Append(N(x2)).Append(' ').Append(N(y2)).Append(" l S Q\n");
        if (output.Length > maxOutputBytes)
            throw new FactsPdfException("FPDF1401", "PDF patterned border exceeds MaxOutputBytes.");
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
