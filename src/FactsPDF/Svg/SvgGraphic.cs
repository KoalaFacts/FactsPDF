namespace FactsPDF;

/// <summary>An affine map x' = A x + C y + E, y' = B x + D y + F, as SVG transform lists and the
/// PDF <c>cm</c> operator write it.</summary>
internal readonly record struct SvgMatrix(double A, double B, double C, double D, double E, double F)
{
    internal static readonly SvgMatrix Identity = new(1, 0, 0, 1, 0, 0);

    internal bool IsIdentity => this == Identity;

    /// <summary>This map applied after <paramref name="inner"/>: (this * inner)(p) = this(inner(p)).</summary>
    internal SvgMatrix Then(SvgMatrix inner) => new(
        A * inner.A + C * inner.B, B * inner.A + D * inner.B,
        A * inner.C + C * inner.D, B * inner.C + D * inner.D,
        A * inner.E + C * inner.F + E, B * inner.E + D * inner.F + F);

    internal (double X, double Y) Apply(double x, double y) => (A * x + C * y + E, B * x + D * y + F);
}

internal enum SvgFillRule : byte { NonZero, EvenOdd }

/// <summary>A clipPath's geometry: its one child shape's path in the clipPath's own coordinates
/// (the clipPath and child transforms applied through <see cref="Local"/>). An empty clipPath
/// has no path and clips everything away.</summary>
internal sealed record SvgClip(SvgSegment[] Path, SvgFillRule Rule, SvgMatrix Local);

/// <summary>A clip-path reference made by an element or one of its ancestors, with the user
/// space of the referencing element; chained outward to the ancestors' references.</summary>
internal sealed record SvgClipReference(string Id, SvgMatrix Space, SvgClipReference? Outer);

/// <summary>One painted shape: its path in its own user space, the map to the outer svg's user
/// space, its resolved paint and the clip references in force.</summary>
internal sealed class SvgShape
{
    internal required SvgSegment[] Path { get; init; }
    internal required SvgMatrix Transform { get; init; }
    internal Rgb? Fill { get; init; }
    internal SvgFillRule FillRule { get; init; }
    internal Rgb? Stroke { get; init; }
    internal double StrokeWidth { get; init; }
    internal byte LineCap { get; init; }
    internal byte LineJoin { get; init; }
    internal double MiterLimit { get; init; }
    internal SvgClipReference? Clips { get; init; }
    internal int SourceOffset { get; init; }
    // Set when the document's clip references are resolved: the clipPaths found, each with the
    // map from its coordinates to the outer svg's user space. A missing reference clips nothing.
    internal (SvgClip Clip, SvgMatrix ToRoot)[] ResolvedClips { get; set; } = [];
    // False when a resolved clipPath is empty, so nothing of the shape can show.
    internal bool Visible { get; set; } = true;
}

/// <summary>
/// An inline svg element read into vector shapes. Its width/height attributes are presentational
/// hints (CSS px converted to points) applied when author CSS sets no width or height; the viewBox
/// gives the intrinsic aspect ratio and, with preserveAspectRatio, the map from user units to the
/// content box.
/// </summary>
internal sealed class SvgGraphic
{
    internal CssLength? WidthHint { get; init; }
    internal double? HeightHint { get; init; }
    internal (double X, double Y, double Width, double Height)? ViewBox { get; init; }
    internal SvgAspectAlign Align { get; init; } = SvgAspectAlign.XMidYMid;
    internal bool Slice { get; init; }
    internal required SvgShape[] Shapes { get; init; }
    // Display commands painting the graphic costs: one per path segment and one per painted
    // shape, clip paths included. Final once clip references are resolved.
    internal int CommandCount { get; set; }

    /// <summary>The intrinsic ratio's width term: the viewBox width, else the width hint.</summary>
    internal double RatioWidth => ViewBox?.Width ?? WidthHint?.Value ?? 300;
    /// <summary>The intrinsic ratio's height term: the viewBox height, else the height hint.</summary>
    internal double RatioHeight => ViewBox?.Height ?? HeightHint ?? 150;

    /// <summary>The map from the svg's user space to PDF page space for a content box whose
    /// top-left corner is (x, top) in layout coordinates.</summary>
    internal SvgMatrix Placement(double x, double top, double width, double height, double pageHeight)
    {
        double scaleX = 0.75, scaleY = 0.75, minX = 0, minY = 0, offsetX = 0, offsetY = 0;
        if (ViewBox is { } box)
        {
            minX = box.X; minY = box.Y;
            scaleX = width / box.Width;
            scaleY = height / box.Height;
            if (Align != SvgAspectAlign.None)
            {
                var uniform = Slice ? Math.Max(scaleX, scaleY) : Math.Min(scaleX, scaleY);
                scaleX = scaleY = uniform;
                var (alignX, alignY) = Align.Factors();
                offsetX = (width - box.Width * uniform) * alignX;
                offsetY = (height - box.Height * uniform) * alignY;
            }
        }
        // Layout y grows downward from the page top; PDF y grows upward from the page bottom.
        return new(scaleX, 0, 0, -scaleY, x + offsetX - minX * scaleX, pageHeight - top - offsetY + minY * scaleY);
    }
}

internal enum SvgAspectAlign : byte
{
    None, XMinYMin, XMidYMin, XMaxYMin, XMinYMid, XMidYMid, XMaxYMid, XMinYMax, XMidYMax, XMaxYMax
}

internal static class ExtensionsToSvgAspectAlign
{
    internal static (double X, double Y) Factors(this SvgAspectAlign align) => align switch
    {
        SvgAspectAlign.XMinYMin => (0, 0),
        SvgAspectAlign.XMidYMin => (0.5, 0),
        SvgAspectAlign.XMaxYMin => (1, 0),
        SvgAspectAlign.XMinYMid => (0, 0.5),
        SvgAspectAlign.XMaxYMid => (1, 0.5),
        SvgAspectAlign.XMinYMax => (0, 1),
        SvgAspectAlign.XMidYMax => (0.5, 1),
        SvgAspectAlign.XMaxYMax => (1, 1),
        _ => (0.5, 0.5),
    };
}

/// <summary>
/// The bounds every inline svg is read within: a display-command budget shared by the whole
/// document (each path segment and painted shape is one PDF display command, failing FPDF1401 as
/// the page-level count would, but before layout), and a magnitude bound on every number so that
/// no coordinate, radius or transform can produce non-finite or unbounded PDF numbers (FPDF1103).
/// </summary>
internal sealed class SvgLimits(int maxCommands, CancellationToken cancellation)
{
    /// <summary>The largest magnitude accepted for any SVG number, in user units.</summary>
    internal const double MaxNumber = 1_000_000;

    internal CancellationToken Cancellation { get; } = cancellation;
    internal int Commands { get; private set; }

    internal void Command(int offset, int count = 1)
    {
        if ((long)Commands + count > maxCommands)
            throw new FactsPdfException("FPDF1401", "SVG content exceeds the PDF display command limit.", offset);
        Commands += count;
    }

    internal static void CheckNumber(double value, int offset)
    {
        if (!double.IsFinite(value) || Math.Abs(value) > MaxNumber)
            throw new FactsPdfException("FPDF1103", "SVG number is outside the supported range of -1000000 to 1000000.", offset);
    }

    internal static void CheckPoint(double x, double y, int offset)
    {
        CheckNumber(x, offset);
        CheckNumber(y, offset);
    }

    internal static void CheckMatrix(SvgMatrix m, int offset)
    {
        CheckNumber(m.A, offset); CheckNumber(m.B, offset); CheckNumber(m.C, offset);
        CheckNumber(m.D, offset); CheckNumber(m.E, offset); CheckNumber(m.F, offset);
    }
}
