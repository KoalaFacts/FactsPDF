using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

// A style attribute's compiled declarations live on its token, so reading a styled element adds
// no entry (and no growth of buckets and entries) to a per-conversion map keyed by tag offset.
[TestFixture]
[NonParallelizable]
public sealed class InlineDeclarationLookupTests
{
    private static string Styled(int count)
    {
        var html = new StringBuilder();
        for (var i = 0; i < count; i++) html.Append("<p style=\"color:#123\">x</p>");
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

    // Main 44356fd: 824.5 bytes per styled paragraph; with declarations on the token: 768.0.
    [Test]
    public void ReadingAStyledParagraphAddsNoOffsetMapEntry()
    {
        var perElement = (ReadAllocated(Styled(800)) - ReadAllocated(Styled(400))) / 400.0;
        TestContext.Out.WriteLine($"reading a styled paragraph: {perElement:F1} bytes");
        Assert.That(perElement, Is.LessThan(800));
    }

    [Test]
    public void InlineDeclarationsStillReachTheirOwnElements()
    {
        var text = Encoding.Latin1.GetString(Pdf.Write().ToBytes(
            "<p>a</p><p style=\"color:#ff0000\">b</p><p>c</p><p style=\"color:#0000ff\">d</p>"));
        Assert.That(text, Does.Contain("1 0 0 rg"));
        Assert.That(text, Does.Contain("0 0 1 rg"));
        var error = Assert.Throws<FactsPdfException>(() => Pdf.Write().ToBytes("<p style=\"color:red\">a</p><p style=\"width:-1pt\">b</p>"))!;
        Assert.That(error.SourceOffset, Is.EqualTo("<p style=\"color:red\">a</p><p style=\"width:".Length));
    }
}
