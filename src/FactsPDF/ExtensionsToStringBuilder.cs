using System.Globalization;

// Extensions live in the namespace of the type they extend, so callers need no extra using.
#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace System.Text;
#pragma warning restore IDE0130

internal static class ExtensionsToStringBuilder
{
    /// <summary>
    /// Appends <paramref name="value"/> formatted with the invariant culture, exactly as
    /// <c>value.ToString(format, CultureInfo.InvariantCulture)</c>, without allocating a string or a box.
    /// </summary>
    internal static StringBuilder AppendInvariant(this StringBuilder output, double value, string format)
    {
        Span<char> text = stackalloc char[64];
        return value.TryFormat(text, out var written, format, CultureInfo.InvariantCulture)
            ? output.Append(text[..written])
            : output.Append(value.ToString(format, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Appends <paramref name="value"/> as upper-case hex with at least <paramref name="digits"/> digits,
    /// exactly as <c>value.ToString("X" + digits, CultureInfo.InvariantCulture)</c>, without allocating.
    /// </summary>
    internal static StringBuilder AppendHex(this StringBuilder output, int value, int digits)
    {
        Span<char> text = stackalloc char[8];
        var length = 0;
        for (var rest = (uint)value; rest != 0 || length < digits; rest >>= 4)
            text[^++length] = (char)((rest & 0xF) < 10 ? '0' + (rest & 0xF) : 'A' + (rest & 0xF) - 10);
        return output.Append(text[^length..]);
    }
}
