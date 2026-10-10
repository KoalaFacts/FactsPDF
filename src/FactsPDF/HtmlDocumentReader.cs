namespace FactsPDF;

internal static class HtmlDocumentReader
{
    private sealed record Frame(string Name, TextStyle Style, BlockNode? Box);
    private static bool ParagraphTag(string name) => name is "p" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6";
    private static bool Container(string name) => name is "div" or "section" or "article" or "body" or "html";

    // Keep the old internal paragraph view for existing editor/tests; actual
    // conversions now receive a structured document via ReadTree.
    public static List<Paragraph> Read(string html, PdfOptions options, CancellationToken cancellation)
        => BlockTreeTraversal.Paragraphs(ReadTree(html, options, cancellation), cancellation).ToList();

    internal static DocumentRoot ReadTree(string html, PdfOptions options, CancellationToken cancellation)
    {
        // Compile every source before computing styles: a later style block can affect earlier content.
        var sheets = CssStylesheets.Collect(html, options, cancellation);
        var root = new TextStyle(options.FontSize, 1.2, new Rgb(0, 0, 0), TextAlignment.Left);
        var stack = new List<Frame> { new("#root", root, null) };
        var path = new List<CssElement>(); // Actual elements only; the synthetic document root is not selectable.
        var result = new DocumentRoot();
        Paragraph? current = null;
        var anonymous = false;
        var elements = 0; var insideStyle = false;

        List<BlockChild> CurrentChildren()
        {
            for (var i = stack.Count - 1; i >= 0; i--)
            {
                var box = stack[i].Box;
                if (box is not null) return box.Children;
            }
            return result.Children;
        }

        void Flush()
        {
            if (current is not null)
                CurrentChildren().Add(new ParagraphNode(current, anonymous));
            current = null;
            anonymous = false;
        }
        void Pop() { stack.RemoveAt(stack.Count - 1); path.RemoveAt(path.Count - 1); }
        void Add(string text, TextStyle style, bool lineBreak = false)
        {
            if (current is null && !lineBreak && text.All(HtmlTokens.Space)) return;
            if (current is null)
            {
                // Preserve the existing text-layout paragraph style for a
                // first-inline-run span (including text-align). The retained
                // BlockNode.Style separately stores the actual containing-box
                // computed style for future block-aware layout.
                current = new Paragraph(stack[^1].Style);
                anonymous = true;
            }
            current.Runs.Add(new(text, style, lineBreak));
        }

        foreach (var token in HtmlTokens.Read(html, cancellation))
        {
            cancellation.ThrowIfCancellationRequested();
            if (token.Kind == HtmlTokenKind.StyleText) continue;
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
            if (name == "style")
            {
                if (token.Kind == HtmlTokenKind.End)
                {
                    if (!insideStyle) throw new FactsPdfException("FPDF1101", "Unexpected style end tag.", token.Offset);
                    insideStyle = false; continue;
                }
                if (++elements > options.MaxElements) throw new FactsPdfException("FPDF1002", "Element limit exceeded.", token.Offset);
                if (!(stack[^1].Name is "#root" or "head" || Container(stack[^1].Name)))
                    throw new FactsPdfException("FPDF1101", "style must be at document, head or container level.", token.Offset);
                if (stack.Count > options.MaxDepth) throw new FactsPdfException("FPDF1003", "Nesting limit exceeded.", token.Offset);
                insideStyle = true; continue;
            }
            if (!(ParagraphTag(name) || Container(name) || name is "span" or "br" or "head" or "title" or "meta"))
                throw new FactsPdfException("FPDF1102", $"Element '{name}' is not supported in this development slice.", token.Offset);
            if (token.Kind == HtmlTokenKind.End)
            {
                if (stack[^1].Name == "p" && name != "p" && Container(name)) { Flush(); Pop(); }
                if (stack.Count == 1 || stack[^1].Name != name)
                    throw new FactsPdfException("FPDF1101", $"Unexpected end tag '{name}'.", token.Offset);
                if (ParagraphTag(name) || Container(name)) Flush();
                Pop(); continue;
            }
            if (++elements > options.MaxElements) throw new FactsPdfException("FPDF1002", "Element limit exceeded.", token.Offset);
            if (token.SelfClosing && name is not ("br" or "meta"))
                throw new FactsPdfException("FPDF1101", "Non-void HTML tags cannot be XML-self-closed in this subset.", token.Offset);
            if ((ParagraphTag(name) || Container(name)) && stack[^1].Name == "p") { Flush(); Pop(); }
            if ((ParagraphTag(name) || Container(name)) && stack.Any(f => ParagraphTag(f.Name) || f.Name == "span"))
                throw new FactsPdfException("FPDF1101", "Unsupported block/inline nesting.", token.Offset);
            if (stack[^1].Name == "title" || (stack[^1].Name == "head" && name is not ("title" or "meta")))
                throw new FactsPdfException("FPDF1101", "Unsupported head/title content.", token.Offset);
            if (name == "head" && stack[^1].Name is not ("html" or "#root"))
                throw new FactsPdfException("FPDF1101", "head must be at document level.", token.Offset);
            if (name is "title" or "meta" && stack[^1].Name != "head")
                throw new FactsPdfException("FPDF1101", "title/meta must be inside head.", token.Offset);

            var parent = stack[^1].Style;
            var style = parent with { MarginBefore = 0, MarginAfter = ParagraphTag(name) ? 8 : 0,
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
                    // Declarations were parsed once during source collection.
                }
                else if (name == "meta" && attribute == "charset" && value.Equals("utf-8", StringComparison.OrdinalIgnoreCase)) { }
                else if (attribute is "id" or "class" or "lang" or "title" || attribute.StartsWith("data-", StringComparison.Ordinal)) { }
                else throw new FactsPdfException("FPDF1103", $"Attribute '{attribute}' is not supported.", token.Offset);
            }
            if (name == "meta") continue;
            path.Add(CssElement.From(token));
            if (name is not ("head" or "title"))
                style = sheets.Compute(style, parent, root, path, ParagraphTag(name), token.Offset, allowStyling: name != "br");
            if (style.FontSize is < 1 or > 144) throw new FactsPdfException("FPDF1202", "Computed font size is outside the supported range.", token.Offset);
            if (name == "br") { path.RemoveAt(path.Count - 1); Add("", parent, true); continue; }
            if (stack.Count > options.MaxDepth) throw new FactsPdfException("FPDF1003", "Nesting limit exceeded.", token.Offset);
            if (ParagraphTag(name) || Container(name)) Flush();
            // `html`, head/title metadata and spans remain structural
            // inheritance/style contexts, never painted block boxes.
            BlockNode? box = null;
            if (ParagraphTag(name) || name is "body" or "div" or "section" or "article")
            {
                box = new BlockNode(name, style, token.Offset);
                CurrentChildren().Add(box);
            }
            stack.Add(new(name, style, box));
            if (ParagraphTag(name))
            {
                current = new Paragraph(style);
                anonymous = false;
            }
        }
        // HTML permits omitted p/body/html end tags; other unclosed elements are rejected here.
        while (stack.Count > 1 && stack[^1].Name is "p" or "body" or "html")
        {
            Flush(); // attach under the correct parent BEFORE popping
            Pop();
        }
        if (stack.Count > 1) throw new FactsPdfException("FPDF1101", $"Unclosed element '{stack[^1].Name}'.", html.Length);
        Flush();
        return result;
    }
}
