using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// Every page has the html, head and body elements the HTML parser builds, written or not
/// (HtmlImpliedElements). Selectors and rules that name them match a page without those tags,
/// and content after the body end tag, exactly as Chrome's DOM does. The expected matches follow
/// HTML tree construction (the "initial" to "in body" insertion modes) and Chrome's
/// Element.matches() on the same markup.
/// </summary>
[TestFixture]
public sealed class ImpliedDocumentElementTests
{
    private static readonly Rgb Red = new(1, 0, 0), Black = new(0, 0, 0);

    private static byte[] Pdf(string html, PdfOptions? options = null) => TableLayoutTests.Pdf(html, options);

    private static Rgb[] Colors(string html)
        => TableLayoutTests.Layout(html).SelectMany(page => page.Runs)
            .Where(run => run.Text.Trim().Length > 0).Select(run => run.Style.Color).ToArray();

    // body > p, html body p and :root > body > p match a fragment's top-level p, not a nested one.
    [TestCase("body > p")]
    [TestCase("html > body > p")]
    [TestCase("html body > p")]
    [TestCase(":root > body > p")]
    [TestCase("html > :last-child > p")]
    public void BodySelectorsMatchAFragmentAsInChrome(string selector)
        => Assert.That(Colors("<style>" + selector + "{color:#f00}</style><p>A</p><div><p>B</p></div><p>C</p>"),
            Is.EqualTo(new[] { Red, Black, Red }));

    [Test]
    public void ContentAfterTheBodyEndTagIsInBody()
    {
        Assert.That(Colors("<html><head><style>body > p{color:#f00}</style></head><body><p>A</p></body></html><p>B</p>"),
            Is.EqualTo(new[] { Red, Red }));
        Assert.That(Colors("<html><head><style>body > p:last-child{color:#f00}</style></head><body><p>A</p></body><p>B</p></html>"),
            Is.EqualTo(new[] { Black, Red }));
    }

    [Test]
    public void HeadContentBeforeBodyContentGoesIntoHead()
    {
        // style, meta, link, base and title before body content are head elements in Chrome.
        Assert.That(Pdf("<meta charset=utf-8><link rel=icon href=/i.png><p>A</p>"), Is.EqualTo(Pdf("<p>A</p>")));
        // The title is head metadata too: it is the PDF Title and prints nothing.
        Assert.That(System.Text.Encoding.Latin1.GetString(Pdf("<meta charset=utf-8><title>T</title><p>A</p>")), Does.Contain("/Title <FEFF0054>"));
        // A style after </head> is still put into head, so p stays body's first child.
        Assert.That(Colors("<html><head></head><style>body > p:first-child{color:#f00}</style><body><p>A</p></body></html>"),
            Is.EqualTo(new[] { Red }));
        // Both style elements are in head, so p is body's only child.
        Assert.That(Colors("<style>p:only-child{color:#f00}</style><style>i{color:#000}</style><p>A</p>"),
            Is.EqualTo(new[] { Red }));
    }

    [Test]
    public void BodyAndHtmlRulesApplyToAPageWithoutThoseTags()
    {
        const string content = "<p>Paid</p><div><p>Due</p></div>";
        var implied = Pdf("<style>body{color:#1f4e79}html{font-size:14pt}</style>" + content);
        Assert.That(implied, Is.EqualTo(Pdf("<html><head><style>body{color:#1f4e79}html{font-size:14pt}</style></head><body>" + content + "</body></html>")));
        Assert.That(implied, Is.Not.EqualTo(Pdf(content)));
        // A body with box declarations paints and lays out like an explicit body.
        Assert.That(Pdf("<style>body{padding:12pt;background:#eef}</style>" + content),
            Is.EqualTo(Pdf("<html><head></head><body style='padding:12pt;background:#eef'>" + content + "</body></html>")));
    }

    [Test]
    public void AnImpliedBodyIsTheSameBoxAsAnExplicitOne()
    {
        // The user-agent sheet gives body its 8px margin, so the implied body is a box holding the paragraph.
        var root = HtmlDocumentReader.ReadTree("<p>A</p>", new PdfOptions(), default);
        var body = (BlockNode)root.Children.Single();
        Assert.That(body.Name, Is.EqualTo("body"));
        Assert.That(((BlockNode)body.Children.Single()).Name, Is.EqualTo("p"));
        Assert.That(Pdf("<p>A</p>"), Is.EqualTo(Pdf("<html><head></head><body><p>A</p></body></html>")));
    }

    [Test]
    public void ImpliedElementsCountAgainstNoLimit()
    {
        // Two elements are written; the implied html, head and body are not counted.
        Assert.That(Pdf("<div><p>A</p></div>", new PdfOptions { MaxElements = 2 }), Is.Not.Empty);
        Assert.That(Assert.Throws<FactsPdfException>(() => Pdf("<div><p>A</p></div><p>B</p>", new PdfOptions { MaxElements = 2 }))!.Code,
            Is.EqualTo("FPDF1002"));
    }

    [TestCase("<html><body><p>A</p></body></html><body class=late><p>B</p></body>")]
    [TestCase("<p>A</p><html lang=en><p>B</p>")]
    [TestCase("<html><head></head><body><head id=h></head><p>A</p></body></html>")]
    public void RepeatedDocumentTagsWithAttributesFail(string html)
    {
        var error = Assert.Throws<FactsPdfException>(() => Pdf(html))!;
        Assert.That(error.Code, Is.EqualTo("FPDF1101"));
        Assert.That(error.SourceOffset, Is.GreaterThan(0));
    }

    [TestCase("<html><body><p>A</p></body></html><body><p>B</p></body>", "<p>A</p><p>B</p>")]
    [TestCase("<div><body><p>A</p></body></div>", "<div><p>A</p></div>")]
    [TestCase("<p>A</p><head></head><html><p>B</p></html>", "<p>A</p><p>B</p>")]
    public void RepeatedDocumentTagsWithoutAttributesAreIgnored(string html, string equivalent)
        => Assert.That(Pdf(html), Is.EqualTo(Pdf(equivalent)));
}
