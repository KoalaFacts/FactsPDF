namespace FactsPDF;

/// <summary>Strict CSS paint semantics; neither parser nor layout loads external resources.</summary>
internal static class CssPaintValues
{
    internal static Rgb? Background(string value, int offset, Rgb? currentColor = null)
        => value switch
        {
            "transparent" => null,
            // Validation runs without a computed style; the winning box
            // declaration supplies the element's final text colour later.
            "currentcolor" => currentColor,
            _ => InlineCss.Color(value, offset)
        };

    // Each physical corner radius is zero or a visible 0.25–72pt length.
    // No percentages, elliptical slash syntax or font-relative units.
    internal static double BorderRadius(string value, int offset)
    {
        var length = CssBoxValues.Length(value, offset);
        if (length.IsPercent || length.Value > 72 || length.Value is > 0 and < 0.25)
            throw new FactsPdfException("FPDF1202",
                "border-radius requires zero or a 0.25pt–72pt physical length; percentages are unsupported.", offset);
        return length.Value;
    }

    // One-to-four rectangular radii: TL, TR, BR, BL, in the legacy
    // shorthand-compatible order. No elliptical radius syntax.
    internal static (double TopLeft, double TopRight, double BottomRight, double BottomLeft)
        BorderRadii(string value, int offset)
    {
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length is < 1 or > 4)
            throw new FactsPdfException("FPDF1202",
                "border-radius takes one to four physical corner lengths.", offset);
        var a = BorderRadius(parts[0], offset);
        var b = parts.Length >= 2 ? BorderRadius(parts[1], offset) : a;
        var c = parts.Length >= 3 ? BorderRadius(parts[2], offset) : a;
        var d = parts.Length == 4 ? BorderRadius(parts[3], offset) : b;
        return (a, b, c, d);
    }

    internal static Rgb? BorderColor(string value, int offset)
        => value == "currentcolor" ? null : InlineCss.Color(value, offset);

    internal static BorderPattern BorderStyle(string value, int offset) => value switch
    {
        "solid" or "none" => BorderPattern.Solid,
        "dashed" => BorderPattern.Dashed,
        "dotted" => BorderPattern.Dotted,
        _ => throw new FactsPdfException("FPDF1202",
            "Only solid, none, dashed and dotted border styles are supported.", offset)
    };

    internal static bool TryBorderWidth(string value, out double width, bool allowRelative = false,
        double? rootFontSize = null, double? elementFontSize = null)
    {
        width = 0;
        if (value is "thin" or "medium" or "thick")
        { width = value == "thin" ? 0.75 : value == "medium" ? 2.25 : 3.75; return true; }
        // Syntax validation may accept a bounded relative length without a
        // computed font. Only the winning border declaration gets the final
        // root/element font scale; outlines deliberately remain physical.
        if (value.EndsWith('%') || !CssBoxValues.TryLength(value, out var x, out _,
            rootFontSize, allowRem: allowRelative, elementFontSize: elementFontSize,
            allowEm: allowRelative) || x.IsPercent) return false;
        width = x.Value;
        return true;
    }

    // Outlines use bounded visible widths and do not alter the box model.
    internal static double OutlineWidth(string value, int offset)
    {
        var width = BorderWidth(value, offset);
        if (width > 6 || width > 0 && width < 0.2)
            throw new FactsPdfException("FPDF1202",
                "Outline width must be zero or from 0.2pt to 6pt.", offset);
        return width;
    }

    internal static double OutlineOffset(string value, int offset)
    {
        var length = CssBoxValues.Length(value, offset);
        if (length.IsPercent || length.Value > 12)
            throw new FactsPdfException("FPDF1202",
                "Outline offset must be a nonnegative physical length no larger than 12pt.", offset);
        return length.Value;
    }

    internal static double BorderWidth(string value, int offset, bool allowRelative = false,
        double? rootFontSize = null, double? elementFontSize = null)
    {
        if (!TryBorderWidth(value, out var result, allowRelative, rootFontSize, elementFontSize))
            throw new FactsPdfException("FPDF1202",
                "Border width must be a bounded nonnegative print length, scoped em/rem, or thin/medium/thick.", offset);
        return result;
    }
}
