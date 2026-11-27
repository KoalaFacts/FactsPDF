using static FactsPDF.TextLines;

namespace FactsPDF;

internal sealed record MeasuredTable(double Width, double Height,
    List<PlacedText> Runs, List<PaintedBox> PaintBoxes, List<PlacedImage> Images,
    List<MeasuredTableRow> Rows, double Top, double Bottom)
{
    /// <summary>The caption's height above the table box (0 without a caption). Its runs are the
    /// first <see cref="CaptionRunCount"/> runs and its box, if painted, the first paint box.</summary>
    internal double CaptionHeight { get; init; }
    internal int CaptionRunCount { get; init; }
    internal int CaptionBoxCount { get; init; }
    /// <summary>Collapsed borders: the full painted segments of every horizontal grid line, in
    /// table coordinates (null for separated borders). A fragment break paints half of a line.</summary>
    internal List<PaintedBox>[]? CollapsedLines { get; init; }
}

// A row's paint range is known only after every row is measured; it is assigned once to the
// row itself (SetBoxRange) instead of a second record copy per row.
internal sealed record MeasuredTableRow(double Top, double Height, bool IsHeader, int SourceOffset,
    int RunStart, int RunCount, int ImageStart, int ImageCount, MeasuredTableCell[] Cells, bool CanSplit = false, bool IsFirstBodyGroup = false, bool AvoidBreakInside = false)
{
    // The collapsed table's top border half above a header row, inside the row so that it repeats
    // with the header on every fragment, as in Chrome; the cells start below it.
    internal double Lead { get; init; }

    internal int BoxStart { get; private set; }
    internal int BoxCount { get; private set; }

    internal void SetBoxRange(int start, int count)
    {
        BoxStart = start;
        BoxCount = count;
    }
}

/// <summary>Measure the table once, retaining only charged output commands.</summary>
internal static class TableLayout
{
    private readonly record struct Edges(double Left, double Right, double Top, double Bottom);
    // Placement ranges are retained only for the current measured row. Do not
    // shift the full-height shared cell paint or split-row continuation units.
    private readonly record struct CellPlacement(CellVerticalAlign Alignment, int SourceOffset,
        double NaturalHeight, int RunStart, int RunEnd, int ImageStart, int ImageEnd,
        int ImageBoxStart, int ImageBoxEnd);

    internal static MeasuredTable Measure(LayoutTable item, PdfOptions options,
        Dictionary<(FontRole Role, int Scalar), (PdfFont? Font, double Width)> resolved,
        Action<int, int> charge, Action<int, int> ensure, CancellationToken cancellation, TableFreshSpace? freshSpace = null)
    {
        var table = item.Node;
        var style = table.LayoutBox;
        var grid = table.Collapsed;
        var edges = ResolveEdges(style, item.AvailableWidth, table.SourceOffset);
        // As for existing blocks, auto fills the parent's content area with
        // the outer box; an explicit width describes the table's content, or
        // with box-sizing:border-box its outer box including padding and borders.
        var specifiedWidth = style.Width is { } specified
            ? Checked(specified.Resolve(item.AvailableWidth)
                - (style.BorderBoxSizing ? edges.Left + edges.Right : 0), table.SourceOffset)
            : (double?)null;
        var tracks = new double[table.Columns.Count];
        // Unless every column has an explicit width, columns use automatic layout;
        // with auto table width the table then shrinks to its max-content width.
        // A cell width also takes automatic layout, where it constrains its column.
        var automatic = !table.Columns.TrueForAll(column => column.Width is not null) || HasCellWidth(table);
        var width = automatic
            ? TableAutoLayout.Resolve(table, specifiedWidth, item.AvailableWidth - edges.Left - edges.Right, tracks,
                options, resolved, cancellation)
            : specifiedWidth ?? item.AvailableWidth - edges.Left - edges.Right;
        var outerWidth = width + edges.Left + edges.Right;
        if (width <= 0 || outerWidth > item.AvailableWidth + 0.000001)
            throw Geometry("Table outer width exceeds containing content width or is zero.", table.SourceOffset);
        if (!automatic)
        {
            var totalWidth = 0d;
            for (var i = 0; i < tracks.Length; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                var column = table.Columns[i];
                var length = column.Width!.Value;
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
        }

        var runs = new List<PlacedText>();
        var boxes = new List<PaintedBox>();
        var images = new List<PlacedImage>();
        var caption = table.Caption is { } captionCell
            ? MeasureCaption(captionCell, outerWidth, options, resolved, charge, ensure, runs, boxes, cancellation)
            : default;
        // A collapsed table repeats the top half of its top border with its header (see Lead).
        var headerLead = 0d;
        if (grid is not null && table.Rows[0].IsHeader)
        {
            headerLead = edges.Top;
            edges = edges with { Top = 0 };
        }
        var firstTop = caption.Height + edges.Top;
        var rows = new List<MeasuredTableRow>();
        // A row's retained cell boxes (null until its first box) and its image decorations
        // (an exact-size copy, the shared empty array for a row without any).
        var rowBoxes = new List<List<PaintedBox>?>();
        var rowImageBoxes = new List<PaintedBox[]>();
        var ambiguous = new List<int>();
        // An uncertain last occupied row may need terminal bottom decoration.
        // Defer aligned-cell failure until we know whether a later occupied
        // row makes that predecessor a complete nonterminal row.
        var ambiguousVerticalOffsets = new List<int>();
        // A kept (break-inside:avoid) last row that fits a fresh page but is split only to carry the
        // table's bottom decoration. Only a multi-page table splits rows.
        var ambiguousAvoidOffset = -1;
        // One scratch list for every cell of this table; each cell keeps an
        // exact-size copy of its units.
        var units = new List<MeasuredCellUnit>();
        // Scratch lists for the row being measured, cleared at the start of every row.
        var imageBoxes = new List<PaintedBox>();
        var placements = new List<CellPlacement>();
        var firstBodyGroup = true;
        var headerHeight = 0d;
        var top = firstTop;
        var usableHeight = options.ContentHeight;
        var fresh = freshSpace ?? new(usableHeight, 0);
        bool measuringHeader = false;
        void CheckHeight(double height, int offset)
        {
            if (!double.IsFinite(height) || (measuringHeader && height > usableHeight + 0.000001))
                throw Geometry("A table row exceeds the usable page height.", offset);
        }
        void RetainCellBoxes(int index)
        {
            var measuredRow = rows[index];
            foreach (var cell in measuredRow.Cells)
            {
                cancellation.ThrowIfCancellationRequested();
                // Count first: most cells paint nothing, so no box is created for them.
                var commands = PaintedBox.CountCommands(cell.Width, measuredRow.Height - measuredRow.Lead, cell.BoxStyle);
                charge(commands, cell.SourceOffset);
                if (commands > 0)
                    (rowBoxes[index] ??= new(measuredRow.Cells.Length)).Add(new PaintedBox(cell.X,
                        measuredRow.Top + measuredRow.Lead, cell.Width, measuredRow.Height - measuredRow.Lead,
                        cell.BoxStyle, cell.TextColor, 0, SourceOffset: cell.SourceOffset));
            }
        }
        // First and last decoration may belong to different pages.
        CheckHeight(firstTop, table.SourceOffset);
        CheckHeight(edges.Bottom, table.SourceOffset);
        foreach (var row in table.Rows)
        {
            cancellation.ThrowIfCancellationRequested();
            measuringHeader = row.IsHeader;
            var lead = row.IsHeader ? headerLead : 0;
            top += lead;
            var cells = new MeasuredTableCell[row.Cells.Count];
            var cellIndex = 0;
            var runStart = runs.Count;
            var imageStart = images.Count;
            imageBoxes.Clear();
            placements.Clear();
            var rowHeight = 0d;
            var x = edges.Left;
            var track = 0;
            foreach (var cell in row.Cells)
            {
                cancellation.ThrowIfCancellationRequested();
                var cellRunStart = runs.Count;
                var cellImageStart = images.Count;
                var cellImageBoxStart = imageBoxes.Count;
                var cellWidth = 0d;
                for (var j = 0; j < cell.ColumnSpan; j++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    cellWidth += tracks[track++];
                }
                var cellEdges = ResolveEdges(cell.BoxStyle, cellWidth, cell.SourceOffset);
                var contentWidth = cellWidth - cellEdges.Left - cellEdges.Right;
                // An automatic column is never narrower than its cells' min-content, so only
                // an empty column can leave a cell no content width; clear the roundoff.
                if (automatic && contentWidth > -0.000001) contentWidth = Math.Max(0, contentWidth);
                else if (contentWidth <= 0)
                    throw Geometry("Cell content width must remain positive after padding and borders.", cell.SourceOffset);
                units.Clear();
                var height = cellEdges.Top;
                var pending = 0d;
                CheckHeight(height + cellEdges.Bottom, cell.SourceOffset);
                foreach (var content in cell.Contents)
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (content is TableCellImage imageContent)
                    {
                        var image = imageContent.Image;
                        var geometry = ImageLayout.Resolve(image, contentWidth);
                        var gap = pending;
                        height += gap;
                        pending = 0;
                        CheckHeight(height + geometry.OuterHeight + cellEdges.Bottom, image.SourceOffset);
                        var imageX = x + cellEdges.Left + (cell.Style.Alignment switch
                        {
                            TextAlignment.Center => (contentWidth - geometry.OuterWidth) / 2,
                            TextAlignment.Right => contentWidth - geometry.OuterWidth,
                            _ => 0
                        });
                        charge(1, image.SourceOffset);
                        images.Add(new(image.Image, imageX + geometry.Left, top + height + geometry.Top,
                            geometry.Width, geometry.Height));
                        var imageIndex = images.Count - 1;
                        var decorationStart = imageBoxes.Count;
                        var imageBox = new PaintedBox(imageX, top + height, geometry.OuterWidth,
                            geometry.OuterHeight, image.BoxStyle, image.Style.Color, 0, SourceOffset: image.SourceOffset);
                        charge(imageBox.CommandCount, image.SourceOffset);
                        if (imageBox.CommandCount > 0) imageBoxes.Add(imageBox);
                        units.Add(new(TableCellUnitKind.Image, image.SourceOffset, top + height,
                            geometry.OuterHeight, gap, units.Count == 0, runs.Count, 0, imageIndex,
                            decorationStart, imageBoxes.Count - decorationStart));
                        height += geometry.OuterHeight;
                        continue;
                    }
                    var paragraph = ((TableCellText)content).Paragraph;
                    var offset = paragraph.SourceOffset;
                    var before = paragraph.Style.MarginBefore;
                    var after = paragraph.Style.MarginAfter;
                    // Empty paragraphs contribute their largest margin once,
                    // without wrapping, resolving fonts, or allocating lines.
                    var hasText = HasTextContent(paragraph, cancellation);
                    var occupied = hasText;
                    if (!occupied)
                        foreach (var run in paragraph.Runs)
                        {
                            cancellation.ThrowIfCancellationRequested();
                            // A pre-line paragraph made entirely of authored
                            // newlines occupies real line height, not text
                            // display commands. Keep hasText (and ensure)
                            // unchanged for empty painted lines.
                            if (run.IsBreak || paragraph.Style.PreserveLineBreaks &&
                                (run.Text.Contains('\n') || run.Text.Contains('\r') ||
                                 run.Text.Contains('\f')))
                            { occupied = true; break; }
                        }
                    if (!occupied)
                    {
                        pending = Math.Max(pending, Math.Max(before, after));
                        CheckHeight(height + pending + cellEdges.Bottom, offset);
                        if (units.Count == 0 && height + pending > usableHeight + .000001)
                            throw Geometry("Initial cell spacing exceeds usable page height.", offset);
                        continue;
                    }
                    var paragraphGap = Math.Max(pending, before);
                    height += paragraphGap;
                    pending = 0;
                    if (units.Count == 0 && height > usableHeight + .000001)
                        throw Geometry("Initial cell spacing exceeds usable page height.", offset);
                    CheckHeight(height + cellEdges.Bottom, offset);
                    // Fail before allocating the shared wrapper's glyph buffers.
                    if (hasText) ensure(1, offset);
                    try
                    {
                        foreach (var line in Wrap(paragraph, contentWidth, options, resolved, cancellation))
                        {
                            cancellation.ThrowIfCancellationRequested();
                            CheckHeight(height + line.Height + cellEdges.Bottom, offset);
                            // Match ordinary paragraph placement: the first logical line
                            // consumes the inset before alignment, including inherited
                            // text-indent from a report container outside the table.
                            var lineWidth = contentWidth - line.FirstLineIndent;
                            var alignedX = x + cellEdges.Left + line.FirstLineIndent +
                                ((line.AlignmentOverride ?? paragraph.Style.Alignment) switch
                                {
                                    TextAlignment.Center => (lineWidth - line.Width) / 2,
                                    TextAlignment.Right => lineWidth - line.Width,
                                    _ => 0
                                });
                            var lineStart = runs.Count;
                            AppendLine(line, alignedX, -top - height - line.Ascent,
                                runs, charge, cancellation, offset);
                            units.Add(new(TableCellUnitKind.TextLine, offset, top + height, line.Height,
                                paragraphGap, units.Count == 0, lineStart, runs.Count - lineStart, -1, 0, 0));
                            paragraphGap = 0;
                            height += line.Height;
                        }
                    }
                    catch (FactsPdfException error) when (error.SourceOffset < 0)
                    {
                        throw new FactsPdfException(error.Code, error.Message[(error.Code.Length + 2)..], offset);
                    }
                    pending = after;
                    CheckHeight(height + pending + cellEdges.Bottom, offset);
                }
                CheckHeight(height + pending + cellEdges.Bottom, cell.SourceOffset);
                var naturalHeight = height + pending + cellEdges.Bottom;
                rowHeight = Math.Max(rowHeight, naturalHeight);
                placements.Add(new(cell.BoxStyle.VerticalAlign, cell.SourceOffset, naturalHeight,
                    cellRunStart, runs.Count, cellImageStart, images.Count,
                    cellImageBoxStart, imageBoxes.Count));
                cells[cellIndex++] = new(x, cellWidth, x + cellEdges.Left, contentWidth, cellEdges.Top,
                    cellEdges.Bottom, units.Count == 0 ? pending : units[0].LeadingGap, pending,
                    cell.BoxStyle, cell.Style.Color, cell.SourceOffset, units.ToArray());
                x += cellWidth;
                if (!double.IsFinite(x)) throw Geometry("Cell coordinate must remain finite.", cell.SourceOffset);
            }
            // Resolve a predecessor only after the next occupied row has been
            // measured. Until then its terminal bottom may make it splittable.
            if (!row.IsHeader && rowHeight > 0)
            {
                foreach (var index in ambiguous) RetainCellBoxes(index);
                ambiguous.Clear();
                ambiguousVerticalOffsets.Clear();
                ambiguousAvoidOffset = -1;
            }
            var isFirstBodyGroup = !row.IsHeader && firstBodyGroup;
            var capacity = fresh.UsableHeight - headerHeight
                - (isFirstBodyGroup ? firstTop + fresh.FirstLeading : 0);
            var canSplit = !row.IsHeader && rowHeight > capacity + .000001;
            if (canSplit && grid is not null) throw UnsplitCollapsedRow(row.SourceOffset);
            var uncertain = !row.IsHeader && !canSplit && rowHeight + edges.Bottom > capacity + .000001;
            if (uncertain && row.AvoidBreakInside && ambiguousAvoidOffset < 0) ambiguousAvoidOffset = row.SourceOffset;
            // When the complete row remains atomic, place its shorter cell
            // content within the shared painted row height. A fragmented body
            // row has independent cell cursors and cannot yet give middle/bottom
            // their CSS meaning. Reject it rather than silently top-aligning.
            foreach (var placement in placements)
            {
                cancellation.ThrowIfCancellationRequested();
                if (placement.Alignment == CellVerticalAlign.Top) continue;
                if (canSplit)
                    throw Geometry("vertical-align:middle/bottom requires an unsplit table row.", placement.SourceOffset);
                if (uncertain) ambiguousVerticalOffsets.Add(placement.SourceOffset);
                var free = rowHeight - placement.NaturalHeight;
                if (!double.IsFinite(free) || free < -0.000001)
                    throw Geometry("Table cell vertical alignment has invalid row geometry.", placement.SourceOffset);
                var shift = Math.Max(0, free) * (placement.Alignment == CellVerticalAlign.Middle ? .5 : 1);
                for (var i = placement.RunStart; i < placement.RunEnd; i++)
                    runs[i] = runs[i] with { Baseline = runs[i].Baseline - shift };
                for (var i = placement.ImageStart; i < placement.ImageEnd; i++)
                    images[i] = images[i] with { Top = images[i].Top + shift };
                for (var i = placement.ImageBoxStart; i < placement.ImageBoxEnd; i++)
                    imageBoxes[i] = imageBoxes[i] with { Top = imageBoxes[i].Top + shift };
            }
            rows.Add(new(top - lead, rowHeight + lead, row.IsHeader, row.SourceOffset,
                runStart, runs.Count - runStart, imageStart, images.Count - imageStart, cells, canSplit, isFirstBodyGroup,
                !row.IsHeader && row.AvoidBreakInside) { Lead = lead });
            rowBoxes.Add(null);
            rowImageBoxes.Add(imageBoxes.Count == 0 ? [] : imageBoxes.ToArray());
            if (uncertain) ambiguous.Add(rows.Count - 1);
            else if (!canSplit) RetainCellBoxes(rows.Count - 1);
            if (row.IsHeader) headerHeight = rowHeight + lead;
            else if (rowHeight > 0) firstBodyGroup = false;
            top += rowHeight;
            CheckHeight(top, row.SourceOffset);
        }
        // The remaining uncertain rows really are terminal: only here does
        // table-bottom decoration force their continuation. Earlier uncertain
        // rows were resolved by the following occupied row, so they may use
        // valid whole-row middle/bottom placement without false rejection.
        if (ambiguousVerticalOffsets.Count > 0)
            throw Geometry("vertical-align:middle/bottom requires an unsplit terminal table row.",
                ambiguousVerticalOffsets[0]);
        if (grid is not null && ambiguous.Count > 0) throw UnsplitCollapsedRow(rows[ambiguous[0]].SourceOffset);
        foreach (var index in ambiguous)
            rows[index] = rows[index] with { CanSplit = true };
        var collapsedLines = grid is null ? null : CollapsedLines(grid, rows, tracks, edges.Left, table.SourceOffset);
        void AddCollapsed(List<PaintedBox> segments)
        {
            foreach (var segment in segments)
            {
                cancellation.ThrowIfCancellationRequested();
                charge(segment.CommandCount, table.SourceOffset);
                boxes.Add(segment);
            }
        }
        // Assign stable paint ranges once in source order, without insertion or
        // shifting suffixes. Image decorations always follow all row cells.
        for (var i = 0; i < rows.Count; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            var start = boxes.Count;
            if (rowBoxes[i] is { } retained) boxes.AddRange(retained);
            if (collapsedLines is not null)
            {
                // Each row paints its top grid line, then its vertical segments, and the last row the
                // table's bottom line. A header paints the line below it as well, so the line
                // repeats with the header; the first body row then leaves it out.
                var header = rows[0].IsHeader;
                if (!(i == 1 && header)) AddCollapsed(collapsedLines[i]);
                AddCollapsed(VerticalSegments(grid!, rows, i, tracks, edges.Left, table.SourceOffset));
                if (i == 0 && header) AddCollapsed(collapsedLines[1]);
                if (i == rows.Count - 1) AddCollapsed(collapsedLines[rows.Count]);
            }
            var imageStart = boxes.Count;
            boxes.AddRange(rowImageBoxes[i]);
            foreach (var cell in rows[i].Cells)
                for (var j = 0; j < cell.Units.Length; j++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    var unit = cell.Units[j];
                    if (unit.Kind == TableCellUnitKind.Image)
                        cell.Units[j] = unit with { ImageBoxStart = imageStart + unit.ImageBoxStart };
                }
            rows[i].SetBoxRange(start, boxes.Count - start);
        }
        var outerHeight = top + edges.Bottom;
        CheckHeight(outerHeight, table.SourceOffset);
        if (ambiguousAvoidOffset >= 0 && outerHeight > usableHeight + 0.000001) throw UnkeptRow(ambiguousAvoidOffset);
        var tableBox = new PaintedBox(0, caption.Height, outerWidth, outerHeight - caption.Height, style, table.Style.Color, 0,
            SourceOffset: table.SourceOffset);
        // Preserve the E1 command order and bytes for every short table.
        // Long tables charge their actual sliced decorations during placement.
        if (outerHeight <= usableHeight + 0.000001)
        {
            charge(tableBox.CommandCount, table.SourceOffset);
            if (tableBox.CommandCount > 0) boxes.Insert(0, tableBox);
        }
        return new(outerWidth, outerHeight, runs, boxes, images, rows, firstTop, edges.Bottom)
        {
            CaptionHeight = caption.Height, CaptionRunCount = caption.RunCount, CaptionBoxCount = caption.BoxCount,
            CollapsedLines = collapsedLines
        };
    }

    private readonly record struct CaptionLayout(double Height, int RunCount, int BoxCount);

    // The caption is a block above the table box, as wide as the table's border box: its padding
    // and borders inside that width, its text wrapped and aligned like cell text.
    private static CaptionLayout MeasureCaption(TableCell caption, double width, PdfOptions options,
        Dictionary<(FontRole Role, int Scalar), (PdfFont? Font, double Width)> resolved,
        Action<int, int> charge, Action<int, int> ensure, List<PlacedText> runs, List<PaintedBox> boxes,
        CancellationToken cancellation)
    {
        // Chrome widens the table to a wider caption's min-content width instead.
        var (minimum, _) = TableAutoLayout.CellWidths(caption, options, resolved, cancellation);
        if (minimum > width + 0.000001)
            throw Geometry("A caption wider than its table (its min-content width with padding and borders) is not supported.",
                caption.SourceOffset);
        var edges = ResolveEdges(caption.BoxStyle, width, caption.SourceOffset);
        var contentWidth = width - edges.Left - edges.Right;
        if (contentWidth <= 0)
            throw Geometry("Caption content width must remain positive after padding and borders.", caption.SourceOffset);
        var height = edges.Top;
        foreach (var content in caption.Contents)
        {
            cancellation.ThrowIfCancellationRequested();
            var paragraph = ((TableCellText)content).Paragraph;
            var offset = paragraph.SourceOffset;
            if (HasTextContent(paragraph, cancellation)) ensure(1, offset);
            try
            {
                foreach (var line in Wrap(paragraph, contentWidth, options, resolved, cancellation))
                {
                    cancellation.ThrowIfCancellationRequested();
                    var lineWidth = contentWidth - line.FirstLineIndent;
                    var x = edges.Left + line.FirstLineIndent + ((line.AlignmentOverride ?? paragraph.Style.Alignment) switch
                    {
                        TextAlignment.Center => (lineWidth - line.Width) / 2,
                        TextAlignment.Right => lineWidth - line.Width,
                        _ => 0
                    });
                    AppendLine(line, x, -height - line.Ascent, runs, charge, cancellation, offset);
                    height += line.Height;
                }
            }
            catch (FactsPdfException error) when (error.SourceOffset < 0)
            {
                throw new FactsPdfException(error.Code, error.Message[(error.Code.Length + 2)..], offset);
            }
        }
        height += edges.Bottom;
        if (!double.IsFinite(height) || height > 14_400)
            throw Geometry("Resolved table length exceeds supported bounds.", caption.SourceOffset);
        var box = new PaintedBox(0, 0, width, height, caption.BoxStyle, caption.Style.Color, 0,
            SourceOffset: caption.SourceOffset);
        var commands = box.CommandCount;
        charge(commands, caption.SourceOffset);
        if (commands > 0) boxes.Add(box);
        return new(height, runs.Count, commands > 0 ? 1 : 0);
    }

    // The y of a horizontal grid line in table coordinates: the top of its row (below a header's
    // lead), or the bottom of the last row.
    private static double LineY(List<MeasuredTableRow> rows, int line)
        => line == rows.Count ? rows[^1].Top + rows[^1].Height : rows[line].Top + rows[line].Lead;

    private static double GridX(double[] tracks, double left, int line)
    {
        var x = left;
        for (var c = 0; c < line; c++) x += tracks[c];
        return x;
    }

    // The widest vertical segment meeting horizontal grid line `line` at vertical line `column`.
    private static double VerticalJoin(TableBorderGrid grid, int line, int column)
        => Math.Max(line > 0 ? grid.Vertical(line - 1, column).Width : 0,
            line < grid.Rows ? grid.Vertical(line, column).Width : 0);

    // The widest horizontal segment meeting vertical line `column` on horizontal line `line`.
    private static double HorizontalJoin(TableBorderGrid grid, int line, int column)
        => Math.Max(column > 0 ? grid.Horizontal(line, column - 1).Width : 0,
            column < grid.Columns ? grid.Horizontal(line, column).Width : 0);

    // Every horizontal segment, centred on its grid line and reaching over half the widest vertical
    // border at each end, as Chrome paints collapsed borders.
    private static List<PaintedBox>[] CollapsedLines(TableBorderGrid grid, List<MeasuredTableRow> rows,
        double[] tracks, double left, int sourceOffset)
    {
        var lines = new List<PaintedBox>[rows.Count + 1];
        for (var line = 0; line <= rows.Count; line++)
        {
            var segments = new List<PaintedBox>();
            lines[line] = segments;
            var y = LineY(rows, line);
            var x = left;
            for (var c = 0; c < grid.Columns; c++)
            {
                var next = x + tracks[c];
                var edge = grid.Horizontal(line, c);
                if (edge.Paints)
                {
                    var start = x - VerticalJoin(grid, line, c) / 2;
                    var end = next + VerticalJoin(grid, line, c + 1) / 2;
                    segments.Add(new PaintedBox(start, y - edge.Width / 2, end - start, edge.Width,
                        new BoxStyle { BorderTop = Stroke(edge) }, edge.Color, 0, SourceOffset: sourceOffset));
                }
                x = next;
            }
        }
        return lines;
    }

    // A row's vertical segments, centred on their grid lines. A segment at least as wide as the
    // widest horizontal border at a joint covers the joint; a narrower one stops at that border.
    // Only the last row's segments reach down into the table's bottom border; the next row's
    // segments cover the rest of an inner joint.
    private static List<PaintedBox> VerticalSegments(TableBorderGrid grid, List<MeasuredTableRow> rows, int row,
        double[] tracks, double left, int sourceOffset)
    {
        var segments = new List<PaintedBox>();
        var top = LineY(rows, row);
        var bottom = LineY(rows, row + 1);
        for (var line = 0; line <= grid.Columns; line++)
        {
            var edge = grid.Vertical(row, line);
            if (!edge.Paints) continue;
            var above = HorizontalJoin(grid, row, line);
            var below = HorizontalJoin(grid, row + 1, line);
            var start = edge.Width >= above ? top - above / 2 : top + above / 2;
            var end = edge.Width < below ? bottom - below / 2
                : row == rows.Count - 1 ? bottom + below / 2 : bottom;
            if (end <= start) continue;
            var x = GridX(tracks, left, line);
            segments.Add(new PaintedBox(x - edge.Width / 2, start, edge.Width, end - start,
                new BoxStyle { BorderLeft = Stroke(edge) }, edge.Color, 0, SourceOffset: sourceOffset));
        }
        return segments;
    }

    private static BorderEdge Stroke(CollapsedEdge edge) => new(edge.Width, true, edge.Color) { Pattern = edge.Pattern };

    // Chrome paints a collapsed row split across pages with cell fragments whose borders are not
    // modelled here; fail rather than paint an approximation.
    private static FactsPdfException UnsplitCollapsedRow(int offset) => Geometry(
        "border-collapse:collapse is not implemented for a table row that must split across pages.", offset);

    private static bool HasCellWidth(TableNode table)
    {
        foreach (var row in table.Rows)
            foreach (var cell in row.Cells)
                if (cell.BoxStyle.Width is not null) return true;
        return false;
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

    // Chrome keeps such a row whole and lets the table's bottom padding and border overflow the page
    // bottom into the margin and the next page; FactsPDF never paints outside the usable page.
    private static FactsPdfException UnkeptRow(int offset) => Geometry(
        "break-inside:avoid last table row must fit a fresh page with its repeated header and the table's bottom padding and border.", offset);
}
