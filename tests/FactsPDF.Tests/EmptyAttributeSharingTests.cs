using NUnit.Framework;

namespace FactsPDF.Tests;

// Start tags without attributes, and elements without classes, share one empty
// collection instead of allocating their own (the tokenizer runs twice per conversion).
[TestFixture]
public sealed class EmptyAttributeSharingTests
{
    [Test]
    public void StartTagsWithoutAttributesShareOneEmptyDictionary()
    {
        var starts = HtmlTokens.Read("<p>a</p><p>b</p>", default).Where(t => t.Kind == HtmlTokenKind.Start).ToList();
        Assert.That(starts, Has.Count.EqualTo(2));
        Assert.That(starts[0].Attributes, Is.Empty);
        Assert.That(starts[1].Attributes, Is.SameAs(starts[0].Attributes));
    }

    [Test]
    public void AttributesStillReadCaseInsensitivelyAndStayPrivate()
    {
        var starts = HtmlTokens.Read("<p ID=\"x\" Class=\"a b\">a</p><p>b</p>", default).Where(t => t.Kind == HtmlTokenKind.Start).ToList();
        Assert.That(starts[0].Attributes!["id"], Is.EqualTo("x"));
        Assert.That(starts[0].Attributes, Is.Not.SameAs(starts[1].Attributes));
        Assert.That(starts[1].Attributes, Is.Empty);
        var element = CssElement.From(starts[0]);
        Assert.That(element.Classes, Is.EquivalentTo(new[] { "a", "b" }));
    }

    [Test]
    public void ElementsWithoutClassesShareOneEmptySet()
    {
        var starts = HtmlTokens.Read("<p>a</p><p class=\" \">b</p><p id=\"q\">c</p>", default).Where(t => t.Kind == HtmlTokenKind.Start).ToList();
        var classes = starts.Select(t => CssElement.From(t).Classes).ToList();
        Assert.That(classes[0], Is.Empty);
        Assert.That(classes[1], Is.SameAs(classes[0]));
        Assert.That(classes[2], Is.SameAs(classes[0]));
    }
}
