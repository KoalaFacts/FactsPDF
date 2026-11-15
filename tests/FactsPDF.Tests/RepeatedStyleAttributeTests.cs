using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

// A style text compiled earlier in the same conversion is replayed with the new attribute's
// offsets instead of being tokenized, parsed and compiled again. The replay must charge the same
// declaration budget, report the same offsets and render the same bytes as compiling every time.
[TestFixture]
[NonParallelizable]
public sealed class RepeatedStyleAttributeTests
{
    private static readonly PdfOptions Options = new() { MaxElements = 100_000 };

    private static byte[] Render(string html, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, options ?? new PdfOptions());
        return output.ToArray();
    }

    private static string Paragraphs(int count, Func<int, string> style)
    {
        var html = new StringBuilder();
        for (var i = 0; i < count; i++) html.Append("<p style=\"").Append(style(i)).Append("\">AB</p>");
        return html.ToString();
    }

    private static double ReadBytesPerParagraph(Func<int, string> style)
    {
        long Read(string source)
        {
            for (var i = 0; i < 10; i++) HtmlDocumentReader.ReadTree(source, Options, default);
            var least = long.MaxValue;
            for (var i = 0; i < 3; i++)
            {
                var before = GC.GetAllocatedBytesForCurrentThread();
                HtmlDocumentReader.ReadTree(source, Options, default);
                least = Math.Min(least, GC.GetAllocatedBytesForCurrentThread() - before);
            }
            return least;
        }
        var small = Read(Paragraphs(1_000, style));
        var large = Read(Paragraphs(2_000, style));
        return (large - small) / 1_000.0;
    }

    [Test]
    public void ARepeatedStyleTextIsNotParsedAgain()
    {
        // Every paragraph repeats one style text; the 200-row table's cells do the same.
        var repeated = ReadBytesPerParagraph(_ => "padding:3pt;border:0.5pt solid #789");
        TestContext.Out.WriteLine($"repeated style: {repeated:F1} bytes per paragraph");
        Assert.That(repeated, Is.LessThan(1_600));
    }

    [Test]
    public void AReplayedDeclarationCopiesOnlyItsOrderAndOffset()
    {
        // padding and border expand into 16 declarations; a replay copies each one's order and
        // offset and shares what it says (property, name, value, importance) with the first compile.
        var repeated = ReadBytesPerParagraph(_ => "padding:3pt;border:0.5pt solid #789");
        TestContext.Out.WriteLine($"replayed declarations: {repeated:F1} bytes per paragraph");
        Assert.That(repeated, Is.LessThan(1_600));
    }

    [Test]
    public void RepeatedAndDistinctStyleTextsRenderTheSameBytes()
    {
        // Distinct texts (a different amount of trailing whitespace each) are always compiled.
        var repeated = Paragraphs(40, _ => "color:#123;margin:2pt 4pt;border:1pt solid red");
        var distinct = Paragraphs(40, i => "color:#123;margin:2pt 4pt;border:1pt solid red" + new string(' ', i));
        Assert.That(Render(repeated), Is.EqualTo(Render(distinct)));
    }

    [Test]
    public void TheDeclarationBudgetFailsAtTheSameReplayedDeclaration()
    {
        // Two declarations per paragraph: the sixth is the third paragraph's font-size, reported at its value.
        const string style = "color:red; font-size:10pt";
        var html = Paragraphs(4, _ => style);
        var error = Assert.Throws<FactsPdfException>(() => Render(html, Options with { MaxCssDeclarations = 5 }))!;
        var third = html.IndexOf(style, html.IndexOf(style, html.IndexOf(style, StringComparison.Ordinal) + 1, StringComparison.Ordinal) + 1, StringComparison.Ordinal);
        Assert.That((error.Code, error.SourceOffset), Is.EqualTo(("FPDF1205", third + style.IndexOf("10pt", StringComparison.Ordinal))));
    }

    [Test]
    public void ACascadeErrorPointsAtTheReplayedElementsDeclaration()
    {
        // The first paragraph accepts the text; the replayed copy on a list item fails where the
        // list item's own declaration starts.
        const string style = "  text-indent:4pt";
        var html = $"<p style=\"{style}\">A</p><ul><li style=\"{style}\">B</li></ul>";
        var expected = Assert.Throws<FactsPdfException>(() => Render($"<ul><li style=\"{style}\">B</li></ul>"))!;
        var error = Assert.Throws<FactsPdfException>(() => Render(html))!;
        var li = html.IndexOf("<li", StringComparison.Ordinal);
        Assert.That((error.Code, error.Message), Is.EqualTo((expected.Code, expected.Message)));
        Assert.That(error.SourceOffset, Is.EqualTo(expected.SourceOffset + li - "<ul>".Length));
    }

    [Test]
    public void AStyleWithCharacterReferencesKeepsItsOwnOffsets()
    {
        // Same decoded text as the first paragraph, but spelled with a reference: compiled with
        // its origin map, so the budget failure points into its own source.
        var html = "<p style=\"color:red;font-size:9pt\">A</p><p style=\"color:red;font-size:9p&#116;\">B</p>";
        var error = Assert.Throws<FactsPdfException>(() => Render(html, Options with { MaxCssDeclarations = 3 }))!;
        Assert.That(error.SourceOffset, Is.EqualTo(html.IndexOf("9p&#116;", StringComparison.Ordinal)));
    }
}
