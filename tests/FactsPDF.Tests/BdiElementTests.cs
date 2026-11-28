using NUnit.Framework;

namespace FactsPDF.Tests;

// bdi only isolates the direction of its text; FactsPDF lays every run out left to right, so it
// renders exactly like span (R5cg). Before, it failed FPDF1102.
[TestFixture]
public sealed class BdiElementTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 260, PageHeight = 300, Margin = 12 };

    private static byte[] Pdf(string html)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, Page);
        return output.ToArray();
    }

    [TestCase("<p>Customer <bdi>Acme Pty Ltd</bdi>: 3 orders</p>")]
    [TestCase("<ul><li>User <bdi class=n id=u lang=en>jo</bdi> posted</li></ul>")]
    [TestCase("<bdi>Anonymous</bdi> text")]
    [TestCase("<style>bdi{color:#c00}bdi.n{text-decoration:underline}</style><p>A <bdi class=n>B</bdi></p>")]
    public void RendersLikeASpan(string html)
        => Assert.That(Pdf(html), Is.EqualTo(Pdf(html.Replace("bdi", "span"))));

    [TestCase("<bdi><p>Block</p></bdi>", "FPDF1101")]
    [TestCase("<bdi dir='rtl'>A</bdi>", "FPDF1103")]
    [TestCase("<marquee>A</marquee>", "FPDF1102")]
    public void UnsupportedUsesStillFail(string html, string code)
    {
        using var output = new MemoryStream();
        var ex = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, Page))!;
        Assert.Multiple(() =>
        {
            Assert.That(ex.Code, Is.EqualTo(code));
            Assert.That(output.Length, Is.Zero);
        });
    }
}
