using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

// Z2c: a letter-spacing change between runs writes its `Tc` operator straight into the page
// content stream, and a patterned border edge checks its rounded endpoints without formatting
// them to strings. Both serializers are measured on serialization alone.
[TestFixture]
[NonParallelizable]
public sealed class ContentStreamTextStateAllocationTests
{
    private static readonly PdfFont Font = PdfFont.FromBytes(FontFixture.Create(listDigits: true));

    private static long SerializeBytesPer(string html, PdfOptions options, int units)
    {
        var pages = TextLayout.Layout(HtmlDocumentReader.ReadTree(html, options, default), options, default);
        var embed = options.Fonts.Count > 0;
        long Serialize()
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            using (embed ? UnicodePdfSerializer.Build(pages, options, default) : PdfSerializer.Build(pages, options, default)) { }
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }
        for (var i = 0; i < 10; i++) Serialize(); // warm up JIT tiers and the output buffer pool
        long allocated = 0;
        for (var i = 0; i < 5; i++) allocated += Serialize();
        return allocated / 5 / units;
    }

    private static string Tracked(int paragraphs)
    {
        var html = new StringBuilder();
        for (var i = 0; i < paragraphs; i++)
            html.Append(i % 2 == 0 ? "<p style=\"letter-spacing:1.25pt\">AB</p>" : "<p>AB</p>");
        return html.ToString();
    }

    private static string Dashed(int boxes)
    {
        var html = new StringBuilder();
        for (var i = 0; i < boxes; i++)
            html.Append("<div style=\"border:0.75pt dashed #345;padding:2pt\">AB</div>");
        return html.ToString();
    }

    // Every run below changes the letter spacing. On main each change formatted its number and
    // the `Tc` text into two strings: 108 (Courier) and 144 (subset font) serialize bytes per run,
    // 43 and 80 after Z2c (the rest is per-page objects). The limits fail on main.
    [TestCase(false, 55)]
    [TestCase(true, 95)]
    public void LetterSpacingChangesAllocateNoStringPerRun(bool embedFont, int limit)
    {
        var options = embedFont ? new PdfOptions { Fonts = [Font], SubsetFonts = true } : new PdfOptions();
        var html = Tracked(2_000);
        var perRun = SerializeBytesPer(html, options, 2_000);
        TestContext.Out.WriteLine($"serialize bytes per tracked/untracked run ({(embedFont ? "subset font" : "Courier")}): {perRun}");
        var pdf = Encoding.Latin1.GetString(embedFont ? Pdf.Write().WithFonts(f => f.Regular(Font).Subset()).ToBytes(html) : Pdf.Write().ToBytes(html));
        Assert.Multiple(() =>
        {
            Assert.That(pdf, Does.Contain(" Tf 1.25 Tc "));
            Assert.That(pdf, Does.Contain(" Tf 0 Tc "));
            Assert.That(perRun, Is.LessThan(limit));
        });
    }

    // Each patterned edge compared four formatted endpoint strings on main: 711 serialize bytes
    // per dashed box (four edges), 247 after Z2c. The limit fails on main.
    [Test]
    public void PatternedBorderEdgesAllocateNoEndpointStrings()
    {
        var perBox = SerializeBytesPer(Dashed(500), new PdfOptions(), 500);
        TestContext.Out.WriteLine($"serialize bytes per dashed box: {perBox}");
        Assert.That(perBox, Is.LessThan(300));
    }
}
