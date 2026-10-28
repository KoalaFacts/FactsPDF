namespace FactsPDF;

internal static class HtmlDocumentReader
{
    private sealed record Frame(string Name, TextStyle Style, BoxStyle BoxStyle, BlockNode? Box, int SourceOffset = -1);
    private static bool ParagraphTag(string name) => name is "p" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6";
    private static bool ListTag(string name) => name is "ol" or "ul";
    private static bool Container(string name) => name is "div" or "section" or "article" or "body" or "html" or "ol" or "ul" or "li";
    private static bool BoxTag(string name) => name is "img" or "table" or "col" or "td" or "th"
        || ParagraphTag(name) || Container(name) && name != "html";

    private static bool InlineTag(string name) => name is "span" or "b" or "strong" or "i" or "em";

    // Keep the old internal paragraph view for existing editor/tests; actual
    // conversions now receive a structured document via ReadTree.
    public static List<Paragraph> Read(string html, PdfOptions options, CancellationToken cancellation)
        => BlockTreeTraversal.Paragraphs(ReadTree(html, options, cancellation), cancellation).ToList();

    internal static DocumentRoot ReadTree(string html, PdfOptions options, CancellationToken cancellation)
    {
        // Compile every source before computing styles: a later style block can affect earlier content.
        var sheets = CssStylesheets.Collect(html, options, cancellation);
        var root = new TextStyle(options.FontSize, 1.2, new Rgb(0, 0, 0), TextAlignment.Left);
        var rootFontSize = root.FontSize; // Actual html computed size or configured initial size.
        var stack = new List<Frame> { new("#root", root, new BoxStyle(), null) };
        var path = new List<CssElement>(); // Actual elements only; the synthetic document root is not selectable.
        var result = new DocumentRoot();
        Paragraph? current = null;
        TableMarkup? table = null;
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
            {
                if (table?.Cell is { } cell)
                {
                    // Formatting whitespace and empty inline wrappers are not
                    // anonymous flow items. Explicit p margins are retained.
                    if (!anonymous || current.Runs.Any(r => r.IsBreak) || TextLines.HasTextContent(current, cancellation))
                        cell.Contents.Add(new TableCellText(current));
                }
                else CurrentChildren().Add(new ParagraphNode(current, anonymous));
            }
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
                current = table?.Cell is { } cell
                    ? new Paragraph(cell.Style, cell.SourceOffset)
                    : new Paragraph(stack[^1].Style, stack[^1].SourceOffset);
                anonymous = true;
            }
            // Keep is a block hint, never part of glyph-run coalescing.
            current.Runs.Add(new(text, style.KeepWithNext ? style with { KeepWithNext = false } : style, lineBreak, stack[^1].SourceOffset));
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
                var decoded = HtmlTokens.Decode(token.Value, token.Offset);
                if (ListTag(stack[^1].Name) || table is not null && table.Cell is null)
                {
                    if (!decoded.All(HtmlTokens.Space))
                        throw new FactsPdfException("FPDF1101", "Structural list/table elements cannot contain direct text.", token.Offset);
                    continue;
                }
                Add(decoded, stack[^1].Style);
                continue;
            }
            var name = token.Value;
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
                if (stack.Count > options.MaxDepth) throw new FactsPdfException("FPDF1003", "Nesting limit exceeded.", token.Offset);
                insideStyle = true; continue;
            }
            if (!(ParagraphTag(name) || Container(name) || InlineTag(name) || TableMarkup.IsTag(name)
                || name is "img" or "br" or "head" or "title" or "meta"))
                throw new FactsPdfException("FPDF1102", $"Element '{name}' is not supported in this development slice.", token.Offset);
            if (token.Kind == HtmlTokenKind.End)
            {
                if (table is null && stack[^1].Name == "p" && name != "p" && Container(name)) { Flush(); Pop(); }
                if (ListTag(name) && stack[^1].Name == "li") { Flush(); Pop(); }
                if (stack.Count == 1 || stack[^1].Name != name)
                    throw new FactsPdfException("FPDF1101", $"Unexpected end tag '{name}'.", token.Offset);
                if (TableMarkup.IsTag(name))
                {
                    if (name is "td" or "th") Flush();
                    table!.End(name, token.Offset);
                    if (name == "table") table = null;
                    Pop(); continue;
                }
                if (ParagraphTag(name) || Container(name)) Flush();
                Pop(); continue;
            }
            if (++elements > options.MaxElements) throw new FactsPdfException("FPDF1002", "Element limit exceeded.", token.Offset);
            if (token.SelfClosing && name is not ("img" or "br" or "meta" or "col"))
                throw new FactsPdfException("FPDF1101", "Non-void HTML tags cannot be XML-self-closed in this subset.", token.Offset);
            if (name == "table" && (table is not null || stack.Any(f => ParagraphTag(f.Name) || InlineTag(f.Name) || ListTag(f.Name) || f.Name == "li")))
                throw new FactsPdfException("FPDF1101", "Tables must be standalone blocks outside text elements, lists and tables.", token.Offset);
            if (TableMarkup.IsTag(name) && name != "table" && table is null)
                throw new FactsPdfException("FPDF1101", "Table structural elements require their explicit table parents.", token.Offset);
            table?.CheckChild(stack[^1].Name, name, token.Offset);
            if (name == "img" && stack.Any(f => ParagraphTag(f.Name) || InlineTag(f.Name) || ListTag(f.Name) || f.Name == "li"))
                throw new FactsPdfException("FPDF1101", "Images must be standalone blocks outside paragraphs, inline elements and lists.", token.Offset);
            if (table is null && (ParagraphTag(name) || Container(name)) && stack[^1].Name == "p") { Flush(); Pop(); }
            if (name == "li" && stack[^1].Name == "li") { Flush(); Pop(); }
            if ((ParagraphTag(name) || Container(name)) && stack.Any(f => ParagraphTag(f.Name) || InlineTag(f.Name)))
                throw new FactsPdfException("FPDF1101", "Unsupported block/inline nesting.", token.Offset);
            if (ListTag(stack[^1].Name) && name != "li" || name == "li" && !ListTag(stack[^1].Name))
                throw new FactsPdfException("FPDF1101", "li must be a direct child of ol/ul, whose only element children are li.", token.Offset);
            if (ListTag(name) && stack.Count(f => ListTag(f.Name)) >= 2)
                throw new FactsPdfException("FPDF1101", "Lists support at most two nested levels.", token.Offset);
            if (ListTag(name)) result.HasLists = true;
            if (stack[^1].Name == "title" || (stack[^1].Name == "head" && name is not ("title" or "meta")))
                throw new FactsPdfException("FPDF1101", "Unsupported head/title content.", token.Offset);
            if (name == "head" && stack[^1].Name is not ("html" or "#root"))
                throw new FactsPdfException("FPDF1101", "head must be at document level.", token.Offset);
            if (name is "title" or "meta" && stack[^1].Name != "head")
                throw new FactsPdfException("FPDF1101", "title/meta must be inside head.", token.Offset);

            var parent = stack[^1].Style;
            var parentBox = stack[^1].BoxStyle;
            var boxStyle = new BoxStyle();
            var style = parent with { MarginBefore = 0, MarginAfter = ParagraphTag(name) ? 8 : 0,
                BreakBefore = false, BreakAfter = false, KeepWithNext = false };
            if (name is "b" or "strong" or "th") style = style with { Bold = true };
            if (name is "i" or "em") style = style with { Italic = true };
            if (name.Length == 2 && name[0] == 'h' && name[1] is >= '1' and <= '6')
                style = style with { FontSize = style.FontSize * (name[1] switch
                { '1' => 2, '2' => 1.7, '3' => 1.5, '4' => 1.3, '5' => 1.15, _ => 1 }) };
            var columnSpan = 1;
            foreach (var (attribute, value) in token.Attributes!)
            {
                if (attribute == "style")
                {
                    if (name is "head" or "title" or "meta" or "br")
                        throw new FactsPdfException("FPDF1103", "Styling this metadata/void element is not supported.", token.Offset);
                    // Declarations were parsed once during source collection.
                }
                else if (attribute == "colspan" && name is "td" or "th")
                    columnSpan = ParseColumnSpan(value, table!.Node.Columns.Count, token.Offset, cancellation);
                else if (name == "meta" && attribute == "charset" && value.Equals("utf-8", StringComparison.OrdinalIgnoreCase)) { }
                else if (name == "img" && attribute is "src" or "alt") { }
                else if (attribute is "id" or "class" or "lang" or "title" || attribute.StartsWith("data-", StringComparison.Ordinal)) { }
                else throw new FactsPdfException("FPDF1103", $"Attribute '{attribute}' is not supported.", token.Offset);
            }
            if (name == "meta") continue;
            path.Add(CssElement.From(token));
            if (name is not ("head" or "title"))
                (style, boxStyle) = sheets.Compute(style, parent, root, rootFontSize, parentBox, path,
                    ParagraphTag(name), BoxTag(name),
                    token.Offset, allowStyling: name != "br");
            if (style.FontSize is < 1 or > 144) throw new FactsPdfException("FPDF1202", "Computed font size is outside the supported range.", token.Offset);
            if (name == "html") rootFontSize = style.FontSize;
            if (name == "br") { path.RemoveAt(path.Count - 1); Add("", parent, true); continue; }
            if (stack.Count > options.MaxDepth) throw new FactsPdfException("FPDF1003", "Nesting limit exceeded.", token.Offset);
            if (name == "table")
            {
                Flush();
                table = new(new TableNode(style, boxStyle, token.Offset));
                CurrentChildren().Add(table.Node);
                stack.Add(new(name, style, boxStyle, null, token.Offset));
                continue;
            }
            if (TableMarkup.IsTag(name))
            {
                table!.Start(name, style, boxStyle, token.Offset, columnSpan);
                if (name == "col") { path.RemoveAt(path.Count - 1); continue; }
                stack.Add(new(name, style, boxStyle, null, token.Offset));
                continue;
            }
            if (name == "img")
            {
                if (!token.Attributes.TryGetValue("src", out var resourceName) || !ImageResources.ValidName(resourceName))
                    throw new FactsPdfException("FPDF1103", "img requires an explicit resource name in src; paths and URLs are unsupported.", token.Offset);
                if (!options.Images.TryGetValue(resourceName, out var image))
                    throw new FactsPdfException("FPDF1604", $"Image resource '{resourceName}' was not supplied.", token.Offset);
                var imageNode = new ImageNode(image, style, boxStyle, token.Offset, token.Attributes.GetValueOrDefault("alt"));
                Flush();
                if (table?.Cell is { } imageCell) imageCell.Contents.Add(new TableCellImage(imageNode));
                else CurrentChildren().Add(imageNode);
                path.RemoveAt(path.Count - 1); continue;
            }
            if (ParagraphTag(name) || Container(name)) Flush();
            // `html`, head/title metadata and spans remain structural
            // inheritance/style contexts, never painted block boxes.
            BlockNode? box = null;
            if (BoxTag(name) && table?.Cell is null)
            {
                box = new BlockNode(name, style, boxStyle, token.Offset);
                CurrentChildren().Add(box);
            }
            stack.Add(new(name, style, boxStyle, box, token.Offset));
            if (ParagraphTag(name))
            {
                current = new Paragraph(style, token.Offset);
                anonymous = false;
            }
        }
        // HTML permits omitted p/body/html end tags; other unclosed elements are rejected here.
        while (table is null && stack.Count > 1 && stack[^1].Name is "p" or "body" or "html")
        {
            Flush(); // attach under the correct parent BEFORE popping
            Pop();
        }
        if (stack.Count > 1) throw new FactsPdfException("FPDF1101", $"Unclosed element '{stack[^1].Name}'.", html.Length);
        Flush();
        return result;
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
