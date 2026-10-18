using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FactsPDF.WasmProbe;

internal static class Program
{
    public static void Main() { }
}

// Private probe protocol, not an addition to the FactsPDF public library API.
[SupportedOSPlatform("browser")]
public static partial class BrowserExports
{
    [JSExport]
    public static string Convert(string html, byte[] latinFont, byte[] cjkFont, bool subsetFonts)
        => JsonSerializer.Serialize(ProbeConversion.Run(html, latinFont, cjkFont, subsetFonts),
            ProbeJsonContext.Default.ProbeResult);

    [JSExport]
    public static string ConvertStyles(string html, byte[] fontBytes, int[] fontLengths, int[] fontRoles, bool subsetFonts)
        => JsonSerializer.Serialize(ProbeConversion.RunStyles(html, fontBytes, fontLengths, fontRoles, subsetFonts),
            ProbeJsonContext.Default.ProbeResult);

    [JSExport]
    public static string ConvertImages(string html, byte[] fontBytes, int[] fontLengths, int[] fontRoles,
        byte[] imageBytes, int[] imageLengths, string[] imageNames, bool subsetFonts)
        => JsonSerializer.Serialize(ProbeConversion.RunImages(html, fontBytes, fontLengths, fontRoles,
            imageBytes, imageLengths, imageNames, subsetFonts), ProbeJsonContext.Default.ProbeResult);

    [JSExport]
    public static bool IsBrowser() => OperatingSystem.IsBrowser();
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ProbeResult))]
internal partial class ProbeJsonContext : JsonSerializerContext;
