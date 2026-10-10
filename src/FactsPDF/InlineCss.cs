using System.Globalization;

namespace FactsPDF;

internal static class InlineCss
{
    public static TextStyle Apply(TextStyle style, string css, bool paragraph, bool inline, int offset)
    {
        foreach (var declaration in css.Split(';'))
        {
            if (string.IsNullOrWhiteSpace(declaration)) continue;
            var colon = declaration.IndexOf(':');
            if (colon <= 0) throw Invalid("Expected property: value.", offset);
            var name = declaration[..colon].Trim().ToLowerInvariant();
            var value = declaration[(colon + 1)..].Trim().ToLowerInvariant();
            if (value.Length == 0 || value.Contains('!')) throw Invalid("Empty values and !important are not supported.", offset);
            switch (name)
            {
                case "font-size":
                    var size = Length(value, offset);
                    if (size is < 1 or > 144) throw Invalid("font-size must be between 1pt and 144pt.", offset);
                    style = style with { FontSize = size };
                    break;
                case "color": style = style with { Color = Color(value, offset) }; break;
                case "line-height":
                    var factor = Number(value, offset);
                    if (factor is < 1 or > 10) throw Invalid("line-height must be a unitless number from 1 to 10.", offset);
                    style = style with { LineHeight = factor };
                    break;
                case "text-align" when !inline:
                    style = style with { Alignment = value switch
                    {
                        "left" => TextAlignment.Left, "center" => TextAlignment.Center, "right" => TextAlignment.Right,
                        _ => throw Invalid("Only left, center and right alignment are supported.", offset)
                    } };
                    break;
                case "margin-top" when paragraph: style = style with { MarginBefore = Margin(value, offset) }; break;
                case "margin-bottom" when paragraph: style = style with { MarginAfter = Margin(value, offset) }; break;
                case "break-before" when paragraph: style = style with { BreakBefore = PageBreak(value, offset) }; break;
                case "break-after" when paragraph: style = style with { BreakAfter = PageBreak(value, offset) }; break;
                default: throw new FactsPdfException("FPDF1201", $"CSS property '{name}' is not supported on this element.", offset);
            }
        }
        return style;
    }

    private static double Length(string value, int offset)
    {
        if (value == "0") return 0;
        if (value.EndsWith("pt", StringComparison.Ordinal)) return Number(value[..^2], offset);
        if (value.EndsWith("px", StringComparison.Ordinal)) return Number(value[..^2], offset) * 0.75;
        throw Invalid("This length requires pt or px units.", offset);
    }

    private static double Margin(string value, int offset)
    {
        var result = Length(value, offset);
        if (result is < 0 or > 14_400) throw Invalid("Margins must be between 0 and 14400pt.", offset);
        return result;
    }

    private static double Number(string value, int offset)
    {
        if (!double.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out var result) || !double.IsFinite(result))
            throw Invalid("Expected a finite decimal number.", offset);
        return result;
    }

    private static bool PageBreak(string value, int offset) => value switch
    {
        "page" => true, "auto" => false, _ => throw Invalid("Only auto/page breaks are supported.", offset)
    };

    internal static Rgb Color(string value, int offset)
    {
        value = value switch
        {
            "black" => "#000000", "white" => "#ffffff", "red" => "#ff0000",
            "green" => "#008000", "blue" => "#0000ff", "gray" or "grey" => "#808080", _ => value
        };
        if (value.Length == 4 && value[0] == '#')
            value = $"#{value[1]}{value[1]}{value[2]}{value[2]}{value[3]}{value[3]}";
        if (value.Length != 7 || value[0] != '#' || !uint.TryParse(value.AsSpan(1),
            NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var rgb))
            throw Invalid("Use #rgb, #rrggbb or a documented named color.", offset);
        return new(((rgb >> 16) & 255) / 255d, ((rgb >> 8) & 255) / 255d, (rgb & 255) / 255d);
    }

    private static FactsPdfException Invalid(string text, int offset) => new("FPDF1202", text, offset);
}
