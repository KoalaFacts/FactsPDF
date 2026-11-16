using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

// Z5d: table layout measures every row through scratch lists shared by the whole table, so a
// row allocates no placement list, image-decoration list or empty retained-box list of its own.
[TestFixture]
[NonParallelizable]
public sealed class TableRowScratchTests
{
    private static string Table(int rows, string cell)
    {
        var html = new StringBuilder("<table style=\"width:520pt\"><colgroup><col style=\"width:80pt\"><col style=\"width:300pt\">"
            + "<col style=\"width:140pt\"></colgroup><tbody>");
        for (var i = 0; i < rows; i++) html.Append("<tr>").Append(cell).Append(cell).Append(cell).Append("</tr>");
        return html.Append("</tbody></table>").ToString();
    }

    private static long LayoutBytes(string html, PdfOptions options)
    {
        var document = HtmlDocumentReader.ReadTree(html, options, default);
        var before = GC.GetAllocatedBytesForCurrentThread();
        TextLayout.Layout(document, options, default);
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    private static double LayoutBytesPerRow(string cell)
    {
        var options = new PdfOptions();
        string small = Table(200, cell), large = Table(400, cell);
        for (var i = 0; i < 5; i++) { LayoutBytes(small, options); LayoutBytes(large, options); } // warm up
        long smallBytes = 0, largeBytes = 0;
        const int runs = 5;
        for (var i = 0; i < runs; i++) { smallBytes += LayoutBytes(small, options); largeBytes += LayoutBytes(large, options); }
        return (largeBytes - smallBytes) / (double)runs / 200;
    }

    [Test]
    public void LayingOutAPlainTableRowAllocatesNoPerRowScratchLists()
    {
        var perRow = LayoutBytesPerRow("<td>AB</td>");
        TestContext.Out.WriteLine($"plain table row layout: {perRow:F1} bytes");
        Assert.That(perRow, Is.LessThan(LimitPlain));
    }

    [Test]
    public void LayingOutABorderedTableRowAllocatesItsRetainedBoxesAtExactSize()
    {
        var perRow = LayoutBytesPerRow("<td style=\"border:0.5pt solid #789\">AB</td>");
        TestContext.Out.WriteLine($"bordered table row layout: {perRow:F1} bytes");
        Assert.That(perRow, Is.LessThan(LimitBordered));
    }

    // Main before Z5d: 2,138.4 and 2,652.2 bytes per row; with Z5d 1,850.4 and 2,388.2;
    // with Z5e (no paint-free cell box, no second row record, cells in an array) 1,458.4 and 2,284.2.
    private const double LimitPlain = 1_680, LimitBordered = 2_500;
}
