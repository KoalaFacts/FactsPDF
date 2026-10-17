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
    public static bool IsBrowser() => OperatingSystem.IsBrowser();
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ProbeResult))]
internal partial class ProbeJsonContext : JsonSerializerContext;
