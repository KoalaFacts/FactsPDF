using System.Collections.Frozen;
using System.Globalization;
using FactsPDF.CssSyntax;

namespace FactsPDF;

/// <summary>
/// Media Queries 4 evaluated as Chrome evaluates them when it prints: the media type is
/// <c>print</c>, and the viewport is the page area (the page size less the margins, the same area
/// <c>vw</c> and <c>vh</c> resolve against). A media query list matches when any of its queries
/// does; an empty list matches.
/// <para>Supported: the media types (<c>print</c> and <c>all</c> match, every other type such as
/// <c>screen</c> does not), <c>not</c>, <c>only</c>, <c>and</c>, <c>or</c>, nested conditions, and
/// the features <c>width</c>, <c>height</c>, <c>aspect-ratio</c> (with their <c>min-</c> and
/// <c>max-</c> forms and the range syntax), <c>orientation</c>, <c>color</c> (boolean form),
/// <c>monochrome</c>, <c>color-index</c> and <c>grid</c>, whose print values are fixed (a colour
/// device without a palette or a character grid).</para>
/// <para>A malformed query (a structure the grammar does not allow, such as <c>screen or print</c>)
/// is <c>not all</c>, as a browser treats it: <see cref="Outcome.Invalid"/> reports the first one so
/// strict mode can fail and non-strict mode can warn. A media feature the evaluator does not model
/// (<c>hover</c>, <c>resolution</c>, <c>prefers-color-scheme</c>, an unknown name, a
/// general-enclosed condition, a value it cannot compute) fails <c>FPDF1204</c> in both modes,
/// because guessing its value could print a stylesheet Chrome does not.</para>
/// </summary>
internal static class CssMediaQueries
{
    /// <summary>The result of evaluating a media query list.</summary>
    /// <param name="Matches">Whether the list matches when printing.</param>
    /// <param name="UsesPageArea">Whether a feature read the page area (width, height, aspect-ratio, orientation).</param>
    /// <param name="Invalid">The message of the first malformed query (which counts as <c>not all</c>), or null.</param>
    internal readonly record struct Outcome(bool Matches, bool UsesPageArea, string? Invalid);

    // 1px is 0.75pt; em and rem in a media query are the initial font size, 16px (Media Queries 4, section 1.3).
    private const double PointsPerPixel = 0.75, InitialFontSizePoints = 12;
    // Terms per query examined; a longer query is malformed, which bounds the work a hostile prelude causes.
    private const int MaxTerms = 512;

    // Features Chrome knows whose print value the evaluator does not model: they fail rather than guess.
    private static readonly FrozenSet<string> UnmodelledFeatures = new[]
    {
        "any-hover", "any-pointer", "hover", "pointer", "resolution", "min-resolution", "max-resolution",
        "color-gamut", "dynamic-range", "video-dynamic-range", "forced-colors", "inverted-colors",
        "prefers-color-scheme", "prefers-contrast", "prefers-reduced-motion", "prefers-reduced-data",
        "prefers-reduced-transparency", "scripting", "update", "overflow-block", "overflow-inline",
        "display-mode", "scan", "device-width", "min-device-width", "max-device-width", "device-height",
        "min-device-height", "max-device-height", "device-aspect-ratio", "min-device-aspect-ratio",
        "max-device-aspect-ratio", "-webkit-device-pixel-ratio", "-webkit-min-device-pixel-ratio",
        "-webkit-max-device-pixel-ratio", "-webkit-transform-3d", "device-posture", "shape",
        "horizontal-viewport-segments", "vertical-viewport-segments", "navigation-controls",
        "min-color", "max-color",
    }.ToFrozenSet(StringComparer.Ordinal);

    private sealed class MalformedException(string message) : Exception(message);

    private enum Feature { Width, Height, AspectRatio, Orientation, Color, Monochrome, ColorIndex, Grid }

    private sealed class Context(Func<(double Width, double Height)> pageArea, int offset)
    {
        internal int Offset { get; } = offset;
        internal bool UsesPageArea { get; private set; }
        private (double Width, double Height)? area;
        internal (double Width, double Height) PageArea()
        {
            UsesPageArea = true;
            return area ??= pageArea();
        }
        internal int Terms { get; set; }
    }

    /// <summary>Evaluates a media query list (an <c>@media</c> prelude, or a parsed <c>media</c> attribute).</summary>
    /// <param name="prelude">The list's component values.</param>
    /// <param name="pageArea">The page area in points, read only when a feature needs it.</param>
    /// <param name="offset">Where a failure is reported.</param>
    internal static Outcome Evaluate(IReadOnlyList<CssSyntaxComponent> prelude, Func<(double Width, double Height)> pageArea, int offset)
    {
        var context = new Context(pageArea, offset);
        var queries = new List<List<CssSyntaxComponent>> { new() };
        var empty = true;
        foreach (var item in prelude)
        {
            if (item is CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Whitespace }) continue;
            empty = false;
            if (item is CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Comma }) queries.Add([]);
            else queries[^1].Add(item);
        }
        if (empty) return new(true, false, null);
        var matches = false; string? invalid = null;
        // Every query is evaluated, so an unmodelled feature fails wherever it is written.
        foreach (var query in queries)
        {
            try { matches |= Query(query, context); }
            catch (MalformedException error) { invalid ??= error.Message; }
        }
        return new(matches, context.UsesPageArea, invalid);
    }

    private static bool Query(List<CssSyntaxComponent> items, Context context)
    {
        if (items.Count == 0) throw new MalformedException("A media query list has an empty query.");
        if (items.Count > MaxTerms) throw new MalformedException("A media query is too long.");
        var i = 0;
        if (items[0] is CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.Ident } first })
        {
            var lower = first.Value.ToLowerInvariant();
            var modifier = lower is "not" or "only" && items.Count > 1 && items[1] is CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Ident };
            if (lower == "only" && !modifier) throw new MalformedException("only must be followed by a media type.");
            if (modifier || lower != "not")
            {
                if (modifier) i++;
                var type = ((CssTokenComponent)items[i]).Token.Value.ToLowerInvariant();
                if (type is "not" or "only" or "and" or "or" or "layer")
                    throw new MalformedException($"'{type}' is not a media type.");
                i++;
                // When printing, the print medium is the only one that applies; all matches every medium.
                var result = type is "print" or "all";
                if (i < items.Count)
                {
                    if (!Keyword(items[i], "and")) throw new MalformedException("A media type is followed only by 'and' and a condition.");
                    i++;
                    if (i >= items.Count) throw new MalformedException("A media query ends after 'and'.");
                    result &= Condition(items, ref i, allowOr: false, context);
                    if (i != items.Count) throw new MalformedException("A media query has extra terms after its condition.");
                }
                return lower == "not" && modifier ? !result : result;
            }
        }
        var matches = Condition(items, ref i, allowOr: true, context);
        if (i != items.Count) throw new MalformedException("A media query has extra terms after its condition.");
        return matches;
    }

    private static bool Keyword(CssSyntaxComponent item, string keyword)
        => item is CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.Ident } token } && token.Value.Equals(keyword, StringComparison.OrdinalIgnoreCase);

    // <media-condition>: not <in-parens> | <in-parens> [and <in-parens>]* | <in-parens> [or <in-parens>]*.
    private static bool Condition(List<CssSyntaxComponent> items, ref int i, bool allowOr, Context context)
    {
        if (Keyword(items[i], "not"))
        {
            i++;
            if (i >= items.Count) throw new MalformedException("A media condition ends after 'not'.");
            return !InParens(items[i++], context);
        }
        var result = InParens(items[i++], context);
        string? joiner = null;
        while (i < items.Count && (Keyword(items[i], "and") || Keyword(items[i], "or")))
        {
            var word = ((CssTokenComponent)items[i]).Token.Value.ToLowerInvariant();
            if (joiner is not null && joiner != word) throw new MalformedException("A media condition mixes 'and' and 'or' without parentheses.");
            if (word == "or" && !allowOr) throw new MalformedException("'or' needs parentheses after a media type.");
            joiner = word;
            i++;
            if (i >= items.Count) throw new MalformedException($"A media condition ends after '{word}'.");
            var next = InParens(items[i++], context);
            result = word == "and" ? result & next : result | next;
        }
        return result;
    }

    private static bool InParens(CssSyntaxComponent item, Context context)
    {
        if (item is CssFunctionComponent function)
            throw Unsupported($"The general-enclosed media condition {function.Name}(...) is not supported.", context);
        if (item is not CssBlockComponent { Opening: CssSyntaxTokenKind.OpenParen } block)
            throw new MalformedException("A media condition needs parentheses.");
        var content = new List<CssSyntaxComponent>();
        foreach (var value in block.Values)
            if (value is not CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Whitespace }) content.Add(value);
        if (content.Count == 0) throw Unsupported("The empty general-enclosed media condition () is not supported.", context);
        if ((context.Terms += content.Count) > MaxTerms) throw new MalformedException("A media query is too long.");
        if (Keyword(content[0], "not") || content[0] is CssBlockComponent { Opening: CssSyntaxTokenKind.OpenParen })
        {
            var i = 0;
            var nested = Condition(content, ref i, allowOr: true, context);
            if (i != content.Count) throw new MalformedException("A nested media condition has extra terms.");
            return nested;
        }
        return MediaFeature(block.Values, content, context);
    }

    private static FactsPdfException Unsupported(string message, Context context)
        => new("FPDF1204", message, context.Offset);

    private static bool MediaFeature(IReadOnlyList<CssSyntaxComponent> raw, List<CssSyntaxComponent> content, Context context)
    {
        // Boolean: (name).
        if (content.Count == 1 && content[0] is CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.Ident } boolean })
        {
            var name = boolean.Value.ToLowerInvariant();
            var feature = Known(name, context);
            if (name.StartsWith("min-", StringComparison.Ordinal) || name.StartsWith("max-", StringComparison.Ordinal))
                throw Unsupported($"The media feature ({boolean.Value}) needs a value.", context);
            return feature switch
            {
                Feature.Width => context.PageArea().Width != 0,
                Feature.Height => context.PageArea().Height != 0,
                Feature.AspectRatio or Feature.Orientation => Touch(context),
                Feature.Color => true,
                _ => false, // monochrome, color-index and grid are 0 on a colour page.
            };
        }
        // Plain: (name: value).
        if (content.Count >= 3 && content[0] is CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.Ident } plain }
            && content[1] is CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Colon })
        {
            var name = plain.Value.ToLowerInvariant();
            var prefix = name.StartsWith("min-", StringComparison.Ordinal) ? -1 : name.StartsWith("max-", StringComparison.Ordinal) ? 1 : 0;
            var feature = Known(name, context);
            var value = content.GetRange(2, content.Count - 2);
            if (feature == Feature.Orientation)
            {
                if (value.Count != 1 || value[0] is not CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.Ident } keyword })
                    throw Unsupported("orientation takes portrait or landscape.", context);
                var (width, height) = context.PageArea();
                return keyword.Value.ToLowerInvariant() switch
                {
                    "portrait" => height >= width,
                    "landscape" => width > height,
                    _ => throw Unsupported($"orientation takes portrait or landscape, not '{keyword.Value}'.", context),
                };
            }
            if (feature == Feature.Grid)
            {
                if (value.Count != 1 || Integer(value[0]) is not (0 or 1))
                    throw Unsupported("grid takes 0 or 1.", context);
                return Integer(value[0]) == 0;
            }
            if (feature == Feature.Color)
                throw Unsupported("Only the boolean (color) form is supported: the colour depth Chrome reports is not modelled.", context);
            var actual = Actual(feature, context, out var ratio);
            var expected = Value(feature, value, context);
            var compare = ratio ? CompareRatio(actual, expected) : actual.Value.CompareTo(expected.Value);
            return prefix switch { -1 => compare >= 0, 1 => compare <= 0, _ => compare == 0 };
        }
        return Range(raw, context);
    }

    private static bool Touch(Context context) { context.PageArea(); return true; }

    private static Feature Known(string name, Context context)
    {
        var bare = name.StartsWith("min-", StringComparison.Ordinal) || name.StartsWith("max-", StringComparison.Ordinal) ? name[4..] : name;
        Feature? feature = bare switch
        {
            "width" => Feature.Width,
            "height" => Feature.Height,
            "aspect-ratio" => Feature.AspectRatio,
            "orientation" when bare == name => Feature.Orientation,
            "color" when bare == name => Feature.Color,
            "monochrome" => Feature.Monochrome,
            "color-index" => Feature.ColorIndex,
            "grid" when bare == name => Feature.Grid,
            _ => null,
        };
        if (feature is { } known) return known;
        if (UnmodelledFeatures.Contains(name))
            throw Unsupported($"The media feature '{name}' is not supported: its value when Chrome prints is not modelled.", context);
        throw Unsupported($"'{name}' is not a supported media feature.", context);
    }

    // A feature's print value: lengths in points, a ratio as (width, height), integers as themselves.
    private readonly record struct Number(double Value, double Denominator = 1);

    private static Number Actual(Feature feature, Context context, out bool ratio)
    {
        ratio = feature == Feature.AspectRatio;
        switch (feature)
        {
            case Feature.Width: return new(context.PageArea().Width / PointsPerPixel);
            case Feature.Height: return new(context.PageArea().Height / PointsPerPixel);
            case Feature.AspectRatio:
                var (width, height) = context.PageArea();
                return new(width, height);
            default: return new(0); // monochrome and color-index
        }
    }

    private static int CompareRatio(Number actual, Number expected)
        => (actual.Value * expected.Denominator).CompareTo(expected.Value * actual.Denominator);

    private static Number Value(Feature feature, List<CssSyntaxComponent> value, Context context)
    {
        switch (feature)
        {
            case Feature.Width or Feature.Height:
                if (value.Count != 1) throw Unsupported("A width or height media feature takes one length.", context);
                return new(Length(value[0], context));
            case Feature.AspectRatio:
                return Ratio(value, context);
            default:
                if (value.Count != 1 || Integer(value[0]) is not { } integer)
                    throw Unsupported("monochrome and color-index take a non-negative integer.", context);
                return new(integer);
        }
    }

    private static int? Integer(CssSyntaxComponent item)
        => item is CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.Number, IsInteger: true } token }
           && int.TryParse(token.Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value) && value >= 0
            ? value : null;

    // A length in CSS pixels.
    private static double Length(CssSyntaxComponent item, Context context)
    {
        if (item is CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.Number } zero } && Parse(zero.Value) == 0) return 0;
        if (item is not CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.Dimension, Unit: { } unit } token })
            throw Unsupported("A width or height media feature takes a length.", context);
        var number = Parse(token.Value);
        if (!double.IsFinite(number) || number < 0)
            throw Unsupported("Negative or non-finite lengths in media queries are not supported.", context);
        var points = unit.ToLowerInvariant() switch
        {
            "px" => number * PointsPerPixel,
            "pt" => number,
            "pc" => number * 12,
            "in" => number * 72,
            "cm" => number * 72 / 2.54,
            "mm" => number * 72 / 25.4,
            "q" => number * 72 / 101.6,
            "em" or "rem" => number * InitialFontSizePoints,
            _ => throw Unsupported($"The unit '{unit}' is not supported in media queries (supported: px, pt, pc, in, cm, mm, q, em, rem).", context),
        };
        return points / PointsPerPixel;
    }

    private static Number Ratio(List<CssSyntaxComponent> value, Context context)
    {
        double Positive(CssSyntaxComponent item)
        {
            var number = item is CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.Number } token } ? Parse(token.Value) : double.NaN;
            if (number is not (> 0 and < double.PositiveInfinity))
                throw Unsupported("aspect-ratio takes a ratio of two positive numbers.", context);
            return number;
        }
        if (value.Count == 1) return new(Positive(value[0]), 1);
        if (value.Count == 3 && value[1] is CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.Delim, Value: "/" } })
            return new(Positive(value[0]), Positive(value[2]));
        throw Unsupported("aspect-ratio takes a ratio such as 16/9.", context);
    }

    private static double Parse(string text)
        => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : double.NaN;

    // Range syntax: (name op value), (value op name) or (value lt name lt value), with <, <=, >, >= or =.
    private static bool Range(IReadOnlyList<CssSyntaxComponent> raw, Context context)
    {
        // Split into operands and operators; an operator is '<' or '>' optionally followed at once by '=', or '='.
        var parts = new List<List<CssSyntaxComponent>> { new() };
        var operators = new List<string>();
        for (var k = 0; k < raw.Count; k++)
        {
            if (raw[k] is CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.Delim, Value: "<" or ">" or "=" } delim })
            {
                var op = delim.Value;
                if (op != "=" && k + 1 < raw.Count && raw[k + 1] is CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.Delim, Value: "=" } })
                { op += "="; k++; }
                operators.Add(op);
                parts.Add([]);
                continue;
            }
            if (raw[k] is CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Whitespace }) continue;
            parts[^1].Add(raw[k]);
        }
        if (operators.Count is not (1 or 2) || parts.Any(p => p.Count == 0))
            throw Unsupported("The media feature is not a valid plain, boolean or range form.", context);
        int NameAt()
        {
            for (var p = 0; p < parts.Count; p++)
                if (parts[p].Count == 1 && parts[p][0] is CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Ident }) return p;
            return -1;
        }
        var nameAt = NameAt();
        if (nameAt < 0 || operators.Count == 2 && nameAt != 1)
            throw Unsupported("A media feature range names the feature once, between or beside its values.", context);
        var name = ((CssTokenComponent)parts[nameAt][0]).Token.Value.ToLowerInvariant();
        var feature = Known(name, context);
        if (name.StartsWith("min-", StringComparison.Ordinal) || name.StartsWith("max-", StringComparison.Ordinal)
            || feature is Feature.Orientation or Feature.Grid or Feature.Color)
            throw Unsupported($"The media feature '{name}' does not take the range syntax here.", context);
        if (operators.Count == 2 && !(operators.All(o => o[0] == '<') || operators.All(o => o[0] == '>')))
            throw Unsupported("A two-sided media feature range uses < or > on both sides.", context);
        if (operators.Count == 2 && operators.Any(o => o == "="))
            throw Unsupported("A two-sided media feature range cannot use =.", context);
        var actual = Actual(feature, context, out var ratio);
        var result = true;
        for (var o = 0; o < operators.Count; o++)
        {
            // Operator o sits between parts[o] and parts[o + 1].
            var nameOnLeft = nameAt == o;
            var expected = Value(feature, parts[nameOnLeft ? o + 1 : o], context);
            var compare = ratio ? CompareRatio(actual, expected) : actual.Value.CompareTo(expected.Value);
            // Express as "feature op value": with the feature on the right, mirror the operator.
            var op = operators[o];
            if (!nameOnLeft) op = op switch { "<" => ">", "<=" => ">=", ">" => "<", ">=" => "<=", _ => op };
            result &= op switch
            {
                "<" => compare < 0,
                "<=" => compare <= 0,
                ">" => compare > 0,
                ">=" => compare >= 0,
                _ => compare == 0,
            };
        }
        return result;
    }
}
