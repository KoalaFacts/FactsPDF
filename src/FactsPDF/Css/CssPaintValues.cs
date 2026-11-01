namespace FactsPDF;

/// <summary>Strict CSS paint semantics; neither parser nor layout loads external resources.</summary>
internal static class CssPaintValues
{
    internal static Rgb? Background(string value, int offset)
        => value == "transparent" ? null : InlineCss.Color(value, offset);

    // One physical radius for all four corners, 0 or a visible 0.25–72pt
    // length. No percentages/ellipses/per-corner radii are falsely accepted.
    internal static double BorderRadius(string value, int offset)
    {
        var length = CssBoxValues.Length(value, offset);
        if (length.IsPercent || length.Value > 72 || length.Value is > 0 and < 0.25)
            throw new FactsPdfException("FPDF1202",
                "border-radius requires zero or a 0.25pt–72pt physical length; percentages are unsupported.", offset);
        return length.Value;
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
