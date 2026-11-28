using System.Collections.ObjectModel;

namespace FactsPDF;

/// <summary>Which of the four explicit default chains a weight and style select.</summary>
internal enum FontRoleKind { Normal, Bold, Italic, BoldItalic }

/// <summary>
/// What selects a text style's font chain: its resolved font-family list (null when no entry names a
/// family this conversion has, so only the default chains apply) and its weight, style and stretch.
/// Equal roles share glyph resolution caches.
/// </summary>
internal readonly record struct FontRole(FontFamilyList? Families, float Weight, FontSlant Slant, float Stretch)
{
    /// <summary>The default chain role: CSS matching against faces of weight 400 and 700 picks bold above 500, and italic for italic or oblique.</summary>
    internal FontRoleKind Kind => (Weight > 500, Slant != FontSlant.Normal) switch
    {
        (true, true) => FontRoleKind.BoldItalic,
        (true, false) => FontRoleKind.Bold,
        (false, true) => FontRoleKind.Italic,
        _ => FontRoleKind.Normal,
    };

    /// <summary>Plain normal text in the default chain (whose diagnostics keep their original wording).</summary>
    internal bool IsPlainNormal => Families is null && Kind == FontRoleKind.Normal;
}

internal static class FontRoles
{
    private const int MaxFontFaces = 32, MaxFontResources = 32;

    internal static FontRole Role(TextStyle style)
    {
        // Without a resolved family only the default chains apply: canonical weights keep one
        // cache entry per chain, as before font-family resolution existed.
        if (style.Families is null)
            return new(null, style.Weight > 500 ? 700 : 400, style.Slant == FontSlant.Normal ? FontSlant.Normal : FontSlant.Italic, 100);
        return new(style.Families, style.Weight, style.Slant, style.Stretch);
    }

    internal static string Name(FontRole role) => role.Kind switch
    {
        FontRoleKind.Bold => "bold", FontRoleKind.Italic => "italic",
        FontRoleKind.BoldItalic => "bold-italic", _ => "normal"
    };

    /// <summary>The text is drawn with the built-in Courier fallback: plain normal text and no regular chain.</summary>
    internal static bool UsesBuiltInFont(PdfOptions options, TextStyle style) => options.Fonts.Count == 0 && Role(style).IsPlainNormal;

    internal static IReadOnlyList<PdfFont> Chain(PdfOptions options, FontRole role, int sourceOffset)
    {
        var kind = role.Kind;
        var fonts = kind switch
        {
            FontRoleKind.Bold => options.BoldFonts, FontRoleKind.Italic => options.ItalicFonts,
            FontRoleKind.BoldItalic => options.BoldItalicFonts, _ => options.Fonts
        };
        if (role.Families is { } families)
            fonts = families.Chain(role.Weight, role.Slant, role.Stretch, fonts, sourceOffset);
        if (kind != FontRoleKind.Normal && fonts.Count == 0)
            throw new FactsPdfException("FPDF1507", $"Supply an explicit font chain for {Name(role)} text.", sourceOffset);
        return fonts;
    }

    /// <summary>The bytes of every distinct font the caller supplied (chains, registered faces and font resources).</summary>
    internal static long SuppliedFontBytes(PdfOptions options)
    {
        var unique = new HashSet<PdfFont>(ReferenceEqualityComparer.Instance);
        long total = 0;
        foreach (var font in AllSupplied(options)) if (unique.Add(font)) total += font.ByteLength;
        return total;
    }

    private static IEnumerable<PdfFont> AllSupplied(PdfOptions options)
    {
        foreach (var chain in new[] { options.Fonts, options.BoldFonts, options.ItalicFonts, options.BoldItalicFonts })
            foreach (var font in chain) yield return font;
        foreach (var face in options.FontFaces) yield return face.Font;
        foreach (var font in options.FontResources.Values) yield return font;
    }

    internal static PdfOptions Snapshot(PdfOptions options)
    {
        if (options.Fonts is null || options.Fonts.Count > 8)
            throw new ArgumentException("Supply a non-null font list with at most eight entries.", nameof(options));
        IReadOnlyList<PdfFont>[] chains = [options.Fonts, options.BoldFonts, options.ItalicFonts, options.BoldItalicFonts];
        if (chains.Any(chain => chain is null) || chains.Sum(chain => (long)chain.Count) > 8)
            throw new ArgumentException("Supply non-null font lists with at most eight entries in total.", nameof(options));
        if (options.FontFaces is null || options.FontFaces.Count > MaxFontFaces || options.FontFaces.Any(face => face?.Font is null || string.IsNullOrEmpty(face.Family)))
            throw new ArgumentException($"Supply at most {MaxFontFaces} non-null registered font faces with family names.", nameof(options));
        if (options.FontResources is null || options.FontResources.Count > MaxFontResources || options.FontResources.Any(entry => entry.Value is null || !ValidResourceName(entry.Key)))
            throw new ArgumentException($"Supply at most {MaxFontResources} named non-null font resources.", nameof(options));
        if (options.GenericFamilies is null || options.GenericFamilies.Any(entry => !FontFamilyNames.IsGeneric(entry.Key)
                || !options.FontFaces.Any(face => face.Family == entry.Value)))
            throw new ArgumentException("Each generic family must map to a registered family name.", nameof(options));
        var unique = new HashSet<PdfFont>(ReferenceEqualityComparer.Instance);
        long totalFontBytes = 0;
        var snapshots = new PdfFont[chains.Length][];
        for (var i = 0; i < chains.Length; i++)
        {
            var chain = new List<PdfFont>();
            foreach (var font in chains[i])
            {
                if (font is null) throw new ArgumentException("Font entries cannot be null.", nameof(options));
                if (unique.Add(font))
                {
                    totalFontBytes += font.ByteLength;
                    if (totalFontBytes > options.MaxTotalFontBytes)
                        throw new FactsPdfException("FPDF1503", "Fonts exceed MaxTotalFontBytes.");
                }
                if (!chain.Contains(font)) chain.Add(font);
            }
            snapshots[i] = chain.ToArray();
        }
        var faces = options.FontFaces.ToArray();
        var resources = new Dictionary<string, PdfFont>(options.FontResources, StringComparer.Ordinal);
        foreach (var font in faces.Select(face => face.Font).Concat(resources.Values))
            if (unique.Add(font))
            {
                totalFontBytes += font.ByteLength;
                if (totalFontBytes > options.MaxTotalFontBytes)
                    throw new FactsPdfException("FPDF1503", "Fonts exceed MaxTotalFontBytes.");
            }
        return options with
        {
            Fonts = snapshots[0], BoldFonts = snapshots[1], ItalicFonts = snapshots[2], BoldItalicFonts = snapshots[3],
            FontFaces = faces,
            FontResources = resources.Count == 0 ? FontResourceDefaults.Empty : new ReadOnlyDictionary<string, PdfFont>(resources),
            GenericFamilies = options.GenericFamilies.Count == 0 ? FontResourceDefaults.NoGenerics
                : new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(options.GenericFamilies, StringComparer.Ordinal)),
        };
    }

    /// <summary>A font resource name is the exact URL text an <c>@font-face</c> src names: 1 to 2048 printable ASCII characters.</summary>
    internal static bool ValidResourceName(string? name)
    {
        if (name is null || name.Length is < 1 or > 2048) return false;
        foreach (var c in name) if (c is <= ' ' or > '~') return false;
        return true;
    }
}

/// <summary>The shared empty font registration maps.</summary>
internal static class FontResourceDefaults
{
    internal static readonly IReadOnlyDictionary<string, PdfFont> Empty =
        new ReadOnlyDictionary<string, PdfFont>(new Dictionary<string, PdfFont>(StringComparer.Ordinal));
    internal static readonly IReadOnlyDictionary<string, string> NoGenerics =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal));
}
