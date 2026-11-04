using System.Globalization;
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
                        "Linked text must have a visible bounded PDF annotation rectangle.");
                var id = checked(firstId + entries.Count);
                entries.Add(new(id, left, bottom, right, top, uri));
                pageIds[page].Add(id);
            }
        }
    }

    internal string PageEntry(int page)
        => pageIds[page].Count == 0 ? "" :
            " /Annots [" + string.Join(" ", pageIds[page].Select(id =>
                id.ToString(CultureInfo.InvariantCulture) + " 0 R")) + "]";

    internal string ObjectBody(int index)
    {
        var a = entries[index];
        // An ASCII URI is emitted as a PDF hex string, never interpolated as
        // literal PDF syntax (parentheses, backslashes and ampersands are safe).
        var uriHex = Convert.ToHexString(Encoding.ASCII.GetBytes(a.Uri));
        return FormattableString.Invariant(
            $"<< /Type /Annot /Subtype /Link /Rect [{N(a.Left)} {N(a.Bottom)} {N(a.Right)} {N(a.Top)}] /Border [0 0 0] /A << /S /URI /URI <{uriHex}> >> >>");
    }

    internal static string ValidateUri(string input, int offset)
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
        "Link href requires an absolute ASCII HTTPS URL or one mailto address (max 2048 chars).", offset);

    private static string N(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);
}
