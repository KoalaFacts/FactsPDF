using NUnit.Framework;

namespace FactsPDF.Tests;

// time and data are inline text with no default style (R5bw). Their datetime and value attributes
// are machine-readable metadata: accepted on that element only and never drawn.
[TestFixture]
public sealed class TimeDataElementTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 260, PageHeight = 180, Margin = 12 };

    private static byte[] Pdf(string html)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, Page);
        return output.ToArray();
    }

    [TestCase("<time>25 Nov 2026</time>")]
    [TestCase("<time datetime='2026-11-25'>25 Nov 2026</time>")]
    [TestCase("<time datetime='2026-11-25T09:30:00+11:00' class=d>25 Nov 2026</time>")]
    [TestCase("<data value='SKU-4471'>25 Nov 2026</data>")]
    [TestCase("<data>25 Nov 2026</data>")]
    public void RendersLikeASpan(string element)
        => Assert.That(Pdf($"<p>Due {element}.</p>"), Is.EqualTo(Pdf("<p>Due <span>25 Nov 2026</span>.</p>")));

    [Test]
    public void AuthorCssAndTableCellsApply()
    {
        const string source = "<style>time{color:#c00;text-decoration:underline}data.n{font-size:9pt}</style>" +
            "<table style='width:140pt'><colgroup><col style='width:140pt'></colgroup><tbody><tr>" +
            "<td style='padding:3pt'>Due <time datetime='2026-11-25'>25 Nov</time> <data class=n value='7'>seven</data></td>" +
            "</tr></tbody></table>";
        const string oracle = "<table style='width:140pt'><colgroup><col style='width:140pt'></colgroup><tbody><tr>" +
            "<td style='padding:3pt'>Due <span style='color:#c00;text-decoration:underline'>25 Nov</span> " +
            "<span style='font-size:9pt'>seven</span></td></tr></tbody></table>";
        Assert.That(Pdf(source), Is.EqualTo(Pdf(oracle)));
    }

    [TestCase("<p><time value='1'>A</time></p>", "FPDF1103")]
    [TestCase("<p><data datetime='2026'>A</data></p>", "FPDF1103")]
    [TestCase("<p><span datetime='2026'>A</span></p>", "FPDF1103")]
    [TestCase("<p><time style='text-align:justify'>A</time></p>", "FPDF1201")]
    [TestCase("<time><div>A</div></time>", "FPDF1101")]
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
