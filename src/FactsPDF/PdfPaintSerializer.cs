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
    private static readonly TextDecoration[] DecorationOrder =
        [TextDecoration.Underline, TextDecoration.LineThrough, TextDecoration.Overline];
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
                output.Append("q ").AppendInvariant(color.R, Number).Append(' ').AppendInvariant(color.G, Number).Append(' ').AppendInvariant(color.B, Number).Append(" rg ").AppendInvariant(x - r, Number).Append(' ').AppendInvariant(y - r, Number).Append(' ').AppendInvariant(2 * r, Number).Append(' ').AppendInvariant(2 * r, Number).Append(" re f Q\n");
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
                    output.Append("q ").AppendInvariant(color.R, Number).Append(' ').AppendInvariant(color.G, Number).Append(' ').AppendInvariant(color.B, Number).Append(" RG ").AppendInvariant(r * 0.25, Number).Append(" w ").AppendInvariant(x + radius, Number).Append(' ').AppendInvariant(y, Number).Append(" m ");
                else
                    output.Append("q ").AppendInvariant(color.R, Number).Append(' ').AppendInvariant(color.G, Number).Append(' ').AppendInvariant(color.B, Number).Append(" rg ").AppendInvariant(x + r, Number).Append(' ').AppendInvariant(y, Number).Append(" m ");
                output.AppendInvariant(x + radius, Number).Append(' ').AppendInvariant(y + k, Number).Append(' ').AppendInvariant(x + k, Number).Append(' ').AppendInvariant(y + radius, Number).Append(' ').AppendInvariant(x, Number).Append(' ').AppendInvariant(y + radius, Number).Append(" c ");
                output.AppendInvariant(x - k, Number).Append(' ').AppendInvariant(y + radius, Number).Append(' ').AppendInvariant(x - radius, Number).Append(' ').AppendInvariant(y + k, Number).Append(' ').AppendInvariant(x - radius, Number).Append(' ').AppendInvariant(y, Number).Append(" c ");
                output.AppendInvariant(x - radius, Number).Append(' ').AppendInvariant(y - k, Number).Append(' ').AppendInvariant(x - k, Number).Append(' ').AppendInvariant(y - radius, Number).Append(' ').AppendInvariant(x, Number).Append(' ').AppendInvariant(y - radius, Number).Append(" c ");
                output.AppendInvariant(x + k, Number).Append(' ').AppendInvariant(y - radius, Number).Append(' ').AppendInvariant(x + radius, Number).Append(' ').AppendInvariant(y - k, Number).Append(' ').AppendInvariant(x + radius, Number).Append(' ').AppendInvariant(y, Number).Append(" c");
                output.Append(hollow ? " S Q\n" : " f Q\n");
            }
            if (output.Length > maxOutputBytes)
                throw new FactsPdfException("FPDF1401", "PDF content exceeds MaxOutputBytes.", disc.SourceOffset);
        }
    }

    // Decorations follow the already wrapped, font-resolved glyph advances.
    // One painted path per selected line is emitted for each measured run,
    // keeping Courier and embedded TrueType geometry consistent.
    internal static void AppendTextDecoration(StringBuilder output, PlacedText run,
        int maxOutputBytes)
    {
        if (run.Style.Decoration == TextDecoration.None || run.Width <= 0) return;
        var size = run.Style.FontSize;
        var thickness = run.Style.DecorationThickness ?? Math.Max(0.3, size * 0.06);
        foreach (var line in DecorationOrder)
        {
            if ((run.Style.Decoration & line) == TextDecoration.None) continue;
            var centre = line switch
            {
                TextDecoration.Underline => run.Baseline - (run.Style.UnderlineOffset ?? 0.12 * size),
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
                output.Append("q ").AppendInvariant(c.R, Number).Append(' ').AppendInvariant(c.G, Number).Append(' ')
                    .AppendInvariant(c.B, Number).Append(" rg ").AppendInvariant(run.X, Number).Append(' ').AppendInvariant(y, Number)
                    .Append(' ').AppendInvariant(run.Width, Number).Append(' ').AppendInvariant(thickness, Number)
                    .Append(" re f Q\n");
            }
            else
            {
                // A single PDF stroked path is charged as one decoration command.
                // Clip the dash marks/round caps inside the measured glyph advance:
                // no extension into neighbouring cells or outside the PDF panel.
                if (run.Width < 0.001)
                    throw new FactsPdfException("FPDF1302", "Text decoration is too short to paint.", run.SourceOffset);
                output.Append("q ").AppendInvariant(c.R, Number).Append(' ').AppendInvariant(c.G, Number).Append(' ')
                    .AppendInvariant(c.B, Number).Append(" RG ").AppendInvariant(thickness, Number).Append(" w ");
                if (run.Style.DecorationPattern == TextDecorationPattern.Dashed)
                    output.Append('[').AppendInvariant(3 * thickness, Number).Append(' ')
                        .AppendInvariant(3 * thickness, Number).Append("] 0 d ");
                else if (run.Style.DecorationPattern == TextDecorationPattern.Dotted)
                    output.Append("1 J [0.001 ").AppendInvariant(2 * thickness, Number).Append("] 0 d ");
                else
                    throw new InvalidOperationException("Unsupported text decoration stroke.");
                output.AppendInvariant(run.X, Number).Append(' ').AppendInvariant(y, Number).Append(' ')
                    .AppendInvariant(run.Width, Number).Append(' ').AppendInvariant(thickness, Number)
                    .Append(" re W n ").AppendInvariant(run.X, Number).Append(' ').AppendInvariant(centre, Number)
                    .Append(" m ").AppendInvariant(run.X + run.Width, Number).Append(' ').AppendInvariant(centre, Number)
                    .Append(" l S Q\n");
            }
            if (output.Length > maxOutputBytes)
                throw new FactsPdfException("FPDF1401", "PDF text decoration exceeds MaxOutputBytes.", run.SourceOffset);
        }

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
            if (s.HasRoundedCorners)
            {
                RoundedBox(output, box, pageHeight, maxOutputBytes);
                continue;
            }
            if (s.BackgroundColor.HasValue)
                Rect(output, s.BackgroundColor.Value, x, y, width, height, pageHeight, maxOutputBytes, box.SourceOffset);

            // Slice does not repeat horizontal borders on intermediate pages.
            // Short first/last fragments clip the border to their own bounds;
            // these exact heights are also used by PaintedBox.CommandCount.
            var top = box.TopBorderHeight;
            var right = s.BorderRight.EffectiveWidth;
            var bottom = box.BottomBorderHeight;
            var left = s.BorderLeft.EffectiveWidth;
            if (top > 0)
                Border(output, s.BorderTop, box.TextColor, x, y, width, top,
                    horizontal: true, pageHeight, maxOutputBytes, box.SourceOffset);
            if (bottom > 0)
                Border(output, s.BorderBottom, box.TextColor, x, y + height - bottom, width, bottom,
                    horizontal: true, pageHeight, maxOutputBytes, box.SourceOffset);
            var middleTop = y + top;
            var middleHeight = Math.Max(0, height - top - bottom);
            if (left > 0)
                Border(output, s.BorderLeft, box.TextColor, x, middleTop, left, middleHeight,
                    horizontal: false, pageHeight, maxOutputBytes, box.SourceOffset);
            if (right > 0)
                Border(output, s.BorderRight, box.TextColor, x + width - right, middleTop, right, middleHeight,
                    horizontal: false, pageHeight, maxOutputBytes, box.SourceOffset);
            AppendOutline(output, box, pageHeight, maxOutputBytes);
        }
    }


    // One PDF paint operation, separated from border geometry and text flow.
    // Side strokes continue on every page; horizontal strokes are present
    // only on the first and last page fragment, respectively.
    private static void AppendOutline(StringBuilder output, PaintedBox box,
        double pageHeight, int maxOutputBytes)
    {
        var edge = box.Style.Outline;
        if (edge.EffectiveWidth <= 0 || box.Width <= 0 || box.Height <= 0) return;
        var thickness = edge.EffectiveWidth;
        var gap = box.Style.OutlineOffset + thickness / 2;
        var xLeft = box.X - gap;
        var xRight = box.X + box.Width + gap;
        var yTop = pageHeight - box.Top + gap;
        var yBottom = pageHeight - box.Top - box.Height - gap;
        if (!double.IsFinite(xLeft + xRight + yTop + yBottom))
            throw new FactsPdfException("FPDF1302", "Invalid report outline geometry.", box.SourceOffset);
        var ink = edge.Color ?? box.TextColor;
        output.Append("q ").AppendInvariant(ink.R, Number).Append(' ').AppendInvariant(ink.G, Number).Append(' ')
            .AppendInvariant(ink.B, Number).Append(" RG ").AppendInvariant(thickness, Number).Append(" w ");
        if (edge.Pattern == BorderPattern.Dashed)
            output.Append('[').AppendInvariant(3 * thickness, Number).Append(' ')
                .AppendInvariant(3 * thickness, Number).Append("] 0 d ");
        else if (edge.Pattern == BorderPattern.Dotted)
            output.Append("1 J [0.001 ").AppendInvariant(2 * thickness, Number).Append("] 0 d ");
        else if (edge.Pattern != BorderPattern.Solid)
            throw new InvalidOperationException("Invalid outline pattern.");
        if (edge.Pattern == BorderPattern.Solid)
        {
            // A solid outline must be one joined path at each visible corner.
            // Independent butt-capped edge subpaths leave an unpainted outer
            // quadrant at thick corners. The default miter join closes it.
            // Continuations have no imaginary top/bottom cap across pages.
            output.Append("0 j ");
            if (box.IsFirstFragment && box.IsLastFragment)
                output.AppendInvariant(xLeft, Number).Append(' ').AppendInvariant(yBottom, Number).Append(" m ")
                    .AppendInvariant(xLeft, Number).Append(' ').AppendInvariant(yTop, Number).Append(" l ")
                    .AppendInvariant(xRight, Number).Append(' ').AppendInvariant(yTop, Number).Append(" l ")
                    .AppendInvariant(xRight, Number).Append(' ').AppendInvariant(yBottom, Number).Append(" l h ");
            else if (box.IsFirstFragment)
                output.AppendInvariant(xLeft, Number).Append(' ').AppendInvariant(yBottom, Number).Append(" m ")
                    .AppendInvariant(xLeft, Number).Append(' ').AppendInvariant(yTop, Number).Append(" l ")
                    .AppendInvariant(xRight, Number).Append(' ').AppendInvariant(yTop, Number).Append(" l ")
                    .AppendInvariant(xRight, Number).Append(' ').AppendInvariant(yBottom, Number).Append(" l ");
            else if (box.IsLastFragment)
                output.AppendInvariant(xLeft, Number).Append(' ').AppendInvariant(yTop, Number).Append(" m ")
                    .AppendInvariant(xLeft, Number).Append(' ').AppendInvariant(yBottom, Number).Append(" l ")
                    .AppendInvariant(xRight, Number).Append(' ').AppendInvariant(yBottom, Number).Append(" l ")
                    .AppendInvariant(xRight, Number).Append(' ').AppendInvariant(yTop, Number).Append(" l ");
            else
                output.AppendInvariant(xLeft, Number).Append(' ').AppendInvariant(yTop, Number).Append(" m ")
                    .AppendInvariant(xLeft, Number).Append(' ').AppendInvariant(yBottom, Number).Append(" l ")
                    .AppendInvariant(xRight, Number).Append(' ').AppendInvariant(yTop, Number).Append(" m ")
                    .AppendInvariant(xRight, Number).Append(' ').AppendInvariant(yBottom, Number).Append(" l ");
        }
        else
        {
            // Dashed/dotted paths intentionally remain separate: their dash
            // phase and rounded caps restart at each scoped report-panel edge.
            output.AppendInvariant(xLeft, Number).Append(' ').AppendInvariant(yTop, Number).Append(" m ")
                .AppendInvariant(xLeft, Number).Append(' ').AppendInvariant(yBottom, Number).Append(" l ")
                .AppendInvariant(xRight, Number).Append(' ').AppendInvariant(yTop, Number).Append(" m ")
                .AppendInvariant(xRight, Number).Append(' ').AppendInvariant(yBottom, Number).Append(" l ");
            if (box.IsFirstFragment)
                output.AppendInvariant(xLeft, Number).Append(' ').AppendInvariant(yTop, Number).Append(" m ")
                    .AppendInvariant(xRight, Number).Append(' ').AppendInvariant(yTop, Number).Append(" l ");
            if (box.IsLastFragment)
                output.AppendInvariant(xLeft, Number).Append(' ').AppendInvariant(yBottom, Number).Append(" m ")
                    .AppendInvariant(xRight, Number).Append(' ').AppendInvariant(yBottom, Number).Append(" l ");
        }
        output.Append("S Q\n");
        if (output.Length > maxOutputBytes)
            throw new FactsPdfException("FPDF1401", "PDF outline content exceeds MaxOutputBytes.", box.SourceOffset);
    }

    // Four physical corners paint one closed outer background and one even-odd
    // border ring. On continued boxes the top two radii belong only to the
    // first fragment; the bottom two belong only to the final fragment.
    private static void RoundedBox(StringBuilder output, PaintedBox box,
        double pageHeight, int maxOutputBytes)
    {
        if (box.Width <= 0 || box.Height <= 0) return;
        var style = box.Style;
        var x = box.X;
        var y = pageHeight - box.Top - box.Height;
        var w = box.Width;
        var h = box.Height;
        var top = box.IsFirstFragment;
        var bottom = box.IsLastFragment;
        var uniform = style.CornerRadius == style.CornerTopRightRadius &&
            style.CornerRadius == style.CornerBottomRightRadius &&
            style.CornerRadius == style.CornerBottomLeftRadius;
        double tl, tr, br, bl;
        if (uniform)
        {
            // Preserve historical one-radius rounding and PDF byte output.
            var r = Math.Min(style.CornerRadius,
                Math.Min(w / 2, h / (top && bottom ? 2 : 1)));
            tl = tr = top ? r : 0;
            br = bl = bottom ? r : 0;
        }
        else
        {
            (tl, tr, br, bl) = FitCorners(
                top ? style.CornerRadius : 0,
                top ? style.CornerTopRightRadius : 0,
                bottom ? style.CornerBottomRightRadius : 0,
                bottom ? style.CornerBottomLeftRadius : 0, w, h);
        }

        if (style.BackgroundColor is Rgb bg)
        {
            output.Append("q ").AppendInvariant(bg.R, Number).Append(' ').AppendInvariant(bg.G, Number).Append(' ')
                .AppendInvariant(bg.B, Number).Append(" rg ");
            RoundedPath(output, x, y, w, h, tl, tr, br, bl);
            output.Append("f Q\n");
            if (output.Length > maxOutputBytes)
                throw new FactsPdfException("FPDF1401", "PDF rounded background exceeds MaxOutputBytes.", box.SourceOffset);
        }

        var border = style.BorderLeft.EffectiveWidth;
        if (border <= 0) return;
        var color = style.BorderLeft.Color ?? box.TextColor;
        output.Append("q ").AppendInvariant(color.R, Number).Append(' ').AppendInvariant(color.G, Number).Append(' ')
            .AppendInvariant(color.B, Number).Append(" rg ");
        RoundedPath(output, x, y, w, h, tl, tr, br, bl);
        var topInset = top ? Math.Min(border, h) : 0;
        var bottomInset = bottom ? Math.Min(border, h - topInset) : 0;
        var innerWidth = w - 2 * border;
        var innerHeight = h - topInset - bottomInset;
        if (innerWidth > 0 && innerHeight > 0)
        {
            double itl, itr, ibr, ibl;
            if (uniform)
            {
                // The legacy inner-ring clamp must remain byte-for-byte
                // identical for every formerly supported single-radius card.
                var topInner = top ? Math.Max(0, tl - border) : 0;
                var bottomInner = bottom ? Math.Max(0, bl - border) : 0;
                var clamp = Math.Min(innerWidth / 2,
                    innerHeight / (top && bottom ? 2 : 1));
                itl = itr = Math.Min(topInner, clamp);
                ibr = ibl = Math.Min(bottomInner, clamp);
            }
            else
                (itl, itr, ibr, ibl) = FitCorners(
                    Math.Max(0, tl - border), Math.Max(0, tr - border),
                    Math.Max(0, br - border), Math.Max(0, bl - border),
                    innerWidth, innerHeight);
            RoundedPath(output, x + border, y + bottomInset,
                innerWidth, innerHeight, itl, itr, ibr, ibl);
            output.Append("f* Q\n");
        }
        else output.Append("f Q\n");
        if (output.Length > maxOutputBytes)
            throw new FactsPdfException("FPDF1401", "PDF rounded border exceeds MaxOutputBytes.", box.SourceOffset);
    }

    // CSS's proportional corner fitting rule prevents adjacent curves from
    // overlapping even on narrow cards and short first/last page fragments.
    // There is no elliptical syntax: both axes use the same radius at a corner.
    private static (double TL, double TR, double BR, double BL) FitCorners(
        double tl, double tr, double br, double bl, double width, double height)
    {
        var scale = 1d;
        if (tl + tr > width) scale = Math.Min(scale, width / (tl + tr));
        if (bl + br > width) scale = Math.Min(scale, width / (bl + br));
        if (tl + bl > height) scale = Math.Min(scale, height / (tl + bl));
        if (tr + br > height) scale = Math.Min(scale, height / (tr + br));
        return (tl * scale, tr * scale, br * scale, bl * scale);
    }

    // PDF uses bottom-origin coordinates. Cubic arcs are tangent to adjacent
    // straight edges at each independently measured corner.
    private static void RoundedPath(StringBuilder output, double x, double y,
        double width, double height, double tl, double tr, double br, double bl)
    {
        const double k = 0.5522847498307936;
        var right = x + width;
        var upper = y + height;
        output.AppendInvariant(x + bl, Number).Append(' ').AppendInvariant(y, Number).Append(" m ");
        output.AppendInvariant(right - br, Number).Append(' ').AppendInvariant(y, Number).Append(" l ");
        if (br > 0)
            output.AppendInvariant(right - br + k * br, Number).Append(' ').AppendInvariant(y, Number).Append(' ')
                .AppendInvariant(right, Number).Append(' ').AppendInvariant(y + br - k * br, Number).Append(' ')
                .AppendInvariant(right, Number).Append(' ').AppendInvariant(y + br, Number).Append(" c ");
        else output.AppendInvariant(right, Number).Append(' ').AppendInvariant(y, Number).Append(" l ");
        output.AppendInvariant(right, Number).Append(' ').AppendInvariant(upper - tr, Number).Append(" l ");
        if (tr > 0)
            output.AppendInvariant(right, Number).Append(' ').AppendInvariant(upper - tr + k * tr, Number).Append(' ')
                .AppendInvariant(right - tr + k * tr, Number).Append(' ').AppendInvariant(upper, Number).Append(' ')
                .AppendInvariant(right - tr, Number).Append(' ').AppendInvariant(upper, Number).Append(" c ");
        else output.AppendInvariant(right, Number).Append(' ').AppendInvariant(upper, Number).Append(" l ");
        output.AppendInvariant(x + tl, Number).Append(' ').AppendInvariant(upper, Number).Append(" l ");
        if (tl > 0)
            output.AppendInvariant(x + tl - k * tl, Number).Append(' ').AppendInvariant(upper, Number).Append(' ')
                .AppendInvariant(x, Number).Append(' ').AppendInvariant(upper - tl + k * tl, Number).Append(' ')
                .AppendInvariant(x, Number).Append(' ').AppendInvariant(upper - tl, Number).Append(" c ");
        else output.AppendInvariant(x, Number).Append(' ').AppendInvariant(upper, Number).Append(" l ");
        output.AppendInvariant(x, Number).Append(' ').AppendInvariant(y + bl, Number).Append(" l ");
        if (bl > 0)
            output.AppendInvariant(x, Number).Append(' ').AppendInvariant(y + bl - k * bl, Number).Append(' ')
                .AppendInvariant(x + bl - k * bl, Number).Append(' ').AppendInvariant(y, Number).Append(' ')
                .AppendInvariant(x + bl, Number).Append(' ').AppendInvariant(y, Number).Append(" c ");
        else output.AppendInvariant(x, Number).Append(' ').AppendInvariant(y, Number).Append(" l ");
        output.Append("h ");
    }

    // A stroked edge replaces exactly one solid-border rectangle in the
    // display-command budget. Each stroke is independently enclosed in q/Q,
    // so PDF dash state never leaks into later text, images or panel borders.
    // Stroke centre lines are inset by half the visible width, including the
    // round caps on dotted strokes. Top/bottom are omitted on middle fragments;
    // side patterns restart on each retained page fragment (no CSS slice clone).
    private static void Border(StringBuilder output, BorderEdge edge, Rgb textColor,
        double x, double top, double width, double height, bool horizontal,
        double pageHeight, int maxOutputBytes, int sourceOffset)
    {
        if (width <= 0 || height <= 0 || edge.EffectiveWidth <= 0) return;
        var color = edge.Color ?? textColor;
        if (edge.Pattern == BorderPattern.Solid)
        {
            Rect(output, color, x, top, width, height, pageHeight, maxOutputBytes, sourceOffset);
            return;
        }
        var stroke = horizontal ? height : width;
        // Reject genuinely sub-millipoint widths, but tolerate the roundoff
        // introduced when page coordinates are subtracted for a 0.001pt edge.
        // Both values serialize to the same visible 0.001pt PDF linewidth.
        if (stroke < 0.001 - 1e-10)
            throw new FactsPdfException("FPDF1302", "Patterned border is too thin to paint.", sourceOffset);
        // Border strokes span the *full* measured edge and are clipped to the
        // edge rectangle. An inset centreline can disappear after formatting rounds
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
        if (SamePdfNumber(x1, x2) && SamePdfNumber(y1, y2))
            throw new FactsPdfException("FPDF1302", "Patterned border edge is too short to paint.", sourceOffset);
        output.Append("q ").AppendInvariant(color.R, Number).Append(' ').AppendInvariant(color.G, Number).Append(' ')
            .AppendInvariant(color.B, Number).Append(" RG ").AppendInvariant(stroke, Number).Append(" w ");
        if (edge.Pattern == BorderPattern.Dashed)
            output.Append('[').AppendInvariant(3 * stroke, Number).Append(' ').AppendInvariant(3 * stroke, Number).Append("] 0 d ");
        else if (edge.Pattern == BorderPattern.Dotted)
            output.Append("1 J [0.001 ").AppendInvariant(2 * stroke, Number).Append("] 0 d ");
        else
            throw new InvalidOperationException("Invalid patterned border kind.");
        // One clip per stroke, inside the q/Q state pair. This prevents both
        // dash fragments and round caps from painting outside the measured box.
        output.AppendInvariant(x, Number).Append(' ').AppendInvariant(pageHeight - top - height, Number)
            .Append(' ').AppendInvariant(width, Number).Append(' ').AppendInvariant(height, Number)
            .Append(" re W n ");
        output.AppendInvariant(x1, Number).Append(' ').AppendInvariant(y1, Number).Append(" m ")
            .AppendInvariant(x2, Number).Append(' ').AppendInvariant(y2, Number).Append(" l S Q\n");
        if (output.Length > maxOutputBytes)
            throw new FactsPdfException("FPDF1401", "PDF patterned border exceeds MaxOutputBytes.", sourceOffset);
    }

    private static void Rect(StringBuilder output, Rgb color, double x, double top,
        double width, double height, double pageHeight, int maxOutputBytes, int sourceOffset)
    {
        if (width <= 0 || height <= 0) return;
        var y = pageHeight - top - height;
        output.Append("q ").AppendInvariant(color.R, Number).Append(' ').AppendInvariant(color.G, Number).Append(' ')
            .AppendInvariant(color.B, Number).Append(" rg ").AppendInvariant(x, Number).Append(' ').AppendInvariant(y, Number)
            .Append(' ').AppendInvariant(width, Number).Append(' ').AppendInvariant(height, Number)
            .Append(" re f Q\n");
        // Bound the temporary PDF content stream as rectangles are appended.
        // A command can exceed the limit by at most its own small encoding;
        // never build an unbounded string before the serializer rejects it.
        if (output.Length > maxOutputBytes)
            throw new FactsPdfException("FPDF1401", "PDF paint content exceeds MaxOutputBytes.", sourceOffset);
    }

    private const string Number = "0.###";

    // True when both values serialize to the same PDF number, compared without allocating
    // the two strings (falls back to them only for values longer than 64 characters).
    private static bool SamePdfNumber(double a, double b)
    {
        Span<char> left = stackalloc char[64];
        Span<char> right = stackalloc char[64];
        return a.TryFormat(left, out var leftLength, Number, CultureInfo.InvariantCulture)
            && b.TryFormat(right, out var rightLength, Number, CultureInfo.InvariantCulture)
            ? left[..leftLength].SequenceEqual(right[..rightLength])
            : a.ToString(Number, CultureInfo.InvariantCulture) == b.ToString(Number, CultureInfo.InvariantCulture);
    }
}
