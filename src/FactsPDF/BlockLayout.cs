namespace FactsPDF;

internal abstract record BlockLayoutStep;
internal sealed record BeginBlock(double PaddingTop, BoxStyle Style, double X,
    double OuterWidth, Rgb TextColor, int SourceOffset, bool IsParagraph,
    double MarginTop = 0, double RequiredRuleHeight = 0,
    double MinimumOuterHeight = 0, double TrailingSpace = 0,
    double? FixedOuterHeight = null,
    double? IndivisibleEmptyClosingMargin = null,
    double DeferredBlankParagraphMargin = 0) : BlockLayoutStep;
internal sealed record EndBlock(double PaddingBottom, double MarginBottom = 0) : BlockLayoutStep;
internal sealed record LayoutParagraph(Paragraph Paragraph, double ContentX, double ContentWidth,
    ListMarker? Marker = null, double MarkerRightX = 0) : BlockLayoutStep;
internal sealed record LayoutImage(ImageNode Node, double X, double ContentX, double ContentTop,
    double Width, double Height, double OuterWidth, double OuterHeight) : BlockLayoutStep;
internal sealed record LayoutHeadingGroup(IReadOnlyList<BlockChild> Children, int Start, int End,
    double ContentWidth) : BlockLayoutStep;
internal sealed record LayoutTable(TableNode Node, double X, double AvailableWidth) : BlockLayoutStep;
internal sealed record LayoutKeepPanel(BlockNode Node, double ContentWidth,
    double Top, double Bottom, double MarginTop, double MarginBottom) : BlockLayoutStep;

/// <summary>
/// Iterative, lazy containing-block resolution. All positions are PDF points;
/// percentages (even vertical padding) use the parent content-box width.
/// M8 adds per-page box fragments with sliced background and side borders.
/// </summary>
internal static class BlockLayout
{
    private readonly record struct Frame(IReadOnlyList<BlockChild> Children, int Index, double X,
        double ContentWidth, double Bottom, bool IsRoot, double Gutter = 0, double MarkerGap = 0,
        double MarkerRightX = 0, int KeepUntil = 0, double MarginBottom = 0, bool IsDocumentBody = false,
        double? ParagraphMarginBasis = null);

    internal static IEnumerable<BlockLayoutStep> Steps(DocumentRoot root, PdfOptions options,
        CancellationToken cancellation, ListLayout? lists = null)
    {
        ArgumentNullException.ThrowIfNull(root);
        var frames = new Stack<Frame>();
        frames.Push(new(root.Children, 0, options.Margin, options.PageWidth - 2 * options.Margin, 0, true));
        while (frames.Count > 0)
        {
            cancellation.ThrowIfCancellationRequested();
            var frame = frames.Pop();
            if (frame.Index == frame.Children.Count)
            {
                if (!frame.IsRoot) yield return new EndBlock(frame.Bottom, frame.MarginBottom);
                continue;
            }
            if (frame.Index >= frame.KeepUntil && frame.Children[frame.Index] is BlockNode candidate &&
                HeadingKeep.IsHeading(candidate.Name) && candidate.Style.KeepWithNext)
            {
                var (end, valid) = HeadingKeep.Find(frame.Children, frame.Index, cancellation);
                frame = frame with { KeepUntil = end };
                if (valid) yield return new LayoutHeadingGroup(frame.Children, frame.Index, end, frame.ContentWidth);
            }
            frames.Push(frame with { Index = frame.Index + 1 });
            switch (frame.Children[frame.Index])
            {
                case TableNode table:
                    yield return new LayoutTable(table, frame.X, frame.ContentWidth);
                    break;
                case ImageNode image:
                {
                    var geometry = ImageLayout.Resolve(image, frame.ContentWidth);
                    yield return new LayoutImage(image, frame.X, frame.X + geometry.Left, geometry.Top,
                        geometry.Width, geometry.Height, geometry.OuterWidth, geometry.OuterHeight);
                    break;
                }
                case ParagraphNode paragraph:
                    yield return new LayoutParagraph(
                        ResolveParagraphMargins(paragraph.Paragraph, frame.ParagraphMarginBasis ?? frame.ContentWidth, options),
                        frame.X, frame.ContentWidth,
                        lists?.Marker(paragraph.Paragraph), frame.MarkerRightX);
                    break;
                case BlockNode node:
                {
                    var style = node.BoxStyle;
                    var gutter = node.Name == "li" && !style.MarkerInside ? frame.Gutter : 0;
                    var containing = frame.ContentWidth - gutter;
                    if (containing < -0.000001)
                        throw new FactsPdfException("FPDF1302", "List marker gutter exceeds containing content width.", node.SourceOffset);
                    containing = Math.Max(0, containing);
                    var (marginLeft, _, width) = ResolveMargins(style, containing, node.SourceOffset);
                    var x = frame.X + gutter + marginLeft;
                    var markerRight = node.Name == "li" ? frame.X + gutter - frame.MarkerGap : frame.MarkerRightX;
                    var box = ResolveBox(style, width, node.SourceOffset, containing);
                    // Margins live outside the painted box. They advance the
                    // vertical cursor once, unlike continuation-page padding.
                    var marginTop = Checked(style.MarginTop.Resolve(containing), node.SourceOffset);
                    var marginBottom = Checked(style.MarginBottom.Resolve(containing), node.SourceOffset);
                    // A percentage outside margin is indivisible: even a
                    // multi-page panel cannot place one margin taller than
                    // a fresh page. Reject before deferred empty spacing can
                    // be discarded when the next paragraph starts a new page.
                    var usablePageHeight = options.PageHeight - 2 * options.Margin;
                    if ((style.MarginTop.IsPercent && marginTop > usablePageHeight + 0.000001) ||
                        (style.MarginBottom.IsPercent && marginBottom > usablePageHeight + 0.000001))
                        throw new FactsPdfException("FPDF1302",
                            "Percentage outside margin cannot fit on a usable PDF page.", node.SourceOffset);
                    // Empty report placeholders may contain explicit, zero-height
                    // <p></p> slots. They still have no fragment in which to
                    // split an outside percentage margin; do not silently drop
                    // the closing gap when their successor starts a new page.
                    // A paragraph with text, breaks, authored spacing or paint
                    // is not an empty slot and retains normal layout.
                    // Even content-free nested wrappers can reserve real vertical
                    // padding. Measure their mandatory geometry before a blank
                    // percentage-margin panel chooses its page, rather than
                    // dropping its closing gap when a following line paginates.
                    var blankNestedHeight = 0d;
                    var blankParagraphMargin = 0d;
                    var blankNested = (style.MarginTop.IsPercent || style.MarginBottom.IsPercent) &&
                        TryMeasureUnpaintedBlankContents(node, box.Inner, cancellation,
                            out blankNestedHeight, out blankParagraphMargin);
                    var noOccupiedChildren = node.Children.Count == 0 ||
                        node.Children.All(child => child is BlockNode paragraph &&
                            paragraph.Name == "p" && paragraph.Children.Count == 1 &&
                            paragraph.Children[0] is ParagraphNode content &&
                            content.Paragraph.Runs.Count == 0 &&
                            HasNoParagraphOutsideMargin(paragraph.Style) &&
                            !paragraph.Style.BreakBefore && !paragraph.Style.BreakAfter &&
                            !paragraph.BoxStyle.BreakBefore && !paragraph.BoxStyle.BreakAfter &&
                            !paragraph.BoxStyle.HasLayoutPaint &&
                            paragraph.BoxStyle.PaddingTop.Value == 0 &&
                            paragraph.BoxStyle.PaddingBottom.Value == 0 &&
                            paragraph.BoxStyle.MinHeight is null &&
                            paragraph.BoxStyle.Height is null) ||
                        blankNested;
                    // Border/padding and blank nested padding belong to the
                    // same indivisible footprint as the closing outside margin.
                    var emptyOuterHeight = box.Top + box.Bottom +
                        (blankNested ? blankNestedHeight : 0);
                    var requiredEmptyPaintHeight = node.Name == "hr" ||
                        (noOccupiedChildren && (style.HasLayoutPaint || blankNestedHeight > 0))
                        ? emptyOuterHeight : 0;
                    // min-height is genuine reserved page space. Content-box
                    // adds vertical decoration; border-box counts it inside.
                    var minimumOuterHeight = style.MinHeight is { } minimum
                        ? (style.BorderBoxSizing
                            ? Math.Max(minimum.Value, box.Top + box.Bottom)
                            : minimum.Value + box.Top + box.Bottom)
                        : 0;
                    if (!double.IsFinite(minimumOuterHeight) || minimumOuterHeight > 14_400)
                        throw new FactsPdfException("FPDF1302",
                            "Computed minimum panel height exceeds supported bounds.", node.SourceOffset);
                    // Fixed-height panels must fit on one page with no clipping.
                    double? fixedOuterHeight = style.Height is { } fixedHeight
                        ? (style.BorderBoxSizing
                            ? Math.Max(fixedHeight.Value, box.Top + box.Bottom)
                            : fixedHeight.Value + box.Top + box.Bottom)
                        : null;
                    if (!double.IsFinite(fixedOuterHeight ?? 0) || fixedOuterHeight > 14_400)
                        throw new FactsPdfException("FPDF1302",
                            "Computed fixed panel height exceeds supported bounds.", node.SourceOffset);
                    // Empty blocks (including hr) have no content fragment
                    // over which their percentage outside margins can split.
                    // Preflight both margins and the full empty box together,
                    // including when they fit a fresh page but not the
                    // remaining space of the current page.
                    double? indivisibleClosingMargin = noOccupiedChildren &&
                        (style.MarginTop.IsPercent || style.MarginBottom.IsPercent)
                        ? marginBottom : null;
                    if (indivisibleClosingMargin.HasValue &&
                        marginTop + Math.Max(emptyOuterHeight,
                            Math.Max(minimumOuterHeight, fixedOuterHeight ?? 0)) + marginBottom
                            > usablePageHeight + 0.000001)
                        throw new FactsPdfException("FPDF1302",
                            "Empty report box and percentage outside margins cannot fit on a page.", node.SourceOffset);
                    if (style.AvoidBreakInside)
                    {
                        // Only a root-level flat text card has the bounded,
                        // measurable footprint promised by break-inside:avoid.
                        // Reject unsupported nested/forced/sized contents instead
                        // of claiming an arbitrary block can be kept together.
                        if ((!frame.IsRoot && !frame.IsDocumentBody) || style.MinHeight.HasValue || style.Height.HasValue ||
                            node.Children.Count is < 1 or > 8)
                            throw new FactsPdfException("FPDF1201",
                                "Kept report card requires one to eight direct text paragraphs and no fixed/minimum height.",
                                node.SourceOffset);
                        foreach (var child in node.Children)
                        {
                            if (child is not BlockNode item ||
                                item.Name is not ("p" or "pre" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6") ||
                                item.Children.Count != 1 ||
                                item.Children[0] is not ParagraphNode paragraph ||
                                item.Style.BreakBefore || item.Style.BreakAfter ||
                                paragraph.Paragraph.Style.BreakBefore || paragraph.Paragraph.Style.BreakAfter ||
                                !TextLines.HasTextContent(paragraph.Paragraph, cancellation))
                                throw new FactsPdfException("FPDF1201",
                                    "Kept report card supports only nonempty, direct flat paragraphs/headings without inner page breaks.",
                                    node.SourceOffset);
                        }
                        yield return new LayoutKeepPanel(node, box.Inner, box.Top, box.Bottom, marginTop, marginBottom);
                    }
                    yield return new BeginBlock(box.Top, style, x,
                        box.OuterWidth, node.Style.Color, node.SourceOffset,
                        node.Name is "p" or "pre" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6",
                        marginTop,
                        // A blank wrapper's padding and sized leaves occupy
                        // the current page too; reserve that measured height
                        // with the parent's opening and closing outside margins.
                        indivisibleClosingMargin.HasValue ? emptyOuterHeight : requiredEmptyPaintHeight,
                        minimumOuterHeight, box.Bottom + marginBottom, fixedOuterHeight,
                        indivisibleClosingMargin,
                        DeferredBlankParagraphMargin: blankNested ? blankParagraphMargin : 0);
                    frames.Push(new(lists?.Children(node) ?? node.Children, 0, x + style.BorderLeft.EffectiveWidth + box.PaddingLeft, box.Inner, box.Bottom, false,
                        lists?.Gutter(node) ?? 0, node.Name is "ol" or "ul" ? 0.5 * node.Style.FontSize : 0, markerRight,
                         MarginBottom: marginBottom, IsDocumentBody: node.Name == "body" && frame.IsRoot,
                        ParagraphMarginBasis: node.Name is "p" or "pre" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6"
                            ? containing : null));
                    break;
                }
                default:
                    throw new InvalidOperationException("Unexpected block layout child.");
            }
        }
    }

    // An exact zero percent is zero at every containing width. Treat it like
    // 0pt for empty-slot preflight; nonzero percentages must keep their
    // deferred layout geometry and cannot be mistaken for an empty slot.
    private static bool HasNoParagraphOutsideMargin(TextStyle style) =>
        style.MarginBefore == 0 && style.MarginAfter == 0 &&
        style.PercentMarginBefore.GetValueOrDefault().Value == 0 &&
        style.PercentMarginAfter.GetValueOrDefault().Value == 0;

    // Resolve only when the actual containing width is known; never turn 10% into 10pt.
    // Keep/heading preflight uses the same conversion as ordinary paragraph placement.
    internal static Paragraph ResolveParagraphMargins(Paragraph paragraph, double containingWidth, PdfOptions options)
    {
        var style = paragraph.Style;
        if (style.PercentMarginBefore is null && style.PercentMarginAfter is null)
            return paragraph;
        var before = Checked(style.PercentMarginBefore?.Resolve(containingWidth) ?? style.MarginBefore,
            paragraph.SourceOffset);
        var after = Checked(style.PercentMarginAfter?.Resolve(containingWidth) ?? style.MarginAfter,
            paragraph.SourceOffset);
        var usableHeight = options.PageHeight - 2 * options.Margin;
        if (before > usableHeight + 0.000001 || after > usableHeight + 0.000001)
            throw new FactsPdfException("FPDF1302",
                "Percentage paragraph outside margin cannot fit on a usable PDF page.",
                paragraph.SourceOffset);
        return paragraph with
        {
            Style = style with
            {
                MarginBefore = before, MarginAfter = after,
                PercentMarginBefore = null, PercentMarginAfter = null
            }
        };
    }

    // Iterative, bounded, and using the same containing widths as Steps.
    // Painted, breaking or text-bearing descendants are not blank. Sized
    // leaves with no children reserve their bounded fixed/minimum height.
    private static bool TryMeasureUnpaintedBlankContents(BlockNode node,
        double containingWidth, CancellationToken cancellation, out double mandatoryHeight,
        out double paragraphMargin)
    {
        mandatoryHeight = 0;
        paragraphMargin = 0;
        var largestBlankParagraphMargin = 0d;
        var pending = new Stack<(BlockChild Child, double Width)>();
        foreach (var child in node.Children)
            pending.Push((child, containingWidth));
        while (pending.Count > 0)
        {
            cancellation.ThrowIfCancellationRequested();
            var (item, width) = pending.Pop();
            if (item is not BlockNode child)
                return false;
            var box = child.BoxStyle;
            if (box.HasLayoutPaint || box.DisplayNone || box.BreakBefore || box.BreakAfter ||
                box.AvoidBreakInside || box.MarginTop.Value != 0 || box.MarginBottom.Value != 0 ||
                ((box.MinHeight.HasValue || box.Height.HasValue) && child.Children.Count != 0) ||
                child.Style.BreakBefore || child.Style.BreakAfter)
                return false;
            if (child.Name is "p" or "pre" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6")
            {
                if (box.PaddingTop.Value != 0 || box.PaddingBottom.Value != 0 ||
                    child.Children.Count != 1 ||
                    child.Children[0] is not ParagraphNode content ||
                    content.Paragraph.Runs.Count != 0)
                    return false;
                // A blank, unpainted paragraph can still have a nonzero outside
                // margin. TextLayout defers the largest such margin, including
                // percentages based on this paragraph's containing content width.
                // Count it when deciding whether the blank parent's two outside
                // margins can fit on one physical page.
                var paragraph = content.Paragraph;
                var before = Checked(paragraph.Style.PercentMarginBefore?.Resolve(width) ??
                    paragraph.Style.MarginBefore, paragraph.SourceOffset);
                var after = Checked(paragraph.Style.PercentMarginAfter?.Resolve(width) ??
                    paragraph.Style.MarginAfter, paragraph.SourceOffset);
                largestBlankParagraphMargin = Math.Max(largestBlankParagraphMargin,
                    Math.Max(before, after));
            }
            else if (child.Name is "div" or "section" or "article" or "blockquote")
            {
                var (_, _, available) = ResolveMargins(box, width, child.SourceOffset);
                var resolved = ResolveBox(box, available, child.SourceOffset, width);
                var decoration = resolved.Top + resolved.Bottom;
                var reserved = decoration;
                if (box.MinHeight is { } minimum)
                    reserved = Math.Max(reserved, minimum.Value +
                        (box.BorderBoxSizing ? 0 : decoration));
                if (box.Height is { } fixedHeight)
                    reserved = Math.Max(reserved, fixedHeight.Value +
                        (box.BorderBoxSizing ? 0 : decoration));
                mandatoryHeight += reserved;
                if (!double.IsFinite(mandatoryHeight) || mandatoryHeight > 14_400)
                    throw new FactsPdfException("FPDF1302",
                        "Nested blank report geometry exceeds supported bounds.", child.SourceOffset);
                foreach (var descendant in child.Children)
                    pending.Push((descendant, resolved.Inner));
            }
            else return false;
        }
        paragraphMargin = largestBlankParagraphMargin;
        mandatoryHeight += largestBlankParagraphMargin;
        if (!double.IsFinite(mandatoryHeight) || mandatoryHeight > 14_400)
            throw new FactsPdfException("FPDF1302",
                "Blank report content with paragraph spacing exceeds supported bounds.", node.SourceOffset);
        return true;
    }

    internal readonly record struct ResolvedBox(double PaddingLeft, double Top, double Bottom, double Inner, double OuterWidth);

    internal static (double Left, double Right, double Available) ResolveMargins(
        BoxStyle style, double containingWidth, int offset)
    {
        var left = style.MarginLeftAuto ? 0 : Checked(style.MarginLeft.Resolve(containingWidth), offset);
        var right = style.MarginRightAuto ? 0 : Checked(style.MarginRight.Resolve(containingWidth), offset);
        if (left + right > containingWidth + 0.000001)
            throw new FactsPdfException("FPDF1302", "Paragraph horizontal margins exceed containing width.", offset);
        var available = Math.Max(0, containingWidth - left - right);
        if (!style.MarginLeftAuto && !style.MarginRightAuto)
            return (left, right, available);

        // Auto margins consume the *remaining outer-box* width, after the
        // explicit content width, padding and borders have been resolved.
        // With width:auto, the box already fills the available width, so an
        // auto side resolves to zero, preserving existing full-width behavior.
        var box = ResolveBox(style, available, offset, containingWidth);
        var free = available - box.OuterWidth;
        if (free < -0.000001)
            throw new FactsPdfException("FPDF1302", "Auto margins cannot fit the specified box width.", offset);
        free = Math.Max(0, free);
        if (style.MarginLeftAuto && style.MarginRightAuto)
        {
            left += free / 2;
            right += free / 2;
        }
        else if (style.MarginLeftAuto) left += free;
        else right += free;
        return (left, right, Math.Max(0, containingWidth - left - right));
    }

    internal static ResolvedBox ResolveBox(BoxStyle style, double width, int offset,
        double? percentageBasis = null)
    {
        double Resolve(CssLength length) => Checked(length.Resolve(percentageBasis ?? width), offset);
        var left = Resolve(style.PaddingLeft);
        var right = Resolve(style.PaddingRight);
        var top = Resolve(style.PaddingTop);
        var bottom = Resolve(style.PaddingBottom);
        var bLeft = style.BorderLeft.EffectiveWidth;
        var bRight = style.BorderRight.EffectiveWidth;
        var bTop = style.BorderTop.EffectiveWidth;
        var bBottom = style.BorderBottom.EffectiveWidth;
        var edges = left + right + bLeft + bRight;
        // Explicit border-box width includes border and padding, unlike the
        // legacy content-box contract. Auto width still fills available space.
        var inner = style.Width.HasValue
            ? Resolve(style.Width.Value) - (style.BorderBoxSizing ? edges : 0)
            : width - edges;
        if (style.MinWidth.HasValue || style.MaxWidth.HasValue)
        {
            // CSS min/max constrain the used content or border-box measure,
            // not just the explicitly declared width. This includes width:auto
            // and lets max-width create real free space for auto margins.
            var measure = style.BorderBoxSizing ? inner + edges : inner;
            if (style.MaxWidth.HasValue)
                measure = Math.Min(measure, Resolve(style.MaxWidth.Value));
            if (style.MinWidth.HasValue)
                measure = Math.Max(measure, Resolve(style.MinWidth.Value)); // min wins over max
            inner = measure - (style.BorderBoxSizing ? edges : 0);
        }
        if (inner < -0.000001 || inner + edges > width + 0.000001)
            throw new FactsPdfException("FPDF1302", "Box outer width exceeds containing content width.", offset);
        inner = Math.Max(0, inner);
        return new(left, top + bTop, bottom + bBottom, inner, inner + left + right + bLeft + bRight);
    }

    private static double Checked(double value, int offset)
    {
        if (!double.IsFinite(value) || value < 0 || value > 14_400)
            throw new FactsPdfException("FPDF1302", "Resolved box length exceeds supported bounds.", offset);
        return value;
    }
}
