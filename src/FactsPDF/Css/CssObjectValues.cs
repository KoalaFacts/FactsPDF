namespace FactsPDF;

/// <summary>How a replaced element's content is fitted into its content box (object-fit).</summary>
internal enum ObjectFit { Fill, Contain, Cover, None, ScaleDown }

/// <summary>
/// One axis of object-position: the offset of the content's edge from the box's edge is
/// <c>(box - content) * Percent / 100 + Offset</c> points, which covers a keyword (0, 50 or 100%), a
/// percentage, a length and a keyword with an offset from its edge (<c>right 10pt</c> is 100% and -10pt).
/// </summary>
internal readonly record struct ObjectAxis(double Percent, double Offset)
{
    internal static readonly ObjectAxis Centre = new(50, 0);
    internal double Resolve(double free) => free * Percent / 100 + Offset;
}

/// <summary>The computed object-fit and object-position of an img, shared by the box style and the placed image.</summary>
internal readonly record struct ObjectPlacement(ObjectFit Fit, ObjectAxis X, ObjectAxis Y)
{
    internal static readonly ObjectPlacement Initial = new(ObjectFit.Fill, ObjectAxis.Centre, ObjectAxis.Centre);
}

/// <summary>Parses and validates object-fit and object-position values (CSS Images 3 and CSS Values 4 &lt;position&gt;).</summary>
internal static class CssObjectValues
{
    internal static ObjectFit Fit(string value, int offset) => value switch
    {
        "fill" => ObjectFit.Fill,
        "contain" => ObjectFit.Contain,
        "cover" => ObjectFit.Cover,
        "none" => ObjectFit.None,
        "scale-down" => ObjectFit.ScaleDown,
        _ => throw new FactsPdfException("FPDF1202", "object-fit requires fill, contain, cover, none or scale-down.", offset)
    };

    // A component of a position: a keyword, optionally followed by an offset from its edge, or a bare
    // length or percentage.
    private readonly record struct Part(string? Keyword, string? Length);

    /// <summary>
    /// Parses a normalised (lower-case, space-separated) position of one to four terms. em and rem
    /// lengths resolve against <paramref name="elementFontSize"/> and <paramref name="rootFontSize"/>.
    /// </summary>
    internal static (ObjectAxis X, ObjectAxis Y) Position(string value, int offset, double rootFontSize, double elementFontSize)
    {
        var terms = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        ObjectAxis Axis(string? keyword, string? length) => AxisOf(keyword, length, rootFontSize, elementFontSize, offset);
        switch (terms.Length)
        {
            case 1:
                if (!IsKeyword(terms[0])) return (Axis(null, terms[0]), ObjectAxis.Centre);
                return terms[0] is "top" or "bottom"
                    ? (ObjectAxis.Centre, Axis(terms[0], null)) : (Axis(terms[0], null), ObjectAxis.Centre);
            case 2:
            {
                var (first, second) = (terms[0], terms[1]);
                var firstKeyword = IsKeyword(first);
                var secondKeyword = IsKeyword(second);
                if (firstKeyword && secondKeyword)
                {
                    if (first is "left" or "right" or "center" && second is "top" or "bottom" or "center")
                        return (Axis(first, null), Axis(second, null));
                    if (first is "top" or "bottom" or "center" && second is "left" or "right" or "center")
                        return (Axis(second, null), Axis(first, null));
                    throw Invalid(offset);
                }
                if (firstKeyword && first is not ("left" or "right" or "center")) throw Invalid(offset);
                if (secondKeyword && second is not ("top" or "bottom" or "center")) throw Invalid(offset);
                return (Axis(firstKeyword ? first : null, firstKeyword ? null : first),
                    Axis(secondKeyword ? second : null, secondKeyword ? null : second));
            }
            case 3 or 4:
            {
                var parts = new List<Part>(2);
                for (var i = 0; i < terms.Length;)
                {
                    if (!IsKeyword(terms[i])) throw Invalid(offset);
                    string? length = null;
                    if (i + 1 < terms.Length && !IsKeyword(terms[i + 1]))
                    {
                        if (terms[i] == "center") throw Invalid(offset);
                        length = terms[i + 1];
                    }
                    parts.Add(new(terms[i], length));
                    i += length is null ? 1 : 2;
                }
                if (parts.Count != 2) throw Invalid(offset);
                Part first = parts[0], second = parts[1];
                var firstVertical = first.Keyword is "top" or "bottom";
                var secondVertical = second.Keyword is "top" or "bottom";
                var firstHorizontal = first.Keyword is "left" or "right";
                var secondHorizontal = second.Keyword is "left" or "right";
                if (firstVertical && secondVertical || firstHorizontal && secondHorizontal) throw Invalid(offset);
                // center fills whichever axis the other keyword leaves.
                var swapped = firstVertical || secondHorizontal;
                var (horizontal, vertical) = swapped ? (second, first) : (first, second);
                return (Axis(horizontal.Keyword, horizontal.Length), Axis(vertical.Keyword, vertical.Length));
            }
            default:
                throw Invalid(offset);
        }
    }

    private static bool IsKeyword(string term) => term is "left" or "right" or "top" or "bottom" or "center";

    private static FactsPdfException Invalid(int offset) =>
        new("FPDF1202", "Invalid object-position value.", offset);

    private static ObjectAxis AxisOf(string? keyword, string? length, double rootFontSize, double elementFontSize, int offset)
    {
        var amount = length is null ? default : Length(length, rootFontSize, elementFontSize, offset);
        return keyword switch
        {
            null => amount.IsPercent ? new(amount.Value, 0) : new(0, amount.Value),
            "left" or "top" => length is null ? new(0, 0) : amount.IsPercent ? new(amount.Value, 0) : new(0, amount.Value),
            "right" or "bottom" => length is null ? new(100, 0) : amount.IsPercent ? new(100 - amount.Value, 0) : new(100, -amount.Value),
            _ => ObjectAxis.Centre
        };
    }

    // A signed length or percentage; the box length parser takes the magnitude.
    private static CssLength Length(string text, double rootFontSize, double elementFontSize, int offset)
    {
        var negative = text.StartsWith('-');
        var magnitude = negative || text.StartsWith('+') ? text[1..] : text;
        var length = CssBoxValues.Length(magnitude, offset, rootFontSize, allowRem: true, elementFontSize, allowEm: true);
        return negative ? new CssLength(-length.Value, length.IsPercent) : length;
    }
}
