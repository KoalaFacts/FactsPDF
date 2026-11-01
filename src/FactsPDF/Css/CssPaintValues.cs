namespace FactsPDF;

/// <summary>Strict CSS paint semantics; neither parser nor layout loads external resources.</summary>
internal static class CssPaintValues
{
    internal static Rgb? Background(string value, int offset)
        => value == "transparent" ? null : InlineCss.Color(value, offset);

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

    internal static bool TryBorderWidth(string value, out double width)
    {
        width = 0;
        if (value is "thin" or "medium" or "thick")
        { width = value == "thin" ? 0.75 : value == "medium" ? 2.25 : 3.75; return true; }
        try
        {
            if (value.EndsWith('%')) return false;
            var x = CssBoxValues.Length(value, 0);
            if (x.IsPercent) return false;
            width = x.Value;
            return true;
        }
        catch (FactsPdfException) { return false; }
    }

    internal static double BorderWidth(string value, int offset)
    {
        if (!TryBorderWidth(value, out var result))
            throw new FactsPdfException("FPDF1202", "Border width must be a nonnegative pt/px/mm/cm/in length or thin/medium/thick.", offset);
        return result;
    }
}
