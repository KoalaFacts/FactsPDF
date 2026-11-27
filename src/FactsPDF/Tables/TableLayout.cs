using static FactsPDF.TextLines;

namespace FactsPDF;

internal sealed record MeasuredTable(double Width, double Height,
    List<PlacedText> Runs, List<PaintedBox> PaintBoxes, List<PlacedImage> Images,
    List<MeasuredTableRow> Rows, double Top, double Bottom);

// A row's paint range is known only after every row is measured; it is assigned once to the
// row itself (SetBoxRange) instead of a second record copy per row.
internal sealed record MeasuredTableRow(double Top, double Height, bool IsHeader, int SourceOffset,
    int RunStart, int RunCount, int ImageStart, int ImageCount, MeasuredTableCell[] Cells, bool CanSplit = false, bool IsFirstBodyGroup = false)
{
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
        var style = table.BoxStyle;
        var edges = ResolveEdges(style, item.AvailableWidth, table.SourceOffset);
        // As for existing blocks, auto fills the parent's content area with
        // the outer box; an explicit width describes the table's content, or
        // with box-sizing:border-box its outer box including padding and borders.
        var width = style.Width is { } specified
            ? Checked(specified.Resolve(item.AvailableWidth)
                - (style.BorderBoxSizing ? edges.Left + edges.Right : 0), table.SourceOffset)
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
        var images = new List<PlacedImage>();
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
        // The first kept (break-inside:avoid) row that pagination would split, and likewise a kept
        // row that only the terminal decoration would split. Only a multi-page table splits rows.
        var splitAvoidOffset = -1;
        var ambiguousAvoidOffset = -1;
        // One scratch list for every cell of this table; each cell keeps an
        // exact-size copy of its units.
        var units = new List<MeasuredCellUnit>();
        // Scratch lists for the row being measured, cleared at the start of every row.
        var imageBoxes = new List<PaintedBox>();
        var placements = new List<CellPlacement>();
        var firstBodyGroup = true;
        var headerHeight = 0d;
        var top = edges.Top;
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
                var commands = PaintedBox.CountCommands(cell.Width, measuredRow.Height, cell.BoxStyle);
                charge(commands, cell.SourceOffset);
                if (commands > 0)
                    (rowBoxes[index] ??= new(measuredRow.Cells.Length)).Add(new PaintedBox(cell.X,
                        measuredRow.Top, cell.Width, measuredRow.Height, cell.BoxStyle, cell.TextColor, 0,
                        SourceOffset: cell.SourceOffset));
            }
        }
        // First and last decoration may belong to different pages.
        CheckHeight(edges.Top, table.SourceOffset);
        CheckHeight(edges.Bottom, table.SourceOffset);
        foreach (var row in table.Rows)
        {
            cancellation.ThrowIfCancellationRequested();
            measuringHeader = row.IsHeader;
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
                if (contentWidth <= 0)
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
                - (isFirstBodyGroup ? edges.Top + fresh.FirstLeading : 0);
            var canSplit = !row.IsHeader && rowHeight > capacity + .000001;
            var uncertain = !row.IsHeader && !canSplit && rowHeight + edges.Bottom > capacity + .000001;
            if (canSplit && row.AvoidBreakInside && splitAvoidOffset < 0) splitAvoidOffset = row.SourceOffset;
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
            rows.Add(new(top, rowHeight, row.IsHeader, row.SourceOffset,
                runStart, runs.Count - runStart, imageStart, images.Count - imageStart, cells, canSplit, isFirstBodyGroup));
            rowBoxes.Add(null);
            rowImageBoxes.Add(imageBoxes.Count == 0 ? [] : imageBoxes.ToArray());
            if (uncertain) ambiguous.Add(rows.Count - 1);
            else if (!canSplit) RetainCellBoxes(rows.Count - 1);
            if (row.IsHeader) headerHeight = rowHeight;
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
        if (splitAvoidOffset < 0) splitAvoidOffset = ambiguousAvoidOffset;
        foreach (var index in ambiguous)
            rows[index] = rows[index] with { CanSplit = true };
        // Assign stable paint ranges once in source order, without insertion or
        // shifting suffixes. Image decorations always follow all row cells.
        for (var i = 0; i < rows.Count; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            var start = boxes.Count;
            if (rowBoxes[i] is { } retained) boxes.AddRange(retained);
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
        if (splitAvoidOffset >= 0 && outerHeight > usableHeight + 0.000001) throw UnkeptRow(splitAvoidOffset);
        var tableBox = new PaintedBox(0, 0, outerWidth, outerHeight, style, table.Style.Color, 0,
            SourceOffset: table.SourceOffset);
        // Preserve the E1 command order and bytes for every short table.
        // Long tables charge their actual sliced decorations during placement.
        if (outerHeight <= usableHeight + 0.000001)
        {
            charge(tableBox.CommandCount, table.SourceOffset);
            if (tableBox.CommandCount > 0) boxes.Insert(0, tableBox);
        }
        return new(outerWidth, outerHeight, runs, boxes, images, rows, edges.Top, edges.Bottom);
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

    // A kept row FactsPDF would split fails, as a kept report card that cannot fit a page does:
    // a browser would first move it to a fresh page or break before the header instead.
    private static FactsPdfException UnkeptRow(int offset) => Geometry(
        "break-inside:avoid table row must fit a fresh page with its repeated header and table decoration.", offset);
}
