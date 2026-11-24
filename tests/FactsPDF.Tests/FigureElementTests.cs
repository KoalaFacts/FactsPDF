using NUnit.Framework;

namespace FactsPDF.Tests;

// figure renders like blockquote (the HTML user-agent sheet gives both margin 1em 40px) and
// figcaption like div (display:block only) (R5bz). Before, each failed FPDF1102.
[TestFixture]
public sealed class FigureElementTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 260, PageHeight = 300, Margin = 12 };

    private static byte[] Pdf(string html, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, options ?? Page);
        return output.ToArray();
    }

    [Test]
    public void FigureRendersLikeABlockquote()
        => Assert.That(Pdf("<p>Before</p><figure><p>Revenue grew 12%.</p></figure><p>After</p>"),
            Is.EqualTo(Pdf("<p>Before</p><blockquote><p>Revenue grew 12%.</p></blockquote><p>After</p>")));

    [Test]
    public void FigureDiffersFromADivByItsMargins()
        => Assert.That(Pdf("<figure><p>Chart</p></figure>"), Is.Not.EqualTo(Pdf("<div><p>Chart</p></div>")));

    [Test]
    public void FigcaptionRendersLikeADiv()
        => Assert.That(Pdf("<figure><p>Chart</p><figcaption>Figure 1. Revenue by quarter.</figcaption></figure>"),
            Is.EqualTo(Pdf("<blockquote><p>Chart</p><div>Figure 1. Revenue by quarter.</div></blockquote>")));

    [Test]
    public void AnImageWithACaptionRendersLikeTheBlockquoteOracle()
    {
        var options = new PdfOptions
        {
            PageWidth = 260, PageHeight = 300, Margin = 12,
            Images = new Dictionary<string, PdfImage> { ["chart"] = PdfImage.FromBytes(JpegResourceTests.Bytes("rgb-420")) }
        };
        Assert.That(Pdf("<figure><img src='chart' style='width:120pt'><figcaption>Figure 1</figcaption></figure>", options),
            Is.EqualTo(Pdf("<blockquote><img src='chart' style='width:120pt'><div>Figure 1</div></blockquote>", options)));
    }

    [Test]
    public void AuthorCssAppliesAsToTheOracles()
    {
        const string css = "<style>figure{margin:0 0 6pt 0;border:1pt solid #333;padding:4pt}" +
            "figcaption{color:#555;margin-top:2pt}figure.chart figcaption{color:#c00}</style>";
        const string oracle = "<style>blockquote{margin:0 0 6pt 0;border:1pt solid #333;padding:4pt}" +
            "div{color:#555;margin-top:2pt}blockquote.chart div{color:#c00}</style>";
        Assert.That(Pdf($"{css}<figure class=chart id=f1><p>Q1 120</p><figcaption>Revenue</figcaption></figure>"),
            Is.EqualTo(Pdf($"{oracle}<blockquote class=chart id=f1><p>Q1 120</p><div>Revenue</div></blockquote>")));
    }

    [Test]
    public void AFigcaptionClosesAnOpenParagraph()
        => Assert.That(Pdf("<figure><p>Chart<figcaption>Caption</figcaption></figure>"),
            Is.EqualTo(Pdf("<blockquote><p>Chart<div>Caption</div></blockquote>")));

    [Test]
    public void AFigcaptionInAListItemRendersLikeADiv()
        => Assert.That(Pdf("<ul><li><figcaption>Caption</figcaption></li></ul>"),
            Is.EqualTo(Pdf("<ul><li><div>Caption</div></li></ul>")));

    [Test]
    public void ALongFigurePaginatesLikeABlockquote()
    {
        var body = string.Concat(Enumerable.Repeat("<p>Quarterly revenue line.</p>", 60));
        var figure = Pdf($"<figure>{body}<figcaption>End</figcaption></figure>");
        Assert.Multiple(() =>
        {
            Assert.That(System.Text.RegularExpressions.Regex.Count(
                System.Text.Encoding.Latin1.GetString(figure), @"/Type\s*/Page\b"), Is.GreaterThan(1));
            Assert.That(figure, Is.EqualTo(Pdf($"<blockquote>{body}<div>End</div></blockquote>")));
        });
    }

    [TestCase("<ul><li><figure>A</figure></li></ul>")]
    [TestCase("<ol><li><p>x</p><figure>A</figure></li></ol>")]
    public void AFigureInAListFailsWithItsOwnMessage(string html)
    {
        using var output = new MemoryStream();
        var ex = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, Page))!;
        Assert.Multiple(() =>
        {
            Assert.That(ex.Code, Is.EqualTo("FPDF1101"));
            Assert.That(ex.Message, Does.Contain("figure must be outside lists and tables."));
            Assert.That(output.Length, Is.Zero);
        });
    }

    [TestCase("<table><tr><td><figure>A</figure></td></tr></table>", "FPDF1101")]
    [TestCase("<p><span><figcaption>A</figcaption></span></p>", "FPDF1101")]
    [TestCase("<figure>A</figcaption>", "FPDF1101")]
    [TestCase("<figure href='x'>A</figure>", "FPDF1103")]
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
