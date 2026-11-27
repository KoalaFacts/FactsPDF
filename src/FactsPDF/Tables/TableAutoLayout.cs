using System.Globalization;

namespace FactsPDF;

/// <summary>
/// Automatic table layout: column widths from the cells' min-content and max-content widths, for a
/// table whose columns have no explicit width (no <c>colgroup</c>, or <c>col</c> elements without one).
/// </summary>
/// <remarks>
/// <para>
/// Each column's minimum is the widest min-content contribution of its cells and its maximum the
/// widest max-content contribution, both including the cell's horizontal padding and borders. The
/// table's used content width is its explicit width, or with <c>width:auto</c> the sum of the
/// maximums capped at the available width; it never drops below the sum of the minimums.
/// </para>
/// <para>
/// The width is then shared out the way the automatic table algorithm (and Chrome) does it: at the
/// sum of the minimums every column gets its minimum; between the two sums each column gets its
/// minimum plus a share of the extra width in proportion to its own max-minus-min range (all columns
/// get exactly their maximums when the width equals the sum of the maximums); above the sum of the
/// maximums the extra width goes to the columns in proportion to their maximums, or equally when
/// every maximum is zero. Any rounding remainder goes to the last column.
/// </para>
/// </remarks>
internal static class TableAutoLayout
{
    /// <summary>
    /// Fills <paramref name="tracks"/> with the automatic column widths and returns the table's used
    /// content width, or throws FPDF1302 when the columns cannot fit the available width.
    /// </summary>
    internal static double Resolve(TableNode table, double? specifiedWidth, double availableWidth, double[] tracks,
        PdfOptions options, Dictionary<(FontRole Role, int Scalar), (PdfFont? Font, double Width)> resolved,
        CancellationToken cancellation)
    {
        // tracks holds the column minimums while measuring; maximums are kept separately.
        var maximums = new double[tracks.Length];
        List<(int Start, int Span, double Min, double Max)>? spanning = null;
        foreach (var row in table.Rows)
        {
            cancellation.ThrowIfCancellationRequested();
            var column = 0;
            foreach (var cell in row.Cells)
            {
                cancellation.ThrowIfCancellationRequested();
                var (min, max) = CellWidths(cell, options, resolved, cancellation);
                if (cell.ColumnSpan == 1)
                {
                    tracks[column] = Math.Max(tracks[column], min);
                    maximums[column] = Math.Max(maximums[column], max);
                }
                else (spanning ??= []).Add((column, cell.ColumnSpan, min, max));
                column += cell.ColumnSpan;
            }
        }
        for (var i = 0; i < tracks.Length; i++) maximums[i] = Math.Max(maximums[i], tracks[i]);
        if (spanning is not null)
        {
            // Spanning cells widen the columns they cover, narrowest spans first (in source order
            // within one span): the cell's minimum is shared out over the covered columns' minimums
            // and maximums as a table width would be, and each column keeps the larger minimum;
            // then the cell's maximum likewise raises the columns' maximums.
            var shares = new double[TableMarkup.MaxColumns];
            foreach (var (start, span, min, max) in spanning.OrderBy(static cell => cell.Span))
            {
                cancellation.ThrowIfCancellationRequested();
                Widen(tracks.AsSpan(start, span), maximums.AsSpan(start, span), shares.AsSpan(0, span), min, raiseMaximums: false);
                Widen(tracks.AsSpan(start, span), maximums.AsSpan(start, span), shares.AsSpan(0, span), max, raiseMaximums: true);
            }
        }
        double minimum = 0, maximum = 0;
        for (var i = 0; i < tracks.Length; i++)
        {
            minimum += tracks[i];
            maximum += maximums[i];
        }
        // An explicit width wider than the container fails in the caller's table width check.
        if (minimum > availableWidth + 0.000001)
            throw Geometry(string.Create(CultureInfo.InvariantCulture,
                $"Automatic table columns need at least {minimum:0.###}pt, but only {availableWidth:0.###}pt is available."),
                table.SourceOffset);
        var target = Math.Max(minimum, specifiedWidth ?? Math.Min(availableWidth, maximum));
        Distribute(tracks, maximums, minimum, maximum, target);
        return target;
    }

    // Raises the minimums (or, with raiseMaximums, the maximums) of the columns a spanning cell
    // covers to the share each gets of the cell's width.
    private static void Widen(Span<double> minimums, Span<double> maximums, Span<double> shares, double width, bool raiseMaximums)
    {
        double minimum = 0, maximum = 0;
        for (var i = 0; i < minimums.Length; i++)
        {
            minimum += minimums[i];
            maximum += maximums[i];
        }
        minimums.CopyTo(shares);
        Distribute(shares, maximums, minimum, maximum, width);
        for (var i = 0; i < minimums.Length; i++)
        {
            if (raiseMaximums) maximums[i] = Math.Max(maximums[i], shares[i]);
            else
            {
                minimums[i] = Math.Max(minimums[i], shares[i]);
                maximums[i] = Math.Max(maximums[i], minimums[i]);
            }
        }
    }

    /// <summary>
    /// Shares <paramref name="target"/> out over columns with the given minimums (in
    /// <paramref name="tracks"/>, replaced by the result) and maximums.
    /// </summary>
    internal static void Distribute(Span<double> tracks, ReadOnlySpan<double> maximums, double minimum, double maximum, double target)
    {
        if (target <= minimum) return;
        if (target == maximum)
        {
            maximums.CopyTo(tracks);
            return;
        }
        var last = tracks.Length - 1;
        var assigned = 0d;
        if (target < maximum)
        {
            var range = maximum - minimum;
            var extra = target - minimum;
            for (var i = 0; i < last; i++)
            {
                tracks[i] += extra * (maximums[i] - tracks[i]) / range;
                assigned += tracks[i];
            }
        }
        else
        {
            var extra = target - maximum;
            for (var i = 0; i < last; i++)
            {
                tracks[i] = maximums[i] + (maximum > 0 ? extra * maximums[i] / maximum : extra / tracks.Length);
                assigned += tracks[i];
            }
        }
        // The last column takes the remainder, so the columns sum to the table width exactly.
        tracks[last] = Math.Max(tracks[last], target - assigned);
    }

    /// <summary>A cell's min-content and max-content widths, including its horizontal padding and borders.</summary>
    internal static (double Min, double Max) CellWidths(TableCell cell, PdfOptions options,
        Dictionary<(FontRole Role, int Scalar), (PdfFont? Font, double Width)> resolved, CancellationToken cancellation)
    {
        var box = cell.BoxStyle;
        if (box.PaddingLeft.IsPercent || box.PaddingRight.IsPercent || box.PaddingTop.IsPercent || box.PaddingBottom.IsPercent)
            throw Geometry("Percentage cell padding is not supported in automatic table layout; give every column an explicit col width.", cell.SourceOffset);
        var edges = box.PaddingLeft.Value + box.BorderLeft.EffectiveWidth + box.PaddingRight.Value + box.BorderRight.EffectiveWidth;
        double min = 0, max = 0;
        foreach (var content in cell.Contents)
        {
            cancellation.ThrowIfCancellationRequested();
            if (content is TableCellImage { Image: var image })
            {
                var style = image.BoxStyle;
                if (style.Width is { IsPercent: true } || style.MinWidth is { IsPercent: true } || style.MaxWidth is { IsPercent: true }
                    || style.Height is { IsPercent: true } || style.MinHeight is { IsPercent: true } || style.MaxHeight is { IsPercent: true }
                    || style.PaddingLeft.IsPercent || style.PaddingRight.IsPercent || style.PaddingTop.IsPercent || style.PaddingBottom.IsPercent)
                    throw Geometry("Percentage image sizes are not supported in automatic table layout; give every column an explicit col width.", image.SourceOffset);
                // Without percentages the image's outer width does not depend on the cell width;
                // the largest supported length only lets the fit check pass.
                var width = ImageLayout.Resolve(image, 14_400).OuterWidth;
                min = Math.Max(min, width);
                max = Math.Max(max, width);
                continue;
            }
            var paragraph = ((TableCellText)content).Paragraph;
            if (paragraph.Style.FirstLineIndent.IsPercent && paragraph.Style.FirstLineIndent.Value != 0)
                throw Geometry("Percentage text-indent is not supported in automatic table layout; give every column an explicit col width.", paragraph.SourceOffset);
            try
            {
                var (paragraphMin, paragraphMax) = TextLines.IntrinsicWidths(paragraph, options, resolved, cancellation);
                min = Math.Max(min, paragraphMin);
                max = Math.Max(max, paragraphMax);
            }
            catch (FactsPdfException error) when (error.SourceOffset < 0)
            {
                throw new FactsPdfException(error.Code, error.Message[(error.Code.Length + 2)..], paragraph.SourceOffset);
            }
        }
        return (min + edges, max + edges);
    }

    private static FactsPdfException Geometry(string message, int offset) => new("FPDF1302", message, offset);
}
