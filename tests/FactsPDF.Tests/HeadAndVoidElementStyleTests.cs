using NUnit.Framework;

namespace FactsPDF.Tests;

// Declarations that apply to nothing (or to nothing a printed page shows) are valid CSS and print as
// the page without them in Chrome 158, so they no longer fail: style on the display:none metadata
// elements of head and on br, list-style on a block container, vertical-align on a block box, and
// color:currentColor on an element with its own user-agent colour. What can change the print still
// fails explicitly (a display that would show head, a display:none br removes the break).
[TestFixture]
public sealed class HeadAndVoidElementStyleTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 260, PageHeight = 300, Margin = 12 };

    private static byte[] Pdf(string html)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, Page);
        return output.ToArray();
    }

    private static string Doc(string head, string body) => "<!doctype html><html><head>" + head + "</head><body>" + body + "</body></html>";

    private static FactsPdfException Fails(string html)
        => Assert.Throws<FactsPdfException>(() => Pdf(html))!;

    [TestCase("<meta name=x content=y style='color:red;margin:9pt'>")]
    [TestCase("<title style='display:none;font-size:30pt'>T</title>")]
    [TestCase("<link rel=icon href=x.png style='color:red'>")]
    [TestCase("<base href='https://example.com/' style='width:50pt'>")]
    [TestCase("<title style='display:block'>T</title>")]
    public void StyleOnAMetadataElementOfHeadChangesNothing(string head)
        => Assert.That(Pdf(Doc(head, "<p>A</p>")), Is.EqualTo(Pdf(Doc(head.Replace(" style=", " data-s=", StringComparison.Ordinal), "<p>A</p>"))));

    [Test]
    public void ABadDeclarationOnAMetadataElementStillFails()
        => Assert.That(Fails(Doc("<meta name=x content=y style='colr:red'>", "<p>A</p>")).Code, Is.EqualTo("FPDF1201"));

    [Test]
    public void ADisplayThatWouldShowHeadStillFails()
        => Assert.That(Fails("<!doctype html><html><head style='display:block'><title>T</title></head><body><p>A</p></body></html>").Code,
            Is.EqualTo("FPDF1103"));

    [TestCase("<title style='display:block'>T</title><p>A</p>")]
    [TestCase("<meta name=x content=y style='display:block'><p>A</p>")]
    public void ADisplayThatWouldShowABodyMetadataElementStillFails(string body)
        => Assert.That(Fails(Doc("", body)).Code, Is.EqualTo("FPDF1103"));

    // Chrome 158 breaks the line the same way whatever a br is styled with.
    [TestCase("font-size:40pt")]
    [TestCase("line-height:80pt;margin:9pt;padding:5pt;border:2pt solid red;background:#fdd")]
    [TestCase("color:red;white-space:pre;width:50pt;height:50pt;text-align:right;vertical-align:top")]
    [TestCase("display:block")]
    public void AStyledBreakStillBreaksTheLine(string style)
        => Assert.That(Pdf(Doc("", $"<p>Aa<br style='{style}'>Bb</p>")), Is.EqualTo(Pdf(Doc("", "<p>Aa<br>Bb</p>"))));

    [Test]
    public void ABreakStyledFromAStylesheetStillBreaksTheLine()
        => Assert.That(Pdf(Doc("<style>br{font-size:30pt;line-height:60pt;margin:4pt}</style>", "<p>Aa<br>Bb</p>")),
            Is.EqualTo(Pdf(Doc("", "<p>Aa<br>Bb</p>"))));

    // display:none removes the break (Chrome prints Aa and Bb on one line); a later display wins.
    [Test]
    public void ADisplayNoneBreakRemovesTheBreak()
    {
        Assert.That(Pdf(Doc("", "<p>Aa<br style='display:none'>Bb</p>")), Is.EqualTo(Pdf(Doc("", "<p>AaBb</p>"))));
        Assert.That(Pdf(Doc("<style>br{display:none}</style>", "<p>Aa<br>Bb</p>")), Is.EqualTo(Pdf(Doc("", "<p>AaBb</p>"))));
        Assert.That(Pdf(Doc("<style>br{display:none}</style>", "<p>Aa<br style='display:block'>Bb</p>")),
            Is.EqualTo(Pdf(Doc("", "<p>Aa<br>Bb</p>"))));
        Assert.That(Pdf(Doc("", "<p>Aa<br style='display:none'>Bb</p>")), Is.Not.EqualTo(Pdf(Doc("", "<p>Aa<br>Bb</p>"))));
    }

    // list-style is inherited: on a div it reaches the list below, whose own user-agent type wins
    // (Chrome prints discs for list-style-type:square on the div); the position is inherited.
    [Test]
    public void ListStyleTypeOnAContainerLeavesTheListsUserAgentType()
    {
        const string list = "<ul><li>Aa</li><li>Bb</li></ul>";
        Assert.That(Pdf(Doc("", $"<div style='list-style-type:square'>{list}</div>")), Is.EqualTo(Pdf(Doc("", $"<div>{list}</div>"))));
        Assert.That(Pdf(Doc("<style>body{list-style:none}</style>", list)), Is.EqualTo(Pdf(Doc("", list))));
    }

    [Test]
    public void ListStyleOnAContainerReachesAListThatInheritsIt()
    {
        const string list = "<ul style='list-style-type:inherit'><li>Aa</li></ul>";
        Assert.That(Pdf(Doc("", $"<div style='list-style-type:square'>{list}</div>")),
            Is.EqualTo(Pdf(Doc("", "<div><ul style='list-style-type:square'><li>Aa</li></ul></div>"))));
        Assert.That(Pdf(Doc("", "<div style='list-style-position:inside'><ul><li>Aa</li><li>Bb</li></ul></div>")),
            Is.EqualTo(Pdf(Doc("", "<div><ul style='list-style-position:inside'><li>Aa</li><li>Bb</li></ul></div>"))));
    }

    [Test]
    public void AnExplicitListTypeStillChangesTheMarkers()
        => Assert.That(Pdf(Doc("", "<ul style='list-style-type:square'><li>Aa</li></ul>")),
            Is.Not.EqualTo(Pdf(Doc("", "<ul><li>Aa</li></ul>"))));

    [Test]
    public void ListStyleOnInlineTextAndTablePartsIsHandledBySubject()
    {
        Assert.That(Pdf(Doc("<style>*{list-style:none}</style>", "<p>Aa<span>x</span></p>")),
            Is.EqualTo(Pdf(Doc("", "<p>Aa<span>x</span></p>"))));
        Assert.That(Fails(Doc("<style>td{list-style:none}</style>", "<table><tr><td>x</td></tr></table>")).Code, Is.EqualTo("FPDF1201"));
    }

    [TestCase("<p style='vertical-align:top'>Aa</p><div style='vertical-align:middle'>Bb</div>", "<p>Aa</p><div>Bb</div>")]
    [TestCase("<ul><li style='vertical-align:bottom'>Aa</li></ul>", "<ul><li>Aa</li></ul>")]
    [TestCase("<p style='vertical-align:10pt'>Aa</p><hr style='vertical-align:text-top'>", "<p>Aa</p><hr>")]
    public void VerticalAlignOnABlockBoxChangesNothing(string with, string without)
        => Assert.That(Pdf(Doc("", with)), Is.EqualTo(Pdf(Doc("", without))));

    [Test]
    public void VerticalAlignOnInlineTextStillFails()
        => Assert.That(Fails(Doc("", "<p>Aa <b style='vertical-align:middle'>Bb</b></p>")).Code, Is.EqualTo("FPDF1201"));

    // currentColor is the parent's colour, not the a element's own link blue.
    [Test]
    public void CurrentColorOnALinkInheritsTheParentColour()
    {
        static string Link(string color) => $"<p style='color:#c00'><a href='https://example.com/x' style='color:{color}'>L</a></p>";
        var current = Pdf(Doc("", Link("currentColor")));
        Assert.That(current, Is.EqualTo(Pdf(Doc("", Link("inherit")))));
        Assert.That(current, Is.Not.EqualTo(Pdf(Doc("", "<p style='color:#c00'><a href='https://example.com/x'>L</a></p>"))));
    }
}
