using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// Inline text (span, b, strong, i, em, small, u, s, del, ins, a) lays out no box of its own, so
/// a zero margin or padding and a transparent background (normalize.css
/// a{background-color:transparent}, the classic *{margin:0;padding:0} on inline text) render
/// byte-identical to the page without them. A value that would move or paint the text still fails
/// with FPDF1201 and writes nothing.
/// </summary>
[TestFixture]
public sealed class InlineZeroBoxResetTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 400, Margin = 20 };

    private const string Content =
        "<h2 style='font-weight:normal'>Invoice</h2><p>Paid <span>in</span> <a href='https://example.com'>full</a>, " +
        "<small>see</small> <u>below</u> <s>old</s> <del>x</del> <ins>y</ins></p>" +
        "<div style='background-color:#eee'><p>Total <span>42</span></p></div>";

    private static byte[] Pdf(string html)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, Page);
        return output.ToArray();
    }

    private static string Doc(string css) =>
        $"<!doctype html><html><head><style>{css}</style></head><body>{Content}</body></html>";

    private const string Inline = "span,a,small,u,s,del,ins";

    [TestCase("a{background-color:transparent}")]
    [TestCase("a{background:transparent}")]
    [TestCase("a{background:none}")]
    [TestCase("A{BACKGROUND-COLOR:TRANSPARENT!important}")]
    [TestCase(Inline + "{margin:0;padding:0}")]
    [TestCase(Inline + "{margin:0px 0pt 0mm 0%;padding:0em 0rem}")]
    [TestCase(Inline + "{margin-inline:0;margin-block:0;padding-inline:0;padding-block:0}")]
    [TestCase(Inline + "{margin:initial;padding:unset;background-color:initial}")]
    [TestCase("span{background:unset;padding:-0pt}")]
    public void ZeroInlineBoxRendersLikeTheUndeclaredPage(string css)
    {
        Assert.That(Pdf(Doc(css)), Is.EqualTo(Pdf(Doc(""))), css);
    }

    [Test]
    public void InlineStyleAttributeIsAccepted()
    {
        var reset = Doc("").Replace("<span>in</span>", "<span style='margin:0;padding:0;background:none'>in</span>");
        var plain = Doc("");
        Assert.That(Pdf(reset), Is.EqualTo(Pdf(plain.Replace("<span>in</span>", "<span style=''>in</span>"))));
    }

    [Test]
    public void ARealInlineColourStillPaintsAgainstTheReset()
    {
        // The dropped reset must not hide a later real declaration on another property.
        Assert.That(Pdf(Doc("a{background-color:transparent;color:#c00}")), Is.EqualTo(Pdf(Doc("a{color:#c00}"))));
    }

    [TestCase("span{border-radius:3pt}", "3pt")]
    [TestCase("a{outline:1pt solid red}", "1pt")]
    public void InlineBoxValuesThatWouldRenderStillFailWithoutOutput(string css, string at)
    {
        var html = Doc(css);
        using var output = new MemoryStream();
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, Page));
        Assert.That(error!.Code, Is.EqualTo("FPDF1201"), css);
        Assert.That(output.Length, Is.Zero);
        Assert.That(error.SourceOffset, Is.EqualTo(html.IndexOf(at, StringComparison.Ordinal)), css);
    }

    [Test]
    public void BoldAndItalicTextAcceptTheResetToo()
    {
        var font = PdfFont.FromBytes(FontFixture.Create());
        var options = new PdfOptions
        {
            PageWidth = 300, PageHeight = 400, Margin = 20,
            Fonts = [font], BoldFonts = [font], ItalicFonts = [font], BoldItalicFonts = [font]
        };
        const string body = "<p>A <b>B</b> <strong>W</strong> <i>i</i> <em>A</em></p>";
        static byte[] Render(string html, PdfOptions o)
        {
            using var output = new MemoryStream();
            PdfConverter.Convert(html, output, o);
            return output.ToArray();
        }
        Assert.That(Render($"<style>b,strong,i,em{{margin:0;padding:0;background:none}}</style>{body}", options),
            Is.EqualTo(Render(body, options)));
    }

    [Test]
    public void TheSdkAcceptsTheReset()
    {
        var reset = FactsPDF.Pdf.Write().ToBytes(Doc("a{background-color:transparent}" + Inline + "{margin:0;padding:0}"));
        Assert.That(reset, Is.EqualTo(FactsPDF.Pdf.Write().ToBytes(Doc(""))));
    }
}
