namespace FactsPDF;

internal abstract record BlockLayoutStep;
internal sealed record BeginBlock(double PaddingTop, BoxStyle Style, double X,
    double OuterWidth, Rgb TextColor, int SourceOffset, bool IsParagraph) : BlockLayoutStep;
internal sealed record EndBlock(double PaddingBottom) : BlockLayoutStep;
internal sealed record LayoutParagraph(Paragraph Paragraph, double ContentX, double ContentWidth) : BlockLayoutStep;

/// <summary>
/// Iterative, lazy containing-block resolution. All positions are PDF points;
/// percentages (even vertical padding) use the parent content-box width.
/// M7 adds single-page paint to M6 geometry; page fragmentation remains M8.
/// </summary>
internal static class BlockLayout
{
    private readonly record struct Frame(List<BlockChild> Children, int Index, double X,
        double ContentWidth, double Bottom, bool IsRoot);

    internal static IEnumerable<BlockLayoutStep> Steps(DocumentRoot root, PdfOptions options,
        CancellationToken cancellation)
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
                if (!frame.IsRoot) yield return new EndBlock(frame.Bottom);
                continue;
            }
            frames.Push(frame with { Index = frame.Index + 1 });
            switch (frame.Children[frame.Index])
            {
                case ParagraphNode paragraph:
                    yield return new LayoutParagraph(paragraph.Paragraph, frame.X, frame.ContentWidth);
                    break;
                case BlockNode node:
                {
                    var style = node.BoxStyle;
                    var width = frame.ContentWidth;
                    double Resolve(CssLength length) => Checked(length.Resolve(width), node.SourceOffset);
                    var left = Resolve(style.PaddingLeft);
                    var right = Resolve(style.PaddingRight);
                    var top = Resolve(style.PaddingTop);
                    var bottom = Resolve(style.PaddingBottom);
                    var bLeft = style.BorderLeft.EffectiveWidth;
                    var bRight = style.BorderRight.EffectiveWidth;
                    var bTop = style.BorderTop.EffectiveWidth;
                    var bBottom = style.BorderBottom.EffectiveWidth;
                    var inner = style.Width.HasValue
                        ? Resolve(style.Width.Value) : frame.ContentWidth - left - right - bLeft - bRight;
                    if (inner < -0.000001 || inner + left + right + bLeft + bRight > frame.ContentWidth + 0.000001)
                        throw new FactsPdfException("FPDF1302", "Box outer width exceeds containing content width.", node.SourceOffset);
                    inner = Math.Max(0, inner);
                    yield return new BeginBlock(top + bTop, style, frame.X,
                        inner + left + right + bLeft + bRight, node.Style.Color, node.SourceOffset,
                        node.Name is "p" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6");
                    frames.Push(new(node.Children, 0, frame.X + bLeft + left, inner, bottom + bBottom, false));
                    break;
                }
                default:
                    throw new InvalidOperationException("Unexpected block layout child.");
            }
        }
    }

    private static double Checked(double value, int offset)
    {
        if (!double.IsFinite(value) || value < 0 || value > 14_400)
            throw new FactsPdfException("FPDF1302", "Resolved box length exceeds supported bounds.", offset);
        return value;
    }
}
