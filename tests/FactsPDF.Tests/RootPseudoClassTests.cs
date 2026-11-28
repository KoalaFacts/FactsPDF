using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// :root is the structural pseudo-class matching the document's root element, which in HTML is
/// the html element. It renders exactly like the same declarations on html when specificity does
/// not decide anything, counts as a pseudo-class (0,1,0) when it does, and composes with type,
/// class, descendant and child selectors and with the structural pseudo-classes. Unsupported
/// pseudo-classes and pseudo-elements fail with FPDF1203.
/// </summary>
[TestFixture]
public sealed class RootPseudoClassTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 400, Margin = 20 };

    private const string Content =
        "<h2>Invoice</h2><div class='panel'><p>Paid in full</p></div><p class='note'>Thanks</p>";

    private static byte[] Pdf(string html)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, Page);
        return output.ToArray();
    }

    private static string Document(string css, string htmlAttributes = "") =>
        $"<!doctype html><html{htmlAttributes}><head><style>h2{{font-weight:normal}}{css}</style></head><body>{Content}</body></html>";

    [TestCase(":root{color:#1f4e79}", "html{color:#1f4e79}")]
    [TestCase(":ROOT{color:#1f4e79}", "html{color:#1f4e79}")]
    [TestCase(":root{font-size:14pt;line-height:1.4}", "html{font-size:14pt;line-height:1.4}")]
    [TestCase(":root p{color:#1f4e79}", "html p{color:#1f4e79}")]
    [TestCase(":root>body{color:#1f4e79}", "html>body{color:#1f4e79}")]
    [TestCase("html:root{color:#1f4e79}", "html{color:#1f4e79}")]
    [TestCase(":root .panel{border:1pt solid #333;padding:4pt}", ".panel{border:1pt solid #333;padding:4pt}")]
    [TestCase(":root,p{color:#1f4e79}", "html,p{color:#1f4e79}")]
    [TestCase(":root{margin:0;padding:0}", "")]
    public void RootRendersLikeTheSameRuleOnHtml(string root, string html)
    {
        Assert.That(Pdf(Document(root)), Is.EqualTo(Pdf(Document(html))), root);
    }

    [Test]
    public void TheComparedRulesChangeThePage()
    {
        Assert.That(Pdf(Document("html{color:#1f4e79}")), Is.Not.EqualTo(Pdf(Document(""))));
        Assert.That(Pdf(Document("html p{color:#1f4e79}")), Is.Not.EqualTo(Pdf(Document(""))));
    }

    [Test]
    public void RootBeatsHtmlByPseudoClassSpecificity()
    {
        // :root (0,1,0) beats html (0,0,1) even though html is declared later.
        var rootWins = Pdf(Document(":root{color:#1f4e79}html{color:#7a1f1f}"));
        Assert.That(rootWins, Is.EqualTo(Pdf(Document("html{color:#1f4e79}"))));
        Assert.That(rootWins, Is.Not.EqualTo(Pdf(Document("html{color:#7a1f1f}"))));
    }

    [Test]
    public void ClassOnHtmlTiesWithRootAndSourceOrderDecides()
    {
        // .doc and :root both have specificity (0,1,0): the later one wins.
        Assert.That(Pdf(Document(":root{color:#1f4e79}.doc{color:#7a1f1f}", " class='doc'")),
            Is.EqualTo(Pdf(Document("html{color:#7a1f1f}", " class='doc'"))));
        Assert.That(Pdf(Document(".doc{color:#7a1f1f}:root{color:#1f4e79}", " class='doc'")),
            Is.EqualTo(Pdf(Document("html{color:#1f4e79}", " class='doc'"))));
    }

    [Test]
    public void HtmlRootIsBeatenByTwoPseudoClassesAndRootBeatsATypeChain()
    {
        // html:root is (0,1,1) and beats :root (0,1,0) declared later.
        Assert.That(Pdf(Document("html:root{color:#1f4e79}:root{color:#7a1f1f}")),
            Is.EqualTo(Pdf(Document("html{color:#1f4e79}"))));
        // :root p (0,1,1) beats body p (0,0,2) declared later.
        Assert.That(Pdf(Document(":root p{color:#1f4e79}body p{color:#7a1f1f}")),
            Is.EqualTo(Pdf(Document("p{color:#1f4e79}"))));
    }

    [Test]
    public void RootMatchesOnlyTheRootElement()
    {
        // :root never matches a descendant element.
        Assert.That(Pdf(Document("p:root{color:#1f4e79}")), Is.EqualTo(Pdf(Document(""))));
        Assert.That(Pdf(Document("body :root{color:#1f4e79}")), Is.EqualTo(Pdf(Document(""))));
        Assert.That(Pdf(Document(":root:root{color:#1f4e79}")), Is.EqualTo(Pdf(Document("html{color:#1f4e79}"))));
    }

    [Test]
    public void RootWithoutAnHtmlTagMatchesTheImpliedHtml()
    {
        // The HTML parser implies html, so :root and html{} both match it, as in Chrome.
        const string body = "<p>Paid</p>";
        var root = Pdf($"<style>:root{{color:#1f4e79}}</style>{body}");
        Assert.That(root, Is.EqualTo(Pdf($"<style>html{{color:#1f4e79}}</style>{body}")));
        Assert.That(root, Is.EqualTo(Pdf($"<p style='color:#1f4e79'>Paid</p>")));
        Assert.That(root, Is.Not.EqualTo(Pdf(body)));
    }

    [Test]
    public void RootComposesWithStructuralPseudoClassesAndNot()
    {
        // html is the document's only element; :not(:root) is every other element.
        Assert.That(Pdf(Document(":root:first-child:only-of-type{color:#1f4e79}")), Is.EqualTo(Pdf(Document("html{color:#1f4e79}"))));
        Assert.That(Pdf(Document(":root:not(:root){color:#1f4e79}")), Is.EqualTo(Pdf(Document(""))));
        Assert.That(Pdf(Document("p:not(:root){color:#1f4e79}")), Is.EqualTo(Pdf(Document("p{color:#1f4e79}"))));
        Assert.That(Pdf(Document(":root > :last-child > p:first-of-type{color:#1f4e79}")),
            Is.EqualTo(Pdf(Document("body > p:first-of-type{color:#1f4e79}"))));
    }

    [TestCase(":hover{color:red}", "Unsupported pseudo-class ':hover'.")]
    [TestCase("::first-line{color:red}", "Pseudo-elements are not supported.")]
    [TestCase(":root::first-line{color:red}", "Pseudo-elements are not supported.")]
    [TestCase(": root{color:red}", "Expected a pseudo-class name adjacent to ':'.")]
    [TestCase(":root(){color:red}", "Unsupported pseudo-class ':root()'.")]
    [TestCase(":rooted{color:red}", "Unsupported pseudo-class ':rooted'.")]
    public void OtherPseudoClassesStillFail(string css, string message)
    {
        var error = Assert.Throws<FactsPdfException>(() => Pdf(Document(css)))!;
        Assert.That(error.Code, Is.EqualTo("FPDF1203"), css);
        Assert.That(error.Message, Does.Contain(message), css);
    }
}
