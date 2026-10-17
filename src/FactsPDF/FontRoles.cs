namespace FactsPDF;

internal enum FontRole { Normal, Bold, Italic, BoldItalic }

internal static class FontRoles
{
    internal static FontRole Role(TextStyle style) => (style.Bold, style.Italic) switch
    {
        (true, true) => FontRole.BoldItalic,
        (true, false) => FontRole.Bold,
        (false, true) => FontRole.Italic,
        _ => FontRole.Normal
    };

    internal static string Name(FontRole role) => role switch
    {
        FontRole.Bold => "bold", FontRole.Italic => "italic",
        FontRole.BoldItalic => "bold-italic", _ => "normal"
    };

    internal static IReadOnlyList<PdfFont> Chain(PdfOptions options, FontRole role, int sourceOffset)
    {
        var fonts = role switch
        {
            FontRole.Bold => options.BoldFonts, FontRole.Italic => options.ItalicFonts,
            FontRole.BoldItalic => options.BoldItalicFonts, _ => options.Fonts
        };
        if (role != FontRole.Normal && fonts.Count == 0)
            throw new FactsPdfException("FPDF1507", $"Supply an explicit font chain for {Name(role)} text.", sourceOffset);
        return fonts;
    }

    internal static PdfOptions Snapshot(PdfOptions options)
    {
        if (options.Fonts is null || options.Fonts.Count > 8)
            throw new ArgumentException("Supply a non-null font list with at most eight entries.", nameof(options));
        IReadOnlyList<PdfFont>[] chains = [options.Fonts, options.BoldFonts, options.ItalicFonts, options.BoldItalicFonts];
        if (chains.Any(chain => chain is null) || chains.Sum(chain => (long)chain.Count) > 8)
            throw new ArgumentException("Supply non-null font lists with at most eight entries in total.", nameof(options));
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
        return options with { Fonts = snapshots[0], BoldFonts = snapshots[1], ItalicFonts = snapshots[2], BoldItalicFonts = snapshots[3] };
    }
}
