namespace FactsPDF;

/// <summary>
/// One measured flex line, ready to place at a page cursor. X coordinates are on the page; tops
/// are relative to the line's top; baselines and disc centres are relative PDF coordinates that
/// the caller offsets by <c>PageHeight - lineTop</c>.
/// </summary>
internal sealed record MeasuredFlex(double Height, List<PlacedText> Runs, List<PaintedBox> PaintBoxes,
    List<PlacedImage> Images, List<PlacedDisc> Discs, bool KeepWhole);

/// <summary>
/// Single-line, row-direction flexbox (CSS Flexible Box Layout §9) for report flex containers:
/// flex base sizes from flex-basis, width or max-content, the flexible-length resolution loop with
/// min/max clamping and automatic minimum sizes, auto margins, justify-content, column-gap,
/// align-items/align-self (stretch, start, end, center, baseline) and order. Each item's content
/// is laid out by the ordinary block layout at its resolved width, on one page; a row is placed
/// whole and is never split across pages.
/// </summary>
internal static partial class FlexLayout
{
    private const double Epsilon = 0.000001;

    private sealed class Item
    {
        public required BlockChild Child;
        public required int SourceOffset;
        public required BoxStyle Box;
        public double MarginLeft, MarginRight, MarginTop, MarginBottom;
        public bool AutoLeft, AutoRight;
        public double Edges; // horizontal padding and borders
        public double ContainerWidth; // what the item's percentages resolve against
        public double Base, Hypothetical, Min, Max, Target;
        public bool Frozen;
        public double OuterHeight, Baseline, CrossOffset;
        public double X; // the left edge of the item's margin box on the page
        public FlexAlign Align;
        public bool Unsplittable = true;
        public FragmentOutput? Output;
        public ImageLayout.Geometry Image;
        public double ImageWidth, ImageHeight;
        public double Outer => Target + Edges + MarginLeft + MarginRight;
    }

    private sealed class FragmentOutput
    {
        public required LayoutPage Page;
        public required double PageHeight;
    }

    internal static MeasuredFlex Measure(LayoutFlexLine line, PdfOptions options,
        Dictionary<(FontRole Role, int Scalar), (PdfFont? Font, double Width)> resolved,
        LayoutBudget budget, CancellationToken cancellation)
    {
        var container = line.Container;
        var flex = container.BoxStyle.Flex;
        var width = line.ContentWidth;
        var usableHeight = options.PageHeight - 2 * options.Margin;
        var items = new List<Item>(container.Children.Count);
        foreach (var child in container.Children)
        {
            cancellation.ThrowIfCancellationRequested();
            items.Add(CreateItem(child, width, cancellation));
        }
        // order: a stable sort, so equal orders keep document order.
        var ordered = items.Select((item, index) => (item, index))
            .OrderBy(pair => pair.item.Box.Flex.Order).ThenBy(pair => pair.index).Select(pair => pair.item).ToList();
        var gap = Checked(flex.ColumnGap?.Resolve(width) ?? 0, container.SourceOffset);
        var available = width - gap * Math.Max(0, ordered.Count - 1);

        // Flex base and hypothetical main sizes (content-box widths).
        foreach (var item in ordered)
        {
            cancellation.ThrowIfCancellationRequested();
            SizeItem(item, width, options, resolved, cancellation);
        }
        ResolveFlexibleLengths(ordered, available, cancellation);
        var used = 0d;
        foreach (var item in ordered) used += item.Outer;
        var free = available - used;
        if (free < -0.001)
            throw new FactsPdfException("FPDF1302",
                "Flex items cannot shrink below their minimum content widths to fit the flex container.", container.SourceOffset);
        free = Math.Max(0, free);

        // Main-axis placement: auto margins take the free space first, then justify-content.
        var autoMargins = ordered.Sum(item => (item.AutoLeft ? 1 : 0) + (item.AutoRight ? 1 : 0));
        if (autoMargins > 0 && free > 0)
        {
            var share = free / autoMargins;
            foreach (var item in ordered)
            {
                if (item.AutoLeft) item.MarginLeft = share;
                if (item.AutoRight) item.MarginRight = share;
            }
            free = 0;
        }
        var count = ordered.Count;
        var (leading, between) = flex.JustifyContent switch
        {
            FlexJustify.End => (free, 0d),
            FlexJustify.Center => (free / 2, 0d),
            FlexJustify.SpaceBetween => count > 1 ? (0d, free / (count - 1)) : (0d, 0d),
            FlexJustify.SpaceAround => (free / count / 2, free / count),
            FlexJustify.SpaceEvenly => (free / (count + 1), free / (count + 1)),
            _ => (0d, 0d)
        };

        // Lay every item out at its resolved width to find its height and baseline.
        var x = line.X + leading;
        foreach (var item in ordered)
        {
            cancellation.ThrowIfCancellationRequested();
            item.Align = item.Box.Flex.AlignSelf ?? flex.AlignItems;
            item.X = x;
            x += item.Outer + gap + between;
            LayoutItem(item, options, resolved, usableHeight, stretchedHeight: null, cancellation);
        }

        // Cross size of the line: the tallest item, or the baseline-aligned extent, within the
        // container's definite height or above its minimum height.
        var (natural, maxAscent) = NaturalCross(ordered);
        var containerBox = container.BoxStyle;
        var verticalEdges = line.VerticalEdges;
        double crossSize;
        if (containerBox.Height is { } height)
        {
            crossSize = Math.Max(0, height.Value - (containerBox.BorderBoxSizing ? verticalEdges : 0));
            if (natural > crossSize + Epsilon)
                throw new FactsPdfException("FPDF1302",
                    "Flex items are taller than their fixed-height flex container.", container.SourceOffset);
        }
        else
        {
            var minimum = containerBox.MinHeight is { } min
                ? Math.Max(0, min.Value - (containerBox.BorderBoxSizing ? verticalEdges : 0)) : 0;
            crossSize = Math.Max(natural, minimum);
        }
        if (crossSize > usableHeight + Epsilon)
            throw new FactsPdfException("FPDF1302",
                "A flex row is taller than the usable page height; flex rows are not split across pages.", container.SourceOffset);
        AlignCross(ordered, crossSize, maxAscent, options, resolved, usableHeight, cancellation);
        return Assemble(ordered, crossSize, options, budget, cancellation);
    }

    // The line's natural cross size: its tallest item, or the extent of its baseline-aligned
    // items, and the largest baseline among them.
    private static (double Natural, double MaxAscent) NaturalCross(List<Item> items)
    {
        double natural = 0, maxAscent = 0, maxDescent = 0;
        foreach (var item in items)
        {
            natural = Math.Max(natural, item.OuterHeight);
            if (item.Align != FlexAlign.Baseline) continue;
            maxAscent = Math.Max(maxAscent, item.Baseline);
            maxDescent = Math.Max(maxDescent, item.OuterHeight - item.Baseline);
        }
        return (Math.Max(natural, maxAscent + maxDescent), maxAscent);
    }

    // Cross-axis placement within the line; a stretched item is laid out again at the line's
    // height so its own box (and any nested flex line) takes the definite size.
    private static void AlignCross(List<Item> items, double crossSize, double maxAscent, PdfOptions options,
        Dictionary<(FontRole Role, int Scalar), (PdfFont? Font, double Width)> resolved,
        double usableHeight, CancellationToken cancellation)
    {
        foreach (var item in items)
        {
            cancellation.ThrowIfCancellationRequested();
            switch (item.Align)
            {
                case FlexAlign.Stretch:
                    if (crossSize - item.OuterHeight > Epsilon && CanStretch(item))
                        LayoutItem(item, options, resolved, usableHeight,
                            crossSize - item.MarginTop - item.MarginBottom, cancellation);
                    break;
                case FlexAlign.End: item.CrossOffset = crossSize - item.OuterHeight; break;
                case FlexAlign.Center: item.CrossOffset = (crossSize - item.OuterHeight) / 2; break;
                case FlexAlign.Baseline: item.CrossOffset = maxAscent - item.Baseline; break;
            }
        }
    }

    // Assembles a line of laid-out items at their X positions and charges its display commands,
    // item by item.
    private static MeasuredFlex Assemble(List<Item> items, double crossSize, PdfOptions options,
        LayoutBudget budget, CancellationToken cancellation)
    {
        var runs = new List<PlacedText>();
        var boxes = new List<PaintedBox>();
        var images = new List<PlacedImage>();
        var discs = new List<PlacedDisc>();
        var keepWhole = true;
        foreach (var item in items)
        {
            cancellation.ThrowIfCancellationRequested();
            keepWhole &= item.Unsplittable;
            var itemX = item.X + item.MarginLeft;
            if (item.Child is ImageNode imageNode)
            {
                var top = item.CrossOffset + item.MarginTop;
                var box = new PaintedBox(itemX, top, item.ImageWidth + item.Edges,
                    item.ImageHeight + item.Image.OuterHeight - item.Image.Height,
                    imageNode.BoxStyle, imageNode.Style.Color, 0, SourceOffset: imageNode.SourceOffset);
                budget.Charge(1 + box.CommandCount, imageNode.SourceOffset, options);
                if (box.CommandCount > 0) boxes.Add(box);
                images.Add(new(imageNode.Image, itemX + item.Image.Left, top + item.Image.Top,
                    item.ImageWidth, item.ImageHeight));
            }
            else if (item.Output is { } output)
            {
                var page = output.Page;
                var commands = page.Discs.Count + page.Images.Count;
                foreach (var run in page.Runs)
                    commands += 1 + TextDecorationCommands.Count(run.Style.Decoration) + (run.Style.LinkUri is null ? 0 : 1);
                foreach (var box in page.PaintBoxes) commands += box.CommandCount;
                budget.Charge(commands, item.SourceOffset, options);
                // The fragment was laid out with the item's left margin at x = 0 and its top at y = 0.
                var dx = item.X;
                var dy = item.CrossOffset;
                foreach (var run in page.Runs)
                    runs.Add(run with { X = run.X + dx, Baseline = run.Baseline - output.PageHeight - dy });
                foreach (var box in page.PaintBoxes.OrderBy(box => box.Order))
                    boxes.Add(box with { X = box.X + dx, Top = box.Top + dy });
                foreach (var image in page.Images)
                    images.Add(image with { X = image.X + dx, Top = image.Top + dy });
                foreach (var disc in page.Discs)
                    discs.Add(disc with { X = disc.X + dx, Y = disc.Y - output.PageHeight - dy });
            }
        }
        return new(crossSize, runs, boxes, images, discs, keepWhole);
    }

    private static Item CreateItem(BlockChild child, double width, CancellationToken cancellation)
    {
        switch (child)
        {
            case Paragraph paragraph:
                return new Item { Child = paragraph, SourceOffset = paragraph.SourceOffset, Box = BoxStyle.Initial };
            case ImageNode image:
            {
                var item = new Item { Child = image, SourceOffset = image.SourceOffset, Box = image.BoxStyle };
                return item;
            }
            case BlockNode node:
            {
                var box = node.BoxStyle;
                if (box.MarginTop.IsPercent || box.MarginBottom.IsPercent ||
                    node.Children.Count == 1 && node.Children[0] is Paragraph { Style: var text } &&
                    (text.PercentMarginBefore is not null || text.PercentMarginAfter is not null))
                    throw new FactsPdfException("FPDF1201",
                        "Percentage vertical margins are not supported on flex items.", node.SourceOffset);
                var item = new Item
                {
                    Child = node, SourceOffset = node.SourceOffset, Box = box,
                    AutoLeft = box.MarginLeftAuto, AutoRight = box.MarginRightAuto,
                    MarginLeft = box.MarginLeftAuto ? 0 : Checked(box.MarginLeft.Resolve(width), node.SourceOffset),
                    MarginRight = box.MarginRightAuto ? 0 : Checked(box.MarginRight.Resolve(width), node.SourceOffset),
                };
                // Paragraph and heading margins are held in their text style.
                if (ParagraphTag(node.Name) && node.Children.Count == 1 && node.Children[0] is Paragraph content)
                {
                    item.MarginTop = content.Style.MarginBefore;
                    item.MarginBottom = content.Style.MarginAfter;
                }
                else
                {
                    item.MarginTop = Checked(box.MarginTop.Value, node.SourceOffset);
                    item.MarginBottom = Checked(box.MarginBottom.Value, node.SourceOffset);
                }
                cancellation.ThrowIfCancellationRequested();
                return item;
            }
            case TableNode table:
                throw new FactsPdfException("FPDF1201",
                    "A table cannot be a flex item; wrap it in a report panel with an explicit width.", table.SourceOffset);
            default:
                throw new InvalidOperationException("Unexpected flex item.");
        }
    }

    private static bool ParagraphTag(string name) => name is "p" or "pre" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6";

    // Flex base size, hypothetical main size and min/max main sizes, all content-box widths.
    private static void SizeItem(Item item, double width, PdfOptions options,
        Dictionary<(FontRole Role, int Scalar), (PdfFont? Font, double Width)> resolved, CancellationToken cancellation)
    {
        var box = item.Box;
        var offset = item.SourceOffset;
        var flex = box.Flex;
        item.ContainerWidth = width;
        double minContent, maxContent;
        if (item.Child is ImageNode image)
        {
            // A replaced item: its natural size within the container, after its own width,
            // min-width and max-width; percentages use the flex container's width.
            item.Image = ImageLayout.Resolve(image, width);
            item.Edges = item.Image.OuterWidth - item.Image.Width;
            minContent = maxContent = item.Image.Width;
        }
        else
        {
            item.Edges = item.Child is BlockNode
                ? Checked(box.PaddingLeft.Resolve(width), offset) + Checked(box.PaddingRight.Resolve(width), offset)
                    + box.BorderLeft.EffectiveWidth + box.BorderRight.EffectiveWidth
                : 0;
            (minContent, maxContent) = (-1, -1);
        }
        var sizing = box.BorderBoxSizing ? item.Edges : 0;
        double ContentOf(CssLength length) => Math.Max(0, Checked(length.Resolve(width), offset) - sizing);
        (double Min, double Max) Content()
        {
            if (minContent < 0)
            {
                var sizes = item.Child is BlockNode node
                    ? ContentSizes(node, options, resolved, cancellation)
                    : Intrinsic.Of(item.Child, options, resolved, cancellation);
                if (double.IsNaN(sizes.Max))
                    throw new FactsPdfException("FPDF1201",
                        "A table cannot size a flex item from its content; give the item an explicit width or flex-basis.", offset);
                (minContent, maxContent) = sizes;
            }
            return (minContent, maxContent);
        }
        var specifiedWidth = item.Child is ImageNode ? (double?)null
            : box.Width is { } w ? ContentOf(w) : null;
        // flex-basis, else a definite width, else the max-content size.
        item.Base = flex.Basis is { } basis && !flex.BasisContent ? ContentOf(basis)
            : !flex.BasisContent && specifiedWidth is { } specified ? specified
            : Content().Max;
        item.Max = box.MaxWidth is { } max ? ContentOf(max) : double.PositiveInfinity;
        if (box.MinWidth is { } min) item.Min = ContentOf(min);
        else
        {
            // The automatic minimum size: the content size suggestion (min-content), or the
            // specified width when smaller, never above the maximum.
            var suggestion = Content().Min;
            if (specifiedWidth is { } specifiedMin) suggestion = Math.Min(suggestion, specifiedMin);
            item.Min = Math.Min(suggestion, item.Max);
        }
        item.Hypothetical = Math.Max(item.Min, Math.Min(item.Max, item.Base));
    }

    // CSS Flexbox §9.7, on outer sizes: freeze inflexible items, then repeatedly distribute the
    // remaining free space by grow factors or scaled shrink factors and freeze clamped items.
    private static void ResolveFlexibleLengths(List<Item> items, double available, CancellationToken cancellation)
    {
        var hypothetical = 0d;
        foreach (var item in items) hypothetical += item.Hypothetical + item.Edges + item.MarginLeft + item.MarginRight;
        var grow = hypothetical < available;
        foreach (var item in items)
        {
            var factor = grow ? item.Box.Flex.Grow : item.Box.Flex.Shrink;
            item.Frozen = factor == 0 || (grow ? item.Base > item.Hypothetical : item.Base < item.Hypothetical);
            item.Target = item.Frozen ? item.Hypothetical : item.Base;
        }
        double FreeSpace()
        {
            var sum = 0d;
            foreach (var item in items)
                sum += (item.Frozen ? item.Target : item.Base) + item.Edges + item.MarginLeft + item.MarginRight;
            return available - sum;
        }
        var initialFree = FreeSpace();
        for (var round = 0; round <= items.Count; round++)
        {
            cancellation.ThrowIfCancellationRequested();
            if (items.All(item => item.Frozen)) break;
            var remaining = FreeSpace();
            var factors = items.Where(item => !item.Frozen)
                .Sum(item => grow ? item.Box.Flex.Grow : item.Box.Flex.Shrink);
            if (factors < 1 && Math.Abs(initialFree * factors) < Math.Abs(remaining))
                remaining = initialFree * factors;
            if (grow)
            {
                foreach (var item in items.Where(item => !item.Frozen))
                    item.Target = item.Base + (factors > 0 ? remaining * item.Box.Flex.Grow / factors : 0);
            }
            else
            {
                var scaled = items.Where(item => !item.Frozen).Sum(item => item.Box.Flex.Shrink * item.Base);
                foreach (var item in items.Where(item => !item.Frozen))
                    item.Target = item.Base - (scaled > 0 ? Math.Abs(remaining) * item.Box.Flex.Shrink * item.Base / scaled : 0);
            }
            var violation = 0d;
            foreach (var item in items.Where(item => !item.Frozen))
            {
                var clamped = Math.Max(Math.Max(0, item.Min), Math.Min(item.Max, item.Target));
                violation += clamped - item.Target;
                item.Hypothetical = clamped; // reused as the clamped target of this round
            }
            foreach (var item in items.Where(item => !item.Frozen))
            {
                var minViolation = item.Hypothetical > item.Target + Epsilon;
                var maxViolation = item.Hypothetical < item.Target - Epsilon;
                if (Math.Abs(violation) <= Epsilon || violation > 0 && minViolation || violation < 0 && maxViolation)
                    item.Frozen = true;
                item.Target = item.Hypothetical;
            }
        }
    }

    private static bool CanStretch(Item item) =>
        item.Child is ImageNode image ? image.BoxStyle.Height is null
        : item.Child is BlockNode node && node.BoxStyle.Height is null;

    // Lays the item out at its resolved width (and, when stretched, at a definite outer height
    // without margins) and records its outer height, first baseline and output.
    private static void LayoutItem(Item item, PdfOptions options,
        Dictionary<(FontRole Role, int Scalar), (PdfFont? Font, double Width)> resolved,
        double usableHeight, double? stretchedHeight, CancellationToken cancellation)
    {
        if (item.Child is ImageNode image)
        {
            var geometry = item.Image;
            var verticalEdges = geometry.OuterHeight - geometry.Height;
            item.ImageWidth = item.Target;
            item.ImageHeight = stretchedHeight is { } stretched ? Math.Max(0, stretched - verticalEdges)
                : image.BoxStyle.Height is not null || Math.Abs(item.Target - geometry.Width) <= Epsilon ? geometry.Height
                : item.Target * image.Image.PixelHeight / image.Image.PixelWidth;
            if (item.ImageWidth <= 0 || item.ImageHeight <= 0)
                throw new FactsPdfException("FPDF1302", "A flex item image needs a positive width and height.", image.SourceOffset);
            item.OuterHeight = item.ImageHeight + verticalEdges;
            // An image has no baseline: synthesize it from the bottom of its border box.
            item.Baseline = item.OuterHeight;
            return;
        }
        var outerWidth = item.Outer;
        var fragment = new DocumentRoot { HasLists = true };
        BlockNode? node = null;
        if (item.Child is BlockNode original)
        {
            var box = original.BoxStyle;
            var percentBasis = item.ContainerWidth;
            var sized = box with
            {
                Width = new CssLength(item.Target),
                BorderBoxSizing = false,
                MinWidth = null,
                MaxWidth = null,
                MarginLeft = new CssLength(item.MarginLeft),
                MarginRight = new CssLength(item.MarginRight),
                MarginLeftAuto = false,
                MarginRightAuto = false,
                PaddingLeft = new CssLength(box.PaddingLeft.Resolve(percentBasis)),
                PaddingRight = new CssLength(box.PaddingRight.Resolve(percentBasis)),
                PaddingTop = new CssLength(box.PaddingTop.Resolve(percentBasis)),
                PaddingBottom = new CssLength(box.PaddingBottom.Resolve(percentBasis)),
                Height = stretchedHeight is { } stretched
                    ? new CssLength(Math.Max(0, stretched - BorderAndPaddingHeight(box, percentBasis)))
                    : box.Height,
            };
            node = new BlockNode(original.Name, original.Style, sized, original.SourceOffset);
            foreach (var child in original.Children) node.Add(child);
            fragment.Children.Add(node);
        }
        else fragment.Children.Add(item.Child);
        var fragmentOptions = options with
        {
            PageWidth = outerWidth,
            PageHeight = usableHeight,
            Margin = 0,
            MaxPages = 1,
            // Charged once the line is assembled, so a stretched item is not charged twice.
            MaxDisplayCommands = int.MaxValue,
        };
        var end = new FragmentEnd();
        List<LayoutPage> pages;
        try
        {
            pages = TextLayout.LayoutFragment(fragment, fragmentOptions, resolved, new LayoutBudget(), end, cancellation);
        }
        catch (FactsPdfException error) when (error.Code == "FPDF1303")
        {
            throw new FactsPdfException("FPDF1302",
                "A flex item is taller than the usable page height; flex rows are not split across pages.", item.SourceOffset);
        }
        var page = pages[0];
        item.Output = new FragmentOutput { Page = page, PageHeight = usableHeight };
        item.OuterHeight = node is null || ParagraphTag(node.Name)
            ? end.EndY
            : Math.Max(0, end.RootBlockEnd) + item.MarginBottom;
        // The first baseline: the topmost text baseline, else the bottom of the border box.
        var first = double.PositiveInfinity;
        var baselines = new HashSet<double>();
        foreach (var run in page.Runs)
        {
            var below = usableHeight - run.Baseline;
            first = Math.Min(first, below);
            baselines.Add(Math.Round(below, 6));
        }
        item.Baseline = double.IsPositiveInfinity(first) ? item.OuterHeight - item.MarginBottom : first;
        // A browser keeps a single paragraph of up to three lines together (orphans and widows of
        // two) and never breaks inside an image; anything more could be split between pages.
        item.Unsplittable = Leaves(item.Child, cancellation) <= 1 && baselines.Count <= 3;
    }

    private static double BorderAndPaddingHeight(BoxStyle box, double width) =>
        box.PaddingTop.Resolve(width) + box.PaddingBottom.Resolve(width)
        + box.BorderTop.EffectiveWidth + box.BorderBottom.EffectiveWidth;

    private static int Leaves(BlockChild child, CancellationToken cancellation)
    {
        var count = 0;
        var pending = new Stack<BlockChild>();
        pending.Push(child);
        while (pending.Count > 0)
        {
            cancellation.ThrowIfCancellationRequested();
            switch (pending.Pop())
            {
                case Paragraph paragraph:
                    if (TextLines.HasTextContent(paragraph, cancellation) || paragraph.Runs.HasBreak()) count++;
                    break;
                case BlockNode node:
                    foreach (var nested in node.Children) pending.Push(nested);
                    break;
                default:
                    count++;
                    break;
            }
        }
        return count;
    }

    // The content-box min-content and max-content widths of a block's children.
    internal static (double Min, double Max) ContentSizes(BlockNode node, PdfOptions options,
        Dictionary<(FontRole Role, int Scalar), (PdfFont? Font, double Width)> resolved, CancellationToken cancellation)
        => Intrinsic.Content(node, options, resolved, cancellation);

    /// <summary>
    /// html{display:flex} makes body its only flex item. A browser then sizes body to its
    /// max-content width, capped at the page: when that fills the page the layout is exactly the
    /// block layout and every flex property places nothing. A narrower body would be centred or
    /// shrunk, which is not implemented, so it fails.
    /// </summary>
    internal static void CheckRootFlexNoOp(DocumentRoot root, PdfOptions options, CancellationToken cancellation)
    {
        var width = options.PageWidth - 2 * options.Margin;
        if (root.Children.Count != 1 || root.Children[0] is not BlockNode { Name: "body" } body)
            throw new FactsPdfException("FPDF1201",
                "html display:flex is supported only with a single body element as its flex item.", root.FlexRootOffset);
        if (body.BoxStyle.Width is { } specified && specified.Resolve(width) >= width - 0.001)
            return;
        var resolved = new Dictionary<(FontRole Role, int Scalar), (PdfFont? Font, double Width)>();
        var box = BlockLayout.ResolveBox(body.BoxStyle, width, body.SourceOffset, width);
        var edges = box.OuterWidth - box.Inner;
        foreach (var child in body.Children)
        {
            cancellation.ThrowIfCancellationRequested();
            var (_, max) = Intrinsic.Of(child, options, resolved, cancellation);
            if (!double.IsNaN(max) && max + edges >= width - 0.001) return;
        }
        throw new FactsPdfException("FPDF1201",
            "html display:flex would shrink a body narrower than the page to its content and centre it; it is accepted only when the body fills the page width.",
            root.FlexRootOffset);
    }

    private static double Checked(double value, int offset)
    {
        if (!double.IsFinite(value) || value < 0 || value > 14_400)
            throw new FactsPdfException("FPDF1302", "Resolved flex length exceeds supported bounds.", offset);
        return value;
    }

    /// <summary>
    /// Min-content and max-content contributions (margin boxes) of block children, measured with
    /// the glyph widths the line wrapper uses. Percentages of an indefinite size count as auto (and
    /// percentage insets as zero), as browsers do when sizing from content. A table has no content
    /// size here (NaN).
    /// </summary>
    internal static class Intrinsic
    {
        internal static (double Min, double Max) Of(BlockChild child, PdfOptions options,
            Dictionary<(FontRole Role, int Scalar), (PdfFont? Font, double Width)> resolved, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            switch (child)
            {
                case Paragraph paragraph:
                    return TextLines.IntrinsicWidths(paragraph, options, resolved, cancellation);
                case ImageNode image:
                {
                    var style = image.BoxStyle;
                    var percent = style.Width is { IsPercent: true } || style.MaxWidth is { IsPercent: true }
                        || style.MinWidth is { IsPercent: true };
                    if (!percent)
                    {
                        var geometry = ImageLayout.Resolve(image, 14_400);
                        return (geometry.OuterWidth, geometry.OuterWidth);
                    }
                    var edges = style.PaddingLeft.Value * (style.PaddingLeft.IsPercent ? 0 : 1)
                        + style.PaddingRight.Value * (style.PaddingRight.IsPercent ? 0 : 1)
                        + style.BorderLeft.EffectiveWidth + style.BorderRight.EffectiveWidth;
                    var natural = image.Image.PixelWidth * 0.75 + edges;
                    // A percentage width or max-width makes a replaced box compressible: its
                    // min-content contribution is zero.
                    var compressible = style.Width is { IsPercent: true } || style.MaxWidth is { IsPercent: true };
                    return (compressible ? 0 : natural, natural);
                }
                case BlockNode node:
                {
                    var style = node.BoxStyle;
                    double Fixed(CssLength length) => length.IsPercent ? 0 : length.Value;
                    var padding = Fixed(style.PaddingLeft) + Fixed(style.PaddingRight)
                        + style.BorderLeft.EffectiveWidth + style.BorderRight.EffectiveWidth;
                    var margins = (style.MarginLeftAuto ? 0 : Fixed(style.MarginLeft))
                        + (style.MarginRightAuto ? 0 : Fixed(style.MarginRight));
                    var sizing = style.BorderBoxSizing ? padding : 0;
                    double min, max;
                    if (style.Width is { IsPercent: false } width)
                        min = max = Math.Max(0, width.Value - sizing);
                    else
                    {
                        (min, max) = Content(node, options, resolved, cancellation);
                        if (double.IsNaN(max)) return (double.NaN, double.NaN);
                    }
                    if (style.MaxWidth is { IsPercent: false } upper)
                    {
                        min = Math.Min(min, Math.Max(0, upper.Value - sizing));
                        max = Math.Min(max, Math.Max(0, upper.Value - sizing));
                    }
                    if (style.MinWidth is { IsPercent: false } lower)
                    {
                        min = Math.Max(min, Math.Max(0, lower.Value - sizing));
                        max = Math.Max(max, Math.Max(0, lower.Value - sizing));
                    }
                    return (min + padding + margins, max + padding + margins);
                }
                default:
                    return (double.NaN, double.NaN);
            }
        }

        internal static (double Min, double Max) Content(BlockNode node, PdfOptions options,
            Dictionary<(FontRole Role, int Scalar), (PdfFont? Font, double Width)> resolved, CancellationToken cancellation)
        {
            double min = 0, max = 0;
            var gutter = 0d;
            if (node.Name is "ol" or "ul" or "menu" && node.Children.Count > 0)
            {
                var list = new DocumentRoot { HasLists = true };
                list.Children.Add(node);
                gutter = ListLayout.Prepare(list, options, cancellation)?.Gutter(node) ?? 0;
            }
            if (node.BoxStyle.IsGridContainer)
            {
                // A grid is as wide as its columns: fixed tracks at their length (percentages of
                // an indefinite width count as auto), other tracks at their items' contributions.
                var tracks = node.BoxStyle.Flex.GridColumns ?? [new GridTrack(GridTrackKind.Auto)];
                var columnMin = new double[tracks.Length];
                var columnMax = new double[tracks.Length];
                for (var i = 0; i < node.Children.Count; i++)
                {
                    var (childMin, childMax) = Of(node.Children[i], options, resolved, cancellation);
                    if (double.IsNaN(childMax)) return (double.NaN, double.NaN);
                    var column = i % tracks.Length;
                    columnMin[column] = Math.Max(columnMin[column], childMin);
                    columnMax[column] = Math.Max(columnMax[column], childMax);
                }
                var columnGap = node.BoxStyle.Flex.ColumnGap is { IsPercent: false } fixedColumnGap ? fixedColumnGap.Value : 0;
                for (var c = 0; c < tracks.Length; c++)
                {
                    var fixedTrack = tracks[c].Kind == GridTrackKind.Fixed && !tracks[c].Length.IsPercent;
                    min += (fixedTrack ? tracks[c].Length.Value : columnMin[c]) + (c > 0 ? columnGap : 0);
                    max += (fixedTrack ? tracks[c].Length.Value : columnMax[c]) + (c > 0 ? columnGap : 0);
                }
                return (min, max);
            }
            if (node.BoxStyle.IsFlexContainer)
            {
                // A single-line flex container is as wide as its items side by side.
                var gap = node.BoxStyle.Flex.ColumnGap is { IsPercent: false } fixedGap ? fixedGap.Value : 0;
                for (var i = 0; i < node.Children.Count; i++)
                {
                    var (childMin, childMax) = Of(node.Children[i], options, resolved, cancellation);
                    if (double.IsNaN(childMax)) return (double.NaN, double.NaN);
                    var spacing = i > 0 ? gap : 0;
                    min += childMin + spacing;
                    max += childMax + spacing;
                }
                return (min, max);
            }
            foreach (var child in node.Children)
            {
                var (childMin, childMax) = Of(child, options, resolved, cancellation);
                if (double.IsNaN(childMax)) return (double.NaN, double.NaN);
                var inset = child is BlockNode { Name: "li" } item && !item.BoxStyle.MarkerInside ? gutter : 0;
                min = Math.Max(min, childMin + inset);
                max = Math.Max(max, childMax + inset);
            }
            return (min, max);
        }
    }
}
