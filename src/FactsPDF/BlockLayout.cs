namespace FactsPDF;

internal abstract record BlockLayoutStep;
internal sealed record BeginBlock(double PaddingTop, BoxStyle Style, double X,
    double OuterWidth, Rgb TextColor, int SourceOffset, bool IsParagraph,
    double MarginTop = 0, double RequiredRuleHeight = 0) : BlockLayoutStep;
internal sealed record EndBlock(double PaddingBottom, double MarginBottom = 0) : BlockLayoutStep;
internal sealed record LayoutParagraph(Paragraph Paragraph, double ContentX, double ContentWidth,
    ListMarker? Marker = null, double MarkerRightX = 0) : BlockLayoutStep;
internal sealed record LayoutImage(ImageNode Node, double X, double ContentX, double ContentTop,
    double Width, double Height, double OuterWidth, double OuterHeight) : BlockLayoutStep;
internal sealed record LayoutHeadingGroup(IReadOnlyList<BlockChild> Children, int Start, int End,
    double ContentWidth) : BlockLayoutStep;
internal sealed record LayoutTable(TableNode Node, double X, double AvailableWidth) : BlockLayoutStep;

/// <summary>
/// Iterative, lazy containing-block resolution. All positions are PDF points;
/// percentages (even vertical padding) use the parent content-box width.
/// M8 adds per-page box fragments with sliced background and side borders.
/// </summary>
internal static class BlockLayout
{
    private readonly record struct Frame(IReadOnlyList<BlockChild> Children, int Index, double X,
        double ContentWidth, double Bottom, bool IsRoot, double Gutter = 0, double MarkerGap = 0,
        double MarkerRightX = 0, int KeepUntil = 0, double MarginBottom = 0);

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
                    yield return new LayoutParagraph(paragraph.Paragraph, frame.X, frame.ContentWidth,
                        lists?.Marker(paragraph.Paragraph), frame.MarkerRightX);
                    break;
                case BlockNode node:
                {
                    var style = node.BoxStyle;
                    var gutter = node.Name == "li" ? frame.Gutter : 0;
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
                    // The standalone hr and independent empty painted-block oracle
                    // both need the same physical fit check before opening a box.
                    // Otherwise an empty decorated div can overflow at page end
                    // even when the equivalent hr is correctly moved intact.
                    var requiredEmptyPaintHeight = node.Name == "hr" ||
                        (node.Children.Count == 0 && style.HasPaint)
                        ? box.Top + box.Bottom : 0;
                    yield return new BeginBlock(box.Top, style, x,
                        box.OuterWidth, node.Style.Color, node.SourceOffset,
                        node.Name is "p" or "pre" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6",
                        marginTop, requiredEmptyPaintHeight);
                    frames.Push(new(lists?.Children(node) ?? node.Children, 0, x + style.BorderLeft.EffectiveWidth + box.PaddingLeft, box.Inner, box.Bottom, false,
                        lists?.Gutter(node) ?? 0, node.Name is "ol" or "ul" ? 0.5 * node.Style.FontSize : 0, markerRight, MarginBottom: marginBottom));
                    break;
                }
                default:
                    throw new InvalidOperationException("Unexpected block layout child.");
            }
        }
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
