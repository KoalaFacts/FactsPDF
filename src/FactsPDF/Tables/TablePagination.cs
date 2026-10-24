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

    internal static TableFragmentPlan FitWithContinuations(MeasuredTable table, TableCursor cursor,
        double available, TableFreshSpace freshSpace, bool first, CancellationToken cancellation)
    {
        var header = table.Rows[0].IsHeader ? table.Rows[0].Height : 0;
        var height = header + (first ? table.Top : 0);
        var parts = new List<TableFragmentPart>();
        var nextCursor = cursor;
        var index = cursor.RowIndex;
        while (index < table.Rows.Count)
        {
            cancellation.ThrowIfCancellationRequested();
            var occupied = index;
            while (occupied + 1 < table.Rows.Count && table.Rows[occupied].Height == 0)
            {
                cancellation.ThrowIfCancellationRequested();
                occupied++;
            }
            var groupEnd = occupied + 1;
            while (groupEnd < table.Rows.Count && table.Rows[groupEnd].Height == 0)
            {
                cancellation.ThrowIfCancellationRequested();
                groupEnd++;
            }
            var terminal = groupEnd == table.Rows.Count ? table.Bottom : 0;
            var row = table.Rows[occupied];
            if (!row.CanSplit)
            {
                if (height + row.Height + terminal > available + .000001) break;
                for (var i = index; i < groupEnd; i++) parts.Add(new(i, null));
                height += row.Height + terminal;
                index = groupEnd;
                nextCursor = new(index);
                continue;
            }
            var continuation = nextCursor.Continuation ?? new(new int[row.Cells.Count], true);
            // Logical group identity survives positive heights that round away
            // when accumulated into table-local coordinates.
            var firstCapacity = freshSpace.UsableHeight - header
                - (row.IsFirstBodyGroup ? table.Top + freshSpace.FirstLeading : 0);
            if (continuation.IsFirstFragment)
                TableRowFragmentation.Validate(row, firstCapacity, freshSpace.UsableHeight - header, terminal, cancellation);
            var result = TableRowFragmentation.Fit(row, continuation, available - height,
                terminal, false, cancellation, freshSpace.UsableHeight - header);
            if (result.Status == RowFitStatus.NoProgress) break;
            var slice = result.Fragment!;
            for (var i = index; i < occupied; i++) parts.Add(new(i, null));
            parts.Add(new(occupied, slice));
            height += slice.Height;
            if (!slice.IsLast)
            {
                nextCursor = new(occupied, new(slice.Cells.Select(cell => cell.EndUnit).ToArray(), false));
                break;
            }
            for (var i = occupied + 1; i < groupEnd; i++) parts.Add(new(i, null));
            height += terminal;
            index = groupEnd;
            nextCursor = new(index);
        }
        return new(parts, height, nextCursor);
    }
}
