using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

// Guards managed allocation per conversion against regressions. Budgets sit about 15% above
// the measured values, so they catch real growth without being sensitive to runtime noise.
// Lower a budget whenever an optimization lands.
[TestFixture]
[NonParallelizable]
public sealed class AllocationBudgetTests
{
    private static readonly PdfFont Font = PdfFont.FromBytes(FontFixture.Create(listDigits: true));

    private static long AllocatedPerConversion(string html, PdfWriter writer)
    {
        for (var i = 0; i < 3; i++) writer.ToBytes(html); // warm up JIT tiers and caches
        var before = GC.GetAllocatedBytesForCurrentThread();
        const int runs = 5;
        for (var i = 0; i < runs; i++) writer.ToBytes(html);
        return (GC.GetAllocatedBytesForCurrentThread() - before) / runs;
    }

    private static string Table(int rows)
    {
        var html = new StringBuilder("<table style=\"width:520pt\"><colgroup><col style=\"width:80pt\"><col style=\"width:300pt\">"
            + "<col style=\"width:140pt\"></colgroup><tbody>");
        for (var i = 0; i < rows; i++)
            html.Append("<tr><td style=\"padding:3pt;border:0.5pt solid #789\">AB</td><td style=\"padding:3pt\">Wi AB Wi AB Wi AB Wi</td><td>AB</td></tr>");
        return html.Append("</tbody></table>").ToString();
    }

    [Test]
    public void QuickStartStaysWithinItsAllocationBudget()
    {
        var html = File.ReadAllText(Path.Combine(FindRoot(), "examples", "quick-start.html"));
        var allocated = AllocatedPerConversion(html, Pdf.Write());
        TestContext.Out.WriteLine($"quick-start: {allocated / 1024} KiB per conversion");
        Assert.That(allocated, Is.LessThan(350 * 1024));
    }

    [Test]
    public void StyledTableStaysWithinItsAllocationBudget()
    {
        var allocated = AllocatedPerConversion(Table(200), Pdf.Write().WithFonts(f => f.Regular(Font).Subset()));
        TestContext.Out.WriteLine($"200-row styled table: {allocated / 1024} KiB per conversion");
        Assert.That(allocated, Is.LessThan(6_920 * 1024));
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "FactsPDF.slnx"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Repository root not found.");
    }
}
