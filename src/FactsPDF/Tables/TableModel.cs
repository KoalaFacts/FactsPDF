namespace FactsPDF;

// A table is one indivisible leaf in ordinary block traversal. Structural
// ancestry remains available to CSS while parsing, not as paginatable boxes.
internal sealed record TableNode(TextStyle Style, BoxStyle BoxStyle, int SourceOffset) : BlockChild
{
    internal List<TableColumn> Columns { get; } = [];
    internal List<TableRow> Rows { get; } = [];
}

internal sealed record TableColumn(CssLength? Width, int SourceOffset);
internal sealed record TableRow(bool IsHeader, int SourceOffset)
{
    internal List<TableCell> Cells { get; } = [];
}
internal sealed record TableCell(Paragraph Paragraph, BoxStyle BoxStyle, int SourceOffset)
{
    internal ImageNode? Image { get; set; }
}
