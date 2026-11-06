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
                output.Append(CultureInfo.InvariantCulture, $"q {(color.R):0.###} {(color.G):0.###} {(color.B):0.###} rg {(x - r):0.###} {(y - r):0.###} {(2 * r):0.###} {(2 * r):0.###} re f Q\n");
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
                    output.Append(CultureInfo.InvariantCulture, $"q {(color.R):0.###} {(color.G):0.###} {(color.B):0.###} RG {(r * 0.25):0.###} w {(x + radius):0.###} {(y):0.###} m ");
                else
                    output.Append(CultureInfo.InvariantCulture, $"q {(color.R):0.###} {(color.G):0.###} {(color.B):0.###} rg {(x + r):0.###} {(y):0.###} m ");
                output.Append(CultureInfo.InvariantCulture, $"{(x + radius):0.###} {(y + k):0.###} {(x + k):0.###} {(y + radius):0.###} {(x):0.###} {(y + radius):0.###} c ");
                output.Append(CultureInfo.InvariantCulture, $"{(x - k):0.###} {(y + radius):0.###} {(x - radius):0.###} {(y + k):0.###} {(x - radius):0.###} {(y):0.###} c ");
                output.Append(CultureInfo.InvariantCulture, $"{(x - radius):0.###} {(y - k):0.###} {(x - k):0.###} {(y - radius):0.###} {(x):0.###} {(y - radius):0.###} c ");
                output.Append(CultureInfo.InvariantCulture, $"{(x + k):0.###} {(y - radius):0.###} {(x + radius):0.###} {(y - k):0.###} {(x + radius):0.###} {(y):0.###} c");
                output.Append(hollow ? " S Q\n" : " f Q\n");
            }
            if (output.Length > maxOutputBytes) throw new FactsPdfException("FPDF1401", "PDF content exceeds MaxOutputBytes.");
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
                output.Append("q ").Append(CultureInfo.InvariantCulture, $"{(c.R):0.###}").Append(' ').Append(CultureInfo.InvariantCulture, $"{(c.G):0.###}").Append(' ')
                    .Append(CultureInfo.InvariantCulture, $"{(c.B):0.###}").Append(" rg ").Append(CultureInfo.InvariantCulture, $"{(run.X):0.###}").Append(' ').Append(CultureInfo.InvariantCulture, $"{(y):0.###}")
                    .Append(' ').Append(CultureInfo.InvariantCulture, $"{(run.Width):0.###}").Append(' ').Append(CultureInfo.InvariantCulture, $"{(thickness):0.###}")
                    .Append(" re f Q\n");
            }
            else
            {
                // A single PDF stroked path is charged as one decoration command.
                // Clip the dash marks/round caps inside the measured glyph advance:
                // no extension into neighbouring cells or outside the PDF panel.
                if (run.Width < 0.001)
                    throw new FactsPdfException("FPDF1302", "Text decoration is too short to paint.");
                output.Append("q ").Append(CultureInfo.InvariantCulture, $"{(c.R):0.###}").Append(' ').Append(CultureInfo.InvariantCulture, $"{(c.G):0.###}").Append(' ')
                    .Append(CultureInfo.InvariantCulture, $"{(c.B):0.###}").Append(" RG ").Append(CultureInfo.InvariantCulture, $"{(thickness):0.###}").Append(" w ");
                if (run.Style.DecorationPattern == TextDecorationPattern.Dashed)
                    output.Append('[').Append(CultureInfo.InvariantCulture, $"{(3 * thickness):0.###}").Append(' ')
                        .Append(CultureInfo.InvariantCulture, $"{(3 * thickness):0.###}").Append("] 0 d ");
                else if (run.Style.DecorationPattern == TextDecorationPattern.Dotted)
                    output.Append("1 J [0.001 ").Append(CultureInfo.InvariantCulture, $"{(2 * thickness):0.###}").Append("] 0 d ");
                else
                    throw new InvalidOperationException("Unsupported text decoration stroke.");
                output.Append(CultureInfo.InvariantCulture, $"{(run.X):0.###}").Append(' ').Append(CultureInfo.InvariantCulture, $"{(y):0.###}").Append(' ')
                    .Append(CultureInfo.InvariantCulture, $"{(run.Width):0.###}").Append(' ').Append(CultureInfo.InvariantCulture, $"{(thickness):0.###}")
                    .Append(" re W n ").Append(CultureInfo.InvariantCulture, $"{(run.X):0.###}").Append(' ').Append(CultureInfo.InvariantCulture, $"{(centre):0.###}")
                    .Append(" m ").Append(CultureInfo.InvariantCulture, $"{(run.X + run.Width):0.###}").Append(' ').Append(CultureInfo.InvariantCulture, $"{(centre):0.###}")
                    .Append(" l S Q\n");
            }
            if (output.Length > maxOutputBytes)
                throw new FactsPdfException("FPDF1401", "PDF text decoration exceeds MaxOutputBytes.");
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
            throw new FactsPdfException("FPDF1302", "Invalid report outline geometry.");
        var ink = edge.Color ?? box.TextColor;
        output.Append("q ").Append(CultureInfo.InvariantCulture, $"{(ink.R):0.###}").Append(' ').Append(CultureInfo.InvariantCulture, $"{(ink.G):0.###}").Append(' ')
            .Append(CultureInfo.InvariantCulture, $"{(ink.B):0.###}").Append(" RG ").Append(CultureInfo.InvariantCulture, $"{(thickness):0.###}").Append(" w ");
        if (edge.Pattern == BorderPattern.Dashed)
            output.Append('[').Append(CultureInfo.InvariantCulture, $"{(3 * thickness):0.###}").Append(' ')
                .Append(CultureInfo.InvariantCulture, $"{(3 * thickness):0.###}").Append("] 0 d ");
        else if (edge.Pattern == BorderPattern.Dotted)
            output.Append("1 J [0.001 ").Append(CultureInfo.InvariantCulture, $"{(2 * thickness):0.###}").Append("] 0 d ");
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
                output.Append(CultureInfo.InvariantCulture, $"{(xLeft):0.###}").Append(' ').Append(CultureInfo.InvariantCulture, $"{(yBottom):0.###}").Append(" m ")
                    .Append(CultureInfo.InvariantCulture, $"{(xLeft):0.###}").Append(' ').Append(CultureInfo.InvariantCulture, $"{(yTop):0.###}").Append(" l ")
                    .Append(CultureInfo.InvariantCulture, $"{(xRight):0.###}").Append(' ').Append(CultureInfo.InvariantCulture, $"{(yTop):0.###}").Append(" l ")
                    .Append(CultureInfo.InvariantCulture, $"{(xRight):0.###}").Append(' ').Append(CultureInfo.InvariantCulture, $"{(yBottom):0.###}").Append(" l h ");
            else if (box.IsFirstFragment)
                output.Append(CultureInfo.InvariantCulture, $"{(xLeft):0.###}").Append(' ').Append(CultureInfo.InvariantCulture, $"{(yBottom):0.###}").Append(" m ")
                    .Append(CultureInfo.InvariantCulture, $"{(xLeft):0.###}").Append(' ').Append(CultureInfo.InvariantCulture, $"{(yTop):0.###}").Append(" l ")
                    .Append(CultureInfo.InvariantCulture, $"{(xRight):0.###}").Append(' ').Append(CultureInfo.InvariantCulture, $"{(yTop):0.###}").Append(" l ")
                    .Append(CultureInfo.InvariantCulture, $"{(xRight):0.###}").Append(' ').Append(CultureInfo.InvariantCulture, $"{(yBottom):0.###}").Append(" l ");
            else if (box.IsLastFragment)
                output.Append(CultureInfo.InvariantCulture, $"{(xLeft):0.###}").Append(' ').Append(CultureInfo.InvariantCulture, $"{(yTop):0.###}").Append(" m ")
                    .Append(CultureInfo.InvariantCulture, $"{(xLeft):0.###}").Append(' ').Append(CultureInfo.InvariantCulture, $"{(yBottom):0.###}").Append(" l ")
                    .Append(CultureInfo.InvariantCulture, $"{(xRight):0.###}").Append(' ').Append(CultureInfo.InvariantCulture, $"{(yBottom):0.###}").Append(" l ")
                    .Append(CultureInfo.InvariantCulture, $"{(xRight):0.###}").Append(' ').Append(CultureInfo.InvariantCulture, $"{(yTop):0.###}").Append(" l ");
            else
                output.Append(CultureInfo.InvariantCulture, $"{(xLeft):0.###}").Append(' ').Append(CultureInfo.InvariantCulture, $"{(yTop):0.###}").Append(" m ")
                    .Append(CultureInfo.InvariantCulture, $"{(xLeft):0.###}").Append(' ').Append(CultureInfo.InvariantCulture, $"{(yBottom):0.###}").Append(" l ")
                    .Append(CultureInfo.InvariantCulture, $"{(xRight):0.###}").Append(' ').Append(CultureInfo.InvariantCulture, $"{(yTop):0.###}").Append(" m ")
                    .Append(CultureInfo.InvariantCulture, $"{(xRight):0.###}").Append(' ').Append(CultureInfo.InvariantCulture, $"{(yBottom):0.###}").Append(" l ");
        }
        else
        {
            // Dashed/dotted paths intentionally remain separate: their dash
            // phase and rounded caps restart at each scoped report-panel edge.
            output.Append(CultureInfo.InvariantCulture, $"{(xLeft):0.###}").Append(' ').Append(CultureInfo.InvariantCulture, $"{(yTop):0.###}").Append(" m ")
                .Append(CultureInfo.InvariantCulture, $"{(xLeft):0.###}").Append(' ').Append(CultureInfo.InvariantCulture, $"{(yBottom):0.###}").Append(" l ")
                .Append(CultureInfo.InvariantCulture, $"{(xRight):0.###}").Append(' ').Append(CultureInfo.InvariantCulture, $"{(yTop):0.###}").Append(" m ")
                .Append(CultureInfo.InvariantCulture, $"{(xRight):0.###}").Append(' ').Append(CultureInfo.InvariantCulture, $"{(yBottom):0.###}").Append(" l ");
            if (box.IsFirstFragment)
                output.Append(CultureInfo.InvariantCulture, $"{(xLeft):0.###}").Append(' ').Append(CultureInfo.InvariantCulture, $"{(yTop):0.###}").Append(" m ")
                    .Append(CultureInfo.InvariantCulture, $"{(xRight):0.###}").Append(' ').Append(CultureInfo.InvariantCulture, $"{(yTop):0.###}").Append(" l ");
            if (box.IsLastFragment)
                output.Append(CultureInfo.InvariantCulture, $"{(xLeft):0.###}").Append(' ').Append(CultureInfo.InvariantCulture, $"{(yBottom):0.###}").Append(" m ")
                    .Append(CultureInfo.InvariantCulture, $"{(xRight):0.###}").Append(' ').Append(CultureInfo.InvariantCulture, $"{(yBottom):0.###}").Append(" l ");
        }
        output.Append("S Q\n");
        if (output.Length > maxOutputBytes)
            throw new FactsPdfException("FPDF1401", "PDF outline content exceeds MaxOutputBytes.");
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
            output.Append("q ").Append(CultureInfo.InvariantCulture, $"{(bg.R):0.###}").Append(' ').Append(CultureInfo.InvariantCulture, $"{(bg.G):0.###}").Append(' ')
                .Append(CultureInfo.InvariantCulture, $"{(bg.B):0.###}").Append(" rg ");
            RoundedPath(output, x, y, w, h, tl, tr, br, bl);
            output.Append("f Q\n");
            if (output.Length > maxOutputBytes)
                throw new FactsPdfException("FPDF1401", "PDF rounded background exceeds MaxOutputBytes.");
        }

        var border = style.BorderLeft.EffectiveWidth;
        if (border <= 0) return;
        var color = style.BorderLeft.Color ?? box.TextColor;
        output.Append("q ").Append(CultureInfo.InvariantCulture, $"{(color.R):0.###}").Append(' ').Append(CultureInfo.InvariantCulture, $"{(color.G):0.###}").Append(' ')
            .Append(CultureInfo.InvariantCulture, $"{(color.B):0.###}").Append(" rg ");
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
            throw new FactsPdfException("FPDF1401", "PDF rounded border exceeds MaxOutputBytes.");
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
        output.Append(CultureInfo.InvariantCulture, $"{(x + bl):0.###}").Append(' ').Append(CultureInfo.InvariantCulture, $"{(y):0.###}").Append(" m ");
        output.Append(CultureInfo.InvariantCulture, $"{(right - br):0.###}").Append(' ').Append(CultureInfo.InvariantCulture, $"{(y):0.###}").Append(" l ");
        if (br > 0)
            output.Append(CultureInfo.InvariantCulture, $"{(right - br + k * br):0.###}").Append(' ').Append(CultureInfo.InvariantCulture, $"{(y):0.###}").Append(' ')
                .Append(CultureInfo.InvariantCulture, $"{(right):0.###}").Append(' ').Append(CultureInfo.InvariantCulture, $"{(y + br - k * br):0.###}").Append(' ')
                .Append(CultureInfo.InvariantCulture, $"{(right):0.###}").Append(' ').Append(CultureInfo.InvariantCulture, $"{(y + br):0.###}").Append(" c ");
        else output.Append(CultureInfo.InvariantCulture, $"{(right):0.###}").Append(' ').Append(CultureInfo.InvariantCulture, $"{(y):0.###}").Append(" l ");
        output.Append(CultureInfo.InvariantCulture, $"{(right):0.###}").Append(' ').Append(CultureInfo.InvariantCulture, $"{(upper - tr):0.###}").Append(" l ");
        if (tr > 0)
            output.Append(CultureInfo.InvariantCulture, $"{(right):0.###}").Append(' ').Append(CultureInfo.InvariantCulture, $"{(upper - tr + k * tr):0.###}").Append(' ')
                .Append(CultureInfo.InvariantCulture, $"{(right - tr + k * tr):0.###}").Append(' ').Append(CultureInfo.InvariantCulture, $"{(upper):0.###}").Append(' ')
                .Append(CultureInfo.InvariantCulture, $"{(right - tr):0.###}").Append(' ').Append(CultureInfo.InvariantCulture, $"{(upper):0.###}").Append(" c ");
        else output.Append(CultureInfo.InvariantCulture, $"{(right):0.###}").Append(' ').Append(CultureInfo.InvariantCulture, $"{(upper):0.###}").Append(" l ");
        output.Append(CultureInfo.InvariantCulture, $"{(x + tl):0.###}").Append(' ').Append(CultureInfo.InvariantCulture, $"{(upper):0.###}").Append(" l ");
        if (tl > 0)
            output.Append(CultureInfo.InvariantCulture, $"{(x + tl - k * tl):0.###}").Append(' ').Append(CultureInfo.InvariantCulture, $"{(upper):0.###}").Append(' ')
                .Append(CultureInfo.InvariantCulture, $"{(x):0.###}").Append(' ').Append(CultureInfo.InvariantCulture, $"{(upper - tl + k * tl):0.###}").Append(' ')
                .Append(CultureInfo.InvariantCulture, $"{(x):0.###}").Append(' ').Append(CultureInfo.InvariantCulture, $"{(upper - tl):0.###}").Append(" c ");
        else output.Append(CultureInfo.InvariantCulture, $"{(x):0.###}").Append(' ').Append(CultureInfo.InvariantCulture, $"{(upper):0.###}").Append(" l ");
        output.Append(CultureInfo.InvariantCulture, $"{(x):0.###}").Append(' ').Append(CultureInfo.InvariantCulture, $"{(y + bl):0.###}").Append(" l ");
        if (bl > 0)
            output.Append(CultureInfo.InvariantCulture, $"{(x):0.###}").Append(' ').Append(CultureInfo.InvariantCulture, $"{(y + bl - k * bl):0.###}").Append(' ')
                .Append(CultureInfo.InvariantCulture, $"{(x + bl - k * bl):0.###}").Append(' ').Append(CultureInfo.InvariantCulture, $"{(y):0.###}").Append(' ')
                .Append(CultureInfo.InvariantCulture, $"{(x + bl):0.###}").Append(' ').Append(CultureInfo.InvariantCulture, $"{(y):0.###}").Append(" c ");
        else output.Append(CultureInfo.InvariantCulture, $"{(x):0.###}").Append(' ').Append(CultureInfo.InvariantCulture, $"{(y):0.###}").Append(" l ");
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
        output.Append("q ").Append(CultureInfo.InvariantCulture, $"{(color.R):0.###}").Append(' ').Append(CultureInfo.InvariantCulture, $"{(color.G):0.###}").Append(' ')
            .Append(CultureInfo.InvariantCulture, $"{(color.B):0.###}").Append(" RG ").Append(CultureInfo.InvariantCulture, $"{(stroke):0.###}").Append(" w ");
        if (edge.Pattern == BorderPattern.Dashed)
            output.Append('[').Append(CultureInfo.InvariantCulture, $"{(3 * stroke):0.###}").Append(' ').Append(CultureInfo.InvariantCulture, $"{(3 * stroke):0.###}").Append("] 0 d ");
        else if (edge.Pattern == BorderPattern.Dotted)
            output.Append("1 J [0.001 ").Append(CultureInfo.InvariantCulture, $"{(2 * stroke):0.###}").Append("] 0 d ");
        else
            throw new InvalidOperationException("Invalid patterned border kind.");
        // One clip per stroke, inside the q/Q state pair. This prevents both
        // dash fragments and round caps from painting outside the measured box.
        output.Append(CultureInfo.InvariantCulture, $"{(x):0.###}").Append(' ').Append(CultureInfo.InvariantCulture, $"{(pageHeight - top - height):0.###}")
            .Append(' ').Append(CultureInfo.InvariantCulture, $"{(width):0.###}").Append(' ').Append(CultureInfo.InvariantCulture, $"{(height):0.###}")
            .Append(" re W n ");
        output.Append(CultureInfo.InvariantCulture, $"{(x1):0.###}").Append(' ').Append(CultureInfo.InvariantCulture, $"{(y1):0.###}").Append(" m ")
            .Append(CultureInfo.InvariantCulture, $"{(x2):0.###}").Append(' ').Append(CultureInfo.InvariantCulture, $"{(y2):0.###}").Append(" l S Q\n");
        if (output.Length > maxOutputBytes)
            throw new FactsPdfException("FPDF1401", "PDF patterned border exceeds MaxOutputBytes.");
    }

    private static void Rect(StringBuilder output, Rgb color, double x, double top,
        double width, double height, double pageHeight, int maxOutputBytes)
    {
        if (width <= 0 || height <= 0) return;
        var y = pageHeight - top - height;
        output.Append("q ").Append(CultureInfo.InvariantCulture, $"{(color.R):0.###}").Append(' ').Append(CultureInfo.InvariantCulture, $"{(color.G):0.###}").Append(' ')
            .Append(CultureInfo.InvariantCulture, $"{(color.B):0.###}").Append(" rg ").Append(CultureInfo.InvariantCulture, $"{(x):0.###}").Append(' ').Append(CultureInfo.InvariantCulture, $"{(y):0.###}")
            .Append(' ').Append(CultureInfo.InvariantCulture, $"{(width):0.###}").Append(' ').Append(CultureInfo.InvariantCulture, $"{(height):0.###}")
            .Append(" re f Q\n");
        // Bound the temporary PDF content stream as rectangles are appended.
        // A command can exceed the limit by at most its own small encoding;
        // never build an unbounded string before the serializer rejects it.
        if (output.Length > maxOutputBytes)
            throw new FactsPdfException("FPDF1401", "PDF paint content exceeds MaxOutputBytes.");
    }

    private static string N(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
