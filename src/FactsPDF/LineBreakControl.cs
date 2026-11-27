namespace FactsPDF;

/// <summary>
/// CSS Fragmentation orphans and widows for a paragraph's line boxes, placed as Chrome places them.
/// </summary>
/// <remarks>
/// A paragraph whose computed style has no orphans/widows declaration keeps FactsPDF's established
/// line-by-line page breaks (one line is enough on either side), so existing documents render
/// byte-identical. Once either property reaches the paragraph, both take their CSS values, the
/// undeclared one its initial value 2. Chrome's placement, measured on synthetic printed pages:
/// a paragraph that cannot leave <c>orphans</c> lines on the current page moves whole to the next
/// one when there is content before it to break after; otherwise the break comes where the page
/// runs out. A fragment that can keep its orphans ends early enough to give the next page
/// <c>widows</c> lines, but never before its <c>orphans</c> lines: orphans win when both cannot hold.
/// </remarks>
internal static class LineBreakControl
{
    internal const ushort Initial = 2;

    /// <summary>The effective (orphans, widows), or null for the established line-by-line breaks.</summary>
    internal static (int Orphans, int Widows)? Of(TextStyle style) =>
        style.Orphans == 0 && style.Widows == 0 ? null
            : (style.Orphans == 0 ? Initial : style.Orphans, style.Widows == 0 ? Initial : style.Widows);

    /// <summary>How many of the lines from <paramref name="start"/> fit between top and bottom.</summary>
    internal static int Fitting(IReadOnlyList<double> heights, int start, double top, double bottom)
    {
        var count = 0;
        for (var i = start; i < heights.Count && top + heights[i] <= bottom + 0.000001; i++)
        {
            top += heights[i];
            count++;
        }
        return count;
    }

    /// <summary>
    /// The index of the first line that starts the next page when the fragment from
    /// <paramref name="start"/> begins at top, or <see cref="int.MaxValue"/> when every remaining
    /// line fits. The caller has already moved a paragraph whose first fragment breaks its orphans.
    /// </summary>
    internal static int FragmentEnd(IReadOnlyList<double> heights, int start, double top, double bottom,
        (int Orphans, int Widows) control)
    {
        var fitting = Fitting(heights, start, top, bottom);
        var remaining = heights.Count - start;
        if (fitting >= remaining) return int.MaxValue;
        if (fitting < control.Orphans) return start + fitting;
        return start + Math.Min(fitting, Math.Max(control.Orphans, remaining - control.Widows));
    }
}
