using System.Text;

namespace FactsPDF;

/// <summary>The character encodings FactsPDF decodes: UTF-8, UTF-16LE, UTF-16BE and windows-1252
/// (which the Encoding Standard also names latin1, iso-8859-1, ascii and us-ascii).</summary>
internal enum HtmlEncodingKind : byte { Utf8, Utf16LittleEndian, Utf16BigEndian, Windows1252 }

/// <summary>
/// Encoding labels and byte-stream decoding as the Encoding Standard and the HTML Standard define
/// them (encoding sniffing: a byte order mark first, then the meta prescan, then UTF-8). The
/// engine itself converts .NET strings, which the caller has already decoded; the CLI, which reads
/// bytes, decodes them here. A label naming any other encoding (Shift_JIS, GB18030, ISO-8859-2,
/// ...) fails with FPDF1103 instead of decoding the bytes some other way.
/// </summary>
internal static class HtmlEncoding
{
    /// <summary>Bytes 0x80-0x9F of windows-1252 (the Encoding Standard index; 0x81, 0x8D, 0x8F, 0x90
    /// and 0x9D map to the C1 controls of the same value). Numeric character references to these
    /// values decode the same way (the HTML tokenizer's numeric character reference end state).</summary>
    internal static readonly int[] Windows1252High =
    [
        0x20AC, 0x0081, 0x201A, 0x0192, 0x201E, 0x2026, 0x2020, 0x2021, 0x02C6, 0x2030, 0x0160, 0x2039, 0x0152, 0x008D, 0x017D, 0x008F,
        0x0090, 0x2018, 0x2019, 0x201C, 0x201D, 0x2022, 0x2013, 0x2014, 0x02DC, 0x2122, 0x0161, 0x203A, 0x0153, 0x009D, 0x017E, 0x0178,
    ];

    // Within the first 1024 bytes the prescan reads every tag, as the HTML Standard requires; after
    // them Chrome keeps looking for a meta charset only while the document is still in its head.
    private const int UnconditionalPrescanBytes = 1024;

    /// <summary>The encoding a label names (ASCII white space trimmed, ASCII case-insensitive), or
    /// null for a label FactsPDF does not decode, whether or not the Encoding Standard knows it.</summary>
    internal static HtmlEncodingKind? Label(ReadOnlySpan<char> label)
    {
        label = label.Trim(" \t\n\f\r");
        Span<char> lower = stackalloc char[24];
        if (label.Length > lower.Length) return null;
        for (var i = 0; i < label.Length; i++)
        {
            var c = label[i];
            if (c > 0x7F) return null;
            lower[i] = c is >= 'A' and <= 'Z' ? (char)(c + 32) : c;
        }
        return lower[..label.Length] switch
        {
            "unicode-1-1-utf-8" or "unicode11utf8" or "unicode20utf8" or "utf-8" or "utf8" or "x-unicode20utf8" => HtmlEncodingKind.Utf8,
            "csunicode" or "iso-10646-ucs-2" or "ucs-2" or "unicode" or "unicodefeff" or "utf-16" or "utf-16le" => HtmlEncodingKind.Utf16LittleEndian,
            "unicodefffe" or "utf-16be" => HtmlEncodingKind.Utf16BigEndian,
            "ansi_x3.4-1968" or "ascii" or "cp1252" or "cp819" or "csisolatin1" or "ibm819" or "iso-8859-1" or "iso-ir-100"
                or "iso8859-1" or "iso88591" or "iso_8859-1" or "iso_8859-1:1987" or "l1" or "latin1" or "us-ascii"
                or "windows-1252" or "x-cp1252" => HtmlEncodingKind.Windows1252,
            _ => null
        };
    }

    /// <summary>Throws FPDF1103 unless FactsPDF decodes the encoding a meta element declares.</summary>
    internal static void RequireLabel(string label, int offset)
    {
        if (Label(label) is null)
            throw new FactsPdfException("FPDF1103",
                $"Encoding '{label.Trim()}' is not supported; FactsPDF decodes UTF-8, UTF-16 and windows-1252 (latin1, iso-8859-1, ascii).", offset);
    }

    /// <summary>The charset a meta http-equiv content-type content value names (the HTML
    /// Standard's algorithm for extracting a character encoding from a meta element), or null.</summary>
    internal static string? ContentCharset(ReadOnlySpan<char> content)
    {
        var at = 0;
        while (true)
        {
            var found = content[at..].IndexOf("charset", StringComparison.OrdinalIgnoreCase);
            if (found < 0) return null;
            var i = at + found + 7;
            while (i < content.Length && HtmlTokens.Space(content[i])) i++;
            if (i >= content.Length || content[i] != '=') { at = i; continue; }
            i++;
            while (i < content.Length && HtmlTokens.Space(content[i])) i++;
            if (i >= content.Length) return null;
            if (content[i] is '"' or '\'')
            {
                var quote = content[i];
                var end = content[(i + 1)..].IndexOf(quote);
                return end < 0 ? null : content.Slice(i + 1, end).ToString();
            }
            var stop = i;
            while (stop < content.Length && !HtmlTokens.Space(content[stop]) && content[stop] != ';') stop++;
            return content[i..stop].ToString();
        }
    }

    /// <summary>Decodes a document's bytes as Chrome does: a UTF-8 or UTF-16 byte order mark wins
    /// (and is removed), then a meta charset (or http-equiv content-type) the prescan finds, then
    /// UTF-8. Malformed UTF-8 or UTF-16 fails with a <see cref="DecoderFallbackException"/> instead
    /// of printing replacement characters.</summary>
    internal static string Decode(ReadOnlySpan<byte> bytes)
    {
        HtmlEncodingKind kind;
        if (bytes.StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF])) { kind = HtmlEncodingKind.Utf8; bytes = bytes[3..]; }
        else if (bytes.StartsWith((ReadOnlySpan<byte>)[0xFE, 0xFF])) { kind = HtmlEncodingKind.Utf16BigEndian; bytes = bytes[2..]; }
        else if (bytes.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xFE])) { kind = HtmlEncodingKind.Utf16LittleEndian; bytes = bytes[2..]; }
        else kind = Prescan(bytes) ?? HtmlEncodingKind.Utf8;
        if (kind == HtmlEncodingKind.Windows1252) return DecodeWindows1252(bytes);
        var (encoding, name) = kind switch
        {
            HtmlEncodingKind.Utf16LittleEndian => ((Encoding)new UnicodeEncoding(false, false, true), "UTF-16LE"),
            HtmlEncodingKind.Utf16BigEndian => (new UnicodeEncoding(true, false, true), "UTF-16BE"),
            _ => (new UTF8Encoding(false, true), "UTF-8"),
        };
        try { return encoding.GetString(bytes); }
        catch (DecoderFallbackException) { throw new DecoderFallbackException($"Input must be valid {name}."); }
    }

    private static string DecodeWindows1252(ReadOnlySpan<byte> bytes) => string.Create(bytes.Length, bytes.ToArray(), static (chars, source) =>
    {
        for (var i = 0; i < source.Length; i++)
            chars[i] = source[i] is >= 0x80 and <= 0x9F ? (char)Windows1252High[source[i] - 0x80] : (char)source[i];
    });

    /// <summary>The HTML Standard's prescan of a byte stream for its encoding: the encoding of the
    /// first meta charset (or http-equiv content-type with a charset), or null when there is none.
    /// A label FactsPDF does not decode fails with FPDF1103 rather than being skipped.</summary>
    internal static HtmlEncodingKind? Prescan(ReadOnlySpan<byte> bytes)
    {
        var position = 0;
        while (position < bytes.Length)
        {
            var rest = bytes[position..];
            if (rest.StartsWith("<!--"u8))
            {
                // "<!-->" and "<!--->" end where the comment ends, at the first "-->" from its "<!--".
                var end = bytes[(position + 2)..].IndexOf("-->"u8);
                if (end < 0) return null;
                position += 2 + end + 3;
                continue;
            }
            if (rest.Length > 5 && AsciiLowerEquals(rest[..5], "<meta"u8)
                && (Space(rest[5]) || rest[5] == '/'))
            {
                position += 6;
                if (Meta(bytes, ref position) is { } label)
                {
                    var kind = Label(Encoding.Latin1.GetString(label));
                    if (kind is null)
                        throw new FactsPdfException("FPDF1103",
                            $"Encoding '{Encoding.Latin1.GetString(label).Trim()}' is not supported; FactsPDF decodes UTF-8, UTF-16 and windows-1252 (latin1, iso-8859-1, ascii).");
                    // A meta element cannot switch to UTF-16: the bytes it is written in are not UTF-16.
                    return kind is HtmlEncodingKind.Utf16LittleEndian or HtmlEncodingKind.Utf16BigEndian ? HtmlEncodingKind.Utf8 : kind;
                }
                continue;
            }
            if (rest.Length > 1 && rest[0] == '<' && (IsAsciiLetter(rest[1]) || rest.Length > 2 && rest[1] == '/' && IsAsciiLetter(rest[2])))
            {
                var nameStart = position + (rest[1] == '/' ? 2 : 1);
                position = nameStart;
                while (position < bytes.Length && !Space(bytes[position]) && bytes[position] != '>') position++;
                // Past the first 1024 bytes, a start tag that cannot be in head ends the search.
                if (rest[1] != '/' && position > UnconditionalPrescanBytes && !HeadTag(bytes[nameStart..position])) return null;
                while (Attribute(bytes, ref position, out _, out _)) { }
                continue;
            }
            if (rest.Length > 1 && rest[0] == '<' && rest[1] is (byte)'!' or (byte)'/' or (byte)'?')
            {
                var end = rest.IndexOf((byte)'>');
                if (end < 0) return null;
                position += end + 1;
                continue;
            }
            // Text past the first 1024 bytes (other than white space) is body content.
            if (position >= UnconditionalPrescanBytes && rest[0] != '<' && !Space(rest[0])) return null;
            position++;
        }
        return null;
    }

    // The attributes of one meta element: the label its charset attribute, or the charset its
    // http-equiv=content-type content names, declares; null when it declares none.
    private static byte[]? Meta(ReadOnlySpan<byte> bytes, ref int position)
    {
        bool gotPragma = false, seenHttpEquiv = false, seenContent = false, seenCharset = false;
        bool? needPragma = null;
        byte[]? charset = null;
        while (Attribute(bytes, ref position, out var name, out var value))
        {
            if (AsciiLowerEquals(name, "http-equiv"u8))
            {
                if (seenHttpEquiv) continue;
                seenHttpEquiv = true;
                if (AsciiLowerEquals(value, "content-type"u8)) gotPragma = true;
            }
            else if (AsciiLowerEquals(name, "content"u8))
            {
                if (seenContent) continue;
                seenContent = true;
                if (charset is null && ContentCharset(Encoding.Latin1.GetString(value)) is { } named)
                {
                    charset = Encoding.Latin1.GetBytes(named);
                    needPragma = true;
                }
            }
            else if (AsciiLowerEquals(name, "charset"u8))
            {
                if (seenCharset) continue;
                seenCharset = true;
                if (charset is not null) continue;
                charset = value.ToArray();
                needPragma = false;
            }
        }
        if (needPragma is null || needPragma == true && !gotPragma) return null;
        return charset;
    }

    // The HTML Standard's "get an attribute" for the prescan: false at the end of the tag.
    private static bool Attribute(ReadOnlySpan<byte> bytes, ref int position, out ReadOnlySpan<byte> name, out ReadOnlySpan<byte> value)
    {
        name = value = default;
        while (position < bytes.Length && (Space(bytes[position]) || bytes[position] == '/')) position++;
        if (position >= bytes.Length) return false;
        if (bytes[position] == '>') { position++; return false; }
        var nameStart = position;
        // The first character may be '=' (it is then part of the name).
        position++;
        while (position < bytes.Length && bytes[position] is not ((byte)'=' or (byte)'/' or (byte)'>') && !Space(bytes[position])) position++;
        name = bytes[nameStart..position];
        while (position < bytes.Length && Space(bytes[position])) position++;
        if (position >= bytes.Length || bytes[position] != '=') return true;
        position++;
        while (position < bytes.Length && Space(bytes[position])) position++;
        if (position >= bytes.Length) return true;
        if (bytes[position] is (byte)'"' or (byte)'\'')
        {
            var quote = bytes[position++];
            var end = bytes[position..].IndexOf(quote);
            if (end < 0) { position = bytes.Length; return false; }
            value = bytes.Slice(position, end);
            position += end + 1;
            return true;
        }
        if (bytes[position] == '>') return true;
        var valueStart = position;
        while (position < bytes.Length && !Space(bytes[position]) && bytes[position] != '>') position++;
        value = bytes[valueStart..position];
        return true;
    }

    private static bool HeadTag(ReadOnlySpan<byte> name) => AsciiLowerEquals(name, "html"u8) || AsciiLowerEquals(name, "head"u8)
        || AsciiLowerEquals(name, "meta"u8) || AsciiLowerEquals(name, "link"u8) || AsciiLowerEquals(name, "base"u8)
        || AsciiLowerEquals(name, "title"u8) || AsciiLowerEquals(name, "style"u8) || AsciiLowerEquals(name, "script"u8)
        || AsciiLowerEquals(name, "noscript"u8) || AsciiLowerEquals(name, "template"u8);

    private static bool AsciiLowerEquals(ReadOnlySpan<byte> value, ReadOnlySpan<byte> lower)
    {
        if (value.Length != lower.Length) return false;
        for (var i = 0; i < value.Length; i++)
        {
            var b = value[i];
            if (b is >= (byte)'A' and <= (byte)'Z') b += 32;
            if (b != lower[i]) return false;
        }
        return true;
    }

    private static bool Space(byte b) => b is 0x09 or 0x0A or 0x0C or 0x0D or 0x20;
    private static bool IsAsciiLetter(byte b) => b is >= (byte)'a' and <= (byte)'z' or >= (byte)'A' and <= (byte)'Z';
}
