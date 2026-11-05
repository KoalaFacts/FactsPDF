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

    // Exact CSS Number zero, without floating-point rounding or underflow.
    // Works for direct longhands as well as syntax-lowered shorthand sides.
    internal static bool IsExactlyUnitlessZero(string value) => IsExactlyUnitlessZero(value.AsSpan());

    internal static bool IsExactlyUnitlessZero(ReadOnlySpan<char> source)
    {
        var index = 0;
        if (index < source.Length && source[index] is '+' or '-') index++;
        var digits = 0;
        while (index < source.Length && source[index] is >= '0' and <= '9')
        {
            if (source[index] != '0') return false;
            digits++;
            index++;
        }
        if (index < source.Length && source[index] == '.')
        {
            index++;
            while (index < source.Length && source[index] is >= '0' and <= '9')
            {
                if (source[index] != '0') return false;
                digits++;
                index++;
            }
        }
        if (digits == 0) return false;
        if (index < source.Length && source[index] is 'e' or 'E')
        {
            index++;
            if (index < source.Length && source[index] is '+' or '-') index++;
            var exponentStart = index;
            while (index < source.Length && source[index] is >= '0' and <= '9') index++;
            if (index == exponentStart) return false;
        }
        return index == source.Length;
    }

    internal static CssLength Length(string value, int offset,
        double? rootFontSize = null, bool allowRem = false,
        double? elementFontSize = null, bool allowEm = false)
        => TryLength(value, out var length, out var error, rootFontSize, allowRem, elementFontSize, allowEm)
            ? length : throw new FactsPdfException("FPDF1202", error!, offset);

    // Non-throwing core of Length, so callers probing shorthand terms (such as a border
    // width among style and colour terms) do not use exceptions for control flow.
    internal static bool TryLength(string value, out CssLength result, out string? error,
        double? rootFontSize = null, bool allowRem = false,
        double? elementFontSize = null, bool allowEm = false)
    {
        result = default; error = null;
        if (value == "0") { result = new CssLength(0); return true; }
        double scale;
        bool percent;
        ReadOnlySpan<char> number;
        if (value.EndsWith("pt", StringComparison.Ordinal))
        { number = value.AsSpan(0, value.Length - 2); scale = 1; percent = false; }
        else if (value.EndsWith("px", StringComparison.Ordinal))
        { number = value.AsSpan(0, value.Length - 2); scale = 0.75; percent = false; }
        else if (value.EndsWith("mm", StringComparison.Ordinal))
        { number = value.AsSpan(0, value.Length - 2); scale = 72d / 25.4d; percent = false; }
        else if (value.EndsWith("cm", StringComparison.Ordinal))
        { number = value.AsSpan(0, value.Length - 2); scale = 72d / 2.54d; percent = false; }
        else if (value.EndsWith("in", StringComparison.Ordinal))
        { number = value.AsSpan(0, value.Length - 2); scale = 72d; percent = false; }
        // Print physical units: 1pc = 12pt; 4q = 1mm.
        else if (value.EndsWith("pc", StringComparison.Ordinal))
        { number = value.AsSpan(0, value.Length - 2); scale = 12d; percent = false; }
        else if (value.EndsWith('q'))
        { number = value.AsSpan(0, value.Length - 1); scale = (72d / 25.4d) / 4d; percent = false; }
        else if (allowRem && value.EndsWith("rem", StringComparison.Ordinal))
        { number = value.AsSpan(0, value.Length - 3); scale = rootFontSize ?? 1; percent = false; }
        else if (allowEm && !value.EndsWith("rem", StringComparison.Ordinal)
            && value.EndsWith("em", StringComparison.Ordinal))
        { number = value.AsSpan(0, value.Length - 2); scale = elementFontSize ?? 1; percent = false; }
        else if (value.EndsWith('%'))
        { number = value.AsSpan(0, value.Length - 1); scale = 1; percent = true; }
        else if (IsExactlyUnitlessZero(value))
        { result = new CssLength(0); return true; }
        else { error = "Box lengths require pt, px, mm, cm, in, pc, q, %, scoped rem/em, or unitless zero."; return false; }

        if (!double.TryParse(number, NumberStyles.Float,
            CultureInfo.InvariantCulture, out var parsed) || !double.IsFinite(parsed))
        { error = "Box length must be a finite decimal."; return false; }
        // Values such as 1e-999pt or -1e-999px are nonzero CSS numbers,
        // even when double.TryParse rounds their magnitude to zero. Never
        // silently turn them into a supported zero-length declaration.
        if (parsed == 0 && !IsExactlyUnitlessZero(number))
        { error = "Nonzero box length underflow is unsupported."; return false; }
        var length = parsed * scale;
        if (!double.IsFinite(length) || length < 0 || length > (percent ? 10_000 : 14_400))
        { error = "Box length is outside the bounded nonnegative range."; return false; }
        result = new CssLength(length, percent);
        return true;
    }
}
