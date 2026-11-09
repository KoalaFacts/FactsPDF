using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

// Only a start tag with a style attribute stores the style attribute's source offsets; every other
// token is a plain HtmlToken, 8 bytes smaller.
[TestFixture]
[NonParallelizable]
public sealed class HtmlTokenSizeTests
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

    // Measured on top of Z6g2b1 (552 bytes per paragraph, three 56-byte tokens each): 528 with
    // 48-byte tokens.
    [Test]
    public void ReadingAParagraphAllocatesSmallerTokens()
    {
        // 1,000 and 2,000 paragraphs stay under the reader's 8,192-token pooled list.
        var options = new PdfOptions { MaxElements = 100_000 };
        var small = ReadAllocated(Paragraphs(1_000), options);
        var large = ReadAllocated(Paragraphs(2_000), options);
        var perParagraph = (large - small) / 1_000.0;
        TestContext.Out.WriteLine($"read per paragraph: {perParagraph:F1} bytes");
        Assert.That(perParagraph, Is.LessThan(545));
    }

    [Test]
    public void OnlyAStyledStartTagCarriesStyleOffsets()
    {
        var tokens = HtmlTokens.Read("<p id=\"a\">x</p><div style=\"color:red\"></div><style>p{}</style>", default).ToList();
        Assert.That(tokens, Has.Count.EqualTo(8));
        for (var i = 0; i < tokens.Count; i++)
        {
            var styled = i == 3;
            Assert.That(tokens[i] is StyledHtmlToken, Is.EqualTo(styled), $"token {i}");
            Assert.That(tokens[i].StyleValueOffset >= 0, Is.EqualTo(styled), $"token {i}");
        }
        Assert.That(tokens[3].StyleValueOffset, Is.EqualTo(27));
        Assert.That(tokens[3].StyleOriginOffsets, Is.Null);
    }

    [Test]
    public void AStyledTokenWithACharacterReferenceKeepsItsOriginMap()
    {
        var token = HtmlTokens.Read("<p style=\"color:r&#101;d\">x</p>", default).First();
        Assert.That(token, Is.TypeOf<StyledHtmlToken>());
        Assert.That(token.StyleOriginOffsets, Has.Length.EqualTo("color:red".Length + 1));
    }

    // A styled token's offsets reach the cascade: a value with a character reference still
    // applies, and an invalid one reports its exact page location through the origin map.
    [Test]
    public void StyledTokenOffsetsReachTheCascade()
    {
        var bytes = Pdf.Write().ToBytes("<p>y</p><p style=\"color:#ff&#48;000\">x</p>");
        Assert.That(Encoding.Latin1.GetString(bytes), Does.Contain("1 0 0 rg"));
        const string html = "<p>y</p><p style=\"color:r&#101;d; width:-1pt\">x</p>";
        var error = Assert.Throws<FactsPdfException>(() => Pdf.Write().ToBytes(html))!;
        // The origin map maps the decoded value back past the five-character reference.
        Assert.That(error.SourceOffset, Is.EqualTo(html.IndexOf("-1pt", StringComparison.Ordinal)));
    }
}
