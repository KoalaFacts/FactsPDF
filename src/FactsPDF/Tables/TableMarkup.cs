namespace FactsPDF;

/// <summary>Strict table shape validation; no HTML table repair.</summary>
/// <remarks>
/// An optional <c>caption</c> comes first. Columns come from one <c>colgroup</c> of <c>col</c> elements or, without a <c>colgroup</c>, from
/// the cells of the first row; every later row must cover the same number of columns.
/// </remarks>
internal sealed class TableMarkup(TableNode node)
{
    /// <summary>The most columns a table may have, explicit or taken from its first row.</summary>
    internal const int MaxColumns = 16;
    private enum Phase { NeedColumns, Columns, AfterColumns, Header, AfterHeader, Body, Complete, Footer, AfterFooter }
    private Phase phase;
    private int headerRows, bodyRows, footerRows;
    private int coveredColumns;
    private TableRow? row;
    // The backgrounds of the open row group and row, painted under cells without their own.
    private Rgb? groupBackground, rowBackground;
    // The open row group's box, text colour and index, kept on its rows for collapsed borders.
    private BoxStyle groupBox = BoxStyle.Initial;
    private Rgb groupColor;
    private int groupIndex;
    // True while the first row of a table without colgroup is open: it sets the column count.
    private bool countingColumns;
    internal TableNode Node { get; } = node;
    internal TableCell? Cell { get; private set; }

    /// <summary>The columns a cell may still span: the rest of the row's columns, or of <see cref="MaxColumns"/> while the first row sets the count.</summary>
    internal int RemainingColumns => (countingColumns ? MaxColumns : Node.Columns.Count) - coveredColumns;

    internal static bool IsTag(string name) => name is "table" or "caption" or "colgroup" or "col" or "thead" or "tbody" or "tfoot" or "tr" or "th" or "td";

    internal void CheckChild(string parent, string name, int offset)
    {
        var valid = Cell is not null
            ? name is "p" or "img"
                ? parent is "td" or "th"
                : HtmlDocumentReader.InlineTag(name) || name == "br"
            : parent switch
            {
                "table" => name == "caption" && phase == Phase.NeedColumns && Node.Caption is null && Node.Columns.Count == 0
                    || name == "colgroup" && phase == Phase.NeedColumns
                    || name == "thead" && phase is Phase.NeedColumns or Phase.AfterColumns
                    || name == "tbody" && phase is Phase.NeedColumns or Phase.AfterColumns or Phase.AfterHeader
                    || name == "tfoot" && phase == Phase.Complete,
                "colgroup" => name == "col" && Node.Columns.Count < MaxColumns,
                "thead" => name == "tr" && headerRows == 0,
                "tbody" or "tfoot" => name == "tr",
                // Any row may hold th and td cells, as in HTML (label/value rows: th then td).
                "tr" => row is not null && name is "th" or "td" && RemainingColumns > 0,
                _ => false
            };
        if (!valid) throw Invalid("Unsupported table structure, placement or cell content.", offset);
    }

    internal void Start(string name, TextStyle style, BoxStyle box, int offset, int columnSpan = 1)
    {
        switch (name)
        {
            case "caption":
                // Laid out above the table at the table's width (TableLayout); its own width,
                // margins and caption-side:bottom are not implemented.
                if (box.Width is not null)
                    throw new FactsPdfException("FPDF1201", "caption width is not supported; a caption takes its table's width.", offset);
                Cell = new(style, box, offset, 1);
                Node.Caption = Cell;
                break;
            case "colgroup": phase = Phase.Columns; break;
            case "col": Node.Columns.Add(new(box.Width, offset)); break;
            case "thead":
                if (phase == Phase.NeedColumns) countingColumns = true;
                OpenGroup(style, box);
                phase = Phase.Header; break;
            case "tbody":
                if (phase == Phase.NeedColumns) countingColumns = true;
                OpenGroup(style, box);
                phase = Phase.Body; break;
            // Footer rows are ordinary body rows placed after the tbody rows,
            // where the source already puts them; they are not repeated per page.
            case "tfoot": OpenGroup(style, box); phase = Phase.Footer; break;
            case "tr":
                coveredColumns = 0;
                rowBackground = box.BackgroundColor;
                row = new(phase == Phase.Header, offset, box.AvoidBreakInside)
                {
                    Box = box, Color = style.Color, Group = groupIndex, GroupBox = groupBox, GroupColor = groupColor
                };
                if (row.IsHeader) headerRows++;
                else if (phase == Phase.Footer) footerRows++;
                else bodyRows++;
                Node.Rows.Add(row);
                break;
            case "td": case "th":
                if (columnSpan > RemainingColumns)
                    throw Invalid("Cell span exceeds the remaining table columns.", offset);
                coveredColumns += columnSpan;
                // With separated borders and no spacing a row's cells cover the row exactly, so a
                // row or group background painted under them shows only through cells without
                // their own: the same paint as giving those cells the background.
                if (box.BackgroundColor is null && (rowBackground ?? groupBackground) is { } inherited)
                    box = box with { BackgroundColor = inherited };
                Cell = new(style, box, offset, columnSpan);
                row!.Cells.Add(Cell);
                break;
        }
    }

    internal void End(string name, int offset)
    {
        switch (name)
        {
            case "caption":
                Cell = null; break;
            case "colgroup":
                if (Node.Columns.Count == 0) throw Invalid("colgroup requires at least one explicit column.", offset);
                phase = Phase.AfterColumns; break;
            case "thead":
                if (headerRows != 1) throw Invalid("thead requires exactly one row.", offset);
                phase = Phase.AfterHeader; break;
            case "tbody":
                if (bodyRows == 0) throw Invalid("tbody requires at least one row.", offset);
                phase = Phase.Complete; break;
            case "tfoot":
                if (footerRows == 0) throw Invalid("tfoot requires at least one row.", offset);
                phase = Phase.AfterFooter; break;
            case "tr":
                if (countingColumns)
                {
                    // The first row of a table without colgroup sets its columns, all automatic.
                    if (coveredColumns == 0) throw Invalid("The first table row must contain a cell.", offset);
                    for (var i = 0; i < coveredColumns; i++) Node.Columns.Add(new(null, row!.SourceOffset));
                    countingColumns = false;
                }
                if (coveredColumns != Node.Columns.Count)
                    throw Invalid("Every table row must cover the table's column count.", offset);
                row = null; break;
            case "td": case "th":
                // Retain the legacy empty-text projection, but no phantom text
                // item for image-only cells or rich cells with actual items.
                if (Cell!.Contents.Count == 0)
                    Cell.Contents.Add(new TableCellText(new Paragraph(Cell.Style, Cell.SourceOffset)));
                Cell = null; break;
            case "table":
                if (phase is not (Phase.Complete or Phase.AfterFooter))
                    throw Invalid("table requires an optional caption, an optional colgroup, an optional one-row thead and one nonempty tbody in order, then an optional tfoot.", offset);
                if (Node.Style.CollapseBorders) TableBorderCollapse.Apply(Node);
                break;
        }
    }

    private void OpenGroup(TextStyle style, BoxStyle box)
    {
        groupBackground = box.BackgroundColor;
        groupBox = box;
        groupColor = style.Color;
        groupIndex++;
    }

    private static FactsPdfException Invalid(string message, int offset) => new("FPDF1101", message, offset);
}
