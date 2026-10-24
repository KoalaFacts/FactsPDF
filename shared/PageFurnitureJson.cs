using System.Globalization;
using System.Text;
using System.Text.Json;

namespace FactsPDF.Configuration;

// Source-linked by the CLI and private browser adapter, not a dependency of the core.
// Parsing maps configuration only; the core owns template grammar, fonts and layout.
internal static class PageFurnitureJson
{
    internal const int MaxBytes = 65_536;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    internal static PdfPageFurniture Parse(ReadOnlySpan<byte> utf8, CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        if (utf8.Length > MaxBytes) throw Invalid("$", "must not exceed 65536 UTF-8 bytes, including a BOM");
        try
        {
            // Utf8JsonReader defers decoding strings. Validate all bytes before parsing.
            _ = StrictUtf8.GetCharCount(utf8);
            cancellation.ThrowIfCancellationRequested();
            if (utf8.StartsWith(new byte[] { 0xef, 0xbb, 0xbf })) utf8 = utf8[3..];
            var reader = new Utf8JsonReader(utf8, new JsonReaderOptions
            {
                MaxDepth = 8, AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow
            });
            Read(ref reader, cancellation);
            Require(ref reader, JsonTokenType.StartObject, "$");
            var result = new PdfPageFurniture();
            var seen = 0;
            while (true)
            {
                Read(ref reader, cancellation);
                if (reader.TokenType == JsonTokenType.EndObject) break;
                var name = Name(ref reader, "$", cancellation);
                var bit = name switch { "default" => 1, "firstPage" => 2, _ => 0 };
                Mark(ref seen, bit, "$", name);
                Read(ref reader, cancellation);
                if (name == "default") result = result with { Default = Regions(ref reader, "$.default", cancellation) };
                else result = result with
                {
                    FirstPage = reader.TokenType == JsonTokenType.Null ? null : Regions(ref reader, "$.firstPage", cancellation)
                };
            }
            cancellation.ThrowIfCancellationRequested();
            if (reader.Read()) throw Invalid("$", "must contain exactly one JSON object");
            return result;
        }
        catch (DecoderFallbackException ex) { throw Invalid("$", "must be valid UTF-8", ex); }
        catch (JsonException ex) { throw Invalid("$", "must be strict JSON with a maximum depth of 8", ex); }
    }

    private static PdfPageFurnitureRegions Regions(ref Utf8JsonReader reader, string location, CancellationToken cancellation)
    {
        Require(ref reader, JsonTokenType.StartObject, location);
        var result = new PdfPageFurnitureRegions();
        var seen = 0;
        while (true)
        {
            Read(ref reader, cancellation);
            if (reader.TokenType == JsonTokenType.EndObject) return result;
            var name = Name(ref reader, location, cancellation);
            var bit = name switch
            {
                "headerLeft" => 1, "headerCenter" => 2, "headerRight" => 4,
                "footerLeft" => 8, "footerCenter" => 16, "footerRight" => 32, _ => 0
            };
            Mark(ref seen, bit, location, name);
            Read(ref reader, cancellation);
            var text = reader.TokenType == JsonTokenType.Null ? null : Text(ref reader, location + "." + name, cancellation);
            result = name switch
            {
                "headerLeft" => result with { HeaderLeft = text },
                "headerCenter" => result with { HeaderCenter = text },
                "headerRight" => result with { HeaderRight = text },
                "footerLeft" => result with { FooterLeft = text },
                "footerCenter" => result with { FooterCenter = text },
                _ => result with { FooterRight = text }
            };
        }
    }

    private static PdfPageText Text(ref Utf8JsonReader reader, string location, CancellationToken cancellation)
    {
        Require(ref reader, JsonTokenType.StartObject, location);
        var result = new PdfPageText();
        var seen = 0;
        while (true)
        {
            Read(ref reader, cancellation);
            if (reader.TokenType == JsonTokenType.EndObject) return result;
            var name = Name(ref reader, location, cancellation);
            var bit = name switch { "template" => 1, "fontSize" => 2, "color" => 4, "bold" => 8, "italic" => 16, _ => 0 };
            Mark(ref seen, bit, location, name);
            Read(ref reader, cancellation);
            var field = location + "." + name;
            switch (name)
            {
                case "template":
                    Require(ref reader, JsonTokenType.String, field);
                    result = result with { Template = String(ref reader, field, cancellation) };
                    break;
                case "fontSize":
                    Require(ref reader, JsonTokenType.Number, field);
                    if (!reader.TryGetDouble(out var size) || !double.IsFinite(size) || size is < 1 or > 144)
                        throw Invalid(field, "must be a finite number from 1 to 144");
                    result = result with { FontSize = size };
                    break;
                case "color":
                    Require(ref reader, JsonTokenType.String, field);
                    var color = String(ref reader, field, cancellation);
                    if (!IsRgbHex(color) ||
                        !uint.TryParse(color.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var rgb))
                        throw Invalid(field, "must be exactly #RRGGBB");
                    result = result with { Color = new((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb) };
                    break;
                case "bold": result = result with { Bold = Boolean(ref reader, field) }; break;
                case "italic": result = result with { Italic = Boolean(ref reader, field) }; break;
            }
        }
    }

    private static bool IsRgbHex(string value)
    {
        if (value.Length != 7 || value[0] != '#') return false;
        // Numeric parsing accepts trailing NULs; validate every character first.
        for (var i = 1; i < value.Length; i++)
            if (value[i] is not (>= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F'))
                return false;
        return true;
    }

    private static bool Boolean(ref Utf8JsonReader reader, string location) => reader.TokenType switch
    {
        JsonTokenType.True => true, JsonTokenType.False => false,
        _ => throw Invalid(location, "must be a boolean")
    };

    private static string Name(ref Utf8JsonReader reader, string location, CancellationToken cancellation)
    {
        Require(ref reader, JsonTokenType.PropertyName, location);
        return String(ref reader, location, cancellation);
    }

    private static string String(ref Utf8JsonReader reader, string location, CancellationToken cancellation)
    {
        // Reject unpaired escaped UTF-16 instead of allowing a JSON decoder to repair it.
        // The reader has already checked escape syntax; strict UTF-8 was checked above.
        var raw = reader.ValueSpan;
        for (var i = 0; i < raw.Length; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            if (raw[i] != (byte)'\\') continue;
            if (raw[++i] != (byte)'u') continue;
            var unit = Hex4(raw.Slice(i + 1, 4));
            i += 4;
            if (unit is >= 0xdc00 and <= 0xdfff) throw Invalid(location, "contains an unpaired Unicode surrogate");
            if (unit is < 0xd800 or > 0xdbff) continue;
            if (i + 6 >= raw.Length || raw[i + 1] != (byte)'\\' || raw[i + 2] != (byte)'u')
                throw Invalid(location, "contains an unpaired Unicode surrogate");
            var low = Hex4(raw.Slice(i + 3, 4));
            if (low is < 0xdc00 or > 0xdfff) throw Invalid(location, "contains an unpaired Unicode surrogate");
            i += 6;
        }
        return reader.GetString()!;
    }

    private static int Hex4(ReadOnlySpan<byte> digits)
    {
        var value = 0;
        foreach (var digit in digits)
            value = (value << 4) | (digit <= (byte)'9' ? digit - '0' : (digit | 32) - 'a' + 10);
        return value;
    }

    private static void Mark(ref int seen, int bit, string location, string name)
    {
        if (bit == 0) throw Invalid(location, $"contains unknown property '{name}'");
        if ((seen & bit) != 0) throw Invalid(location, $"contains duplicate property '{name}'");
        seen |= bit;
    }

    private static void Read(ref Utf8JsonReader reader, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (!reader.Read()) throw Invalid("$", "ended before the configuration was complete");
    }

    private static void Require(ref Utf8JsonReader reader, JsonTokenType type, string location)
    {
        if (reader.TokenType != type) throw Invalid(location, $"must be {type}");
    }

    private static ArgumentException Invalid(string location, string message, Exception? inner = null) =>
        new($"PageFurniture {location}: {message}.", "PageFurniture", inner);
}
