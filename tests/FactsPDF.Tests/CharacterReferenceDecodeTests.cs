using NUnit.Framework;

namespace FactsPDF.Tests;

// Character references decode as the HTML tokenizer decodes them (HTML Standard 13.2.5.72-80):
// all 2,231 named references, the 106 legacy names also without a semicolon, numeric references
// with the tokenizer's replacements, and anything else left as written. Decoding writes into one
// buffer and matches names as spans, so it allocates only the decoded string (Z6i).
public sealed class CharacterReferenceDecodeTests
{
    [TestCase("plain", "plain")]
    [TestCase("a &amp; b", "a & b")]
    [TestCase("&lt;p&gt; &quot;q&quot; &apos;s&apos;", "<p> \"q\" 's'")]
    [TestCase("x&nbsp;y", "x\u00A0y")]
    [TestCase("&#65;&#x42;&#X43;", "ABC")]
    [TestCase("&#128512; &#x1F600;", "\U0001F600 \U0001F600")]
    [TestCase("& alone, &; and &&amp;", "& alone, &; and &&")]
    [TestCase("&#1;", "\u0001")]
    // Names beyond the six the earlier subset knew, including two-scalar and astral values.
    [TestCase("&eacute;&euro;&hellip;&mdash;&copy;", "\u00E9\u20AC\u2026\u2014\u00A9")]
    [TestCase("&NotEqualTilde;", "\u2242\u0338")]
    [TestCase("&Afr;", "\U0001D504")]
    [TestCase("&CounterClockwiseContourIntegral;", "\u2233")]
    [TestCase("&fjlig;", "fj")]
    // Names are case-sensitive: &AMP; is a name of its own, &Amp; is no name and stays as written.
    [TestCase("&AMP; &Amp;", "& &Amp;")]
    // Legacy names decode without their semicolon, and the longest name wins ("&not" in "&notit;").
    [TestCase("&amp &lt3 &copy2026", "& <3 \u00A92026")]
    [TestCase("&notit; &notin;", "\u00ACit; \u2209")]
    // A name that is not a legacy one needs its semicolon; an unknown name stays as written.
    [TestCase("&hellip &bogus; &zz", "&hellip &bogus; &zz")]
    // Numeric references: the semicolon is optional, zero, surrogates and values past U+10FFFF are
    // U+FFFD, 0x80-0x9F are the windows-1252 characters, and a missing digit leaves the text alone.
    [TestCase("&#65x &#x41g", "Ax Ag")]
    [TestCase("&#0; &#xD800; &#x110000; &#99999999999;", "\uFFFD \uFFFD \uFFFD \uFFFD")]
    [TestCase("&#128; &#x99; &#x81;", "\u20AC \u2122 \u0081")]
    [TestCase("&#; &#x; &#xg;", "&#; &#x; &#xg;")]
    public void DecodesTextReferencesAsTheHtmlTokenizer(string text, string expected)
        => Assert.That(HtmlTokens.Decode(text), Is.EqualTo(expected));

    // In an attribute value a legacy name without its semicolon followed by '=' or an alphanumeric
    // is not a reference (query strings keep "&copy=2"); the same text in content decodes.
    [TestCase("?a=1&copy=2", "?a=1&copy=2", "?a=1\u00A9=2")]
    [TestCase("?a=1&copyx", "?a=1&copyx", "?a=1\u00A9x")]
    [TestCase("&copy;=x &copy x", "\u00A9=x \u00A9 x", "\u00A9=x \u00A9 x")]
    [TestCase("&amp;=1", "&=1", "&=1")]
    public void AttributeValuesKeepAmbiguousLegacyNames(string text, string attribute, string content)
    {
        Assert.Multiple(() =>
        {
            Assert.That(HtmlTokens.Decode(text, attribute: true), Is.EqualTo(attribute));
            Assert.That(HtmlTokens.Decode(text), Is.EqualTo(content));
        });
    }

    [Test]
    public void TheTableHasEveryNameOfTheHtmlStandard()
    {
        // Spot checks across the table: first and last names, the longest, and a legacy name both ways.
        Assert.Multiple(() =>
        {
            Assert.That(HtmlCharacterReferences.Match("AElig;", out var aelig), Is.EqualTo(6));
            Assert.That(aelig, Is.EqualTo("\u00C6"));
            Assert.That(HtmlCharacterReferences.Match("zwnj;", out var zwnj), Is.EqualTo(5));
            Assert.That(zwnj, Is.EqualTo("\u200C"));
            Assert.That(HtmlCharacterReferences.Match("CounterClockwiseContourIntegral;", out _), Is.EqualTo(HtmlCharacterReferences.LongestName));
            Assert.That(HtmlCharacterReferences.Match("yuml", out var yuml), Is.EqualTo(4));
            Assert.That(yuml, Is.EqualTo("\u00FF"));
            Assert.That(HtmlCharacterReferences.Match("hellip", out _), Is.Zero);
        });
    }

    [Test]
    public void DecodesLongTextBeyondTheStackBuffer()
    {
        var text = string.Concat(Enumerable.Repeat("ab&amp;&#x1F600;", 100));
        var expected = string.Concat(Enumerable.Repeat("ab&\U0001F600", 100));
        Assert.That(HtmlTokens.Decode(text), Is.EqualTo(expected));
    }

    [Test]
    public void ReferencesPrintTheirCharactersInThePdf()
    {
        // Content and attribute values decode before layout: the text and a title attribute
        // selector see the decoded characters, as in Chrome.
        var withReferences = FactsPDF.Pdf.Write().ToBytes(
            "<style>p[title='a&b'] { color: #c00 }</style><p title='a&amp;b'>Tom &amp Jerry &lt3 &#x41;&#66; &bogus;</p>");
        var withText = FactsPDF.Pdf.Write().ToBytes(
            "<style>p[title='a&b'] { color: #c00 }</style><p title='a&amp;b'>Tom &amp; Jerry &lt;3 AB &amp;bogus;</p>");
        Assert.That(withReferences, Is.EqualTo(withText));
    }

    // On main 67951a9 decoding this 59-character text allocated a StringBuilder, its chunk, one
    // name substring per reference and a string per numeric reference: 568 bytes per call. Now
    // only the 29-character result (80 bytes) is allocated.
    [AllocationBudget]
    [Test]
    public void DecodingAllocatesOnlyTheResult()
    {
        const string text = "Tom &amp; Jerry &lt;3 &#x41;&#66; caf&#233; x&nbsp;y &quot;";
        var expected = HtmlTokens.Decode(text);
        const int calls = 1_000;
        for (var i = 0; i < 10; i++) HtmlTokens.Decode(text);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < calls; i++) HtmlTokens.Decode(text);
        var perCall = (GC.GetAllocatedBytesForCurrentThread() - before) / (double)calls;
        var resultBytes = (22 + 2 * expected.Length + 7) / 8 * 8;
        TestContext.Out.WriteLine($"decode: {perCall:F1} bytes per call, result {resultBytes} bytes");
        Assert.That(perCall, Is.LessThanOrEqualTo(resultBytes));
    }
}
