using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

// Z5e: a measured table row holds its cells in one exact-size array, takes its paint range
// without a second record copy, and a cell that paints nothing creates no box.
[TestFixture]
[NonParallelizable]
public sealed class TableRowRecordTests
{
    // The 520pt table must fit the default page, which the 8px body margin would narrow.
    private const string NoBodyMargin = "<style>html,body{margin:0}</style>";

    private static string Table(int rows, string cell)
    {
        var html = new StringBuilder(NoBodyMargin + "<table style=\"width:520pt\"><colgroup><col style=\"width:80pt\"><col style=\"width:300pt\">"
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

    [AllocationBudget]
    [Test]
    public void APaintFreeTableRowAllocatesNoCellBoxesNorASecondRowRecord()
    {
        var perRow = LayoutBytesPerRow("<td style=\"padding:3pt\">AB</td>");
        TestContext.Out.WriteLine($"padded table row layout: {perRow:F1} bytes");
        Assert.That(perRow, Is.LessThan(LimitPadded));
    }

    [Test]
    public void PaintedCellsStillPaintTheirBorders()
    {
        var html = new StringBuilder(NoBodyMargin + "<table style=\"width:520pt\"><colgroup><col style=\"width:80pt\"><col style=\"width:300pt\">"
            + "<col style=\"width:140pt\"></colgroup><tbody>");
        for (var i = 0; i < 3; i++)
            html.Append("<tr><td style=\"padding:3pt\">AB</td><td style=\"border:0.5pt solid #789\">AB</td>"
                + "<td style=\"background:#eee\">AB</td></tr>");
        html.Append("</tbody></table>");
        var options = new PdfOptions();
        var pages = TextLayout.Layout(HtmlDocumentReader.ReadTree(html.ToString(), options, default), options, default);
        // Each row paints its bordered and its background cell; the padded cell paints nothing.
        Assert.That(pages.Sum(page => page.PaintBoxes.Count), Is.EqualTo(6));
    }

    // Main before Z5e: 1,826.6 bytes per row; with Z5e 1,434.6.
    private const double LimitPadded = 1_650;
}
