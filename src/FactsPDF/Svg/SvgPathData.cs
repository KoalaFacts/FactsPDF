using System.Globalization;

namespace FactsPDF;

internal enum SvgSegmentKind : byte { Move, Line, Cubic, Close }

/// <summary>One absolute path segment in user units: a move or line to (X, Y), a cubic Bézier
/// with control points (X1, Y1) and (X2, Y2) ending at (X, Y), or a close of the subpath.</summary>
internal readonly record struct SvgSegment(SvgSegmentKind Kind, double X1, double Y1, double X2, double Y2, double X, double Y)
{
    internal static SvgSegment Move(double x, double y) => new(SvgSegmentKind.Move, 0, 0, 0, 0, x, y);
    internal static SvgSegment Line(double x, double y) => new(SvgSegmentKind.Line, 0, 0, 0, 0, x, y);
    internal static SvgSegment Cubic(double x1, double y1, double x2, double y2, double x, double y) =>
        new(SvgSegmentKind.Cubic, x1, y1, x2, y2, x, y);
    internal static readonly SvgSegment Close = new(SvgSegmentKind.Close, 0, 0, 0, 0, 0, 0);
}

/// <summary>
/// The SVG path data grammar (SVG 2, section 9.3.9): every command letter in absolute and relative
/// form, implicit command repeats (pairs after a moveto are linetos), the comma/whitespace rules,
/// numbers with fractions and exponents, and single-character arc flags. Quadratic curves become
/// cubic curves and elliptical arcs become at most four cubic curves per arc, so a painter only
/// needs PDF's m, l, c and h. Malformed data fails instead of rendering the valid prefix the way a
/// browser does: a typo in a logo should not silently drop half of the logo.
/// </summary>
internal static class SvgPathData
{
    internal static void Parse(string data, int offset, SvgLimits limits, List<SvgSegment> output)
    {
        var text = data.AsSpan();
        var i = 0;
        SkipSpace(text, ref i);
        if (i == text.Length || text[i..].SequenceEqual("none")) return;
        double x = 0, y = 0, startX = 0, startY = 0;
        // The previous segment's second control point for the S and T reflections, valid only
        // when the previous command was a cubic (C/S) or a quadratic (Q/T) respectively.
        double lastCubicX = 0, lastCubicY = 0, lastQuadX = 0, lastQuadY = 0;
        char previous = '\0';
        char command = '\0';
        var closed = false;
        while (true)
        {
            limits.Cancellation.ThrowIfCancellationRequested();
            SkipSpace(text, ref i);
            if (i == text.Length) break;
            var ch = text[i];
            if (char.IsAsciiLetter(ch))
            {
                command = ch;
                i++;
                if (previous == '\0' && command is not ('M' or 'm'))
                    throw Invalid("SVG path data must start with a moveto command.", offset, i - 1);
            }
            else if (command == '\0')
                throw Invalid("SVG path data must start with a moveto command.", offset, i);
            else if (command is 'Z' or 'z')
                throw Invalid("SVG path data has a number after closepath.", offset, i);
            else
            {
                // The same command repeats with the next argument set, which a comma may separate
                // from the previous one; pairs after a moveto are implicit linetos.
                if (ch == ',')
                {
                    i++;
                    SkipSpace(text, ref i);
                    if (i == text.Length || char.IsAsciiLetter(text[i]))
                        throw Invalid("SVG path data has a trailing comma.", offset, i);
                }
                if (previous is 'M') command = 'L';
                else if (previous is 'm') command = 'l';
            }
            var relative = char.IsAsciiLetterLower(command);
            var baseX = relative ? x : 0;
            var baseY = relative ? y : 0;
            // After a closepath, a command other than moveto starts its subpath at the closed
            // subpath's start (the current point); PDF needs that move written explicitly.
            if (closed && command is not ('M' or 'm' or 'Z' or 'z'))
                Add(output, limits, offset, SvgSegment.Move(x, y));
            closed = false;
            switch (char.ToUpperInvariant(command))
            {
                case 'M':
                    x = baseX + Number(text, ref i, offset);
                    y = baseY + Coordinate(text, ref i, offset);
                    startX = x; startY = y;
                    Add(output, limits, offset, SvgSegment.Move(x, y));
                    break;
                case 'L':
                    x = baseX + Number(text, ref i, offset);
                    y = baseY + Coordinate(text, ref i, offset);
                    Add(output, limits, offset, SvgSegment.Line(x, y));
                    break;
                case 'H':
                    x = baseX + Number(text, ref i, offset);
                    Add(output, limits, offset, SvgSegment.Line(x, y));
                    break;
                case 'V':
                    y = baseY + Number(text, ref i, offset);
                    Add(output, limits, offset, SvgSegment.Line(x, y));
                    break;
                case 'C':
                {
                    var x1 = baseX + Number(text, ref i, offset); var y1 = baseY + Coordinate(text, ref i, offset);
                    var x2 = baseX + Coordinate(text, ref i, offset); var y2 = baseY + Coordinate(text, ref i, offset);
                    x = baseX + Coordinate(text, ref i, offset); y = baseY + Coordinate(text, ref i, offset);
                    Add(output, limits, offset, SvgSegment.Cubic(x1, y1, x2, y2, x, y));
                    lastCubicX = x2; lastCubicY = y2;
                    break;
                }
                case 'S':
                {
                    var reflect = char.ToUpperInvariant(previous) is 'C' or 'S';
                    var x1 = reflect ? 2 * x - lastCubicX : x;
                    var y1 = reflect ? 2 * y - lastCubicY : y;
                    var x2 = baseX + Number(text, ref i, offset); var y2 = baseY + Coordinate(text, ref i, offset);
                    x = baseX + Coordinate(text, ref i, offset); y = baseY + Coordinate(text, ref i, offset);
                    Add(output, limits, offset, SvgSegment.Cubic(x1, y1, x2, y2, x, y));
                    lastCubicX = x2; lastCubicY = y2;
                    break;
                }
                case 'Q':
                {
                    var qx = baseX + Number(text, ref i, offset); var qy = baseY + Coordinate(text, ref i, offset);
                    var endX = baseX + Coordinate(text, ref i, offset); var endY = baseY + Coordinate(text, ref i, offset);
                    Quadratic(output, limits, offset, x, y, qx, qy, endX, endY);
                    x = endX; y = endY; lastQuadX = qx; lastQuadY = qy;
                    break;
                }
                case 'T':
                {
                    var reflect = char.ToUpperInvariant(previous) is 'Q' or 'T';
                    var qx = reflect ? 2 * x - lastQuadX : x;
                    var qy = reflect ? 2 * y - lastQuadY : y;
                    var endX = baseX + Number(text, ref i, offset); var endY = baseY + Coordinate(text, ref i, offset);
                    Quadratic(output, limits, offset, x, y, qx, qy, endX, endY);
                    x = endX; y = endY; lastQuadX = qx; lastQuadY = qy;
                    break;
                }
                case 'A':
                {
                    var rx = Math.Abs(Number(text, ref i, offset));
                    var ry = Math.Abs(Coordinate(text, ref i, offset));
                    var rotation = Coordinate(text, ref i, offset);
                    var large = Flag(text, ref i, offset);
                    var sweep = Flag(text, ref i, offset);
                    var endX = baseX + Coordinate(text, ref i, offset); var endY = baseY + Coordinate(text, ref i, offset);
                    Arc(output, limits, offset, x, y, rx, ry, rotation, large, sweep, endX, endY);
                    x = endX; y = endY;
                    break;
                }
                case 'Z':
                    Add(output, limits, offset, SvgSegment.Close);
                    x = startX; y = startY;
                    closed = true;
                    break;
                default:
                    throw Invalid($"SVG path command '{command}' is not defined.", offset, i - 1);
            }
            SvgLimits.CheckPoint(x, y, offset);
            previous = command;
        }
    }

    // The first number of a command's argument set: the command letter or the separator before
    // it has already been consumed, so no comma may lead.
    private static double Number(ReadOnlySpan<char> text, ref int i, int offset)
    {
        SkipSpace(text, ref i);
        return Scan(text, ref i, offset);
    }

    // Any later number of a set: an optional comma (with whitespace around it) may separate it.
    private static double Coordinate(ReadOnlySpan<char> text, ref int i, int offset)
    {
        SkipCommaSpace(text, ref i);
        return Scan(text, ref i, offset);
    }

    private static bool Flag(ReadOnlySpan<char> text, ref int i, int offset)
    {
        SkipCommaSpace(text, ref i);
        if (i < text.Length && text[i] is '0' or '1') return text[i++] == '1';
        throw Invalid("SVG arc flags must be 0 or 1.", offset, i);
    }

    /// <summary>Scans one SVG number (sign, digits, optional fraction, optional exponent) at
    /// <paramref name="i"/>; a following comma is left for the next separator.</summary>
    internal static double Scan(ReadOnlySpan<char> text, ref int i, int offset)
    {
        var start = i;
        if (i < text.Length && text[i] is '+' or '-') i++;
        var digits = 0;
        while (i < text.Length && char.IsAsciiDigit(text[i])) { i++; digits++; }
        if (i < text.Length && text[i] == '.')
        {
            i++;
            while (i < text.Length && char.IsAsciiDigit(text[i])) { i++; digits++; }
        }
        if (digits == 0) throw Invalid("SVG path data expects a number.", offset, start);
        // An exponent needs at least one digit; "1e" or "2em" leaves the letter as a command.
        if (i < text.Length && text[i] is 'e' or 'E')
        {
            var mark = i + 1;
            if (mark < text.Length && text[mark] is '+' or '-') mark++;
            if (mark < text.Length && char.IsAsciiDigit(text[mark]))
            {
                i = mark;
                while (i < text.Length && char.IsAsciiDigit(text[i])) i++;
            }
        }
        if (i - start > 64) throw Invalid("SVG number is too long.", offset, start);
        if (!double.TryParse(text[start..i], NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            throw Invalid("SVG path data has a malformed number.", offset, start);
        SvgLimits.CheckNumber(value, offset);
        return value;
    }

    internal static void SkipSpace(ReadOnlySpan<char> text, ref int i)
    {
        while (i < text.Length && text[i] is ' ' or '\t' or '\r' or '\n' or '\f') i++;
    }

    private static void SkipCommaSpace(ReadOnlySpan<char> text, ref int i)
    {
        SkipSpace(text, ref i);
        if (i < text.Length && text[i] == ',') { i++; SkipSpace(text, ref i); }
    }

    private static void Add(List<SvgSegment> output, SvgLimits limits, int offset, SvgSegment segment)
    {
        limits.Command(offset);
        // Arc and quadratic control points are derived; they must stay finite like every end point.
        if (segment.Kind == SvgSegmentKind.Cubic && !double.IsFinite(segment.X1 + segment.Y1 + segment.X2 + segment.Y2))
            throw new FactsPdfException("FPDF1103", "SVG curve geometry is outside the supported range.", offset);
        output.Add(segment);
    }

    // The degree-elevated cubic of a quadratic Bézier: each control point lies two thirds of the
    // way from an end point to the quadratic control point.
    private static void Quadratic(List<SvgSegment> output, SvgLimits limits, int offset,
        double x0, double y0, double qx, double qy, double x, double y) =>
        Add(output, limits, offset, SvgSegment.Cubic(x0 + 2d / 3 * (qx - x0), y0 + 2d / 3 * (qy - y0),
            x + 2d / 3 * (qx - x), y + 2d / 3 * (qy - y), x, y));

    /// <summary>
    /// An elliptical arc by the endpoint-to-centre conversion of SVG 2 appendix B.2.4, with the
    /// out-of-range corrections of B.2.5: identical end points draw nothing, a zero radius draws a
    /// line, and radii too small to reach the end point are scaled up uniformly. The sweep is
    /// split into pieces of at most 90 degrees, each approximated by one cubic Bézier.
    /// </summary>
    internal static void Arc(List<SvgSegment> output, SvgLimits limits, int offset, double x0, double y0,
        double rx, double ry, double rotationDegrees, bool large, bool sweep, double x, double y)
    {
        if (x0 == x && y0 == y) return;
        if (rx == 0 || ry == 0)
        {
            Add(output, limits, offset, SvgSegment.Line(x, y));
            return;
        }
        var phi = rotationDegrees % 360 * Math.PI / 180;
        var cos = Math.Cos(phi);
        var sin = Math.Sin(phi);
        var dx = (x0 - x) / 2;
        var dy = (y0 - y) / 2;
        var x1 = cos * dx + sin * dy;
        var y1 = -sin * dx + cos * dy;
        var lambda = x1 * x1 / (rx * rx) + y1 * y1 / (ry * ry);
        if (lambda > 1)
        {
            var scale = Math.Sqrt(lambda);
            rx *= scale;
            ry *= scale;
        }
        var numerator = rx * rx * ry * ry - rx * rx * y1 * y1 - ry * ry * x1 * x1;
        var denominator = rx * rx * y1 * y1 + ry * ry * x1 * x1;
        var factor = Math.Sqrt(Math.Max(0, numerator / denominator));
        if (large == sweep) factor = -factor;
        var cx1 = factor * rx * y1 / ry;
        var cy1 = -factor * ry * x1 / rx;
        var cx = cos * cx1 - sin * cy1 + (x0 + x) / 2;
        var cy = sin * cx1 + cos * cy1 + (y0 + y) / 2;
        var start = Angle(1, 0, (x1 - cx1) / rx, (y1 - cy1) / ry);
        var delta = Angle((x1 - cx1) / rx, (y1 - cy1) / ry, (-x1 - cx1) / rx, (-y1 - cy1) / ry);
        if (!sweep && delta > 0) delta -= 2 * Math.PI;
        else if (sweep && delta < 0) delta += 2 * Math.PI;
        var pieces = (int)Math.Ceiling(Math.Abs(delta) / (Math.PI / 2) - 1e-9);
        if (pieces < 1) pieces = 1;
        var step = delta / pieces;
        // Control point distance along the tangent for a unit circular arc of angle `step`.
        var k = 4d / 3 * Math.Tan(step / 4);
        var angle = start;
        for (var piece = 0; piece < pieces; piece++)
        {
            var a1 = angle;
            var a2 = angle + step;
            var (c1, s1) = (Math.Cos(a1), Math.Sin(a1));
            var (c2, s2) = (Math.Cos(a2), Math.Sin(a2));
            // Unit-circle points and tangents, then the ellipse's radii, rotation and centre.
            var (p1x, p1y) = Map(c1 - k * s1, s1 + k * c1);
            var (p2x, p2y) = Map(c2 + k * s2, s2 - k * c2);
            var (ex, ey) = piece == pieces - 1 ? (x, y) : Map(c2, s2);
            Add(output, limits, offset, SvgSegment.Cubic(p1x, p1y, p2x, p2y, ex, ey));
            angle = a2;
        }

        (double X, double Y) Map(double ux, double uy)
        {
            var px = rx * ux;
            var py = ry * uy;
            return (cos * px - sin * py + cx, sin * px + cos * py + cy);
        }
    }

    // The signed angle from vector u to vector v.
    private static double Angle(double ux, double uy, double vx, double vy)
    {
        var dot = ux * vx + uy * vy;
        var length = Math.Sqrt((ux * ux + uy * uy) * (vx * vx + vy * vy));
        var angle = Math.Acos(Math.Clamp(dot / length, -1, 1));
        return ux * vy - uy * vx < 0 ? -angle : angle;
    }

    private static FactsPdfException Invalid(string message, int offset, int position) =>
        new("FPDF1103", $"{message} (path data character {position}).", offset);
}
