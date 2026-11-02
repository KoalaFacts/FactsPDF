using System.Text;

namespace FactsPDF;

/// <summary>Bounded opt-in Unicode PDF Info strings; shared by both serializers.</summary>
internal static class PdfDocumentMetadata
{
    private static (string Name, string? Value)[] Fields(PdfOptions options) =>
    [
        ("Title", options.Title), ("Author", options.Author),
        ("Subject", options.Subject), ("Keywords", options.Keywords)
    ];

    internal static int Validate(PdfOptions options)
    {
        var total = 0;
        foreach (var (name, value) in Fields(options))
        {
            if (value is null) continue;
            if (value.Length is 0 or > 1024 || string.IsNullOrWhiteSpace(value))
                throw new ArgumentException($"PDF {name} requires 1-1024 nonblank UTF-16 units.", name);
            for (var i = 0; i < value.Length; i++)
            {
                var ch = value[i];
                if (char.IsControl(ch))
                    throw new ArgumentException($"PDF {name} must be single-line text without control characters.", name);
                if (char.IsHighSurrogate(ch))
                {
                    if (++i >= value.Length || !char.IsLowSurrogate(value[i]))
                        throw new ArgumentException($"PDF {name} contains malformed UTF-16.", name);
                }
                else if (char.IsLowSurrogate(ch))
                    throw new ArgumentException($"PDF {name} contains malformed UTF-16.", name);
            }
            total += value.Length;
        }
        return total;
    }

    // PDF 1.7 Unicode strings: UTF-16BE, FE FF BOM and hex syntax. No
    // locale, date, HTML lookup or font dependency enters the output.
    internal static string? BuildInfo(PdfOptions options)
    {
        var text = new StringBuilder("<<");
        var present = false;
        foreach (var (name, value) in Fields(options))
        {
            if (value is null) continue;
            present = true;
            text.Append(" /").Append(name).Append(" <FEFF")
                .Append(Convert.ToHexString(Encoding.BigEndianUnicode.GetBytes(value)))
                .Append('>');
        }
        return present ? text.Append(" >>").ToString() : null;
    }
}
