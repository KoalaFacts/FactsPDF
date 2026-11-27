namespace FactsPDF;

internal static class HtmlDocumentReader
{
    // ChildStart: for a frame with a Box, where the box's children begin in the reader's child stack.
    // FlexItem: an inline element that is a direct child of a flex container, so its text is a
    // flex item of its own (CSS blockifies it) rather than part of a shared anonymous paragraph.
    // After: the element's generated ::after content, a block box or inline text, added when it closes.
    // AlignStart: the element's computed text-align is its initial value, start, which a th or
    // caption child turns into the user-agent centre (see ReadTree).
    private readonly record struct Frame(string Name, TextStyle Style, BoxStyle BoxStyle, BlockNode? Box,
        int SourceOffset = -1, int ChildStart = 0, bool AlignStart = true, bool FlexItem = false, object? After = null);

    // The text and computed style of an inline ::before or ::after box.
    private sealed record GeneratedText(string Text, TextStyle Style);
    internal static bool ParagraphTag(string name) => name is "p" or "pre" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6";
    internal static bool ListTag(string name) => name is "ol" or "ul" or "menu";

    // The end tags the reader implies, shared with HtmlElementPositions so both build one tree.
    // Before a start tag: a paragraph or block closes an open p (outside tables), an li an open li.
    internal static bool StartClosesParagraph(string top, string name, bool inTable)
        => !inTable && top == "p" && (ParagraphTag(name) || Container(name));
    internal static bool StartClosesListItem(string top, string name) => name == "li" && top == "li";
    // Before an end tag: a block's end tag closes an open p (outside tables), a list's an open li.
    internal static bool EndClosesParagraph(string top, string name, bool inTable)
        => !inTable && top == "p" && name != "p" && Container(name);
    internal static bool EndClosesListItem(string top, string name) => ListTag(name) && top == "li";
    // header, footer, main, nav, aside, hgroup, search and figcaption are blocks with no user-agent style beyond
    // display:block, so they render exactly like div, section and article; figure renders like
    // blockquote (the HTML user-agent sheet gives both the same margins).
    // dl, dt and dd are blocks like div; dl adds the paragraph-like block margins and dd the start
    // indent of the HTML user-agent sheet (see CssStylesheets).
    internal static bool Container(string name) => name is "div" or "section" or "article" or "header" or "footer"
        or "main" or "nav" or "aside" or "hgroup" or "search" or "body" or "html" or "ol" or "ul" or "menu" or "li" or "blockquote" or "figure" or "figcaption" or "address" or "dl" or "dt" or "dd";
    private static bool BoxTag(string name) => name is "img" or "svg" or "hr" or "table" or "col" or "td" or "th" or "caption"
        || ParagraphTag(name) || Container(name) && name != "html";

    // Inline text elements: no box of their own, only a text style. code, kbd and samp keep the
    // supplied font (FactsPDF has no generic monospace face, as for pre); var, cite and dfn are
    // italic, as the HTML rendering section's user-agent sheet makes them. time and data have no
    // default style; their machine-readable datetime and value attributes are metadata only.
    // q is inline text wrapped in the quotation marks of the user-agent sheet's quotes:auto for
    // English: curly double quotes, and curly single quotes for every nested q. bdi only isolates
    // its text direction, and every run is laid out left to right, so it renders like span.
    internal static bool InlineTag(string name) => name is "span" or "b" or "strong" or "i" or "em"
        or "small" or "u" or "s" or "del" or "ins" or "a" or "code" or "kbd" or "samp" or "var" or "cite" or "dfn"
        or "time" or "data" or "abbr" or "q" or "bdi";

    // The quotation mark a q at this nesting level opens or closes (CSS quotes for English:
    // the outer pair, then the inner pair for every deeper level). Text in the built-in font
    // (normal text with no supplied font chain) is ASCII-only, so it gets the ASCII marks.
    // Index loops: LINQ over the frame list boxes its enumerator per call on .NET 8.
    private static bool AnyFrame(List<Frame> stack, Func<Frame, bool> predicate)
    {
        for (var i = 0; i < stack.Count; i++) if (predicate(stack[i])) return true;
        return false;
    }

    private static int CountFrames(List<Frame> stack, Func<Frame, bool> predicate)
    {
        var count = 0;
        for (var i = 0; i < stack.Count; i++) if (predicate(stack[i])) count++;
        return count;
    }

    private static string Quote(List<Frame> stack, bool open, PdfOptions options)
    {
        var outer = true;
        for (var i = stack.Count - 2; i >= 0; i--)
            if (stack[i].Name == "q") { outer = false; break; }
        if (options.Fonts.Count == 0 && FontRoles.Role(stack[^1].Style) == FontRole.Normal)
            return outer ? "\"" : "'";
        return outer ? open ? "\u201C" : "\u201D" : open ? "\u2018" : "\u2019";
    }

    /// <summary>
    /// The open elements the document reader will hold, replayed while the collecting pass reads
    /// the tokens, so a nesting flood fails with FPDF1003 before the rest of the page is tokenized
    /// (R1c1). It mirrors the reader's pushes and implicit pops (an open <c>p</c> closed by a
    /// block, an open <c>li</c> by the next <c>li</c> or the list's end) and is never deeper than
    /// the reader's stack, so it only fails documents the reader would fail at the same tag. It
    /// stops checking at the first table (where the reader keeps an open <c>p</c>), unsupported
    /// element or unmatched end tag, leaving those to the reader and its own error codes. Because
    /// this pass runs before the reader, a reader-only error (an unsupported attribute, a nesting
    /// rule) on the same or an earlier tag is reported as FPDF1003 instead, as FPDF1002 and CSS
    /// errors from this pass already are.
    /// </summary>
    internal sealed class OpenElements(int maxDepth)
    {
        private readonly List<string> open = [];
        private bool stopped;

        public void Read(HtmlToken token)
        {
            if (stopped || token.Kind is not (HtmlTokenKind.Start or HtmlTokenKind.End)) return;
            var name = token.Value;
            if (name is "meta" or "link" or "base" or "br") return; // the reader skips them before its depth check
            if (name is "style" or "img" or "hr" or "col")
            {
                // Checked by the reader like any element, but never pushed.
                if (token.Kind == HtmlTokenKind.Start) Check(token);
                return;
            }
            if (name == "table" || !(ParagraphTag(name) || Container(name) || InlineTag(name)
                || TableMarkup.IsTag(name) || name is "head" or "title"))
            {
                stopped = true;
                return;
            }
            var top = open.Count == 0 ? null : open[^1];
            if (token.Kind == HtmlTokenKind.End)
            {
                if (top == "p" && name != "p" && Container(name)) top = PopAndPeek();
                if (ListTag(name) && top == "li") top = PopAndPeek();
                if (top != name) { stopped = true; return; }
                open.RemoveAt(open.Count - 1);
                return;
            }
            if (token.SelfClosing) { stopped = true; return; } // FPDF1101 in the reader
            if ((ParagraphTag(name) || Container(name)) && top == "p") top = PopAndPeek();
            if (name == "li" && top == "li") PopAndPeek();
            Check(token);
            open.Add(name);
        }

        // The reader's stack also holds the document root: it fails when 1 + open > MaxDepth.
        private void Check(HtmlToken token)
        {
            if (open.Count >= maxDepth) throw new FactsPdfException("FPDF1003", "Nesting limit exceeded.", token.Offset);
        }

        private string? PopAndPeek()
        {
            open.RemoveAt(open.Count - 1);
            return open.Count == 0 ? null : open[^1];
        }
    }

    // Keep the old internal paragraph view for existing editor/tests; actual
    // conversions now receive a structured document via ReadTree.
    public static List<Paragraph> Read(string html, PdfOptions options, CancellationToken cancellation)
        => BlockTreeTraversal.Paragraphs(ReadTree(html, options, cancellation), cancellation).ToList();

    // The last copy made for a tag whose only defaults are the resets, bold, italic and the
    // paragraph margin. The next element with the same parent and the same values shares it.
    private sealed class SharedDefaults
    {
        public TextStyle? Parent;
        public TextStyle? Style;
    }

    // The element's style before author CSS: the parent's computed style with non-inherited text
    // properties reset and the tag's own defaults. Returns the shared parent itself when nothing
    // differs, a copy shared with earlier siblings when only the resets, bold, italic or the
    // paragraph margin differ, and otherwise one private copy holding every change (see TextStyle).
    // Owned is true only for a private copy; a shared style is copied before any declaration.
    private static (TextStyle Style, bool Owned) ElementDefaults(TextStyle parent, string name, SharedDefaults shared)
    {
        var marginAfter = ParagraphTag(name) ? 8 : 0;
        var bold = parent.Bold || name is "b" or "strong" or "th";
        var italic = parent.Italic || name is "i" or "em" or "var" or "cite" or "dfn" or "address";
        var heading = name.Length == 2 && name[0] == 'h' && name[1] is >= '1' and <= '6';
        if (parent.MarginBefore == 0 && parent.MarginAfter == marginAfter &&
            parent.PercentMarginBefore is null && parent.PercentMarginAfter is null && !parent.BreakBefore && !parent.BreakAfter
            && !parent.KeepWithNext && bold == parent.Bold && italic == parent.Italic && !heading
            && name is not ("small" or "a" or "u" or "ins" or "s" or "del" or "pre"))
            return (parent, false);
        var plain = !heading && name is not ("small" or "a" or "u" or "ins" or "s" or "del" or "pre");
        if (plain && ReferenceEquals(shared.Parent, parent) && shared.Style!.MarginAfter == marginAfter
            && shared.Style.Bold == bold && shared.Style.Italic == italic)
            return (shared.Style, false);
        var style = parent with { };
        style.MarginBefore = 0;
        style.MarginAfter = marginAfter;
        style.PercentMarginBefore = null;
        style.PercentMarginAfter = null;
        style.BreakBefore = false;
        style.BreakAfter = false;
        style.KeepWithNext = false;
        style.Bold = bold;
        style.Italic = italic;
        // Real inline report editing semantics, rendered with existing
        // measured glyphs and PDF decoration strokes. CSS can override
        // these tag defaults through the normal cascade.
        if (name == "small") style.FontSize *= 5d / 6d;
        if (name == "a") { style.Color = new Rgb(0, 0, 0.8); style.Decoration = TextDecoration.Underline; }
        if (name is "u" or "ins") style.Decoration = TextDecoration.Underline;
        if (name is "s" or "del") style.Decoration = TextDecoration.LineThrough;
        if (name == "pre")
        {
            style.NoWrap = true;
            style.PreserveLineBreaks = true;
            style.PreserveSpaces = true;
        }
        if (heading)
            style.FontSize *= name[1] switch { '1' => 2, '2' => 1.7, '3' => 1.5, '4' => 1.3, '5' => 1.15, _ => 1 };
        if (!plain) return (style, true);
        shared.Parent = parent;
        shared.Style = style;
        return (style, false);
    }

    internal static DocumentRoot ReadTree(string html, PdfOptions options, CancellationToken cancellation)
    {
        // One token list per thread is reused across documents. It is taken out of its slot while
        // in use, so a read that starts during another on the same thread gets a list of its own.
        var tokens = pooledTokens ?? new List<HtmlToken>();
        pooledTokens = null;
        // The child stack is pooled the same way.
        var children = pooledChildren ?? new List<BlockChild>();
        pooledChildren = null;
        try { return ReadTree(html, options, tokens, children, cancellation); }
        finally
        {
            // Cleared before pooling so the list never keeps a finished document's tokens alive.
            tokens.Clear();
            if (tokens.Capacity <= MaxPooledTokens) pooledTokens = tokens;
            children.Clear();
            if (children.Capacity <= MaxPooledChildren) pooledChildren = children;
        }
    }

    // The children of every open block, in document order: an open block's children are the tail
    // from its frame's ChildStart, because children are only ever added to the innermost open
    // block. When the block closes they move into one exact-size array (or its lone-child slot),
    // so no block grows its own array by doubling and leaves the smaller copies behind.
    internal const int MaxPooledChildren = 8192;
    [ThreadStatic] private static List<BlockChild>? pooledChildren;

    /// <summary>The calling thread's pooled child stack, for tests: its count is always zero
    /// between reads.</summary>
    internal static (int Count, int Capacity)? PooledChildren =>
        pooledChildren is { } list ? (list.Count, list.Capacity) : null;

    // Keeps the pooled array under the large-object threshold (one 8-byte reference per token).
    internal const int MaxPooledTokens = 8192;
    [ThreadStatic] private static List<HtmlToken>? pooledTokens;

    /// <summary>The calling thread's pooled token list, for tests: its count is always zero
    /// between reads.</summary>
    internal static (int Count, int Capacity)? PooledTokens =>
        pooledTokens is { } list ? (list.Count, list.Capacity) : null;

    private static DocumentRoot ReadTree(string html, PdfOptions options, List<HtmlToken> tokens,
        List<BlockChild> children, CancellationToken cancellation)
    {
        // Compile every source before computing styles: a later style block can affect earlier content.
        // The tokens are read once, here, and walked again below.
        var sheets = CssStylesheets.Collect(html, options, tokens, cancellation);
        sheets.TrackPositions(tokens, cancellation);
        var root = new TextStyle(options.FontSize, 1.2, new Rgb(0, 0, 0), TextAlignment.Left);
        var sharedDefaults = new SharedDefaults();
        // The last shared th/caption default and its centred copy (see the user-agent centring below).
        TextStyle? centredSource = null, centredStyle = null;
        var rootFontSize = root.FontSize; // Actual html computed size or configured initial size.
        var stack = new List<Frame> { new("#root", root, BoxStyle.Initial, null) };
        var path = new List<CssElement>(); // Actual elements only; the synthetic document root is not selectable.
        var result = new DocumentRoot { PageRules = sheets.PageRules };
        Paragraph? current = null;
        TableMarkup? table = null;
        var anonymous = false;
        var elements = 0; var insideStyle = false; var baseSeen = false;
        SvgDocument? svg = null;
        // html, head and body frames the HTML parser implied (HtmlImpliedElements): they are part of
        // the tree selectors see, but no element or depth limit counts them.
        var impliedOpen = 0; var impliedHead = false;

        // Where the innermost open block's children begin in the child stack (0 for the root).
        int ChildStart(int fromFrame)
        {
            for (var i = fromFrame; i >= 0; i--)
                if (stack[i].Box is not null) return stack[i].ChildStart;
            return 0;
        }

        void Flush()
        {
            if (current is not null)
            {
                if (table?.Cell is { } cell)
                {
                    // Formatting whitespace and empty inline wrappers are not
                    // anonymous flow items. Explicit p margins are retained.
                    if (!anonymous || current.Runs.HasBreak() || TextLines.HasTextContent(current, cancellation))
                        cell.Contents.Add(new TableCellText(current));
                }
                else
                {
                    current.IsAnonymous = anonymous;
                    children.Add(current);
                }
            }
            current = null;
            anonymous = false;
        }
        void Pop()
        {
            var closing = stack[^1];
            if (closing.Box is { BoxStyle.DisplayNone: true } hidden)
            {
                // Validate the hidden subtree normally, then remove it as a
                // single block. This keeps it out of heading lookahead, font
                // resolution, page layout and PDF paint/byte budgets.
                // Its own children are dropped with it; its siblings are the parent's tail.
                children.RemoveRange(closing.ChildStart, children.Count - closing.ChildStart);
                var siblingStart = ChildStart(stack.Count - 2);
                if (children.Count == siblingStart || !ReferenceEquals(children[^1], hidden))
                    throw new InvalidOperationException("Hidden report block is not the current sibling.");
                children.RemoveAt(children.Count - 1);
                // Hiding a block must not leave a phantom paragraph boundary
                // between adjacent direct text. The block's opening tag
                // flushed the preceding anonymous paragraph; resume it here
                // so subsequent direct text/inline runs behave exactly as
                // if the hidden block had never existed.
                if (current is null && table is null && children.Count > siblingStart
                    && children[^1] is Paragraph { IsAnonymous: true } before)
                {
                    children.RemoveAt(children.Count - 1);
                    current = before;
                    anonymous = true;
                }
            }
            else closing.Box?.TakeChildren(children, closing.ChildStart);
            stack.RemoveAt(stack.Count - 1);
            path.RemoveAt(path.Count - 1);
        }
        // The open element's ::after: inline text joins its content, a block box is its last child.
        void AfterText() { if (stack[^1].After is GeneratedText text) Add(text.Text, text.Style); }
        void AfterBox() { if (stack[^1].After is BlockNode box) children.Add(box); }
        void Close() { AfterText(); Flush(); AfterBox(); Pop(); }
        // Computes an element's ::before or ::after (the element is the last entry of path) and
        // returns the box it generates: a block box (in a flex container, or with display:block or
        // flex), inline text, or null when content is none or normal or display is none.
        object? Generate(CssPseudoElement pseudo, string name, TextStyle style, BoxStyle box, int offset)
        {
            // A replaced image generates no ::before or ::after box in a browser.
            if (name == "img") return null;
            var (display, content) = sheets.PseudoKeys(path, pseudo);
            if (content is null || content[0] != '"' || display == "none") return null;
            var block = box.HasItems || display is "block" or "flex";
            if (display is not (null or "block" or "flex" or "inline"))
                throw new FactsPdfException("FPDF1201",
                    "::before and ::after support only display inline, block, flex or none.", offset);
            if (!(Container(name) || ParagraphTag(name) || InlineTag(name) || name is "td" or "th")
                || name is "html" or "q")
                throw new FactsPdfException("FPDF1201",
                    "::before and ::after content is supported only on report blocks, list items, paragraphs, inline text and table cells.", offset);
            if (block && (!Container(name) || table is not null))
                throw new FactsPdfException("FPDF1201",
                    "A block ::before or ::after box is supported only in report panels, list items and body outside tables.", offset);
            var (defaults, owned) = ElementDefaults(style, block ? "div" : "span", sharedDefaults);
            var (generatedStyle, generatedBox) = sheets.Compute(defaults, owned, style, root, rootFontSize, box, path,
                paragraph: false, block: block, offset, default, pseudo: pseudo, pseudoName: block ? "div" : "span");
            if (generatedStyle.FontSize is < 1 or > 144)
                throw new FactsPdfException("FPDF1202", "Computed font size is outside the supported range.", offset);
            var text = content[1..];
            if (!block) return new GeneratedText(text, generatedStyle);
            var node = new BlockNode("div", generatedStyle, generatedBox, offset);
            if (text.Length > 0)
            {
                var paragraph = new Paragraph(generatedStyle, offset) { IsAnonymous = true };
                paragraph.Runs.Add(new(text, generatedStyle, false, offset));
                node.Add(paragraph);
            }
            return node;
        }
        void Add(string text, TextStyle style, bool lineBreak = false)
        {
            // Inherited pre/pre-wrap spaces (and pre-line authored breaks) are
            // meaningful even before the first inline run of an anonymous
            // report paragraph. Retain them for the same PDF measurement and
            // tab/control validation used by explicitly styled paragraphs.
            if (current is null && !lineBreak && HtmlTokens.AllSpace(text) &&
                !style.PreserveSpaces && !style.PreserveLineBreaks) return;
            if (current is null)
            {
                // Preserve the existing text-layout paragraph style for a
                // first-inline-run span (including text-align). The retained
                // BlockNode.Style separately stores the actual containing-box
                // computed style for future block-aware layout.
                current = table?.Cell is { } cell
                    ? new Paragraph(cell.Style, cell.SourceOffset)
                    : new Paragraph(stack[^1].Style, stack[^1].SourceOffset);
                anonymous = true;
            }
            // Keep is a block hint, never part of glyph-run coalescing.
            current.Runs.Add(new(text, style.KeepWithNext ? style with { KeepWithNext = false } : style, lineBreak, stack[^1].SourceOffset));
        }
        // An img or svg with display:inline is an atomic inline in the current paragraph, aligned
        // to (and linked by) its parent inline; display:block makes it a block box, which cannot
        // sit inside a paragraph, inline element or list here.
        void AddReplaced(ImageNode node, string name, int offset)
        {
            // A flex or grid item is blockified (CSS Display 3), so a replaced child of a flex or
            // grid container is a block box whatever its display.
            if (node.BoxStyle.DisplayBlock
                || table is null && stack[^1].Box is { BoxStyle.IsFlexContainer: true } or { BoxStyle.IsGridContainer: true })
            {
                if (AnyFrame(stack, f => ParagraphTag(f.Name) || InlineTag(f.Name) || ListTag(f.Name) || f.Name == "li"))
                    throw new FactsPdfException("FPDF1101", name == "svg"
                        ? "An svg with display:block must be a standalone block outside paragraphs, inline elements and lists."
                        : "An img with display:block must be a standalone block outside paragraphs, inline elements and lists.", offset);
                Flush();
                if (table?.Cell is { } cell) cell.Contents.Add(new TableCellImage(node));
                else children.Add(node);
                return;
            }
            // Padding, borders and a background are part of the inline box (TextLines.AppendLine);
            // margins would need margin-box line layout.
            var inlineBox = node.BoxStyle;
            if (inlineBox.MarginTop.Value != 0 || inlineBox.MarginRight.Value != 0 || inlineBox.MarginBottom.Value != 0
                || inlineBox.MarginLeft.Value != 0 || inlineBox.MarginLeftAuto || inlineBox.MarginRightAuto
                || inlineBox.HasRoundedCorners || inlineBox.Outline.EffectiveWidth > 0)
                throw new FactsPdfException("FPDF1201",
                    $"Margins, rounded corners and outlines on an inline {name} are not supported; use display:block.", offset);
            var parentStyle = stack[^1].Style;
            if (current is null)
            {
                current = table?.Cell is { } cell
                    ? new Paragraph(cell.Style, cell.SourceOffset)
                    : new Paragraph(parentStyle, stack[^1].SourceOffset < 0 ? offset : stack[^1].SourceOffset);
                anonymous = true;
            }
            current.Runs.Add(new InlineImageRun(node, parentStyle.KeepWithNext ? parentStyle with { KeepWithNext = false } : parentStyle));
        }

        for (var t = 0; t < tokens.Count; t++)
        {
            var token = tokens[t];
            cancellation.ThrowIfCancellationRequested();
            if (token.Kind == HtmlTokenKind.StyleText) continue;
            if (token.Kind == HtmlTokenKind.Text)
            {
                if (stack[^1].Name == "title") continue;
                if (stack[^1].Name == "head")
                {
                    if (!HtmlTokens.AllSpace(token.Value))
                        throw new FactsPdfException("FPDF1101", "Non-metadata text inside head is not supported.", token.Offset);
                    continue;
                }
                var decoded = HtmlTokens.Decode(token.Value, token.Offset);
                if (ListTag(stack[^1].Name) || table is not null && table.Cell is null)
                {
                    if (!HtmlTokens.AllSpace(decoded))
                        throw new FactsPdfException("FPDF1101", "Structural list/table elements cannot contain direct text.", token.Offset);
                    continue;
                }
                Add(decoded, stack[^1].Style);
                continue;
            }
            var name = token.Value;
            var implied = token is ImpliedHtmlToken;
            if (implied && token.Kind == HtmlTokenKind.Start) { impliedOpen++; impliedHead |= name == "head"; }
            else if (token.Kind == HtmlTokenKind.End && name == "head" && impliedHead) { impliedOpen--; impliedHead = false; }
            if (name == "style")
            {
                if (ListTag(stack[^1].Name) || table is not null)
                    throw new FactsPdfException("FPDF1101", "style cannot be a direct list child or table descendant.", token.Offset);
                if (token.Kind == HtmlTokenKind.End)
                {
                    if (!insideStyle) throw new FactsPdfException("FPDF1101", "Unexpected style end tag.", token.Offset);
                    insideStyle = false; continue;
                }
                if (++elements > options.MaxElements) throw new FactsPdfException("FPDF1002", "Element limit exceeded.", token.Offset);
                if (!(stack[^1].Name is "#root" or "head" || Container(stack[^1].Name)))
                    throw new FactsPdfException("FPDF1101", "style must be at document, head or container level.", token.Offset);
                if (stack.Count - impliedOpen > options.MaxDepth) throw new FactsPdfException("FPDF1003", "Nesting limit exceeded.", token.Offset);
                insideStyle = true; continue;
            }
            if (!(ParagraphTag(name) || Container(name) || InlineTag(name) || TableMarkup.IsTag(name)
                || name is "img" or "svg" or "hr" or "br" or "head" or "title" or "meta" or "link" or "base"))
                throw new FactsPdfException("FPDF1102", $"Element '{name}' is not supported in this development slice.", token.Offset);
            if (token.Kind == HtmlTokenKind.End)
            {
                if (EndClosesParagraph(stack[^1].Name, name, table is not null)) Close();
                if (EndClosesListItem(stack[^1].Name, name)) Close();
                if (stack.Count == 1 || stack[^1].Name != name)
                    throw new FactsPdfException("FPDF1101", $"Unexpected end tag '{name}'.", token.Offset);
                if (TableMarkup.IsTag(name))
                {
                    if (name is "td" or "th" or "caption") { AfterText(); Flush(); }
                    table!.End(name, token.Offset);
                    if (name == "table") table = null;
                    Pop(); continue;
                }
                AfterText();
                if (ParagraphTag(name) || Container(name)) Flush();
                AfterBox();
                if (name == "q") Add(Quote(stack, open: false, options), stack[^1].Style);
                if (stack[^1].FlexItem) Flush();
                Pop(); continue;
            }
            if (!implied && ++elements > options.MaxElements) throw new FactsPdfException("FPDF1002", "Element limit exceeded.", token.Offset);
            // A stylesheet link fails as an unsupported style source wherever it appears, before
            // any placement check.
            if (name == "link")
                foreach (var (attribute, value) in token.Attributes!)
                    if (attribute == "rel") LinkRelations(value, token.Offset);
            // An svg is foreign content, where a self-closing tag closes the element.
            if (token.SelfClosing && name is not ("img" or "svg" or "hr" or "br" or "meta" or "link" or "base" or "col"))
                throw new FactsPdfException("FPDF1101", "Non-void HTML tags cannot be XML-self-closed in this subset.", token.Offset);
            if (name == "table" && (table is not null || AnyFrame(stack, f => ParagraphTag(f.Name) || InlineTag(f.Name) || ListTag(f.Name) || f.Name == "li")))
                throw new FactsPdfException("FPDF1101", "Tables must be standalone blocks outside text elements, lists and tables.", token.Offset);
            if (TableMarkup.IsTag(name) && name != "table" && table is null)
                throw new FactsPdfException("FPDF1101", "Table structural elements require their explicit table parents.", token.Offset);
            // A preformatted report block is an explicitly closed, non-nested
            // paragraph outside lists and tables. Use the ordinary paragraph
            // renderer without inventing tab stops or a font-family.
            if (name == "pre" && (table is not null ||
                AnyFrame(stack, f => ListTag(f.Name) || f.Name == "li")))
                throw new FactsPdfException("FPDF1101",
                    "pre must be a standalone report block outside lists and tables.", token.Offset);
            // Quoted report panels reuse ordinary block layout, but may not
            // acquire unsupported list/table-cell nesting semantics.
            // figure has the same user-agent margins as blockquote and the same limits.
            // dl and dd carry user-agent margins too, so they share those limits.
            if (name is "blockquote" or "figure" or "dl" or "dd" && (table is not null ||
                AnyFrame(stack, f => ListTag(f.Name) || f.Name == "li")))
                throw new FactsPdfException("FPDF1101", name + " must be outside lists and tables.", token.Offset);
            // Standalone void report separator, never a table/list/inline child.
            if (name == "hr" && (table is not null ||
                AnyFrame(stack, f => ParagraphTag(f.Name) || InlineTag(f.Name) ||
                    ListTag(f.Name) || f.Name == "li")))
                throw new FactsPdfException("FPDF1101",
                    "hr must be a standalone block outside paragraphs, lists and tables.", token.Offset);
            table?.CheckChild(stack[^1].Name, name, token.Offset);
            if (StartClosesParagraph(stack[^1].Name, name, table is not null)) Close();
            if (StartClosesListItem(stack[^1].Name, name)) Close();
            if ((ParagraphTag(name) || Container(name)) && AnyFrame(stack, f => ParagraphTag(f.Name) || InlineTag(f.Name)))
                throw new FactsPdfException("FPDF1101", "Unsupported block/inline nesting.", token.Offset);
            if (ListTag(stack[^1].Name) && name != "li" || name == "li" && !ListTag(stack[^1].Name))
                throw new FactsPdfException("FPDF1101", "li must be a direct child of ol/ul, whose only element children are li.", token.Offset);
            if (ListTag(name) && CountFrames(stack, f => ListTag(f.Name)) >= 2)
                throw new FactsPdfException("FPDF1101", "Lists support at most two nested levels.", token.Offset);
            if (ListTag(name)) result.HasLists = true;
            if (stack[^1].Name == "title" || (stack[^1].Name == "head" && name is not ("title" or "meta" or "link" or "base")))
                throw new FactsPdfException("FPDF1101", "Unsupported head/title content.", token.Offset);
            if (name == "head" && stack[^1].Name is not ("html" or "#root"))
                throw new FactsPdfException("FPDF1101", "head must be at document level.", token.Offset);
            if (name is "title" or "meta" or "link" or "base" && stack[^1].Name != "head")
                throw new FactsPdfException("FPDF1101", "title/meta/link/base must be inside head.", token.Offset);
            if (name == "base")
            {
                if (baseSeen) throw new FactsPdfException("FPDF1101", "A document may have only one base element.", token.Offset);
                baseSeen = true;
            }

            var parent = stack[^1].Style;
            var parentBox = stack[^1].BoxStyle;
            var boxStyle = BoxStyle.Initial;
            var (style, styleOwned) = ElementDefaults(parent, name, sharedDefaults);
            // The HTML user-agent sheet's th and caption {text-align:-internal-center}: centred when
            // the parent's computed text-align is its initial value (start), otherwise the
            // parent's alignment is inherited as usual. Author declarations still win below.
            var parentAlignStart = stack[^1].AlignStart;
            if (name is "th" or "caption" && parentAlignStart && style.Alignment != TextAlignment.Center)
            {
                // A shared default stays shared: sibling cells reuse one centred copy of it.
                if (styleOwned) style.Alignment = TextAlignment.Center;
                else if (ReferenceEquals(centredSource, style)) style = centredStyle!;
                else
                {
                    centredSource = style;
                    style = style with { Alignment = TextAlignment.Center };
                    centredStyle = style;
                }
            }
            var columnSpan = 1;
            string? href = null;
            var titled = false;
            var hidden = false;
            var linkRel = false;
            var baseHrefOrTarget = false;
            int? listStart = null, listValue = null;
            var listReversed = false;
            ListMarkerKind? listTypeHint = null;
            foreach (var (attribute, value) in token.Attributes!)
            {
                if (attribute == "style")
                {
                    if (name is "head" or "title" or "meta" or "link" or "base" or "br")
                        throw new FactsPdfException("FPDF1103", "Styling this metadata/void element is not supported.", token.Offset);
                    // Declarations were parsed once during source collection.
                }
                else if (attribute == "href" && name == "a")
                    href = PdfLinkAnnotations.ValidateUri(value, token.Offset);
                // A PDF link annotation always opens its URI outside the document, so the browsing
                // context (target) and the hyperlink relations (noopener, nofollow, ...) change nothing.
                else if (name == "a" && attribute == "target") AnchorTarget("a", value, token.Offset);
                // base only sets the URL that relative URLs resolve against and the default link
                // target. Links must already be absolute and images are named resources, so neither
                // is ever resolved; the href is never read, and target is held to the a rule.
                else if (name == "base" && attribute == "href") baseHrefOrTarget = true;
                else if (name == "base" && attribute == "target")
                {
                    AnchorTarget("base", value, token.Offset);
                    baseHrefOrTarget = true;
                }
                else if (name == "a" && attribute == "rel") AnchorRelations(value, token.Offset);
                else if (attribute == "colspan" && name is "td" or "th")
                    columnSpan = ParseColumnSpan(value, table!.Node.Columns.Count == 0 ? TableMarkup.MaxColumns : table.Node.Columns.Count,
                        token.Offset, cancellation);
                else if (attribute == "start" && name == "ol")
                    listStart = ParseListOrdinal(value, token.Offset, cancellation);
                else if (attribute == "value" && name == "li" && stack[^1].Name == "ol")
                    listValue = ParseListOrdinal(value, token.Offset, cancellation);
                else if (attribute == "reversed" && name == "ol")
                {
                    if (value.Length != 0 && !value.Equals("reversed", StringComparison.OrdinalIgnoreCase))
                        throw new FactsPdfException("FPDF1103",
                            "ol reversed accepts only an empty boolean attribute or 'reversed'.", token.Offset);
                    listReversed = true;
                }
                // A presentational hint below every author declaration (see CssStylesheets.Compute).
                else if (attribute == "type" && name is "ol" or "ul" or "li")
                    listTypeHint = ListMarkerTypes.TypeAttribute(name, value, token.Offset);
                else if (name == "meta" && attribute == "charset" && value.Equals("utf-8", StringComparison.OrdinalIgnoreCase)) { }
                // Named and Open Graph metadata (viewport, description, author, generator, og:title)
                // is document metadata a static page render never reads. color-scheme is the one
                // standard name that changes the rendered canvas and text colours, so it still fails.
                else if (name == "meta" && attribute is "name" or "property" or "content")
                {
                    // Trimmed, so a padded name fails rather than guessing whether a browser trims it.
                    if (attribute == "name" && (value.AsSpan().Trim().Equals("color-scheme", StringComparison.OrdinalIgnoreCase)
                        || value.AsSpan().Trim().Equals("supported-color-schemes", StringComparison.OrdinalIgnoreCase)))
                        throw new FactsPdfException("FPDF1103", "meta color-scheme is not supported.", token.Offset);
                }
                // A link that only names a related resource (icon, canonical, alternate, preconnect,
                // manifest) is never fetched or painted by a static render. A stylesheet link would
                // change the render, and external resources are never loaded, so it fails FPDF1204 like @import.
                else if (name == "link" && attribute == "rel") linkRel = true; // validated before placement
                else if (name == "link" && attribute is "href" or "type" or "sizes" or "hreflang" or "crossorigin"
                    or "as" or "color" or "integrity" or "referrerpolicy") { }
                else if (name == "img" && attribute is "src" or "alt") { }
                // The svg's own attributes (size, viewBox, paint, accessibility) are read by SvgReader.
                else if (name == "svg" && SvgReader.RootAttribute(attribute)) { }
                else if (name == "time" && attribute == "datetime" || name == "data" && attribute == "value"
                    || name == "q" && attribute == "cite") { }
                // Every run is laid out left to right, so the left-to-right direction is already in
                // effect; any other direction (rtl, auto) still fails below.
                else if (attribute == "dir" && value.Equals("ltr", StringComparison.OrdinalIgnoreCase)) { }
                // The user-agent rule [hidden]{display:none}: accepted only where display:none is
                // (ordinary report blocks outside lists and tables); until-found would need find-in-page.
                else if (attribute == "hidden")
                {
                    if (value.Length != 0 && !value.Equals("hidden", StringComparison.OrdinalIgnoreCase))
                        throw new FactsPdfException("FPDF1103", "hidden accepts only an empty boolean attribute or 'hidden'.", token.Offset);
                    if (!(ParagraphTag(name) || Container(name) && name is not ("body" or "html" or "ol" or "ul" or "menu" or "li"))
                        || table is not null || AnyFrame(stack, f => ListTag(f.Name) || f.Name == "li"))
                        throw new FactsPdfException("FPDF1103",
                            "hidden is supported only on ordinary report blocks outside lists and tables.", token.Offset);
                    hidden = true;
                }
                else if (attribute == "title") titled = true;
                else if (attribute is "id" or "class" or "lang" || attribute.StartsWith("data-", StringComparison.Ordinal)) { }
                else throw new FactsPdfException("FPDF1103", $"Attribute '{attribute}' is not supported.", token.Offset);
            }
            if (name == "a")
            {
                if (href is null) throw new FactsPdfException("FPDF1103", "a requires a bounded HTTPS or mailto href.", token.Offset);
                if (AnyFrame(stack, frame => frame.Name == "a"))
                    throw new FactsPdfException("FPDF1101", "Nested links are not supported.", token.Offset);
                style.LinkUri = href; // ElementDefaults always returns a private copy for an a element.
            }
            // abbr[title] { text-decoration: dotted underline } in the HTML rendering section's
            // user-agent sheet; an abbr without a title has no default style. CSS can override it.
            if (name == "abbr" && titled)
            {
                if (!styleOwned) { style = style with { }; styleOwned = true; }
                style.Decoration = TextDecoration.Underline;
                style.DecorationPattern = TextDecorationPattern.Dotted;
            }
            if (name == "link")
            {
                if (!linkRel) throw new FactsPdfException("FPDF1103", "link requires a rel attribute.", token.Offset);
                continue;
            }
            if (name == "base")
            {
                if (!baseHrefOrTarget) throw new FactsPdfException("FPDF1103", "base requires an href or target attribute.", token.Offset);
                continue;
            }
            if (name == "meta") continue;
            path.Add(CssElement.From(token, t));
            if (name is not ("head" or "title"))
                (style, boxStyle) = sheets.Compute(style, styleOwned, parent, root, rootFontSize, parentBox, path,
                    ParagraphTag(name), BoxTag(name),
                    token.Offset, (token as StyledHtmlToken)?.InlineDeclarations ?? default, allowStyling: name != "br", hidden: hidden, listTypeHint: listTypeHint);
            if (style.FontSize is < 1 or > 144) throw new FactsPdfException("FPDF1202", "Computed font size is outside the supported range.", token.Offset);
            var winningTextAlign = name is "head" or "title" ? null : sheets.LastTextAlign;
            var alignStart = winningTextAlign switch
            {
                null => parentAlignStart && name is not ("th" or "caption"),
                "start" or "initial" => true,
                "inherit" or "unset" => parentAlignStart,
                _ => false
            };
            if (name == "html")
            {
                rootFontSize = style.FontSize;
                result.RootStyle = style;
                if (boxStyle.IsFlexContainer) result.FlexRootOffset = token.Offset;
            }
            if (name == "br") { path.RemoveAt(path.Count - 1); Add("", parent, true); continue; }
            if (stack.Count - impliedOpen > options.MaxDepth) throw new FactsPdfException("FPDF1003", "Nesting limit exceeded.", token.Offset);
            object? before = null, after = null;
            if (sheets.HasPseudoRules && name is not ("head" or "title"))
            {
                before = Generate(CssPseudoElement.Before, name, style, boxStyle, token.Offset);
                after = Generate(CssPseudoElement.After, name, style, boxStyle, token.Offset);
            }
            if (name == "table")
            {
                Flush();
                table = new(new TableNode(style, boxStyle, token.Offset));
                children.Add(table.Node);
                stack.Add(new(name, style, boxStyle, null, token.Offset, 0, alignStart));
                continue;
            }
            if (TableMarkup.IsTag(name))
            {
                table!.Start(name, style, boxStyle, token.Offset, columnSpan);
                if (name == "col") { path.RemoveAt(path.Count - 1); continue; }
                stack.Add(new(name, style, boxStyle, null, token.Offset, 0, alignStart, After: after));
                if (before is GeneratedText cellText) Add(cellText.Text, cellText.Style);
                continue;
            }
            if (name == "img")
            {
                if (!token.Attributes.TryGetValue("src", out var resourceName) || !ImageResources.ValidName(resourceName))
                    throw new FactsPdfException("FPDF1103", "img requires an explicit resource name in src; paths and URLs are unsupported.", token.Offset);
                if (!options.Images.TryGetValue(resourceName, out var image))
                    throw new FactsPdfException("FPDF1604", $"Image resource '{resourceName}' was not supplied.", token.Offset);
                AddReplaced(new ImageNode(image, style, boxStyle, token.Offset, token.Attributes.GetValueOrDefault("alt")),
                    name, token.Offset);
                path.RemoveAt(path.Count - 1); continue;
            }
            if (name == "svg")
            {
                svg ??= new SvgDocument(options.MaxDisplayCommands, cancellation);
                var graphic = SvgReader.Read(tokens, ref t, svg, sheets, path, stack.Count - impliedOpen, options.MaxDepth, style.Color);
                AddReplaced(new ImageNode(null, style, SvgSizing(boxStyle, graphic), token.Offset, null, graphic),
                    name, token.Offset);
                path.RemoveAt(path.Count - 1); continue;
            }
            if (name == "hr")
            {
                Flush();
                children.Add(new BlockNode(name, style, boxStyle, token.Offset));
                path.RemoveAt(path.Count - 1);
                continue; // void block; no open frame or text runs
            }
            if (ParagraphTag(name) || Container(name)) Flush();
            // `html`, head/title metadata and spans remain structural
            // inheritance/style contexts, never painted block boxes.
            BlockNode? box = null;
            // An implied body with no box declarations (box-sizing alone changes nothing without a
            // width) adds no box, so a page without body tags keeps its read tree; a styled one paints
            // and lays out like an explicit body.
            if (BoxTag(name) && table?.Cell is null
                && !(implied && boxStyle with { BorderBoxSizing = false } == BoxStyle.Initial))
            {
                box = new BlockNode(name, style, boxStyle, token.Offset)
                {
                    ListStart = listStart, ListValue = listValue, ListReversed = listReversed
                };
                // A paragraph element holds at most its own paragraph (block children are rejected
                // or close it first), which the child list keeps in its first slot without an array.
                children.Add(box);
            }
            // Each inline child of a flex or grid container, and each run of text between them, is its own
            // item: close the anonymous paragraph before and after the inline element.
            var flexItem = InlineTag(name) && table is null && stack[^1].Box is { BoxStyle.HasItems: true };
            if (flexItem) Flush();
            stack.Add(new(name, style, boxStyle, box, token.Offset, children.Count, alignStart, flexItem, after));
            if (name == "q") Add(Quote(stack, open: true, options), style);
            if (ParagraphTag(name))
            {
                current = new Paragraph(style, token.Offset, RunCount(tokens, t + 1, name));
                anonymous = false;
            }
            if (before is BlockNode beforeBox) children.Add(beforeBox);
            else if (before is GeneratedText beforeText) Add(beforeText.Text, beforeText.Style);
        }
        // HTML permits omitted p/body/html end tags; other unclosed elements are rejected here.
        while (table is null && stack.Count > 1 && stack[^1].Name is "p" or "body" or "html")
        {
            Close(); // attach under the correct parent BEFORE popping
        }
        if (stack.Count > 1) throw new FactsPdfException("FPDF1101", $"Unclosed element '{stack[^1].Name}'.", html.Length);
        Flush();
        svg?.Resolve(tokens);
        result.Children.TakeChildren(children, 0);
        return result;
    }
    /// <summary>
    /// An svg's width and height attributes are presentational hints: they become its CSS width
    /// and height unless author CSS sets them. Without a viewBox (no intrinsic ratio) a missing
    /// dimension takes the replaced-element default of 300 by 150 px; with one, a missing dimension
    /// follows the viewBox ratio, and with neither attribute the svg fills its containing block's
    /// width, as Chrome lays it out.
    /// </summary>
    private static BoxStyle SvgSizing(BoxStyle box, SvgGraphic graphic)
    {
        var width = box.Width ?? graphic.WidthHint;
        var height = box.Height ?? (graphic.HeightHint is { } hint ? new CssLength(hint) : null);
        if (graphic.ViewBox is null)
        {
            width ??= new CssLength(225);
            height ??= new CssLength(112.5);
        }
        else if (width is null && height is null) width = new CssLength(100, IsPercent: true);
        return width == box.Width && height == box.Height ? box : box with { Width = width, Height = height };
    }

    // The runs a paragraph element opened just before `start` will hold: one per text token, one
    // per br, img and svg and one per q start or end tag (its quotation mark) until its end tag, or until a block start or end that closes or rejects it. The
    // count is exact for a well-formed paragraph and only sizes the run list, so an input that
    // fails later merely over-counts.
    private static int RunCount(List<HtmlToken> tokens, int start, string name)
    {
        var count = 0;
        for (var i = start; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (token.Kind == HtmlTokenKind.Text) { count++; continue; }
            if (token.Kind == HtmlTokenKind.StyleText) continue;
            var tag = token.Value;
            if (token.Kind == HtmlTokenKind.End && tag == name) break;
            if (ParagraphTag(tag) || Container(tag) || TableMarkup.IsTag(tag) || tag is "hr" or "style") break;
            if (token.Kind == HtmlTokenKind.Start && tag is "br" or "img" or "svg" || tag == "q") count++;
        }
        return count;
    }

    // Bounded positive ASCII decimal list ordinals only. Reject signs,
    // zero, overflow and decimal/exponent spellings instead of silently
    // truncating or wrapping generated PDF marker text.
    // Link types that name a related resource or a connection hint. None is fetched or painted when a
    // page renders to paper; stylesheet (and anything unknown) still fails.
    private static readonly SpanStringSet MetadataLinkTypes = new([
        "icon", "shortcut", "apple-touch-icon", "apple-touch-icon-precomposed", "mask-icon", "manifest", "canonical",
        "alternate", "author", "license", "help", "search", "next", "prev", "me", "pingback", "amphtml",
        "preconnect", "dns-prefetch", "prefetch", "preload", "modulepreload",
    ]);

    private static readonly SpanStringSet HyperlinkTypes = new([
        "noopener", "noreferrer", "nofollow", "opener", "external", "sponsored", "ugc", "author", "license",
        "help", "bookmark", "tag", "next", "prev", "alternate", "me", "privacy-policy", "terms-of-service", "search",
    ]);

    // _blank, _self, _parent, _top (any case) or a navigable name, which never starts with an underscore.
    private static void AnchorTarget(string element, string value, int offset)
    {
        if (value.Length == 0 || value[0] == '_' && !(value.Equals("_blank", StringComparison.OrdinalIgnoreCase)
            || value.Equals("_self", StringComparison.OrdinalIgnoreCase) || value.Equals("_parent", StringComparison.OrdinalIgnoreCase)
            || value.Equals("_top", StringComparison.OrdinalIgnoreCase)))
            throw new FactsPdfException("FPDF1103", element + " target must be _blank, _self, _parent, _top or a name.", offset);
    }

    private static void AnchorRelations(string value, int offset)
    {
        var any = false;
        foreach (var range in value.AsSpan().SplitAny(" \t\r\n\f"))
        {
            var type = value.AsSpan()[range];
            if (type.IsEmpty) continue;
            if (!HyperlinkTypes.Contains(type))
                throw new FactsPdfException("FPDF1103", $"a rel '{type.ToString()}' is not supported.", offset);
            any = true;
        }
        if (!any) throw new FactsPdfException("FPDF1103", "a rel must name at least one link type.", offset);
    }

    private static void LinkRelations(string value, int offset)
    {
        // Any stylesheet token decides the diagnosis, whatever other tokens come first.
        foreach (var range in value.AsSpan().SplitAny(" \t\r\n\f"))
            if (value.AsSpan()[range].Equals("stylesheet", StringComparison.OrdinalIgnoreCase))
                throw new FactsPdfException("FPDF1204", "External stylesheets are not loaded; use a style element.", offset);
        var rest = value.AsSpan();
        var any = false;
        while (true)
        {
            var start = 0;
            while (start < rest.Length && HtmlTokens.Space(rest[start])) start++;
            rest = rest[start..];
            if (rest.IsEmpty) break;
            var end = 0;
            while (end < rest.Length && !HtmlTokens.Space(rest[end])) end++;
            if (!MetadataLinkTypes.Contains(rest[..end]))
                throw new FactsPdfException("FPDF1103",
                    $"link rel '{rest[..end].ToString()}' is not supported; only metadata links (icon, canonical, alternate, preconnect, ...) are accepted.", offset);
            any = true;
            rest = rest[end..];
        }
        if (!any) throw new FactsPdfException("FPDF1103", "link requires a non-empty rel.", offset);
    }

    private static int ParseListOrdinal(string value, int offset, CancellationToken cancellation)
    {
        var start = 0;
        var end = value.Length;
        while (start < end && HtmlTokens.Space(value[start])) { cancellation.ThrowIfCancellationRequested(); start++; }
        while (end > start && HtmlTokens.Space(value[end - 1])) { cancellation.ThrowIfCancellationRequested(); end--; }
        var ordinal = 0;
        for (var i = start; i < end; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            var digit = value[i] - '0';
            if (digit is < 0 or > 9 || ordinal > (ListMarkerTypes.MaxOrdinal - digit) / 10)
                throw new FactsPdfException("FPDF1103",
                    "List ordinals require positive ASCII decimal integers within the supported range.", offset);
            ordinal = ordinal * 10 + digit;
        }
        if (ordinal == 0)
            throw new FactsPdfException("FPDF1103",
                "List ordinals require positive ASCII decimal integers within the supported range.", offset);
        return ordinal;
    }

    private static int ParseColumnSpan(string value, int columns, int offset, CancellationToken cancellation)
    {
        var start = 0;
        var end = value.Length;
        while (start < end && HtmlTokens.Space(value[start])) { cancellation.ThrowIfCancellationRequested(); start++; }
        while (end > start && HtmlTokens.Space(value[end - 1])) { cancellation.ThrowIfCancellationRequested(); end--; }
        var span = 0;
        for (var i = start; i < end; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            var digit = value[i] - '0';
            if (digit is < 0 or > 9 || span > (columns - digit) / 10 || span * 10 + digit > columns)
                throw new FactsPdfException("FPDF1103", "colspan requires ASCII digits representing an explicit positive column count.", offset);
            span = span * 10 + digit;
        }
        if (span == 0)
            throw new FactsPdfException("FPDF1103", "colspan requires ASCII digits representing an explicit positive column count.", offset);
        return span;
    }

}
