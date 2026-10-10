namespace FactsPDF;

internal abstract record BlockLayoutStep;
internal sealed record BeginBlock(double PaddingTop) : BlockLayoutStep;
internal sealed record EndBlock(double PaddingBottom) : BlockLayoutStep;
internal sealed record LayoutParagraph(Paragraph Paragraph, double ContentX, double ContentWidth) : BlockLayoutStep;

/// <summary>
/// Iterative, lazy containing-block resolution. All positions are PDF points;
/// percentages (even vertical padding) use the parent content-box width.
/// Painting and page box fragments are deliberately outside M6.
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
                    var inner = style.Width.HasValue
                        ? Resolve(style.Width.Value) : frame.ContentWidth - left - right;
                    if (inner < -0.000001 || inner + left + right > frame.ContentWidth + 0.000001)
                        throw new FactsPdfException("FPDF1302", "Box outer width exceeds containing content width.", node.SourceOffset);
                    inner = Math.Max(0, inner);
                    yield return new BeginBlock(top);
                    frames.Push(new(node.Children, 0, frame.X + left, inner, bottom, false));
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
