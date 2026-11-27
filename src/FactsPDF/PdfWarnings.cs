namespace FactsPDF;

/// <summary>CSS that is invalid per the CSS specifications and that non-strict mode
/// (<see cref="PdfWriter.WithStrict(bool)"/>) dropped exactly as a browser drops it.</summary>
/// <remarks>Strict mode, the default, fails with a <see cref="FactsPdfException"/> carrying the same
/// <see cref="Code"/>, <see cref="Message"/> and <see cref="SourceOffset"/>. Branch on <see cref="Code"/>,
/// not on the message text, which may change between releases.</remarks>
/// <param name="Code">Stable error code in the form <c>FPDF####</c> that strict mode would have thrown.</param>
/// <param name="Message">Human-readable explanation, without the code prefix.</param>
/// <param name="SourceOffset">Zero-based UTF-16 offset into the HTML near the dropped CSS, or -1 when not available.</param>
public sealed record PdfWarning(string Code, string Message, int SourceOffset)
{
    /// <summary>The warning as <c>FPDF####: message</c>, the form a <see cref="FactsPdfException"/> message takes.</summary>
    public override string ToString() => $"{Code}: {Message}";
}

/// <summary>The warnings of one successful conversion, reported to the handler set with
/// <see cref="PdfWriter.WithWarnings(Action{PdfWarnings})"/>.</summary>
public sealed class PdfWarnings
{
    internal PdfWarnings(IReadOnlyList<PdfWarning> items, int totalCount)
    {
        Items = items;
        TotalCount = totalCount;
    }

    /// <summary>The recorded warnings in source order (by <see cref="PdfWarning.SourceOffset"/>, then in
    /// the order they were found), at most <see cref="PdfLimitsSetup.MaxCssWarnings(int)"/> of them. A
    /// declaration that is dropped for several elements is recorded once.</summary>
    public IReadOnlyList<PdfWarning> Items { get; }

    /// <summary>The number of warnings found, including those beyond the limit, which are counted but
    /// not recorded (and not deduplicated).</summary>
    public int TotalCount { get; }
}

/// <summary>Collects the warnings of one non-strict conversion: the first
/// <see cref="PdfOptions.MaxCssWarnings"/> distinct ones are stored, later ones only counted, so
/// hostile input cannot allocate unbounded diagnostics.</summary>
internal sealed class CssWarnings(int capacity)
{
    private readonly List<PdfWarning> items = [];
    private readonly HashSet<(string Code, int Offset)> recorded = [];

    /// <summary>Every warning found so far, recorded or not.</summary>
    internal int Total { get; private set; }

    internal void Add(FactsPdfException error)
    {
        var prefix = error.Code + ": ";
        var message = error.Message.StartsWith(prefix, StringComparison.Ordinal) ? error.Message[prefix.Length..] : error.Message;
        Add(error.Code, message, error.SourceOffset);
    }

    internal void Add(string code, string message, int offset)
    {
        if (recorded.Contains((code, offset))) return;
        Total++;
        if (items.Count >= capacity) return;
        items.Add(new PdfWarning(code, message, offset));
        recorded.Add((code, offset));
    }

    internal PdfWarnings Report() =>
        new(items.Select((warning, index) => (warning, index)).OrderBy(x => x.warning.SourceOffset).ThenBy(x => x.index)
            .Select(x => x.warning).ToArray(), Total);
}
