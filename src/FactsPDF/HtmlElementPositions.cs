namespace FactsPDF;

/// <summary>
/// Where each element of a document sits among its element siblings, and whether it is empty,
/// for the structural pseudo-classes. Built only when a stylesheet uses one, from the same token
/// list the document reader walks, with the reader's implied end tags (p and li); the reader
/// checks every element's parent against it. Siblings are counted as in a browser's DOM: script
/// and template elements, which collection drops from that list, still count, and document-level
/// content goes where the HTML parser puts it (style, script and template before any body content
/// into head, everything else into body, after the explicit head's or body's own children).
/// </summary>
internal sealed class HtmlElementPositions
{
    /// <summary>An element's 1-based Index among Count element siblings and TypeIndex among
    /// TypeCount siblings of its type, and whether it has no child element or text. Parent is the
    /// start-tag index of its parent in the reader's tree, or -1. Known is false when an html, head
    /// or body tag sits where the HTML parser ignores or merges it; EmptyKnown is also false for
    /// body, which receives the whitespace that follows its end tag.</summary>
    internal readonly record struct Position(int Parent, int Index, int Count, int TypeIndex, int TypeCount,
        bool Known, bool EmptyKnown, bool Empty);

    private struct Slot
    {
        internal int Parent, Index, Count, TypeIndex, TypeCount;
        internal bool EmptyKnown, Empty;
    }

    private readonly record struct Frame(int Token, string Name, int ChildStart);

    private readonly Slot[] slots;
    private bool broken;

    private HtmlElementPositions(int count) => slots = new Slot[count];

    internal Position this[int token]
    {
        get
        {
            if ((uint)token >= (uint)slots.Length)
                throw new InvalidOperationException("The element has no recorded position.");
            var slot = slots[token];
            return new(slot.Parent, slot.Index, slot.Count, slot.TypeIndex, slot.TypeCount,
                !broken, !broken && slot.EmptyKnown, slot.Empty);
        }
    }

    /// <summary>Fails when the reader placed the element at <paramref name="token"/> under a
    /// different parent than these positions assume.</summary>
    internal void CheckParent(int token, int parent)
    {
        if (slots[token].Parent != parent)
            throw new InvalidOperationException("Element positions diverged from the document reader's tree.");
    }

    private static bool Void(string name) => name is "img" or "hr" or "br" or "meta" or "link" or "col";

    // Elements the HTML parser keeps in head while no body content has been seen.
    private static bool HeadContent(string name) => name is "style" or "script" or "template" or "meta" or "link" or "title";

    /// <param name="tokens">The collected document tokens.</param>
    /// <param name="dropped">Script and template elements collection removed, each with the token
    /// index it preceded, in document order.</param>
    /// <param name="cancellation">Cancels the walk.</param>
    internal static HtmlElementPositions Build(List<HtmlToken> tokens,
        IReadOnlyList<(int Token, string Name)>? dropped, CancellationToken cancellation)
    {
        var positions = new HtmlElementPositions(tokens.Count);
        var slots = positions.slots;
        var frames = new List<Frame>();
        var content = new List<bool>(); // per open frame: whether it has a child element or text
        // Children of every open element as start-tag indices (-1 for a dropped element) with their
        // names; a frame's children are the tail from its ChildStart. head and body collect theirs
        // in their own lists, with the document-level content the HTML parser moves into them.
        var children = new List<(int Token, string Name)>();
        var headChildren = new List<(int Token, string Name)>();
        var bodyChildren = new List<(int Token, string Name)>();
        var typeCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        int html = -1, head = -1, body = -1;
        var inBody = false; // body content (explicit or implied) has started
        var headText = false;
        var next = 0;

        bool DocumentLevel() => frames.Count == 0 || frames[^1].Token == html;
        void Content() { if (content.Count > 0) content[^1] = true; }
        void AddChild(int token, string name)
        {
            if (token >= 0) slots[token].Parent = frames.Count == 0 ? -1 : frames[^1].Token;
            if (!DocumentLevel())
            {
                Content();
                if (token >= 0 && name is "html" or "head" or "body") positions.broken = true;
                children.Add((token, name));
                return;
            }
            if (token >= 0 && name == "html")
            {
                if (frames.Count > 0 || html >= 0 || head >= 0 || inBody || headChildren.Count > 0) positions.broken = true;
                else html = token;
            }
            else if (token >= 0 && name == "head")
            {
                if (head >= 0 || inBody || headChildren.Count > 0) positions.broken = true;
                else head = token;
            }
            else if (token >= 0 && name == "body")
            {
                if (body >= 0 || inBody) positions.broken = true;
                else { body = token; inBody = true; }
            }
            else if (!inBody && HeadContent(name)) headChildren.Add((token, name));
            else { inBody = true; bodyChildren.Add((token, name)); }
        }
        void Open(int token, string name)
        {
            frames.Add(new(token, name, children.Count));
            content.Add(false);
        }
        void Close()
        {
            var frame = frames[^1];
            var count = children.Count - frame.ChildStart;
            if (frame.Token == head || frame.Token == body)
            {
                var list = frame.Token == head ? headChildren : bodyChildren;
                for (var k = frame.ChildStart; k < children.Count; k++) list.Add(children[k]);
                if (frame.Token == head) headText = content[^1];
            }
            else
            {
                Place(children, frame.ChildStart, count);
                slots[frame.Token].Empty = !content[^1];
                slots[frame.Token].EmptyKnown = true;
            }
            children.RemoveRange(frame.ChildStart, count);
            frames.RemoveAt(frames.Count - 1);
            content.RemoveAt(content.Count - 1);
        }
        void Place(List<(int Token, string Name)> list, int start, int count)
        {
            typeCounts.Clear();
            for (var k = start; k < start + count; k++)
            {
                cancellation.ThrowIfCancellationRequested();
                var (token, name) = list[k];
                var typeIndex = typeCounts.TryGetValue(name, out var seen) ? seen + 1 : 1;
                typeCounts[name] = typeIndex;
                if (token < 0) continue;
                ref var slot = ref slots[token];
                slot.Index = k - start + 1; slot.Count = count; slot.TypeIndex = typeIndex;
            }
            for (var k = start; k < start + count; k++)
                if (list[k].Token is >= 0 and var token) slots[token].TypeCount = typeCounts[list[k].Name];
        }
        void Dropped(int before)
        {
            while (dropped is not null && next < dropped.Count && dropped[next].Token == before)
                AddChild(-1, dropped[next++].Name);
        }

        for (var t = 0; t < tokens.Count; t++)
        {
            cancellation.ThrowIfCancellationRequested();
            Dropped(t);
            var token = tokens[t];
            if (token.Kind == HtmlTokenKind.Text)
            {
                // The HTML parser drops one line feed right after a pre start tag.
                var text = token.Value.AsSpan();
                if (frames.Count > 0 && frames[^1].Name == "pre" && frames[^1].Token == t - 1)
                    text = text.StartsWith("\r\n") ? text[2..] : text.StartsWith("\n") || text.StartsWith("\r") ? text[1..] : text;
                if (text.IsEmpty) continue;
                // Document-level text other than whitespace starts the body.
                if (DocumentLevel()) inBody |= !HtmlTokens.AllSpace(token.Value);
                else Content();
                continue;
            }
            if (token.Kind == HtmlTokenKind.StyleText)
            {
                if (token.Value.Length > 0) Content();
                continue;
            }
            var name = token.Value;
            var top = frames.Count > 0 ? frames[^1].Name : null;
            if (token.Kind == HtmlTokenKind.Start)
            {
                // The reader's implied end tags: a block closes an open p, an li closes an open li.
                if ((HtmlDocumentReader.ParagraphTag(name) || HtmlDocumentReader.Container(name)) && top == "p")
                {
                    Close(); top = frames.Count > 0 ? frames[^1].Name : null;
                }
                if (name == "li" && top == "li") Close();
                AddChild(t, name);
                if (!Void(name) && !token.SelfClosing) Open(t, name);
                continue;
            }
            if (top == "p" && name != "p" && HtmlDocumentReader.Container(name)) { Close(); top = frames.Count > 0 ? frames[^1].Name : null; }
            if (HtmlDocumentReader.ListTag(name) && top == "li") { Close(); top = frames.Count > 0 ? frames[^1].Name : null; }
            // Any other mismatched end tag fails in the reader before an element after it is styled.
            if (top == name) Close();
        }
        Dropped(tokens.Count);
        while (frames.Count > 0) Close();
        Place(headChildren, 0, headChildren.Count);
        Place(bodyChildren, 0, bodyChildren.Count);
        // html is the document's only element; head and body are always html's two children.
        if (html >= 0) slots[html] = slots[html] with { Index = 1, Count = 1, TypeIndex = 1, TypeCount = 1, Empty = false, EmptyKnown = true };
        if (head >= 0) slots[head] = slots[head] with { Index = 1, Count = 2, TypeIndex = 1, TypeCount = 1, Empty = !headText && headChildren.Count == 0, EmptyKnown = true };
        if (body >= 0) slots[body] = slots[body] with { Index = 2, Count = 2, TypeIndex = 1, TypeCount = 1, EmptyKnown = false };
        return positions;
    }
}
