using System.Globalization;
using System.Text;
using FactsPDF.CssSyntax;

namespace FactsPDF;

/// <summary>
/// The computed paint effects of a box: opacity, transform, transform-origin and box-shadow. None is
/// inherited. A box with opacity below 1 or any transform other than none creates a stacking context
/// (see <see cref="EffectRegion"/>); the shadows are painted with the box's background.
/// </summary>
internal sealed record PaintEffects(double Opacity, CssTransformFunction[]? Transform, TransformOrigin Origin, BoxShadow[]? Shadows)
{
    internal static readonly PaintEffects None = new(1, null, TransformOrigin.Initial, null);

    /// <summary>The opacity quantised to a byte, as Chrome's PDF backend stores a layer's alpha.</summary>
    internal byte Alpha => (byte)Math.Round(Math.Clamp(Opacity, 0, 1) * 255, MidpointRounding.AwayFromZero);

    /// <summary>Whether the box paints as its own stacking context (CSS Color 4 opacity, CSS Transforms 1).</summary>
    internal bool CreatesStackingContext => Opacity < 1 || Transform is not null;
}

/// <summary>transform-origin: X and Y are lengths in points or percentages of the border box; Z is a length.
/// UnsupportedOffset is the source offset of a valid value whose unit the renderer cannot resolve (ex, ch,
/// viewport units), or -1; it fails only when the box also has a transform.</summary>
internal readonly record struct TransformOrigin(CssLength X, CssLength Y, double Z, int UnsupportedOffset = -1)
{
    internal static TransformOrigin Initial => new(new CssLength(50, true), new CssLength(50, true), 0);
}

/// <summary>One box-shadow layer without blur: offsets and spread in points and its colour.</summary>
internal sealed record BoxShadow(bool Inset, double X, double Y, double Spread, Rgb Color);

internal enum TransformArgKind : byte { Number, Length, Percent, Angle }

/// <summary>A transform function argument: a number, a length in points, a percentage or an angle in degrees.</summary>
internal readonly record struct TransformArg(TransformArgKind Kind, double Value);

/// <summary>One transform function with its resolved arguments.</summary>
internal sealed record CssTransformFunction(string Name, TransformArg[] Args);

/// <summary>Parsing, computing and resolving the paint effect properties.</summary>
internal static class CssEffects
{
    private const string Invalid = "FPDF1202";

    private static bool Wide(string text) => text is "inherit" or "initial" or "unset" or "revert" or "revert-layer";

    private static List<CssSyntaxComponent> Components(IReadOnlyList<CssSyntaxComponent> values)
    {
        var list = new List<CssSyntaxComponent>(values.Count);
        foreach (var value in values)
            if (value is not CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Whitespace }) list.Add(value);
        return list;
    }

    private static string? WideKeyword(List<CssSyntaxComponent> components) =>
        components.Count == 1 && components[0] is CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.Ident } word }
            && Wide(word.Value.ToLowerInvariant()) ? word.Value.ToLowerInvariant() : null;

    // The term a token stands for, lower-cased: a number, a dimension with its unit or a percentage.
    private static string Term(CssSyntaxToken token) => token.Kind switch
    {
        CssSyntaxTokenKind.Number => token.Value,
        CssSyntaxTokenKind.Dimension => (token.Value + token.Unit).ToLowerInvariant(),
        CssSyntaxTokenKind.Percentage => token.Value + "%",
        CssSyntaxTokenKind.Ident => token.Value.ToLowerInvariant(),
        CssSyntaxTokenKind.Hash => "#" + token.Value.ToLowerInvariant(),
        _ => throw new FactsPdfException(Invalid, "Unsupported CSS value token.", token.Span.Start)
    };

    // ---- opacity -------------------------------------------------------------------------------

    /// <summary>The normalised value of a validated opacity declaration: a CSS-wide keyword, or the
    /// number (a percentage divided by 100) clamped to [0, 1] as CSS Color 4 computes it.</summary>
    internal static string CompileOpacity(CssDeclarationNode node)
    {
        var components = Components(node.Values);
        if (WideKeyword(components) is { } keyword) return keyword;
        if (components.Count == 1 && components[0] is CssTokenComponent { Token: var token }
            && token.Kind is CssSyntaxTokenKind.Number or CssSyntaxTokenKind.Percentage
            && double.TryParse(token.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number))
        {
            if (token.Kind == CssSyntaxTokenKind.Percentage) number /= 100;
            return Math.Clamp(number, 0, 1).ToString("R", CultureInfo.InvariantCulture);
        }
        throw new FactsPdfException(Invalid, "opacity takes one number or percentage.", node.Span.Start);
    }

    internal static double Opacity(string value) => double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);

    // ---- transform -----------------------------------------------------------------------------

    /// <summary>The normalised value of a transform declaration: none, a CSS-wide keyword, or the function
    /// list as <c>name(arg,arg) name(arg)</c> with lower-cased terms. The list is validated here.</summary>
    internal static string CompileTransform(CssDeclarationNode node)
    {
        var components = Components(node.Values);
        if (WideKeyword(components) is { } keyword) return keyword;
        if (components.Count == 1 && components[0] is CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.Ident } none }
            && none.Value.Equals("none", StringComparison.OrdinalIgnoreCase))
            return "none";
        if (components.Count == 0) throw new FactsPdfException(Invalid, "Empty CSS declaration.", node.Span.Start);
        var text = new StringBuilder();
        foreach (var component in components)
        {
            if (component is not CssFunctionComponent function)
                throw new FactsPdfException(Invalid, "transform takes none or a list of transform functions.", component.Span.Start);
            if (text.Length > 0) text.Append(' ');
            text.Append(function.Name.ToLowerInvariant()).Append('(');
            var expectArgument = true;
            foreach (var argument in function.Values)
            {
                if (argument is CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Whitespace }) continue;
                if (argument is CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Comma })
                {
                    if (expectArgument) throw new FactsPdfException(Invalid, $"Invalid {function.Name}() arguments.", argument.Span.Start);
                    text.Append(','); expectArgument = true;
                    continue;
                }
                if (!expectArgument || argument is not CssTokenComponent { Token: var token }
                    || token.Kind is not (CssSyntaxTokenKind.Number or CssSyntaxTokenKind.Dimension or CssSyntaxTokenKind.Percentage
                        or CssSyntaxTokenKind.Ident))
                    throw new FactsPdfException(Invalid,
                        $"{function.Name}() takes comma-separated numbers, lengths, percentages or angles; nested functions are not supported.", argument.Span.Start);
                text.Append(Term(token)); expectArgument = false;
            }
            if (expectArgument) throw new FactsPdfException(Invalid, $"Invalid {function.Name}() arguments.", function.Span.Start);
            text.Append(')');
        }
        var value = text.ToString();
        _ = Transform(value, node.Span.Start, 16, 16);
        return value;
    }

    /// <summary>The functions of a normalised transform list, with em and rem resolved; null for none.</summary>
    internal static CssTransformFunction[]? Transform(string value, int offset, double rootFontSize, double elementFontSize)
    {
        if (value == "none") return null;
        var parts = value.Split(' ');
        var functions = new CssTransformFunction[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            var open = part.IndexOf('(');
            if (open <= 0 || part[^1] != ')') throw new FactsPdfException(Invalid, "Invalid transform function.", offset);
            var name = part[..open];
            var inner = part[(open + 1)..^1];
            // perspective(none) is the identity.
            if (name == "perspective" && inner == "none") { functions[i] = new(name, []); continue; }
            string[] terms = inner.Length == 0 ? [] : inner.Split(',');
            var args = new TransformArg[terms.Length];
            for (var j = 0; j < terms.Length; j++) args[j] = Argument(terms[j], offset, rootFontSize, elementFontSize);
            CheckFunction(name, args, offset);
            functions[i] = new(name, args);
        }
        return functions;
    }

    private static TransformArg Argument(string term, int offset, double rootFontSize, double elementFontSize)
    {
        if (double.TryParse(term, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            if (!double.IsFinite(number)) throw new FactsPdfException(Invalid, "Transform numbers must be finite.", offset);
            return new(TransformArgKind.Number, number);
        }
        var negative = term.StartsWith('-');
        var magnitude = negative || term.StartsWith('+') ? term[1..] : term;
        var unitStart = 0;
        while (unitStart < magnitude.Length && (char.IsAsciiDigit(magnitude[unitStart]) || magnitude[unitStart] is '.' or 'e' or 'E'
            || magnitude[unitStart] is '+' or '-' && unitStart > 0 && magnitude[unitStart - 1] is 'e' or 'E'))
            unitStart++;
        // An exponent letter followed by no digit is the start of a unit (1em, 2ex).
        while (unitStart > 0 && magnitude[unitStart - 1] is 'e' or 'E') unitStart--;
        var unit = magnitude[unitStart..];
        if (!double.TryParse(magnitude.AsSpan(0, unitStart), NumberStyles.Float, CultureInfo.InvariantCulture, out var amount)
            || !double.IsFinite(amount))
            throw new FactsPdfException(Invalid, $"Invalid transform argument '{term}'.", offset);
        var sign = negative ? -1 : 1;
        double? degrees = unit switch
        {
            "deg" => amount, "grad" => amount * 0.9, "rad" => amount * 180 / Math.PI, "turn" => amount * 360, _ => null
        };
        if (degrees is { } angle) return new(TransformArgKind.Angle, sign * angle);
        if (unit == "%") return new(TransformArgKind.Percent, sign * amount);
        if (!CssBoxValues.TryLength(magnitude, out var length, out _, rootFontSize, allowRem: true, elementFontSize, allowEm: true)
            || length.IsPercent)
            throw new FactsPdfException(Invalid,
                $"Transform length '{term}' needs pt, px, mm, cm, in, pc, q, em or rem; other units are not supported.", offset);
        return new(TransformArgKind.Length, sign * length.Value);
    }

    // The argument count and kinds of each transform function (CSS Transforms 1 and 2). A bare zero
    // stands for a zero length or angle.
    private static void CheckFunction(string name, TransformArg[] args, int offset)
    {
        static bool Zero(TransformArg a) => a.Kind == TransformArgKind.Number && a.Value == 0;
        static bool Number(TransformArg a) => a.Kind == TransformArgKind.Number;
        static bool Scale(TransformArg a) => a.Kind is TransformArgKind.Number or TransformArgKind.Percent;
        static bool Angle(TransformArg a) => a.Kind == TransformArgKind.Angle || Zero(a);
        static bool Length(TransformArg a) => a.Kind == TransformArgKind.Length || Zero(a);
        static bool LengthPercent(TransformArg a) => a.Kind is TransformArgKind.Length or TransformArgKind.Percent || Zero(a);
        var valid = name switch
        {
            "matrix" => args.Length == 6 && args.All(Number),
            "matrix3d" => args.Length == 16 && args.All(Number),
            "translate" => args.Length is 1 or 2 && args.All(LengthPercent),
            "translatex" or "translatey" => args.Length == 1 && LengthPercent(args[0]),
            "translatez" => args.Length == 1 && Length(args[0]),
            "translate3d" => args.Length == 3 && LengthPercent(args[0]) && LengthPercent(args[1]) && Length(args[2]),
            "scale" => args.Length is 1 or 2 && args.All(Scale),
            "scalex" or "scaley" or "scalez" => args.Length == 1 && Scale(args[0]),
            "scale3d" => args.Length == 3 && args.All(Scale),
            "rotate" or "rotatex" or "rotatey" or "rotatez" => args.Length == 1 && Angle(args[0]),
            "rotate3d" => args.Length == 4 && Number(args[0]) && Number(args[1]) && Number(args[2]) && Angle(args[3]),
            "skew" => args.Length is 1 or 2 && args.All(Angle),
            "skewx" or "skewy" => args.Length == 1 && Angle(args[0]),
            "perspective" => args.Length == 0 || args.Length == 1 && Length(args[0]) && args[0].Value >= 0,
            _ => false
        };
        if (!valid)
            throw new FactsPdfException(Invalid, $"Invalid or unsupported transform function {name}() or arguments.", offset);
    }

    /// <summary>
    /// The transform of a box as a top-down page matrix and as PDF cm operands. The function list is
    /// composed left to right about the transform-origin (CSS Transforms 1, section 6) in 4x4 matrices and
    /// flattened as Chrome prints a 3D transform without perspective: the orthographic projection onto the
    /// page (z dropped). A transform whose projection is not affine (perspective acting on a 3D rotation)
    /// and one that turns the box's back face to the viewer fail.
    /// </summary>
    internal static (PdfMatrix TopDown, PdfMatrix Pdf) Resolve(PaintEffects effects, double x, double top, double width,
        double height, double pageHeight, int offset)
    {
        var origin = effects.Origin;
        if (origin.UnsupportedOffset >= 0)
            throw new FactsPdfException(Invalid,
                "transform-origin in ex, ch or viewport units is not supported on a transformed box; use px, pt, em, rem or a percentage.",
                origin.UnsupportedOffset);
        var ox = x + origin.X.Resolve(width);
        var oy = top + origin.Y.Resolve(height);
        var m = Translation(ox, oy, origin.Z);
        foreach (var function in effects.Transform!) m = Multiply(m, Matrix(function, width, height));
        m = Multiply(m, Translation(-ox, -oy, -origin.Z));
        // Row 3 maps a point of the page plane (z = 0) to w; anything but w = 1 is a perspective projection.
        if (Math.Abs(m[12]) > 1e-12 || Math.Abs(m[13]) > 1e-12 || Math.Abs(m[15] - 1) > 1e-9)
            throw new FactsPdfException(Invalid,
                "A transform whose perspective acts on a 3D rotation is not supported; Chrome prints it as a projection that PDF cannot express exactly.", offset);
        var a = m[0]; var b = m[4]; var c = m[1]; var d = m[5]; var e = m[3]; var f = m[7];
        var cofactor = a * d - b * c;
        var det = a * (m[5] * m[10] - m[6] * m[9]) - m[1] * (m[4] * m[10] - m[6] * m[8]) + m[2] * (m[4] * m[9] - m[5] * m[8]);
        if (Math.Abs(cofactor) > 1e-12 && Math.Abs(det) > 1e-12 && cofactor / det < 0)
            throw new FactsPdfException("FPDF1201",
                "A 3D transform that turns the box's back face to the viewer is not supported (backface-visibility is not modelled).", offset);
        var topDown = new PdfMatrix(a, b, c, d, e, f);
        var pdf = new PdfMatrix(a, -b, -c, d, c * pageHeight + e, pageHeight - d * pageHeight - f);
        return (topDown, pdf);
    }

    private static double[] Identity() => [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1];

    private static double[] Translation(double x, double y, double z)
    {
        var m = Identity();
        m[3] = x; m[7] = y; m[11] = z;
        return m;
    }

    // Row-major 4x4 product (column vectors: the right-hand matrix applies first).
    private static double[] Multiply(double[] l, double[] r)
    {
        var p = new double[16];
        for (var row = 0; row < 4; row++)
            for (var col = 0; col < 4; col++)
            {
                var sum = 0d;
                for (var k = 0; k < 4; k++) sum += l[row * 4 + k] * r[k * 4 + col];
                p[row * 4 + col] = sum;
            }
        return p;
    }

    private static double Radians(TransformArg angle) => angle.Value * Math.PI / 180;

    private static double ScaleOf(TransformArg arg) => arg.Kind == TransformArgKind.Percent ? arg.Value / 100 : arg.Value;

    // A translation component: a length in points, or a percentage of the border box size on its axis.
    private static double Offset(TransformArg arg, double size) => arg.Kind == TransformArgKind.Percent ? size * arg.Value / 100 : arg.Value;

    private static double[] Matrix(CssTransformFunction function, double width, double height)
    {
        var args = function.Args;
        var m = Identity();
        switch (function.Name)
        {
            case "matrix":
                // matrix(a, b, c, d, e, f): e and f are px.
                m[0] = args[0].Value; m[4] = args[1].Value; m[1] = args[2].Value; m[5] = args[3].Value;
                m[3] = args[4].Value * 0.75; m[7] = args[5].Value * 0.75;
                break;
            case "matrix3d":
                // Column-major arguments; the translation is in px and the perspective row per px.
                for (var col = 0; col < 4; col++)
                    for (var row = 0; row < 4; row++)
                    {
                        var v = args[col * 4 + row].Value;
                        if (col == 3 && row < 3) v *= 0.75;
                        if (row == 3 && col < 3) v /= 0.75;
                        m[row * 4 + col] = v;
                    }
                break;
            case "translate":
                m[3] = Offset(args[0], width);
                m[7] = args.Length == 2 ? Offset(args[1], height) : 0;
                break;
            case "translatex": m[3] = Offset(args[0], width); break;
            case "translatey": m[7] = Offset(args[0], height); break;
            case "translatez": m[11] = args[0].Value; break;
            case "translate3d": m[3] = Offset(args[0], width); m[7] = Offset(args[1], height); m[11] = args[2].Value; break;
            case "scale":
                m[0] = ScaleOf(args[0]);
                m[5] = args.Length == 2 ? ScaleOf(args[1]) : m[0];
                break;
            case "scalex": m[0] = ScaleOf(args[0]); break;
            case "scaley": m[5] = ScaleOf(args[0]); break;
            case "scalez": m[10] = ScaleOf(args[0]); break;
            case "scale3d": m[0] = ScaleOf(args[0]); m[5] = ScaleOf(args[1]); m[10] = ScaleOf(args[2]); break;
            case "rotate" or "rotatez": return Rotation(0, 0, 1, Radians(args[0]));
            case "rotatex": return Rotation(1, 0, 0, Radians(args[0]));
            case "rotatey": return Rotation(0, 1, 0, Radians(args[0]));
            case "rotate3d": return Rotation(args[0].Value, args[1].Value, args[2].Value, Radians(args[3]));
            case "skew":
                m[1] = Math.Tan(Radians(args[0]));
                m[4] = args.Length == 2 ? Math.Tan(Radians(args[1])) : 0;
                break;
            case "skewx": m[1] = Math.Tan(Radians(args[0])); break;
            case "skewy": m[4] = Math.Tan(Radians(args[0])); break;
            case "perspective":
                // perspective(0) is clamped to 1px (CSS Transforms 2); perspective(none) has no argument.
                if (args.Length == 0) break;
                m[14] = -1 / Math.Max(args[0].Value, 0.75);
                break;
            default:
                throw new InvalidOperationException("Unvalidated transform function.");
        }
        return m;
    }

    // rotate3d(x, y, z, a) as CSS Transforms 2 defines it; a zero vector rotates nothing.
    private static double[] Rotation(double x, double y, double z, double angle)
    {
        var m = Identity();
        var length = Math.Sqrt(x * x + y * y + z * z);
        if (length == 0) return m;
        x /= length; y /= length; z /= length;
        var sc = Math.Sin(angle / 2) * Math.Cos(angle / 2);
        var sq = Math.Sin(angle / 2) * Math.Sin(angle / 2);
        // m[row * 4 + col] = CSS m{col+1}{row+1}.
        m[0] = 1 - 2 * (y * y + z * z) * sq;
        m[4] = 2 * (x * y * sq + z * sc);
        m[8] = 2 * (x * z * sq - y * sc);
        m[1] = 2 * (x * y * sq - z * sc);
        m[5] = 1 - 2 * (x * x + z * z) * sq;
        m[9] = 2 * (y * z * sq + x * sc);
        m[2] = 2 * (x * z * sq + y * sc);
        m[6] = 2 * (y * z * sq - x * sc);
        m[10] = 1 - 2 * (x * x + y * y) * sq;
        // Exact zeros for the quarter turns, so rotate(90deg) writes 0 rather than 6e-17.
        for (var i = 0; i < 11; i++) if (Math.Abs(m[i]) < 1e-15) m[i] = 0;
        return m;
    }

    // ---- transform-origin ----------------------------------------------------------------------

    /// <summary>The computed transform-origin of a validated value (one to three terms joined by spaces).</summary>
    internal static TransformOrigin Origin(string value, int offset, double rootFontSize, double elementFontSize)
    {
        var terms = value.Split(' ');
        CssLength? x = null, y = null;
        var z = 0d;
        var unsupported = -1;
        CssLength Resolve(string term)
        {
            switch (term)
            {
                case "left" or "top": return new CssLength(0, true);
                case "center": return new CssLength(50, true);
                case "right" or "bottom": return new CssLength(100, true);
            }
            var negative = term.StartsWith('-');
            var magnitude = negative || term.StartsWith('+') ? term[1..] : term;
            if (!CssBoxValues.TryLength(magnitude, out var length, out _, rootFontSize, allowRem: true, elementFontSize, allowEm: true))
            {
                unsupported = offset;
                return default;
            }
            return negative ? length with { Value = -length.Value } : length;
        }
        var first = terms[0];
        var vertical = first is "top" or "bottom";
        if (terms.Length == 1)
        {
            if (vertical) { x = new CssLength(50, true); y = Resolve(first); }
            else { x = Resolve(first); y = new CssLength(50, true); }
        }
        else
        {
            var second = terms[1];
            // A vertical keyword first, or a horizontal keyword second, names the axes the other way round.
            if (vertical || second is "left" or "right") { x = Resolve(second); y = Resolve(first); }
            else { x = Resolve(first); y = Resolve(second); }
            if (terms.Length == 3)
            {
                var third = Resolve(terms[2]);
                z = third.IsPercent ? 0 : third.Value;
            }
        }
        return new TransformOrigin(x!.Value, y!.Value, z, unsupported);
    }

    // ---- box-shadow ----------------------------------------------------------------------------

    /// <summary>
    /// The normalised value of a box-shadow declaration whose colours are already lowered to hex terms:
    /// none, a CSS-wide keyword, or the layers joined by commas as <c>[inset ]x y spread colour</c>
    /// (the colour defaults to currentcolor). A layer with a blur radius above zero fails: Chrome paints
    /// it as a Gaussian blur, which is not drawn.
    /// </summary>
    internal static string CompileShadows(CssDeclarationNode node)
    {
        var components = Components(node.Values);
        if (WideKeyword(components) is { } keyword) return keyword;
        if (components.Count == 1 && components[0] is CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.Ident } none }
            && none.Value.Equals("none", StringComparison.OrdinalIgnoreCase))
            return "none";
        var text = new StringBuilder();
        var layer = new List<CssSyntaxToken>();
        void Flush(int at)
        {
            if (layer.Count == 0) throw new FactsPdfException(Invalid, "box-shadow has an empty layer.", at);
            var inset = false; string? color = null; var lengths = new List<string>(4);
            var lengthsDone = false;
            foreach (var token in layer)
            {
                var term = Term(token);
                if (token.Kind == CssSyntaxTokenKind.Ident && term == "inset" && !inset) { inset = true; if (lengths.Count > 0) lengthsDone = true; continue; }
                if (token.Kind is CssSyntaxTokenKind.Number or CssSyntaxTokenKind.Dimension && !lengthsDone)
                {
                    if (token.Kind == CssSyntaxTokenKind.Number && !CssBoxValues.IsExactlyUnitlessZero(term))
                        throw new FactsPdfException(Invalid, "box-shadow lengths need a unit (or are 0).", token.Span.Start);
                    lengths.Add(token.Kind == CssSyntaxTokenKind.Number ? "0" : term);
                    continue;
                }
                if (lengths.Count > 0) lengthsDone = true;
                if (color is null && token.Kind is CssSyntaxTokenKind.Hash or CssSyntaxTokenKind.Ident)
                {
                    if (term != "currentcolor") _ = InlineCss.Color(term, token.Span.Start);
                    color = term;
                    continue;
                }
                throw new FactsPdfException(Invalid, "box-shadow layers take inset, two to four lengths and a colour.", token.Span.Start);
            }
            if (lengths.Count is < 2 or > 4)
                throw new FactsPdfException(Invalid, "box-shadow layers take two to four lengths.", layer[0].Span.Start);
            if (lengths.Count >= 3)
            {
                if (lengths[2].StartsWith('-'))
                    throw new FactsPdfException(Invalid, "box-shadow blur radius must not be negative.", layer[0].Span.Start);
                if (SignedLength(lengths[2], layer[0].Span.Start, 16, 16) != 0)
                    throw new FactsPdfException(Invalid,
                        "A blurred box-shadow (blur radius above 0) is not supported; Chrome paints it as a Gaussian blur.", layer[0].Span.Start);
            }
            foreach (var length in lengths) _ = SignedLength(length, layer[0].Span.Start, 16, 16);
            if (text.Length > 0) text.Append(',');
            if (inset) text.Append("inset ");
            text.Append(lengths[0]).Append(' ').Append(lengths[1]).Append(' ').Append(lengths.Count == 4 ? lengths[3] : "0")
                .Append(' ').Append(color ?? "currentcolor");
            layer.Clear();
        }
        foreach (var component in components)
        {
            if (component is not CssTokenComponent { Token: var token })
                throw new FactsPdfException(Invalid, "box-shadow supports lengths, inset and colours only.", component.Span.Start);
            if (token.Kind == CssSyntaxTokenKind.Comma) { Flush(token.Span.Start); continue; }
            layer.Add(token);
        }
        Flush(node.Span.Start);
        return text.ToString();
    }

    /// <summary>The visible layers of a normalised box-shadow value, in paint order (the first layer on
    /// top), with currentcolor resolved; null when none would paint.</summary>
    internal static BoxShadow[]? Shadows(string value, int offset, double rootFontSize, double elementFontSize, Rgb elementColor)
    {
        if (value == "none") return null;
        var layers = value.Split(',');
        var shadows = new List<BoxShadow>(layers.Length);
        foreach (var layer in layers)
        {
            var terms = layer.Split(' ');
            var inset = terms[0] == "inset";
            var at = inset ? 1 : 0;
            var color = terms[at + 3] == "currentcolor" ? elementColor : InlineCss.Color(terms[at + 3], offset);
            if (color.IsInvisible) continue;
            shadows.Add(new BoxShadow(inset, SignedLength(terms[at], offset, rootFontSize, elementFontSize),
                SignedLength(terms[at + 1], offset, rootFontSize, elementFontSize),
                SignedLength(terms[at + 2], offset, rootFontSize, elementFontSize), color));
        }
        return shadows.Count == 0 ? null : [.. shadows];
    }

    private static double SignedLength(string term, int offset, double rootFontSize, double elementFontSize)
    {
        var negative = term.StartsWith('-');
        var magnitude = negative || term.StartsWith('+') ? term[1..] : term;
        if (!CssBoxValues.TryLength(magnitude, out var length, out var error, rootFontSize, allowRem: true, elementFontSize, allowEm: true)
            || length.IsPercent)
            throw new FactsPdfException(Invalid, error ?? "box-shadow lengths cannot be percentages.", offset);
        return negative ? -length.Value : length.Value;
    }
}
