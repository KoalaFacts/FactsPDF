using NUnit.Framework;

namespace FactsPDF.Tests;

// address is a block whose only user-agent styles are display:block and font-style:italic, so it
// renders exactly like a div whose text is italic (R5ca). Before, it failed FPDF1102.
[TestFixture]
public sealed class AddressElementTests
{
    // Distinct fixture instances per role, so italic runs use a font object of their own.
    private static readonly PdfOptions Page = new()
    {
        PageWidth = 260, PageHeight = 200, Margin = 12,
        Fonts = [PdfFont.FromBytes(FontFixture.Create())], BoldFonts = [PdfFont.FromBytes(FontFixture.Create())],
        ItalicFonts = [PdfFont.FromBytes(FontFixture.Create())], BoldItalicFonts = [PdfFont.FromBytes(FontFixture.Create())],
    };

    private static byte[] Pdf(string html, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, options ?? Page);
        return output.ToArray();
    }

    [Test]
    public void AddressRendersLikeAnItalicDiv()
        => Assert.That(Pdf("<p>A</p><address>AB A<br>WA</address><p>B</p>"),
            Is.EqualTo(Pdf("<p>A</p><div><i>AB A<br>WA</i></div><p>B</p>")));

    [Test]
    public void AddressIsItalicNotPlain()
        => Assert.That(Pdf("<p>A</p><address>A</address>"), Is.Not.EqualTo(Pdf("<p>A</p><div>A</div>")));

    [Test]
    public void ParagraphsInsideInheritTheItalic()
        => Assert.That(Pdf("<address><p>AB <b>W</b> A</p></address>"),
            Is.EqualTo(Pdf("<div><p><i>AB <b>W</b> A</i></p></div>")));

    [Test]
    public void AuthorCssAppliesAsToADivAndCanResetTheItalic()
    {
        const string css = "<style>address{font-style:normal;border:1pt solid #333;padding:4pt;margin:0 0 6pt 0}" +
            "footer address.v{color:#c00}</style>";
        const string oracle = "<style>div.a{border:1pt solid #333;padding:4pt;margin:0 0 6pt 0}" +
            "footer div.v{color:#c00}</style>";
        Assert.That(Pdf($"{css}<footer><address class=v id=x>AB</address></footer>"),
            Is.EqualTo(Pdf($"{oracle}<footer><div class='a v' id=x>AB</div></footer>")));
    }

    [Test]
    public void WithoutAnItalicFontChainItFailsLikeEm()
    {
        var plain = new PdfOptions { PageWidth = 260, PageHeight = 200, Margin = 12 };
        using var a = new MemoryStream();
        using var b = new MemoryStream();
        var address = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert("<address>AB</address>", a, plain))!;
        var em = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert("<p><em>AB</em></p>", b, plain))!;
        Assert.Multiple(() =>
        {
            Assert.That(address.Code, Is.EqualTo(em.Code));
            Assert.That(a.Length, Is.Zero);
        });
    }

    [TestCase("<p><span><address>A</address></span></p>", "FPDF1101")]
    [TestCase("<address><b>A</address>B", "FPDF1101")]
    [TestCase("<address href='x'>A</address>", "FPDF1103")]
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
