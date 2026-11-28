using NUnit.Framework;

namespace FactsPDF.Tests;

// header, footer, main, nav and aside (R5by), hgroup and search (R5cd) are blocks whose only
// user-agent style is display:block, so they render exactly like div. Before, each failed FPDF1102.
[TestFixture]
public sealed class SectioningElementTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 260, PageHeight = 300, Margin = 12 };

    private static byte[] Pdf(string html)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, Page);
        return output.ToArray();
    }

    private const string Pin = "<style>h1{font-weight:normal}</style>";

    private static readonly string[] Names = ["header", "footer", "main", "nav", "aside", "hgroup", "search"];

    [TestCaseSource(nameof(Names))]
    public void RendersLikeADiv(string name)
        => Assert.That(Pdf($"{Pin}<{name}><h1>Invoice</h1><p>Due 25 Nov.</p></{name}><p>After</p>"),
            Is.EqualTo(Pdf(Pin + "<div><h1>Invoice</h1><p>Due 25 Nov.</p></div><p>After</p>")));

    [TestCaseSource(nameof(Names))]
    public void AuthorCssAppliesAsToADiv(string name)
    {
        var css = $"<style>{name}{{background-color:#eee;border:1pt solid #333;padding:4pt;margin:0 0 6pt 0}}" +
            $"{name}.n p{{color:#c00}}</style>";
        var oracle = "<style>div{background-color:#eee;border:1pt solid #333;padding:4pt;margin:0 0 6pt 0}" +
            "div.n p{color:#c00}</style>";
        Assert.That(Pdf($"{css}<{name} class=n id=x><p>Text</p></{name}>"),
            Is.EqualTo(Pdf($"{oracle}<div class=n id=x><p>Text</p></div>")));
    }

    [Test]
    public void APageLayoutNestsAndClosesAnOpenParagraph()
    {
        const string source = "<header><p>Acme<nav><ul><li>One</li></ul></nav></header>" +
            "<main><article><p>Body</p><aside>Note</aside></article></main><footer><p>Page</p></footer>";
        const string oracle = "<div><p>Acme<div><ul><li>One</li></ul></div></div>" +
            "<div><article><p>Body</p><div>Note</div></article></div><div><p>Page</p></div>";
        Assert.That(Pdf(source), Is.EqualTo(Pdf(oracle)));
    }

    [Test]
    public void AHeadingGroupAndSearchRegionRenderLikeDivs()
    {
        const string source = Pin + "<hgroup><h1>Quarterly report</h1><p>Q3 2026</p></hgroup>" +
            "<search><p>Filter: open invoices</p></search><p>Body</p>";
        const string oracle = Pin + "<div><h1>Quarterly report</h1><p>Q3 2026</p></div>" +
            "<div><p>Filter: open invoices</p></div><p>Body</p>";
        Assert.That(Pdf(source), Is.EqualTo(Pdf(oracle)));
    }

    [TestCase("<p><span><header>A</header></span></p>", "FPDF1101")]
    [TestCase("<footer>A</main>", "FPDF1101")]
    [TestCase("<nav href='x'>A</nav>", "FPDF1103")]
    [TestCase("<p><span><hgroup>A</hgroup></span></p>", "FPDF1101")]
    [TestCase("<search>A</hgroup>", "FPDF1101")]
    [TestCase("<hgroup href='x'>A</hgroup>", "FPDF1103")]
    [TestCase("<ruby>A</ruby>", "FPDF1102")]
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
