using System.Globalization;

namespace FactsPDF;

/// <summary>Strict bounded CSS report box lengths, independent of culture and browser APIs.</summary>
internal static class CssBoxValues
{
    // rem uses the computed html root; em uses the target element's own
    // computed font size after cascade. Syntax checks leave scales null;
    // unsupported contexts retain fail-closed defaults.
    internal static CssLength? Width(string value, int offset, double? rootFontSize = null,
        double? elementFontSize = null)
        => value == "auto" ? null : Length(value, offset, rootFontSize, allowRem: true,
            elementFontSize: elementFontSize, allowEm: true);

    internal static CssLength Length(string value, int offset,
        double? rootFontSize = null, bool allowRem = false,
        double? elementFontSize = null, bool allowEm = false)
    {
        if (value == "0") return new CssLength(0);
        double scale;
        bool percent;
        string number;
        if (value.EndsWith("pt", StringComparison.Ordinal))
        { number = value[..^2]; scale = 1; percent = false; }
        else if (value.EndsWith("px", StringComparison.Ordinal))
        { number = value[..^2]; scale = 0.75; percent = false; }
        else if (value.EndsWith("mm", StringComparison.Ordinal))
        { number = value[..^2]; scale = 72d / 25.4d; percent = false; }
        else if (value.EndsWith("cm", StringComparison.Ordinal))
        { number = value[..^2]; scale = 72d / 2.54d; percent = false; }
        else if (value.EndsWith("in", StringComparison.Ordinal))
        { number = value[..^2]; scale = 72d; percent = false; }
        else if (allowRem && value.EndsWith("rem", StringComparison.Ordinal))
        { number = value[..^3]; scale = rootFontSize ?? 1; percent = false; }
        else if (allowEm && !value.EndsWith("rem", StringComparison.Ordinal)
            && value.EndsWith("em", StringComparison.Ordinal))
        { number = value[..^2]; scale = elementFontSize ?? 1; percent = false; }
        else if (value.EndsWith('%'))
        { number = value[..^1]; scale = 1; percent = true; }
        else if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var zero) && zero == 0)
            return new CssLength(0);
        else throw new FactsPdfException("FPDF1202", "Box lengths require pt, px, mm, cm, in, %, scoped rem/em, or unitless zero.", offset);

        if (!double.TryParse(number, NumberStyles.Float,
            CultureInfo.InvariantCulture, out var parsed) || !double.IsFinite(parsed))
            throw new FactsPdfException("FPDF1202", "Box length must be a finite decimal.", offset);
        var length = parsed * scale;
        if (!double.IsFinite(length) || length < 0 || length > (percent ? 10_000 : 14_400))
            throw new FactsPdfException("FPDF1202", "Box length is outside the bounded nonnegative range.", offset);
        return new CssLength(length, percent);
    }
}
