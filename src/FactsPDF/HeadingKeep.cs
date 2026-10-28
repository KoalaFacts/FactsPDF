using static FactsPDF.TextLines;

namespace FactsPDF;

// Bounded sibling lookahead for the opt-in F1 subset. Actual placement remains
// in TextLayout; this measurement never retains display commands or charges them.
internal static class HeadingKeep
{
    internal static bool IsHeading(string name) => name is "h1" or "h2" or "h3" or "h4" or "h5" or "h6";

    internal static (int End, bool Valid) Find(IReadOnlyList<BlockChild> children, int start,
        CancellationToken cancellation)
    {
        var canceled = false;
        for (var i = start; i < children.Count; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            if (children[i] is not BlockNode node ||
                !(node.Name == "p" || IsHeading(node.Name) && node.Style.KeepWithNext) ||
                node.Children.Count != 1 || node.Children[0] is not ParagraphNode paragraph)
                return (Math.Max(start + 1, i), false);
            if (i > start && node.Style.BreakBefore) canceled = true;
            if (node.Name == "p" && HasTextContent(paragraph.Paragraph, cancellation))
                return (i + 1, !canceled);
            if (node.Style.BreakAfter) canceled = true;
        }
        return (children.Count, false);
    }

    private sealed class Cursor(double after, double leading, double closed)
    {
        internal double Y, After = after, Leading = leading, Closed = closed;
        internal double Height => Y + Leading + Closed;
        internal double Gap(double before, double top) => Math.Max(After, before) + Leading + Closed + top;
        internal void Add(double height, double before, double marginAfter, BlockLayout.ResolvedBox box, bool paint, bool hasLines)
        {
            if (hasLines)
            {
                Y += Gap(before, box.Top) + height;
                Leading = Closed = 0;
                After = marginAfter;
            }
            else
            {
                if (paint || box.Top > 0) { Closed += Math.Max(After, before); After = marginAfter; }
                else After = Math.Max(After, marginAfter);
                Closed += box.Top;
            }
            Closed += box.Bottom;
        }
    }

    internal static bool ShouldMove(LayoutHeadingGroup group, PdfOptions options, double remaining,
        double after, double leading, double closed, double freshLeading, bool retainFirstHeadingMargin,
        Dictionary<(FontRole Role, int Scalar), (PdfFont? Font, double Width)> resolved,
        Action<int, int> ensureCommands, CancellationToken cancellation)
    {
        var here = new Cursor(after, leading, closed);
        var fresh = new Cursor(0, freshLeading, 0);
        var usable = options.PageHeight - 2 * options.Margin;
        var commands = 0;
        try
        {
            for (var i = group.Start; i < group.End; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                var node = (BlockNode)group.Children[i];
                var paragraph = ((ParagraphNode)node.Children[0]).Paragraph;
                var (_, _, available) = BlockLayout.ResolveMargins(node.BoxStyle, group.ContentWidth, node.SourceOffset);
                var box = BlockLayout.ResolveBox(node.BoxStyle, available, node.SourceOffset, group.ContentWidth);
                var before = paragraph.Style.MarginBefore;
                // The first heading's top margin is normally trimmed after a
                // keep move, except when a new outer-margined panel moves too.
                var freshBefore = i == group.Start && !retainFirstHeadingMargin ? 0 : before;
                if (HasTextContent(paragraph, cancellation)) ensureCommands(commands + 1, node.SourceOffset);
                var height = 0d;
                var hasLines = false;
                // Wrap can validate the whole current paragraph before its first
                // yield. Bound geometry and later paragraphs, not that validation.
                foreach (var line in Wrap(paragraph, box.Inner, options, resolved, cancellation))
                {
                    hasLines = true;
                    height += line.Height;
                    if (fresh.Y + fresh.Gap(freshBefore, box.Top) + height + box.Bottom > usable + 0.000001)
                        return false;
                    for (var g = 0; g < line.Glyphs.Count;)
                    {
                        var first = line.Glyphs[g++];
                        while (g < line.Glyphs.Count && line.Glyphs[g].Style == first.Style &&
                            ReferenceEquals(line.Glyphs[g].Font, first.Font))
                        {
                            if ((g & 255) == 0) cancellation.ThrowIfCancellationRequested();
                            g++;
                        }
                        ensureCommands(++commands, node.SourceOffset);
                    }
                }
                var paint = new PaintedBox(0, 0, box.OuterWidth, box.Top + height + box.Bottom,
                    node.BoxStyle, node.Style.Color, 0);
                commands += paint.CommandCount;
                ensureCommands(commands, node.SourceOffset);
                here.Add(height, before, paragraph.Style.MarginAfter, box, node.BoxStyle.HasPaint, hasLines);
                fresh.Add(height, freshBefore, paragraph.Style.MarginAfter, box, node.BoxStyle.HasPaint, hasLines);
                if (fresh.Height > usable + 0.000001) return false;
            }
            return here.Height > remaining + 0.000001;
        }
        catch (FactsPdfException)
        {
            // Preserve ordinary layout's error precedence and atomic destination
            // behavior. Cancellation is deliberately not caught.
            return false;
        }
    }
}
