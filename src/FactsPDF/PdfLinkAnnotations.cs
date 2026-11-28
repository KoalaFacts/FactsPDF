using System.Text;

namespace FactsPDF;

/// <summary>Page-local PDF URI link annotations over actually measured text runs.</summary>
internal sealed class PdfLinkAnnotations
{
    private sealed record Link(int Id, double Left, double Bottom, double Right, double Top, string Uri);
    private readonly List<Link> entries = [];
    private readonly List<int>[] pageIds;

    internal int Count => entries.Count;

    internal PdfLinkAnnotations(IReadOnlyList<LayoutPage> pages, PdfOptions options, int firstId,
        CancellationToken cancellation)
    {
        pageIds = new List<int>[pages.Count];
        for (var page = 0; page < pages.Count; page++)
        {
            cancellation.ThrowIfCancellationRequested();
            pageIds[page] = [];
            foreach (var run in pages[page].Runs)
            {
                cancellation.ThrowIfCancellationRequested();
                if (run.Style.LinkUri is not { } uri || run.Width <= 0) continue;
                var left = Math.Max(0, run.X);
                var right = Math.Min(options.PageWidth, run.X + run.Width);
                // Courier keeps the existing accessible text hitbox. For
                // embedded TrueType, expand only where used glyph bounds
                // actually extend beyond it (e.g. precomposed accents).
                var above = 0.83d;
                var below = 0.22d;
                if (run.Font is { } font)
                    foreach (var rune in run.Text.EnumerateRunes())
                    {
                        cancellation.ThrowIfCancellationRequested();
                        var (minY, maxY) = font.GlyphVerticalBounds1000(font.GlyphFor(rune.Value));
                        above = Math.Max(above, maxY / 1000d);
                        below = Math.Max(below, -minY / 1000d);
                    }
                var bottom = Math.Max(0, run.Baseline - run.Style.FontSize * below);
                var top = Math.Min(options.PageHeight, run.Baseline + run.Style.FontSize * above);
                if (!double.IsFinite(left) || !double.IsFinite(right) ||
                    !double.IsFinite(bottom) || !double.IsFinite(top) ||
                    left >= right || bottom >= top)
                    throw new FactsPdfException("FPDF1302",
                        "Linked text must have a visible bounded PDF annotation rectangle.", run.SourceOffset);
                var id = checked(firstId + entries.Count);
                entries.Add(new(id, left / options.PrintShrink, bottom / options.PrintShrink, right / options.PrintShrink, top / options.PrintShrink, uri));
                pageIds[page].Add(id);
            }
            // An inline image inside a link is clickable over its box, as Chrome's link
            // rectangles cover the image fragment.
            foreach (var image in pages[page].Images)
            {
                cancellation.ThrowIfCancellationRequested();
                if (image.LinkUri is not { } uri) continue;
                var left = Math.Max(0, image.X);
                var right = Math.Min(options.PageWidth, image.X + image.Width);
                var bottom = Math.Max(0, options.PageHeight - image.Top - image.Height);
                var top = Math.Min(options.PageHeight, options.PageHeight - image.Top);
                if (!double.IsFinite(left) || !double.IsFinite(right) || !double.IsFinite(bottom) || !double.IsFinite(top)
                    || left >= right || bottom >= top)
                    throw new FactsPdfException("FPDF1302",
                        "A linked image must have a visible bounded PDF annotation rectangle.", image.SourceOffset);
                var id = checked(firstId + entries.Count);
                entries.Add(new(id, left / options.PrintShrink, bottom / options.PrintShrink, right / options.PrintShrink, top / options.PrintShrink, uri));
                pageIds[page].Add(id);
            }
        }
    }

    /// <summary>Appends the page's <c>/Annots</c> entry (nothing for a page without links).</summary>
    internal void AppendPageEntry(StringBuilder output, int page)
    {
        var ids = pageIds[page];
        if (ids.Count == 0) return;
        output.Append(" /Annots [");
        for (var i = 0; i < ids.Count; i++)
        {
            if (i > 0) output.Append(' ');
            output.Append(ids[i]).Append(" 0 R");
        }
        output.Append(']');
    }

    /// <summary>Appends link <paramref name="index"/> as the complete PDF object <paramref name="id"/>.</summary>
    internal void AppendObject(StringBuilder output, int index, int id)
    {
        var a = entries[index];
        output.Append(id).Append(" 0 obj\n<< /Type /Annot /Subtype /Link /Rect [")
            .AppendInvariant(a.Left, Number).Append(' ').AppendInvariant(a.Bottom, Number).Append(' ')
            .AppendInvariant(a.Right, Number).Append(' ').AppendInvariant(a.Top, Number)
            .Append("] /Border [0 0 0] /A << /S /URI /URI <");
        // An ASCII URI is emitted as a PDF hex string, never interpolated as
        // literal PDF syntax (parentheses, backslashes and ampersands are safe).
        // ValidateUri admits only printable ASCII, so each char is one byte.
        foreach (var c in a.Uri) output.AppendHex(c, 2);
        output.Append("> >> >>\nendobj\n");
    }

    /// <summary>The link target of an <c>a href</c>: a valid absolute HTTPS or mailto URI, or in strict mode the
    /// FPDF1103 failure. Non-strict mode (<paramref name="warnings"/> set) reports the same failure as a warning
    /// and returns null: the text is then drawn as an ordinary link but no annotation is written.</summary>
    internal static string? ValidateUri(string input, int offset, CssWarnings? warnings, Uri? baseUri = null)
    {
        try { return ValidateUri(input, offset, baseUri); }
        catch (FactsPdfException error) when (warnings is not null)
        {
            warnings.Add(error);
            return null;
        }
    }

    /// <summary>The link target Chrome writes for an <c>a href</c>: an absolute HTTPS URL or mailto address as
    /// written (the original subset), an absolute HTTP or HTTPS URL (fragments allowed) already in the serialized
    /// form Chrome writes, or a relative reference resolved against the base element's absolute URL.</summary>
    internal static string ValidateUri(string input, int offset, Uri? baseUri = null)
    {
        try { return ValidateOriginal(input, offset); }
        catch (FactsPdfException)
        {
            if (Serialized(input) is { } absolute) return absolute;
            if (baseUri is not null && Resolve(input, baseUri) is { } resolved) return resolved;
            throw;
        }
    }

    /// <summary>The URL relative link hrefs resolve against: the base href when it is an absolute HTTP or
    /// HTTPS URL in the conservative character set; otherwise null (a relative base would resolve against
    /// the document's own URL, which FactsPDF does not know).</summary>
    internal static Uri? BaseUri(string href)
    {
        if (href.Length is < 8 or > 2048 || !SafeCharacters(href) || DecodesUnreserved(href)) return null;
        if (!href.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !href.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return null;
        return Uri.TryCreate(href, UriKind.Absolute, out var parsed) && parsed.Scheme is "http" or "https"
            && parsed.Host.Length > 0 && parsed.UserInfo.Length == 0 ? parsed : null;
    }

    // An absolute http(s) URL Chrome writes unchanged: its serialization equals the text. Only RFC 3986
    // unreserved and reserved characters (no apostrophe, which a special URL's query percent-encodes) and
    // valid percent escapes are allowed, so the serializations of .NET and of the URL Standard agree.
    private static string? Serialized(string input)
    {
        if (input.Length is < 8 or > 2048 || !SafeCharacters(input)) return null;
        if (!input.StartsWith("http://", StringComparison.Ordinal) && !input.StartsWith("https://", StringComparison.Ordinal)) return null;
        if (!Uri.TryCreate(input, UriKind.Absolute, out var parsed) || parsed.Scheme is not ("http" or "https")
            || parsed.Host.Length == 0 || parsed.UserInfo.Length > 0)
            return null;
        return string.Equals(parsed.AbsoluteUri, input, StringComparison.Ordinal) ? input : null;
    }

    // A relative reference (path, query or fragment) resolved against the base URL, as Chrome resolves it.
    // Scheme-relative references, other schemes and escapes of unreserved characters (which .NET decodes
    // and the URL Standard keeps) are refused.
    private static string? Resolve(string input, Uri baseUri)
    {
        if (input.Length is < 1 or > 2048 || !SafeCharacters(input) || DecodesUnreserved(input)
            || input.StartsWith("//", StringComparison.Ordinal) || HasScheme(input))
            return null;
        if (!Uri.TryCreate(baseUri, input, out var resolved) || resolved.Scheme is not ("http" or "https")
            || resolved.UserInfo.Length > 0)
            return null;
        var text = resolved.AbsoluteUri;
        return text.Length <= 2048 && SafeCharacters(text) ? text : null;
    }

    private static bool SafeCharacters(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (char.IsAsciiLetterOrDigit(c) || c is '-' or '.' or '_' or '~' or ':' or '/' or '?' or '#' or '@'
                or '!' or '$' or '&' or '(' or ')' or '*' or '+' or ',' or ';' or '=') continue;
            if (c == '%' && i + 2 < text.Length && Uri.IsHexDigit(text[i + 1]) && Uri.IsHexDigit(text[i + 2])) { i += 2; continue; }
            return false;
        }
        return true;
    }

    // Whether a percent escape encodes an unreserved character (ALPHA, DIGIT, '-', '.', '_', '~').
    private static bool DecodesUnreserved(string text)
    {
        for (var i = text.IndexOf('%'); i >= 0 && i + 2 < text.Length; i = text.IndexOf('%', i + 1))
        {
            var value = int.Parse(text.AsSpan(i + 1, 2), System.Globalization.NumberStyles.AllowHexSpecifier, System.Globalization.CultureInfo.InvariantCulture);
            if (char.IsAsciiLetterOrDigit((char)value) || value is '-' or '.' or '_' or '~') return true;
        }
        return false;
    }

    private static bool HasScheme(string text)
    {
        if (text.Length == 0 || !char.IsAsciiLetter(text[0])) return false;
        for (var i = 1; i < text.Length; i++)
        {
            var c = text[i];
            if (c == ':') return true;
            if (!(char.IsAsciiLetterOrDigit(c) || c is '+' or '-' or '.')) return false;
        }
        return false;
    }

    private static string ValidateOriginal(string input, int offset)
    {
        if (input.Length is < 8 or > 2048 ||
            input.Any(c => c is < '!' or > '~' or '"' or '<' or '>' or '\\' || c == (char)96))
            throw Invalid(offset);
        for (var i = 0; i < input.Length; i++)
        {
            if (input[i] != '%') continue;
            if (i + 2 >= input.Length || !Uri.IsHexDigit(input[i + 1]) || !Uri.IsHexDigit(input[i + 2]))
                throw Invalid(offset);
            i += 2;
        }
        if (!Uri.TryCreate(input, UriKind.Absolute, out var parsed))
            throw Invalid(offset);
        if (input.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
            parsed.Scheme == Uri.UriSchemeHttps && parsed.Host.Length > 0 &&
            parsed.UserInfo.Length == 0 && parsed.Fragment.Length == 0)
            return input;
        if (input.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) &&
            parsed.Scheme == Uri.UriSchemeMailto && !input.Contains('?') && !input.Contains('#'))
        {
            if (IsPlainMailbox(input[7..])) return input;
        }
        throw Invalid(offset);
    }

    // Deliberately narrow plain ASCII mailbox subset: not general RFC syntax.
    // Both local and domain segments must be complete, not URL paths or ports.
    private static bool IsPlainMailbox(string address)
    {
        var at = address.IndexOf('@');
        if (at is < 1 or > 64 || at != address.LastIndexOf('@')) return false;
        var local = address[..at];
        var domain = address[(at + 1)..];
        if (domain.Length is < 1 or > 253 || local[0] == '.' || local[^1] == '.' ||
            local.Contains("..", StringComparison.Ordinal) ||
            !local.All(c => char.IsAsciiLetterOrDigit(c) || c == '.' || c == '+' || c == '-' || c == '_'))
            return false;
        return domain.Split('.').All(label =>
            label.Length is > 0 and <= 63 && label[0] != '-' && label[^1] != '-' &&
            label.All(c => char.IsAsciiLetterOrDigit(c) || c == '-'));
    }

    private static FactsPdfException Invalid(int offset) => new("FPDF1103",
        "Link href requires an absolute ASCII HTTPS URL or one mailto address, an absolute HTTP(S) URL written as Chrome serializes it, or a relative reference with an absolute HTTP(S) base href (max 2048 chars); in-document #fragment links are not supported.", offset);

    private const string Number = "0.######";
}
