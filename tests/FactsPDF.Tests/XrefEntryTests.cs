using System.Globalization;
using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

// Z2d: cross-reference entries are written straight into the output instead of two strings each.
[TestFixture]
[NonParallelizable]
public sealed class XrefEntryTests
{
    private static readonly PdfFont Font = PdfFont.FromBytes(FontFixture.Create(listDigits: true));

    [Test]
    public void EntriesMatchTheFormattedStrings()
    {
        List<long> offsets = [0, 9, 15, 1_234_567, 9_999_999_999, 10_000_000_000, long.MaxValue];
        using var output = new MemoryStream();
        PdfAscii.WriteXrefEntries(output, offsets);
        var expected = new StringBuilder();
        foreach (var offset in offsets.Skip(1)) expected.Append(offset.ToString("0000000000", CultureInfo.InvariantCulture)).Append(" 00000 n \n");
        Assert.That(Encoding.ASCII.GetString(output.ToArray()), Is.EqualTo(expected.ToString()));
    }

    [Test]
    public void OnlyTheFreeEntryWritesNothing()
    {
        using var output = new MemoryStream();
        PdfAscii.WriteXrefEntries(output, [0]);
        Assert.That(output.Length, Is.Zero);
    }

    // 600 forced pages, two objects each. On main every entry allocated its offset string and the
    // joined line: 1,235 (Courier) and 1,249 (subset font) serialize bytes per page, 1,010 and
    // 1,024 after Z2d. The limit fails on main.
    [TestCase(false)]
    [TestCase(true)]
    public void SerializingAllocatesNoStringPerXrefEntry(bool embedFont)
    {
        var options = embedFont ? new PdfOptions { Fonts = [Font], SubsetFonts = true } : new PdfOptions();
        var html = new StringBuilder();
        for (var i = 0; i < 600; i++) html.Append("<p style=\"break-after:page\">AB</p>");
        var pages = TextLayout.Layout(HtmlDocumentReader.ReadTree(html.ToString(), options, default), options, default);
        long Serialize()
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            using (embedFont ? UnicodePdfSerializer.Build(pages, options, default) : PdfSerializer.Build(pages, options, default)) { }
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }
        for (var i = 0; i < 10; i++) Serialize(); // warm up JIT tiers and the output buffer pool
        long allocated = 0;
        for (var i = 0; i < 5; i++) allocated += Serialize();
        var perPage = allocated / 5 / pages.Count;
        TestContext.Out.WriteLine($"serialize bytes per page ({(embedFont ? "subset font" : "Courier")}), {pages.Count} pages: {perPage}");
        Assert.That(pages, Has.Count.EqualTo(600));
        Assert.That(perPage, Is.LessThan(1_100));
    }
}
