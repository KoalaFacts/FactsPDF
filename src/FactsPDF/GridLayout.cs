namespace FactsPDF;

/// <summary>
/// Grid containers with explicit columns (CSS Grid Layout): fixed lengths and percentages, fr
/// fractions and auto tracks, items auto-placed row by row into auto-sized implicit rows, column
/// and row gaps, justify-content, justify-items/justify-self and align-items/align-self. Column
/// widths follow the track sizing algorithm (§12) for single-span items: auto and fr tracks start
/// at their items' min-content contributions, auto tracks grow to their max-content
/// contributions, the leftover space is shared by the fr tracks, and with justify-content normal
/// any space left after that stretches the auto tracks. Each row is measured like a flex line and
/// placed whole; a page may break between rows.
/// </summary>
internal static partial class FlexLayout
{
    internal static List<MeasuredFlex> MeasureGrid(LayoutGrid grid, PdfOptions options,
        Dictionary<(FontRole Role, int Scalar), (PdfFont? Font, double Width)> resolved,
        LayoutBudget budget, CancellationToken cancellation, out double rowGap)
    {
        var container = grid.Container;
        var style = container.BoxStyle.Flex;
        var width = grid.ContentWidth;
        var usableHeight = options.PageHeight - 2 * options.Margin;
        var tracks = style.GridColumns ?? [new GridTrack(GridTrackKind.Auto)];
        var columns = tracks.Length;
        var columnGap = Checked(style.ColumnGap?.Resolve(width) ?? 0, container.SourceOffset);
        // A percentage row gap resolves against the container's indefinite height: zero.
        rowGap = style.RowGap is { IsPercent: false } fixedRowGap ? Checked(fixedRowGap.Value, container.SourceOffset) : 0;

        // Auto-placement in order-modified document order, row by row.
        var children = new List<(BlockChild Child, int Order, int Index)>(container.Children.Count);
        for (var i = 0; i < container.Children.Count; i++)
        {
            var child = container.Children[i];
            if (child is TableNode table)
                throw new FactsPdfException("FPDF1201",
                    "A table cannot be a grid item; wrap it in a report panel.", table.SourceOffset);
            var order = child switch { BlockNode node => node.BoxStyle.Flex.Order, ImageNode image => image.BoxStyle.Flex.Order, _ => 0 };
            children.Add((child, order, i));
        }
        var placed = children.OrderBy(entry => entry.Order).ThenBy(entry => entry.Index).Select(entry => entry.Child).ToList();

        // Track sizing: min-content and max-content contributions of each column's items.
        var minimum = new double[columns];
        var maximum = new double[columns];
        for (var i = 0; i < placed.Count; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            var (min, max) = Intrinsic.Of(placed[i], options, resolved, cancellation);
            if (double.IsNaN(max))
                throw new FactsPdfException("FPDF1201", "A table cannot size a grid column from its content.", container.SourceOffset);
            var column = i % columns;
            minimum[column] = Math.Max(minimum[column], min);
            maximum[column] = Math.Max(maximum[column], max);
        }
        var size = new double[columns];
        var limit = new double[columns];
        for (var c = 0; c < columns; c++)
        {
            switch (tracks[c].Kind)
            {
                case GridTrackKind.Fixed:
                    size[c] = limit[c] = Checked(tracks[c].Length.Resolve(width), container.SourceOffset);
                    break;
                case GridTrackKind.Auto:
                    size[c] = minimum[c];
                    limit[c] = Math.Max(minimum[c], maximum[c]);
                    break;
                default:
                    // minmax(auto, Nfr): the automatic minimum, and no growth before the fr step.
                    size[c] = limit[c] = minimum[c];
                    break;
            }
        }
        var gaps = columnGap * (columns - 1);
        var free = width - gaps - size.Sum();
        if (free < -0.001)
            throw new FactsPdfException("FPDF1302",
                "Grid columns cannot fit their fixed sizes and content minimums in the grid container.", container.SourceOffset);
        // Maximize tracks: share the free space equally among tracks below their growth limits.
        for (var round = 0; round <= columns && free > Epsilon; round++)
        {
            cancellation.ThrowIfCancellationRequested();
            var growable = Enumerable.Range(0, columns).Where(c => size[c] < limit[c] - Epsilon).ToList();
            if (growable.Count == 0) break;
            var share = free / growable.Count;
            foreach (var c in growable)
            {
                var add = Math.Min(share, limit[c] - size[c]);
                size[c] += add;
                free -= add;
            }
        }
        // Expand flexible tracks: one fr is the leftover space over the flex factors, leaving out
        // a track whose minimum is larger than its share.
        var flexible = Enumerable.Range(0, columns).Where(c => tracks[c].Kind == GridTrackKind.Fraction).ToList();
        if (flexible.Count > 0)
        {
            var leftover = width - gaps - Enumerable.Range(0, columns)
                .Where(c => tracks[c].Kind != GridTrackKind.Fraction).Sum(c => size[c]);
            var active = new List<int>(flexible);
            double fr;
            while (true)
            {
                cancellation.ThrowIfCancellationRequested();
                fr = Math.Max(0, leftover) / Math.Max(1, active.Sum(c => tracks[c].Fraction));
                var inflexible = active.Where(c => size[c] > fr * tracks[c].Fraction + Epsilon).ToList();
                if (inflexible.Count == 0) break;
                foreach (var c in inflexible)
                {
                    active.Remove(c);
                    leftover -= size[c];
                }
            }
            foreach (var c in active) size[c] = Math.Max(size[c], fr * tracks[c].Fraction);
        }
        free = Math.Max(0, width - gaps - size.Sum());
        var autoTracks = Enumerable.Range(0, columns).Where(c => tracks[c].Kind == GridTrackKind.Auto).ToList();
        // justify-content normal (and stretch) stretches the auto tracks over any space still free.
        double leading = 0, between = 0;
        if (free > Epsilon)
        {
            if (style.JustifyContent == FlexJustify.Normal && autoTracks.Count > 0)
            {
                foreach (var c in autoTracks) size[c] += free / autoTracks.Count;
            }
            else
            {
                (leading, between) = style.JustifyContent switch
                {
                    FlexJustify.End => (free, 0d),
                    FlexJustify.Center => (free / 2, 0d),
                    FlexJustify.SpaceBetween => columns > 1 ? (0d, free / (columns - 1)) : (0d, 0d),
                    FlexJustify.SpaceAround => (free / columns / 2, free / columns),
                    FlexJustify.SpaceEvenly => (free / (columns + 1), free / (columns + 1)),
                    _ => (0d, 0d)
                };
            }
        }
        var starts = new double[columns];
        var x = grid.X + leading;
        for (var c = 0; c < columns; c++)
        {
            starts[c] = x;
            x += size[c] + columnGap + between;
        }

        // Rows: lay each item out in its grid area, then align the row like a flex line.
        var rows = new List<MeasuredFlex>((placed.Count + columns - 1) / columns);
        for (var rowStart = 0; rowStart < placed.Count; rowStart += columns)
        {
            var items = new List<Item>(columns);
            for (var c = 0; c < columns && rowStart + c < placed.Count; c++)
            {
                cancellation.ThrowIfCancellationRequested();
                var item = PlaceGridItem(placed[rowStart + c], starts[c], size[c], style, options, resolved, cancellation);
                LayoutItem(item, options, resolved, usableHeight, stretchedHeight: null, cancellation);
                items.Add(item);
            }
            var (natural, maxAscent) = NaturalCross(items);
            if (natural > usableHeight + Epsilon)
                throw new FactsPdfException("FPDF1302",
                    "A grid row is taller than the usable page height; grid rows are not split across pages.", container.SourceOffset);
            AlignCross(items, natural, maxAscent, options, resolved, usableHeight, cancellation);
            rows.Add(Assemble(items, natural, options, budget, cancellation));
        }
        return rows;
    }

    // Sizes one grid item in its area: justify-self stretch fills the area (an image keeps its own
    // width and starts the area); start, end and center use the fit-content width.
    private static Item PlaceGridItem(BlockChild child, double areaX, double area, FlexStyle container, PdfOptions options,
        Dictionary<(FontRole Role, int Scalar), (PdfFont? Font, double Width)> resolved, CancellationToken cancellation)
    {
        var item = CreateItem(child, area, cancellation);
        item.ContainerWidth = area;
        var box = item.Box;
        var offset = item.SourceOffset;
        var justify = box.Flex.JustifySelf ?? container.JustifyItems;
        item.Align = box.Flex.AlignSelf ?? container.AlignItems;
        if (child is ImageNode image)
        {
            item.Image = ImageLayout.Resolve(image, area);
            item.Edges = item.Image.OuterWidth - item.Image.Width;
            item.Target = item.Image.Width;
            // A replaced item is not stretched by normal alignment.
            if (justify == FlexAlign.Stretch) justify = FlexAlign.Start;
            if (item.Align == FlexAlign.Stretch) item.Align = FlexAlign.Start;
        }
        else
        {
            item.Edges = child is BlockNode
                ? Checked(box.PaddingLeft.Resolve(area), offset) + Checked(box.PaddingRight.Resolve(area), offset)
                    + box.BorderLeft.EffectiveWidth + box.BorderRight.EffectiveWidth
                : 0;
            var sizing = box.BorderBoxSizing ? item.Edges : 0;
            double ContentOf(CssLength length) => Math.Max(0, Checked(length.Resolve(area), offset) - sizing);
            var available = Math.Max(0, area - item.MarginLeft - item.MarginRight - item.Edges);
            double target;
            if (box.Width is { } specified) target = ContentOf(specified);
            else if (justify == FlexAlign.Stretch && !item.AutoLeft && !item.AutoRight) target = available;
            else
            {
                var (min, max) = child is BlockNode node
                    ? Intrinsic.Content(node, options, resolved, cancellation)
                    : Intrinsic.Of(child, options, resolved, cancellation);
                target = Math.Min(max, Math.Max(min, available));
            }
            if (box.MaxWidth is { } upper) target = Math.Min(target, ContentOf(upper));
            if (box.MinWidth is { } lower) target = Math.Max(target, ContentOf(lower));
            item.Target = target;
        }
        // Auto margins take the area's free space, then justify-self places the item.
        var free = area - item.Outer;
        if (free > Epsilon)
        {
            if (item.AutoLeft && item.AutoRight) { item.MarginLeft += free / 2; item.MarginRight += free / 2; free = 0; }
            else if (item.AutoLeft) { item.MarginLeft += free; free = 0; }
            else if (item.AutoRight) { item.MarginRight += free; free = 0; }
        }
        item.X = areaX + (free <= Epsilon ? 0 : justify switch
        {
            FlexAlign.End => free,
            FlexAlign.Center => free / 2,
            _ => 0
        });
        return item;
    }
}
