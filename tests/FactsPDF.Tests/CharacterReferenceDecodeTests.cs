using NUnit.Framework;

namespace FactsPDF.Tests;

// Z6i: character references are decoded into one buffer and matched as spans, so decoding text
// with references allocates only the decoded string (no builder, name substrings or per-scalar
// strings), and its results and failures are unchanged.
public sealed class CharacterReferenceDecodeTests
{
    [TestCase("plain", "plain")]
    [TestCase("a &amp; b", "a & b")]
    [TestCase("&lt;p&gt; &quot;q&quot; &apos;s&apos;", "<p> \"q\" 's'")]
    [TestCase("x&nbsp;y", "x y")]
    [TestCase("&#65;&#x42;&#X43;", "ABC")]
    [TestCase("&#128512; &#x1F600;", "\U0001F600 \U0001F600")]
    [TestCase("& alone, &; and &&amp;", "& alone, &; and &&")]
    [TestCase("&#1;", "\u0001")]
    public void DecodesSupportedReferences(string text, string expected)
        => Assert.That(HtmlTokens.Decode(text, 0), Is.EqualTo(expected));

    [Test]
    public void DecodesLongTextBeyondTheStackBuffer()
    {
        var text = string.Concat(Enumerable.Repeat("ab&amp;&#x1F600;", 100));
        var expected = string.Concat(Enumerable.Repeat("ab&\U0001F600", 100));
        Assert.That(HtmlTokens.Decode(text, 0), Is.EqualTo(expected));
    }

    [TestCase("a &foo; b", 2, "Unsupported character reference '&foo;'.")]
    [TestCase("x &#0;", 2, "Unsupported character reference '&#0;'.")]
    [TestCase("x &#xD800;", 2, "Unsupported character reference '&#xD800;'.")]
    [TestCase("x &#x110000;", 2, "Unsupported character reference '&#x110000;'.")]
    [TestCase("ab &amp", 3, "Character references need a supported name and semicolon.")]
    [TestCase("&abcdefghijklmnopqrstuvwxyz0123456789;", 0, "Character references need a supported name and semicolon.")]
    public void RejectsUnsupportedReferencesWithTheirOffset(string text, int index, string message)
    {
        var error = Assert.Throws<FactsPdfException>(() => HtmlTokens.Decode(text, 100))!;
        Assert.Multiple(() =>
        {
            Assert.That(error.Code, Is.EqualTo("FPDF1104"));
            Assert.That(error.SourceOffset, Is.EqualTo(100 + index));
            Assert.That(error.Message, Does.Contain(message));
        });
    }

    // On main 67951a9 decoding this 59-character text allocated a StringBuilder, its chunk, one
    // name substring per reference and a string per numeric reference: 568 bytes per call. Now
    // only the 29-character result (80 bytes) is allocated.
    [AllocationBudget]
    [Test]
    public void DecodingAllocatesOnlyTheResult()
    {
        const string text = "Tom &amp; Jerry &lt;3 &#x41;&#66; caf&#233; x&nbsp;y &quot;";
        var expected = HtmlTokens.Decode(text, 0);
        const int calls = 1_000;
        for (var i = 0; i < 10; i++) HtmlTokens.Decode(text, 0);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < calls; i++) HtmlTokens.Decode(text, 0);
        var perCall = (GC.GetAllocatedBytesForCurrentThread() - before) / (double)calls;
        var resultBytes = (22 + 2 * expected.Length + 7) / 8 * 8;
        TestContext.Out.WriteLine($"decode: {perCall:F1} bytes per call, result {resultBytes} bytes");
        Assert.That(perCall, Is.LessThanOrEqualTo(resultBytes));
    }
}
