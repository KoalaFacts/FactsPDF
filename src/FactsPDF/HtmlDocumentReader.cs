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
        int SourceOffset = -1, int ChildStart = 0, bool AlignStart = true, bool FlexItem = false, object? After = null,
        string? Source = null);

    // The text and computed style of an inline ::before or ::after box.
    private sealed record GeneratedText(string Text, TextStyle Style);

    // A ::before or ::after box whose style is computed but whose content is not yet known: its counters
    // are read when the box is placed (::before after its element opens, ::after when it closes), after
    // the counters its element and children changed. Result caches the realised box.
    private sealed class PendingPseudo(bool block, string content, TextStyle style, BoxStyle box, int offset)
    {
        internal readonly bool Block = block;
        internal readonly string Content = content;
        internal readonly TextStyle Style = style;
        internal readonly BoxStyle Box = box;
        internal readonly int Offset = offset;
        internal bool Done;
        internal object? Result;
    }
    internal static bool ParagraphTag(string name) => name is "p" or "pre" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6";
    internal static bool ListTag(string name) => name is "ol" or "ul" or "menu";

    // The end tags the reader implies, shared with HtmlElementPositions so both build one tree.
    // Before a start tag: a paragraph or block closes an open p (outside tables), an li an open li.
    internal static bool StartClosesParagraph(string top, string name, bool inTable)
        => !inTable && top == "p" && (ParagraphTag(name) || Container(name) || name is "table" or "dialog");
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
        or "main" or "nav" or "aside" or "hgroup" or "search" or "body" or "html" or "ol" or "ul" or "menu" or "li" or "blockquote" or "figure" or "figcaption" or "address" or "dl" or "dt" or "dd"
        or "form" or "details" or "summary" or "fieldset";

    // The elements a document reads that are void and draw nothing themselves: wbr (a soft wrap
    // opportunity), source, track and area (display:none in the user-agent sheet) and an embed with
    // no src (which a browser lays out as nothing).
    internal static bool VoidInlineTag(string name) => name is "wbr" or "source" or "track" or "area" or "embed";

    // canvas, video and iframe are replaced boxes with no content to draw here: they lay out like an
    // inline svg of their own size (their children are never rendered).
    internal static bool ReplacedTag(string name) => name is "canvas" or "video" or "iframe";

    // datalist and a closed dialog are display:none, and an audio without controls is
    // display:none !important: the subtree is read for its end tag and dropped.
    internal static bool HiddenTag(string name) => name is "datalist" or "dialog" or "audio";

    // form, details, summary and fieldset are blocks that lay out like a div, so author CSS is
    // validated as it is for a div; canvas, video and iframe like an svg.
    private static string? SubjectAlias(string name) => name switch
    {
        "form" or "details" or "summary" or "fieldset" or "datalist" or "dialog" or "audio" => "div",
        "canvas" or "video" or "iframe" => "svg",
        _ => null
    };
    private static bool BoxTag(string name) => name is "img" or "svg" or "hr" or "table" or "col" or "td" or "th" or "caption"
        || ReplacedTag(name) || ParagraphTag(name) || Container(name) && name != "html";

    // Inline text elements: no box of their own, only a text style. code, kbd and samp keep the
    // supplied font (FactsPDF has no generic monospace face, as for pre); var, cite and dfn are
    // italic, as the HTML rendering section's user-agent sheet makes them. time and data have no
    // default style; their machine-readable datetime and value attributes are metadata only.
    // q is inline text wrapped in the quotation marks of the user-agent sheet's quotes:auto for
    // English: curly double quotes, and curly single quotes for every nested q. bdi only isolates
    // its text direction, and every run is laid out left to right, so it renders like span.
    // label, output and map are inline with no user-agent style (a label's pointer cursor, an
    // output's bidi isolation and a map's geometry change nothing on paper); picture is an inline
    // wrapper whose img child draws (see the source rule below); bdo with dir=ltr (the only
    // direction every left-to-right run can honour) is a span; a valid custom element name
    // (x-card) is an HTMLElement the user-agent sheet gives no style, so it is an inline span too.
    internal static bool InlineTag(string name) => name is "span" or "b" or "strong" or "i" or "em"
        or "small" or "big" or "u" or "s" or "del" or "ins" or "a" or "code" or "kbd" or "samp" or "var" or "cite" or "dfn"
        or "time" or "data" or "abbr" or "q" or "bdi" or "label" or "output" or "map" or "picture" or "bdo" or "object" or "slot"
        || IsCustomElement(name);

    /// <summary>A valid custom element name (HTML Standard): a lower-case ASCII letter first, a hyphen
    /// somewhere and no reserved name. Such an element is never an HTML element with its own default
    /// style, so without author CSS it is an inline box.</summary>
    internal static bool IsCustomElement(string name)
    {
        if (name.Length < 3 || name[0] is < 'a' or > 'z' || !name.Contains('-')) return false;
        foreach (var ch in name.AsSpan())
            if (ch is not (>= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_')) return false;
        return name is not ("annotation-xml" or "color-profile" or "font-face" or "font-face-src" or "font-face-uri"
            or "font-face-format" or "font-face-name" or "missing-glyph");
    }

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
    // The attributes an element takes that change nothing on paper: relations to other elements
    // (label for, output for, a form owner), names, and the form submission settings. A custom
    // element takes any attribute, since only author CSS (attribute selectors) can read it. A
    // picture's source takes the resource candidates its fallback img stands for; its media
    // condition would pick another image by an environment a printed page has no answer for.
    private static bool ElementAttribute(string name, string attribute, int offset)
    {
        switch (name)
        {
            case "label": return attribute is "for" or "form";
            case "output": return attribute is "for" or "form" or "name";
            case "map": return attribute == "name";
            case "form": return attribute is "action" or "method" or "enctype" or "target" or "name" or "autocomplete"
                or "novalidate" or "accept-charset" or "rel";
            case "source":
                if (attribute == "media")
                    throw new FactsPdfException("FPDF1103",
                        "source media conditions are not supported: FactsPDF always draws the img inside the picture.", offset);
                return attribute is "src" or "srcset" or "sizes" or "type" or "width" or "height";
            case "track": return attribute is "kind" or "src" or "srclang" or "label" or "default";
            case "details": return attribute == "name";
            case "slot": return attribute == "name";
            case "fieldset": return attribute is "disabled" or "form" or "name";
            case "datalist": return false;
            case "dialog":
                if (attribute == "open")
                    throw new FactsPdfException("FPDF1103",
                        "An open dialog is positioned absolutely and centred by the user-agent sheet, which is not supported; only a closed dialog (display:none) is.", offset);
                return attribute == "closedby";
            case "audio":
                if (attribute == "controls")
                    throw new FactsPdfException("FPDF1103", "audio controls draw a player, which is not supported; an audio without controls is not rendered.", offset);
                return attribute is "src" or "preload" or "autoplay" or "loop" or "muted" or "crossorigin" or "disableremoteplayback";
            case "canvas": return false;
            case "video":
                if (attribute is "controls" or "src" or "poster")
                    throw new FactsPdfException("FPDF1103",
                        $"video {attribute} needs a media resource or a player, which is not supported; a video with neither prints as a transparent box.", offset);
                return attribute is "autoplay" or "loop" or "muted" or "playsinline" or "preload" or "crossorigin"
                    or "disablepictureinpicture" or "disableremoteplayback";
            case "iframe":
                if (attribute is "src" or "srcdoc" or "scrolling")
                    throw new FactsPdfException("FPDF1103",
                        $"iframe {attribute} loads or scrolls a document, which is not supported; an iframe with none prints as an empty bordered box.", offset);
                return attribute is "name" or "allow" or "allowfullscreen" or "sandbox" or "loading" or "referrerpolicy";
            case "embed":
                if (attribute is "src" or "type")
                    throw new FactsPdfException("FPDF1103", $"embed {attribute} loads a plug-in resource, which is not supported.", offset);
                return attribute is "width" or "height";
            case "object":
                if (attribute is "data" or "type" or "usemap" or "typemustmatch")
                    throw new FactsPdfException("FPDF1103",
                        $"object {attribute} names a resource FactsPDF never loads; an object with none prints its fallback content.", offset);
                return attribute is "name" or "form" or "width" or "height";
            case "area": return attribute is "shape" or "coords" or "href" or "alt" or "target" or "download" or "ping"
                or "rel" or "referrerpolicy";
            default: return IsCustomElement(name);
        }
    }

    internal sealed class OpenElements(int maxDepth)
    {
        private readonly List<string> open = [];
        private bool stopped;

        public void Read(HtmlToken token)
        {
            if (stopped || token.Kind is not (HtmlTokenKind.Start or HtmlTokenKind.End)) return;
            var name = token.Value;
            if (name is "meta" or "link" or "base" or "br" || VoidInlineTag(name)) return; // the reader skips them before its depth check
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

    // The last reset copy made for a parent style and the parent it was made for: the next element
    // with the same parent shares it.
    private sealed class SharedDefaults
    {
        public TextStyle? Parent;
        public TextStyle? Style;
    }

    // The element's style before the cascade: the parent's computed style with the non-inherited text
    // properties (margins, page breaks, keep-with-next) reset. The element's own defaults come from
    // the user-agent stylesheet in CssStylesheets.Compute, below every author declaration. Returns the
    // parent itself when nothing needs resetting, and otherwise a copy shared with earlier siblings;
    // a shared style is copied before any declaration (see TextStyle).
    private static TextStyle ElementDefaults(TextStyle parent, SharedDefaults shared)
    {
        if (parent.MarginBefore == 0 && parent.MarginAfter == 0 &&
            parent.PercentMarginBefore is null && parent.PercentMarginAfter is null && !parent.BreakBefore && !parent.BreakAfter
            && !parent.KeepWithNext)
            return parent;
        if (ReferenceEquals(shared.Parent, parent)) return shared.Style!;
        var style = parent with { };
        style.MarginBefore = 0;
        style.MarginAfter = 0;
        style.PercentMarginBefore = null;
        style.PercentMarginAfter = null;
        style.BreakBefore = false;
        style.BreakAfter = false;
        style.KeepWithNext = false;
        shared.Parent = parent;
        shared.Style = style;
        return style;
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
            CssStylesheets.ReturnPooled();
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
        var root = new TextStyle(options.FontSize, TextStyle.NormalLineHeight, new Rgb(0, 0, 0), TextAlignment.Left);
        var sharedDefaults = new SharedDefaults();
        // The last shared th/caption default and its centred copy (see the user-agent centring below).
        TextStyle? centredSource = null, centredStyle = null;
        var rootFontSize = root.FontSize; // Actual html computed size or configured initial size.
        var stack = new List<Frame> { new("#root", root, BoxStyle.Initial, null) };
        // CSS counters, tracked only when a stylesheet sets or prints one.
        var counterScopes = sheets.UsesCounters ? new CounterScopes() : null;
        var path = new List<CssElement>(); // Actual elements only; the synthetic document root is not selectable.
        var result = new DocumentRoot { PageRules = sheets.PageRules, UsesPageAreaMedia = sheets.UsesPageAreaMedia };
        Paragraph? current = null;
        // The box of the open cell paragraph when it has padding; cell paragraphs have no other box.
        BoxStyle? currentPadding = null;
        TableMarkup? table = null;
        // The tables a nested table (a table directly in a cell of another) interrupts, innermost last.
        Stack<TableMarkup>? outerTables = null;
        // Open block boxes (a list and its items) inside the current table cell: their content is a
        // block tree, kept whole as one cell content, while a flat cell holds text and images only.
        var cellBlocks = 0;
        var anonymous = false;
        var elements = 0; var insideStyle = false; var baseSeen = false;
        // The base element's absolute http(s) URL, which relative link hrefs resolve against; null when
        // there is none or it is not one (a relative base resolves against the unknown document URL).
        Uri? baseUri = null;
        // Images written inline as data: URLs, decoded on first use.
        DataUriImages? dataImages = null;
        SvgDocument? svg = null;
        // html, head and body frames the HTML parser implied (HtmlImpliedElements): they are part of
        // the tree selectors see, but no element or depth limit counts them.
        var impliedOpen = 0; var impliedHead = false;
        // The details elements still open, innermost last: whether each is open, its first summary and
        // where its children end after it (what a closed details keeps).
        var detailsStates = new List<DetailsState>();
        // Collapsible white space right after a summary marker is at the start of its line.
        var trimLeading = false;

        // Where the innermost open block's children begin in the child stack (0 for the root).
        int ChildStart(int fromFrame)
        {
            for (var i = fromFrame; i >= 0; i--)
                if (stack[i].Box is not null) return stack[i].ChildStart;
            return 0;
        }

        // The current table cell holds flat text and images (no list box is open in it).
        bool FlatCell() => table?.Cell is not null && cellBlocks == 0;

        void Flush()
        {
            if (current is not null)
            {
                if (FlatCell() && table!.Cell is { } cell)
                {
                    // Formatting whitespace and empty inline wrappers are not
                    // anonymous flow items. Explicit p margins are retained.
                    if (!anonymous || current.Runs.HasBreak() || TextLines.HasTextContent(current, cancellation))
                        cell.Contents.Add(currentPadding is null ? new TableCellText(current) : new PaddedTableCellText(current, currentPadding));
                }
                else
                {
                    current.IsAnonymous = anonymous;
                    children.Add(current);
                }
            }
            current = null;
            currentPadding = null;
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
            else
            {
                closing.Box?.TakeChildren(children, closing.ChildStart);
                // The outermost list of a cell becomes one content of the cell instead of a child of
                // the block that holds the table.
                if (closing.Box is not null && table?.Cell is { } listCell && cellBlocks == 1)
                {
                    if (!ReferenceEquals(children[^1], closing.Box))
                        throw new InvalidOperationException("Cell list is not the current child.");
                    children.RemoveAt(children.Count - 1);
                    listCell.Contents.Add(new TableCellBlock(closing.Box));
                }
            }
            if (closing.Box is not null && table?.Cell is not null) cellBlocks--;
            stack.RemoveAt(stack.Count - 1);
            path.RemoveAt(path.Count - 1);
            counterScopes?.Close(stack.Count - 1);
        }
        // The open element's ::after: inline text joins its content, a block box is its last child.
        void AfterText() { if (Realize(stack[^1].After) is GeneratedText text) Add(text.Text, text.Style); }
        void AfterBox() { if (Realize(stack[^1].After) is BlockNode box) children.Add(box); }
        void Close() { AfterText(); Flush(); AfterBox(); Pop(); }
        // Computes an element's ::before or ::after (the element is the last entry of path) and
        // returns the box it generates: a block box (in a flex container, or with display:block or
        // flex), inline text, or null when content is none or normal or display is none.
        object? Generate(CssPseudoElement pseudo, string name, TextStyle style, BoxStyle box, int offset)
        {
            // A replaced image generates no ::before or ::after box in a browser.
            if (name == "img" || ReplacedTag(name)) return null;
            var (display, content) = sheets.PseudoKeys(path, pseudo);
            if (content is null || content[0] is not ('"' or CounterContent.Marker) || display == "none") return null;
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
            var defaults = ElementDefaults(style, sharedDefaults);
            var (generatedStyle, generatedBox) = sheets.Compute(defaults, false, style, root, rootFontSize, box, path,
                paragraph: false, block: block, offset, default, pseudo: pseudo, pseudoName: block ? "div" : "span");
            if (generatedStyle.FontSize is < 1 or > 144)
                throw new FactsPdfException("FPDF1202", "Computed font size is outside the supported range.", offset);
            return new PendingPseudo(block, content, generatedStyle, generatedBox, offset);
        }
        // Places a pseudo-element box: applies its own counter properties, evaluates its content against
        // the counters in scope (the stack's innermost frame is its originating element) and builds the
        // inline text or block box. Done once; ::after is realised when its element closes.
        object? Realize(object? pending)
        {
            if (pending is not PendingPseudo pseudo) return null;
            if (pseudo.Done) return pseudo.Result;
            pseudo.Done = true;
            string text;
            if (CounterContent.IsEncoded(pseudo.Content))
            {
                // A subtree that display:none removes has no boxes, so counters there change nothing.
                if (InHiddenSubtree() || pseudo.Box.DisplayNone) text = "";
                else
                {
                    counterScopes!.SetTop(stack.Count - 1);
                    counterScopes.Apply(pseudo.Box.Counters);
                    text = CounterContent.Evaluate(pseudo.Content, counterScopes, pseudo.Offset);
                    // A counter the pseudo-element nested under an outer one is out of scope after it.
                    counterScopes.Close(stack.Count - 1);
                }
            }
            else
            {
                if (counterScopes is not null && !InHiddenSubtree() && !pseudo.Box.DisplayNone)
                {
                    counterScopes.SetTop(stack.Count - 1);
                    counterScopes.Apply(pseudo.Box.Counters);
                    counterScopes.Close(stack.Count - 1);
                }
                text = pseudo.Content[1..];
            }
            if (!pseudo.Block) return pseudo.Result = new GeneratedText(text, pseudo.Style);
            var node = new BlockNode("div", pseudo.Style, pseudo.Box, pseudo.Offset);
            if (text.Length > 0)
            {
                var paragraph = new Paragraph(pseudo.Style, pseudo.Offset) { IsAnonymous = true };
                paragraph.Runs.Add(new(text, pseudo.Style, false, pseudo.Offset));
                node.Add(paragraph);
            }
            return pseudo.Result = node;
        }
        bool InHiddenSubtree()
        {
            for (var i = stack.Count - 1; i > 0; i--)
                if (stack[i].BoxStyle.DisplayNone) return true;
            return false;
        }
        // Applies an element's own counter properties and the user-agent list counters (ol and ul reset
        // list-item, li increments it) in document order. An element with display:none, or inside one,
        // has no box and counts nothing.
        void ApplyCounters(string name, BoxStyle box, int? listStart, int? listValue, bool listReversed, int offset)
        {
            if (counterScopes is null) return;
            counterScopes.SetTop(stack.Count - 1);
            if (box.DisplayNone || InHiddenSubtree()) return;
            foreach (var author in new[] { box.Counters.Reset, box.Counters.Increment, box.Counters.Set })
                if (author is not null && Array.Exists(author, op => op.Name == "list-item"))
                    throw new FactsPdfException("FPDF1201",
                        "Changing the list-item counter with counter-reset, counter-increment or counter-set is not supported; list markers number themselves.", offset);
            if (name is "ol") counterScopes.Reset("list-item", (listStart ?? 1) - 1, listReversed);
            else if (name is "ul" or "menu") counterScopes.Reset("list-item", 0);
            counterScopes.Apply(box.Counters);
            // Chrome numbers counter(list-item) by the ol start and one per li; the li value attribute
            // changes the marker only.
            if (name == "li") counterScopes.Increment("list-item", 1);
            // An element that opens no frame has no descendants for a nested counter to serve.
            if (name is "br" or "img" or "svg" or "hr" or "col") counterScopes.Close(stack.Count - 1);
        }
        void Add(string text, TextStyle style, bool lineBreak = false)
        {
            if (trimLeading)
            {
                if (lineBreak) trimLeading = false;
                else if (!style.PreserveSpaces)
                {
                    text = text.TrimStart(HtmlWhitespace);
                    if (text.Length == 0) return;
                    trimLeading = false;
                }
            }
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
                current = FlatCell() && table!.Cell is { } cell
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
                || (table is null || cellBlocks > 0) && stack[^1].Box is { BoxStyle.IsFlexContainer: true } or { BoxStyle.IsGridContainer: true })
            {
                if (AnyFrame(stack, f => ParagraphTag(f.Name) || InlineTag(f.Name) || ListTag(f.Name) || f.Name == "li"))
                    throw new FactsPdfException("FPDF1101", name == "svg"
                        ? "An svg with display:block must be a standalone block outside paragraphs, inline elements and lists."
                        : "An img with display:block must be a standalone block outside paragraphs, inline elements and lists.", offset);
                Flush();
                if (FlatCell() && table!.Cell is { } cell) cell.Contents.Add(new TableCellImage(node));
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
            trimLeading = false;
            if (current is null)
            {
                current = FlatCell() && table!.Cell is { } cell
                    ? new Paragraph(cell.Style, cell.SourceOffset)
                    : new Paragraph(parentStyle, stack[^1].SourceOffset < 0 ? offset : stack[^1].SourceOffset);
                anonymous = true;
            }
            current.Runs.Add(new InlineImageRun(node, parentStyle.KeepWithNext ? parentStyle with { KeepWithNext = false } : parentStyle));
        }

        // The box of a canvas, video or iframe: an svg-like vector box with nothing to draw, sized as a
        // browser sizes it without a resource. A canvas is as large as its width and height (300 by
        // 150 CSS px by default) and keeps that ratio when CSS gives only one dimension; a video
        // and an iframe take the width and height attributes as CSS hints, each defaulting to
        // 300px or 150px independently.
        ImageNode ReplacedBox(string tag, TextStyle style, BoxStyle box, string? widthAttribute, string? heightAttribute, int offset)
        {
            svg ??= new SvgDocument(options.MaxDisplayCommands, cancellation);
            if (tag == "canvas")
            {
                var canvasWidth = CanvasLength(widthAttribute, 300, offset);
                var canvasHeight = CanvasLength(heightAttribute, 150, offset);
                var canvas = svg.Add(new SvgGraphic { ViewBox = (0, 0, canvasWidth, canvasHeight), Shapes = [] });
                if (box.Width is null && box.Height is null) box = box with { Width = new CssLength(canvasWidth * 0.75) };
                return new ImageNode(null, style, box, offset, null, canvas);
            }
            var width = box.Width ?? new CssLength(225);
            var height = box.Height ?? new CssLength(112.5);
            var empty = svg.Add(new SvgGraphic { ViewBox = (0, 0, 1, 1), Shapes = [] });
            return new ImageNode(null, style, box with { Width = width, Height = height }, offset, null, empty);
        }

        // The triangle of a summary's list-style:disclosure-closed or -open marker, measured against
        // Chrome: a box 1.059em wide that stands on the baseline, holding a triangle centred 0.34em
        // above it (right-pointing 0.57em by 0.656em when closed, down-pointing 0.667em by 0.5625em
        // when open) that starts at the box's left edge, in the summary's text colour.
        ImageNode DisclosureMarker(TextStyle style, bool open, int offset)
        {
            svg ??= new SvgDocument(options.MaxDisplayCommands, cancellation);
            var size = style.FontSize;
            double advance = 1.059 * size, boxHeight = 0.67 * size, centre = boxHeight - 0.34 * size;
            SvgSegment[] path;
            if (open)
            {
                double width = 0.667 * size, half = 0.5625 * size / 2;
                path = [SvgSegment.Move(0, centre - half), SvgSegment.Line(width, centre - half),
                    SvgSegment.Line(width / 2, centre + half), SvgSegment.Close];
            }
            else
            {
                double width = 0.57 * size, half = 0.656 * size / 2;
                path = [SvgSegment.Move(0, centre - half), SvgSegment.Line(width, centre),
                    SvgSegment.Line(0, centre + half), SvgSegment.Close];
            }
            var shape = new SvgShape { Path = path, Transform = SvgMatrix.Identity, Fill = style.Color, SourceOffset = offset };
            var graphic = svg.Add(new SvgGraphic { ViewBox = (0, 0, advance, boxHeight), Shapes = [shape] });
            var box = BoxStyle.Initial with { Width = new CssLength(advance), Height = new CssLength(boxHeight) };
            return new ImageNode(null, style, box, offset, null, graphic);
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
                var raw = token.Value;
                // A newline right after the start tag of pre is not part of its text (the HTML parser drops it).
                if (stack[^1].Name == "pre" && t > 0 && tokens[t - 1] is { Kind: HtmlTokenKind.Start, Value: "pre" } && raw.Length > 0)
                    raw = raw.StartsWith("\r\n", StringComparison.Ordinal) ? raw[2..] : raw[0] is '\n' or '\r' ? raw[1..] : raw;
                var decoded = HtmlTokens.Decode(raw, token.Offset);
                if (ListTag(stack[^1].Name) || table is not null && table.Cell is null)
                {
                    if (!HtmlTokens.AllSpace(decoded))
                        throw new FactsPdfException("FPDF1101", "Structural list/table elements cannot contain direct text.", token.Offset);
                    continue;
                }
                if (stack[^1].Name == "details" && detailsStates[^1] is { SummarySeen: false } && !HtmlTokens.AllSpace(decoded))
                    throw new FactsPdfException("FPDF1101", "A details element must start with its summary element; one without it prints a localized default label, which is not supported.", token.Offset);
                Add(decoded, stack[^1].Style);
                continue;
            }
            var name = token.Value;
            // The element's own name, which selectors and its end tag use; a custom element that CSS
            // makes a block is read as a div (below), and its frame remembers the name it was written with.
            var source = name;
            if (token.Kind == HtmlTokenKind.End && stack.Count > 1 && stack[^1].Source == name) name = stack[^1].Name;
            var implied = token is ImpliedHtmlToken;
            if (implied && token.Kind == HtmlTokenKind.Start) { impliedOpen++; impliedHead |= name == "head"; }
            else if (token.Kind == HtmlTokenKind.End && name == "head" && impliedHead) { impliedOpen--; impliedHead = false; }
            else if (implied && token.Kind == HtmlTokenKind.End && name == "tbody") impliedOpen--;
            if (token.Kind == HtmlTokenKind.Start && IsCustomElement(name) && !token.SelfClosing)
            {
                path.Add(CssElement.From(token, t));
                var customDisplay = sheets.AuthorDisplay(path, (token as StyledHtmlToken)?.InlineDeclarations ?? default);
                path.RemoveAt(path.Count - 1);
                if (customDisplay == "none")
                {
                    // display:none removes the whole subtree, unread, as it does for datalist.
                    t = EndOfElement(tokens, t, name, null);
                    continue;
                }
                if (customDisplay is "block" or "flex" or "grid") name = "div";
            }
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
                || VoidInlineTag(name) || ReplacedTag(name) || HiddenTag(name)
                || name is "img" or "svg" or "hr" or "br" or "head" or "title" or "meta" or "link" or "base"))
                throw new FactsPdfException("FPDF1102", $"Element '{name}' is not supported in this development slice.", token.Offset);
            if (token.Kind == HtmlTokenKind.End)
            {
                if (EndClosesParagraph(stack[^1].Name, name, table is not null && cellBlocks == 0)) Close();
                if (EndClosesListItem(stack[^1].Name, name)) Close();
                if (stack.Count == 1 || stack[^1].Name != name)
                    throw new FactsPdfException("FPDF1101", name == "p"
                        ? "An unmatched </p> (for example after a table that closed its paragraph) would make a browser insert an empty paragraph; remove it."
                        : $"Unexpected end tag '{name}'.", token.Offset);
                if (TableMarkup.IsTag(name))
                {
                    if (name is "td" or "th" or "caption") { AfterText(); Flush(); }
                    table!.End(name, token.Offset);
                    if (name == "table") table = outerTables is { Count: > 0 } ? outerTables.Pop() : null;
                    Pop(); continue;
                }
                AfterText();
                if (ParagraphTag(name) || Container(name)) Flush();
                AfterBox();
                if (name == "q") Add(Quote(stack, open: false, options), stack[^1].Style);
                if (stack[^1].FlexItem) Flush();
                if (name == "details")
                {
                    // A closed details shows its first summary only: whatever follows is
                    // display:none (content-visibility:hidden in the user-agent sheet).
                    var state = detailsStates[^1];
                    detailsStates.RemoveAt(detailsStates.Count - 1);
                    if (!state.SummarySeen)
                        throw new FactsPdfException("FPDF1101",
                            "A details element without a summary prints a localized default label, which is not supported; add a summary.", token.Offset);
                    if (!state.Open) children.RemoveRange(state.AfterSummary, children.Count - state.AfterSummary);
                }
                var firstSummaryEnd = name == "summary" && detailsStates.Count > 0 && detailsStates[^1].SummaryDepth == stack.Count;
                Pop();
                if (firstSummaryEnd) { detailsStates[^1].AfterSummary = children.Count; detailsStates[^1].SummaryDepth = -1; }
                continue;
            }
            if (!implied && ++elements > options.MaxElements) throw new FactsPdfException("FPDF1002", "Element limit exceeded.", token.Offset);
            // A stylesheet link with other link types fails as an unsupported style source wherever it
            // appears, before any placement check; rel="stylesheet" alone was applied by CssStylesheets.
            var stylesheetLink = false;
            if (name == "link")
                foreach (var (attribute, value) in token.Attributes!)
                    if (attribute == "rel") stylesheetLink = LinkRelations(value, token.Offset);
            // An svg is foreign content, where a self-closing tag closes the element.
            if (token.SelfClosing && !VoidInlineTag(name) && name is not ("img" or "svg" or "hr" or "br" or "meta" or "link" or "base" or "col"))
                throw new FactsPdfException("FPDF1101", "Non-void HTML tags cannot be XML-self-closed in this subset.", token.Offset);
            if (name == "table" && table is not null)
                CheckNestedTable(table, outerTables?.Count ?? 0, cellBlocks, stack[^1].Name, token.Offset);
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
            if (name != "table") table?.CheckChild(stack[^1].Name, name, token.Offset, cellBlocks > 0);
            if (StartClosesParagraph(stack[^1].Name, name, table is not null && cellBlocks == 0)) Close();
            if (StartClosesListItem(stack[^1].Name, name)) Close();
            // A table start tag closes an open p (the HTML parser, standards mode); any other text
            // element still open around it would have to be split around the table, which only inline
            // elements in a block, list item or body can be (the text before it is its own line box).
            // The HTML parser ignores a form start tag while another form is open, which would
            // leave this one's content in the outer form; a nested form fails instead of guessing.
            if (name == "form" && AnyFrame(stack, f => f.Name == "form"))
                throw new FactsPdfException("FPDF1101", "A form cannot be nested inside another form.", token.Offset);
            if (name == "table" && AnyFrame(stack, f => ParagraphTag(f.Name)))
                throw new FactsPdfException("FPDF1101",
                    "A table cannot be placed inside a heading, pre or an inline element of a paragraph; close the element before the table.", token.Offset);
            if (stack[^1].Name == "details" && detailsStates[^1] is { SummarySeen: false } && name != "summary")
                throw new FactsPdfException("FPDF1101", "A details element must start with its summary element.", token.Offset);
            // A slot is display:contents: it is no box, so a block inside it joins the block around the slot.
            if ((ParagraphTag(name) || Container(name)) && AnyFrame(stack, f => ParagraphTag(f.Name) || InlineTag(f.Name) && f.Name != "slot"))
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
            var style = ElementDefaults(parent, sharedDefaults);
            var styleOwned = false;
            // The HTML user-agent sheet's th and caption {text-align:-internal-center}: centred when
            // the parent's computed text-align is its initial value (start), otherwise the
            // parent's alignment is inherited as usual. Author declarations still win below.
            var parentAlignStart = stack[^1].AlignStart;
            // The first summary of a details is the one the user-agent sheet gives the disclosure
            // marker (details > summary:first-of-type); the check above makes it the first child.
            var firstSummary = name == "summary" && stack[^1].Name == "details" && detailsStates[^1] is { SummarySeen: false };
            if (firstSummary) detailsStates[^1].SummarySeen = true;
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
            var hrefGiven = false;
            var hidden = false;
            var linkRel = false;
            var baseHrefOrTarget = false;
            string? widthAttribute = null, heightAttribute = null;
            var borderless = false;
            var detailsOpen = false;
            int? listStart = null, listValue = null;
            var listReversed = false;
            ListMarkerKind? listTypeHint = null;
            CssDeclaration? widthHint = null, heightHint = null;
            foreach (var (attribute, value) in token.Attributes!)
            {
                if (attribute == "style")
                {
                    if (name is "head" or "title" or "meta" or "link" or "base" or "br")
                        throw new FactsPdfException("FPDF1103", "Styling this metadata/void element is not supported.", token.Offset);
                    // Declarations were parsed once during source collection.
                }
                else if (attribute == "href" && name == "a")
                {
                    // Non-strict mode keeps the text as a link (blue, underlined) without an annotation
                    // when the target is not an absolute HTTPS or mailto URI.
                    hrefGiven = true;
                    href = PdfLinkAnnotations.ValidateUri(value, token.Offset, options.CssWarnings, baseUri);
                }
                // A PDF link annotation always opens its URI outside the document, so the browsing
                // context (target) and the hyperlink relations (noopener, nofollow, ...) change nothing.
                else if (name == "a" && attribute == "target") AnchorTarget("a", value, token.Offset);
                // base sets the URL that relative link hrefs resolve against (when it is an absolute
                // http or https URL) and the default link target, held to the a rule. Images and
                // stylesheets are caller-supplied resources named by their exact text, never resolved.
                else if (name == "base" && attribute == "href")
                {
                    baseHrefOrTarget = true;
                    baseUri = PdfLinkAnnotations.BaseUri(value);
                }
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
                // A stylesheet link's media query list was evaluated when its stylesheet was applied.
                else if (name == "link" && attribute == "media" && stylesheetLink) { }
                else if (name == "link" && attribute is "href" or "type" or "sizes" or "hreflang" or "crossorigin"
                    or "as" or "color" or "integrity" or "referrerpolicy") { }
                else if (name == "img" && attribute is "src" or "alt") { }
                // Presentational hints for CSS width and height, below every author declaration.
                else if (attribute == "width" && HtmlPresentationalHints.Width(name))
                    widthHint = HtmlPresentationalHints.Declaration(CssProperty.Width, attribute, value, token.Offset);
                else if (attribute == "height" && HtmlPresentationalHints.Height(name))
                    heightHint = HtmlPresentationalHints.Declaration(CssProperty.Height, attribute, value, token.Offset);
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
                    // Elements that are display:none anyway (or have no box) take it as they are.
                    if (!(name is "source" or "track" or "area" or "embed" || HiddenTag(name))
                        && (!(ParagraphTag(name) || Container(name) && name is not ("body" or "html" or "ol" or "ul" or "menu" or "li"))
                            || table is not null || AnyFrame(stack, f => ListTag(f.Name) || f.Name == "li")))
                        throw new FactsPdfException("FPDF1103",
                            "hidden is supported only on ordinary report blocks outside lists and tables.", token.Offset);
                    hidden = true;
                }
                else if (attribute is "id" or "class" or "title" or "lang" || attribute.StartsWith("data-", StringComparison.Ordinal)) { }
                // An event handler content attribute only runs script on an event; FactsPDF runs no
                // script (see script elements) and a printed page receives no events, so it changes nothing.
                else if (attribute.Length > 2 && attribute.StartsWith("on", StringComparison.Ordinal)) { }
                // A canvas's width and height are its intrinsic size, not CSS hints (video and iframe use the
                // presentational hints above).
                else if (name == "canvas" && attribute is "width" or "height")
                {
                    if (attribute == "width") widthAttribute = value; else heightAttribute = value;
                }
                else if (name == "iframe" && attribute == "frameborder")
                {
                    if (value is "0" or "no") borderless = true;
                    else if (value is not ("1" or "yes"))
                        throw new FactsPdfException("FPDF1103", "iframe frameborder accepts only 0, 1, no or yes.", token.Offset);
                }
                else if (name == "details" && attribute == "open") detailsOpen = true;
                else if (ElementAttribute(source, attribute, token.Offset)) { }
                else throw new FactsPdfException("FPDF1103", $"Attribute '{attribute}' is not supported.", token.Offset);
            }
            if (name == "a")
            {
                if (!hrefGiven) throw new FactsPdfException("FPDF1103", "a requires a bounded HTTPS or mailto href.", token.Offset);
                if (AnyFrame(stack, frame => frame.Name == "a"))
                    throw new FactsPdfException("FPDF1101", "Nested links are not supported.", token.Offset);
                if (href is not null)
                {
                    if (!styleOwned) { style = style with { }; styleOwned = true; }
                    style.LinkUri = href;
                }
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
            if (VoidInlineTag(name))
            {
                path.Add(CssElement.From(token, t));
                var voidDeclarations = (token as StyledHtmlToken)?.InlineDeclarations ?? default;
                if (name == "wbr")
                {
                    // A soft wrap opportunity with no glyph: the zero-width space the line breaker
                    // knows (TextLines.WordBreakOpportunity), in the style it would have in the text.
                    var (wordBreakStyle, _) = sheets.Compute(style, styleOwned, parent, root, rootFontSize, parentBox, path,
                        false, false, token.Offset, voidDeclarations);
                    path.RemoveAt(path.Count - 1);
                    // Where white-space forbids soft wrapping (nowrap, pre) a browser still honours the
                    // opportunity in ways this line breaker does not model, so it fails there.
                    if (wordBreakStyle.NoWrap)
                        throw new FactsPdfException("FPDF1201", "wbr inside white-space:nowrap or pre text is not supported.", token.Offset);
                    Add(((char)TextLines.WordBreakOpportunity).ToString(), wordBreakStyle);
                    continue;
                }
                // source, track and area (and embed) are display:none or box-less in the user-agent sheet.
                var (voidStyle, voidBox) = sheets.Compute(style, styleOwned, parent, root, rootFontSize, parentBox, path,
                    false, true, token.Offset, voidDeclarations, hidden: true, pseudoName: "div");
                path.RemoveAt(path.Count - 1);
                // An embed never gets a box, whatever its display (probed in Chrome); source, track and
                // area get an empty block box when an author display shows them.
                if (name == "embed" || voidBox.DisplayNone) continue;
                if (table is not null || AnyFrame(stack, f => ParagraphTag(f.Name) || InlineTag(f.Name) || ListTag(f.Name) || f.Name == "li"))
                    throw new FactsPdfException("FPDF1101",
                        $"A {name} shown by an author display must be a standalone block outside paragraphs, inline elements, lists and tables.", token.Offset);
                Flush();
                children.Add(new BlockNode("div", voidStyle, voidBox, token.Offset));
                continue;
            }
            path.Add(CssElement.From(token, t));
            if (name is not ("head" or "title"))
                (style, boxStyle) = sheets.Compute(style, styleOwned, parent, root, rootFontSize, parentBox, path,
                    ParagraphTag(name), BoxTag(name) || HiddenTag(name),
                    token.Offset, (token as StyledHtmlToken)?.InlineDeclarations ?? default, allowStyling: name != "br",
                    hidden: hidden || HiddenTag(name), listTypeHint: listTypeHint, widthHint: widthHint, heightHint: heightHint,
                    pseudoName: SubjectAlias(name) ?? (name != source && Container(name) ? name : null), uaName: firstSummary ? "summary" : name == "iframe" ? borderless ? "iframe-borderless" : "iframe" : name == "fieldset" ? name : null);
            if (style.FontSize is < 1 or > 144) throw new FactsPdfException("FPDF1202", "Computed font size is outside the supported range.", token.Offset);
            if (name is not ("head" or "title")) ApplyCounters(name, boxStyle, listStart, listValue, listReversed, token.Offset);
            if (name == "slot")
            {
                // A slot is display:contents: it generates no box, so no box property can show, a text
                // decoration is not drawn on it, and as the child of a flex or grid container its content
                // would become items of that container.
                if (boxStyle with { MarkerKind = BoxStyle.Initial.MarkerKind, MarkerInside = false, BorderBoxSizing = false } != BoxStyle.Initial
                    || style.Decoration != parent.Decoration)
                    throw new FactsPdfException("FPDF1201",
                        "A slot is display:contents and draws no box: margins, padding, borders, backgrounds, sizes and text decorations on it are not supported.", token.Offset);
                if ((table is null || cellBlocks > 0) && stack[^1].Box is { BoxStyle.HasItems: true })
                    throw new FactsPdfException("FPDF1201", "A slot directly inside a flex or grid container is not supported.", token.Offset);
            }
            if (HiddenTag(name))
            {
                // The subtree is display:none, so it is dropped unread (only its end tag is looked
                // for). An author display that shows a dialog or datalist would need a box this
                // renderer never builds, so it fails; an audio's display:none is !important.
                path.RemoveAt(path.Count - 1);
                if (name != "audio" && !boxStyle.DisplayNone)
                    throw new FactsPdfException("FPDF1201", $"{name} is display:none; an author display that shows it is not supported.", token.Offset);
                t = EndOfElement(tokens, t, name, null);
                continue;
            }
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
                result.CanvasColor = boxStyle.BackgroundColor;
                if (boxStyle.IsFlexContainer) result.FlexRootOffset = Math.Max(token.Offset, 0);
            }
            if (name == "br") { path.RemoveAt(path.Count - 1); Add("", parent, true); continue; }
            if (stack.Count - impliedOpen > options.MaxDepth) throw new FactsPdfException("FPDF1003", "Nesting limit exceeded.", token.Offset);
            object? before = null, after = null;
            if (sheets.HasPseudoRules && name is not ("head" or "title"))
            {
                before = Generate(CssPseudoElement.Before, name, style, boxStyle, token.Offset);
                after = Generate(CssPseudoElement.After, name, style, boxStyle, token.Offset);
            }
            if (name == "details" && (before is not null || after is not null))
                throw new FactsPdfException("FPDF1201", "::before and ::after on a details element are not supported.", token.Offset);
            if (name == "table")
            {
                Flush();
                var node = new TableNode(style, boxStyle, token.Offset);
                if (table is not null)
                {
                    // A table in a cell is block content of that cell, laid out like a list in one: a
                    // wrapper block holds it, and the cell lays the wrapper out as a fragment.
                    var cell = table.Cell!;
                    var wrapper = new BlockNode("div", cell.Style, BoxStyle.Initial, token.Offset);
                    wrapper.Add(node);
                    cell.Contents.Add(new TableCellBlock(wrapper));
                    (outerTables ??= new()).Push(table);
                }
                else children.Add(node);
                table = new(node);
                stack.Add(new(name, style, boxStyle, null, token.Offset, 0, alignStart));
                continue;
            }
            if (TableMarkup.IsTag(name))
            {
                table!.Start(name, style, boxStyle, token.Offset, columnSpan);
                if (name == "col") { path.RemoveAt(path.Count - 1); continue; }
                stack.Add(new(name, style, boxStyle, null, token.Offset, 0, alignStart, After: after));
                if (Realize(before) is GeneratedText cellText) Add(cellText.Text, cellText.Style);
                continue;
            }
            if (name == "img")
            {
                if (!token.Attributes.TryGetValue("src", out var resourceName))
                    throw new FactsPdfException("FPDF1103", "img requires an explicit resource name in src; paths and URLs are unsupported.", token.Offset);
                PdfImage? image;
                if (DataUriImages.IsDataUri(resourceName)) image = (dataImages ??= new(options, cancellation)).Load(resourceName, token.Offset);
                else if (!ImageResources.ValidName(resourceName))
                    throw new FactsPdfException("FPDF1103", "img requires an explicit resource name or a data:image URL in src; paths and URLs are unsupported.", token.Offset);
                else if (!options.Images.TryGetValue(resourceName, out image))
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
            if (ReplacedTag(name))
            {
                AddReplaced(ReplacedBox(name, style, boxStyle, widthAttribute, heightAttribute, token.Offset), name, token.Offset);
                path.RemoveAt(path.Count - 1);
                t = EndOfElement(tokens, t, name, name == "video"
                    ? source => throw new FactsPdfException("FPDF1103",
                        "video source needs a media resource, which is not supported; a video with none prints as a transparent box.", source.Offset)
                    : null);
                continue;
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
            if (BoxTag(name) && (table?.Cell is null || cellBlocks > 0 || ListTag(name))
                && !(implied && boxStyle with { BorderBoxSizing = false } == BoxStyle.Initial))
            {
                box = new BlockNode(name is "form" or "details" or "summary" or "fieldset" ? "div" : name, style, boxStyle, token.Offset)
                {
                    ListStart = listStart, ListValue = listValue, ListReversed = listReversed
                };
                // A paragraph element holds at most its own paragraph (block children are rejected
                // or close it first), which the child list keeps in its first slot without an array.
                children.Add(box);
            }
            // Each inline child of a flex or grid container, and each run of text between them, is its own
            // item: close the anonymous paragraph before and after the inline element.
            var flexItem = InlineTag(name) && (table is null || cellBlocks > 0) && stack[^1].Box is { BoxStyle.HasItems: true };
            if (flexItem) Flush();
            stack.Add(new(name, style, boxStyle, box, token.Offset, children.Count, alignStart, flexItem, after,
                name != source ? source : null));
            if (box is not null && table?.Cell is not null) cellBlocks++;
            if (name == "q") Add(Quote(stack, open: true, options), style);
            if (name == "details") detailsStates.Add(new DetailsState { Open = detailsOpen });
            if (firstSummary)
            {
                detailsStates[^1].SummaryDepth = stack.Count;
                if (boxStyle.MarkerKind == ListMarkerKind.Disclosure)
                {
                    if (!boxStyle.MarkerInside)
                        throw new FactsPdfException("FPDF1201", "A summary marker outside the summary (list-style-position:outside) is not supported.", token.Offset);
                    AddReplaced(DisclosureMarker(style, detailsStates[^1].Open, token.Offset), "summary marker", token.Offset);
                    trimLeading = true;
                }
                else if (boxStyle.MarkerKind != ListMarkerKind.None)
                    throw new FactsPdfException("FPDF1201",
                        "A summary marker other than the disclosure triangle (the default) and list-style-type:none is not supported.", token.Offset);
            }
            if (ParagraphTag(name))
            {
                current = new Paragraph(style, token.Offset, RunCount(tokens, t + 1, name));
                anonymous = false;
                if (FlatCell() && (boxStyle.PaddingTop.Value != 0 || boxStyle.PaddingRight.Value != 0
                    || boxStyle.PaddingBottom.Value != 0 || boxStyle.PaddingLeft.Value != 0))
                    currentPadding = boxStyle;
            }
            var realizedBefore = Realize(before);
            if (realizedBefore is BlockNode beforeBox) children.Add(beforeBox);
            else if (realizedBefore is GeneratedText beforeText) Add(beforeText.Text, beforeText.Style);
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

    private static readonly char[] HtmlWhitespace = [' ', '\t', '\r', '\n', '\f'];

    private sealed class DetailsState
    {
        public bool Open, SummarySeen;
        // The depth of the first summary's frame, and the child count once it has closed.
        public int SummaryDepth = -1, AfterSummary;
    }

    // The index of the end tag that closes the element whose start tag is tokens[start], counting
    // nested elements of the same name; inspect sees every token in between.
    private static int EndOfElement(List<HtmlToken> tokens, int start, string name, Action<HtmlToken>? inspect)
    {
        var depth = 1;
        for (var i = start + 1; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (token.Kind is not (HtmlTokenKind.Start or HtmlTokenKind.End)) continue;
            if (token.Kind == HtmlTokenKind.Start && token.Value == "source") inspect?.Invoke(token);
            if (token.Value != name) continue;
            if (token.Kind == HtmlTokenKind.End) { if (--depth == 0) return i; }
            else if (!token.SelfClosing) depth++;
        }
        throw new FactsPdfException("FPDF1101", $"Unclosed element '{name}'.", tokens[start].Offset);
    }

    // A canvas width or height attribute: a non-negative integer of ASCII digits in CSS px.
    private static double CanvasLength(string? value, double fallback, int offset)
    {
        if (value is null) return fallback;
        if (value.Length == 0 || value.Length > 5 || !value.All(char.IsAsciiDigit) || value == "0")
            throw new FactsPdfException("FPDF1103", "canvas width and height must be positive integers of at most five digits.", offset);
        return int.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>The most tables that may nest: a table, one in its cell, and one in that table's cell.</summary>
    internal const int MaxTableNesting = 3;

    // A table may start directly in a td or th that holds no list box (the cell is flat or already
    // holds blocks); anywhere else the nesting would need layout this engine does not have.
    private static void CheckNestedTable(TableMarkup outer, int outerCount, int cellBlocks, string parent, int offset)
    {
        if (outer.Cell is null || parent is not ("td" or "th"))
            throw new FactsPdfException("FPDF1101",
                "A table can be nested only directly inside a td or th cell, not in a caption, a row or another element.", offset);
        if (cellBlocks > 0)
            throw new FactsPdfException("FPDF1101", "A table cannot be nested inside a list in a table cell.", offset);
        if (outerCount + 2 > MaxTableNesting)
            throw new FactsPdfException("FPDF1003",
                $"Tables nest at most {MaxTableNesting} levels deep (a table, one in its cell and one in that table's cell).", offset);
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

    // Whether the link is a stylesheet link (rel="stylesheet" alone), whose caller-supplied stylesheet
    // CssStylesheets applied; any other combination with stylesheet (alternate stylesheet) still fails.
    private static bool LinkRelations(string value, int offset)
    {
        if (value.AsSpan().Trim(" \t\r\n\f").Equals("stylesheet", StringComparison.OrdinalIgnoreCase)) return true;
        // Any stylesheet token decides the diagnosis, whatever other tokens come first.
        foreach (var range in value.AsSpan().SplitAny(" \t\r\n\f"))
            if (value.AsSpan()[range].Equals("stylesheet", StringComparison.OrdinalIgnoreCase))
                throw new FactsPdfException("FPDF1204", "Only rel=\"stylesheet\" alone is supported; alternate stylesheets and other link types combined with it are not applied.", offset);
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
        return false;
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
