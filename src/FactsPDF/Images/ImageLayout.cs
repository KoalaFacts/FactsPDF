namespace FactsPDF;

/// <summary>Shared image content and outer-box geometry; all percentages use containing width.</summary>
internal static class ImageLayout
{
    internal readonly record struct Geometry(double Left, double Top, double Width, double Height,
        double OuterWidth, double OuterHeight);

    internal static Geometry Resolve(ImageNode image, double containingWidth)
    {
        var style = image.BoxStyle;
        double Resolve(CssLength length) => Checked(length.Resolve(containingWidth), image.SourceOffset);
        var left = Resolve(style.PaddingLeft) + style.BorderLeft.EffectiveWidth;
        var right = Resolve(style.PaddingRight) + style.BorderRight.EffectiveWidth;
        var top = Resolve(style.PaddingTop) + style.BorderTop.EffectiveWidth;
        var bottom = Resolve(style.PaddingBottom) + style.BorderBottom.EffectiveWidth;
        // A direct image retains its pixel aspect ratio. The used width is first
        // chosen from its natural or specified measure, then bounded in the
        // declared box-sizing space. min-width wins over max-width, as for
        // report panels. Percentages always use the actual containing width,
        // including the combined track width for a colspan image cell.
        var edges = Checked(left + right, image.SourceOffset);
        var verticalEdges = Checked(top + bottom, image.SourceOffset);
        // A physical CSS height is an actual image-matrix dimension, not a
        // syntactic hint. With auto width, derive width from the pixel ratio;
        // with two explicit dimensions, the PDF image is deliberately stretched.
        // Width bounds remain independent when height is explicitly fixed.
        var fixedHeight = style.Height is { } specifiedHeight
            ? Checked(Resolve(specifiedHeight) - (style.BorderBoxSizing ? verticalEdges : 0),
                image.SourceOffset)
            : (double?)null;
        var width = style.Width is { } specified
            ? Resolve(specified) - (style.BorderBoxSizing ? edges : 0)
            : fixedHeight is { } h
                ? h * image.Image.PixelWidth / image.Image.PixelHeight // clamp after min/max-width
                : image.Image.PixelWidth * 0.75;
        var measure = style.BorderBoxSizing ? width + edges : width;
        if (style.MaxWidth is { } max) measure = Math.Min(measure, Resolve(max));
        if (style.MinWidth is { } min) measure = Math.Max(measure, Resolve(min));
        width = Checked(measure - (style.BorderBoxSizing ? edges : 0), image.SourceOffset);
        // A vertical limit measures the same content-box/border-box space as
        // height. With auto height, limit the PDF matrix proportionally on
        // both axes; with explicit height, preserve the independent width.
        // The minimum wins over a contradictory maximum, as for width.
        var unconstrainedHeight = fixedHeight ?? width * image.Image.PixelHeight / image.Image.PixelWidth;
        var heightMeasure = unconstrainedHeight + (style.BorderBoxSizing ? verticalEdges : 0);
        if (style.MaxHeight is { } maxHeight)
            heightMeasure = Math.Min(heightMeasure, Resolve(maxHeight));
        if (style.MinHeight is { } minHeight)
            heightMeasure = Math.Max(heightMeasure, Resolve(minHeight));
        // Preserve the legacy height:auto computation byte-for-byte when no
        // vertical bound is present; border-box arithmetic can otherwise
        // introduce a rounding difference in every existing image PDF.
        var height = style.MinHeight.HasValue || style.MaxHeight.HasValue
            ? Checked(heightMeasure - (style.BorderBoxSizing ? verticalEdges : 0), image.SourceOffset)
            : Checked(unconstrainedHeight, image.SourceOffset);
        if (fixedHeight is null && (style.MinHeight.HasValue || style.MaxHeight.HasValue)
            && Math.Abs(height - unconstrainedHeight) > 0.00000001)
        {
            // Height:auto preserves pixel aspect even after a CSS width cap.
            // Reject mutually unsatisfiable min/max-width versus height bounds
            // rather than silently throwing away either author's constraint.
            width = Checked(height * image.Image.PixelWidth / image.Image.PixelHeight, image.SourceOffset);
            var resultingWidth = width + (style.BorderBoxSizing ? edges : 0);
            // The existing width clamp applies max first, then min: when
            // min-width exceeds max-width the effective upper bound is the
            // minimum, not infinity. Otherwise vertical constraints would
            // silently override both specified horizontal limits.
            var minBound = style.MinWidth is { } minWidth ? Resolve(minWidth) : 0;
            var maxBound = style.MaxWidth is { } maxWidth
                ? Math.Max(Resolve(maxWidth), minBound)
                : double.PositiveInfinity;
            if (resultingWidth + 0.000001 < minBound ||
                resultingWidth > maxBound + 0.000001)
                throw new FactsPdfException("FPDF1302",
                    "Image height and width constraints cannot preserve the intrinsic aspect ratio.", image.SourceOffset);
        }
        var outerWidth = Checked(width + edges, image.SourceOffset);
        var outerHeight = Checked(height + verticalEdges, image.SourceOffset);
        if (width <= 0 || height <= 0 || outerWidth > containingWidth + 0.000001)
            throw new FactsPdfException("FPDF1302", "Image content dimensions must be positive and the outer width must fit its containing content.", image.SourceOffset);
        return new(left, top, width, height, outerWidth, outerHeight);
    }

    private static double Checked(double value, int offset)
    {
        if (!double.IsFinite(value) || value < 0 || value > 14_400)
            throw new FactsPdfException("FPDF1302", "Resolved box length exceeds supported bounds.", offset);
        return value;
    }
}
