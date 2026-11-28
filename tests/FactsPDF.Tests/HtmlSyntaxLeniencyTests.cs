using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// Two things the HTML parser ignores. The trailing slash of a start tag that is neither a void element
/// nor foreign content (<c>&lt;div/&gt;</c> opens a div that a later end tag closes), and event handler
/// content attributes (on*), which only run script on an event a printed page never receives and
/// which FactsPDF never runs (like script elements). Every page is byte-identical to the page without
/// the slash or the attribute; a void element and svg keep their self-closing forms.
/// </summary>
[TestFixture]
public sealed class HtmlSyntaxLeniencyTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 400, Margin = 20 };

    private static byte[] Pdf(string html)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, Page);
        return output.ToArray();
    }

    [TestCase("<div/>A</div><div>B</div>", "<div>A</div><div>B</div>")]
    [TestCase("<div style='border:1pt solid #000'>A<div/>B</div>C</div>", "<div style='border:1pt solid #000'>A<div>B</div>C</div>")]
    [TestCase("<p/>A<p>B</p>", "<p>A<p>B</p>")]
    [TestCase("<p>A<span/>B</span> C</p>", "<p>A<span>B</span> C</p>")]
    [TestCase("<ul><li/>A</li><li>B</li></ul>", "<ul><li>A</li><li>B</li></ul>")]
    [TestCase("<table><tr><td/>A</td></tr></table>", "<table><tr><td>A</td></tr></table>")]
    [TestCase("<h1/>A</h1>", "<h1>A</h1>")]
    public void ASlashOnANonVoidStartTagIsIgnored(string html, string withoutSlash)
        => Assert.That(Pdf(html), Is.EqualTo(Pdf(withoutSlash)));

    [Test]
    public void ADivOpenedWithASlashStillNeedsItsEndTag()
    {
        // The element stays open, as in the HTML parser: the page is incomplete exactly like <div>A.
        var ex = Assert.Throws<FactsPdfException>(() => Pdf("<div/>A"))!;
        Assert.That(ex.Code, Is.EqualTo("FPDF1101"));
        Assert.That(Assert.Throws<FactsPdfException>(() => Pdf("<div>A"))!.Code, Is.EqualTo("FPDF1101"));
    }

    [Test]
    public void VoidElementsAndSvgKeepTheirSelfClosingForms()
    {
        Assert.That(Pdf("<p>A<br/>B</p>"), Is.EqualTo(Pdf("<p>A<br>B</p>")));
        Assert.That(Pdf("<hr/><p>A</p>"), Is.EqualTo(Pdf("<hr><p>A</p>")));
        const string closed = "<svg width='20' height='20'><rect width='5' height='5'/></svg>";
        Assert.That(Pdf("<p>x</p>" + closed), Is.EqualTo(Pdf("<p>x</p><svg width='20' height='20'><rect width='5' height='5'></rect></svg>")));
        // An svg element itself may close in one tag.
        Assert.DoesNotThrow(() => Pdf("<p>x</p><svg width='20' height='20'/>"));
    }

    [Test]
    public void ASelfClosedStyleElementStillReadsItsContentAsCss()
    {
        // <style/> is a start tag: its raw text runs to </style>, as in the HTML parser.
        Assert.That(Pdf("<style/>p{color:#c00}</style><p>A</p>"), Is.EqualTo(Pdf("<style>p{color:#c00}</style><p>A</p>")));
    }

    [TestCase("<p onclick='x()'>A</p>", "<p>A</p>")]
    [TestCase("<p ONCLICK=\"alert(1)\" onmouseover=x>A</p>", "<p>A</p>")]
    [TestCase("<div onload='y' onfoo>A</div>", "<div>A</div>")]
    [TestCase("<body onload='init()'><p>A</p></body>", "<body><p>A</p></body>")]
    [TestCase("<table onclick='x'><tr onclick='x'><td onclick='x'>A</td></tr></table>", "<table><tr><td>A</td></tr></table>")]
    [TestCase("<ul onclick='x'><li onclick='x'>A</li></ul>", "<ul><li>A</li></ul>")]
    [TestCase("<p><a href='https://example.com' onclick='x'>A</a></p>", "<p><a href='https://example.com'>A</a></p>")]
    [TestCase("<style onload='x'>p{color:#c00}</style><p>A</p>", "<style>p{color:#c00}</style><p>A</p>")]
    [TestCase("<p>x</p><svg width='20' height='20' onclick='x'><rect width='5' height='5' onmouseover='y'/></svg>",
        "<p>x</p><svg width='20' height='20'><rect width='5' height='5'/></svg>")]
    public void EventHandlerAttributesChangeNothing(string html, string withoutAttribute)
        => Assert.That(Pdf(html), Is.EqualTo(Pdf(withoutAttribute)));

    [TestCase("<p o='x'>A</p>")]
    [TestCase("<p on>A</p>")]
    [TestCase("<p once='x'>A</p>")]
    [TestCase("<p onclick>A</p>")]
    public void OnlyNamesStartingWithOnAndAtLeastOneMoreLetterAreEventHandlers(string html)
    {
        // "on" alone and names that do not start with it stay unsupported; every longer name is a handler.
        var handler = html.Contains("once", StringComparison.Ordinal) || html.Contains("onclick", StringComparison.Ordinal);
        if (handler) Assert.DoesNotThrow(() => Pdf(html));
        else Assert.That(Assert.Throws<FactsPdfException>(() => Pdf(html))!.Code, Is.EqualTo("FPDF1103"));
    }
}
