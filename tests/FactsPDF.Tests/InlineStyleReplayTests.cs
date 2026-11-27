using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

// A style attribute text seen before in the conversion is not compiled again, and its element no
// longer gets a new declaration object (and array) per declaration: the cascade views the shared
// compiled declarations at the element's own order and offset through reused slots.
[TestFixture]
[NonParallelizable]
public sealed class InlineStyleReplayTests
{
    private const string Cell = "padding:3pt;border:0.5pt solid #789"; // 16 longhand declarations

    private static string Styled(int count)
    {
        var html = new StringBuilder();
        for (var i = 0; i < count; i++) html.Append("<div style=\"").Append(Cell).Append("\">x</div>");
        return html.ToString();
    }

    private static long ReadAllocated(string html)
    {
        var options = new PdfOptions();
        for (var i = 0; i < 3; i++) HtmlDocumentReader.ReadTree(html, options, default);
        long best = long.MaxValue;
        for (var run = 0; run < 3; run++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            HtmlDocumentReader.ReadTree(html, options, default);
            best = Math.Min(best, GC.GetAllocatedBytesForCurrentThread() - before);
        }
        return best;
    }

    // Main 38289de: 1,136 bytes per replayed 16-declaration block (16 replay objects of 32 bytes
    // plus their 152-byte array); with shared declarations and slots: 480.
    [AllocationBudget]
    [Test]
    public void ReplayingAStyleAttributeAllocatesNoDeclarationPerElement()
    {
        var perElement = (ReadAllocated(Styled(800)) - ReadAllocated(Styled(400))) / 400.0;
        TestContext.Out.WriteLine($"reading a replayed styled block: {perElement:F1} bytes");
        Assert.That(perElement, Is.LessThan(600));
    }

    [Test]
    public void AReplayedDeclarationFailsAtItsOwnElementsOffset()
    {
        const string html = "<p style=\"margin-top:1pt\">a<span style=\"margin-top:1pt\">b</span></p>";
        var error = Assert.Throws<FactsPdfException>(() => Pdf.Write().ToBytes(html))!;
        Assert.That(error.Code, Is.EqualTo("FPDF1201"));
        // The value of the span's declaration, not the paragraph's (the first compile's) offset.
        Assert.That(error.SourceOffset, Is.EqualTo(html.IndexOf("1pt", html.IndexOf("<span", StringComparison.Ordinal), StringComparison.Ordinal)));
    }

    [Test]
    public void TheLastOfARepeatedPropertyStillWinsWhenReplayed()
    {
        const string style = "color:#ff0000;color:#0000ff";
        var text = Encoding.Latin1.GetString(Pdf.Write().ToBytes(
            $"<p style=\"{style}\">a</p><p style=\"{style}\">b</p><p style=\"{style}\">c</p>"));
        Assert.That(text, Does.Not.Contain("1 0 0 rg"));
        Assert.That(text, Does.Contain("0 0 1 rg"));
    }

    [Test]
    public void ImportantStylesheetRulesStillBeatAReplayedAttribute()
    {
        var text = Encoding.Latin1.GetString(Pdf.Write().ToBytes(
            "<style>p{color:#00ff00 !important}</style><p style=\"color:#ff0000\">a</p><p style=\"color:#ff0000\">b</p>"));
        Assert.That(text, Does.Not.Contain("1 0 0 rg"));
        Assert.That(text, Does.Contain("0 1 0 rg"));
    }

    // Distinct texts are compiled afresh; the same text is replayed. Both must paint the same page.
    [Test]
    public void ReplayedAndFreshlyCompiledStylesRenderTheSameBytes()
    {
        var replayed = new StringBuilder(); var fresh = new StringBuilder();
        for (var i = 0; i < 6; i++)
        {
            replayed.Append("<div style=\"").Append(Cell).Append("\">r</div><p style=\"color:#123;margin-top:2pt\">t</p>");
            fresh.Append("<div style=\"").Append(Cell).Append(';', i + 1).Append("\">r</div><p style=\"color:#123;margin-top:2pt")
                .Append(' ', i).Append("\">t</p>");
        }
        Assert.That(Pdf.Write().ToBytes(replayed.ToString()), Is.EqualTo(Pdf.Write().ToBytes(fresh.ToString())));
    }
}
