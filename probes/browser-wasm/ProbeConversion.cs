namespace FactsPDF.WasmProbe;

internal static class ProbeConversion
{
    internal static ProbeResult Run(string html, byte[] latinFont, byte[] cjkFont, bool subsetFonts)
    {
        var before = GC.GetTotalMemory(false);
        try
        {
            var fonts = new List<PdfFont>();
            foreach (var bytes in new[] { latinFont, cjkFont })
                if (bytes.Length > 0) fonts.Add(PdfFont.LoadTrueType(bytes));
            using var output = new MemoryStream();
            var result = PdfConverter.Convert(html, output,
                new PdfOptions { Fonts = fonts, SubsetFonts = subsetFonts });
            return new(System.Convert.ToBase64String(output.ToArray()), result.PageCount, null, null,
                before, GC.GetTotalMemory(false));
        }
        catch (FactsPdfException error)
        {
            return new(null, 0, error.Code, error.Message, before, GC.GetTotalMemory(false));
        }
    }
}

internal sealed record ProbeResult(string? PdfBase64, int PageCount, string? ErrorCode,
    string? ErrorMessage, double ManagedHeapBeforeBytes, double ManagedHeapAfterBytes);
