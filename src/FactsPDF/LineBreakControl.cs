namespace FactsPDF;

/// <summary>
/// CSS Fragmentation orphans and widows for a paragraph's line boxes, placed as Chrome places them.
/// </summary>
/// <remarks>
/// Every paragraph uses its computed values, 2 (the CSS initial value) where none is declared.
/// Chrome's placement, measured on synthetic printed pages: a paragraph that cannot leave
/// <c>orphans</c> lines on the current page moves whole to the next one when there is content
/// before it to break after; otherwise the break comes where the page runs out. A fragment that
/// can keep its orphans ends early enough to give the next page <c>widows</c> lines, but never
/// before its <c>orphans</c> lines: orphans win when both cannot hold.
/// </remarks>
internal static class LineBreakControl
{
    internal const ushort Initial = 2;

    /// <summary>The computed (orphans, widows); a style with 0 has the initial value 2.</summary>
    internal static (int Orphans, int Widows) Of(TextStyle style) =>
        (style.Orphans == 0 ? Initial : style.Orphans, style.Widows == 0 ? Initial : style.Widows);

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
