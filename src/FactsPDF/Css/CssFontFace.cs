using System.Globalization;
using FactsPDF.CssSyntax;

namespace FactsPDF;

/// <summary>One <c>src</c> entry of an <c>@font-face</c> rule: a URL (a bound font resource or a data: URL) or a <c>local()</c> font name.</summary>
internal sealed record FontFaceSource(string? Url, string? Local, bool FormatSupported, int Offset);

/// <summary>A parsed <c>@font-face</c> rule: the family it adds a face to, the face's descriptor ranges and its sources.</summary>
internal sealed record FontFaceRule(string Family, string DisplayFamily, float MinWeight, float MaxWeight, FontSlant Slant,
    float MinStretch, float MaxStretch, FontFaceSource[] Sources, int Offset);

/// <summary>
/// <c>@font-face</c> (CSS Fonts 4 §4): font-family and src are required; font-weight, font-style and
/// font-stretch give the face's matching ranges; font-display only steers loading on screen and is
/// validated and dropped. unicode-range, metric overrides, size-adjust, feature and variation
/// settings and named instances change what is printed in ways the renderer does not implement,
/// so any value other than their initial one fails FPDF1202.
/// </summary>
internal static class CssFontFace
{
    internal static FontFaceRule? Parse(CssAtRuleNode at, CssWarnings? warnings)
    {
        if (at.Prelude.Any(c => c is not CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Whitespace }))
            throw new FactsPdfException("FPDF1204", "@font-face takes no prelude, only a block of descriptors.", at.Span.Start);
        if (at.Contents is null)
            throw new FactsPdfException("FPDF1204", "@font-face needs a block of descriptors.", at.Span.Start);
        (string Key, string Display)? family = null;
        FontFaceSource[]? sources = null;
        (float, float) weight = (400, 400), stretch = (100, 100);
        var slant = FontSlant.Normal;
        foreach (var content in at.Contents)
        {
            if (content is not CssDeclarationNode descriptor)
                throw new FactsPdfException("FPDF1204", "@font-face contains only descriptors.", content.Span.Start);
            // An invalid descriptor is dropped by a browser, which keeps the rest of the rule.
            try { Descriptor(descriptor); }
            catch (FactsPdfException error) when (warnings is not null && error.Code == "FPDF1204") { warnings.Add(error); }
        }
        if (family is null || sources is null)
        {
            var error = new FactsPdfException("FPDF1204", "@font-face needs both a valid font-family and a valid src descriptor.", at.Span.Start);
            if (warnings is null) throw error;
            warnings.Add(error);
            return null;
        }
        return new FontFaceRule(family.Value.Key, family.Value.Display, weight.Item1, weight.Item2, slant,
            stretch.Item1, stretch.Item2, sources, at.Span.Start);

        void Descriptor(CssDeclarationNode descriptor)
        {
            var values = descriptor.Values.Where(c => c is not CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Whitespace }).ToArray();
            var offset = values.Length > 0 ? values[0].Span.Start : descriptor.Span.Start;
            if (descriptor.Important)
                throw Invalid("@font-face descriptors cannot be !important.", descriptor.Span.Start);
            switch (descriptor.Name.ToLowerInvariant())
            {
                case "font-family": family = Family(values, offset); break;
                case "src": sources = Sources(values, offset); break;
                case "font-weight": weight = Range(values, offset, "font-weight", Weight); break;
                case "font-style": slant = Style(values, offset); break;
                case "font-stretch": stretch = Range(values, offset, "font-stretch", Stretch); break;
                case "font-display":
                    if (Ident(values) is not ("auto" or "block" or "swap" or "fallback" or "optional"))
                        throw Invalid("font-display takes auto, block, swap, fallback or optional.", offset);
                    break;
                case "unicode-range":
                    throw new FactsPdfException("FPDF1202",
                        "@font-face unicode-range is not supported: a face limited to a character range changes which face draws each character; drop the descriptor (one face per family covers every character).", offset);
                case "font-feature-settings" or "font-variation-settings" or "font-language-override"
                    or "ascent-override" or "descent-override" or "line-gap-override":
                    if (Ident(values) != "normal")
                        throw new FactsPdfException("FPDF1202", $"@font-face {descriptor.Name.ToLowerInvariant()} supports only normal: the renderer applies no features, variations or metric overrides.", offset);
                    break;
                case "size-adjust":
                    if (!(values.Length == 1 && values[0] is CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.Percentage } size }
                        && Number(size.Value) == 100))
                        throw new FactsPdfException("FPDF1202", "@font-face size-adjust supports only 100%: the renderer does not scale a face's glyphs.", offset);
                    break;
                case "font-named-instance":
                    if (Ident(values) != "auto")
                        throw new FactsPdfException("FPDF1202", "@font-face font-named-instance supports only auto: variable fonts are not supported.", offset);
                    break;
                default:
                    throw Invalid($"Unknown @font-face descriptor '{descriptor.Name}'.", descriptor.Span.Start);
            }
        }
    }

    // <family-name>: one string, or identifiers that are not a generic family or CSS-wide keyword.
    private static (string Key, string Display) Family(CssSyntaxComponent[] values, int offset)
    {
        if (values.Length == 1 && values[0] is CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.String } text } && text.Value.Length > 0)
            return (FontFamilyNames.Fold(text.Value), text.Value);
        if (values.Length > 0 && values.All(v => v is CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Ident }))
        {
            var words = values.Select(v => ((CssTokenComponent)v).Token.Value).ToArray();
            var key = FontFamilyNames.Fold(string.Join(' ', words));
            if (!(words.Length == 1 && (FontFamilyNames.IsGeneric(key)
                || key is "inherit" or "initial" or "unset" or "revert" or "revert-layer" or "default")))
                return (key, string.Join(' ', words));
        }
        throw Invalid("@font-face font-family takes one family name: a string or identifiers that are not a generic family.", offset);
    }

    private static FontFaceSource[] Sources(CssSyntaxComponent[] values, int offset)
    {
        var sources = new List<FontFaceSource>();
        var entry = new List<CssSyntaxComponent>();
        for (var i = 0; i <= values.Length; i++)
        {
            if (i < values.Length && values[i] is not CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Comma }) { entry.Add(values[i]); continue; }
            if (entry.Count == 0) throw Invalid("@font-face src has an empty entry.", i < values.Length ? values[i].Span.Start : offset);
            sources.Add(Source(entry));
            entry.Clear();
        }
        return [.. sources];
    }

    private static FontFaceSource Source(List<CssSyntaxComponent> entry)
    {
        var first = entry[0];
        var at = first.Span.Start;
        if (first is CssFunctionComponent { Name: var function } local && function.Equals("local", StringComparison.OrdinalIgnoreCase))
        {
            if (entry.Count != 1) throw Invalid("local() in @font-face src takes no format().", at);
            var name = Arguments(local);
            if (name.Length == 1 && name[0] is CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.String } text } && text.Value.Length > 0)
                return new(null, text.Value, true, at);
            if (name.Length > 0 && name.All(v => v is CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Ident }))
                return new(null, string.Join(' ', name.Select(v => ((CssTokenComponent)v).Token.Value)), true, at);
            throw Invalid("local() takes one font name.", at);
        }
        string? url = first switch
        {
            CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.Url } token } => token.Value,
            CssFunctionComponent { Name: var name } call when name.Equals("url", StringComparison.OrdinalIgnoreCase)
                && Arguments(call) is [CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.String } quoted }] => quoted.Value,
            _ => null,
        };
        if (url is null || url.Length == 0) throw Invalid("Each @font-face src entry is url(...) with an optional format(), or local(name).", at);
        var supported = true;
        for (var i = 1; i < entry.Count; i++)
        {
            if (entry[i] is CssFunctionComponent { Name: var hint } format && hint.Equals("format", StringComparison.OrdinalIgnoreCase) && i == 1)
            {
                var names = Arguments(format).Where(v => v is not CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Comma }).ToArray();
                if (names.Length == 0 || names.Any(v => v is not CssTokenComponent { Token.Kind: CssSyntaxTokenKind.String or CssSyntaxTokenKind.Ident }))
                    throw Invalid("format() takes font format names.", entry[i].Span.Start);
                // A format the browser cannot load makes it skip the entry; so does FactsPDF.
                supported = names.Any(v => ((CssTokenComponent)v).Token.Value.ToLowerInvariant() is "woff" or "woff2" or "truetype" or "opentype"
                    or "woff-variations" or "woff2-variations" or "truetype-variations" or "opentype-variations");
                continue;
            }
            if (entry[i] is CssFunctionComponent { Name: var tech } && tech.Equals("tech", StringComparison.OrdinalIgnoreCase))
                throw new FactsPdfException("FPDF1202", "@font-face src tech() is not supported: the renderer loads no color or variation technologies.", entry[i].Span.Start);
            throw Invalid("A url() in @font-face src may be followed only by format().", entry[i].Span.Start);
        }
        return new(url, null, supported, at);
    }

    private static CssSyntaxComponent[] Arguments(CssFunctionComponent function) =>
        function.Values.Where(c => c is not CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Whitespace }).ToArray();

    // One value, or two giving a range (swapped into ascending order).
    private static (float, float) Range(CssSyntaxComponent[] values, int offset, string name, Func<CssSyntaxComponent, float?> parse)
    {
        if (values.Length == 1 && Ident(values) == "auto")
            throw new FactsPdfException("FPDF1202", $"@font-face {name}:auto is not supported; write the face's {name}.", offset);
        if (values.Length is < 1 or > 2) throw Invalid($"@font-face {name} takes one value or a range of two.", offset);
        var low = parse(values[0]) ?? throw Invalid($"Invalid @font-face {name} value.", values[0].Span.Start);
        var high = values.Length == 2 ? parse(values[1]) ?? throw Invalid($"Invalid @font-face {name} value.", values[1].Span.Start) : low;
        return low <= high ? (low, high) : (high, low);
    }

    private static float? Weight(CssSyntaxComponent value) => value switch
    {
        CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.Ident } word } => word.Value.ToLowerInvariant() switch { "normal" => 400, "bold" => 700, _ => null },
        CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.Number } number } when Number(number.Value) is >= 1 and <= 1000 => (float)Number(number.Value),
        _ => null,
    };

    private static float? Stretch(CssSyntaxComponent value) => value switch
    {
        CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.Ident } word } => StretchKeyword(word.Value.ToLowerInvariant()),
        CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.Percentage } percent } when Number(percent.Value) >= 0 => (float)Number(percent.Value),
        _ => null,
    };

    /// <summary>The percentage of a font-stretch keyword, or null.</summary>
    internal static float? StretchKeyword(string word) => word switch
    {
        "ultra-condensed" => 50, "extra-condensed" => 62.5f, "condensed" => 75, "semi-condensed" => 87.5f, "normal" => 100,
        "semi-expanded" => 112.5f, "expanded" => 125, "extra-expanded" => 150, "ultra-expanded" => 200, _ => null,
    };

    // normal | italic | oblique [<angle>{1,2}]: an oblique face with a positive angle matches as
    // oblique; oblique 0deg is upright. Negative angles (backslanted faces) are not supported.
    private static FontSlant Style(CssSyntaxComponent[] values, int offset)
    {
        var word = values.Length > 0 && values[0] is CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.Ident } first } ? first.Value.ToLowerInvariant() : null;
        if (values.Length == 1 && word == "normal") return FontSlant.Normal;
        if (values.Length == 1 && word == "italic") return FontSlant.Italic;
        if (word == "auto") throw new FactsPdfException("FPDF1202", "@font-face font-style:auto is not supported; write the face's style.", offset);
        if (word != "oblique" || values.Length > 3) throw Invalid("@font-face font-style takes normal, italic or oblique with up to two angles.", offset);
        if (values.Length == 1) return FontSlant.Oblique;
        var angles = values.Skip(1).Select(v => Angle(v) ?? throw Invalid("Invalid oblique angle.", v.Span.Start)).ToArray();
        if (angles.Any(a => a is < -90 or > 90)) throw Invalid("Oblique angles lie from -90deg to 90deg.", values[1].Span.Start);
        if (angles.Any(a => a < 0))
            throw new FactsPdfException("FPDF1202", "Negative oblique angles (backslanted faces) are not supported.", values[1].Span.Start);
        return angles.Max() > 0 ? FontSlant.Oblique : FontSlant.Normal;
    }

    /// <summary>An angle token in degrees, or null.</summary>
    internal static double? Angle(CssSyntaxComponent value)
    {
        if (value is not CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.Dimension, Unit: { } unit } token }) return null;
        var number = Number(token.Value);
        return unit.ToLowerInvariant() switch
        {
            "deg" => number, "grad" => number * 0.9, "rad" => number * 180 / Math.PI, "turn" => number * 360, _ => null,
        };
    }

    private static string? Ident(CssSyntaxComponent[] values) =>
        values.Length == 1 && values[0] is CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.Ident } word } ? word.Value.ToLowerInvariant() : null;

    private static double Number(string text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : double.NaN;

    // Invalid per CSS Fonts: a browser drops the rule, so non-strict mode drops it with a warning
    // (FPDF1204, the at-rule code); valid values the renderer cannot print fail FPDF1202 in both modes.
    private static FactsPdfException Invalid(string message, int offset) => new("FPDF1204", message, offset);
}
