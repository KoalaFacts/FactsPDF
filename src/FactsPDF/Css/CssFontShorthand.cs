using FactsPDF.CssSyntax;

namespace FactsPDF;

/// <summary>
/// Splits the value of the CSS <c>font</c> shorthand (CSS Fonts 4):
/// <c>[ [ font-style || font-variant-css2 || font-weight || font-stretch-css3 ]? font-size [ / line-height ]? font-family# ]</c>,
/// a system font keyword, or a CSS-wide keyword. Only the structure is decided here; each part is validated and
/// compiled as the longhand it belongs to, so the shorthand has no second implementation of any longhand.
/// </summary>
internal static class CssFontShorthand
{
    internal enum Form { Invalid, CssWide, SystemFont, Longhands }

    /// <summary>A half-open range of indexes into the value components; <see cref="Start"/> is -1 when absent.</summary>
    internal readonly record struct Part(int Start, int End)
    {
        internal static readonly Part None = new(-1, -1);
        internal bool Present => Start >= 0;
    }

    internal readonly record struct Split(
        Form Form, Part Style, Part Variant, Part Weight, Part Stretch, Part Size, Part LineHeight, Part Family);

    private static readonly Split Rejected = new(Form.Invalid, Part.None, Part.None, Part.None, Part.None, Part.None, Part.None, Part.None);

    internal static Split Parse(IReadOnlyList<CssSyntaxComponent> values)
    {
        // Index of every non-whitespace component: the grammar is whitespace-insensitive.
        var at = new List<int>(values.Count);
        for (var i = 0; i < values.Count; i++)
            if (values[i] is not CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Whitespace }) at.Add(i);
        if (at.Count == 0) return Rejected;
        if (at.Count == 1 && Word(values[at[0]]) is { } only)
        {
            if (only is "inherit" or "initial" or "unset" or "revert" or "revert-layer") return Rejected with { Form = Form.CssWide };
            if (IsSystemFont(only)) return Rejected with { Form = Form.SystemFont };
        }

        Part style = Part.None, variant = Part.None, weight = Part.None, stretch = Part.None;
        var normals = 0;
        var position = 0;
        var sizeStarts = false;
        for (; position < at.Count && !sizeStarts; position++)
        {
            var component = values[at[position]];
            var word = Word(component);
            if (word is null)
            {
                // A <number [1,1000]> is a font-weight; anything else starts the font-size.
                if (!IsWeightNumber(component)) break;
                if (weight.Present) return Rejected;
                weight = Single(at, position);
                continue;
            }
            switch (word)
            {
                case "normal": normals++; break;
                case "italic":
                    if (style.Present) return Rejected;
                    style = Single(at, position);
                    break;
                case "oblique":
                    if (style.Present) return Rejected;
                    if (position + 1 < at.Count && IsAngle(values[at[position + 1]]))
                    {
                        style = new(at[position], at[position + 1] + 1);
                        position++;
                    }
                    else style = Single(at, position);
                    break;
                case "small-caps":
                    if (variant.Present) return Rejected;
                    variant = Single(at, position);
                    break;
                case "bold" or "bolder" or "lighter":
                    if (weight.Present) return Rejected;
                    weight = Single(at, position);
                    break;
                case "ultra-condensed" or "extra-condensed" or "condensed" or "semi-condensed"
                    or "semi-expanded" or "expanded" or "extra-expanded" or "ultra-expanded":
                    if (stretch.Present) return Rejected;
                    stretch = Single(at, position);
                    break;
                default:
                    sizeStarts = true;
                    break;
            }
        }
        if (sizeStarts) position--;
        var specified = (style.Present ? 1 : 0) + (variant.Present ? 1 : 0) + (weight.Present ? 1 : 0) + (stretch.Present ? 1 : 0);
        if (specified + normals > 4 || position >= at.Count) return Rejected;
        var size = Single(at, position);
        position++;
        var lineHeight = Part.None;
        if (position < at.Count && values[at[position]] is CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.Delim, Value: "/" } })
        {
            if (position + 1 >= at.Count) return Rejected;
            lineHeight = Single(at, position + 1);
            position += 2;
        }
        if (position >= at.Count) return Rejected;
        // Unlike the font-family longhand, Chrome rejects a CSS-wide keyword or default among the families.
        for (var i = position; i < at.Count; i++)
            if (Word(values[at[i]]) is "inherit" or "initial" or "unset" or "revert" or "revert-layer" or "default") return Rejected;
        return new Split(Form.Longhands, style, variant, weight, stretch, size, lineHeight, new(at[position], at[^1] + 1));
    }

    private static Part Single(List<int> at, int position) => new(at[position], at[position] + 1);

    private static string? Word(CssSyntaxComponent component) =>
        component is CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.Ident } token } ? token.Value.ToLowerInvariant() : null;

    private static bool IsWeightNumber(CssSyntaxComponent component) =>
        component is CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.Number } token }
        && double.TryParse(token.Value, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var number)
        && number is >= 1 and <= 1000;

    private static bool IsAngle(CssSyntaxComponent component) =>
        component is CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.Dimension } token }
        && token.Unit is not null && token.Unit.ToLowerInvariant() is "deg" or "grad" or "rad" or "turn";

    // CSS Fonts 4 <system-family-name>, plus the WebKit control fonts Chrome also accepts.
    private static bool IsSystemFont(string word) =>
        word is "caption" or "icon" or "menu" or "message-box" or "small-caption" or "status-bar"
            or "-webkit-small-control" or "-webkit-mini-control" or "-webkit-control";

    internal static CssSyntaxComponent[] Slice(IReadOnlyList<CssSyntaxComponent> values, Part part)
    {
        var slice = new CssSyntaxComponent[part.End - part.Start];
        for (var i = 0; i < slice.Length; i++) slice[i] = values[part.Start + i];
        return slice;
    }
}
