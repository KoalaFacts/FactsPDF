namespace FactsPDF;

// A table is one indivisible leaf in ordinary block traversal. Structural
// ancestry remains available to CSS while parsing, not as paginatable boxes.
internal sealed record TableNode(TextStyle Style, BoxStyle BoxStyle, int SourceOffset) : BlockChild
{
    internal List<TableColumn> Columns { get; } = [];
    internal List<TableRow> Rows { get; } = [];
    /// <summary>The optional caption, laid out above the table at its width.</summary>
    internal TableCell? Caption { get; set; }
    /// <summary>The resolved borders of a border-collapse:collapse table (null when separated).</summary>
    internal TableBorderGrid? Collapsed { get; set; }
    /// <summary>The box the table lays out and paints with: its own, or in the collapsing model
    /// no padding and half its outer collapsed borders, which paint nothing themselves.</summary>
    internal BoxStyle LayoutBox => Collapsed?.TableBox ?? BoxStyle;
}

internal sealed record TableColumn(CssLength? Width, int SourceOffset);
internal sealed record TableRow(bool IsHeader, int SourceOffset, bool AvoidBreakInside = false)
{
    internal List<TableCell> Cells { get; } = [];
    // The row's and its row group's own box and text colour; only collapsed borders read them.
    internal BoxStyle Box { get; init; } = BoxStyle.Initial;
    internal Rgb Color { get; init; }
    internal int Group { get; init; }
    internal BoxStyle GroupBox { get; init; } = BoxStyle.Initial;
    internal Rgb GroupColor { get; init; }
}
internal abstract record TableCellContent;
internal sealed record TableCellText(Paragraph Paragraph) : TableCellContent;
internal sealed record TableCellImage(ImageNode Image) : TableCellContent;
internal sealed record TableCell(TextStyle Style, BoxStyle BoxStyle, int SourceOffset, int ColumnSpan)
{
    private TableCellContents contents;

    // Stored in the cell itself instead of a separate List object and array per cell; Contents
    // returns the list by reference so Add grows this cell's copy.
    internal ref TableCellContents Contents => ref contents;
}

/// <summary>A table cell's growable content list, stored inline in its <see cref="TableCell"/>.</summary>
/// <remarks>
/// A lone item (the usual text or image cell) is kept in the list's own slot; only a second item
/// allocates an array (2, then doubling), so a one-item cell allocates no list and no array.
/// </remarks>
internal struct TableCellContents : IReadOnlyList<TableCellContent>
{
    // null (no items), the single TableCellContent, or a TableCellContent[] holding every item.
    private object? items;
    private int count;

    public readonly int Count => count;

    /// <summary>How many items fit before the list grows (0 before the first item, 1 for the inline slot).</summary>
    public readonly int Capacity => items is TableCellContent[] array ? array.Length : items is null ? 0 : 1;

    public readonly TableCellContent this[int index]
        => (uint)index < (uint)count ? At(items, index) : throw new ArgumentOutOfRangeException(nameof(index));

    private static TableCellContent At(object? items, int index)
        => items is TableCellContent[] array ? array[index] : (TableCellContent)items!;

    internal void Add(TableCellContent content)
    {
        if (items is TableCellContent[] array)
        {
            if (count == array.Length) Array.Resize(ref array, checked(array.Length * 2));
            array[count++] = content;
            items = array;
        }
        else if (count == 0)
        {
            items = content;
            count = 1;
        }
        else
        {
            items = new TableCellContent[] { (TableCellContent)items!, content };
            count = 2;
        }
    }

    public readonly Enumerator GetEnumerator() => new(items, count);
    readonly IEnumerator<TableCellContent> IEnumerable<TableCellContent>.GetEnumerator() => GetEnumerator();
    readonly System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    internal struct Enumerator(object? items, int count) : IEnumerator<TableCellContent>
    {
        private int index = -1;
        public readonly TableCellContent Current => At(items, index);
        readonly object System.Collections.IEnumerator.Current => Current;
        public bool MoveNext() => ++index < count;
        public void Reset() => index = -1;
        public readonly void Dispose() { }
    }
}
