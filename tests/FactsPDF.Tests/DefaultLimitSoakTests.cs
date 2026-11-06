using NUnit.Framework;

namespace FactsPDF.Tests;

// R1b: inputs sized at or just past the default resource limits either render or fail with the
// expected FactsPdfException, and the managed allocation of that one conversion stays within a
// budget. Budgets sit about 15% above the measured values and only go down.
[TestFixture]
[NonParallelizable]
public sealed class DefaultLimitSoakTests
{
    public sealed record SoakCase(string Name, Func<string> Html, string? Code, int BudgetMiB)
    {
        public override string ToString() => Name;
    }

    private static string Repeat(string text, int count) => string.Concat(Enumerable.Repeat(text, count));

    // Measured on .NET 10 (Linux x64) at introduction: 40, 3, 332, 120, 14 and 32 MiB.
    public static readonly SoakCase[] Cases =
    [
        // About one million input characters of wrapping text in a single paragraph.
        new("one paragraph near MaxInputCharacters", () => "<p>" + Repeat("word ", 199_990) + "</p>", null, 47),
        // One unbreakable million-character word cannot fit the page width.
        new("unbreakable word near MaxInputCharacters", () => "<p>" + new string('x', 999_990) + "</p>", "FPDF1302", 5),
        // 49,000 list items: under MaxElements and MaxPages, close to both.
        new("49,000 list items", () => "<ul>" + Repeat("<li>a</li>", 49_000) + "</ul>", null, 383),
        // 49,000 paragraphs: more pages than MaxPages.
        new("49,000 paragraphs past MaxPages", () => Repeat("<p>a</p>", 49_000), "FPDF1303", 139),
        // 16,000 table rows: more elements than MaxElements.
        new("16,000 table rows past MaxElements", () => "<table>" + Repeat("<tr><td>a</td><td>b</td></tr>", 16_000) + "</table>", "FPDF1101", 17),
        // 4,000 descendant selectors over 2,000 nested matches: past MaxCssMatchOperations.
        new("selector matching past MaxCssMatchOperations", () => "<style>"
            + string.Concat(Enumerable.Range(0, 4_000).Select(i => $"div p span.c{i} b{{color:red}}"))
            + "</style>" + Repeat("<div><p><span class=c1><b>a</b></span></p></div>", 2_000), "FPDF1205", 38),
    ];

    [TestCaseSource(nameof(Cases))]
    public void DefaultLimitInputStaysWithinItsAllocationBudget(SoakCase soak)
    {
        var html = soak.Html();
        var writer = Pdf.Write();
        Convert(writer, html, soak.Code); // warm up JIT tiers
        var before = GC.GetAllocatedBytesForCurrentThread();
        Convert(writer, html, soak.Code);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        TestContext.Out.WriteLine($"{soak.Name}: {allocated / 1_048_576} MiB");
        Assert.That(allocated, Is.LessThan((long)soak.BudgetMiB * 1_048_576));
    }

    private static void Convert(PdfWriter writer, string html, string? code)
    {
        if (code is null)
        {
            Assert.That(writer.ToBytes(html), Is.Not.Empty);
            return;
        }
        var error = Assert.Throws<FactsPdfException>(() => writer.ToBytes(html));
        Assert.That(error!.Code, Is.EqualTo(code));
    }
}
