using System.Globalization;
using FactsPDF.CssSyntax;

namespace FactsPDF;

/// <summary>
/// Whether a transform value is the identity: a transform function list (or one of the individual
/// transform properties) whose every function leaves the box where it is. The renderer lays out no
/// transform, but an identity one (a spinner's <c>rotate(0deg)</c> keyframe, <c>translateX(0)</c>,
/// <c>scale(1)</c>) paints exactly as none does.
/// </summary>
internal static class CssTransforms
{
    /// <summary>Whether the declaration of a transform property holds only identity transforms.</summary>
    internal static bool IsIdentity(string property, IReadOnlyList<CssSyntaxComponent> values)
    {
        if (property is not ("transform" or "-webkit-transform" or "rotate" or "scale" or "translate")) return false;
        var any = false;
        foreach (var value in values)
        {
            if (value is CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Whitespace }) continue;
            if (value is CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.Ident } word })
            {
                if (!word.Value.Equals("none", StringComparison.OrdinalIgnoreCase)) return false;
                any = true;
                continue;
            }
            if (value is not CssFunctionComponent function || !IdentityFunction(function)) return false;
            any = true;
        }
        return any;
    }

    private static bool IdentityFunction(CssFunctionComponent function)
    {
        var scale = function.Name.ToLowerInvariant() switch
        {
            "rotate" or "rotatex" or "rotatey" or "rotatez" or "skew" or "skewx" or "skewy"
                or "translate" or "translatex" or "translatey" or "translatez" => 0d,
            "scale" or "scalex" or "scaley" or "scalez" => 1d,
            _ => double.NaN
        };
        if (double.IsNaN(scale)) return false;
        var seen = 0;
        foreach (var argument in function.Values)
        {
            if (argument is CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Whitespace or CssSyntaxTokenKind.Comma }) continue;
            if (argument is not CssTokenComponent { Token: var token }) return false;
            if (!double.TryParse(token.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)) return false;
            if (token.Kind == CssSyntaxTokenKind.Percentage)
            {
                // A scale of 100% is the identity; a length or angle is zero only at 0%.
                if (scale == 1) number /= 100;
            }
            else if (token.Kind == CssSyntaxTokenKind.Dimension && function.Name.StartsWith("rotate", StringComparison.OrdinalIgnoreCase))
            {
                // A whole number of turns is the identity.
                var degrees = token.Unit?.ToLowerInvariant() switch
                {
                    "deg" => number, "grad" => number * 0.9, "turn" => number * 360, "rad" => number * 180 / Math.PI, _ => double.NaN
                };
                if (double.IsNaN(degrees) || Math.Abs(degrees % 360) > 1e-9) return false;
                number = 0;
            }
            else if (token.Kind is not (CssSyntaxTokenKind.Number or CssSyntaxTokenKind.Dimension)) return false;
            if (number != scale) return false;
            seen++;
        }
        return seen > 0;
    }
}
