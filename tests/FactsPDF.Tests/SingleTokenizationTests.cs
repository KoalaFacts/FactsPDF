using NUnit.Framework;

namespace FactsPDF.Tests;

// The page is tokenized once: stylesheet collection records the tokens and the document reader
// walks the same list. Error precedence and the cascade must stay exactly as they were when
// each pass tokenized the page on its own.
[TestFixture]
public sealed class SingleTokenizationTests
{
    private static FactsPdfException Fail(string html) =>
        Assert.Throws<FactsPdfException>(() => Pdf.Write().ToBytes(html))!;

    private static List<Paragraph> Paragraphs(string html)
    {
        var found = new List<Paragraph>();
        void Walk(IEnumerable<BlockChild> children)
        {
            foreach (var child in children)
                if (child is Paragraph node) found.Add(node);
                else if (child is BlockNode block) Walk(block.Children);
        }
        Walk(HtmlDocumentReader.ReadTree(html, new(), default).Children);
        return found;
    }

    [Test]
    public void StyleElementAfterContentStillStylesEarlierContent()
    {
        var paragraph = Paragraphs("<p class=\"a\">x</p><style>.a{font-size:20pt}</style>").Single();
        Assert.That(paragraph.Runs.Single().Style.FontSize, Is.EqualTo(20));
    }

    [Test]
    public void StylesheetErrorBeforeALaterMalformedTagIsReportedFirst()
    {
        var error = Fail("<style>@import url(x.css);</style><p>x</p><1>");
        Assert.That((error.Code, error.SourceOffset), Is.EqualTo(("FPDF1204", 7)));
    }

    [Test]
    public void MalformedTagAfterAStructuralErrorIsReportedFirst()
    {
        // The reader's own structural error comes earlier in the page, but every token is read
        // (and so every tokenizer error found) before the reader walks the tree.
        var html = "<ul>text</ul><!-- unterminated";
        var error = Fail(html);
        Assert.That((error.Code, error.Message, error.SourceOffset),
            Is.EqualTo(("FPDF1101", "FPDF1101: Unterminated comment.", html.IndexOf("<!--", StringComparison.Ordinal))));
    }

    [Test]
    public void EveryTokenReachesTheReaderInOrder()
    {
        var text = Paragraphs("<p>a</p><style>p{color:red}</style><p>b</p><p>c</p>").Select(p => p.Runs.Single().Text);
        Assert.That(text, Is.EqualTo(new[] { "a", "b", "c" }));
    }
}
