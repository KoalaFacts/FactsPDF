using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

// The reader keeps each open element's selector view (name, id, classes, attributes) in its
// ancestor path. As a value in that list it costs no object per element.
[TestFixture]
[NonParallelizable]
public sealed class CssElementValueTests
{
    private static string Paragraphs(int count)
    {
        var html = new StringBuilder();
        for (var i = 0; i < count; i++) html.Append("<p>Paragraph ").Append(i).Append("</p>");
        return html.ToString();
    }

    private static long ReadAllocated(string html, PdfOptions options)
    {
        for (var i = 0; i < 3; i++) HtmlDocumentReader.ReadTree(html, options, default);
        var before = GC.GetAllocatedBytesForCurrentThread();
        HtmlDocumentReader.ReadTree(html, options, default);
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    // Measured on main 0098a5f: 656 bytes read per paragraph with one 56-byte element object
    // each; 600 as a value.
    [Test]
    public void ReadingAParagraphAllocatesNoElementObject()
    {
        // 1,000 and 2,000 paragraphs stay under the reader's 8,192-token pooled list.
        var options = new PdfOptions { MaxElements = 100_000 };
        var small = ReadAllocated(Paragraphs(1_000), options);
        var large = ReadAllocated(Paragraphs(2_000), options);
        var perParagraph = (large - small) / 1_000.0;
        TestContext.Out.WriteLine($"read per paragraph: {perParagraph:F1} bytes");
        Assert.That(perParagraph, Is.LessThan(PerParagraphLimit));
    }

    [Test]
    public void FromKeepsNameIdClassesAndAttributes()
    {
        var token = HtmlTokens.Read("<p id=\"lead\" class=\"a b a\" data-x=\"1\">", default).First();
        var element = CssElement.From(token);
        Assert.That(element.Name, Is.EqualTo("p"));
        Assert.That(element.Id, Is.EqualTo("lead"));
        Assert.That(element.Classes.Names.ToArray(), Is.EqualTo(new[] { "a", "b" }));
        Assert.That(element.Attributes["data-x"], Is.EqualTo("1"));
    }

    // Ancestor matching walks the path by index; each case renders red text only if the selector
    // matched through the value path.
    [TestCase("section p { color: #ff0000 }", "<section><div><p>x</p></div></section>")]
    [TestCase("div > p { color: #ff0000 }", "<section><div><p>x</p></div></section>")]
    [TestCase("section > div > p.lead { color: #ff0000 }", "<section><div><p class=\"lead\">x</p></div></section>")]
    [TestCase("#outer p[data-k=\"v\"] { color: #ff0000 }", "<div id=\"outer\"><div><p data-k=\"v\">x</p></div></div>")]
    [TestCase(".a .b .c { color: #ff0000 }", "<div class=\"a\"><div class=\"b\"><p class=\"c\">x</p></div></div>")]
    public void AncestorSelectorsMatchThroughThePath(string css, string body)
    {
        var matched = Pdf.Write().ToBytes($"<style>{css}</style>{body}");
        var plain = Pdf.Write().ToBytes(body);
        Assert.That(Encoding.Latin1.GetString(matched), Does.Contain("1 0 0 rg"));
        Assert.That(Encoding.Latin1.GetString(plain), Does.Not.Contain("1 0 0 rg"));
    }

    [TestCase("section > p { color: #ff0000 }", "<section><div><p>x</p></div></section>")]
    [TestCase("article p { color: #ff0000 }", "<section><div><p>x</p></div></section>")]
    [TestCase(".a .c { color: #ff0000 }", "<div class=\"b\"><p class=\"c\">x</p></div>")]
    public void AncestorSelectorsThatDoNotMatchLeaveTheTextAlone(string css, string body)
    {
        var bytes = Pdf.Write().ToBytes($"<style>{css}</style>{body}");
        Assert.That(Encoding.Latin1.GetString(bytes), Does.Not.Contain("1 0 0 rg"));
    }

    private const double PerParagraphLimit = 620;
}
