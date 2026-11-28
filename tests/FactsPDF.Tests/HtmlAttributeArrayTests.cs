using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

// A start tag's attributes are one exact-size array of name/value pairs instead of a hash map per
// tag; lookups, order, duplicates and case behave as the map did.
[TestFixture]
[NonParallelizable]
public sealed class HtmlAttributeArrayTests
{
    private static List<HtmlToken> Read(string html) => HtmlTokens.Read(html, default).ToList();

    private static long TokenizeAllocated(string html)
    {
        for (var i = 0; i < 3; i++) _ = HtmlTokens.Read(html, default).Count();
        var before = GC.GetAllocatedBytesForCurrentThread();
        _ = HtmlTokens.Read(html, default).Count();
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    private static string Cells(int count, string attributes)
    {
        var html = new StringBuilder();
        for (var i = 0; i < count; i++) html.Append("<td ").Append(attributes).Append('>');
        return html.ToString();
    }

    // Measured on main 5d30f8a: 280 bytes per one-attribute start tag with a Dictionary per tag
    // (map, buckets and entries); 136 with the token, its HtmlAttributes and a one-pair array.
    [AllocationBudget]
    [Test]
    public void AStartTagWithOneAttributeAllocatesNoHashMap()
    {
        var small = TokenizeAllocated(Cells(1_000, "class=\"c\""));
        var large = TokenizeAllocated(Cells(2_000, "class=\"c\""));
        var perTag = (large - small) / 1_000.0;
        TestContext.Out.WriteLine($"tokenize per one-attribute tag: {perTag:F1} bytes");
        Assert.That(perTag, Is.LessThan(150));
    }

    [Test]
    public void AttributesKeepSourceOrderAndIgnoreCaseOnLookup()
    {
        var attributes = Read("<td ID=a class=b colspan=2 data-x=\"&amp;\">")[0].Attributes!;
        Assert.That(attributes.Select(pair => pair.Key), Is.EqualTo(new[] { "id", "class", "colspan", "data-x" }));
        Assert.That(attributes.Count, Is.EqualTo(4));
        Assert.That(attributes["CLASS"], Is.EqualTo("b"));
        Assert.That(attributes.GetValueOrDefault("data-x"), Is.EqualTo("&"));
        Assert.That(attributes.GetValueOrDefault("alt"), Is.Null);
        Assert.That(attributes.TryGetValue("missing", out _), Is.False);
        Assert.Throws<KeyNotFoundException>(() => _ = attributes["missing"]);
        var pairs = new List<string>();
        foreach (var (name, value) in attributes) pairs.Add(name + "=" + value);
        Assert.That(pairs, Is.EqualTo(new[] { "id=a", "class=b", "colspan=2", "data-x=&" }));
    }

    // A repeated attribute keeps its first value (the HTML parser's recovery), whether the repeat is found by
    // direct comparison or, past sixteen attributes, through the per-thread name set.
    [TestCase(2)]
    [TestCase(16)]
    [TestCase(17)]
    [TestCase(40)]
    public void ADuplicateAttributeKeepsTheFirstValue(int distinct)
    {
        var html = new StringBuilder("<p");
        for (var i = 0; i < distinct; i++) html.Append(" a").Append(i).Append("=v");
        html.Append(" A1=w>");
        var attributes = Read(html.ToString())[0].Attributes!;
        Assert.That(attributes, Has.Count.EqualTo(distinct));
        Assert.That(attributes["a1"], Is.EqualTo("v"));
    }

    [TestCase(17)]
    [TestCase(300)]
    public void ALongAttributeListLooksUpEveryName(int count)
    {
        var html = new StringBuilder("<p");
        for (var i = 0; i < count; i++) html.Append(" a").Append(i).Append("=v").Append(i);
        var attributes = Read(html.Append('>').ToString())[0].Attributes!;
        Assert.That(attributes.Count, Is.EqualTo(count));
        for (var i = 0; i < count; i++) Assert.That(attributes["A" + i], Is.EqualTo("v" + i));
        Assert.That(attributes.ContainsKey("a" + count), Is.False);
    }

    // A tag that fails part-way leaves no attributes for the next tag read on the same thread.
    [Test]
    public void AFailedTagLeavesNoAttributesBehind()
    {
        Assert.Throws<FactsPdfException>(() => Read("<p id=a class=b id=&bogus;>"));
        Assert.Throws<FactsPdfException>(() => Read("<p id=a class=\"b>"));
        var attributes = Read("<p title=t>")[0].Attributes!;
        Assert.That(attributes.Select(pair => pair.Key), Is.EqualTo(new[] { "title" }));
        Assert.That(Read("<p>")[0].Attributes, Is.SameAs(Read("<b>")[0].Attributes));
    }

    // Attribute selectors and the reader's attribute walk see the same values as before.
    [Test]
    public void AttributeSelectorsMatchThroughTheArray()
    {
        var html = "<style>p[lang=\"en\"]{color:#ff0000} p[id]{color:#00ff00}</style>"
            + "<p lang=\"en\">r</p><p class=\"c\" id=\"g\">g</p><p>n</p>";
        var pdf = Encoding.Latin1.GetString(Pdf.Write().ToBytes(html));
        Assert.That(pdf, Does.Contain("1 0 0 rg"));
        Assert.That(pdf, Does.Contain("0 1 0 rg"));
    }
}
