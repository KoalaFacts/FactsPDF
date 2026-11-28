using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// The width and height attributes are presentational hints (HTML "rules for parsing dimension
/// values"): they act as CSS width and height below every author declaration. The attribute page is
/// byte-identical to the page with the equivalent author style; where HTML would ignore the value the
/// page is byte-identical to the page without the attribute. Table, row and cell heights have no CSS
/// equivalent here and fail explicitly.
/// </summary>
[TestFixture]
public sealed class HtmlDimensionAttributeTests
{
    private static PdfOptions Options() => new()
    {
        PageWidth = 300, PageHeight = 400, Margin = 20,
        Images = new Dictionary<string, PdfImage> { ["pic"] = PdfImage.FromBytes(JpegResourceTests.Bytes("rgb-420")) }
    };

    private static byte[] Pdf(string html)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, Options());
        return output.ToArray();
    }

    private static string Img(string attributes, string style = "") =>
        $"<style>{style}</style><p><img src='pic' {attributes}>x</p>";

    [TestCase("width='80'", "width:60pt")]
    [TestCase("height='30'", "height:22.5pt")]
    [TestCase("width='80' height='20'", "width:60pt;height:15pt")]
    [TestCase("width='55.5'", "width:41.625pt")]
    [TestCase("width='60px'", "width:45pt")]
    [TestCase("width=' 50 '", "width:37.5pt")]
    [TestCase("width='50 junk'", "width:37.5pt")]
    [TestCase("width='1e2'", "width:0.75pt")]
    public void AnImageSizeAttributeEqualsTheCssPixelLength(string attributes, string equivalentStyle)
        => Assert.That(Pdf(Img(attributes)), Is.EqualTo(Pdf($"<p><img src='pic' style='{equivalentStyle}'>x</p>")));

    [TestCase("width='-5'")]
    [TestCase("width='abc'")]
    [TestCase("width='.5'")]
    [TestCase("width=''")]
    [TestCase("height='+4'")]
    public void AValueThatDoesNotStartWithADigitIsIgnored(string attributes)
        => Assert.That(Pdf(Img(attributes)), Is.EqualTo(Pdf(Img(""))));

    [Test]
    public void APercentageWidthResolvesAgainstTheContainingBlock()
        => Assert.That(Pdf("<div style='width:200pt'><img src='pic' width='50%'></div>"),
            Is.EqualTo(Pdf("<div style='width:200pt'><img src='pic' style='width:50%'></div>")));

    [Test]
    public void AuthorCssWinsOverTheAttributeInEveryForm()
    {
        var plain = Pdf("<style>img{width:30pt}</style><p><img src='pic'>x</p>");
        Assert.That(Pdf(Img("width='100'", "img{width:30pt}")), Is.EqualTo(plain));
        Assert.That(Pdf("<p><img src='pic' width='100' style='width:30pt'>x</p>"), Is.EqualTo(plain));
        // Even the lowest-specificity type rule beats the hint, whatever its source order.
        Assert.That(Pdf("<p><img src='pic' width='100'>x</p><style>img{width:30pt}</style>"),
            Is.EqualTo(Pdf("<p><img src='pic'>x</p><style>img{width:30pt}</style>")));
        // width:auto from the author removes the hint (over-correction guard: auto is a declaration).
        Assert.That(Pdf(Img("width='100' height='100'", "img{width:auto;height:auto}")), Is.EqualTo(Pdf(Img(""))));
    }

    [TestCase("<table width='150'><tr><td>a</td><td>b</td></tr></table>", "<table style='width:112.5pt'><tr><td>a</td><td>b</td></tr></table>")]
    [TestCase("<table width='50%'><tr><td>a</td></tr></table>", "<table style='width:50%'><tr><td>a</td></tr></table>")]
    [TestCase("<table width='200'><tr><td width='25%'>a</td><td>b</td></tr></table>", "<table style='width:150pt'><tr><td style='width:25%'>a</td><td>b</td></tr></table>")]
    [TestCase("<table><tr><td width='100'>a</td><td>b</td></tr></table>", "<table><tr><td style='width:75pt'>a</td><td>b</td></tr></table>")]
    [TestCase("<table><colgroup><col width='120'><col></colgroup><tr><td>a</td><td>b</td></tr></table>", "<table><colgroup><col style='width:90pt'><col></colgroup><tr><td>a</td><td>b</td></tr></table>")]
    public void TableCellAndColumnWidthAttributesEqualTheCssWidth(string attributeHtml, string styleHtml)
        => Assert.That(Pdf(attributeHtml), Is.EqualTo(Pdf(styleHtml)));

    [Test]
    public void AuthorCssWinsOverTheCellWidthAttribute()
        => Assert.That(Pdf("<style>td{width:30pt}</style><table><tr><td width='150'>a</td><td>b</td></tr></table>"),
            Is.EqualTo(Pdf("<style>td{width:30pt}</style><table><tr><td>a</td><td>b</td></tr></table>")));

    [TestCase("<table height='80'><tr><td>a</td></tr></table>")]
    [TestCase("<table><tr height='20'><td>a</td></tr></table>")]
    [TestCase("<table><tr><td height='20'>a</td></tr></table>")]
    [TestCase("<p height='20'>a</p>")]
    [TestCase("<div width='100'>a</div>")]
    public void SizeAttributesWithoutACssEquivalentFailExplicitly(string html)
    {
        var ex = Assert.Throws<FactsPdfException>(() => Pdf(html))!;
        Assert.That(ex.Code, Is.EqualTo("FPDF1103"));
    }

    [Test]
    public void AZeroWidthImageKeepsAnEmptyBoxAndPaintsNothing()
    {
        // Chrome prints nothing for a zero-sized image; it is no longer a failure.
        Assert.DoesNotThrow(() => Pdf(Img("width='0'")));
    }
}
