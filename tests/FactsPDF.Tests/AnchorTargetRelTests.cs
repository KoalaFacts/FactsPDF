using NUnit.Framework;

namespace FactsPDF.Tests;

// Email builders and CMS exports write <a href target="_blank" rel="noopener noreferrer">. A PDF link
// annotation always opens its URI outside the document, so target and the hyperlink relations change
// nothing (R5cn); before, either attribute failed FPDF1103.
[TestFixture]
public sealed class AnchorTargetRelTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 280, PageHeight = 170, Margin = 12 };

    private static byte[] Pdf(string html)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, Page);
        return output.ToArray();
    }

    private const string Plain = "<p>Pay <a href=\"https://example.com/pay\">online</a> today</p>";

    [TestCase("<p>Pay <a href=\"https://example.com/pay\" target=\"_blank\" rel=\"noopener noreferrer\">online</a> today</p>")]
    [TestCase("<p>Pay <a target=_BLANK href=\"https://example.com/pay\" REL=NoFollow>online</a> today</p>")]
    [TestCase("<p>Pay <a href=\"https://example.com/pay\" target=_self rel=' external\tsponsored ugc '>online</a> today</p>")]
    [TestCase("<p>Pay <a href=\"https://example.com/pay\" target=_top>online</a> today</p>")]
    [TestCase("<p>Pay <a href=\"https://example.com/pay\" target=_parent>online</a> today</p>")]
    [TestCase("<p>Pay <a href=\"https://example.com/pay\" target=payments>online</a> today</p>")]
    [TestCase("<p>Pay <a href=\"https://example.com/pay\" rel=\"privacy-policy terms-of-service bookmark tag\">online</a> today</p>")]
    public void TargetAndRelRenderAsIfAbsent(string html)
        => Assert.That(Pdf(html), Is.EqualTo(Pdf(Plain)));

    [TestCase("<p><a href=\"https://example.com\" target=\"\">x</a></p>")]
    [TestCase("<p><a href=\"https://example.com\" target=_new>x</a></p>")]
    [TestCase("<p><a href=\"https://example.com\" rel=\"\">x</a></p>")]
    [TestCase("<p><a href=\"https://example.com\" rel=\"noopener stylesheet\">x</a></p>")]
    [TestCase("<p><a href=\"https://example.com\" rel=icon>x</a></p>")]
    [TestCase("<p><span target=_blank>x</span></p>")]
    [TestCase("<p target=_blank>x</p>")]
    public void OtherTargetsAndRelationsFail(string html)
    {
        using var output = new MemoryStream();
        var ex = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, Page))!;
        Assert.Multiple(() =>
        {
            Assert.That(ex.Code, Is.EqualTo("FPDF1103"));
            Assert.That(output.Length, Is.Zero);
        });
    }

    [Test]
    public void ALinkWithoutHrefStillFails()
    {
        using var output = new MemoryStream();
        var ex = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert("<p><a target=_blank rel=noopener>x</a></p>", output, Page))!;
        Assert.That(ex.Code, Is.EqualTo("FPDF1103"));
    }
}
