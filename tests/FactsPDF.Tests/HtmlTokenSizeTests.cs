using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

// Only a start tag with attributes stores an attribute map (an AttributedHtmlToken), and only one
// with a style attribute also stores that attribute's source offsets (a StyledHtmlToken); text, end
// and attribute-less start tags are plain 32-byte HtmlTokens.
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

    // Measured on top of Z6g2b3 (496 bytes per paragraph, three 48-byte tokens each): 472 with
    // 40-byte tokens that store no attribute map.
    [Test]
    public void ReadingAParagraphAllocatesTokensWithoutAnAttributeField()
    {
        var options = new PdfOptions { MaxElements = 100_000 };
        var small = ReadAllocated(Paragraphs(1_000), options);
        var large = ReadAllocated(Paragraphs(2_000), options);
        var perParagraph = (large - small) / 1_000.0;
        TestContext.Out.WriteLine($"read per paragraph: {perParagraph:F1} bytes");
        Assert.That(perParagraph, Is.LessThan(485));
    }

    // Measured on top of Z6g2b4 (472 bytes per paragraph, three 40-byte tokens each): 448 with a
    // one-byte token kind, which packs beside SelfClosing so a plain token is 32 bytes.
    [Test]
    public void ReadingAParagraphAllocatesTokensWithAOneByteKind()
    {
        var options = new PdfOptions { MaxElements = 100_000 };
        var small = ReadAllocated(Paragraphs(1_000), options);
        var large = ReadAllocated(Paragraphs(2_000), options);
        var perParagraph = (large - small) / 1_000.0;
        TestContext.Out.WriteLine($"read per paragraph: {perParagraph:F1} bytes");
        Assert.That(perParagraph, Is.LessThan(460));
    }

    // Only a start tag with attributes stores an attribute map; a start tag without attributes
    // reads the shared empty map, and text and end tags have none.
    [Test]
    public void OnlyAStartTagWithAttributesCarriesAnAttributeMap()
    {
        var tokens = HtmlTokens.Read("<p>x</p><p id=\"a\" class=\"b\">y</p><br/>", default).ToList();
        Assert.That(tokens, Has.Count.EqualTo(7));
        Assert.That(tokens.Select(t => t.GetType() == typeof(AttributedHtmlToken)),
            Is.EqualTo(new[] { false, false, false, true, false, false, false }));
        Assert.That(tokens[0].Attributes, Is.Empty);
        Assert.That(tokens[0].Attributes, Is.SameAs(tokens[6].Attributes));
        Assert.That(tokens[6].SelfClosing, Is.True);
        Assert.That(tokens[1].Attributes, Is.Null);
        Assert.That(tokens[2].Attributes, Is.Null);
        Assert.That(tokens[3].Attributes, Is.EqualTo(new Dictionary<string, string> { ["id"] = "a", ["class"] = "b" }));
        Assert.That(tokens[3].StyleValueOffset, Is.EqualTo(-1));
        var styled = HtmlTokens.Read("<p style=\"color:red\" id=\"z\">x</p>", default).First();
        Assert.That(styled, Is.TypeOf<StyledHtmlToken>());
        Assert.That(styled.Attributes!["id"], Is.EqualTo("z"));
        Assert.That(styled.Attributes["style"], Is.EqualTo("color:red"));
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

    // Measured on top of main c10e703 (440 bytes per paragraph): 416 once a paragraph stores its runs
    // inline (an array and a count) instead of in a separate List object.
    [Test]
    public void ReadingAParagraphKeepsItsRunsWithoutAListObject()
    {
        var options = new PdfOptions { MaxElements = 100_000 };
        var small = ReadAllocated(Paragraphs(1_000), options);
        var large = ReadAllocated(Paragraphs(2_000), options);
        var perParagraph = (large - small) / 1_000.0;
        TestContext.Out.WriteLine($"read per paragraph: {perParagraph:F1} bytes");
        Assert.That(perParagraph, Is.LessThan(430));
    }

    // Measured on top of main 9d40e06 (416 bytes per paragraph): 392 once a block keeps its first
    // child in a slot of its child list instead of a List object plus a one-slot array.
    [Test]
    public void ReadingAParagraphKeepsItsChildWithoutAnArray()
    {
        var options = new PdfOptions { MaxElements = 100_000 };
        var small = ReadAllocated(Paragraphs(1_000), options);
        var large = ReadAllocated(Paragraphs(2_000), options);
        var perParagraph = (large - small) / 1_000.0;
        TestContext.Out.WriteLine($"read per paragraph: {perParagraph:F1} bytes");
        Assert.That(perParagraph, Is.LessThan(405));
    }

    // The whole conversion, not only reading: layout must not give the saving back (LINQ's All over
    // the child list boxed its enumerator once per block). Measured on top of main 9d40e06: 896 bytes
    // per paragraph converted; 872 with the child slot and the index-loop All.
    [Test]
    public void ConvertingAParagraphKeepsTheReadSavingThroughLayout()
    {
        static long Converted(string html)
        {
            for (var i = 0; i < 3; i++) Pdf.Write().ToBytes(html);
            var before = GC.GetAllocatedBytesForCurrentThread();
            Pdf.Write().ToBytes(html);
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }
        var perParagraph = (Converted(Paragraphs(400)) - Converted(Paragraphs(200))) / 200.0;
        TestContext.Out.WriteLine($"converted per paragraph: {perParagraph:F1} bytes");
        Assert.That(perParagraph, Is.LessThan(885));
    }

    [Test]
    public void BlockChildrenGrowEnumerateAndRemoveInOrder()
    {
        var list = new BlockChildList();
        Assert.That(list, Is.Empty);
        Assert.That(list.Capacity, Is.EqualTo(1));
        var nodes = Enumerable.Range(0, 11).Select(i => new BlockNode("div" + i, new TextStyle(12, 1.2, new Rgb(0, 0, 0), TextAlignment.Left), BoxStyle.Initial, i)).ToArray();
        list.Add(nodes[0]);
        Assert.That(list.Capacity, Is.EqualTo(1));
        for (var i = 1; i < nodes.Length; i++) list.Add(nodes[i]);
        Assert.That(list.Capacity, Is.EqualTo(1 + 16));
        Assert.That(list, Is.EqualTo(nodes));
        var seen = new List<BlockChild>();
        foreach (var child in list) seen.Add(child);
        Assert.That(seen, Is.EqualTo(nodes));
        Assert.That(list[^1], Is.SameAs(nodes[^1]));
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = list[11]);
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = list[-1]);
        list.RemoveAt(list.Count - 1);
        list.RemoveAt(0);
        Assert.That(list, Is.EqualTo(nodes[1..10]));
        while (list.Count > 0) list.RemoveAt(list.Count - 1);
        Assert.That(list, Is.Empty);
        list.Add(nodes[5]);
        Assert.That(list, Is.EqualTo(new[] { nodes[5] }));
    }

    [Test]
    public void InlineRunsGrowAndEnumerateInOrder()
    {
        var style = new TextStyle(12, 1.2, new Rgb(0, 0, 0), TextAlignment.Left);
        var paragraph = new Paragraph(style);
        Assert.That(paragraph.Runs.Count, Is.Zero);
        Assert.That(paragraph.Runs.Capacity, Is.Zero);
        Assert.That(paragraph.Runs.HasBreak(), Is.False);
        for (var i = 0; i < 9; i++) paragraph.Runs.Add(new TextRun(i.ToString(System.Globalization.CultureInfo.InvariantCulture), style, IsBreak: i == 7));
        Assert.That(paragraph.Runs.Count, Is.EqualTo(9));
        Assert.That(paragraph.Runs.Capacity, Is.EqualTo(16));
        Assert.That(paragraph.Runs.HasBreak(), Is.True);
        var texts = new List<string>();
        foreach (var run in paragraph.Runs) texts.Add(run.Text);
        Assert.That(texts, Is.EqualTo(new[] { "0", "1", "2", "3", "4", "5", "6", "7", "8" }));
        Assert.That(paragraph.Runs[^1].Text, Is.EqualTo("8"));
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = paragraph.Runs[9]);

        var sized = new Paragraph(style, 0, runCapacity: 1);
        sized.Runs.Add(new TextRun("a", style));
        Assert.That(sized.Runs.Capacity, Is.EqualTo(1));
        sized.Runs.Add(new TextRun("b", style));
        Assert.That(sized.Runs.Capacity, Is.EqualTo(2));
        Assert.That(sized.Runs.Select(r => r.Text), Is.EqualTo(new[] { "a", "b" }));
    }
}
