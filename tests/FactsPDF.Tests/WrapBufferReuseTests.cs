using NUnit.Framework;

namespace FactsPDF.Tests;

// Wrap reuses its line and word glyph lists across paragraphs on one thread. A Wrap opened while
// another is still running must get its own lists, and a finished or failed Wrap must never leave
// glyphs behind for the next one.
[TestFixture]
[NonParallelizable]
public sealed class WrapBufferReuseTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 500, Margin = 20 };
    private static readonly TextStyle Style = new(12, 1.2, new Rgb(0, 0, 0), TextAlignment.Left);

    private static Paragraph Paragraph(string text)
    {
        var result = new Paragraph(Style);
        result.Runs.Add(new TextRun(text, Style));
        return result;
    }

    private static string Text(TextLines.Line line) =>
        string.Concat(line.Glyphs.Select(g => char.ConvertFromUtf32(g.Scalar)));

    private static List<string> Lines(Paragraph paragraph, Dictionary<(FontRole, int), (PdfFont?, double)> resolved) =>
        [.. TextLines.Wrap(paragraph, 100, Page, resolved, default).Select(Text)];

    [Test]
    public void AWrapOpenedInsideAnotherKeepsBothLinesIntact()
    {
        var resolved = new Dictionary<(FontRole, int), (PdfFont?, double)>();
        using var outer = TextLines.Wrap(Paragraph("AAAA BBBB CCCC"), 100, Page, resolved, default).GetEnumerator();
        Assert.That(outer.MoveNext(), Is.True);
        var first = Text(outer.Current);
        Assert.That(Lines(Paragraph("ZZ YY"), resolved), Is.EqualTo(new[] { "ZZ YY" }));
        Assert.That(Text(outer.Current), Is.EqualTo(first), "the nested Wrap must not touch the open line");
        var rest = new List<string>();
        while (outer.MoveNext()) rest.Add(Text(outer.Current));
        Assert.That(string.Join("|", rest.Prepend(first)), Is.EqualTo("AAAA BBBB|CCCC"));
    }

    [Test]
    public void ConsecutiveWrapsStartFromEmptyLists()
    {
        var resolved = new Dictionary<(FontRole, int), (PdfFont?, double)>();
        Assert.That(Lines(Paragraph("AAAA BBBB CCCC"), resolved), Is.EqualTo(new[] { "AAAA BBBB", "CCCC" }));
        Assert.That(Lines(Paragraph("D"), resolved), Is.EqualTo(new[] { "D" }));
        // An abandoned Wrap returns its lists when disposed; the next one still starts empty.
        using (var abandoned = TextLines.Wrap(Paragraph("EEEE FFFF GGGG"), 100, Page, resolved, default).GetEnumerator())
            Assert.That(abandoned.MoveNext(), Is.True);
        Assert.That(Lines(Paragraph("H"), resolved), Is.EqualTo(new[] { "H" }));
    }

    [Test]
    public void AFailedWrapDoesNotLeakGlyphsIntoTheNextOne()
    {
        var resolved = new Dictionary<(FontRole, int), (PdfFont?, double)>();
        var error = Assert.Throws<FactsPdfException>(() => Lines(Paragraph("AAAA " + new string('X', 40)), resolved));
        Assert.That(error!.Code, Is.EqualTo("FPDF1302"));
        Assert.That(Lines(Paragraph("I J"), resolved), Is.EqualTo(new[] { "I J" }));
    }

    [Test]
    public void ARepeatedConversionWithNestedListMarkersIsByteIdentical()
    {
        const string Html = "<ol><li>First item text that wraps across several lines in a narrow page</li>"
            + "<li>Second<ol><li>Nested item</li></ol></li></ol><p>After the list</p>";
        var writer = Pdf.Write().WithPage(p => p.Size(200, 300).Margin(20));
        var first = writer.ToBytes(Html);
        Assert.That(writer.ToBytes(Html), Is.EqualTo(first));
    }
}
