using NUnit.Framework;

namespace FactsPDF.Tests;

// The tokenizer returns known tag and attribute names without copying them, and start tags
// without attributes share one empty attribute map; names and attributes stay unchanged.
[TestFixture]
public sealed class HtmlTokenNameTests
{
    private static List<HtmlToken> Read(string html) => HtmlTokens.Read(html, default).ToList();

    [TestCase("<TD>", "td")]
    [TestCase("<Td>", "td")]
    [TestCase("<colGroup>", "colgroup")]
    [TestCase("<H1>", "h1")]
    [TestCase("<x-Widget>", "x-widget")]
    [TestCase("<my_TAG>", "my_tag")]
    [TestCase("<tdx>", "tdx")]
    [TestCase("<t>", "t")]
    public void TagNamesAreLowerCased(string html, string expected) =>
        Assert.That(Read(html)[0].Value, Is.EqualTo(expected));

    [TestCase("CLASS", "class")]
    [TestCase("ColSpan", "colspan")]
    [TestCase("data-Row", "data-row")]
    [TestCase("classx", "classx")]
    public void AttributeNamesAreLowerCased(string name, string expected)
    {
        var token = Read($"<td {name}=\"v\">")[0];
        Assert.That(token.Attributes!.Keys, Is.EqualTo(new[] { expected }));
        Assert.That(token.Attributes[expected], Is.EqualTo("v"));
    }

    [Test]
    public void KnownNamesAreReturnedWithoutACopy()
    {
        var tokens = Read("<td CLASS=a><TD class=b></td></TD>");
        Assert.That(tokens[1].Value, Is.SameAs(tokens[0].Value));
        Assert.That(tokens[3].Value, Is.SameAs(tokens[2].Value));
        Assert.That(tokens[1].Attributes!.Keys.Single(), Is.SameAs(tokens[0].Attributes!.Keys.Single()));
    }

    [Test]
    public void StartTagsWithoutAttributesShareOneEmptyMap()
    {
        var tokens = Read("<p><span>x</span><br/><p class=c>");
        Assert.That(tokens[1].Attributes, Is.SameAs(tokens[0].Attributes));
        Assert.That(tokens[0].Attributes, Is.Empty);
        Assert.That(tokens[0].Attributes!.Comparer, Is.EqualTo(StringComparer.OrdinalIgnoreCase));
        Assert.That(tokens[4].Attributes, Is.SameAs(tokens[0].Attributes)); // <br/>
        Assert.That(tokens[5].Attributes, Is.Not.SameAs(tokens[0].Attributes));
        Assert.That(tokens[5].Attributes!["CLASS"], Is.EqualTo("c"));
        Assert.That(Read("<b>")[0].Attributes, Is.Empty);
    }

    [Test]
    public void DuplicateAttributesInDifferentCaseStillFail()
    {
        var error = Assert.Throws<FactsPdfException>(() => Read("<td Class=a CLASS=b>"));
        Assert.That(error!.Code, Is.EqualTo("FPDF1101"));
        Assert.That(error.Message, Does.Contain("Duplicate attribute."));
    }

    [Test]
    public void ReadingKnownTagsAllocatesNoNameCopies()
    {
        const string html = "<table><tbody><tr><td></td><td></td></tr></tbody></table>";
        static long Measure(string html)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            foreach (var _ in HtmlTokens.Read(html, default)) { }
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }
        for (var i = 0; i < 3; i++) Measure(html);
        var known = Measure(html);
        var unknown = Measure(html.Replace("td", "tq", StringComparison.Ordinal));
        Assert.That(known, Is.LessThan(unknown), "known names must not allocate a copy each");
    }
}
