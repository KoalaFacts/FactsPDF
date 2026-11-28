using System.Globalization;

namespace FactsPDF.Tests.UaDefaults;

/// <summary>
/// Reads the resolved default values of a probe element out of FactsPDF's document model,
/// in the vocabulary of the Chrome snapshot (lengths in pt, keywords as CSS serialises them).
/// A null value means the property is not modelled for that element.
/// </summary>
internal sealed class UaDefaultsModel
{
    internal static readonly string[] Properties =
    [
        "display",
        "margin-top", "margin-right", "margin-bottom", "margin-left",
        "padding-top", "padding-right", "padding-bottom", "padding-left",
        "border-top-width", "border-right-width", "border-bottom-width", "border-left-width",
        "border-top-style", "border-right-style", "border-bottom-style", "border-left-style",
        "font-size", "font-weight", "font-style", "line-height",
        "text-align", "text-decoration-line", "vertical-align", "list-style-type", "white-space",
    ];

    /// <summary>The CSS initial value of each property, which an unmodelled property keeps in FactsPDF.</summary>
    internal static readonly IReadOnlyDictionary<string, string> Initial = new Dictionary<string, string>
    {
        ["display"] = "inline",
        ["margin-top"] = "0pt", ["margin-right"] = "0pt", ["margin-bottom"] = "0pt", ["margin-left"] = "0pt",
        ["padding-top"] = "0pt", ["padding-right"] = "0pt", ["padding-bottom"] = "0pt", ["padding-left"] = "0pt",
        ["border-top-width"] = "0pt", ["border-right-width"] = "0pt", ["border-bottom-width"] = "0pt", ["border-left-width"] = "0pt",
        ["border-top-style"] = "none", ["border-right-style"] = "none", ["border-bottom-style"] = "none", ["border-left-style"] = "none",
        ["font-size"] = "12pt", ["font-weight"] = "400", ["font-style"] = "normal", ["line-height"] = "normal",
        ["text-align"] = "start", ["text-decoration-line"] = "none", ["vertical-align"] = "baseline",
        ["list-style-type"] = "disc", ["white-space"] = "normal",
    };

    private readonly string html;
    private readonly DocumentRoot root;
    private readonly Dictionary<int, BlockNode> blocks = [];
    private readonly Dictionary<int, TableNode> tables = [];
    private readonly Dictionary<int, TableRow> rows = [];
    private readonly Dictionary<int, TableCell> cells = [];
    private readonly Dictionary<int, TableCell> captions = [];
    private readonly Dictionary<int, ImageNode> images = [];
    private readonly Dictionary<int, TextRun> runs = [];
    private readonly Dictionary<int, InlineBoxRun> boxRuns = [];
    private readonly List<TextRun> breaks = [];
    private readonly List<(int Offset, TextStyle Style)> paragraphs = [];
    private readonly List<(int Offset, TableRow Row, TextStyle TableStyle)> orderedRows = [];

    internal UaDefaultsModel(string html, PdfOptions options)
    {
        this.html = html;
        root = HtmlDocumentReader.ReadTree(html, options, default);
        foreach (var child in root.Children) Index(child);
        orderedRows.Sort((a, b) => a.Offset.CompareTo(b.Offset));
    }

    private void Index(BlockChild child)
    {
        switch (child)
        {
            case BlockNode block:
                blocks.TryAdd(block.SourceOffset, block);
                foreach (var inner in block.Children) Index(inner);
                break;
            case Paragraph paragraph:
                IndexParagraph(paragraph);
                break;
            case ImageNode image:
                images.TryAdd(image.SourceOffset, image);
                break;
            case TableNode table:
                tables.TryAdd(table.SourceOffset, table);
                if (table.Caption is { } caption) { captions.TryAdd(caption.SourceOffset, caption); IndexCell(caption); }
                foreach (var row in table.Rows)
                {
                    rows.TryAdd(row.SourceOffset, row);
                    orderedRows.Add((row.SourceOffset, row, table.Style));
                    foreach (var cell in row.Cells) { cells.TryAdd(cell.SourceOffset, cell); IndexCell(cell); }
                }
                break;
        }
    }

    private void IndexCell(TableCell cell)
    {
        foreach (var content in cell.Contents)
        {
            if (content is TableCellText text) IndexParagraph(text.Paragraph);
            else if (content is TableCellImage image) images.TryAdd(image.Image.SourceOffset, image.Image);
        }
    }

    private void IndexParagraph(Paragraph paragraph)
    {
        paragraphs.Add((paragraph.SourceOffset, paragraph.Style));
        foreach (var run in paragraph.Runs)
        {
            if (run is InlineBoxRun edge) { if (edge.IsStart) boxRuns.TryAdd(edge.SourceOffset, edge); }
            else if (run is InlineImageRun inline) images.TryAdd(inline.Image.SourceOffset, inline.Image);
            else if (run.IsBreak) breaks.Add(run);
            else runs.TryAdd(run.SourceOffset, run);
        }
    }

    /// <summary>The resolved default values of the element a target key names.</summary>
    internal IReadOnlyDictionary<string, string?> Resolve(string key)
    {
        var (tag, _) = UaProbe.ParseTarget(key);
        var offset = UaCorpus.Offset(html, key);
        var values = new Dictionary<string, string?>();
        foreach (var property in Properties) values[property] = null;

        TextStyle? text;
        BoxStyle? box = null;
        string? display;
        string? verticalAlign = null;
        double textMarginBefore = 0, textMarginAfter = 0;

        if (tag == "html") { text = root.RootStyle; display = "block"; }
        else if (blocks.TryGetValue(offset, out var block) && block.Name == tag)
        {
            text = block.Style;
            box = block.BoxStyle;
            display = tag == "li" ? "list-item" : "block";
            textMarginBefore = block.Style.MarginBefore;
            textMarginAfter = block.Style.MarginAfter;
        }
        else if (tag == "table" && tables.TryGetValue(offset, out var table)) { text = table.Style; box = table.BoxStyle; display = "table"; }
        else if (tag == "caption" && captions.TryGetValue(offset, out var caption)) { text = caption.Style; box = caption.BoxStyle; display = "table-caption"; }
        else if (tag is "td" or "th" && cells.TryGetValue(offset, out var cell))
        {
            text = cell.Style; box = cell.BoxStyle; display = "table-cell";
            verticalAlign = Align(cell.BoxStyle.VerticalAlign);
        }
        else if (tag == "tr" && rows.TryGetValue(offset, out var row))
        {
            text = orderedRows.First(r => ReferenceEquals(r.Row, row)).TableStyle;
            box = row.Box;
            display = "table-row";
            verticalAlign = Align(row.Box.VerticalAlign);
        }
        else if (tag is "thead" or "tbody" or "tfoot")
        {
            var (_, firstRow, tableStyle) = orderedRows.First(r => r.Offset > offset);
            text = tableStyle;
            box = firstRow.GroupBox;
            // FactsPDF lays a tfoot out as the last rows of a tbody.
            display = tag == "thead" ? "table-header-group" : "table-row-group";
            verticalAlign = Align(firstRow.GroupBox.VerticalAlign);
        }
        else if (tag == "col") { text = null; display = "table-column"; }
        else if (tag == "colgroup") { text = null; display = null; }
        else if (tag is "img" or "svg" && images.TryGetValue(offset, out var image))
        {
            text = image.Style;
            box = image.BoxStyle;
            display = image.BoxStyle.DisplayBlock ? "block" : "inline";
            verticalAlign = image.BoxStyle.InlineAlign.Kind.ToString().ToLowerInvariant();
        }
        else
        {
            // An inline element is a text run; its run is merged into the paragraph's when the styles are equal.
            var run = runs.TryGetValue(offset, out var found) ? found : tag == "br" ? breaks.FirstOrDefault() : null;
            text = run?.Style ?? paragraphs.Last(p => p.Offset <= offset).Style;
            display = "inline";
            if (boxRuns.TryGetValue(offset, out var edgeRun))
                verticalAlign = edgeRun.Box.Align.Kind switch
                {
                    InlineAlignKind.Sub => "sub", InlineAlignKind.Super => "super", _ => "baseline",
                };
        }

        values["display"] = display;
        if (box is not null)
        {
            values["margin-top"] = Length(box.MarginTop, textMarginBefore);
            values["margin-bottom"] = Length(box.MarginBottom, textMarginAfter);
            values["margin-left"] = Length(box.MarginLeft);
            values["margin-right"] = Length(box.MarginRight);
            values["padding-top"] = Length(box.PaddingTop);
            values["padding-right"] = Length(box.PaddingRight);
            values["padding-bottom"] = Length(box.PaddingBottom);
            values["padding-left"] = Length(box.PaddingLeft);
            Border(values, "top", box.BorderTop);
            Border(values, "right", box.BorderRight);
            Border(values, "bottom", box.BorderBottom);
            Border(values, "left", box.BorderLeft);
            values["list-style-type"] = Marker(box.MarkerKind);
        }
        if (text is not null)
        {
            values["font-size"] = Points(text.FontSize);
            values["font-weight"] = text.Bold ? "700" : "400";
            values["font-style"] = text.Italic ? "italic" : "normal";
            values["line-height"] = text.HasNormalLineHeight ? "normal" : Points(text.AbsoluteLineHeight ?? text.LineHeight * text.FontSize);
            values["text-align"] = text.Alignment switch
            {
                TextAlignment.Left => "start",
                TextAlignment.Center => "center",
                TextAlignment.Right => "right",
                _ => "justify",
            };
            values["text-decoration-line"] = Decoration(text.Decoration);
            values["white-space"] = text.PreserveLineBreaks && text.PreserveSpaces ? "pre" : text.NoWrap ? "nowrap" : "normal";
        }
        values["vertical-align"] = verticalAlign;
        return values;
    }

    private static string Align(CellVerticalAlign align) => align == CellVerticalAlign.DefaultMiddle ? "middle" : align.ToString().ToLowerInvariant();

    private static void Border(Dictionary<string, string?> values, string side, BorderEdge edge)
    {
        values["border-" + side + "-width"] = Points(edge.EffectiveWidth);
        values["border-" + side + "-style"] = edge.Solid ? "solid" : "none";
    }

    private static string Length(CssLength length, double extraPoints = 0)
        => length.IsPercent ? Number(length.Value) + "%" : Points(length.Value + extraPoints);

    private static string Points(double value) => Number(value) + "pt";

    private static string Number(double value)
        => Math.Round(value, 3, MidpointRounding.AwayFromZero).ToString("0.###", CultureInfo.InvariantCulture);

    private static string Decoration(TextDecoration decoration)
    {
        if (decoration == TextDecoration.None) return "none";
        var parts = new List<string>();
        if (decoration.HasFlag(TextDecoration.Underline)) parts.Add("underline");
        if (decoration.HasFlag(TextDecoration.Overline)) parts.Add("overline");
        if (decoration.HasFlag(TextDecoration.LineThrough)) parts.Add("line-through");
        return string.Join(' ', parts);
    }

    private static string Marker(ListMarkerKind kind) => kind switch
    {
        ListMarkerKind.Disc => "disc",
        ListMarkerKind.Circle => "circle",
        ListMarkerKind.Square => "square",
        ListMarkerKind.Decimal => "decimal",
        ListMarkerKind.LowerAlpha => "lower-alpha",
        ListMarkerKind.UpperAlpha => "upper-alpha",
        ListMarkerKind.LowerRoman => "lower-roman",
        ListMarkerKind.UpperRoman => "upper-roman",
        _ => "none",
    };
}
