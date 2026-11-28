namespace FactsPDF;

/// <summary>
/// The meta element's attributes, judged as a browser's print output does. A meta element only
/// names metadata a static render never reads, with the exceptions checked here: a character set
/// other than UTF-8 would decode the source differently, <c>color-scheme</c> resolving to dark
/// repaints the canvas and text, a refresh navigates away and a Content-Security-Policy can block
/// the page's own styles. Those fail with FPDF1103; every other meta changes nothing.
/// </summary>
internal static class HtmlMeta
{
    private static readonly System.Buffers.SearchValues<char> CharsetEnd = System.Buffers.SearchValues.Create(" \t\n\f\r;");

    /// <summary>Validates the attributes of one meta start tag; throws FPDF1103 for what is not supported.</summary>
    internal static void Validate(HtmlToken token)
    {
        var attributes = token.Attributes!;
        attributes.TryGetValue("name", out var name);
        attributes.TryGetValue("content", out var content);
        if (attributes.TryGetValue("charset", out var charset) && !Utf8Label(charset))
            throw Unsupported("A meta charset other than UTF-8 is not supported: the source is read as UTF-8.", token);
        // The name matches ASCII case-insensitively and is not trimmed: Chrome ignores " color-scheme".
        if (name is not null && name.Equals("color-scheme", StringComparison.OrdinalIgnoreCase)
            && content is not null && ResolvesToDark(content))
            throw Unsupported("A dark meta color-scheme repaints the canvas and text colours and is not supported.", token);
        if (!attributes.TryGetValue("http-equiv", out var equiv)) return;
        equiv = equiv.Trim();
        if (equiv.Equals("refresh", StringComparison.OrdinalIgnoreCase))
            throw Unsupported("meta http-equiv=refresh navigates away from the page and is not supported.", token);
        if (equiv.Equals("content-security-policy", StringComparison.OrdinalIgnoreCase)
            || equiv.Equals("content-security-policy-report-only", StringComparison.OrdinalIgnoreCase))
        {
            // The report-only policy blocks nothing; the enforced one can block inline styles and images.
            if (equiv.Length == "content-security-policy".Length)
                throw Unsupported("meta http-equiv=Content-Security-Policy can block the page's own styles and is not supported.", token);
        }
        else if (equiv.Equals("content-type", StringComparison.OrdinalIgnoreCase) && content is not null
            && CharsetOf(content) is { } declared && !Utf8Label(declared))
            throw Unsupported("A meta content-type charset other than UTF-8 is not supported: the source is read as UTF-8.", token);
    }

    private static FactsPdfException Unsupported(string message, HtmlToken token) => new("FPDF1103", message, token.Offset);

    // The WHATWG Encoding labels of UTF-8, matched after stripping ASCII white space.
    private static bool Utf8Label(string label)
    {
        var value = label.AsSpan().Trim(" \t\n\f\r");
        return value.Equals("utf-8", StringComparison.OrdinalIgnoreCase) || value.Equals("utf8", StringComparison.OrdinalIgnoreCase)
            || value.Equals("unicode-1-1-utf-8", StringComparison.OrdinalIgnoreCase)
            || value.Equals("unicode11utf8", StringComparison.OrdinalIgnoreCase)
            || value.Equals("unicode20utf8", StringComparison.OrdinalIgnoreCase)
            || value.Equals("x-unicode20utf8", StringComparison.OrdinalIgnoreCase);
    }

    // The charset a content-type value declares ("text/html; charset=x"), or null without one.
    private static string? CharsetOf(string content)
    {
        var at = content.IndexOf("charset", StringComparison.OrdinalIgnoreCase);
        if (at < 0) return null;
        var rest = content.AsSpan(at + "charset".Length).TrimStart(" \t\n\f\r");
        if (rest.IsEmpty || rest[0] != '=') return null;
        rest = rest[1..].TrimStart(" \t\n\f\r");
        if (!rest.IsEmpty && rest[0] is '"' or '\'')
        {
            var quote = rest[0]; rest = rest[1..];
            var end = rest.IndexOf(quote);
            return end < 0 ? null : rest[..end].ToString();
        }
        var stop = rest.IndexOfAny(CharsetEnd);
        return (stop < 0 ? rest : rest[..stop]).ToString();
    }

    /// <summary>
    /// Whether a color-scheme content value makes a browser print on a dark canvas. The value is the
    /// CSS property grammar, <c>normal | [ light | dark | &lt;custom-ident&gt; ]+ &amp;&amp; only?</c>, and
    /// an invalid value is ignored. Printing prefers light, so a list that also names light stays
    /// light; only a list that names dark and not light is dark.
    /// </summary>
    internal static bool ResolvesToDark(string content)
    {
        var words = new List<string>();
        var rest = content.AsSpan();
        while (true)
        {
            rest = TrimLeading(rest);
            if (rest.IsEmpty) break;
            var end = 0;
            while (end < rest.Length && !IsSeparator(rest[end])) end++;
            if (end == 0) return false; // a comma, bang or other non-identifier token invalidates the value
            words.Add(rest[..end].ToString().ToLowerInvariant());
            rest = rest[end..];
        }
        var onlyAt = -1; var dark = false; var light = false; var names = 0;
        for (var i = 0; i < words.Count; i++)
        {
            var word = words[i];
            if (word == "only")
            {
                if (onlyAt >= 0 || i != 0 && i != words.Count - 1) return false;
                onlyAt = i;
                continue;
            }
            if (word is "normal" or "inherit" or "initial" or "unset" or "revert" or "revert-layer" or "default") return false;
            if (word == "dark") dark = true;
            else if (word == "light") light = true;
            else if (!ValidIdentifier(word)) return false;
            names++;
        }
        return names > 0 && dark && !light;
    }

    // Skips white space and CSS comments.
    private static ReadOnlySpan<char> TrimLeading(ReadOnlySpan<char> text)
    {
        while (true)
        {
            text = text.TrimStart(" \t\n\f\r");
            if (!text.StartsWith("/*", StringComparison.Ordinal)) return text;
            var end = text[2..].IndexOf("*/", StringComparison.Ordinal);
            if (end < 0) return ReadOnlySpan<char>.Empty;
            text = text[(end + 4)..];
        }
    }

    private static bool IsSeparator(char ch) => ch is ' ' or '\t' or '\n' or '\f' or '\r' or '/' or ',' or '!' or ';' or '(' or ')' or '"' or '\'';

    private static bool ValidIdentifier(string word)
    {
        if (word.Length == 0 || char.IsAsciiDigit(word[0])) return false;
        if (word[0] == '-' && (word.Length == 1 || char.IsAsciiDigit(word[1]))) return false;
        foreach (var ch in word)
            if (!(char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_' || ch >= '\u0080')) return false;
        return true;
    }
}
