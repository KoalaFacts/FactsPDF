using static FactsPDF.TextLines;

namespace FactsPDF;

internal sealed record MeasuredTable(double Width, double Height,
    List<PlacedText> Runs, List<PaintedBox> PaintBoxes, List<PlacedImage> Images,
    List<MeasuredTableRow> Rows, double Top, double Bottom);

internal sealed record MeasuredTableRow(double Top, double Height, bool IsHeader, int SourceOffset,
    int RunStart, int RunCount, int BoxStart, int BoxCount, int ImageStart, int ImageCount, List<MeasuredTableCell> Cells, bool CanSplit = false, bool IsFirstBodyGroup = false);

/// <summary>Measure the table once, retaining only charged output commands.</summary>
internal static class TableLayout
{
    private readonly record struct Edges(double Left, double Right, double Top, double Bottom);

    internal static MeasuredTable Measure(LayoutTable item, PdfOptions options,
        Dictionary<(FontRole Role, int Scalar), (PdfFont? Font, double Width)> resolved,
        CancellationToken cancellation, Action<int, int> charge, Action<int, int> ensure, TableFreshSpace? freshSpace = null)
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
        var images = new List<PlacedImage>();
        var rows = new List<MeasuredTableRow>();
        var rowBoxes = new List<List<PaintedBox>>();
        var rowImageBoxes = new List<List<PaintedBox>>();
        var ambiguous = new List<int>();
        var firstBodyGroup = true;
        var headerHeight = 0d;
        var top = edges.Top;
        var usableHeight = options.PageHeight - 2 * options.Margin;
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
                var box = new PaintedBox(cell.X, measuredRow.Top, cell.Width, measuredRow.Height,
                    cell.BoxStyle, cell.TextColor, 0);
                charge(box.CommandCount, cell.SourceOffset);
                if (box.CommandCount > 0) rowBoxes[index].Add(box);
            }
        }
        // First and last decoration may belong to different pages.
        CheckHeight(edges.Top, table.SourceOffset);
        CheckHeight(edges.Bottom, table.SourceOffset);
        foreach (var row in table.Rows)
        {
            cancellation.ThrowIfCancellationRequested();
            measuringHeader = row.IsHeader;
            var cells = new List<MeasuredTableCell>();
            var runStart = runs.Count;
            var imageStart = images.Count;
            var imageBoxes = new List<PaintedBox>();
            var rowHeight = 0d;
            var x = edges.Left;
            var track = 0;
            foreach (var cell in row.Cells)
            {
                cancellation.ThrowIfCancellationRequested();
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
                var units = new List<MeasuredCellUnit>();
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
                            geometry.OuterHeight, image.BoxStyle, image.Style.Color, 0);
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
                            if (run.IsBreak) { occupied = true; break; }
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
                            var alignedX = x + cellEdges.Left + (paragraph.Style.Alignment switch
                            {
                                TextAlignment.Center => (contentWidth - line.Width) / 2,
                                TextAlignment.Right => contentWidth - line.Width,
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
                rowHeight = Math.Max(rowHeight, height + pending + cellEdges.Bottom);
                cells.Add(new(x, cellWidth, x + cellEdges.Left, contentWidth, cellEdges.Top,
                    cellEdges.Bottom, units.Count == 0 ? pending : units[0].LeadingGap, pending,
                    cell.BoxStyle, cell.Style.Color, cell.SourceOffset, units));
                x += cellWidth;
                if (!double.IsFinite(x)) throw Geometry("Cell coordinate must remain finite.", cell.SourceOffset);
            }
            // Resolve a predecessor only after the next occupied row has been
            // measured. Until then its terminal bottom may make it splittable.
            if (!row.IsHeader && rowHeight > 0)
            {
                foreach (var index in ambiguous) RetainCellBoxes(index);
                ambiguous.Clear();
            }
            var isFirstBodyGroup = !row.IsHeader && firstBodyGroup;
            var capacity = fresh.UsableHeight - headerHeight
                - (isFirstBodyGroup ? edges.Top + fresh.FirstLeading : 0);
            var canSplit = !row.IsHeader && rowHeight > capacity + .000001;
            var uncertain = !row.IsHeader && !canSplit && rowHeight + edges.Bottom > capacity + .000001;
            rows.Add(new(top, rowHeight, row.IsHeader, row.SourceOffset,
                runStart, runs.Count - runStart, 0, 0, imageStart, images.Count - imageStart, cells, canSplit, isFirstBodyGroup));
            rowBoxes.Add([]);
            rowImageBoxes.Add(imageBoxes);
            if (uncertain) ambiguous.Add(rows.Count - 1);
            else if (!canSplit) RetainCellBoxes(rows.Count - 1);
            if (row.IsHeader) headerHeight = rowHeight;
            else if (rowHeight > 0) firstBodyGroup = false;
            top += rowHeight;
            CheckHeight(top, row.SourceOffset);
        }
        foreach (var index in ambiguous)
            rows[index] = rows[index] with { CanSplit = true };
        // Assign stable paint ranges once in source order, without insertion or
        // shifting suffixes. Image decorations always follow all row cells.
        for (var i = 0; i < rows.Count; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            var start = boxes.Count;
            boxes.AddRange(rowBoxes[i]);
            var imageStart = boxes.Count;
            boxes.AddRange(rowImageBoxes[i]);
            foreach (var cell in rows[i].Cells)
                for (var j = 0; j < cell.Units.Count; j++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    var unit = cell.Units[j];
                    if (unit.Kind == TableCellUnitKind.Image)
                        cell.Units[j] = unit with { ImageBoxStart = imageStart + unit.ImageBoxStart };
                }
            rows[i] = rows[i] with { BoxStart = start, BoxCount = boxes.Count - start };
        }
        var outerHeight = top + edges.Bottom;
        CheckHeight(outerHeight, table.SourceOffset);
        var tableBox = new PaintedBox(0, 0, outerWidth, outerHeight, style, table.Style.Color, 0);
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
}
