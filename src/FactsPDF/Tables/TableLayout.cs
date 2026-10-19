using static FactsPDF.TextLines;

namespace FactsPDF;

internal sealed record MeasuredTable(double Width, double Height,
    List<PlacedText> Runs, List<PaintedBox> PaintBoxes,
    List<MeasuredTableRow> Rows, double Top, double Bottom);

internal sealed record MeasuredTableRow(double Top, double Height, bool IsHeader, int SourceOffset,
    int RunStart, int RunCount, int BoxStart, int BoxCount);

/// <summary>Measure the table once, retaining only charged output commands.</summary>
internal static class TableLayout
{
    private readonly record struct Edges(double Left, double Right, double Top, double Bottom);

    internal static MeasuredTable Measure(LayoutTable item, PdfOptions options,
        Dictionary<(FontRole Role, int Scalar), (PdfFont? Font, double Width)> resolved,
        CancellationToken cancellation, Action<int, int> charge, Action<int, int> ensure)
    {
        var table = item.Node;
        var style = table.BoxStyle;
        var edges = ResolveEdges(style, item.AvailableWidth, table.SourceOffset);
        // As for existing blocks, auto fills the parent's content area with
        // the outer box; an explicit width describes the table's content.
        var width = style.Width is { } specified
            ? Checked(specified.Resolve(item.AvailableWidth), table.SourceOffset)
            : item.AvailableWidth - edges.Left - edges.Right;
        var outerWidth = width + edges.Left + edges.Right;
        if (width <= 0 || outerWidth > item.AvailableWidth + 0.000001)
            throw Geometry("Table outer width exceeds containing content width or is zero.", table.SourceOffset);
        var tracks = new double[table.Columns.Count];
        var totalWidth = 0d;
        for (var i = 0; i < tracks.Length; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            var column = table.Columns[i];
            if (column.Width is not { } length)
                throw Geometry("Every column requires an explicit positive width.", column.SourceOffset);
            tracks[i] = Checked(length.Resolve(width), column.SourceOffset);
            if (tracks[i] <= 0)
                throw Geometry("Column width must be positive.", column.SourceOffset);
            totalWidth += tracks[i];
        }
        // Allow only floating-point roundoff at the inclusive decimal boundary.
        // One ULP per track plus the table width covers resolution/summation;
        // the actual tracks remain unchanged, with no width redistribution.
        var scale = Math.Max(totalWidth, width);
        var roundoff = (Math.BitIncrement(scale) - scale) * (tracks.Length + 1);
        if (Math.Abs(totalWidth - width) > 0.001 + roundoff)
            throw Geometry("Column widths must sum to the table content width within 0.001pt.", table.SourceOffset);

        var runs = new List<PlacedText>();
        var boxes = new List<PaintedBox>();
        var rows = new List<MeasuredTableRow>();
        var top = edges.Top;
        var usableHeight = options.PageHeight - 2 * options.Margin;
        void CheckHeight(double height, int offset)
        {
            if (!double.IsFinite(height) || height > usableHeight + 0.000001)
                throw Geometry("A table row exceeds the usable page height.", offset);
        }
        // First and last decoration may belong to different pages.
        CheckHeight(edges.Top, table.SourceOffset);
        CheckHeight(edges.Bottom, table.SourceOffset);
        foreach (var row in table.Rows)
        {
            cancellation.ThrowIfCancellationRequested();
            var runStart = runs.Count;
            var boxStart = boxes.Count;
            var rowHeight = 0d;
            var x = edges.Left;
            for (var i = 0; i < row.Cells.Count; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                var cell = row.Cells[i];
                var cellEdges = ResolveEdges(cell.BoxStyle, tracks[i], cell.SourceOffset);
                var contentWidth = tracks[i] - cellEdges.Left - cellEdges.Right;
                if (contentWidth <= 0)
                    throw Geometry("Cell content width must remain positive after padding and borders.", cell.SourceOffset);
                var height = cellEdges.Top;
                CheckHeight(height + cellEdges.Bottom, cell.SourceOffset);
                // A spent allowance must fail before allocating another cell's
                // glyph/word buffers, just as it does for ordinary paragraphs.
                if (HasTextContent(cell.Paragraph, cancellation)) ensure(1, cell.SourceOffset);
                try
                {
                    foreach (var line in Wrap(cell.Paragraph, contentWidth, options, resolved, cancellation))
                    {
                        cancellation.ThrowIfCancellationRequested();
                        CheckHeight(height + line.Height + cellEdges.Bottom, cell.SourceOffset);
                        var alignedX = x + cellEdges.Left + (cell.Paragraph.Style.Alignment switch
                        {
                            TextAlignment.Center => (contentWidth - line.Width) / 2,
                            TextAlignment.Right => contentWidth - line.Width,
                            _ => 0
                        });
                        AppendLine(line, alignedX, -top - height - line.Ascent,
                            runs, charge, cancellation, cell.SourceOffset);
                        height += line.Height;
                    }
                }
                catch (FactsPdfException error) when (error.SourceOffset < 0)
                {
                    // Preserve the diagnostic and its text while adding the
                    // cell location when the shared wrapper has no finer one.
                    throw new FactsPdfException(error.Code, error.Message[(error.Code.Length + 2)..], cell.SourceOffset);
                }
                rowHeight = Math.Max(rowHeight, height + cellEdges.Bottom);
                x += tracks[i];
            }
            x = edges.Left;
            for (var i = 0; i < row.Cells.Count; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                var cell = row.Cells[i];
                var box = new PaintedBox(x, top, tracks[i], rowHeight, cell.BoxStyle, cell.Paragraph.Style.Color, 0);
                charge(box.CommandCount, cell.SourceOffset);
                if (box.CommandCount > 0) boxes.Add(box);
                x += tracks[i];
            }
            rows.Add(new(top, rowHeight, row.IsHeader, row.SourceOffset,
                runStart, runs.Count - runStart, boxStart, boxes.Count - boxStart));
            top += rowHeight;
        }
        var outerHeight = top + edges.Bottom;
        var tableBox = new PaintedBox(0, 0, outerWidth, outerHeight, style, table.Style.Color, 0);
        // Preserve the E1 command order and bytes for every short table.
        // Long tables charge their actual sliced decorations during placement.
        if (outerHeight <= usableHeight + 0.000001)
        {
            charge(tableBox.CommandCount, table.SourceOffset);
            if (tableBox.CommandCount > 0) boxes.Insert(0, tableBox);
        }
        return new(outerWidth, outerHeight, runs, boxes, rows, edges.Top, edges.Bottom);
    }

    private static Edges ResolveEdges(BoxStyle style, double containingWidth, int offset)
        => new(Checked(style.PaddingLeft.Resolve(containingWidth), offset) + style.BorderLeft.EffectiveWidth,
            Checked(style.PaddingRight.Resolve(containingWidth), offset) + style.BorderRight.EffectiveWidth,
            Checked(style.PaddingTop.Resolve(containingWidth), offset) + style.BorderTop.EffectiveWidth,
            Checked(style.PaddingBottom.Resolve(containingWidth), offset) + style.BorderBottom.EffectiveWidth);

    private static double Checked(double value, int offset)
    {
        if (!double.IsFinite(value) || value < 0 || value > 14_400)
            throw Geometry("Resolved table length exceeds supported bounds.", offset);
        return value;
    }

    private static FactsPdfException Geometry(string message, int offset) => new("FPDF1302", message, offset);
}
