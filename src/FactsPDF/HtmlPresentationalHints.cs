using System.Text;

namespace FactsPDF;

/// <summary>
/// The width and height attributes as presentational hints: HTML maps them to the CSS width and
/// height properties at the lowest cascade priority, below every author declaration. The value
/// follows the HTML "rules for parsing dimension values": ASCII white space, then digits with an
/// optional fraction, then an optional percent sign; anything after the number is ignored, and a
/// value that does not start with a digit (a sign, a dot, text) is ignored as a whole.
/// </summary>
internal static class HtmlPresentationalHints
{
    /// <summary>Whether the element maps a width attribute to CSS width.</summary>
    internal static bool Width(string element) => element is "img" or "video" or "iframe" or "table" or "td" or "th" or "col" or "colgroup";

    /// <summary>Whether the element maps a height attribute to CSS height. Table, row and cell heights have
    /// no CSS equivalent here (height applies to panels and images), so their attribute stays unsupported.</summary>
    internal static bool Height(string element) => element is "img" or "video" or "iframe";

    /// <summary>The CSS length or percentage the attribute value stands for (such as 100px or 50%), or
    /// null when HTML ignores the value.</summary>
    internal static string? Dimension(string value)
    {
        var i = 0;
        while (i < value.Length && HtmlTokens.Space(value[i])) i++;
        var start = i;
        while (i < value.Length && char.IsAsciiDigit(value[i])) i++;
        if (i == start) return null;
        var end = i;
        if (i + 1 < value.Length && value[i] == '.' && char.IsAsciiDigit(value[i + 1]))
        {
            i++;
            while (i < value.Length && char.IsAsciiDigit(value[i])) i++;
            end = i;
        }
        var number = new StringBuilder(value, start, end - start, end - start);
        return number.Append(i < value.Length && value[i] == '%' ? "%" : "px").ToString();
    }

    /// <summary>The hint declaration for one attribute, or null when its value is ignored.</summary>
    internal static CssDeclaration? Declaration(CssProperty property, string attribute, string value, int offset)
        => Dimension(value) is { } css ? CssDeclaration.Create(property, attribute, css, false, 0, offset) : null;
}
