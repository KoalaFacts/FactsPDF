using System.Globalization;

namespace FactsPDF;

/// <summary>Strict bounded CSS report box lengths, independent of culture and browser APIs.</summary>
internal static class CssBoxValues
{
    /// <summary>Points per ex, ch, lh and rlh for the element being computed.</summary>
    internal sealed record FontMetricScales(double Ex, double Ch, double LineHeight, double RootLineHeight);

    // Set by the cascade only while it applies the declarations of one element whose values use a
    // font-relative unit; without it (syntax checks) ex, ch, lh and rlh validate with a scale of 1.
    [ThreadStatic] internal static FontMetricScales? Metrics;

    /// <summary>Whether a computed length is a plain number with an ex, ch, lh or rlh unit.</summary>
    internal static bool UsesFontMetricUnit(string value) => value.Length > 2 && IsMetricUnit(value);

    private static bool IsMetricUnit(ReadOnlySpan<char> value) =>
        value.EndsWith("rlh", StringComparison.Ordinal)
        || value.Length > 2 && (value.EndsWith("ex", StringComparison.Ordinal) && !value.EndsWith("rex", StringComparison.Ordinal)
            || value.EndsWith("ch", StringComparison.Ordinal) && !value.EndsWith("rch", StringComparison.Ordinal)
            || value.EndsWith("lh", StringComparison.Ordinal));

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
        // The percentage plus length a calc() sum resolves to: P%+Npt or P%-Npt (see CssMath).
        if (value.IndexOf('%') is var percentAt and > 0 && percentAt < value.Length - 1 && value[percentAt + 1] is '+' or '-'
            && value.EndsWith("pt", StringComparison.Ordinal))
        {
            if (!double.TryParse(value.AsSpan(0, percentAt), NumberStyles.Float, CultureInfo.InvariantCulture, out var percentage)
                || !double.TryParse(value.AsSpan(percentAt + 1, value.Length - percentAt - 3), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var fixedPart)
                || !double.IsFinite(percentage) || !double.IsFinite(fixedPart))
            { error = "Box length must be a finite decimal."; return false; }
            if (percentage < 0 || percentage > 10_000 || Math.Abs(fixedPart) > 14_400)
            { error = "Box length is outside the bounded range."; return false; }
            result = new CssLength(percentage, true, fixedPart);
            return true;
        }
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
        else if (allowRem && value.EndsWith("rlh", StringComparison.Ordinal))
        { number = value.AsSpan(0, value.Length - 3); scale = Metrics?.RootLineHeight ?? 1; percent = false; }
        else if (allowEm && IsMetricUnit(value))
        {
            number = value.AsSpan(0, value.Length - 2); percent = false;
            scale = value.EndsWith("ex", StringComparison.Ordinal) ? Metrics?.Ex ?? 1
                : value.EndsWith("ch", StringComparison.Ordinal) ? Metrics?.Ch ?? 1 : Metrics?.LineHeight ?? 1;
        }
        else if (value.EndsWith('%'))
        { number = value.AsSpan(0, value.Length - 1); scale = 1; percent = true; }
        else if (IsExactlyUnitlessZero(value))
        { result = new CssLength(0); return true; }
        else { error = "Box lengths require pt, px, mm, cm, in, pc, q, %, scoped rem/em/ex/ch/lh/rlh, or unitless zero."; return false; }

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
