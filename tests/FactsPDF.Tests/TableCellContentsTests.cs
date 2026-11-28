using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

// A table cell keeps its content items inline (one slot, then an array from the second item)
// instead of a List object and its four-slot array per cell, so reading a one-item cell
// allocates no list of its own.
[TestFixture]
[NonParallelizable]
public sealed class TableCellContentsTests
{
    private static readonly PdfFont Font = PdfFont.FromBytes(FontFixture.Create(listDigits: true));

    private static string Table(int rows)
    {
        var html = new StringBuilder("<table style=\"width:520pt\"><colgroup><col style=\"width:80pt\"><col style=\"width:300pt\">"
            + "<col style=\"width:140pt\"></colgroup><tbody>");
        for (var i = 0; i < rows; i++)
            html.Append("<tr><td style=\"padding:3pt;border:0.5pt solid #789\">AB</td><td style=\"padding:3pt\">Wi AB Wi AB Wi AB Wi</td><td>AB</td></tr>");
        return html.Append("</tbody></table>").ToString();
    }

    private static long ReadAllocated(string html, PdfOptions options)
    {
        for (var i = 0; i < 3; i++) HtmlDocumentReader.ReadTree(html, options, default);
        var before = GC.GetAllocatedBytesForCurrentThread();
        HtmlDocumentReader.ReadTree(html, options, default);
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    private static TableCell[] Cells(string html)
    {
        var root = HtmlDocumentReader.ReadTree(html, new PdfOptions(), default);
        var table = (TableNode)((BlockNode)root.Children[0]).Children[0]; // inside the body
        return [.. table.Rows.SelectMany(row => row.Cells)];
    }

    // Measured on main e17e015: 1,708.6 bytes read per three-cell row with a List and its
    // four-slot array per cell; 1,468.6 with the item inline (80 bytes less per cell).
    [AllocationBudget]
    [Test]
    public void ReadingOneItemCellsAllocatesNoContentListPerCell()
    {
        var options = new PdfOptions { Fonts = [Font], SubsetFonts = true };
        var small = ReadAllocated(Table(200), options);
        var large = ReadAllocated(Table(400), options);
        var perRow = (large - small) / 200.0;
        TestContext.Out.WriteLine($"read per three-cell row: {perRow:F1} bytes");
        Assert.That(perRow, Is.LessThan(PerRowLimit));
    }

    private const double PerRowLimit = 1_520;

    [Test]
    public void AOneItemCellKeepsItsItemInline()
    {
        var cell = Cells("<table><colgroup><col></colgroup><tbody><tr><td>AB</td></tr></tbody></table>")[0];
        Assert.That(cell.Contents.Count, Is.EqualTo(1));
        Assert.That(cell.Contents.Capacity, Is.EqualTo(1));
        Assert.That(cell.Contents[0], Is.TypeOf<TableCellText>());
    }

    [Test]
    public void AnEmptyCellKeepsItsEmptyTextItem()
    {
        var cell = Cells("<table><colgroup><col></colgroup><tbody><tr><td></td></tr></tbody></table>")[0];
        Assert.That(cell.Contents.Count, Is.EqualTo(1));
        Assert.That(((TableCellText)cell.Contents[0]).Paragraph.Runs.Count, Is.EqualTo(0));
    }

    [Test]
    public void ItemsBeyondTheInlineSlotKeepTheirOrder()
    {
        var style = Cells("<table><colgroup><col></colgroup><tbody><tr><td>AB</td></tr></tbody></table>")[0].Style;
        var contents = new TableCellContents();
        var items = new TableCellContent[5];
        for (var i = 0; i < items.Length; i++)
        {
            items[i] = new TableCellText(new Paragraph(style, i));
            contents.Add(items[i]);
        }
        Assert.That(contents.Count, Is.EqualTo(5));
        Assert.That(contents.Capacity, Is.EqualTo(8));
        Assert.That(contents, Is.EqualTo(items));
        for (var i = 0; i < items.Length; i++) Assert.That(contents[i], Is.SameAs(items[i]));
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = contents[5]);
    }
}
