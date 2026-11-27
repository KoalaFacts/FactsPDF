namespace FactsPDF;

/// <summary>
/// Table cells that hold a list. A cell is a formatting-context root, so its content is laid out as
/// one block-flow fragment at the cell's content width on one page: markers, list and item margins
/// (collapsing among the cell's own blocks but never out of the cell), flex items, paragraphs and
/// images then follow the ordinary block layout, and the fragment's height is the cell content's.
/// </summary>
internal static class TableCellBlocks
{
    internal static bool Has(TableCell cell)
    {
        foreach (var content in cell.Contents)
            if (content is TableCellBlock) return true;
        return false;
    }

    internal static bool Has(TableRow row)
    {
        foreach (var cell in row.Cells)
            if (Has(cell)) return true;
        return false;
    }

    /// <param name="Page">The fragment's output: coordinates relative to the cell content's top left, PDF y relative to <paramref name="PageHeight"/>.</param>
    /// <param name="PageHeight">The fragment's page height, which its text baselines and disc centres are relative to.</param>
    /// <param name="Height">The cell content's height, trailing margins of the last block included.</param>
    internal sealed record Measured(LayoutPage Page, double PageHeight, double Height);

    internal static Measured Measure(TableCell cell, double contentWidth, PdfOptions options,
        Dictionary<(FontRole Role, int Scalar), (PdfFont? Font, double Width)> resolved, CancellationToken cancellation)
    {
        // The wrapper is the fragment's formatting-context root, so the cell's blocks collapse their
        // margins with each other like siblings but not through the cell's edges.
        var root = new BlockNode("div", cell.Style, BoxStyle.Initial, cell.SourceOffset);
        foreach (var content in cell.Contents)
        {
            cancellation.ThrowIfCancellationRequested();
            switch (content)
            {
                case TableCellBlock block:
                    root.Add(block.Node);
                    break;
                case TableCellImage image:
                    // A flat cell aligns its images by the cell's text-align; a block fragment does not.
                    if (cell.Style.Alignment != TextAlignment.Left)
                        throw new FactsPdfException("FPDF1201",
                            "An image in a table cell that also holds a list is supported only with left-aligned text.",
                            image.Image.SourceOffset);
                    root.Add(image.Image);
                    break;
                case TableCellText { Padding: { } padding } padded:
                {
                    var box = new BlockNode("p", padded.Paragraph.Style, padding, padded.Paragraph.SourceOffset);
                    box.Add(padded.Paragraph);
                    root.Add(box);
                    break;
                }
                case TableCellText text:
                    root.Add(text.Paragraph);
                    break;
                default:
                    throw new InvalidOperationException("Unexpected table cell content.");
            }
        }
        var fragment = new DocumentRoot { HasLists = true };
        fragment.Children.Add(root);
        var usableHeight = options.ContentHeight;
        var fragmentOptions = options with
        {
            PageWidth = contentWidth,
            PageHeight = usableHeight,
            Margin = 0,
            SideMargins = null,
            MaxPages = 1,
            // Charged once the cell is placed, by the table.
            MaxDisplayCommands = int.MaxValue,
        };
        var end = new FragmentEnd();
        List<LayoutPage> pages;
        try
        {
            pages = TextLayout.LayoutFragment(fragment, fragmentOptions, resolved, new LayoutBudget(), end, cancellation);
        }
        catch (FactsPdfException error) when (error.Code == "FPDF1303")
        {
            throw new FactsPdfException("FPDF1302",
                "A table cell with a list is taller than the usable page height; such cells are not split across pages.",
                cell.SourceOffset);
        }
        return new(pages[0], usableHeight, Math.Max(0, end.RootBlockEnd));
    }

    /// <summary>The display commands of a fragment's output, counted like <see cref="FlexLayout"/> lines.</summary>
    internal static int Commands(LayoutPage page)
    {
        var commands = page.Discs.Count + page.Images.Count;
        foreach (var run in page.Runs)
            commands += 1 + TextDecorationCommands.Count(run.Style.Decoration) + (run.Style.LinkUri is null ? 0 : 1);
        foreach (var box in page.PaintBoxes) commands += box.CommandCount;
        return commands;
    }
}
