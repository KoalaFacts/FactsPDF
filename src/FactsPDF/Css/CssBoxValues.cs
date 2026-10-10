using System.Globalization;

namespace FactsPDF;

/// <summary>Strict bounded CSS width/padding values, independent of culture and browser APIs.</summary>
internal static class CssBoxValues
{
    internal static CssLength? Width(string value, int offset)
        => value == "auto" ? null : Length(value, offset);

    internal static CssLength Length(string value, int offset)
    {
        if (value == "0") return new CssLength(0);
        double scale;
        bool percent;
        string number;
        if (value.EndsWith("pt", StringComparison.Ordinal))
        { number = value[..^2]; scale = 1; percent = false; }
        else if (value.EndsWith("px", StringComparison.Ordinal))
        { number = value[..^2]; scale = 0.75; percent = false; }
        else if (value.EndsWith('%'))
        { number = value[..^1]; scale = 1; percent = true; }
        else throw new FactsPdfException("FPDF1202", "Width and padding require pt, px, %, or unitless zero.", offset);

        if (!double.TryParse(number, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out var parsed) || !double.IsFinite(parsed))
            throw new FactsPdfException("FPDF1202", "Box length must be a finite decimal.", offset);
        var length = parsed * scale;
        if (!double.IsFinite(length) || length < 0 || length > (percent ? 10_000 : 14_400))
            throw new FactsPdfException("FPDF1202", "Box length is outside the bounded nonnegative range.", offset);
        return new CssLength(length, percent);
    }
}
