using System.Globalization;

namespace FactsPDF;

internal sealed record ListMarker(string? Text, TextStyle Style, double Width, int SourceOffset);

/// <summary>Per-layout marker metadata. Parsed trees remain reusable and unmodified.</summary>
internal sealed class ListLayout
{
    private readonly Dictionary<BlockNode, double> gutters = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<Paragraph, ListMarker> markers = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<BlockNode, IReadOnlyList<BlockChild>> children = new(ReferenceEqualityComparer.Instance);

    internal double Gutter(BlockNode node) => gutters.GetValueOrDefault(node);
    internal ListMarker? Marker(Paragraph paragraph) => markers.GetValueOrDefault(paragraph);
    internal IReadOnlyList<BlockChild> Children(BlockNode node) => children.GetValueOrDefault(node) ?? node.Children;

    internal static ListLayout? Prepare(DocumentRoot root, PdfOptions options, CancellationToken cancellation)
    {
        if (!root.HasLists) return null;
        var result = new ListLayout();
        var widths = new Dictionary<(FontRole Role, int Scalar), double>();
        foreach (var child in Descendants(root.Children, cancellation))
        {
            if (child is not BlockNode list || list.Name is not ("ol" or "ul")) continue;
            var maximum = 0d;
            for (var index = 0; index < list.Children.Count; index++)
            {
                cancellation.ThrowIfCancellationRequested();
                var item = (BlockNode)list.Children[index];
                var text = list.Name == "ol" ? (index + 1).ToString(CultureInfo.InvariantCulture) + "." : null;
                var width = text is null ? 0.3 * item.Style.FontSize : Measure(text, item.Style, item.SourceOffset);
                maximum = Math.Max(maximum, width);
                Paragraph? paragraph = null;
                foreach (var descendant in Descendants(item.Children, cancellation))
                {
                    if (descendant is BlockNode { Name: "ol" or "ul" }) break;
                    if (descendant is ParagraphNode leaf) { paragraph = leaf.Paragraph; break; }
                }
                if (paragraph is null)
                {
                    paragraph = new Paragraph(item.Style, item.SourceOffset);
                    var view = new List<BlockChild>(item.Children.Count + 1) { new ParagraphNode(paragraph, true) };
                    view.AddRange(item.Children);
                    result.children.Add(item, view);
                }
                result.markers.Add(paragraph, new(text, item.Style, width, item.SourceOffset));
            }
            if (list.Children.Count > 0)
                result.gutters.Add(list, Math.Max(2 * list.Style.FontSize, maximum + 0.5 * list.Style.FontSize));
        }
        return result;

        double Measure(string text, TextStyle style, int offset)
        {
            var role = FontRoles.Role(style);
            var fonts = FontRoles.Chain(options, role, offset);
            var total = 0d;
            foreach (var scalar in text)
            {
                cancellation.ThrowIfCancellationRequested();
                if (!widths.TryGetValue((role, scalar), out var width))
                {
                    if (fonts.Count == 0) width = 600;
                    else
                    {
                        PdfFont? chosen = null; ushort glyph = 0;
                        foreach (var font in fonts)
                        {
                            glyph = font.GlyphFor(scalar);
                            if (glyph != 0) { chosen = font; break; }
                        }
                        if (chosen is null)
                            throw new FactsPdfException("FPDF1504", $"No supplied {FontRoles.Name(role)} font contains list marker U+{(int)scalar:X4}.", offset);
                        width = chosen.Width1000(glyph);
                    }
                    widths.Add((role, scalar), width);
                }
                // Decimal markers are later rendered by TextLines using Tc;
                // the allocated list gutter must measure the same advance or
                // inherited tracking can move the marker into the item text.
                total += width * style.FontSize / 1000 + style.LetterSpacing;
            }
            return total;
        }
    }

    private static IEnumerable<BlockChild> Descendants(IReadOnlyList<BlockChild> children, CancellationToken cancellation)
    {
        var stack = new Stack<(IReadOnlyList<BlockChild> Children, int Index)>();
        stack.Push((children, 0));
        while (stack.Count > 0)
        {
            cancellation.ThrowIfCancellationRequested();
            var frame = stack.Pop();
            if (frame.Index == frame.Children.Count) continue;
            var child = frame.Children[frame.Index];
            stack.Push((frame.Children, frame.Index + 1));
            yield return child;
            if (child is BlockNode node) stack.Push((node.Children, 0));
        }
    }
}
