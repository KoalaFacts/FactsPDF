using NUnit.Framework;

namespace FactsPDF.Tests;

// An element's class attribute is read into an ordered list without duplicates; an element
// without classes shares one empty list and a single class reuses the attribute text.
[TestFixture]
public sealed class CssClassListTests
{
    private static CssElement Element(string html) => CssElement.From(HtmlTokens.Read(html, default).First());

    [TestCase("<p>")]
    [TestCase("<p class=\"\">")]
    [TestCase("<p class=\" \t\r\n\f\">")]
    public void NoClassesAreEmpty(string html)
    {
        var classes = Element(html).Classes;
        Assert.That(classes.Names.ToArray(), Is.Empty);
        Assert.That(classes.Contains("p"), Is.False);
        Assert.That(classes.Contains(""), Is.False);
    }

    [Test]
    public void ElementsWithoutClassesShareOneList()
    {
        var first = Element("<p>").Classes;
        var second = Element("<div class=\"  \">").Classes;
        Assert.That(first.SharesStorageWith(second), Is.True);
    }

    [Test]
    public void ASingleClassReusesTheAttributeText()
    {
        var token = HtmlTokens.Read("<p class=total>", default).First();
        var names = CssElement.From(token).Classes.Names;
        Assert.That(names.Length, Is.EqualTo(1));
        Assert.That(names[0], Is.SameAs(token.Attributes!["class"]));
    }

    [TestCase("<p class=\"a b\ta\nc\fb\">", new[] { "a", "b", "c" })]
    [TestCase("<p class=\"  total  \">", new[] { "total" })]
    [TestCase("<p class=\"B b B\">", new[] { "B", "b" })]
    public void ClassesKeepFirstOccurrenceOrderWithoutDuplicates(string html, string[] expected)
    {
        var classes = Element(html).Classes;
        Assert.That(classes.Names.ToArray(), Is.EqualTo(expected));
        foreach (var name in expected) Assert.That(classes.Contains(name), Is.True, name);
        Assert.That(classes.Contains("d"), Is.False);
    }

    [Test]
    public void ClassesAreCaseSensitive()
    {
        var classes = Element("<p class=Total>").Classes;
        Assert.That(classes.Contains("Total"), Is.True);
        Assert.That(classes.Contains("total"), Is.False);
    }

    [TestCase(16)]
    [TestCase(17)]
    [TestCase(400)]
    public void ManyClassesAreAllFoundOnce(int count)
    {
        var names = Enumerable.Range(0, count).Select(i => "c" + i).ToArray();
        var html = "<p class=\"" + string.Join(' ', names) + " " + string.Join(' ', names.Reverse()) + "\">";
        var classes = Element(html).Classes;
        Assert.That(classes.Names.ToArray(), Is.EqualTo(names));
        foreach (var name in names) Assert.That(classes.Contains(name), Is.True, name);
        Assert.That(classes.Contains("c" + count), Is.False);
    }

    [Test]
    public void DuplicateAndManyClassesStillCascade()
    {
        var classes = string.Join(' ', Enumerable.Range(0, 40).Select(i => "k" + i));
        var html = "<html><head><style>.a { color: #ff0000 } .a.k39 { color: #00ff00 }</style></head>" +
            "<body><p class=\"a a " + classes + " a\">Text</p></body></html>";
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, new PdfOptions());
        var text = System.Text.Encoding.Latin1.GetString(output.ToArray());
        Assert.That(text, Does.Contain("0 1 0 rg"));
        Assert.That(text, Does.Not.Contain("1 0 0 rg"));
    }
}
