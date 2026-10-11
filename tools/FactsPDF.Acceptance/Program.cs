using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using FactsPDF;

namespace FactsPDF.Acceptance;

internal static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            if (args.Length == 1 && args[0] == "info")
            {
                JsonOutput.Write(RuntimeInfo()); return 0;
            }
            var settings = Settings.Parse(args);
            return settings.Command == "render" ? RenderCommand.Run(settings) : MeasureCommand.Run(settings);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or FormatException)
        {
            JsonOutput.Write(new Dictionary<string, object?> { ["status"] = "infrastructure-error", ["message"] = ex.Message });
            return 2;
        }
    }
    internal static Dictionary<string, object?> RuntimeInfo() => new()
    {
        // SDK-generated assembly metadata is fixed at compilation, not caller environment.
        ["source_sha"] = typeof(Program).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+').Last(),
        ["framework"] = RuntimeInformation.FrameworkDescription,
        ["architecture"] = RuntimeInformation.ProcessArchitecture.ToString(),
        ["dynamic_code_supported"] = RuntimeFeature.IsDynamicCodeSupported,
        ["process_id"] = Environment.ProcessId
    };
}

internal sealed class Settings
{
    internal string Command = "";
    internal string Input = "";
    internal string Output = "";
    internal List<string> FontPaths = [];
    internal PdfOptions Options = new();
    internal bool Probe;
    internal bool PreCancelled;
    internal int? InvalidScalar;
    internal int Samples = 30;
    internal int Warmups = 3;

    internal static Settings Parse(string[] args)
    {
        if (args.Length < 2 || args[0] is not ("render" or "measure"))
            throw new ArgumentException("Use render <html> <pdf> or measure <html> with explicit options.");
        var s = new Settings { Command = args[0], Input = Path.GetFullPath(args[1]) };
        var i = 2;
        if (s.Command == "render")
        {
            if (args.Length < 3) throw new ArgumentException("Missing output path");
            s.Output = Path.GetFullPath(args[i++]);
        }
        string Value() => i < args.Length ? args[i++] : throw new ArgumentException("Missing option value");
        int Number(string text, bool zero = false)
            => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n >= (zero ? 0 : 1)
                ? n : throw new ArgumentException("Expected a bounded nonnegative decimal integer");
        while (i < args.Length)
        {
            switch (args[i++])
            {
                case "--font":
                    if (s.FontPaths.Count == 8) throw new ArgumentException("At most eight explicit fonts");
                    s.FontPaths.Add(Path.GetFullPath(Value())); break;
                case "--subset-fonts": s.Options = s.Options with { SubsetFonts = true }; break;
                case "--probe-existing-output": s.Probe = true; break;
                case "--pre-cancelled": s.PreCancelled = true; break;
                case "--invalid-scalar":
                    var scalar = Number(Value());
                    if (scalar is < 0xD800 or > 0xDFFF) throw new ArgumentException("Probe requires one isolated surrogate");
                    s.InvalidScalar = scalar; break;
                case "--samples": s.Samples = Number(Value()); break;
                case "--warmups": s.Warmups = Number(Value(), true); break;
                case "--max-pages": s.Options = s.Options with { MaxPages = Number(Value()) }; break;
                case "--limit":
                    var pair = Value().Split('=');
                    if (pair.Length != 2) throw new ArgumentException("Use --limit Name=positive-int");
                    s.Options = WithLimit(s.Options, pair[0], Number(pair[1])); break;
                default: throw new ArgumentException("Unknown acceptance-host option");
            }
        }
        if (s.Samples > 1000 || s.Warmups > 100) throw new ArgumentException("Excessive acceptance sample count");
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (s.Command == "render" && (string.Equals(s.Output, s.Input, comparison) || s.FontPaths.Any(p => string.Equals(p, s.Output, comparison))))
            throw new ArgumentException("Output must not overwrite an input resource");
        if (s.Command == "measure" && (s.Probe || s.PreCancelled || s.InvalidScalar.HasValue))
            throw new ArgumentException("Diagnostic probes apply to render only");
        return s;
    }
    private static PdfOptions WithLimit(PdfOptions o, string key, int value) => key switch
    {
        "MaxPages" => o with { MaxPages = value },
        "MaxInputCharacters" => o with { MaxInputCharacters = value },
        "MaxElements" => o with { MaxElements = value },
        "MaxDepth" => o with { MaxDepth = value },
        "MaxCssCharacters" => o with { MaxCssCharacters = value },
        "MaxCssDeclarations" => o with { MaxCssDeclarations = value },
        "MaxDisplayCommands" => o with { MaxDisplayCommands = value },
        "MaxOutputBytes" => o with { MaxOutputBytes = value },
        _ => throw new ArgumentException("Only existing whitelisted PdfOptions budgets are allowed")
    };
    internal PdfOptions LoadFonts(CancellationToken token)
    {
        List<PdfFont> fonts = []; long total = 0;
        foreach (var path in FontPaths)
        {
            token.ThrowIfCancellationRequested();
            using var file = File.OpenRead(path);
            var remaining = Options.MaxTotalFontBytes - total;
            if (remaining <= 0) throw new FactsPdfException("FPDF1503", "Font quota exhausted");
            var font = PdfFont.LoadTrueType(file, (int)Math.Min(PdfFont.DefaultMaxBytes, remaining), token);
            fonts.Add(font); total += font.ByteLength;
        }
        return Options with { Fonts = fonts.ToArray() };
    }
}

internal static class JsonOutput
{
    internal static void Write(object? value)
    {
        using var writer = new Utf8JsonWriter(Console.OpenStandardOutput());
        Value(writer, value); writer.Flush();
    }
    private static void Value(Utf8JsonWriter w, object? value)
    {
        switch (value)
        {
            case null: w.WriteNullValue(); break;
            case string s: w.WriteStringValue(s); break;
            case bool b: w.WriteBooleanValue(b); break;
            case int n: w.WriteNumberValue(n); break;
            case long n: w.WriteNumberValue(n); break;
            case double n: w.WriteNumberValue(n); break;
            case IReadOnlyDictionary<string, object?> d:
                w.WriteStartObject(); foreach (var item in d) { w.WritePropertyName(item.Key); Value(w, item.Value); } w.WriteEndObject(); break;
            case IEnumerable<object?> a:
                w.WriteStartArray(); foreach (var item in a) Value(w, item); w.WriteEndArray(); break;
            default: throw new InvalidOperationException("Unregistered acceptance JSON value type");
        }
    }
}
