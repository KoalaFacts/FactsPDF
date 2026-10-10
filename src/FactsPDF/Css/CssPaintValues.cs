namespace FactsPDF;

/// <summary>Strict CSS paint semantics; neither parser nor layout loads external resources.</summary>
internal static class CssPaintValues
{
    internal static Rgb? Background(string value, int offset)
        => value == "transparent" ? null : InlineCss.Color(value, offset);

    internal static Rgb? BorderColor(string value, int offset)
        => value == "currentcolor" ? null : InlineCss.Color(value, offset);

    internal static bool BorderStyle(string value, int offset) => value switch
    {
        "solid" => true, "none" => false,
        _ => throw new FactsPdfException("FPDF1202", "Only solid and none border styles are supported.", offset)
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
            throw new FactsPdfException("FPDF1202", "Border width must be a nonnegative pt/px length or thin/medium/thick.", offset);
        return result;
    }
}
