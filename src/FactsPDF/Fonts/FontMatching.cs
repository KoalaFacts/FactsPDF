using System.Text;

namespace FactsPDF;

/// <summary>The font-style axis CSS font matching compares: normal, italic, or oblique at a positive angle.</summary>
internal enum FontSlant : byte { Normal, Italic, Oblique }

/// <summary>A caller-registered face and the (ASCII lower-case) family name it answers to.</summary>
internal sealed record RegisteredFontFace(string Family, PdfFont Font);

/// <summary>
/// One face a family name can select: a caller-registered font, or an <c>@font-face</c> rule whose
/// source loads only when the matcher first picks it (fonts load on demand, CSS Fonts 4 §4.10).
/// Per conversion: the loaded font is remembered for the conversion that created the candidate.
/// </summary>
internal sealed class FontFaceCandidate
{
    private readonly Func<PdfFont?>? load;
    private PdfFont? font;
    private bool loaded;

    internal FontFaceCandidate(string family, PdfFont font, int priority)
    {
        Family = family; this.font = font; loaded = true; Priority = priority;
        MinWeight = MaxWeight = font.Weight; Slant = font.Slant;
        MinStretch = MaxStretch = (float)font.Stretch;
    }

    internal FontFaceCandidate(FontFaceRule rule, Func<PdfFont?> load, int priority)
    {
        Family = rule.DisplayFamily; this.load = load; Priority = priority; WebFont = true;
        MinWeight = rule.MinWeight; MaxWeight = rule.MaxWeight; Slant = rule.Slant;
        MinStretch = rule.MinStretch; MaxStretch = rule.MaxStretch;
    }

    internal string Family { get; }
    internal float MinWeight { get; }
    internal float MaxWeight { get; }
    internal FontSlant Slant { get; }
    internal float MinStretch { get; }
    internal float MaxStretch { get; }
    /// <summary>An <c>@font-face</c> face, which Chrome synthesizes by its web-font rule rather than its installed-font rule.</summary>
    internal bool WebFont { get; }
    /// <summary>Breaks ties between faces with the same descriptors: the later <c>@font-face</c> rule, the earlier registration.</summary>
    internal int Priority { get; }

    /// <summary>The face's font, loading an <c>@font-face</c> source the first time; null when no source applies.</summary>
    internal PdfFont? Font
    {
        get
        {
            if (!loaded) { font = load!(); loaded = true; }
            return font;
        }
    }
}

/// <summary>The CSS Fonts 4 font matching algorithm (§5.2 step 4) over one family's faces.</summary>
internal static class FontMatcher
{
    /// <summary>
    /// Narrows the faces by font-stretch, then font-style, then font-weight, each time keeping the
    /// faces nearest the desired value in the order the specification checks values.
    /// </summary>
    internal static FontFaceCandidate Match(IReadOnlyList<FontFaceCandidate> faces, float weight, FontSlant slant, float stretch)
    {
        var set = Nearest(faces, face => StretchRank(face, stretch));
        set = Nearest(set, face => (StyleRank(face.Slant, slant), 0f));
        set = Nearest(set, face => WeightRank(face, weight));
        var chosen = set[0];
        for (var i = 1; i < set.Count; i++) if (set[i].Priority > chosen.Priority) chosen = set[i];
        return chosen;
    }

    private static List<FontFaceCandidate> Nearest(IReadOnlyList<FontFaceCandidate> faces, Func<FontFaceCandidate, (int Tier, float Distance)> rank)
    {
        var best = (Tier: int.MaxValue, Distance: float.MaxValue);
        var result = new List<FontFaceCandidate>(faces.Count);
        foreach (var face in faces)
        {
            var r = rank(face);
            if (r.Tier < best.Tier || r.Tier == best.Tier && r.Distance < best.Distance) { best = r; result.Clear(); }
            if (r.Tier == best.Tier && r.Distance == best.Distance) result.Add(face);
        }
        return result;
    }

    // At or below 100%, narrower widths are checked in descending order before wider ones in
    // ascending order; above 100%, wider first, then narrower.
    internal static (int, float) StretchRank(FontFaceCandidate face, float desired)
    {
        if (face.MinStretch <= desired && desired <= face.MaxStretch) return (0, 0);
        if (desired <= 100) return face.MaxStretch < desired ? (1, desired - face.MaxStretch) : (2, face.MinStretch - desired);
        return face.MinStretch > desired ? (1, face.MinStretch - desired) : (2, desired - face.MaxStretch);
    }

    // Italic checks italic, then oblique, then normal faces; oblique checks oblique, then italic,
    // then normal; normal checks normal, then oblique, then italic.
    internal static int StyleRank(FontSlant face, FontSlant desired) => desired switch
    {
        FontSlant.Italic => face switch { FontSlant.Italic => 0, FontSlant.Oblique => 1, _ => 2 },
        FontSlant.Oblique => face switch { FontSlant.Oblique => 0, FontSlant.Italic => 1, _ => 2 },
        _ => face switch { FontSlant.Normal => 0, FontSlant.Oblique => 1, _ => 2 },
    };

    // From 400 to 500: heavier weights up to 500 ascending, then lighter descending, then heavier
    // than 500 ascending. Below 400: lighter descending, then heavier ascending. Above 500:
    // heavier ascending, then lighter descending.
    internal static (int, float) WeightRank(FontFaceCandidate face, float desired)
    {
        if (face.MinWeight <= desired && desired <= face.MaxWeight) return (0, 0);
        if (desired is >= 400 and <= 500)
        {
            if (face.MinWeight > desired && face.MinWeight <= 500) return (1, face.MinWeight - desired);
            if (face.MaxWeight < desired) return (2, desired - face.MaxWeight);
            return (3, face.MinWeight - desired);
        }
        if (desired < 400) return face.MaxWeight < desired ? (1, desired - face.MaxWeight) : (2, face.MinWeight - desired);
        return face.MinWeight > desired ? (1, face.MinWeight - desired) : (2, desired - face.MaxWeight);
    }

    /// <summary>
    /// The style Chrome would synthesize to draw the matched face at the requested weight and
    /// style ("bold", "italic" or "bold and italic"), or null when the face is drawn as it is. Blink
    /// emboldens a web font when the request is at least 600 and the face's heaviest weight is
    /// under 600, and an installed font also when the request exceeds its weight by more than 200;
    /// it slants an upright face for italic and oblique requests.
    /// </summary>
    internal static string? Synthesis(FontFaceCandidate face, float weight, FontSlant slant)
    {
        var bold = weight >= 600 && face.MaxWeight < 600 || !face.WebFont && weight > face.MaxWeight + 200;
        var italic = slant != FontSlant.Normal && face.Slant == FontSlant.Normal;
        return bold && italic ? "bold and italic" : bold ? "bold" : italic ? "italic" : null;
    }
}

/// <summary>
/// A computed font-family list resolved against the conversion's families: for each entry that
/// names a registered family, a mapped generic family or an <c>@font-face</c> family, the faces it
/// can select. Unknown names are skipped (as a browser skips families that are not installed);
/// an unmapped generic family ends the list, because a browser always resolves a generic family,
/// and the explicit default chains stand in for it. Interned per conversion, so styles compare it
/// by reference.
/// </summary>
internal sealed class FontFamilyList
{
    private readonly IReadOnlyList<FontFaceCandidate>[] families;
    private readonly string[] names;
    private readonly Dictionary<(float Weight, FontSlant Slant, float Stretch), PdfFont[]> chains = [];

    internal FontFamilyList(IReadOnlyList<FontFaceCandidate>[] families, string[] names, bool genericMonospaceOnly)
    {
        this.families = families; this.names = names; IsGenericMonospaceOnly = genericMonospaceOnly;
    }

    /// <summary>The list is exactly the generic <c>monospace</c>, mapped to a registered family (Chrome then sizes keyword font sizes from its monospace default).</summary>
    internal bool IsGenericMonospaceOnly { get; }

    /// <summary>The family names that resolved, for messages.</summary>
    internal string Names => string.Join(", ", names);

    /// <summary>
    /// The fallback chain for one weight, style and stretch: the face each family matches, then the
    /// explicit default chain of the role. A face Chrome would have to synthesize fails FPDF1507.
    /// </summary>
    internal IReadOnlyList<PdfFont> Chain(float weight, FontSlant slant, float stretch, IReadOnlyList<PdfFont> defaults, int sourceOffset)
    {
        if (chains.TryGetValue((weight, slant, stretch), out var cached)) return cached;
        var chain = new List<PdfFont>(families.Length + defaults.Count);
        for (var i = 0; i < families.Length; i++)
        {
            var face = FontMatcher.Match(families[i], weight, slant, stretch);
            if (face.Font is not { } font) continue;
            if (FontMatcher.Synthesis(face, weight, slant) is { } synthesized)
                throw new FactsPdfException("FPDF1507",
                    $"font-family '{names[i]}' has no face for weight {weight:0.###} {(slant == FontSlant.Normal ? "upright" : "italic/oblique")} text: Chrome would synthesize {synthesized}, which FactsPDF never does. Register a real {synthesized} face of this family.",
                    sourceOffset);
            if (!chain.Contains(font)) chain.Add(font);
        }
        foreach (var font in defaults) if (!chain.Contains(font)) chain.Add(font);
        var result = chain.ToArray();
        chains[(weight, slant, stretch)] = result;
        return result;
    }
}

/// <summary>The canonical form a computed font-family value is kept in, and the generic family names.</summary>
internal static class FontFamilyNames
{
    /// <summary>The generic families Chrome resolves to a platform font; any other identifier is a family name.</summary>
    internal static bool IsGeneric(string name) => name is "serif" or "sans-serif" or "monospace" or "cursive" or "fantasy" or "system-ui" or "math";

    /// <summary>ASCII lower case: CSS compares family names ASCII case-insensitively.</summary>
    internal static string Fold(string name)
    {
        var builder = new StringBuilder(name.Length);
        foreach (var c in name) builder.Append(c is >= 'A' and <= 'Z' ? (char)(c + 32) : c);
        return builder.ToString();
    }

    /// <summary>Joins entries as <c>"string"</c> (with <c>\</c> escapes) or space-joined identifiers, separated by commas.</summary>
    internal static string Format(IEnumerable<(string Name, bool Quoted)> entries)
    {
        var builder = new StringBuilder();
        foreach (var (name, quoted) in entries)
        {
            if (builder.Length > 0) builder.Append(',');
            if (!quoted) { builder.Append(name); continue; }
            builder.Append('"');
            foreach (var c in name) { if (c is '"' or '\\') builder.Append('\\'); builder.Append(c); }
            builder.Append('"');
        }
        return builder.ToString();
    }

    /// <summary>The entries of a value <see cref="Format"/> wrote.</summary>
    internal static List<(string Name, bool Quoted)> Parse(string value)
    {
        var entries = new List<(string, bool)>();
        var at = 0;
        while (at < value.Length)
        {
            if (value[at] == '"')
            {
                var builder = new StringBuilder();
                at++;
                while (at < value.Length && value[at] != '"')
                {
                    if (value[at] == '\\' && at + 1 < value.Length) at++;
                    builder.Append(value[at++]);
                }
                at++; // closing quote
                entries.Add((builder.ToString(), true));
            }
            else
            {
                var end = value.IndexOf(',', at);
                if (end < 0) end = value.Length;
                entries.Add((value[at..end], false));
                at = end;
            }
            if (at < value.Length && value[at] == ',') at++;
        }
        return entries;
    }
}

/// <summary>
/// Resolves computed font-family values for one conversion: the caller's registered families and
/// generic family mappings, the stylesheet's <c>@font-face</c> rules (which hide a registered
/// family of the same name, as they hide an installed one) and their on-demand sources.
/// </summary>
internal sealed class FontFamilyResolver
{
    private readonly PdfOptions options;
    private readonly IReadOnlyList<FontFaceRule> rules;
    private readonly Dictionary<string, FontFamilyList?> lists = new(StringComparer.Ordinal);
    private Dictionary<string, List<FontFaceCandidate>>? registered, declared;
    private long fontBytesLeft = -1;

    internal FontFamilyResolver(PdfOptions options, IReadOnlyList<FontFaceRule> rules)
    {
        this.options = options; this.rules = rules;
    }

    /// <summary>The interned list for a canonical font-family value, or null when no entry resolves (the default chains apply).</summary>
    internal FontFamilyList? List(string value)
    {
        if (lists.TryGetValue(value, out var known)) return known;
        var entries = FontFamilyNames.Parse(value);
        var families = new List<IReadOnlyList<FontFaceCandidate>>();
        var names = new List<string>();
        var monospaceOnly = false;
        foreach (var (name, quoted) in entries)
        {
            var key = FontFamilyNames.Fold(name);
            if (!quoted && FontFamilyNames.IsGeneric(key))
            {
                if (!options.GenericFamilies.TryGetValue(key, out var mapped)) break;
                if (Registered().TryGetValue(mapped, out var generic))
                {
                    families.Add(generic); names.Add(name);
                    monospaceOnly = key == "monospace" && entries.Count == 1;
                }
                break;
            }
            if (Declared().TryGetValue(key, out var faces) || Registered().TryGetValue(key, out faces))
            {
                families.Add(faces); names.Add(name);
            }
        }
        var list = families.Count == 0 ? null : new FontFamilyList([.. families], [.. names], monospaceOnly);
        lists[value] = list;
        return list;
    }

    private Dictionary<string, List<FontFaceCandidate>> Registered()
    {
        if (registered is not null) return registered;
        registered = new(StringComparer.Ordinal);
        for (var i = 0; i < options.FontFaces.Count; i++)
        {
            var face = options.FontFaces[i];
            if (!registered.TryGetValue(face.Family, out var faces)) registered[face.Family] = faces = [];
            faces.Add(new FontFaceCandidate(face.Family, face.Font, -i));
        }
        return registered;
    }

    private Dictionary<string, List<FontFaceCandidate>> Declared()
    {
        if (declared is not null) return declared;
        declared = new(StringComparer.Ordinal);
        for (var i = 0; i < rules.Count; i++)
        {
            var rule = rules[i];
            if (!declared.TryGetValue(rule.Family, out var faces)) declared[rule.Family] = faces = [];
            faces.Add(new FontFaceCandidate(rule, () => Load(rule), i));
        }
        return declared;
    }

    // The first src entry that applies: an unsupported format() is skipped, local() matches a
    // supplied font by full or PostScript name, a data: URL decodes in place and any other URL
    // must be a font resource the caller bound. When URLs were named but none was bound, the
    // conversion fails rather than printing a fallback face Chrome would not.
    private PdfFont? Load(FontFaceRule rule)
    {
        FontFaceSource? missing = null;
        foreach (var source in rule.Sources)
        {
            if (!source.FormatSupported) continue;
            if (source.Local is { } local)
            {
                if (Local(local) is { } installed) return installed;
                continue;
            }
            var url = source.Url!;
            if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return DataFont(url, source.Offset);
            if (options.FontResources.TryGetValue(url, out var bound)) return bound;
            missing ??= source;
        }
        if (missing is not null)
            throw new FactsPdfException("FPDF1508",
                $"@font-face source '{Shorten(missing.Url!)}' for font-family '{rule.DisplayFamily}' was not supplied; bind the exact URL text as a font resource (WithFontResource, CLI --font-resource url=path). Fonts are never downloaded.",
                missing.Offset);
        return null;
    }

    private PdfFont? Local(string name)
    {
        var key = FontFamilyNames.Fold(name);
        bool Matches(PdfFont font) => font.FullName is { } full && FontFamilyNames.Fold(full) == key
            || FontFamilyNames.Fold(font.PostScriptName) == key;
        foreach (var face in options.FontFaces) if (Matches(face.Font)) return face.Font;
        foreach (var chain in new[] { options.Fonts, options.BoldFonts, options.ItalicFonts, options.BoldItalicFonts })
            foreach (var font in chain) if (Matches(font)) return font;
        return null;
    }

    private PdfFont DataFont(string url, int offset)
    {
        var comma = url.IndexOf(',', StringComparison.Ordinal);
        if (comma < 0 || !url.AsSpan(5, comma - 5).EndsWith(";base64", StringComparison.OrdinalIgnoreCase))
            throw new FactsPdfException("FPDF1202", "@font-face data: URLs must be base64-encoded (data:font/woff2;base64,...).", offset);
        byte[] bytes;
        try { bytes = Convert.FromBase64String(url[(comma + 1)..]); }
        catch (FormatException) { throw new FactsPdfException("FPDF1501", "@font-face data: URL is not valid base64.", offset); }
        if (fontBytesLeft < 0) fontBytesLeft = options.MaxTotalFontBytes - FontRoles.SuppliedFontBytes(options);
        if (bytes.Length > fontBytesLeft) throw new FactsPdfException("FPDF1503", "@font-face data: fonts exceed MaxTotalFontBytes.", offset);
        PdfFont font;
        try { font = PdfFont.FromBytes(bytes, (int)Math.Min(PdfFont.DefaultMaxBytes, Math.Max(1, fontBytesLeft))); }
        catch (FactsPdfException error) when (error.SourceOffset < 0) { throw new FactsPdfException(error.Code, error.Message[(error.Code.Length + 2)..], offset); }
        fontBytesLeft -= font.ByteLength;
        if (fontBytesLeft < 0) throw new FactsPdfException("FPDF1503", "@font-face data: fonts exceed MaxTotalFontBytes.", offset);
        return font;
    }

    private static string Shorten(string url) => url.Length <= 120 ? url : url[..117] + "...";
}
