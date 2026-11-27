using System.Text;

namespace FactsPDF;

/// <summary>
/// Writes an inline svg into the page content stream as PDF vector operators: a clip to the
/// content box (an outer svg clips its overflow), the viewBox map, then each shape as path
/// construction operators painted with f/f* (fill), S (stroke) or B/B* (fill then stroke, SVG's
/// default paint order) inside its own q/Q with its clip paths and transform.
/// </summary>
internal static class SvgPainter
{
    private const string Matrix = "0.######";
    private const string Coordinate = "0.####";

    internal static void Append(StringBuilder content, SvgGraphic graphic, PlacedImage placed, PdfOptions options)
    {
        var offset = graphic.Shapes.Length > 0 ? graphic.Shapes[0].SourceOffset : -1;
        content.Append("q ").AppendInvariant(placed.X, Matrix).Append(' ')
            .AppendInvariant(options.PageHeight - placed.Top - placed.Height, Matrix).Append(' ')
            .AppendInvariant(placed.Width, Matrix).Append(' ').AppendInvariant(placed.Height, Matrix).Append(" re W n ");
        AppendMatrix(content, graphic.Placement(placed.X, placed.Top, placed.Width, placed.Height, options.PageHeight));
        content.Append(" cm\n");
        foreach (var shape in graphic.Shapes)
        {
            if (!shape.Visible) continue;
            content.Append('q');
            foreach (var (clip, toRoot) in shape.ResolvedClips)
            {
                AppendPath(content, clip.Path, toRoot);
                content.Append(clip.Rule == SvgFillRule.EvenOdd ? " W* n" : " W n");
            }
            if (!shape.Transform.IsIdentity)
            {
                content.Append(' ');
                AppendMatrix(content, shape.Transform);
                content.Append(" cm");
            }
            if (shape.Fill is { } fill) AppendColor(content, fill).Append(" rg");
            if (shape.Stroke is { } stroke)
            {
                AppendColor(content, stroke).Append(" RG ").AppendInvariant(shape.StrokeWidth, Coordinate).Append(" w");
                if (shape.LineCap != 0) content.Append(' ').Append(shape.LineCap).Append(" J");
                if (shape.LineJoin != 0) content.Append(' ').Append(shape.LineJoin).Append(" j");
                content.Append(' ').AppendInvariant(shape.MiterLimit, Coordinate).Append(" M");
            }
            AppendPath(content, shape.Path, SvgMatrix.Identity);
            var evenOdd = shape.FillRule == SvgFillRule.EvenOdd;
            content.Append(shape.Fill is null ? " S" : shape.Stroke is null ? evenOdd ? " f*" : " f" : evenOdd ? " B*" : " B");
            content.Append(" Q\n");
            if (content.Length > options.MaxOutputBytes)
                throw new FactsPdfException("FPDF1401", "PDF svg content exceeds MaxOutputBytes.", shape.SourceOffset);
        }
        content.Append("Q\n");
        if (content.Length > options.MaxOutputBytes)
            throw new FactsPdfException("FPDF1401", "PDF svg content exceeds MaxOutputBytes.", offset);
    }

    private static void AppendMatrix(StringBuilder content, SvgMatrix m) =>
        content.AppendInvariant(m.A, Matrix).Append(' ').AppendInvariant(m.B, Matrix).Append(' ')
            .AppendInvariant(m.C, Matrix).Append(' ').AppendInvariant(m.D, Matrix).Append(' ')
            .AppendInvariant(m.E, Matrix).Append(' ').AppendInvariant(m.F, Matrix);

    private static StringBuilder AppendColor(StringBuilder content, Rgb color) =>
        content.Append(' ').AppendInvariant(color.R, Coordinate).Append(' ').AppendInvariant(color.G, Coordinate)
            .Append(' ').AppendInvariant(color.B, Coordinate);

    // Path construction operators; the map places clip geometry in the outer svg's user space.
    private static void AppendPath(StringBuilder content, SvgSegment[] path, SvgMatrix map)
    {
        foreach (var segment in path)
        {
            switch (segment.Kind)
            {
                case SvgSegmentKind.Move:
                    Point(content, map, segment.X, segment.Y).Append(" m");
                    break;
                case SvgSegmentKind.Line:
                    Point(content, map, segment.X, segment.Y).Append(" l");
                    break;
                case SvgSegmentKind.Cubic:
                    Point(content, map, segment.X1, segment.Y1);
                    Point(content, map, segment.X2, segment.Y2);
                    Point(content, map, segment.X, segment.Y).Append(" c");
                    break;
                default:
                    content.Append(" h");
                    break;
            }
        }
    }

    private static StringBuilder Point(StringBuilder content, SvgMatrix map, double x, double y)
    {
        var (px, py) = map.Apply(x, y);
        return content.Append(' ').AppendInvariant(px, Coordinate).Append(' ').AppendInvariant(py, Coordinate);
    }
}
