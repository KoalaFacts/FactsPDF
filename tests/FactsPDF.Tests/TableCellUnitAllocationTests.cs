using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace FactsPDF.Tests;

// Table measurement records one unit per cell line or image. Units are values in one
// exact-size array per cell, built through a scratch list shared by the whole table, so a
// one-line cell costs no unit object and no growable list of its own.
[TestFixture]
[NonParallelizable]
public sealed class TableCellUnitAllocationTests
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

    private static long LayoutAllocated(string html, PdfOptions options)
    {
        var root = HtmlDocumentReader.ReadTree(html, options, default);
        for (var i = 0; i < 3; i++) TextLayout.Layout(root, options, default);
        var before = GC.GetAllocatedBytesForCurrentThread();
        TextLayout.Layout(root, options, default);
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    // Measured on main 53e7385: 4,873 bytes of layout per three-cell row with a unit object and a
    // list per cell; 4,633 with units as values in exact-size arrays; 3,961 on eb531d2 and 3,817
    // once the line iterator holds no List enumerator.
    [Test]
    public void OneLineCellsAllocateNoUnitObjectOrListOfTheirOwn()
    {
        var options = new PdfOptions { Fonts = [Font], SubsetFonts = true };
        var small = LayoutAllocated(Table(200), options);
        var large = LayoutAllocated(Table(400), options);
        var perRow = (large - small) / 200.0;
        TestContext.Out.WriteLine($"layout per row: {perRow:F1} bytes");
        Assert.That(perRow, Is.LessThan(PerRowLimit));
    }

    private const double PerRowLimit = 3_900;

    [Test]
    public void ARowTallerThanAPageStillSplitsAcrossPages()
    {
        var text = new StringBuilder();
        for (var i = 0; i < 1_500; i++) text.Append("AB Wi ");
        var html = "<table style=\"width:300pt\"><colgroup><col style=\"width:200pt\"><col style=\"width:100pt\"></colgroup>"
            + "<tbody><tr><td>" + text + "</td><td>AB</td></tr></tbody></table>";
        var pdf = Pdf.Write().ToBytes(html);
        var content = Encoding.Latin1.GetString(pdf);
        Assert.That(content, Does.StartWith("%PDF-"));
        Assert.That(Regex.Count(content, "/Type /Page\\b"), Is.GreaterThan(1));
    }
}
