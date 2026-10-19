namespace FactsPDF;

/// <summary>Choose a whole-row fragment, never a header without a body row.</summary>
internal static class TablePagination
{
    internal readonly record struct Fragment(int EndRow, double Height);

    internal static Fragment Fit(MeasuredTable table, int startRow, double available,
        bool first, CancellationToken cancellation)
    {
        var height = (first ? table.Top : 0) + (table.Rows[0].IsHeader ? table.Rows[0].Height : 0);
        var end = startRow;
        while (end < table.Rows.Count)
        {
            cancellation.ThrowIfCancellationRequested();
            // Leading zero-height rows travel with the next occupied row;
            // trailing ones travel with the preceding row. Neither can create
            // an orphan header or detach bottom decoration from the last row.
            var groupEnd = end;
            var next = height;
            do
            {
                cancellation.ThrowIfCancellationRequested();
                next += table.Rows[groupEnd++].Height;
            } while (groupEnd < table.Rows.Count && table.Rows[groupEnd - 1].Height == 0);
            while (groupEnd < table.Rows.Count && table.Rows[groupEnd].Height == 0)
            {
                cancellation.ThrowIfCancellationRequested();
                groupEnd++;
            }
            var trailing = groupEnd == table.Rows.Count ? table.Bottom : 0;
            if (next + trailing > available + 0.000001) break;
            height = next;
            end = groupEnd;
        }
        if (end == table.Rows.Count) height += table.Bottom;
        return new(end, height);
    }
}
