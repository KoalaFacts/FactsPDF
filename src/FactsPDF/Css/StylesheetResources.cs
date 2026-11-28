using System.Collections.ObjectModel;

namespace FactsPDF;

/// <summary>
/// Caller-supplied stylesheets that <c>&lt;link rel="stylesheet" href="..."&gt;</c> and
/// <c>@import</c> refer to by their exact URL text. FactsPDF never downloads anything: an external
/// stylesheet renders only when the caller supplies its text under the href written in the page.
/// </summary>
internal static class StylesheetResources
{
    /// <summary>The longest href a stylesheet resource may be named by.</summary>
    internal const int MaxNameLength = 2048;

    /// <summary>The deepest chain of nested <c>@import</c> rules followed.</summary>
    internal const int MaxImportDepth = 16;

    internal static readonly IReadOnlyDictionary<string, string> Empty =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal));

    /// <summary>Copies and checks the caller's stylesheet map once per conversion.</summary>
    internal static PdfOptions Snapshot(PdfOptions options, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (options.Stylesheets is null) throw new ArgumentException("Supply a non-null stylesheet resource map.", nameof(options));
        if (ReferenceEquals(options.Stylesheets, Empty)) return options;
        var sheets = new Dictionary<string, string>(StringComparer.Ordinal);
        long characters = 0;
        foreach (var (name, css) in options.Stylesheets)
        {
            cancellation.ThrowIfCancellationRequested();
            if (sheets.Count == options.MaxStylesheetResources)
                throw new FactsPdfException("FPDF1205", "Stylesheets exceed MaxStylesheetResources.");
            if (!ValidName(name) || css is null)
                throw new ArgumentException("Stylesheet entries require a valid href and non-null CSS text.", nameof(options));
            if (!sheets.TryAdd(name, css)) throw new ArgumentException("Stylesheet hrefs must be unique.", nameof(options));
            // Each sheet is charged against MaxCssCharacters again whenever it is linked or imported;
            // the supplied set as a whole must fit once.
            characters += css.Length;
            if (characters > options.MaxCssCharacters)
                throw new FactsPdfException("FPDF1205", "Supplied stylesheets exceed MaxCssCharacters.");
        }
        return options with { Stylesheets = new ReadOnlyDictionary<string, string>(sheets) };
    }

    /// <summary>An href as written in the page: 1 to 2048 printable ASCII characters without white
    /// space, quotes, angle brackets, backslashes or backticks.</summary>
    internal static bool ValidName(string? name)
    {
        if (name is null || name.Length is < 1 or > MaxNameLength) return false;
        foreach (var c in name)
            if (c is < '!' or > '~' or '"' or '\'' or '<' or '>' or '\\' or '`') return false;
        return true;
    }
}
