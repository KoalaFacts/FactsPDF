using NUnit.Framework;

namespace FactsPDF.Tests;

// dir="ltr" names the only direction FactsPDF lays out, so it is accepted on any element and
// changes nothing (R5ci). Email and document builders put it on html, body, tables and
// paragraphs. Before, it failed FPDF1103; rtl and auto still do.
[TestFixture]
public sealed class DirLtrAttributeTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 260, PageHeight = 300, Margin = 12 };

    private static byte[] Pdf(string html)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, Page);
        return output.ToArray();
    }

    [TestCase("<html dir=\"ltr\"><body dir=ltr><p dir='LTR'>Invoice 42</p></body></html>",
        "<html><body><p>Invoice 42</p></body></html>")]
    [TestCase("<div dir=ltr><ul dir=ltr><li dir=ltr>Item <span dir=ltr>one</span></li></ul></div>",
        "<div><ul><li>Item <span>one</span></li></ul></div>")]
    [TestCase("<table dir=ltr><colgroup><col style='width:236pt'></colgroup><tbody dir=ltr><tr dir=ltr><td dir=ltr>Cell</td></tr></tbody></table>",
        "<table><colgroup><col style='width:236pt'></colgroup><tbody><tr><td>Cell</td></tr></tbody></table>")]
    [TestCase("<p>Name: <bdi dir=ltr>Acme</bdi></p>", "<p>Name: <bdi>Acme</bdi></p>")]
    public void LeftToRightRendersAsIfAbsent(string html, string without)
        => Assert.That(Pdf(html), Is.EqualTo(Pdf(without)));

    [TestCase("<p dir=rtl>A</p>")]
    [TestCase("<p dir=auto>A</p>")]
    [TestCase("<p dir>A</p>")]
    [TestCase("<html dir=rtl><p>A</p></html>")]
    public void OtherDirectionsStillFail(string html)
    {
        using var output = new MemoryStream();
        var ex = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, Page))!;
        Assert.Multiple(() =>
        {
            Assert.That(ex.Code, Is.EqualTo("FPDF1103"));
            Assert.That(output.Length, Is.Zero);
        });
    }
}
