namespace FactsPDF;

internal static class HtmlDocumentReader
{
    private sealed record Frame(string Name, TextStyle Style);
    private static bool ParagraphTag(string name) => name is "p" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6";
    private static bool Container(string name) => name is "div" or "section" or "article" or "body" or "html";

    public static List<Paragraph> Read(string html, PdfOptions options, CancellationToken cancellation)
    {
        var root = new TextStyle(options.FontSize, 1.2, new Rgb(0, 0, 0), TextAlignment.Left);
        var stack = new List<Frame> { new("#root", root) };
        var result = new List<Paragraph>();
        Paragraph? current = null;
        var elements = 0;

        void Flush()
        {
            if (current is not null) result.Add(current);
            current = null;
        }
        void Add(string text, TextStyle style, bool lineBreak = false)
        {
            if (current is null && !lineBreak && text.All(HtmlTokens.Space)) return;
            current ??= new Paragraph(stack[^1].Style);
            current.Runs.Add(new(text, style, lineBreak));
        }

        foreach (var token in HtmlTokens.Read(html, cancellation))
        {
            cancellation.ThrowIfCancellationRequested();
            if (token.Kind == HtmlTokenKind.Text)
            {
                if (stack[^1].Name == "title") continue;
                if (stack[^1].Name == "head")
                {
                    if (!token.Value.All(HtmlTokens.Space))
                        throw new FactsPdfException("FPDF1101", "Non-metadata text inside head is not supported.", token.Offset);
                    continue;
                }
                Add(HtmlTokens.Decode(token.Value, token.Offset), stack[^1].Style);
                continue;
            }
            var name = token.Value;
            if (!(ParagraphTag(name) || Container(name) || name is "span" or "br" or "head" or "title" or "meta"))
                throw new FactsPdfException("FPDF1102", $"Element '{name}' is not supported in this development slice.", token.Offset);
            if (token.Kind == HtmlTokenKind.End)
            {
                if (stack[^1].Name == "p" && name != "p" && Container(name))
                { Flush(); stack.RemoveAt(stack.Count - 1); }
                if (stack.Count == 1 || stack[^1].Name != name)
                    throw new FactsPdfException("FPDF1101", $"Unexpected end tag '{name}'.", token.Offset);
                if (ParagraphTag(name) || Container(name)) Flush();
                stack.RemoveAt(stack.Count - 1);
                continue;
            }
            if (++elements > options.MaxElements) throw new FactsPdfException("FPDF1002", "Element limit exceeded.", token.Offset);
            if (token.SelfClosing && name is not ("br" or "meta"))
                throw new FactsPdfException("FPDF1101", "Non-void HTML tags cannot be XML-self-closed in this subset.", token.Offset);
            if ((ParagraphTag(name) || Container(name)) && stack[^1].Name == "p")
            { Flush(); stack.RemoveAt(stack.Count - 1); }
            if ((ParagraphTag(name) || Container(name)) && stack.Any(f => ParagraphTag(f.Name) || f.Name == "span"))
                throw new FactsPdfException("FPDF1101", "Unsupported block/inline nesting.", token.Offset);
            if (stack[^1].Name == "title" || (stack[^1].Name == "head" && name is not ("title" or "meta")))
                throw new FactsPdfException("FPDF1101", "Unsupported head/title content.", token.Offset);
            if (name == "head" && stack[^1].Name is not ("html" or "#root"))
                throw new FactsPdfException("FPDF1101", "head must be at document level.", token.Offset);
            if (name is "title" or "meta" && stack[^1].Name != "head")
                throw new FactsPdfException("FPDF1101", "title/meta must be inside head.", token.Offset);

            var style = stack[^1].Style with { MarginBefore = 0, MarginAfter = ParagraphTag(name) ? 8 : 0,
                BreakBefore = false, BreakAfter = false };
            if (name.Length == 2 && name[0] == 'h' && name[1] is >= '1' and <= '6')
                style = style with { FontSize = style.FontSize * (name[1] switch
                { '1' => 2, '2' => 1.7, '3' => 1.5, '4' => 1.3, '5' => 1.15, _ => 1 }) };
            foreach (var (attribute, value) in token.Attributes!)
            {
                if (attribute == "style")
                {
                    if (name is "head" or "title" or "meta" or "br")
                        throw new FactsPdfException("FPDF1103", "Styling this metadata/void element is not supported.", token.Offset);
                    style = InlineCss.Apply(style, value, ParagraphTag(name), name == "span", token.Offset);
                }
                else if (name == "meta" && attribute == "charset" && value.Equals("utf-8", StringComparison.OrdinalIgnoreCase)) { }
                else if (attribute is "id" or "class" or "lang" or "title" || attribute.StartsWith("data-", StringComparison.Ordinal)) { }
                else throw new FactsPdfException("FPDF1103", $"Attribute '{attribute}' is not supported.", token.Offset);
            }
            if (style.FontSize is < 1 or > 144) throw new FactsPdfException("FPDF1202", "Computed font size is outside the supported range.", token.Offset);
            if (name == "br") { Add("", stack[^1].Style, true); continue; }
            if (name == "meta") continue;
            if (stack.Count > options.MaxDepth) throw new FactsPdfException("FPDF1003", "Nesting limit exceeded.", token.Offset);
            if (ParagraphTag(name) || Container(name)) Flush();
            stack.Add(new(name, style));
            if (ParagraphTag(name)) current = new Paragraph(style);
        }
        // HTML permits omitted p/body/html end tags; other unclosed elements are rejected here.
        while (stack.Count > 1 && stack[^1].Name is "p" or "body" or "html") stack.RemoveAt(stack.Count - 1);
        if (stack.Count > 1) throw new FactsPdfException("FPDF1101", $"Unclosed element '{stack[^1].Name}'.", html.Length);
        Flush();
        return result;
    }
}
