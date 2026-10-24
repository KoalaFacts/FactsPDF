using FactsPDF.Configuration;

namespace FactsPDF.WasmProbe;

internal static class ProbeConversion
{
    // Private interop protocol: consecutive font bytes, lengths and explicit
    // role IDs (normal=0, bold=1, italic=2, bold-italic=3). Each role retains
    // its input order; there is no family matching or fixed pair requirement.
    internal static ProbeResult RunStyles(string html, byte[] packed, int[] lengths, int[] roles, bool subsetFonts)
        => RunImages(html, packed, lengths, roles, [], [], [], subsetFonts);

    internal static ProbeResult RunImages(string html, byte[] packed, int[] lengths, int[] roles,
        byte[] imageBytes, int[] imageLengths, string[] imageNames, bool subsetFonts)
        => RunImagesCore(html, packed, lengths, roles, imageBytes, imageLengths, imageNames, subsetFonts, null);

    // Configuration is parsed before inspecting packed resources, so malformed
    // furniture wins over a simultaneous invalid font/image transport.
    internal static ProbeResult RunPageFurniture(string html, byte[] packed, int[] lengths, int[] roles,
        byte[] imageBytes, int[] imageLengths, string[] imageNames, byte[] furnitureJson, bool subsetFonts)
    {
        var before = GC.GetTotalMemory(false);
        try
        {
            if (furnitureJson is null)
                throw new ArgumentException("PageFurniture JSON bytes are required.", nameof(furnitureJson));
            var furniture = PageFurnitureJson.Parse(furnitureJson);
            return RunImagesCore(html, packed, lengths, roles, imageBytes, imageLengths, imageNames, subsetFonts, furniture);
        }
        catch (ArgumentException error)
        {
            return new(null, 0, "B2_INPUT_FURNITURE", error.Message, before, GC.GetTotalMemory(false));
        }
    }

    private static ProbeResult RunImagesCore(string html, byte[] packed, int[] lengths, int[] roles,
        byte[] imageBytes, int[] imageLengths, string[] imageNames, bool subsetFonts, PdfPageFurniture? furniture)
    {
        var before = GC.GetTotalMemory(false);
        if (packed is null || lengths is null || roles is null || lengths.Length != roles.Length || lengths.Length > 8
            || lengths.Any(length => length <= 0) || roles.Any(role => role is < 0 or > 3)
            || lengths.Sum(length => (long)length) != packed.Length)
            return new(null, 0, "B2_INPUT_FONTS", "Invalid explicit font role configuration.", before, GC.GetTotalMemory(false));
        if (imageBytes is null || imageLengths is null || imageNames is null || imageLengths.Length != imageNames.Length
            || imageLengths.Length > 16 || imageLengths.Any(length => length <= 0)
            || imageLengths.Sum(length => (long)length) != imageBytes.Length
            || imageNames.Any(name => name is null || name.Length is < 1 or > 128 || !char.IsAsciiLetterOrDigit(name[0])
                || name.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('.' or '_' or '-')))
            || imageNames.Distinct(StringComparer.Ordinal).Count() != imageNames.Length)
            return new(null, 0, "B2_INPUT_IMAGES", "Invalid explicit image configuration.", before, GC.GetTotalMemory(false));
        try
        {
            var options = new PdfOptions { SubsetFonts = subsetFonts, PageFurniture = furniture };
            if (imageBytes.Length > options.MaxTotalImageBytes || imageLengths.Any(length => length > 8_388_608))
                throw new FactsPdfException("FPDF1603", "Images exceed explicit resource byte limits.");
            if (packed.Length > options.MaxTotalFontBytes)
                throw new FactsPdfException("FPDF1503", "Fonts exceed MaxTotalFontBytes.");
            var chains = Enumerable.Range(0, 4).Select(_ => new List<PdfFont>()).ToArray();
            var offset = 0;
            for (var i = 0; i < lengths.Length; i++)
            {
                chains[roles[i]].Add(PdfFont.LoadTrueType(packed.AsSpan(offset, lengths[i])));
                offset += lengths[i];
            }
            options = options with { Fonts = chains[0], BoldFonts = chains[1], ItalicFonts = chains[2], BoldItalicFonts = chains[3] };
            if (imageLengths.Length > 0)
            {
                var images = new Dictionary<string, PdfImage>(StringComparer.Ordinal); offset = 0;
                for (var i = 0; i < imageLengths.Length; i++)
                {
                    images.Add(imageNames[i], PdfImage.LoadImage(imageBytes.AsSpan(offset, imageLengths[i])));
                    offset += imageLengths[i];
                }
                options = options with { Images = images };
            }
            return Convert(html, options, before);
        }
        catch (FactsPdfException error)
        {
            return new(null, 0, error.Code, error.Message, before, GC.GetTotalMemory(false));
        }
    }

    internal static ProbeResult Run(string html, byte[] latinFont, byte[] cjkFont, bool subsetFonts)
    {
        var before = GC.GetTotalMemory(false);
        try
        {
            var fonts = new List<PdfFont>();
            foreach (var bytes in new[] { latinFont, cjkFont })
                if (bytes.Length > 0) fonts.Add(PdfFont.LoadTrueType(bytes));
            return Convert(html, new PdfOptions { Fonts = fonts, SubsetFonts = subsetFonts }, before);
        }
        catch (FactsPdfException error)
        {
            return new(null, 0, error.Code, error.Message, before, GC.GetTotalMemory(false));
        }
    }

    private static ProbeResult Convert(string html, PdfOptions options, double before)
    {
        using var output = new MemoryStream();
        var result = PdfConverter.Convert(html, output, options);
        return new(System.Convert.ToBase64String(output.ToArray()), result.PageCount, null, null,
            before, GC.GetTotalMemory(false));
    }
}

internal sealed record ProbeResult(string? PdfBase64, int PageCount, string? ErrorCode,
    string? ErrorMessage, double ManagedHeapBeforeBytes, double ManagedHeapAfterBytes);
