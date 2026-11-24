using NUnit.Framework;

namespace FactsPDF.Tests;

// code, kbd, samp, var, cite and dfn are inline text elements (R5bv). The first three keep the
// supplied font (no generic monospace face, as for pre); the last three are italic, as the HTML
// rendering section's user-agent sheet makes them. Each renders exactly like the span spelling.
[TestFixture]
public sealed class PhrasingElementTests
{
    // Distinct fixture instances per role, so italic runs use a font object of their own.
    private static readonly PdfOptions Page = new()
    {
        PageWidth = 260, PageHeight = 180, Margin = 12,
        Fonts = [PdfFont.FromBytes(FontFixture.Create())], BoldFonts = [PdfFont.FromBytes(FontFixture.Create())],
        ItalicFonts = [PdfFont.FromBytes(FontFixture.Create())], BoldItalicFonts = [PdfFont.FromBytes(FontFixture.Create())],
    };

    private static byte[] Pdf(string html, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, options ?? Page);
        return output.ToArray();
    }

    private static void Reject(string html, string code)
    {
        using var output = new MemoryStream();
        var ex = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, Page))!;
        Assert.Multiple(() =>
        {
            Assert.That(ex.Code, Is.EqualTo(code));
            Assert.That(output.Length, Is.Zero);
        });
    }

    [TestCase("code", "")]
    [TestCase("kbd", "")]
    [TestCase("samp", "")]
    [TestCase("var", "font-style:italic")]
    [TestCase("cite", "font-style:italic")]
    [TestCase("dfn", "font-style:italic")]
    public void RendersLikeTheEquivalentSpan(string tag, string defaults)
    {
        var source = $"<p>A <{tag}>A A</{tag}> A</p>";
        var oracle = $"<p>A <span style='{defaults}'>A A</span> A</p>";
        Assert.That(Pdf(source), Is.EqualTo(Pdf(oracle)));
    }

    [Test]
    public void ItalicElementsDifferFromPlainText()
        => Assert.That(Pdf("<p>A <var>A</var></p>"), Is.Not.EqualTo(Pdf("<p>A <span>A</span></p>")));

    [Test]
    public void AuthorCssClassesAndInheritanceApply()
    {
        const string source = "<style>code{color:#c00;font-weight:bold}.k kbd{text-decoration:underline}" +
            "cite{font-style:normal}</style>" +
            "<p class=k>A <kbd>A</kbd> A <code>A <var>A</var></code> A <cite>A</cite></p>";
        const string oracle = "<p>A <span style='text-decoration:underline'>A</span> A " +
            "<span style='color:#c00;font-weight:bold'>A <span style='font-style:italic'>A</span></span> " +
            "A <span>A</span></p>";
        Assert.That(Pdf(source), Is.EqualTo(Pdf(oracle)));
    }

    [Test]
    public void WorksInPreHeadingsListsAndTableCells()
    {
        const string source = "<h2>A <code>A</code> A</h2><pre><code>A A\n  A</code></pre>" +
            "<ul><li>A <samp>A</samp></li></ul>" +
            "<table style='width:140pt'><colgroup><col style='width:140pt'></colgroup><tbody><tr>" +
            "<td style='padding:3pt'>A <code>A</code> <dfn>A</dfn></td></tr></tbody></table>";
        const string oracle = "<h2>A <span>A</span> A</h2><pre><span>A A\n  A</span></pre>" +
            "<ul><li>A <span>A</span></li></ul>" +
            "<table style='width:140pt'><colgroup><col style='width:140pt'></colgroup><tbody><tr>" +
            "<td style='padding:3pt'>A <span>A</span> <span style='font-style:italic'>A</span></td></tr></tbody></table>";
        Assert.That(Pdf(source), Is.EqualTo(Pdf(oracle)));
    }

    [TestCase("var")]
    [TestCase("cite")]
    [TestCase("dfn")]
    public void ItalicElementsNeedAnItalicFontChainLikeEm(string tag)
    {
        var standard = new PdfOptions { PageWidth = 260, PageHeight = 180, Margin = 12 };
        using var output = new MemoryStream();
        var ex = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert($"<p>A <{tag}>B</{tag}></p>", output, standard))!;
        Assert.That(ex.Code, Is.EqualTo("FPDF1507"));
        Assert.That(PdfConverter.Convert("<p>A <code>B</code> <kbd>C</kbd> <samp>D</samp></p>", new MemoryStream(), standard), Is.Not.Null);
    }

    [Test]
    public void ZeroInsetResetsAreAcceptedAsOnOtherInlineText()
        => Assert.That(Pdf("<style>code,kbd{margin:0;padding:0;background:transparent;box-sizing:border-box}</style>" +
            "<p>A <code>A</code> <kbd>A</kbd></p>"), Is.EqualTo(Pdf("<p>A <code>A</code> <kbd>A</kbd></p>")));

    [TestCase("<p>A <code style='text-align:justify'>A</code></p>", "FPDF1201")]
    [TestCase("<p>A <var style='text-align-last:center'>A</var></p>", "FPDF1201")]
    [TestCase("<p>A <code style='margin:2pt'>A</code></p>", "FPDF1201")]
    [TestCase("<code><div>Bad nesting</div></code>", "FPDF1101")]
    [TestCase("<p><kbd>Unclosed</p>", "FPDF1101")]
    [TestCase("<p><dfn datetime='x'>A</dfn></p>", "FPDF1103")]
    [TestCase("<p><mark>A</mark></p>", "FPDF1102")]
    public void UnsupportedUsesStillFail(string html, string code) => Reject(html, code);
}
