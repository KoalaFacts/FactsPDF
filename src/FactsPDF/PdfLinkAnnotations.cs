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
                var bottom = Math.Max(0, run.Baseline - run.Style.FontSize * 0.22);
                var top = Math.Min(options.PageHeight, run.Baseline + run.Style.FontSize * 0.83);
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
            var address = input[7..];
            if (address.Count(c => c == '@') == 1 &&
                !address.Contains(',') && !address.Contains(';') && !address.Contains('%') &&
                address.IndexOf('@') is > 0 and < 255 &&
                address.IndexOf('@') < address.Length - 1)
                return input;
        }
        throw Invalid(offset);
    }

    private static FactsPdfException Invalid(int offset) => new("FPDF1103",
        "Link href requires an absolute ASCII HTTPS URL or one mailto address (max 2048 chars).", offset);

    private static string N(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);
}
