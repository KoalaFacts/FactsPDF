using System.Globalization;

namespace FactsPDF;

/// <summary>
/// Automatic table layout: column widths from the cells' min-content and max-content widths and
/// from any explicit <c>col</c> or cell widths, for a table whose columns do not all have explicit
/// widths (no <c>colgroup</c>, <c>col</c> elements without one, or a mix).
/// </summary>
/// <remarks>
/// <para>
/// Each column has a minimum (the widest min-content contribution of its cells), a maximum (the
/// widest max-content contribution), optionally a percentage, and is constrained when a <c>col</c>
/// or one of its cells gives it a fixed width; contributions include the cell's horizontal padding
/// and borders. A fixed <c>col</c> width is the column's starting minimum and maximum; a fixed cell
/// width raises the cell's maximum to that border-box width, and a constrained column's maximum
/// ignores the maximums of unconstrained cells. Percentages above a running total of 100% are cut.
/// </para>
/// <para>
/// The table's used content width is its explicit width, or with <c>width:auto</c> its max-content
/// width capped at the available width, then limited by <c>max-width</c> and raised by
/// <c>min-width</c>; it never drops below the sum of the minimums. The
/// max-content width is the sum of the maximums, or larger when percentage columns need it: a
/// percentage column of maximum M at P% needs a table of M·100/P, and the other columns' maximums
/// need their sum divided by the percentage left over.
/// </para>
/// <para>
/// The width is then shared out the way Chrome does it, by trying four guesses in order and
/// stopping at the first that reaches the width: every column at its minimum; percentage columns
/// at their percentage of the width; constrained columns at their maximum; auto columns at their
/// maximum. Between two guesses the columns that grow in that step share the extra width in
/// proportion to how much they grow (equally when none grows); past the last guess the extra
/// goes to auto columns in proportion to their maximums, else to constrained columns likewise,
/// else to percentage columns in proportion to their percentages. The last column that grows
/// takes any rounding remainder.
/// </para>
/// </remarks>
internal static class TableAutoLayout
{
    private enum Kind : byte { Auto, Constrained, Percent }

    // The columns of one table (or the covered columns of one spanning cell, as a slice).
    private readonly ref struct Columns(Span<double> minimums, Span<double> maximums, Span<double> percents, Span<bool> constrained)
    {
        internal readonly Span<double> Minimums = minimums;
        internal readonly Span<double> Maximums = maximums;
        internal readonly Span<double> Percents = percents;
        internal readonly Span<bool> Constrained = constrained;
        internal int Count => Minimums.Length;
        internal Kind KindOf(int i) => Percents[i] > 0 ? Kind.Percent : Constrained[i] ? Kind.Constrained : Kind.Auto;
        internal Columns Slice(int start, int length) => new(Minimums.Slice(start, length), Maximums.Slice(start, length),
            Percents.Slice(start, length), Constrained.Slice(start, length));
    }

    // Measures every column's minimum (into tracks), maximum, percentage and constrained flag from
    // the cells and the col widths, spanning cells last.
    private static Columns Collect(TableNode table, double[] tracks, PdfOptions options,
        Dictionary<(FontRole Role, int Scalar), (PdfFont? Font, double Width)> resolved, CancellationToken cancellation)
    {
        // tracks holds the column minimums while measuring.
        var count = tracks.Length;
        var maximums = new double[count];
        var percents = new double[count];
        var constrained = new bool[count];
        var measured = new bool[count];
        for (var i = 0; i < table.Columns.Count && i < count; i++)
        {
            if (table.Columns[i].Width is not { } width) continue;
            if (width.IsPercent)
            {
                percents[i] = width.Value;
                continue;
            }
            tracks[i] = maximums[i] = width.Value;
            constrained[i] = measured[i] = true;
        }
        List<(int Start, int Span, double Min, double Max)>? spanning = null;
        foreach (var row in table.Rows)
        {
            cancellation.ThrowIfCancellationRequested();
            var column = 0;
            foreach (var cell in row.Cells)
            {
                cancellation.ThrowIfCancellationRequested();
                var (min, max) = CellWidths(cell, options, resolved, cancellation);
                var width = cell.BoxStyle.Width;
                if (cell.ColumnSpan != 1)
                {
                    if (width is not null)
                        throw Geometry("A spanning cell cannot have its own width in automatic table layout.", cell.SourceOffset);
                    (spanning ??= []).Add((column, cell.ColumnSpan, min, max));
                    column += cell.ColumnSpan;
                    continue;
                }
                var cellConstrained = width is { IsPercent: false };
                if (width is { IsPercent: false } fixedWidth)
                    max = Math.Max(min, fixedWidth.Value + (cell.BoxStyle.BorderBoxSizing ? 0 : Edges(cell.BoxStyle)));
                if (!measured[column])
                {
                    tracks[column] = min;
                    maximums[column] = max;
                    constrained[column] = cellConstrained;
                    measured[column] = true;
                }
                else
                {
                    tracks[column] = Math.Max(tracks[column], min);
                    if (constrained[column]) { if (cellConstrained) maximums[column] = Math.Max(maximums[column], max); }
                    else maximums[column] = cellConstrained ? max : Math.Max(maximums[column], max);
                    constrained[column] |= cellConstrained;
                }
                maximums[column] = Math.Max(maximums[column], tracks[column]);
                if (width is { IsPercent: true, Value: > 0 } percent && percent.Value > percents[column])
                    percents[column] = percent.Value;
                column++;
            }
        }
        var columns = new Columns(tracks, maximums, percents, constrained);
        CapPercentages(percents);
        if (spanning is not null)
        {
            // Spanning cells widen the columns they cover, narrowest spans first (in source order
            // within one span): the cell's minimum is shared out over the covered columns as a
            // table width would be, and each column keeps the larger minimum; then the cell's
            // maximum likewise raises the columns' maximums.
            var shares = new double[TableMarkup.MaxColumns];
            foreach (var (start, span, min, max) in spanning.OrderBy(static cell => cell.Span))
            {
                cancellation.ThrowIfCancellationRequested();
                var covered = columns.Slice(start, span);
                Widen(covered, shares.AsSpan(0, span), min, raiseMaximums: false);
                Widen(covered, shares.AsSpan(0, span), max, raiseMaximums: true);
            }
        }
        return columns;
    }

    /// <summary>
    /// A table's content-box min-content and max-content widths: the sum of the column minimums and
    /// the table's max-content width, before any width, min-width, max-width or available width.
    /// </summary>
    internal static (double Min, double Max) Content(TableNode table, PdfOptions options,
        Dictionary<(FontRole Role, int Scalar), (PdfFont? Font, double Width)> resolved, CancellationToken cancellation)
    {
        var tracks = new double[table.Columns.Count];
        var columns = Collect(table, tracks, options, resolved, cancellation);
        var minimum = 0d;
        for (var i = 0; i < tracks.Length; i++) minimum += tracks[i];
        return (minimum, Math.Max(minimum, MaxContent(columns)));
    }

    /// <summary>
    /// Fills <paramref name="tracks"/> with the automatic column widths and returns the table's used
    /// content width, or throws FPDF1302 when the columns cannot fit the available width.
    /// </summary>
    internal static double Resolve(TableNode table, double? specifiedWidth, double availableWidth, double[] tracks,
        PdfOptions options, Dictionary<(FontRole Role, int Scalar), (PdfFont? Font, double Width)> resolved,
        CancellationToken cancellation, double? minWidth = null, double? maxWidth = null)
    {
        var count = tracks.Length;
        var columns = Collect(table, tracks, options, resolved, cancellation);
        var minimum = 0d;
        for (var i = 0; i < count; i++) minimum += tracks[i];
        if (minimum > availableWidth + 0.000001)
            throw Geometry(string.Create(CultureInfo.InvariantCulture,
                $"Automatic table columns need at least {minimum:0.###}pt, but only {availableWidth:0.###}pt is available."),
                table.SourceOffset);
        // An explicit width wider than the container fails in the caller's table width check.
        // min-width and max-width constrain the width the columns would otherwise get (min-width
        // wins over max-width); the columns' minimums still floor it, as Chrome reruns the layout
        // at the constrained width.
        var natural = specifiedWidth ?? Math.Min(availableWidth, MaxContent(columns));
        if (maxWidth is { } upper) natural = Math.Min(natural, upper);
        if (minWidth is { } lower) natural = Math.Max(natural, lower);
        var target = Math.Max(minimum, natural);
        Span<double> result = stackalloc double[count];
        Distribute(result, columns, target, constrainedTarget: true);
        result.CopyTo(tracks);
        return target;
    }

    // Percentages past a running total of 100% are cut to what is left, as in browsers.
    private static void CapPercentages(Span<double> percents)
    {
        var total = 0d;
        for (var i = 0; i < percents.Length; i++)
        {
            if (percents[i] <= 0) continue;
            percents[i] = Math.Min(percents[i], Math.Max(0, 100 - total));
            total += percents[i];
        }
    }

    // The table's max-content width: the sum of the maximums, widened so percentage columns can
    // have their percentage of it; with 100% taken by percentages and other columns wanting width,
    // the table is as wide as it may be.
    private static double MaxContent(Columns columns)
    {
        double sum = 0, otherSum = 0, percentEstimate = 0, remaining = 100;
        for (var i = 0; i < columns.Count; i++)
        {
            sum += columns.Maximums[i];
            if (columns.Percents[i] > 0)
            {
                if (columns.Maximums[i] > 0) percentEstimate = Math.Max(percentEstimate, columns.Maximums[i] * 100 / columns.Percents[i]);
                remaining -= columns.Percents[i];
            }
            else otherSum += columns.Maximums[i];
        }
        var otherEstimate = remaining > 0 ? otherSum * 100 / remaining : otherSum > 0 ? double.PositiveInfinity : 0;
        return Math.Max(sum, Math.Max(percentEstimate, otherEstimate));
    }

    // Raises the minimums (or, with raiseMaximums, the maximums) of the columns a spanning cell
    // covers to the share each gets of the cell's width.
    private static void Widen(Columns covered, Span<double> shares, double width, bool raiseMaximums)
    {
        Distribute(shares, covered, width, constrainedTarget: false);
        for (var i = 0; i < covered.Count; i++)
        {
            if (raiseMaximums) covered.Maximums[i] = Math.Max(covered.Maximums[i], shares[i]);
            else
            {
                covered.Minimums[i] = Math.Max(covered.Minimums[i], shares[i]);
                covered.Maximums[i] = Math.Max(covered.Maximums[i], covered.Minimums[i]);
            }
        }
    }

    private static double PercentWidth(Columns columns, int i, double target)
        => Math.Max(columns.Minimums[i], target * columns.Percents[i] / 100);

    // Shares target out over the columns into result. Past every maximum, a constrained target (the
    // table width) grows constrained columns before percentage columns; an unconstrained one (a
    // spanning cell without a width) grows percentage columns first, as Chrome does.
    private static void Distribute(Span<double> result, Columns columns, double target, bool constrainedTarget)
    {
        double minGuess = 0, percentGuess = 0, specifiedGuess = 0, maxGuess = 0;
        double percentGrowth = 0, constrainedGrowth = 0, autoGrowth = 0, autoMaximums = 0, constrainedMaximums = 0, totalPercent = 0;
        int percentCount = 0, constrainedCount = 0, autoCount = 0;
        for (var i = 0; i < columns.Count; i++)
        {
            var min = columns.Minimums[i];
            var max = columns.Maximums[i];
            minGuess += min;
            switch (columns.KindOf(i))
            {
                case Kind.Percent:
                    var percent = PercentWidth(columns, i, target);
                    percentGuess += percent; specifiedGuess += percent; maxGuess += percent;
                    percentGrowth += percent - min; totalPercent += columns.Percents[i]; percentCount++;
                    break;
                case Kind.Constrained:
                    percentGuess += min; specifiedGuess += max; maxGuess += max;
                    constrainedGrowth += max - min; constrainedMaximums += max; constrainedCount++;
                    break;
                default:
                    percentGuess += min; specifiedGuess += min; maxGuess += max;
                    autoGrowth += max - min; autoMaximums += max; autoCount++;
                    break;
            }
        }
        target = Math.Max(target, minGuess);
        // Each column's width in the guess below the target, and which kind grows past it.
        Kind? growing;
        double baseline, growth;
        if (target <= minGuess) { growing = null; baseline = minGuess; growth = 0; }
        else if (target <= percentGuess) { growing = Kind.Percent; baseline = minGuess; growth = percentGrowth; }
        else if (target <= specifiedGuess) { growing = Kind.Constrained; baseline = percentGuess; growth = constrainedGrowth; }
        else if (target <= maxGuess) { growing = Kind.Auto; baseline = specifiedGuess; growth = autoGrowth; }
        else { growing = null; baseline = maxGuess; growth = 0; }
        var aboveMax = target > maxGuess;
        // Past the last guess: auto columns grow by their maximums, else constrained columns,
        // else percentage columns by their percentages (percentage columns first for a spanning cell).
        Kind? extraKind = !aboveMax ? null : autoCount > 0 ? Kind.Auto
            : constrainedCount > 0 && (constrainedTarget || percentCount == 0) ? Kind.Constrained
            : percentCount > 0 ? Kind.Percent : null;
        var exact = growing == Kind.Auto && target == maxGuess;
        var extra = target - baseline;
        var last = -1;
        for (var i = 0; i < columns.Count; i++)
        {
            var kind = columns.KindOf(i);
            var min = columns.Minimums[i];
            var max = columns.Maximums[i];
            // The column's size at the guess the target passed.
            var size = target <= percentGuess ? min
                : target <= specifiedGuess ? kind == Kind.Percent ? PercentWidth(columns, i, target) : min
                : target <= maxGuess ? kind == Kind.Percent ? PercentWidth(columns, i, target) : kind == Kind.Constrained ? max : min
                : kind == Kind.Percent ? PercentWidth(columns, i, target) : max;
            if (exact && kind == Kind.Auto) size = max;
            else if (growing is { } step && kind == step)
            {
                var full = step switch { Kind.Percent => PercentWidth(columns, i, target), _ => max };
                var count = step switch { Kind.Percent => percentCount, Kind.Constrained => constrainedCount, _ => autoCount };
                var delta = growth > 0 ? extra * (full - min) / growth : extra / count;
                size = min + delta;
                last = i;
            }
            else if (extraKind is { } grow && kind == grow)
            {
                var (weight, total, count) = grow switch
                {
                    Kind.Auto => (max, autoMaximums, autoCount),
                    Kind.Constrained => (max, constrainedMaximums, constrainedCount),
                    _ => (columns.Percents[i], totalPercent, percentCount),
                };
                var delta = total > 0 ? extra * weight / total : extra / count;
                size += delta;
                last = i;
            }
            result[i] = size;
        }
        // The last column that grew takes the rounding remainder, so the columns sum to the target.
        if (last < 0) return;
        var others = 0d;
        for (var i = 0; i < columns.Count; i++)
            if (i != last) others += result[i];
        result[last] = Math.Max(columns.Minimums[last], target - others);
    }

    private static double Edges(BoxStyle box)
        => box.PaddingLeft.Value + box.BorderLeft.EffectiveWidth + box.PaddingRight.Value + box.BorderRight.EffectiveWidth;

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
            if (content is TableCellBlock block)
            {
                // A list's contribution includes its marker gutter, margins and padding; its items
                // (flex rows too) are sized by the flex and block intrinsic sizes.
                var (blockMin, blockMax) = FlexLayout.Intrinsic.Of(block.Node, options, resolved, cancellation);
                if (double.IsNaN(blockMax))
                    throw Geometry("A list in a table cell cannot be sized from its content.", block.Node.SourceOffset);
                min = Math.Max(min, blockMin);
                max = Math.Max(max, blockMax);
                continue;
            }
            var paragraph = ((TableCellText)content).Paragraph;
            var paragraphPadding = ((TableCellText)content).Padding is { } padded
                ? padded.PaddingLeft.Value + padded.PaddingRight.Value : 0;
            if (paragraph.Style.FirstLineIndent.IsPercent && paragraph.Style.FirstLineIndent.Value != 0)
                throw Geometry("Percentage text-indent is not supported in automatic table layout; give every column an explicit col width.", paragraph.SourceOffset);
            try
            {
                var (paragraphMin, paragraphMax) = TextLines.IntrinsicWidths(paragraph, options, resolved, cancellation);
                min = Math.Max(min, paragraphMin + paragraphPadding);
                max = Math.Max(max, paragraphMax + paragraphPadding);
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
