namespace FactsPDF;

/// <summary>
/// Where each element of a document sits among its element siblings, and whether it is empty,
/// for the structural pseudo-classes. Built only when a stylesheet uses one, from the token list
/// the document reader walks (which already has the html, head and body structure the HTML parser
/// builds, see HtmlImpliedElements) with the reader's own implied end tags
/// (HtmlDocumentReader.StartClosesParagraph and the others), so both build the same tree. Where
/// the reader would reject a token instead, it fails before any later element is styled. Script and
/// template elements, which collection drops from the list, still count as siblings, as in the DOM.
/// </summary>
internal sealed class HtmlElementPositions
{
    /// <summary>An element's 1-based Index among Count element siblings and TypeIndex among
    /// TypeCount siblings of its type, and whether it has no child element or text.</summary>
    internal readonly record struct Position(int Index, int Count, int TypeIndex, int TypeCount, bool Empty);

    private struct Slot
    {
        internal int Index, Count, TypeIndex, TypeCount, Previous, Next, FirstChild, Parent;
        internal bool Empty;
    }

    private readonly record struct Frame(int Token, string Name, int ChildStart, bool CellList = false);

    private readonly Slot[] slots;
    private readonly List<HtmlToken> tokens;
    private readonly IReadOnlyList<(int Token, string Name, HtmlToken Source)>? dropped;

    // A node is an element of the document: its start tag's index in the token list, or, for a
    // script or template element the collection dropped, the token count plus its place among them.
    private HtmlElementPositions(List<HtmlToken> tokens, IReadOnlyList<(int Token, string Name, HtmlToken Source)>? dropped)
    {
        this.tokens = tokens;
        this.dropped = dropped;
        slots = new Slot[tokens.Count + (dropped?.Count ?? 0)];
        for (var i = 0; i < slots.Length; i++) slots[i].FirstChild = -1;
    }

    /// <summary>The node of the element sibling just before this one, or -1 for a first child.</summary>
    internal int PreviousSibling(int node) => slots[node].Previous;

    /// <summary>The node of the element sibling just after this one, or -1 for a last child.</summary>
    internal int NextSibling(int node) => slots[node].Next;

    /// <summary>The node of the element's first child element, or -1 when it has none.</summary>
    internal int FirstChild(int node) => slots[node].FirstChild;

    /// <summary>The node of the element's parent, or -1 for the document's root element.</summary>
    internal int Parent(int node) => slots[node].Parent;

    /// <summary>The element a node stands for: a dropped script or template keeps its attributes, so
    /// a selector can test it as a sibling.</summary>
    internal CssElement Element(int node) => node < tokens.Count
        ? CssElement.From(tokens[node], node)
        : CssElement.From(dropped![node - tokens.Count].Source, node);

    internal Position this[int token]
    {
        get
        {
            var slot = slots[token];
            return new(slot.Index, slot.Count, slot.TypeIndex, slot.TypeCount, slot.Empty);
        }
    }

    // Elements the reader never opens: void elements and those it skips.
    private static bool Void(string name) => name is "img" or "hr" or "br" or "meta" or "link" or "base" or "col";

    /// <param name="tokens">The collected document tokens.</param>
    /// <param name="dropped">Script and template elements collection removed, each with the token
    /// index it preceded, in document order.</param>
    /// <param name="cancellation">Cancels the walk.</param>
    internal static HtmlElementPositions Build(List<HtmlToken> tokens,
        IReadOnlyList<(int Token, string Name, HtmlToken Source)>? dropped, CancellationToken cancellation)
    {
        var positions = new HtmlElementPositions(tokens, dropped);
        var slots = positions.slots;
        var frames = new List<Frame>();
        var content = new List<bool>(); // per open frame: whether it has a child element or text
        // Children of every open element as nodes (start-tag indices, or the ids of dropped elements) with their
        // names; a frame's children are the tail from its ChildStart.
        var children = new List<(int Token, string Name)>();
        var typeCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var tables = 0; // open table elements: the reader implies no p end tag inside a table
        var cellLists = 0; // open lists inside a table cell: the reader implies end tags there as outside tables
        var next = 0;

        string? Top() => frames.Count > 0 ? frames[^1].Name : null;
        void AddChild(int token, string name)
        {
            if (content.Count > 0) content[^1] = true;
            children.Add((token, name));
        }
        void Close()
        {
            var frame = frames[^1];
            var count = children.Count - frame.ChildStart;
            typeCounts.Clear();
            for (var k = frame.ChildStart; k < children.Count; k++)
            {
                cancellation.ThrowIfCancellationRequested();
                var (token, name) = children[k];
                var typeIndex = typeCounts.TryGetValue(name, out var seen) ? seen + 1 : 1;
                typeCounts[name] = typeIndex;
                ref var slot = ref slots[token];
                slot.Index = k - frame.ChildStart + 1; slot.Count = count; slot.TypeIndex = typeIndex;
                slot.Previous = k > frame.ChildStart ? children[k - 1].Token : -1;
                slot.Next = k + 1 < children.Count ? children[k + 1].Token : -1;
                slot.Parent = frame.Token;
            }
            slots[frame.Token].FirstChild = count > 0 ? children[frame.ChildStart].Token : -1;
            for (var k = frame.ChildStart; k < children.Count; k++)
                slots[children[k].Token].TypeCount = typeCounts[children[k].Name];
            slots[frame.Token].Empty = !content[^1];
            if (frame.Name == "table") tables--;
            if (frame.CellList) cellLists--;
            children.RemoveRange(frame.ChildStart, count);
            frames.RemoveAt(frames.Count - 1);
            content.RemoveAt(content.Count - 1);
        }

        for (var t = 0; t < tokens.Count; t++)
        {
            cancellation.ThrowIfCancellationRequested();
            while (dropped is not null && next < dropped.Count && dropped[next].Token == t)
            { AddChild(tokens.Count + next, dropped[next].Name); next++; }
            var token = tokens[t];
            if (token.Kind == HtmlTokenKind.Text)
            {
                // The HTML parser drops one line feed right after a pre start tag.
                var text = token.Value.AsSpan();
                if (Top() == "pre" && frames[^1].Token == t - 1)
                    text = text.StartsWith("\r\n") ? text[2..] : text.StartsWith("\n") || text.StartsWith("\r") ? text[1..] : text;
                if (!text.IsEmpty && content.Count > 0) content[^1] = true;
                continue;
            }
            if (token.Kind == HtmlTokenKind.StyleText)
            {
                if (token.Value.Length > 0 && content.Count > 0) content[^1] = true;
                continue;
            }
            var name = token.Value;
            if (token.Kind == HtmlTokenKind.Start)
            {
                if (Top() is { } open && HtmlDocumentReader.StartClosesParagraph(open, name, tables > 0 && cellLists == 0)) Close();
                if (Top() is { } item && HtmlDocumentReader.StartClosesListItem(item, name)) Close();
                AddChild(t, name);
                if (Void(name) || token.SelfClosing) continue;
                // A list in a table cell (or any element inside one) is block content the reader builds a tree for.
                var cellList = tables > 0 && (cellLists > 0 || HtmlDocumentReader.ListTag(name) && Top() is "td" or "th");
                if (cellList) cellLists++;
                frames.Add(new(t, name, children.Count, cellList));
                content.Add(false);
                if (name == "table") tables++;
                continue;
            }
            if (Top() is { } p && HtmlDocumentReader.EndClosesParagraph(p, name, tables > 0 && cellLists == 0)) Close();
            if (Top() is { } li && HtmlDocumentReader.EndClosesListItem(li, name)) Close();
            // The reader rejects any other mismatched end tag before it styles a later element.
            if (Top() == name) Close();
        }
        while (dropped is not null && next < dropped.Count) { AddChild(tokens.Count + next, dropped[next].Name); next++; }
        while (frames.Count > 0) Close();
        // The document's own children: html, its only element, unless the reader rejects the page.
        var rootCount = children.Count;
        for (var k = 0; k < rootCount; k++)
        {
            var token = children[k].Token;
            slots[token] = slots[token] with { Index = k + 1, Count = rootCount, TypeIndex = k + 1, TypeCount = rootCount,
                Previous = k > 0 ? children[k - 1].Token : -1, Next = k + 1 < rootCount ? children[k + 1].Token : -1, Parent = -1 };
        }
        return positions;
    }
}
