using System.Diagnostics.Tracing;
using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

// Measurement tool for allocation work (roadmap Z items), not a guard: it prints allocated bytes
// per phase (read, layout, serialize) and the types sampled by GCAllocationTick, so each run does
// not rebuild an ad-hoc listener. Run it with
// dotnet test tests/FactsPDF.Tests -c Release --filter "FullyQualifiedName~AllocationProfileTests" --logger "console;verbosity=detailed"
[TestFixture]
[Explicit("Prints an allocation profile; budgets live in AllocationBudgetTests.")]
[NonParallelizable]
public sealed class AllocationProfileTests
{
    private static readonly PdfFont Font = PdfFont.FromBytes(FontFixture.Create(listDigits: true));

    [Test]
    public void ProfileTheStyledTable()
    {
        var options = new PdfOptions { Fonts = [Font], SubsetFonts = true };
        Profile("200-row styled table", Table(200), options);
    }

    [Test]
    public void ProfileQuickStart() =>
        Profile("quick-start", File.ReadAllText(Path.Combine(FindRoot(), "examples", "quick-start.html")), new PdfOptions());

    internal static string Table(int rows)
    {
        var html = new StringBuilder("<table style=\"width:520pt\"><colgroup><col style=\"width:80pt\"><col style=\"width:300pt\">"
            + "<col style=\"width:140pt\"></colgroup><tbody>");
        for (var i = 0; i < rows; i++)
            html.Append("<tr><td style=\"padding:3pt;border:0.5pt solid #789\">AB</td><td style=\"padding:3pt\">Wi AB Wi AB Wi AB Wi</td><td>AB</td></tr>");
        return html.Append("</tbody></table>").ToString();
    }

    private static void Profile(string name, string html, PdfOptions options)
    {
        const int runs = 5;
        for (var i = 0; i < 3; i++) Convert(html, options, null); // warm up JIT tiers and pools
        var phases = new long[3];
        using var listener = new AllocationTickListener();
        for (var i = 0; i < runs; i++) Convert(html, options, phases);
        listener.Dispose();
        var output = TestContext.Out;
        output.WriteLine($"{name}: read {phases[0] / runs / 1024} KiB, layout {phases[1] / runs / 1024} KiB, "
            + $"serialize {phases[2] / runs / 1024} KiB per conversion");
        output.WriteLine("Sampled types (about one sample per 100 KB allocated, all runs):");
        foreach (var (type, bytes) in listener.Top(15)) output.WriteLine($"  {bytes / 1024,8} KiB  {type}");
        Assert.That(phases[1], Is.GreaterThan(0));
    }

    private static void Convert(string html, PdfOptions options, long[]? phases)
    {
        var start = GC.GetAllocatedBytesForCurrentThread();
        var root = HtmlDocumentReader.ReadTree(html, options, default);
        var read = GC.GetAllocatedBytesForCurrentThread();
        var pages = TextLayout.Layout(root, options, default);
        var laidOut = GC.GetAllocatedBytesForCurrentThread();
        using (options.Fonts.Count > 0 ? UnicodePdfSerializer.Build(pages, options, default) : PdfSerializer.Build(pages, options, default)) { }
        var end = GC.GetAllocatedBytesForCurrentThread();
        if (phases is null) return;
        phases[0] += read - start;
        phases[1] += laidOut - read;
        phases[2] += end - laidOut;
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

    private sealed class AllocationTickListener : EventListener
    {
        private readonly Dictionary<string, long> bytesByType = [];
        private readonly object gate = new();

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name == "Microsoft-Windows-DotNETRuntime")
                EnableEvents(eventSource, EventLevel.Verbose, (EventKeywords)0x1); // GC keyword
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            if (eventData.EventName is not "GCAllocationTick_V4" and not "GCAllocationTick_V3" || eventData.Payload is null) return;
            var type = eventData.Payload[eventData.PayloadNames!.IndexOf("TypeName")] as string ?? "?";
            var amount = System.Convert.ToInt64(eventData.Payload[eventData.PayloadNames.IndexOf("AllocationAmount64")], System.Globalization.CultureInfo.InvariantCulture);
            lock (gate) bytesByType[type] = bytesByType.GetValueOrDefault(type) + amount;
        }

        public (string Type, long Bytes)[] Top(int count)
        {
            lock (gate) return bytesByType.OrderByDescending(pair => pair.Value).Take(count).Select(pair => (pair.Key, pair.Value)).ToArray();
        }
    }
}
