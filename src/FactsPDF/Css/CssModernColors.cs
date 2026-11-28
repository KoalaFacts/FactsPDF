namespace FactsPDF;

/// <summary>
/// The CSS Color 4 functions lab(), lch(), oklab(), oklch() and color() converted to sRGB the way
/// Chrome paints them into a PDF: through CIE XYZ (Bradford-adapted between D50 and D65) to linear
/// sRGB, encoded with the sRGB transfer curve, and clipped channel by channel to the sRGB gamut (no
/// chroma reduction), so an out-of-gamut colour prints as its clipped sRGB value.
/// </summary>
internal static class CssModernColors
{
    /// <summary>The predefined color spaces of color().</summary>
    internal enum Space { Srgb, SrgbLinear, DisplayP3, A98Rgb, ProphotoRgb, Rec2020, XyzD50, XyzD65 }

    /// <summary>Resolves a color() space name (ASCII case-insensitive), or false for a name that
    /// is not predefined (dashed names need @color-profile).</summary>
    internal static bool TrySpace(string name, out Space space)
    {
        switch (name.ToLowerInvariant())
        {
            case "srgb": space = Space.Srgb; return true;
            case "srgb-linear": space = Space.SrgbLinear; return true;
            case "display-p3": space = Space.DisplayP3; return true;
            case "a98-rgb": space = Space.A98Rgb; return true;
            case "prophoto-rgb": space = Space.ProphotoRgb; return true;
            case "rec2020": space = Space.Rec2020; return true;
            case "xyz-d50": space = Space.XyzD50; return true;
            case "xyz" or "xyz-d65": space = Space.XyzD65; return true;
            default: space = default; return false;
        }
    }

    // CIE D50 white point as XYZ (CSS Color 4: x = 0.3457, y = 0.3585).
    private const double WhiteX = 0.3457 / 0.3585, WhiteZ = (1 - 0.3457 - 0.3585) / 0.3585;
    private const double Epsilon = 216d / 24389, Kappa = 24389d / 27;

    /// <summary>CIE Lab (D50) to sRGB bytes. L is clamped to 0..100.</summary>
    internal static (double R, double G, double B) FromLab(double l, double a, double b)
    {
        l = Math.Clamp(l, 0, 100);
        var fy = (l + 16) / 116;
        var fx = fy + a / 500;
        var fz = fy - b / 200;
        var x = (fx * fx * fx > Epsilon ? fx * fx * fx : (116 * fx - 16) / Kappa) * WhiteX;
        var y = l > Kappa * Epsilon ? fy * fy * fy : l / Kappa;
        var z = (fz * fz * fz > Epsilon ? fz * fz * fz : (116 * fz - 16) / Kappa) * WhiteZ;
        return FromXyz(D50ToD65(x, y, z));
    }

    /// <summary>CIE LCH (the polar form of Lab) to sRGB; the chroma is clamped to be non-negative.</summary>
    internal static (double R, double G, double B) FromLch(double l, double c, double hueDegrees)
    {
        c = Math.Max(c, 0);
        var h = hueDegrees * Math.PI / 180;
        return FromLab(l, c * Math.Cos(h), c * Math.Sin(h));
    }

    /// <summary>Oklab to sRGB. L is clamped to 0..1.</summary>
    internal static (double R, double G, double B) FromOklab(double l, double a, double b)
    {
        l = Math.Clamp(l, 0, 1);
        var lp = l + 0.3963377773761749 * a + 0.2158037573099136 * b;
        var mp = l - 0.1055613458156586 * a - 0.0638541728258133 * b;
        var sp = l - 0.0894841775298119 * a - 1.2914855480194092 * b;
        double lc = lp * lp * lp, mc = mp * mp * mp, sc = sp * sp * sp;
        return FromXyz((
            1.2268798758459243 * lc - 0.5578149944602171 * mc + 0.2813910456659647 * sc,
            -0.0405757452148008 * lc + 1.1122868032803170 * mc - 0.0717110580655164 * sc,
            -0.0763729366746601 * lc - 0.4214933324022432 * mc + 1.5869240198367816 * sc));
    }

    /// <summary>Oklch to sRGB; the chroma is clamped to be non-negative.</summary>
    internal static (double R, double G, double B) FromOklch(double l, double c, double hueDegrees)
    {
        c = Math.Max(c, 0);
        var h = hueDegrees * Math.PI / 180;
        return FromOklab(l, c * Math.Cos(h), c * Math.Sin(h));
    }

    /// <summary>A color() value in a predefined space to sRGB.</summary>
    internal static (double R, double G, double B) FromColor(Space space, double c1, double c2, double c3)
    {
        switch (space)
        {
            case Space.Srgb: return (Clip(c1), Clip(c2), Clip(c3));
            case Space.SrgbLinear: return (Clip(Encode(c1)), Clip(Encode(c2)), Clip(Encode(c3)));
            case Space.XyzD65: return FromXyz((c1, c2, c3));
            case Space.XyzD50: return FromXyz(D50ToD65(c1, c2, c3));
            case Space.DisplayP3:
            {
                double r = Decode(c1), g = Decode(c2), b = Decode(c3);
                return FromXyz((0.4865709486482162 * r + 0.26566769316909306 * g + 0.1982172852343625 * b,
                    0.2289745640697488 * r + 0.6917385218365064 * g + 0.079286914093745 * b,
                    0.04511338185890264 * g + 1.043944368900976 * b));
            }
            case Space.A98Rgb:
            {
                double r = DecodeGamma(c1, 563d / 256), g = DecodeGamma(c2, 563d / 256), b = DecodeGamma(c3, 563d / 256);
                return FromXyz((0.5766690429101305 * r + 0.1855582379065463 * g + 0.1882286462349947 * b,
                    0.29734497525053605 * r + 0.6273635662554661 * g + 0.0752914584939978 * b,
                    0.02703136138641234 * r + 0.07068885253582723 * g + 0.9913375368376388 * b));
            }
            case Space.ProphotoRgb:
            {
                double r = DecodeProphoto(c1), g = DecodeProphoto(c2), b = DecodeProphoto(c3);
                return FromXyz(D50ToD65(0.7977604896723027 * r + 0.13518583717574031 * g + 0.0313493495815248 * b,
                    0.2880711282292934 * r + 0.7118432178101014 * g + 0.00008565396060525902 * b,
                    0.8251046025104601 * b));
            }
            case Space.Rec2020:
            {
                double r = DecodeRec2020(c1), g = DecodeRec2020(c2), b = DecodeRec2020(c3);
                return FromXyz((0.6369580483012914 * r + 0.14461690358620832 * g + 0.1688809751641721 * b,
                    0.2627002120112671 * r + 0.6779980715188708 * g + 0.05930171646986196 * b,
                    0.028072693049087428 * g + 1.060985057710791 * b));
            }
            default: throw new ArgumentOutOfRangeException(nameof(space));
        }
    }

    private static (double X, double Y, double Z) D50ToD65(double x, double y, double z) => (
        0.9554734527042182 * x - 0.023098536874261423 * y + 0.0632593086610217 * z,
        -0.028369706963208136 * x + 1.0099954580058226 * y + 0.021041398966943008 * z,
        0.012314001688319899 * x - 0.020507696433477912 * y + 1.3303659366080753 * z);

    private static (double R, double G, double B) FromXyz((double X, double Y, double Z) xyz)
    {
        var (x, y, z) = xyz;
        var r = 3.2409699419045226 * x - 1.537383177570094 * y - 0.4986107602930034 * z;
        var g = -0.9692436362808796 * x + 1.8759675015077202 * y + 0.04155505740717559 * z;
        var b = 0.05563007969699366 * x - 0.20397695888897652 * y + 1.0569715142428786 * z;
        return (Clip(Encode(r)), Clip(Encode(g)), Clip(Encode(b)));
    }

    private static double Clip(double value) => double.IsNaN(value) ? 0 : Math.Clamp(value, 0, 1);

    // The sRGB transfer curve, odd-extended for negative values.
    private static double Encode(double value)
    {
        var magnitude = Math.Abs(value);
        var encoded = magnitude > 0.0031308 ? 1.055 * Math.Pow(magnitude, 1 / 2.4) - 0.055 : 12.92 * magnitude;
        return value < 0 ? -encoded : encoded;
    }

    private static double Decode(double value)
    {
        var magnitude = Math.Abs(value);
        var linear = magnitude <= 0.04045 ? magnitude / 12.92 : Math.Pow((magnitude + 0.055) / 1.055, 2.4);
        return value < 0 ? -linear : linear;
    }

    private static double DecodeGamma(double value, double gamma)
        => value < 0 ? -Math.Pow(-value, gamma) : Math.Pow(value, gamma);

    private static double DecodeProphoto(double value)
    {
        var magnitude = Math.Abs(value);
        var linear = magnitude <= 16d / 512 ? magnitude / 16 : Math.Pow(magnitude, 1.8);
        return value < 0 ? -linear : linear;
    }

    private static double DecodeRec2020(double value)
    {
        const double alpha = 1.09929682680944, beta = 0.018053968510807;
        var magnitude = Math.Abs(value);
        var linear = magnitude < beta * 4.5 ? magnitude / 4.5 : Math.Pow((magnitude + alpha - 1) / alpha, 1 / 0.45);
        return value < 0 ? -linear : linear;
    }
}
