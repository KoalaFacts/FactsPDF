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
        var width = style.Width is { } specified ? Resolve(specified) : image.Image.PixelWidth * 0.75;
        var height = Checked(width * image.Image.PixelHeight / image.Image.PixelWidth, image.SourceOffset);
        var outerWidth = Checked(width + left + right, image.SourceOffset);
        var outerHeight = Checked(height + top + bottom, image.SourceOffset);
        if (width <= 0 || outerWidth > containingWidth + 0.000001)
            throw new FactsPdfException("FPDF1302", "Image outer width exceeds containing content width or is zero.", image.SourceOffset);
        return new(left, top, width, height, outerWidth, outerHeight);
    }

    private static double Checked(double value, int offset)
    {
        if (!double.IsFinite(value) || value < 0 || value > 14_400)
            throw new FactsPdfException("FPDF1302", "Resolved box length exceeds supported bounds.", offset);
        return value;
    }
}
