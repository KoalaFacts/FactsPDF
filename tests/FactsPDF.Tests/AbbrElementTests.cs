using NUnit.Framework;

namespace FactsPDF.Tests;

// abbr is inline text (R5bx). With a title it gets the HTML user-agent sheet's dotted underline;
// without one it has no default style. The title itself is never drawn.
[TestFixture]
public sealed class AbbrElementTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 260, PageHeight = 180, Margin = 12 };

    private static byte[] Pdf(string html)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, Page);
        return output.ToArray();
    }

    private const string Dotted = "<span style='text-decoration:underline;text-decoration-style:dotted'>GST</span>";

    [Test]
    public void TitledAbbrDrawsADottedUnderline()
    {
        Assert.That(Pdf("<p>Add <abbr title='Goods and Services Tax'>GST</abbr>.</p>"),
            Is.EqualTo(Pdf($"<p>Add {Dotted}.</p>")));
        Assert.That(Pdf("<p>Add <abbr title=''>GST</abbr>.</p>"), Is.EqualTo(Pdf($"<p>Add {Dotted}.</p>")));
        Assert.That(Pdf("<p>Add <abbr title='x'>GST</abbr>.</p>"), Is.Not.EqualTo(Pdf("<p>Add <span>GST</span>.</p>")));
    }

    [Test]
    public void UntitledAbbrIsPlainText()
        => Assert.That(Pdf("<p>Add <abbr class=k>GST</abbr>.</p>"), Is.EqualTo(Pdf("<p>Add <span>GST</span>.</p>")));

    [Test]
    public void CssOverridesTheDefaultAndSiblingsKeepTheirStyle()
    {
        Assert.That(Pdf("<style>abbr{text-decoration:none}</style><p>Add <abbr title='t'>GST</abbr>.</p>"),
            Is.EqualTo(Pdf("<p>Add <span>GST</span>.</p>")));
        Assert.That(Pdf("<p><abbr title='t'>GST</abbr> and <abbr>GST</abbr> and <span>GST</span></p>"),
            Is.EqualTo(Pdf($"<p>{Dotted} and <span>GST</span> and <span>GST</span></p>")));
    }

    [Test]
    public void WorksInTableCells()
    {
        const string before = "<table style='width:140pt'><colgroup><col style='width:140pt'></colgroup><tbody><tr><td style='padding:3pt'>";
        const string after = "</td></tr></tbody></table>";
        Assert.That(Pdf(before + "Rate <abbr title='Goods and Services Tax'>GST</abbr>" + after),
            Is.EqualTo(Pdf(before + "Rate " + Dotted + after)));
    }

    [TestCase("<p><abbr datetime='x'>A</abbr></p>", "FPDF1103")]
    [TestCase("<p><abbr style='text-align:justify'>A</abbr></p>", "FPDF1201")]
    [TestCase("<abbr><div>A</div></abbr>", "FPDF1101")]
    public void UnsupportedUsesStillFail(string html, string code)
    {
        using var output = new MemoryStream();
        var ex = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, Page))!;
        Assert.That(ex.Code, Is.EqualTo(code));
        Assert.That(output.Length, Is.Zero);
    }
}
