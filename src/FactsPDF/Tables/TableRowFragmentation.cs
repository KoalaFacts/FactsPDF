namespace FactsPDF;

internal enum TableCellUnitKind { TextLine, Image }
internal sealed record MeasuredCellUnit(TableCellUnitKind Kind, int SourceOffset, double Top,
    double Height, double LeadingGap, bool IsInitialGap, int RunStart, int RunCount,
    int ImageIndex, int ImageBoxStart, int ImageBoxCount);
internal sealed record MeasuredTableCell(double X, double Width, double ContentX, double ContentWidth,
    double TopEdge, double BottomEdge, double InitialGap, double TerminalGap,
    BoxStyle BoxStyle, Rgb TextColor, int SourceOffset, List<MeasuredCellUnit> Units);
internal readonly record struct TableFreshSpace(double UsableHeight, double FirstLeading);
internal sealed record RowContinuation(int[] NextUnits, bool IsFirstFragment);
internal readonly record struct CellUnitSlice(int CellIndex, int StartUnit, int EndUnit,
    double ContentTop, double OccupiedEnd);
internal sealed record RowFragment(CellUnitSlice[] Cells, double Height, bool IsFirst,
    bool IsLast, int AdvancedUnits);
internal enum RowFitStatus { Progress, NoProgress }
internal readonly record struct RowFitResult(RowFitStatus Status, RowFragment? Fragment);
internal sealed record TableCursor(int RowIndex, RowContinuation? Continuation = null);
internal readonly record struct TableFragmentPart(int RowIndex, RowFragment? Slice);
internal sealed record TableFragmentPlan(List<TableFragmentPart> Parts, double Height, TableCursor Next);

/// <summary>Pure per-cell prefix selection. A candidate never consumes the caller's cursors.</summary>
internal static class TableRowFragmentation
{
    private const double Epsilon = .000001;
    private static FactsPdfException Geometry(int offset) => new("FPDF1302",
        "Table cell unit and required decoration cannot fit on a page.", offset);

    internal static void Validate(MeasuredTableRow row, double firstCapacity,
        double terminalCapacity, double terminalTableBottom, CancellationToken cancellation)
    {
        foreach (var cell in row.Cells)
        {
            cancellation.ThrowIfCancellationRequested();
            var first = cell.TopEdge + cell.InitialGap + (cell.Units.Count == 0 ? 0 : cell.Units[0].Height);
            if (first > firstCapacity + Epsilon)
                throw Geometry(cell.Units.Count == 0 ? cell.SourceOffset : cell.Units[0].SourceOffset);
            if (cell.TerminalGap + cell.BottomEdge + terminalTableBottom > terminalCapacity + Epsilon)
                throw Geometry(cell.SourceOffset);
            foreach (var unit in cell.Units)
            {
                cancellation.ThrowIfCancellationRequested();
                if (unit.Height > terminalCapacity + Epsilon) throw Geometry(unit.SourceOffset);
            }
        }
        if (!row.Cells.Any(cell => cell.Units.Count > 0)) throw Geometry(row.SourceOffset);
    }

    internal static RowFitResult Fit(MeasuredTableRow row, RowContinuation cursor,
        double availableBodyHeight, double terminalTableBottom, bool freshPage,
        CancellationToken cancellation, double? freshTerminalCapacity = null)
    {
        cancellation.ThrowIfCancellationRequested();
        var slices = new CellUnitSlice[row.Cells.Count];
        var advanced = 0;
        var complete = true;
        var height = 0d;
        for (var i = 0; i < slices.Length; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            var cell = row.Cells[i];
            var start = cursor.NextUnits[i];
            var end = start;
            var occupied = cursor.IsFirstFragment ? cell.TopEdge : 0;
            if (occupied > availableBodyHeight + Epsilon) return NoProgress();
            var contentTop = occupied;
            while (end < cell.Units.Count)
            {
                cancellation.ThrowIfCancellationRequested();
                var unit = cell.Units[end];
                // An original gap belongs to its cell's first unit, independent
                // of whether another cell has already advanced this row.
                var gap = end == start && !cursor.IsFirstFragment && !unit.IsInitialGap ? 0 : unit.LeadingGap;
                var next = occupied + gap + unit.Height;
                if (next > availableBodyHeight + Epsilon) break;
                if (end == start) contentTop = occupied + gap;
                occupied = next;
                end++;
            }
            advanced += end - start;
            complete &= end == cell.Units.Count;
            height = Math.Max(height, occupied);
            slices[i] = new(i, start, end, contentTop, occupied);
        }
        if (advanced == 0) return NoProgress();
        if (complete)
        {
            var terminalHeight = 0d;
            for (var i = 0; i < slices.Length; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                terminalHeight = Math.Max(terminalHeight,
                    slices[i].OccupiedEnd + row.Cells[i].TerminalGap + row.Cells[i].BottomEdge);
            }
            if (terminalHeight + terminalTableBottom <= availableBodyHeight + Epsilon)
                return new(RowFitStatus.Progress, new(slices, terminalHeight, cursor.IsFirstFragment, true, advanced));

            // Reserve one actual final unit, choosing the greatest current end
            // and lowest column among at most sixteen fresh-terminal checks.
            var chosen = -1;
            var greatestEnd = double.NegativeInfinity;
            var freshCapacity = freshTerminalCapacity ?? availableBodyHeight;
            for (var i = 0; i < slices.Length; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                var slice = slices[i];
                if (slice.EndUnit == slice.StartUnit) continue;
                var cell = row.Cells[i];
                var unit = cell.Units[slice.EndUnit - 1];
                var reservedHeight = unit.Height + (unit.IsInitialGap ? unit.LeadingGap : 0);
                var terminal = 0d;
                for (var j = 0; j < slices.Length; j++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    terminal = Math.Max(terminal, (i == j ? reservedHeight : 0)
                        + row.Cells[j].TerminalGap + row.Cells[j].BottomEdge);
                }
                if (terminal + terminalTableBottom <= freshCapacity + Epsilon && slice.OccupiedEnd > greatestEnd)
                { chosen = i; greatestEnd = slice.OccupiedEnd; }
            }
            if (chosen < 0) throw Geometry(row.SourceOffset);
            var selected = slices[chosen];
            var selectedCell = row.Cells[chosen];
            var newEnd = selected.EndUnit - 1;
            var newOccupied = cursor.IsFirstFragment ? selectedCell.TopEdge : 0;
            if (newEnd > selected.StartUnit)
            {
                var firstUnit = selectedCell.Units[selected.StartUnit];
                var lastUnit = selectedCell.Units[newEnd - 1];
                newOccupied = selected.ContentTop + lastUnit.Top + lastUnit.Height - firstUnit.Top;
            }
            slices[chosen] = selected with { EndUnit = newEnd, OccupiedEnd = newOccupied };
            advanced--;
            if (advanced == 0) return NoProgress();
            height = slices.Max(slice => slice.OccupiedEnd);
        }
        return new(RowFitStatus.Progress, new(slices, height, cursor.IsFirstFragment, false, advanced));

        RowFitResult NoProgress()
        {
            if (freshPage)
            {
                var blocked = row.Cells.Select((cell, i) => cursor.NextUnits[i] < cell.Units.Count
                    ? cell.Units[cursor.NextUnits[i]].SourceOffset : -1).FirstOrDefault(offset => offset >= 0, row.SourceOffset);
                throw Geometry(blocked);
            }
            return new(RowFitStatus.NoProgress, null);
        }
    }
}
