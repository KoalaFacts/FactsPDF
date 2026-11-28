using System.Globalization;
using FactsPDF.CssSyntax;

namespace FactsPDF;

/// <summary>What a math expression is resolved against: the font sizes em and rem stand for, the page
/// area the viewport units are percentages of, and the length 100% stands for when it is known when the
/// element is computed (font-size and line-height); with no base a percentage stays symbolic.</summary>
internal readonly record struct CssMathContext(double Em, double Rem, double ViewportWidth, double ViewportHeight, double? PercentBase);

/// <summary>
/// CSS Values 4 math functions (calc(), min(), max(), clamp()) and the viewport units (vw, vh, vmin,
/// vmax and their small, large, dynamic, inline and block spellings) for lengths, percentages and
/// numbers. An expression is folded to points when it holds no percentage with an unknown base; a
/// percentage plus a length stays one value with both parts (resolved against the containing block at
/// layout); min(), max() and clamp() that compare a percentage with a length cannot be folded before
/// layout and fail explicitly. Dividing by zero, a number mixed into a length sum, a missing operator or
/// a unit the renderer cannot compute fail with FPDF1202.
/// </summary>
internal static class CssMath
{
    private const int MaxDepth = 32;

    /// <summary>Whether a component is a math function.</summary>
    internal static bool IsMathFunction(CssSyntaxComponent component)
        => component is CssFunctionComponent function
            && function.Name.ToLowerInvariant() is "calc" or "min" or "max" or "clamp" or "-webkit-calc";

    /// <summary>Whether a token is a viewport-relative length.</summary>
    internal static bool IsViewportToken(CssSyntaxComponent component)
        => component is CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.Dimension, Unit: { } unit } } && ViewportUnit(unit) != 0;

    /// <summary>Whether any top-level component is a math function or a viewport length. A math function
    /// inside another function (a colour channel, a gradient stop) stays unsupported there.</summary>
    internal static bool Contains(IReadOnlyList<CssSyntaxComponent> values)
    {
        for (var i = 0; i < values.Count; i++)
            if (IsMathFunction(values[i]) || IsViewportToken(values[i])) return true;
        return false;
    }

    // 1 = width, 2 = height, 3 = min, 4 = max; 0 when the unit is not a viewport unit.
    private static int ViewportUnit(string unit) => unit.ToLowerInvariant() switch
    {
        "vw" or "svw" or "lvw" or "dvw" or "vi" or "svi" or "lvi" or "dvi" => 1,
        "vh" or "svh" or "lvh" or "dvh" or "vb" or "svb" or "lvb" or "dvb" => 2,
        "vmin" or "svmin" or "lvmin" or "dvmin" => 3,
        "vmax" or "svmax" or "lvmax" or "dvmax" => 4,
        _ => 0
    };

    private enum Kind { Number, Length, Percent, Mixed }

    // A value: a number, or a length-percentage as points plus percent.
    private readonly record struct Quantity(Kind Kind, double Number, double Points, double Percent);

    /// <summary>The components with every math function and viewport length replaced by the one token it
    /// resolves to: a number, a length in pt, a percentage, or a length plus percentage written
    /// <c>P%+Npt</c> (see <see cref="CssBoxValues"/>). Throws FPDF1202 for an expression that is invalid
    /// or that cannot be resolved.</summary>
    /// <param name="values">The declaration's value components.</param>
    /// <param name="context">What em, rem, the viewport units and percentages stand for.</param>
    /// <param name="property">The longhand or shorthand the values belong to; it decides whether a bare
    /// number may stand where a length is expected, and clamps ranges as the property does.</param>
    internal static List<CssSyntaxComponent> Resolve(IReadOnlyList<CssSyntaxComponent> values, in CssMathContext context, string property)
    {
        var resolved = new List<CssSyntaxComponent>(values.Count);
        foreach (var value in values) resolved.Add(ResolveComponent(value, context, property));
        return resolved;
    }

    private static CssSyntaxComponent ResolveComponent(CssSyntaxComponent component, in CssMathContext context, string property)
    {
        if (component is CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.Dimension, Unit: { } unit } token } && ViewportUnit(unit) != 0)
        {
            var quantity = Leaf(token, context, token.Span.Start);
            return Token(ClampRange(quantity, property), property, token.Span);
        }
        if (IsMathFunction(component))
        {
            var function = (CssFunctionComponent)component;
            var quantity = Function(function, context, 0);
            return Token(ClampRange(quantity, property), property, function.Span, function.Span.Start);
        }
        return component;
    }

    // Property ranges Chrome clamps a computed math result to, where the plain value would be invalid.
    private static Quantity ClampRange(Quantity quantity, string property)
        => quantity.Kind is Kind.Number or Kind.Mixed || !NonNegative(property)
            ? quantity
            : new(quantity.Kind, quantity.Number, Math.Max(0, quantity.Points), Math.Max(0, quantity.Percent));

    private static bool NonNegative(string property) => property is "width" or "min-width" or "max-width" or "height"
        or "min-height" or "max-height" or "font-size" or "flex-basis" or "column-gap" or "row-gap" or "gap"
        or "text-decoration-thickness"
        || property.StartsWith("padding", StringComparison.Ordinal) || property.StartsWith("border", StringComparison.Ordinal)
        || property.StartsWith("outline", StringComparison.Ordinal) && property != "outline-offset";

    // Properties that take lengths and percentages but no bare number: a calc() number there is invalid CSS.
    private static bool LengthOnly(string property) => NonNegative(property) || property.StartsWith("margin", StringComparison.Ordinal)
        || property is "text-indent" or "letter-spacing" or "word-spacing";

    // Properties whose value may be a bare number.
    private static bool AcceptsNumber(string property) => property is "line-height" or "flex-grow" or "flex-shrink" or "flex"
        or "orphans" or "widows" or "font-weight" or "order" or "opacity" or "z-index" or "flex-flow";

    private static CssTokenComponent Token(Quantity quantity, string property, CssSourceSpan span, int offset = -1)
    {
        var at = offset >= 0 ? offset : span.Start;
        string raw;
        CssSyntaxToken token;
        switch (quantity.Kind)
        {
            case Kind.Number:
                if (!AcceptsNumber(property))
                    throw Invalid($"A number is not a valid value of {property}: the expression has no unit.", at, cssInvalid: LengthOnly(property));
                raw = Format(quantity.Number);
                token = new(CssSyntaxTokenKind.Number, raw, raw, span, IsInteger: quantity.Number == Math.Floor(quantity.Number));
                break;
            case Kind.Length:
                raw = Format(quantity.Points) + "pt";
                token = new(CssSyntaxTokenKind.Dimension, Format(quantity.Points), raw, span, Unit: "pt");
                break;
            case Kind.Percent:
                raw = Format(quantity.Percent) + "%";
                token = new(CssSyntaxTokenKind.Percentage, Format(quantity.Percent), raw, span);
                break;
            default:
                raw = Format(quantity.Percent) + "%" + (quantity.Points < 0 ? "-" : "+") + Format(Math.Abs(quantity.Points)) + "pt";
                token = new(CssSyntaxTokenKind.Dimension, raw, raw, span, Unit: "pt");
                break;
        }
        return new CssTokenComponent(token);
    }

    private static string Format(double value) => value.ToString("0.########", CultureInfo.InvariantCulture);

    // cssInvalid marks an expression the CSS grammar or type rules reject, which a browser drops; the
    // others (division by zero, units and comparisons that depend on layout) are valid CSS this renderer
    // cannot compute, so non-strict mode keeps failing for them.
    private static FactsPdfException Invalid(string message, int offset, bool cssInvalid = true)
    {
        var error = new FactsPdfException("FPDF1202", message, offset);
        if (cssInvalid) error.Data[InvalidCssKey] = true;
        return error;
    }

    private const string InvalidCssKey = "css-invalid";

    /// <summary>Whether the values hold a math expression that is invalid CSS (so a browser drops the
    /// declaration), as opposed to a valid one this renderer cannot compute.</summary>
    internal static bool ProvenInvalid(IReadOnlyList<CssSyntaxComponent> values)
    {
        try { Resolve(values, new(12, 12, 600, 800, 12), "width"); return false; }
        catch (FactsPdfException error) { return error.Data.Contains(InvalidCssKey); }
    }

    private static Quantity Function(CssFunctionComponent function, in CssMathContext context, int depth)
    {
        if (depth > MaxDepth) throw Invalid("Math functions nest at most 32 levels.", function.Span.Start);
        var name = function.Name.ToLowerInvariant();
        var arguments = Split(function);
        switch (name)
        {
            case "calc" or "-webkit-calc":
                if (arguments.Count != 1) throw Invalid("calc() takes one expression.", function.Span.Start);
                return Sum(arguments[0], context, depth + 1);
            case "min" or "max":
            {
                var result = Sum(arguments[0], context, depth + 1);
                for (var i = 1; i < arguments.Count; i++)
                    result = Compare(result, Sum(arguments[i], context, depth + 1), name == "max", function.Span.Start);
                return result;
            }
            default:
            {
                if (arguments.Count != 3) throw Invalid("clamp() takes a minimum, a preferred value and a maximum.", function.Span.Start);
                var low = IsNone(arguments[0]) ? (Quantity?)null : Sum(arguments[0], context, depth + 1);
                var middle = Sum(arguments[1], context, depth + 1);
                var high = IsNone(arguments[2]) ? (Quantity?)null : Sum(arguments[2], context, depth + 1);
                // clamp(MIN, VAL, MAX) is max(MIN, min(VAL, MAX)).
                if (high is { } upper) middle = Compare(middle, upper, false, function.Span.Start);
                if (low is { } lower) middle = Compare(middle, lower, true, function.Span.Start);
                return middle;
            }
        }
    }

    private static bool IsNone(List<CssSyntaxComponent> argument)
        => argument.Count == 1 && argument[0] is CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.Ident, Value: var v } }
            && v.Equals("none", StringComparison.OrdinalIgnoreCase);

    // The comma-separated arguments of a function with the whitespace trimmed away at both ends.
    private static List<List<CssSyntaxComponent>> Split(CssFunctionComponent function)
        => Split(function.Values, function.Span.Start);

    private static List<List<CssSyntaxComponent>> Split(IReadOnlyList<CssSyntaxComponent> values, int offset)
    {
        var arguments = new List<List<CssSyntaxComponent>> { new() };
        foreach (var value in values)
        {
            if (value is CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Comma }) { arguments.Add([]); continue; }
            arguments[^1].Add(value);
        }
        foreach (var argument in arguments)
        {
            while (argument.Count > 0 && argument[0] is CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Whitespace }) argument.RemoveAt(0);
            while (argument.Count > 0 && argument[^1] is CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Whitespace }) argument.RemoveAt(argument.Count - 1);
            if (argument.Count == 0) throw Invalid("A math function argument is empty.", offset);
        }
        return arguments;
    }

    private static Quantity Compare(Quantity a, Quantity b, bool larger, int offset)
    {
        if (a.Kind == Kind.Number != (b.Kind == Kind.Number))
            throw Invalid("min(), max() and clamp() compare values of one type: a number and a length cannot be compared.", offset);
        if (a.Kind == Kind.Number) return larger ? (a.Number >= b.Number ? a : b) : (a.Number <= b.Number ? a : b);
        if (a.Kind == Kind.Mixed || b.Kind == Kind.Mixed || a.Kind != b.Kind)
            throw Invalid("min(), max() and clamp() that compare a percentage with a length depend on the containing block, which is known only at layout; use lengths or percentages alone.", offset, cssInvalid: false);
        var first = a.Kind == Kind.Percent ? a.Percent : a.Points;
        var second = b.Kind == Kind.Percent ? b.Percent : b.Points;
        return larger ? (first >= second ? a : b) : (first <= second ? a : b);
    }

    private static Quantity Add(Quantity a, Quantity b, bool subtract, int offset)
    {
        var sign = subtract ? -1 : 1;
        if (a.Kind == Kind.Number || b.Kind == Kind.Number)
        {
            if (a.Kind != b.Kind) throw Invalid("A sum or difference needs both sides of one type: a number cannot be added to a length or percentage.", offset);
            return new(Kind.Number, a.Number + sign * b.Number, 0, 0);
        }
        var points = a.Points + sign * b.Points;
        var percent = a.Percent + sign * b.Percent;
        var kind = a.Kind == Kind.Percent && b.Kind == Kind.Percent ? Kind.Percent
            : a.Kind == Kind.Length && b.Kind == Kind.Length ? Kind.Length : Kind.Mixed;
        return new(kind, 0, points, percent);
    }

    // sum := product ( S+ ( "+" | "-" ) S+ product )*
    private static Quantity Sum(List<CssSyntaxComponent> items, in CssMathContext context, int depth)
    {
        var at = 0;
        var result = Product(items, ref at, context, depth);
        while (at < items.Count)
        {
            var offset = items[at].Span.Start;
            var sawSpace = false;
            while (at < items.Count && items[at] is CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Whitespace }) { at++; sawSpace = true; }
            if (at >= items.Count) break;
            if (items[at] is not CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.Delim, Value: "+" or "-" } op })
                throw Invalid("A math expression needs an operator between its values (and white space around + and -).", items[at].Span.Start);
            var spaceAfter = at + 1 < items.Count && items[at + 1] is CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Whitespace };
            if (!sawSpace || !spaceAfter) throw Invalid("In a math expression + and - need white space on both sides.", op.Span.Start);
            at++;
            while (at < items.Count && items[at] is CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Whitespace }) at++;
            var right = Product(items, ref at, context, depth);
            result = Add(result, right, op.Value == "-", offset);
        }
        return result;
    }

    // product := value ( S* ( "*" | "/" ) S* value )*
    private static Quantity Product(List<CssSyntaxComponent> items, ref int at, in CssMathContext context, int depth)
    {
        var result = Value(items, ref at, context, depth);
        while (true)
        {
            var save = at;
            while (at < items.Count && items[at] is CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Whitespace }) at++;
            if (at >= items.Count || items[at] is not CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.Delim, Value: "*" or "/" } op })
            { at = save; return result; }
            at++;
            while (at < items.Count && items[at] is CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Whitespace }) at++;
            var right = Value(items, ref at, context, depth);
            if (op.Value == "*")
            {
                if (result.Kind != Kind.Number && right.Kind != Kind.Number)
                    throw Invalid("A product needs a number on at least one side.", op.Span.Start);
                result = result.Kind == Kind.Number
                    ? new(right.Kind, result.Number * right.Number, result.Number * right.Points, result.Number * right.Percent)
                    : new(result.Kind, result.Number * right.Number, result.Points * right.Number, result.Percent * right.Number);
            }
            else
            {
                if (right.Kind != Kind.Number)
                    throw Invalid("A quotient needs a number on the right.", op.Span.Start);
                // Chrome computes a division by zero as infinity, which no page can lay out.
                if (right.Number == 0)
                    throw Invalid("Division by zero in a math expression: Chrome computes infinity, which cannot be laid out.", op.Span.Start, cssInvalid: false);
                result = new(result.Kind, result.Number / right.Number, result.Points / right.Number, result.Percent / right.Number);
            }
        }
    }

    private static Quantity Value(List<CssSyntaxComponent> items, ref int at, in CssMathContext context, int depth)
    {
        if (at >= items.Count) throw Invalid("A math expression is missing a value.", items.Count > 0 ? items[^1].Span.Start : 0);
        var item = items[at++];
        switch (item)
        {
            case CssTokenComponent { Token: var token }:
                return Leaf(token, context, token.Span.Start);
            case CssFunctionComponent function when IsMathFunction(function):
                return Function(function, context, depth);
            case CssBlockComponent { Opening: CssSyntaxTokenKind.OpenParen } block:
                if (depth > MaxDepth) throw Invalid("Math expressions nest at most 32 levels.", block.Span.Start);
                return Sum(Trim(block.Values), context, depth + 1);
            default:
                throw Invalid("This value cannot appear in a math expression.", item.Span.Start);
        }
    }

    private static List<CssSyntaxComponent> Trim(IReadOnlyList<CssSyntaxComponent> values)
    {
        var list = new List<CssSyntaxComponent>(values);
        while (list.Count > 0 && list[0] is CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Whitespace }) list.RemoveAt(0);
        while (list.Count > 0 && list[^1] is CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Whitespace }) list.RemoveAt(list.Count - 1);
        if (list.Count == 0) throw Invalid("Parentheses in a math expression are empty.", values.Count > 0 ? values[0].Span.Start : 0);
        return list;
    }

    private static Quantity Leaf(CssSyntaxToken token, in CssMathContext context, int offset)
    {
        switch (token.Kind)
        {
            case CssSyntaxTokenKind.Number:
                return new(Kind.Number, ParseNumber(token.Value, offset), 0, 0);
            case CssSyntaxTokenKind.Percentage:
            {
                var percent = ParseNumber(token.Value, offset);
                return context.PercentBase is { } basis ? new(Kind.Length, 0, basis * percent / 100, 0) : new(Kind.Percent, 0, 0, percent);
            }
            case CssSyntaxTokenKind.Dimension:
            {
                var value = ParseNumber(token.Value, offset);
                var unit = token.Unit!.ToLowerInvariant();
                if (ViewportUnit(unit) is var viewport and not 0)
                {
                    var (w, h) = (context.ViewportWidth, context.ViewportHeight);
                    var basis = viewport switch { 1 => w, 2 => h, 3 => Math.Min(w, h), _ => Math.Max(w, h) };
                    return new(Kind.Length, 0, value * basis / 100, 0);
                }
                var scale = unit switch
                {
                    "pt" => 1d,
                    "px" => 0.75,
                    "pc" => 12d,
                    "in" => 72d,
                    "cm" => 72d / 2.54,
                    "mm" => 72d / 25.4,
                    "q" => 72d / 25.4 / 4,
                    "em" => context.Em,
                    "rem" => context.Rem,
                    _ => 0d
                };
                if (scale == 0)
                    throw Invalid($"The unit '{token.Unit}' cannot be computed in a math expression (supported: pt, px, pc, in, cm, mm, q, em, rem, vw, vh, vmin, vmax and their small/large/dynamic variants).", offset, cssInvalid: false);
                return new(Kind.Length, 0, value * scale, 0);
            }
            case CssSyntaxTokenKind.Ident:
                return token.Value.ToLowerInvariant() switch
                {
                    "pi" => new(Kind.Number, Math.PI, 0, 0),
                    "e" => new(Kind.Number, Math.E, 0, 0),
                    "infinity" or "-infinity" or "nan" => throw Invalid($"'{token.Value}' is not supported in a math expression (pi and e are).", offset, cssInvalid: false),
                    _ => throw Invalid($"'{token.Value}' is not a math expression value.", offset)
                };
            default:
                throw Invalid("This value cannot appear in a math expression.", offset);
        }
    }

    private static double ParseNumber(string text, int offset)
    {
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value))
            throw Invalid("A math expression number is not finite.", offset);
        return value;
    }
}
