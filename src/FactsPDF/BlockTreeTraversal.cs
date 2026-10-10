namespace FactsPDF;

/// <summary>
/// Iterative document-order text projection from the actual nested box tree.
/// No flat paragraph list is allocated for native PDF conversion.
/// Future box geometry/layout can reuse the same parent-child structure.
/// </summary>
internal static class BlockTreeTraversal
{
    internal static IEnumerable<Paragraph> Paragraphs(DocumentRoot root,
        CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(root);

        // A list/index stack avoids C# recursion and recursion-based stack
        // exhaustion for callers that explicitly raise MaxDepth.
        var pending = new Stack<(List<BlockChild> Children, int Index)>();
        pending.Push((root.Children, 0));
        while (pending.Count > 0)
        {
            cancellation.ThrowIfCancellationRequested();
            var (siblings, index) = pending.Pop();
            if (index == siblings.Count) continue;
            pending.Push((siblings, index + 1));
            switch (siblings[index])
            {
                case ParagraphNode paragraph:
                    yield return paragraph.Paragraph;
                    break;
                case BlockNode box:
                    pending.Push((box.Children, 0));
                    break;
                default:
                    throw new InvalidOperationException("Unexpected block tree child.");
            }
        }
    }
}
