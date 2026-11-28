using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

// The first title element of the page is the PDF Title, as in Chrome's print output (document.title:
// character references decoded, control characters read as spaces, white space collapsed and
// stripped), unless the caller sets Title. A page without a usable title has no /Title (Chrome
// then writes the source file name, which FactsPDF never sees). title is RCDATA, so markup in it is
// text, and a title in body or inside svg follows the same document rules as in a browser.
[TestFixture]
public sealed class TitleElementTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 260, PageHeight = 300, Margin = 12 };

    private static byte[] Pdf(string html, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, options ?? Page);
        return output.ToArray();
    }

    // The decoded /Title of the Info dictionary, or null without one.
    private static string? TitleOf(byte[] pdf)
    {
        var text = Encoding.Latin1.GetString(pdf);
        var at = text.IndexOf("/Title <FEFF", StringComparison.Ordinal);
        if (at < 0) return null;
        var start = at + "/Title <".Length;
        var hex = text[start..text.IndexOf('>', start)];
        return Encoding.BigEndianUnicode.GetString(Convert.FromHexString(hex)[2..]);
    }

    private static string Doc(string head, string body = "<p>Hi</p>") => "<!doctype html><html><head>" + head + "</head><body>" + body + "</body></html>";

    // Expected values are what Chrome 158 writes for each title.
    [TestCase("<title>Hello World</title>", "Hello World")]
    [TestCase("<title>  Hello \n\t  World  </title>", "Hello World")]
    [TestCase("<title>A &amp; B &lt;c&gt; &#x4e2d; &nbsp;x&nbsp;</title>", "A & B <c> 中  x")]
    [TestCase("<title>a<b>bold</b>c</title>", "a<b>bold</b>c")]
    [TestCase("<title>Ti<script>x</script>tle</title>", "Ti<script>x</script>tle")]
    [TestCase("<title>a&#1;&#1;b</title>", "a b")]
    [TestCase("<title>a&#x7f;b&#x1f;c</title>", "a b c")]
    [TestCase("<title>&#11;a&#11;</title>", "a")]
    [TestCase("<title>a   b</title>", "a   b")]
    [TestCase("<title>a&#x1F600;b</title>", "a\U0001F600b")]
    [TestCase("<title>a&#x85;b</title>", "a…b")]
    [TestCase("<TITLE>Up</TITLE >", "Up")]
    [TestCase("<title/>Slash</title>", "Slash")]
    [TestCase("<title>One</title><title>Two</title>", "One")]
    [TestCase("<meta name=x content=y><title>Late</title>", "Late")]
    public void TheTitleElementIsThePdfTitle(string head, string expected)
        => Assert.That(TitleOf(Pdf(Doc(head))), Is.EqualTo(expected));

    [Test]
    public void ALongTitleIsKeptWhole()
        => Assert.That(TitleOf(Pdf(Doc("<title>" + new string('x', 1200) + "</title>"))), Has.Length.EqualTo(1200));

    [TestCase("")]
    [TestCase("<title></title>")]
    [TestCase("<title> \n </title>")]
    [TestCase("<title>&nbsp;</title>")]
    public void ABlankOrMissingTitleWritesNoTitle(string head)
        => Assert.That(TitleOf(Pdf(Doc(head))), Is.Null);

    [Test]
    public void ABlankFirstTitleDoesNotFallThroughToALaterOne()
        => Assert.That(TitleOf(Pdf(Doc("<title> </title><title>Two</title>"))), Is.Null);

    [Test]
    public void AnExplicitTitleOptionWins()
    {
        var options = new PdfOptions { PageWidth = 260, PageHeight = 300, Margin = 12, Title = "Chosen" };
        Assert.That(TitleOf(Pdf(Doc("<title>From HTML</title>"), options)), Is.EqualTo("Chosen"));
    }

    [Test]
    public void TheTitleIsTheOnlyDifferenceFromAPageWithoutIt()
    {
        var with = Pdf(Doc("<title>Report</title>"));
        var without = Pdf(Doc(""), new PdfOptions { PageWidth = 260, PageHeight = 300, Margin = 12, Title = "Report" });
        Assert.That(with, Is.EqualTo(without));
        Assert.That(Pdf(Doc("<title>Report</title>")).Length, Is.GreaterThan(Pdf(Doc("")).Length));
    }

    // A title the parser leaves in body is still the document's title when it is the first one;
    // an svg title is the graphic's own element.
    [TestCase("<p>Hi</p><title>InBody &amp; x</title>", "InBody & x")]
    [TestCase("<p>Hi<title>InP</title>more</p>", "InP")]
    [TestCase("<div><title>InDiv</title><p>x</p></div>", "InDiv")]
    [TestCase("<table><tr><td>x<title>InTd</title></td></tr></table>", "InTd")]
    public void ATitleInBodyIsTheTitleWhenTheHeadHasNone(string body, string expected)
        => Assert.That(TitleOf(Pdf(Doc("", body))), Is.EqualTo(expected));

    [Test]
    public void TheHeadTitleBeatsALaterBodyTitle()
        => Assert.That(TitleOf(Pdf(Doc("<title>Head</title>", "<p>Hi</p><title>Body</title>"))), Is.EqualTo("Head"));

    [Test]
    public void ABodyTitleRendersNothing()
        => Assert.That(Pdf(Doc("", "<p>A</p><title>Hidden</title><p>B</p>")),
            Is.EqualTo(Pdf(Doc("", "<p>A</p><p>B</p>"), new PdfOptions { PageWidth = 260, PageHeight = 300, Margin = 12, Title = "Hidden" })));

    [Test]
    public void AnSvgTitleIsNotTheDocumentTitle()
    {
        var svg = "<svg width=20 height=20><title>Graphic</title><rect width=10 height=10 /></svg><p>x</p>";
        Assert.That(TitleOf(Pdf(Doc("", svg))), Is.Null);
        Assert.That(TitleOf(Pdf(Doc("<title>Head</title>", svg))), Is.EqualTo("Head"));
    }

    [Test]
    public void ATitleInsideATemplateIsInert()
        => Assert.That(TitleOf(Pdf(Doc("", "<template><title>T</title></template><p>x</p>"))), Is.Null);

    [Test]
    public void AnUnclosedTitleFails()
    {
        var ex = Assert.Throws<FactsPdfException>(() => Pdf("<head><title>Never closed<p>x</p>"))!;
        Assert.That(ex.Code, Is.EqualTo("FPDF1101"));
    }

    [Test]
    public void ABodyTitleStillValidatesItsStyleAttribute()
    {
        var ex = Assert.Throws<FactsPdfException>(() => Pdf(Doc("", "<title style='colr:red'>T</title><p>x</p>")))!;
        Assert.That(ex.Code, Is.EqualTo("FPDF1201"));
    }
}
