namespace FactsPDF;

/// <summary>A start or end tag the HTML parser implies: html, head or body the markup omits. It has
/// no attributes and no source position (Offset -1), and counts against no element or depth limit.</summary>
internal sealed record ImpliedHtmlToken(HtmlTokenKind Kind, string Value) : HtmlToken(Kind, Value, false, -1);

/// <summary>
/// Gives the token list the document structure the HTML parser builds (HTML tree construction,
/// the "initial" to "in body" insertion modes): html is the only element at document level, with
/// head and then body as its children. Omitted html, head and body tags are implied where the parser
/// implies them; style, script, template, title, meta, link and base before any body content go into
/// head (also after an explicit &lt;/head&gt;); content after &lt;/body&gt; or &lt;/html&gt; goes
/// into body. A repeated or misplaced html, head or body start tag without attributes is ignored, as
/// the parser ignores it; with attributes the parser would merge them into the existing element, which
/// is not supported (FPDF1101).
/// </summary>
internal sealed class HtmlImpliedElements
{
    private enum Mode { Initial, BeforeHead, InHead, AfterHead, InBody }

    private static readonly ImpliedHtmlToken Html = new(HtmlTokenKind.Start, "html");
    private static readonly ImpliedHtmlToken Head = new(HtmlTokenKind.Start, "head");
    private static readonly ImpliedHtmlToken HeadEnd = new(HtmlTokenKind.End, "head");
    private static readonly ImpliedHtmlToken Body = new(HtmlTokenKind.Start, "body");
    private static readonly ImpliedHtmlToken TableBody = new(HtmlTokenKind.Start, "tbody");
    private static readonly ImpliedHtmlToken TableBodyEnd = new(HtmlTokenKind.End, "tbody");

    private Mode mode;
    // The title or style element open inside head, whose text and end tag pass through unchanged.
    private string? openInHead;
    // Inside a table: whether a row group is open, and whether it is a tbody this class implied.
    private bool inTable, sectionOpen, impliedSection;

    private static bool HeadContent(string name) => name is "style" or "script" or "template" or "title" or "meta" or "link" or "base";

    /// <summary>Appends <paramref name="token"/> to <paramref name="tokens"/>, after the html, head
    /// and body tags it implies, and returns true; returns false for a tag the HTML parser ignores
    /// (or, for &lt;/head&gt;, defers), which is not appended.</summary>
    internal bool Add(List<HtmlToken> tokens, HtmlToken token)
    {
        if (mode == Mode.InBody)
        {
            if (token.Kind == HtmlTokenKind.End && token.Value is "body" or "html" or "head") return false;
            if (token.Kind == HtmlTokenKind.Start && token.Value is "html" or "head" or "body") { Ignore(token); return false; }
            AddInBody(tokens, token);
            return true;
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
                AddInBody(tokens, token);
                return true;
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
            default:
                // Any other end tag before body content fails in the reader as an unexpected end tag.
                tokens.Add(token);
                return true;
        }
    }

    /// <summary>Records a script or template element collection drops: before body content it is
    /// inserted into head.</summary>
    internal void Dropped(List<HtmlToken> tokens, string name)
    {
        if (mode != Mode.InBody && openInHead is null && HeadContent(name)) OpenHead(tokens);
    }

    /// <summary>Implies whatever the document still lacks: every document has html, head and body.</summary>
    internal void Finish(List<HtmlToken> tokens)
    {
        if (mode != Mode.InBody) StartBody(tokens);
    }

    // A tr directly in a table (or after its colgroup or caption) is put in a tbody the parser
    // implies, closed by the next row group or the table's end (HTML "in table" insertion mode).
    private void AddInBody(List<HtmlToken> tokens, HtmlToken token)
    {
        var name = token.Value;
        if (token.Kind == HtmlTokenKind.Start)
        {
            if (name == "table") { inTable = true; sectionOpen = impliedSection = false; }
            else if (inTable && name is "thead" or "tbody" or "tfoot")
            {
                if (impliedSection) tokens.Add(TableBodyEnd);
                sectionOpen = true; impliedSection = false;
            }
            else if (inTable && name == "tr" && !sectionOpen)
            {
                tokens.Add(TableBody);
                sectionOpen = impliedSection = true;
            }
        }
        else if (token.Kind == HtmlTokenKind.End && inTable)
        {
            if (name is "thead" or "tbody" or "tfoot") sectionOpen = impliedSection = false;
            else if (name == "table")
            {
                if (impliedSection) tokens.Add(TableBodyEnd);
                inTable = sectionOpen = impliedSection = false;
            }
        }
        tokens.Add(token);
    }

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
