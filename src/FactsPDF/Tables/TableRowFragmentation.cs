namespace FactsPDF;

internal enum TableCellUnitKind { TextLine, Image }
// A value, kept inline in its cell's exact-size array: one cell line is one unit,
// so a per-unit object and a growable list per cell dominated table measurement.
// A text line's inline images are ImageIndex onwards, ImageBoxCount of them. A line has no image
// decoration boxes, so its ImageBoxStart holds its paragraph's orphans and widows instead (15 bits
// each) and whether it starts that paragraph, which keeps the unit 56 bytes.
internal readonly record struct MeasuredCellUnit(TableCellUnitKind Kind, int SourceOffset, double Top,
    double Height, double LeadingGap, bool IsInitialGap, int RunStart, int RunCount,
    int ImageIndex, int ImageBoxStart, int ImageBoxCount)
{
    private const int ParagraphStartBit = 1 << 30;

    internal static MeasuredCellUnit Line(int sourceOffset, double top, double height, double leadingGap,
        bool isInitialGap, int runStart, int runCount, int imageStart, int imageCount, int lineControl)
        => new(TableCellUnitKind.TextLine, sourceOffset, top, height, leadingGap, isInitialGap, runStart, runCount,
            imageCount == 0 ? -1 : imageStart, lineControl, imageCount);

    internal int LineImageStart => Kind == TableCellUnitKind.TextLine ? ImageIndex : -1;
    internal int LineImageCount => Kind == TableCellUnitKind.TextLine ? ImageBoxCount : 0;
    internal bool StartsParagraph => Kind == TableCellUnitKind.TextLine && (ImageBoxStart & ParagraphStartBit) != 0;
    internal (int Orphans, int Widows) LineControl => (ImageBoxStart & 0x7FFF, (ImageBoxStart >> 15) & 0x7FFF);

    internal static int TextLinePayload(TextStyle style, bool startsParagraph)
    {
        var (orphans, widows) = FactsPDF.LineBreakControl.Of(style);
        return orphans | widows << 15 | (startsParagraph ? ParagraphStartBit : 0);
    }
}
internal sealed record MeasuredTableCell(double X, double Width, double ContentX, double ContentWidth,
    double TopEdge, double BottomEdge, double InitialGap, double TerminalGap,
    BoxStyle BoxStyle, Rgb TextColor, int SourceOffset, MeasuredCellUnit[] Units);
internal readonly record struct TableFreshSpace(double UsableHeight, double FirstLeading);
internal sealed record RowContinuation(int[] NextUnits, bool IsFirstFragment);
internal readonly record struct CellUnitSlice(int CellIndex, int StartUnit, int EndUnit,
    double ContentTop, double OccupiedEnd);
internal sealed record RowFragment(CellUnitSlice[] Cells, double Height, bool IsFirst,
    bool IsLast, int AdvancedUnits);
internal enum RowFitStatus { Progress, NoProgress }
// BreaksOrphans: a cell's paragraph starts this fragment and cannot keep its orphans in it, so
// Chrome would rather break before the row.
internal readonly record struct RowFitResult(RowFitStatus Status, RowFragment? Fragment, bool BreaksOrphans = false);
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
            var first = cell.TopEdge + cell.InitialGap + (cell.Units.Length == 0 ? 0 : cell.Units[0].Height);
            if (first > firstCapacity + Epsilon)
                throw Geometry(cell.Units.Length == 0 ? cell.SourceOffset : cell.Units[0].SourceOffset);
            if (cell.TerminalGap + cell.BottomEdge + terminalTableBottom > terminalCapacity + Epsilon)
                throw Geometry(cell.SourceOffset);
            foreach (var unit in cell.Units)
            {
                cancellation.ThrowIfCancellationRequested();
                if (unit.Height > terminalCapacity + Epsilon) throw Geometry(unit.SourceOffset);
            }
        }
        if (!row.Cells.Any(cell => cell.Units.Length > 0)) throw Geometry(row.SourceOffset);
    }

    internal static RowFitResult Fit(MeasuredTableRow row, RowContinuation cursor,
        double availableBodyHeight, double terminalTableBottom, bool freshPage,
        CancellationToken cancellation, double? freshTerminalCapacity = null)
    {
        cancellation.ThrowIfCancellationRequested();
        var slices = new CellUnitSlice[row.Cells.Length];
        var advanced = 0;
        var complete = true;
        var height = 0d;
        var breaksOrphans = false;
        for (var i = 0; i < slices.Length; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            var cell = row.Cells[i];
            var start = cursor.NextUnits[i];
            var end = start;
            var occupied = cursor.IsFirstFragment ? cell.TopEdge : 0;
            if (occupied > availableBodyHeight + Epsilon) return NoProgress();
            var contentTop = occupied;
            while (end < cell.Units.Length)
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
            var controlled = ControlledEnd(cell.Units, start, end, ref breaksOrphans);
            if (controlled != end)
            {
                end = controlled;
                occupied = end > start
                    ? contentTop + cell.Units[end - 1].Top + cell.Units[end - 1].Height - cell.Units[start].Top
                    : cursor.IsFirstFragment ? cell.TopEdge : 0;
            }
            advanced += end - start;
            complete &= end == cell.Units.Length;
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
                return new(RowFitStatus.Progress, new(slices, terminalHeight, cursor.IsFirstFragment, true, advanced), breaksOrphans);

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
        return new(RowFitStatus.Progress, new(slices, height, cursor.IsFirstFragment, false, advanced), breaksOrphans);

        // A cell paragraph broken at end keeps its orphans and widows as an ordinary paragraph does
        // (LineBreakControl): it breaks before the paragraph when earlier units of this cell are
        // in the fragment, otherwise breaksOrphans asks for a break before the row.
        static int ControlledEnd(MeasuredCellUnit[] units, int start, int end, ref bool breaksOrphans)
        {
            if (end >= units.Length || units[end].Kind != TableCellUnitKind.TextLine || units[end].StartsParagraph)
                return end;
            var first = end;
            while (first > 0 && !units[first].StartsParagraph) first--;
            var last = end + 1;
            while (last < units.Length && units[last].Kind == TableCellUnitKind.TextLine && !units[last].StartsParagraph)
                last++;
            var (orphans, widows) = units[end].LineControl;
            var from = Math.Max(first, start);
            if (end - from < orphans)
            {
                if (first > start) return first;
                breaksOrphans = true;
                return end;
            }
            return Math.Min(end, Math.Max(from + orphans, last - widows));
        }

        RowFitResult NoProgress()
        {
            if (freshPage)
            {
                var blocked = row.Cells.Select((cell, i) => cursor.NextUnits[i] < cell.Units.Length
                    ? cell.Units[cursor.NextUnits[i]].SourceOffset : -1).FirstOrDefault(offset => offset >= 0, row.SourceOffset);
                throw Geometry(blocked);
            }
            return new(RowFitStatus.NoProgress, null);
        }
    }
}
