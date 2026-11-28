using NUnit.Framework;

namespace FactsPDF.Tests;

// Chrome 158 prints a page whose root color-scheme resolves to dark on a #121212 canvas, with the default
// text colour CanvasText (white) and links in LinkText (#9e9eff); print prefers light, so a list that also
// names light stays light. The scheme comes from the root's color-scheme (html or :root rules, the html
// style attribute) or else the first valid meta color-scheme. Chrome probes (headless Chrome 158, 12
// pages incl. links, hr, borders, lists, tables, author colours) print like FactsPDF within antialiasing.
[TestFixture]
public sealed class DarkColorSchemeTests
{
    private static byte[] Pdf(string html, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, options ?? new PdfOptions { PageWidth = 260, PageHeight = 300, Margin = 12 });
        return output.ToArray();
    }

    private const string Body = "<p>Paid <a href='https://example.com/x'>link</a></p><div style='border:2px solid;padding:3px'>Box</div>";

    private static string Doc(string head, string body = Body) => "<!doctype html><html><head>" + head + "</head><body>" + body + "</body></html>";

    // The same page written out with the colours the dark scheme gives (the author-level equivalent).
    private static readonly string Written = Doc("<style>html{background:#121212;color:#fff}a{color:#9e9eff}</style>");

    [TestCase("<meta name=color-scheme content=dark>")]
    [TestCase("<meta name=color-scheme content='only dark'>")]
    [TestCase("<meta name=color-scheme content='dark foo'>")]
    [TestCase("<meta name=color-scheme content=dark><meta name=color-scheme content=light>")]
    [TestCase("<style>html{color-scheme:dark}</style>")]
    [TestCase("<style>:root{color-scheme:only dark}</style>")]
    [TestCase("<meta name=color-scheme content=light><style>html{color-scheme:dark}</style>")]
    [TestCase("<meta name=color-scheme content=dark><style>html{color-scheme:light}html{color-scheme:dark}</style>")]
    public void ADarkRootPrintsOnTheDarkCanvasWithCanvasTextAndLinkText(string head)
        => Assert.That(Pdf(Doc(head)), Is.EqualTo(Pdf(Written)));

    [Test]
    public void TheHtmlStyleAttributeSetsTheScheme()
        => Assert.That(Pdf("<!doctype html><html style='color-scheme:dark'><head></head><body>" + Body + "</body></html>"), Is.EqualTo(Pdf(Written)));

    // Print prefers light, the CSS declaration beats the meta, and the first valid meta wins.
    [TestCase("<meta name=color-scheme content='light dark'>")]
    [TestCase("<meta name=color-scheme content='dark light'>")]
    [TestCase("<meta name=color-scheme content=light><meta name=color-scheme content=dark>")]
    [TestCase("<meta name=color-scheme content=dark><style>html{color-scheme:light}</style>")]
    [TestCase("<style>html{color-scheme:light dark}</style>")]
    [TestCase("<style>html{color-scheme:normal}</style>")]
    public void LightResolvingSchemesPrintLikeThePageWithoutThem(string head)
        => Assert.That(Pdf(Doc(head)), Is.EqualTo(Pdf(Doc(""))));

    [Test]
    public void TheDarkSchemeKeepsAuthorColoursAndAnExplicitCanvas()
    {
        const string author = "<p style='color:#c00'>Red <span style='color:#06c'>Blue</span></p>";
        Assert.That(Pdf(Doc("<meta name=color-scheme content=dark><style>html{background:#ffc}</style>", author)),
            Is.EqualTo(Pdf(Doc("<style>html{background:#ffc;color:#fff}</style>", author))));
        Assert.That(Pdf(Doc("<meta name=color-scheme content=dark>", author)), Is.Not.EqualTo(Pdf(Doc("", author))));
    }

    // A dark scheme anywhere but the root, or one the renderer cannot place, fails explicitly.
    [TestCase("<style>body{color-scheme:dark}</style>")]
    [TestCase("<style>div{color-scheme:dark}</style>")]
    [TestCase("<style>html.x{color-scheme:dark}</style>")]
    [TestCase("<style>html,body{color-scheme:dark}</style>")]
    [TestCase("<style>html{--s:dark;color-scheme:var(--s)}</style>")]
    public void ADarkSchemeAwayFromTheRootFails(string head)
        => Assert.That(Assert.Throws<FactsPdfException>(() => Pdf(Doc(head)))!.Code, Is.EqualTo("FPDF1202"));

    [Test]
    public void ADarkInlineSchemeOnAnotherElementFails()
        => Assert.That(Assert.Throws<FactsPdfException>(() => Pdf(Doc("", "<div style='color-scheme:dark'>A</div>")))!.Code, Is.EqualTo("FPDF1202"));

    [Test]
    public void ADarkAndAnInvalidMetaSchemeAreAmbiguousAndFail()
        => Assert.That(Assert.Throws<FactsPdfException>(() => Pdf(Doc("<meta name=color-scheme content=''><meta name=color-scheme content=dark>")))!.Code,
            Is.EqualTo("FPDF1103"));

    [Test]
    public void PageFurnitureCannotBeCombinedWithTheDarkScheme()
    {
        var html = Doc("<meta name=color-scheme content=dark>");
        var options = new PdfOptions { PageWidth = 260, PageHeight = 300, Margin = 36, PageNumbers = true };
        Assert.That(Assert.Throws<FactsPdfException>(() => Pdf(html, options))!.Code, Is.EqualTo("FPDF1206"));
        Assert.That(Assert.Throws<FactsPdfException>(() => Pdf(Doc("<style>html{color-scheme:dark}@page{@bottom-center{content:'x'}}</style>")))!.Code,
            Is.EqualTo("FPDF1206"));
        Assert.That(Pdf(Doc("<meta name=color-scheme content=light>"), options), Is.Not.Empty);
    }
}
