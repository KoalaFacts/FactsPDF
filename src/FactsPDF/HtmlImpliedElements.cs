namespace FactsPDF;

/// <summary>A start or end tag the HTML parser implies: html, head or body the markup omits, or a
/// tbody, tr or colgroup a table needs. It has no attributes and no source position (Offset -1), and
/// counts against no element or depth limit.</summary>
internal sealed record ImpliedHtmlToken(HtmlTokenKind Kind, string Value) : HtmlToken(Kind, Value, false, -1);

/// <summary>
/// Gives the token list the element tree the HTML parser builds (HTML tree construction, the
/// insertion modes from "initial" to "in cell"), so every later pass reads one well-nested tree:
/// <list type="bullet">
/// <item>html is the only element at document level, with head and then body as its children.
/// Omitted html, head and body tags are implied where the parser implies them; style, script,
/// template, title, meta, link and base before any body content go into head (also after an explicit
/// &lt;/head&gt;); content after &lt;/body&gt; or &lt;/html&gt; goes into body. A repeated or misplaced
/// html, head or body start tag without attributes is ignored, as the parser ignores it; with
/// attributes the parser would merge them into the existing element, which is not supported (FPDF1101).</item>
/// <item>Omitted end tags are written out where the parser implies them: a p closed by a block, an
/// hr, a table or a list item (and by &lt;/li&gt;, &lt;/dd&gt;, a block's end tag, a cell's end), an li
/// by the next li, a dt or dd by the next dt or dd, a heading by the next heading, a cell, row, row
/// group, caption or column group by the next table part or the table's end; a tbody is implied
/// before a tr or cell directly in a table, a tr before a cell directly in a row group and a colgroup
/// before a col directly in a table.</item>
/// <item>An end tag with no open element of its name in scope is ignored, as the parser ignores it
/// (except &lt;/p&gt;, where the parser inserts an empty paragraph, and &lt;/br&gt;, which it reads as a
/// br: both still reach the reader, which rejects them). At the end of the input every open element
/// is closed.</item>
/// </list>
/// The parser closes a formatting element (a, b, code, em, i, s, small, strong, u, ...) implicitly
/// only to reopen it in the next block (the list of active formatting elements and the adoption
/// agency algorithm), and moves misplaced table content in front of the table (foster parenting).
/// Neither is reproduced: an omitted end tag is written out only when no formatting element would be
/// closed by it, and otherwise the tags are left as written, so the reader rejects the misnesting
/// (FPDF1101) instead of printing a tree Chrome would not build.
/// </summary>
internal sealed class HtmlImpliedElements
{
    private enum Mode { Initial, BeforeHead, InHead, AfterHead, InBody }

    private static readonly ImpliedHtmlToken Html = new(HtmlTokenKind.Start, "html");
    private static readonly ImpliedHtmlToken Head = new(HtmlTokenKind.Start, "head");
    private static readonly ImpliedHtmlToken HeadEnd = new(HtmlTokenKind.End, "head");
    private static readonly ImpliedHtmlToken Body = new(HtmlTokenKind.Start, "body");

    private Mode mode;
    // The title or style element open inside head, whose text and end tag pass through unchanged.
    private string? openInHead;
    // The elements open inside body (body itself excluded), innermost last, and whether this class
    // implied each (a tbody, tr or colgroup), whose end it then implies too. One list per thread is
    // reused across documents (returned empty by Finish), so a conversion allocates no new stack.
    private readonly List<(string Name, bool Implied)> open = TakeOpenList();
    [ThreadStatic] private static List<(string Name, bool Implied)>? pooledOpen;
    private const int MaxPooledOpen = 1_024;

    private static List<(string Name, bool Implied)> TakeOpenList()
    {
        var list = pooledOpen ?? new(16);
        pooledOpen = null;
        list.Clear();
        return list;
    }
    // Open svg elements: inside them tags are foreign content, nested exactly as written.
    private int foreign;

    private static bool HeadContent(string name) => name is "style" or "script" or "noscript" or "template" or "title" or "meta" or "link" or "base";

    /// <summary>Appends <paramref name="token"/> to <paramref name="tokens"/>, after the tags it
    /// implies, and returns true; returns false for a tag the HTML parser ignores (or, for
    /// &lt;/head&gt;, defers), which is not appended.</summary>
    internal bool Add(List<HtmlToken> tokens, HtmlToken token)
    {
        if (mode == Mode.InBody)
        {
            if (token.Kind == HtmlTokenKind.End && token.Value is "body" or "html" or "head") return false;
            if (token.Kind == HtmlTokenKind.Start && token.Value is "html" or "head" or "body") { Ignore(token); return false; }
            return AddInBody(tokens, token);
        }
        if (openInHead is not null)
        {
            if (token.Kind == HtmlTokenKind.End && token.Value == openInHead) openInHead = null;
            tokens.Add(token);
            return true;
        }
        switch (token.Kind)
        {
            case HtmlTokenKind.Text when HtmlTokens.AllSpace(token.Value):
            case HtmlTokenKind.StyleText:
                tokens.Add(token);
                return true;
            case HtmlTokenKind.Text:
                StartBody(tokens);
                tokens.Add(token);
                return true;
            case HtmlTokenKind.Start when token.Value == "html":
                if (mode != Mode.Initial) { Ignore(token); return false; }
                tokens.Add(token);
                mode = Mode.BeforeHead;
                return true;
            case HtmlTokenKind.Start when token.Value == "head":
                if (mode == Mode.Initial) { tokens.Add(Html); mode = Mode.BeforeHead; }
                if (mode != Mode.BeforeHead) { Ignore(token); return false; }
                tokens.Add(token);
                mode = Mode.InHead;
                return true;
            case HtmlTokenKind.Start when token.Value == "body":
                CloseHead(tokens);
                tokens.Add(token);
                mode = Mode.InBody;
                return true;
            case HtmlTokenKind.Start when HeadContent(token.Value):
                OpenHead(tokens);
                tokens.Add(token);
                if (token.Value is "style" or "title" && !token.SelfClosing) openInHead = token.Value;
                return true;
            case HtmlTokenKind.Start:
                StartBody(tokens);
                return AddInBody(tokens, token);
            // The head's end tag waits until body content starts: head content after it is still
            // inserted into head.
            // Before head content an end tag head implies the head first.
            case HtmlTokenKind.End when token.Value == "head":
                OpenHead(tokens);
                mode = Mode.AfterHead;
                return false;
            case HtmlTokenKind.End when token.Value is "body" or "html":
                StartBody(tokens);
                return false;
            // The parser reads </br> as a br once body starts; the reader rejects it.
            case HtmlTokenKind.End when token.Value == "br":
                StartBody(tokens);
                tokens.Add(token);
                return true;
            default:
                // Before body content no other element is open, so the parser ignores any other end tag.
                return false;
        }
    }

    /// <summary>Records a script or template element collection drops: before body content it is
    /// inserted into head.</summary>
    internal void Dropped(List<HtmlToken> tokens, string name)
    {
        if (mode != Mode.InBody && openInHead is null && HeadContent(name)) OpenHead(tokens);
    }

    /// <summary>Implies whatever the document still lacks: every document has html, head and body,
    /// and the end of the input closes every element still open (at <paramref name="end"/>).</summary>
    internal void Finish(List<HtmlToken> tokens, int end)
    {
        if (mode != Mode.InBody) StartBody(tokens);
        if (open.Count > 0) Close(tokens, 0, null, end);
        foreign = 0;
        if (open.Capacity <= MaxPooledOpen) pooledOpen = open;
    }

    private bool AddInBody(List<HtmlToken> tokens, HtmlToken token)
    {
        if (token.Kind is HtmlTokenKind.Text or HtmlTokenKind.StyleText)
        {
            tokens.Add(token);
            return true;
        }
        var name = token.Value;
        if (foreign > 0)
        {
            // Foreign content keeps the nesting it is written with; the svg reader checks it.
            if (token.Kind == HtmlTokenKind.Start)
            {
                if (!token.SelfClosing) open.Add((name, false));
                if (name == "svg" && !token.SelfClosing) foreign++;
            }
            else if (open.Count > 0 && open[^1].Name == name)
            {
                open.RemoveAt(open.Count - 1);
                if (name == "svg") foreign--;
            }
            tokens.Add(token);
            return true;
        }
        if (token.Kind == HtmlTokenKind.Start)
        {
            StartTag(tokens, token);
            return true;
        }
        return EndTag(tokens, token);
    }

    private void StartTag(List<HtmlToken> tokens, HtmlToken token)
    {
        var name = token.Value;
        var offset = token.Offset;
        if (TablePart(name) && !TableStructure(tokens, name, offset))
        {
            // Left as written: the reader rejects the misnesting.
            Push(tokens, token);
            return;
        }
        if (name == "li") CloseListItem(tokens, offset, static item => item == "li");
        else if (name is "dd" or "dt") CloseListItem(tokens, offset, static item => item is "dd" or "dt");
        if (ClosesParagraph(name)) CloseParagraph(tokens, offset);
        // A heading start tag closes a heading that is the current node.
        if (Heading(name) && open.Count > 0 && Heading(open[^1].Name)) Close(tokens, open.Count - 1, null, offset);
        Push(tokens, token);
    }

    private void Push(List<HtmlToken> tokens, HtmlToken token)
    {
        tokens.Add(token);
        var name = token.Value;
        if (token.SelfClosing || Void(name)) return;
        open.Add((name, false));
        if (name == "svg") foreign++;
    }

    // The table insertion modes for a table part's start tag: closes the cell, row, row group,
    // caption or column group it ends and implies the tbody, tr or colgroup it needs, until the token
    // can be inserted. False when it cannot be placed in the open table (or no table is open).
    private bool TableStructure(List<HtmlToken> tokens, string name, int offset)
    {
        while (true)
        {
            var table = InScope("table", TableScope);
            // Outside a table only a table itself can start; any other table part is left to the reader.
            if (table < 0) return name == "table";
            var first = table + 1 < open.Count ? open[table + 1].Name : null;
            switch (first)
            {
                case null:
                    switch (name)
                    {
                        case "caption" or "colgroup" or "tbody" or "thead" or "tfoot": return true;
                        case "col": Imply(tokens, "colgroup"); return true;
                        case "tr" or "td" or "th": Imply(tokens, "tbody"); continue;
                        // A table start tag in table mode would close the open table; not reproduced.
                        default: return false;
                    }
                case "caption":
                    // A table in a caption is caption content; any other table part ends the caption.
                    if (name == "table") return true;
                    if (!ClosePoppable(tokens, table + 1, offset)) return false;
                    continue;
                case "colgroup":
                    if (name == "col") return open.Count == table + 2;
                    if (open.Count != table + 2) return false;
                    Close(tokens, table + 1, null, offset);
                    continue;
                case "thead" or "tbody" or "tfoot":
                    var row = table + 2 < open.Count && open[table + 2].Name == "tr" ? table + 2 : -1;
                    if (row < 0)
                    {
                        if (open.Count != table + 2) return false;
                        if (name == "tr") return true;
                        if (name is "td" or "th") { Imply(tokens, "tr"); return true; }
                        if (name == "table") return false;
                        Close(tokens, table + 1, null, offset);
                        continue;
                    }
                    var cell = row + 1 < open.Count && open[row + 1].Name is "td" or "th" ? row + 1 : -1;
                    if (cell >= 0)
                    {
                        // A table in a cell is cell content, inserted like any other.
                        if (name == "table") return true;
                        if (!ClosePoppable(tokens, cell, offset)) return false;
                        continue;
                    }
                    if (open.Count != row + 1) return false;
                    if (name is "td" or "th") return true;
                    if (name == "table") return false;
                    Close(tokens, row, null, offset);
                    continue;
                default:
                    return false;
            }
        }
    }

    private void Imply(List<HtmlToken> tokens, string name)
    {
        tokens.Add(new ImpliedHtmlToken(HtmlTokenKind.Start, name));
        open.Add((name, true));
    }

    // An li (or dt/dd) start tag closes the open item it follows: the nearest open item, past
    // address, div and p and any element that is not special, but not past another special element
    // (a nested list or block starts a new level).
    private void CloseListItem(List<HtmlToken> tokens, int offset, Func<string, bool> item)
    {
        for (var i = open.Count - 1; i >= 0; i--)
        {
            var name = open[i].Name;
            if (item(name))
            {
                ClosePoppable(tokens, i, offset);
                return;
            }
            if (Special(name) && name is not ("address" or "div" or "p")) return;
        }
    }

    private void CloseParagraph(List<HtmlToken> tokens, int offset)
    {
        var p = InScope("p", ButtonScope);
        if (p >= 0) ClosePoppable(tokens, p, offset);
    }

    // Closes open[index] and everything above it unless a formatting element is among them.
    private bool ClosePoppable(List<HtmlToken> tokens, int index, int offset)
    {
        for (var i = index + 1; i < open.Count; i++)
            if (Formatting(open[i].Name)) return false;
        Close(tokens, index, null, offset);
        return true;
    }

    private bool EndTag(List<HtmlToken> tokens, HtmlToken token)
    {
        var name = token.Value;
        int index;
        switch (name)
        {
            case "p":
                index = InScope("p", ButtonScope);
                // Without an open p the parser inserts an empty one; the reader rejects the tag.
                if (index < 0) { tokens.Add(token); return true; }
                break;
            case "br":
                tokens.Add(token);
                return true;
            case "li":
                index = InScope("li", ListItemScope);
                break;
            case "form":
                // The parser removes the form from wherever it is in the stack; only a form that the
                // omitted end tags (p, li, dd, dt) leave as the current node closes the same way here.
                index = InScope("form", DefaultScope);
                if (index < 0) return false;
                var top = open.Count - 1;
                while (top > index && ImpliedEnd(open[top].Name)) top--;
                if (top != index) { tokens.Add(token); return true; }
                break;
            case "td" or "th" or "tr" or "tbody" or "thead" or "tfoot" or "table" or "caption":
                index = InScope(name, TableScope);
                break;
            case "colgroup":
                index = open.Count > 0 && open[^1].Name == "colgroup" ? open.Count - 1 : -1;
                break;
            case "col":
                return false;
            case "h1" or "h2" or "h3" or "h4" or "h5" or "h6":
                // Any open heading in scope closes, whichever level the end tag names.
                index = -1;
                for (var i = open.Count - 1; i >= 0; i--)
                {
                    if (Heading(open[i].Name)) { index = i; break; }
                    if (DefaultScope(open[i].Name)) break;
                }
                if (index >= 0 && open[index].Name != name)
                {
                    if (!ClosePoppable(tokens, index, token.Offset)) { tokens.Add(token); return true; }
                    return true;
                }
                break;
            default:
                if (BlockEnd(name) || name is "dd" or "dt" or "object" or "applet" or "marquee")
                {
                    index = InScope(name, DefaultScope);
                    break;
                }
                // Any other end tag closes the nearest open element of its name unless a special
                // element comes first, when the parser ignores it. A formatting element's end tag
                // there runs the adoption agency algorithm instead, which is not reproduced.
                index = -1;
                for (var i = open.Count - 1; i >= 0; i--)
                {
                    if (open[i].Name == name) { index = i; break; }
                    if (!Special(open[i].Name)) continue;
                    if (Formatting(name) && Contains(name, i)) { tokens.Add(token); return true; }
                    return false;
                }
                break;
        }
        // No open element of the name in scope: the parser ignores the end tag.
        if (index < 0) return false;
        for (var i = index + 1; i < open.Count; i++)
            if (Formatting(open[i].Name)) { tokens.Add(token); return true; }
        Close(tokens, index, token, token.Offset);
        return true;
    }

    // Whether an element of the name is open below position `below`.
    private bool Contains(string name, int below)
    {
        for (var i = below - 1; i >= 0; i--)
            if (open[i].Name == name) return true;
        return false;
    }

    // Writes the end tags of open[index] and everything above it, innermost first, and removes
    // them: an implied element gets an implied end tag; the element at index takes `own` (the end tag
    // the source wrote for it) when there is one; any other gets an end tag at `offset`.
    private void Close(List<HtmlToken> tokens, int index, HtmlToken? own, int offset)
    {
        for (var i = open.Count - 1; i >= index; i--)
        {
            var (name, implied) = open[i];
            tokens.Add(implied ? new ImpliedHtmlToken(HtmlTokenKind.End, name)
                : i == index && own is not null && own.Value == name ? own
                : new HtmlToken(HtmlTokenKind.End, name, false, offset));
            if (name == "svg" && foreign > 0) foreign--;
        }
        open.RemoveRange(index, open.Count - index);
    }

    // The nearest open element of the name in the given scope, or -1.
    private int InScope(string name, Func<string, bool> boundary)
    {
        for (var i = open.Count - 1; i >= 0; i--)
        {
            if (open[i].Name == name) return i;
            if (boundary(open[i].Name)) return -1;
        }
        return -1;
    }

    // The scopes of the HTML parser ("has an element in scope" and its variants). html is never in
    // the list (body is its only open child here), so reaching the bottom is reaching html.
    private static bool DefaultScope(string name) => name is "applet" or "caption" or "table" or "td" or "th" or "marquee"
        or "object" or "template";
    private static bool ButtonScope(string name) => DefaultScope(name) || name == "button";
    private static bool ListItemScope(string name) => DefaultScope(name) || name is "ol" or "ul";
    private static bool TableScope(string name) => name is "table" or "template";

    private static bool TablePart(string name) => name is "caption" or "colgroup" or "col" or "tbody" or "thead" or "tfoot"
        or "tr" or "td" or "th" or "table";

    private static bool Heading(string name) => name is "h1" or "h2" or "h3" or "h4" or "h5" or "h6";

    // The start tags that close an open p in button scope first. table does so in no-quirks mode,
    // the mode of every document FactsPDF reads.
    private static bool ClosesParagraph(string name) => name is "address" or "article" or "aside" or "blockquote" or "center"
        or "details" or "dialog" or "dir" or "div" or "dl" or "fieldset" or "figcaption" or "figure" or "footer" or "header"
        or "hgroup" or "main" or "menu" or "nav" or "ol" or "p" or "search" or "section" or "summary" or "ul" or "pre"
        or "listing" or "form" or "li" or "dd" or "dt" or "plaintext" or "table" or "hr" or "xmp" || Heading(name);

    // The end tags the parser handles as a block's: it closes the element and everything above it.
    private static bool BlockEnd(string name) => name is "address" or "article" or "aside" or "blockquote" or "button"
        or "center" or "details" or "dialog" or "dir" or "div" or "dl" or "fieldset" or "figcaption" or "figure" or "footer"
        or "header" or "hgroup" or "listing" or "main" or "menu" or "nav" or "ol" or "pre" or "search" or "section"
        or "summary" or "ul";

    // "Generate implied end tags": the elements whose end tag may be omitted before another end tag.
    private static bool ImpliedEnd(string name) => name is "dd" or "dt" or "li" or "optgroup" or "option" or "p" or "rb"
        or "rp" or "rt" or "rtc";

    // The formatting elements, which the parser reopens after closing them implicitly.
    private static bool Formatting(string name) => name is "a" or "b" or "big" or "code" or "em" or "font" or "i" or "nobr"
        or "s" or "small" or "strike" or "strong" or "tt" or "u";

    // The HTML parser's special category (HTML namespace).
    private static bool Special(string name) => name is "address" or "applet" or "area" or "article" or "aside" or "base"
        or "basefont" or "bgsound" or "blockquote" or "body" or "br" or "button" or "caption" or "center" or "col"
        or "colgroup" or "dd" or "details" or "dir" or "div" or "dl" or "dt" or "embed" or "fieldset" or "figcaption"
        or "figure" or "footer" or "form" or "frame" or "frameset" or "head" or "header" or "hgroup" or "hr" or "html"
        or "iframe" or "img" or "input" or "keygen" or "li" or "link" or "listing" or "main" or "marquee" or "menu"
        or "meta" or "nav" or "noembed" or "noframes" or "noscript" or "object" or "ol" or "p" or "param" or "plaintext"
        or "pre" or "script" or "search" or "section" or "select" or "source" or "style" or "summary" or "table"
        or "tbody" or "td" or "template" or "textarea" or "tfoot" or "th" or "thead" or "title" or "tr" or "track"
        or "ul" or "wbr" or "xmp" || Heading(name);

    // Elements that never take an end tag (HTML void elements).
    private static bool Void(string name) => name is "area" or "base" or "br" or "col" or "embed" or "hr" or "img" or "input"
        or "link" or "meta" or "param" or "source" or "track" or "wbr" or "keygen" or "basefont" or "bgsound" or "frame";

    private void OpenHead(List<HtmlToken> tokens)
    {
        if (mode == Mode.Initial) { tokens.Add(Html); mode = Mode.BeforeHead; }
        if (mode == Mode.BeforeHead) { tokens.Add(Head); mode = Mode.InHead; }
    }

    private void CloseHead(List<HtmlToken> tokens)
    {
        OpenHead(tokens);
        tokens.Add(HeadEnd);
        mode = Mode.AfterHead;
    }

    private void StartBody(List<HtmlToken> tokens)
    {
        CloseHead(tokens);
        tokens.Add(Body);
        mode = Mode.InBody;
    }

    private static void Ignore(HtmlToken token)
    {
        if (token.Attributes is { Count: > 0 })
            throw new FactsPdfException("FPDF1101",
                $"A repeated or misplaced {token.Value} start tag with attributes is not supported: the HTML parser would merge them into the existing {token.Value} element.",
                token.Offset);
    }
}
