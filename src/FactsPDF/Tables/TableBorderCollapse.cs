namespace FactsPDF;

/// <summary>One resolved border segment of a collapsed-border table; a zero width paints nothing.</summary>
internal readonly record struct CollapsedEdge(double Width, BorderPattern Pattern, Rgb Color, bool Transparent)
{
    internal bool Paints => Width > 0 && !Transparent;
}

/// <summary>
/// The resolved borders of a <c>border-collapse:collapse</c> table: one segment per column on each
/// of the rows + 1 horizontal grid lines and one per row on each of the columns + 1 vertical lines.
/// A vertical line inside a spanning cell has no segment.
/// </summary>
internal sealed class TableBorderGrid
{
    private readonly CollapsedEdge[] horizontal;
    private readonly CollapsedEdge[] vertical;

    internal TableBorderGrid(int rows, int columns, BoxStyle tableBox)
    {
        Rows = rows;
        Columns = columns;
        TableBox = tableBox;
        horizontal = new CollapsedEdge[(rows + 1) * columns];
        vertical = new CollapsedEdge[rows * (columns + 1)];
    }

    internal int Rows { get; }
    internal int Columns { get; }
    internal BoxStyle TableBox { get; }

    /// <summary>The segment of horizontal grid line <paramref name="line"/> (0 = table top) over <paramref name="column"/>.</summary>
    internal ref CollapsedEdge Horizontal(int line, int column) => ref horizontal[line * Columns + column];

    /// <summary>The segment of vertical grid line <paramref name="line"/> (0 = table left) beside <paramref name="row"/>.</summary>
    internal ref CollapsedEdge Vertical(int row, int line) => ref vertical[row * (Columns + 1) + line];
}

/// <summary>
/// The CSS 2.1 collapsing border model (§17.6.2) as Chrome implements it, for cells, rows, row
/// groups and the table (column and column group borders are not accepted).
/// </summary>
/// <remarks>
/// <para>
/// Every grid edge takes the winning border of the boxes that share it: <c>hidden</c> suppresses
/// every other border; <c>none</c> loses to everything; otherwise the wider border wins, then the
/// style (solid, dashed, dotted); a remaining tie goes to the cell over the row, the row group and
/// the table, and between two cells (or rows, or groups) to the one further left or further up.
/// </para>
/// <para>
/// Each cell lays out with half of each winning edge as its border (the widest segment along a
/// spanning cell's edge), and the table with no padding and half the widest outer edge on each
/// side, exactly as Chrome sizes collapsed tables. Those layout borders paint nothing: the winning
/// segments are painted centred on the grid lines by <see cref="TableLayout"/>.
/// </para>
/// </remarks>
internal static class TableBorderCollapse
{
    private struct Candidate
    {
        internal bool Present, Hidden, Transparent;
        internal double Width;
        internal BorderPattern Pattern;
        internal Rgb Color;
    }

    /// <summary>Resolves the table's borders, sets <see cref="TableNode.Collapsed"/> and gives every
    /// cell its half-width layout borders.</summary>
    internal static void Apply(TableNode table)
    {
        var rows = table.Rows;
        var rowCount = rows.Count;
        var columns = table.Columns.Count;
        var horizontal = new Candidate[(rowCount + 1) * columns];
        var vertical = new Candidate[rowCount * (columns + 1)];
        int H(int line, int column) => line * columns + column;
        int V(int row, int line) => row * (columns + 1) + line;

        // Cells first, in source order: a tie keeps the earlier (further up or left) border.
        for (var r = 0; r < rowCount; r++)
        {
            var column = 0;
            foreach (var cell in rows[r].Cells)
            {
                var box = cell.BoxStyle;
                var color = cell.Style.Color;
                for (var c = column; c < column + cell.ColumnSpan; c++)
                {
                    Offer(horizontal, H(r, c), box.BorderTop, color);
                    Offer(horizontal, H(r + 1, c), box.BorderBottom, color);
                }
                Offer(vertical, V(r, column), box.BorderLeft, color);
                Offer(vertical, V(r, column + cell.ColumnSpan), box.BorderRight, color);
                column += cell.ColumnSpan;
            }
        }
        for (var r = 0; r < rowCount; r++)
        {
            var row = rows[r];
            for (var c = 0; c < columns; c++)
            {
                Offer(horizontal, H(r, c), row.Box.BorderTop, row.Color);
                Offer(horizontal, H(r + 1, c), row.Box.BorderBottom, row.Color);
            }
            Offer(vertical, V(r, 0), row.Box.BorderLeft, row.Color);
            Offer(vertical, V(r, columns), row.Box.BorderRight, row.Color);
        }
        for (var r = 0; r < rowCount; r++)
        {
            var row = rows[r];
            var first = r == 0 || rows[r - 1].Group != row.Group;
            var last = r == rowCount - 1 || rows[r + 1].Group != row.Group;
            for (var c = 0; c < columns; c++)
            {
                if (first) Offer(horizontal, H(r, c), row.GroupBox.BorderTop, row.GroupColor);
                if (last) Offer(horizontal, H(r + 1, c), row.GroupBox.BorderBottom, row.GroupColor);
            }
            Offer(vertical, V(r, 0), row.GroupBox.BorderLeft, row.GroupColor);
            Offer(vertical, V(r, columns), row.GroupBox.BorderRight, row.GroupColor);
        }
        var tableBox = table.BoxStyle;
        var tableColor = table.Style.Color;
        for (var c = 0; c < columns; c++)
        {
            Offer(horizontal, H(0, c), tableBox.BorderTop, tableColor);
            Offer(horizontal, H(rowCount, c), tableBox.BorderBottom, tableColor);
        }
        for (var r = 0; r < rowCount; r++)
        {
            Offer(vertical, V(r, 0), tableBox.BorderLeft, tableColor);
            Offer(vertical, V(r, columns), tableBox.BorderRight, tableColor);
        }

        // The table's layout border is half the widest outer segment on each side.
        double top = 0, right = 0, bottom = 0, left = 0;
        for (var c = 0; c < columns; c++)
        {
            top = Math.Max(top, Width(horizontal[H(0, c)]));
            bottom = Math.Max(bottom, Width(horizontal[H(rowCount, c)]));
        }
        for (var r = 0; r < rowCount; r++)
        {
            left = Math.Max(left, Width(vertical[V(r, 0)]));
            right = Math.Max(right, Width(vertical[V(r, columns)]));
        }
        var layoutBox = tableBox with
        {
            PaddingTop = default, PaddingRight = default, PaddingBottom = default, PaddingLeft = default,
            BorderTop = Layout(top / 2), BorderRight = Layout(right / 2),
            BorderBottom = Layout(bottom / 2), BorderLeft = Layout(left / 2)
        };
        var grid = new TableBorderGrid(rowCount, columns, layoutBox);
        for (var line = 0; line <= rowCount; line++)
            for (var c = 0; c < columns; c++)
                grid.Horizontal(line, c) = Resolved(horizontal[H(line, c)]);
        for (var r = 0; r < rowCount; r++)
            for (var line = 0; line <= columns; line++)
                grid.Vertical(r, line) = Resolved(vertical[V(r, line)]);

        for (var r = 0; r < rowCount; r++)
        {
            var cells = rows[r].Cells;
            var column = 0;
            for (var i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                double cellTop = 0, cellBottom = 0;
                for (var c = column; c < column + cell.ColumnSpan; c++)
                {
                    cellTop = Math.Max(cellTop, grid.Horizontal(r, c).Width);
                    cellBottom = Math.Max(cellBottom, grid.Horizontal(r + 1, c).Width);
                }
                cells[i] = cell with
                {
                    BoxStyle = cell.BoxStyle with
                    {
                        BorderTop = Layout(cellTop / 2),
                        BorderBottom = Layout(cellBottom / 2),
                        BorderLeft = Layout(grid.Vertical(r, column).Width / 2),
                        BorderRight = Layout(grid.Vertical(r, column + cell.ColumnSpan).Width / 2)
                    }
                };
                column += cell.ColumnSpan;
            }
        }
        table.Collapsed = grid;
    }

    // A layout-only border: it takes its width in layout and paints nothing.
    private static BorderEdge Layout(double width) => new(width, width > 0, null) { Transparent = true };

    private static double Width(Candidate candidate) => candidate.Present && !candidate.Hidden ? candidate.Width : 0;

    private static CollapsedEdge Resolved(Candidate candidate) => candidate.Present && !candidate.Hidden
        ? new(candidate.Width, candidate.Pattern, candidate.Color, candidate.Transparent)
        : default;

    private static int Rank(BorderPattern pattern) => pattern switch
    {
        BorderPattern.Solid => 3,
        BorderPattern.Dashed => 2,
        _ => 1
    };

    private static void Offer(Candidate[] slots, int index, BorderEdge edge, Rgb owner)
    {
        Candidate next;
        if (edge.Hidden) next = new() { Present = true, Hidden = true };
        // none (and a zero width) has the lowest priority: it never replaces another border.
        else if (!edge.Solid || edge.Width <= 0) return;
        else next = new()
        {
            Present = true, Width = edge.Width, Pattern = edge.Pattern,
            Color = edge.Color ?? owner, Transparent = edge.Transparent
        };
        ref var current = ref slots[index];
        if (!current.Present || next.Hidden && !current.Hidden) { current = next; return; }
        if (current.Hidden) return;
        if (next.Width > current.Width || next.Width == current.Width && Rank(next.Pattern) > Rank(current.Pattern))
            current = next;
    }
}
