namespace FactsPDF;

/// <summary>Strict E1 shape validation; no HTML table repair or implicit columns.</summary>
internal sealed class TableMarkup(TableNode node)
{
    private enum Phase { NeedColumns, Columns, AfterColumns, Header, AfterHeader, Body, Complete }
    private Phase phase;
    private int headerRows, bodyRows;
    private TableRow? row;
    internal TableNode Node { get; } = node;
    internal TableCell? Cell { get; private set; }

    internal static bool IsTag(string name) => name is "table" or "colgroup" or "col" or "thead" or "tbody" or "tr" or "th" or "td";

    internal void CheckChild(string parent, string name, int offset)
    {
        var valid = Cell is not null
            ? name is "span" or "b" or "strong" or "i" or "em" or "br"
            : parent switch
            {
                "table" => name == "colgroup" && phase == Phase.NeedColumns
                    || name == "thead" && phase == Phase.AfterColumns
                    || name == "tbody" && phase is Phase.AfterColumns or Phase.AfterHeader,
                "colgroup" => name == "col" && Node.Columns.Count < 16,
                "thead" => name == "tr" && headerRows == 0,
                "tbody" => name == "tr",
                "tr" => row is not null && name == (row.IsHeader ? "th" : "td")
                    && row.Cells.Count < Node.Columns.Count,
                _ => false
            };
        if (!valid) throw Invalid("Unsupported table structure, placement or cell content.", offset);
    }

    internal void Start(string name, TextStyle style, BoxStyle box, int offset)
    {
        switch (name)
        {
            case "colgroup": phase = Phase.Columns; break;
            case "col": Node.Columns.Add(new(box.Width, offset)); break;
            case "thead": phase = Phase.Header; break;
            case "tbody": phase = Phase.Body; break;
            case "tr":
                row = new(phase == Phase.Header, offset);
                if (row.IsHeader) headerRows++; else bodyRows++;
                Node.Rows.Add(row);
                break;
            case "td": case "th":
                Cell = new(new Paragraph(style, offset), box, offset);
                row!.Cells.Add(Cell);
                break;
        }
    }

    internal void End(string name, int offset)
    {
        switch (name)
        {
            case "colgroup":
                if (Node.Columns.Count == 0) throw Invalid("colgroup requires at least one explicit column.", offset);
                phase = Phase.AfterColumns; break;
            case "thead":
                if (headerRows != 1) throw Invalid("thead requires exactly one row.", offset);
                phase = Phase.AfterHeader; break;
            case "tbody":
                if (bodyRows == 0) throw Invalid("tbody requires at least one row.", offset);
                phase = Phase.Complete; break;
            case "tr":
                if (row!.Cells.Count != Node.Columns.Count)
                    throw Invalid("Every table row must match the explicit column count.", row.SourceOffset);
                row = null; break;
            case "td": case "th": Cell = null; break;
            case "table":
                if (phase != Phase.Complete) throw Invalid("table requires one colgroup and one nonempty tbody in order.", offset);
                break;
        }
    }

    private static FactsPdfException Invalid(string message, int offset) => new("FPDF1101", message, offset);
}
