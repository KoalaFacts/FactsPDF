using NUnit.Framework;

namespace FactsPDF.Tests;

// tfoot follows the tbody and holds ordinary td rows (invoice totals); they render exactly
// like further tbody rows, once, after the body rows. Before, the tag failed FPDF1102.
[TestFixture]
public sealed class TableFooterElementTests
{
    private static readonly PdfOptions Page = new() { Tagged = false,
        PageWidth = 260, PageHeight = 200, Margin = 12,
        Fonts = [PdfFont.FromBytes(FontFixture.Create())], BoldFonts = [PdfFont.FromBytes(FontFixture.Create())],
    };

    private const string Columns = "<colgroup><col style='width:120pt'><col style='width:60pt'></colgroup>";

    private static byte[] Pdf(string html)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, Page);
        return output.ToArray();
    }

    private static string Rows(int count, int from = 0)
        => string.Concat(Enumerable.Range(from, count).Select(i => $"<tr><td>A{new string('B', i % 4)}</td><td>W{new string('A', i % 3)}</td></tr>"));

    [Test]
    public void FooterRowsRenderLikeFurtherBodyRows()
        => Assert.That(Pdf($"<table style='width:180pt'>{Columns}<tbody>{Rows(2)}</tbody><tfoot><tr><td>WAB</td><td>BW</td></tr></tfoot></table>"),
            Is.EqualTo(Pdf($"<table style='width:180pt'>{Columns}<tbody>{Rows(2)}<tr><td>WAB</td><td>BW</td></tr></tbody></table>")));

    [Test]
    public void FooterWorksAfterAHeaderAndWithSeveralRows()
        => Assert.That(Pdf($"<table style='width:180pt'>{Columns}<thead><tr><th>A</th><th>B</th></tr></thead><tbody>{Rows(3)}</tbody>" +
                $"<tfoot>{Rows(2, 3)}</tfoot></table><p>AB</p>"),
            Is.EqualTo(Pdf($"<table style='width:180pt'>{Columns}<thead><tr><th>A</th><th>B</th></tr></thead><tbody>{Rows(5)}</tbody></table><p>AB</p>")));

    [Test]
    public void FooterSelectorsAndInheritanceApplyAsForAClassedBodyRow()
    {
        const string css = "<style>td{border:1pt solid #333;padding:2pt}" +
            "tfoot{color:#c00}tfoot td{font-weight:bold;background-color:#eee}table>tfoot>tr>td.n{text-align:right}</style>";
        const string oracle = "<style>td{border:1pt solid #333;padding:2pt}" +
            "tr.f td{color:#c00;font-weight:bold;background-color:#eee}tr.f td.n{text-align:right}</style>";
        Assert.That(Pdf($"{css}<table style='width:180pt'>{Columns}<tbody>{Rows(2)}</tbody><tfoot id=t class=x><tr><td>WAB</td><td class=n>BW</td></tr></tfoot></table>"),
            Is.EqualTo(Pdf($"{oracle}<table style='width:180pt'>{Columns}<tbody>{Rows(2)}<tr class=f><td>WAB</td><td class=n>BW</td></tr></tbody></table>")));
    }

    [Test]
    public void FooterAcceptsColspanAndStructuralWhitespaceAndComments()
        => Assert.That(Pdf($"<table style='width:180pt'>{Columns}<tbody>{Rows(2)}</tbody>\n <!-- totals -->\n<tfoot>\n <tr><td colspan=2>WAB</td></tr>\n</tfoot> <!-- end -->\n</table>"),
            Is.EqualTo(Pdf($"<table style='width:180pt'>{Columns}<tbody>{Rows(2)}<tr><td colspan=2>WAB</td></tr></tbody></table>")));

    [Test]
    public void LongTablesPaginateWithTheFooterRowsAtTheEnd()
        => Assert.That(Pdf($"<table style='width:180pt'>{Columns}<thead><tr><th>A</th><th>B</th></tr></thead><tbody>{Rows(30)}</tbody><tfoot>{Rows(3, 30)}</tfoot></table>"),
            Is.EqualTo(Pdf($"<table style='width:180pt'>{Columns}<thead><tr><th>A</th><th>B</th></tr></thead><tbody>{Rows(33)}</tbody></table>")));

    // Chrome draws a table that has only a tfoot (invoice totals) as its footer rows alone, and a
    // tfoot written before the tbody (or the thead) after the body rows; measured against its printed PDF.
    [Test]
    public void ATableWithOnlyAFooterRendersLikeThoseRowsInABody()
        => Assert.That(Pdf($"<table style='width:180pt'>{Columns}<tfoot>{Rows(3)}</tfoot></table><p>AB</p>"),
            Is.EqualTo(Pdf($"<table style='width:180pt'>{Columns}<tbody>{Rows(3)}</tbody></table><p>AB</p>")));

    [Test]
    public void AFooterOnlyTableWithoutColumnsTakesItsColumnsFromTheFirstFooterRow()
        => Assert.That(Pdf($"<table><tfoot><tr><td colspan='2'></td><td>WAB</td></tr><tr><td>A</td><td>B</td><td>W</td></tr></tfoot></table>"),
            Is.EqualTo(Pdf($"<table><tbody><tr><td colspan='2'></td><td>WAB</td></tr><tr><td>A</td><td>B</td><td>W</td></tr></tbody></table>")));

    [Test]
    public void AHeaderAndAFooterWithoutABodyRenderLikeHeaderAndBodyRows()
        => Assert.That(Pdf($"<table style='width:180pt'>{Columns}<thead><tr><th>A</th><th>B</th></tr></thead><tfoot>{Rows(2)}</tfoot></table><p>AB</p>"),
            Is.EqualTo(Pdf($"<table style='width:180pt'>{Columns}<thead><tr><th>A</th><th>B</th></tr></thead><tbody>{Rows(2)}</tbody></table><p>AB</p>")));

    [Test]
    public void AFooterWrittenBeforeTheBodyIsLaidOutAfterIt()
    {
        var footer = "<tfoot><tr><td>WAB</td><td>BW</td></tr></tfoot>";
        var after = $"<table style='width:180pt'>{Columns}<tbody>{Rows(3)}</tbody>{footer}</table><p>AB</p>";
        Assert.That(Pdf($"<table style='width:180pt'>{Columns}{footer}<tbody>{Rows(3)}</tbody></table><p>AB</p>"), Is.EqualTo(Pdf(after)));
        Assert.That(Pdf($"<table style='width:180pt'>{Columns}{footer}<thead><tr><th>A</th><th>B</th></tr></thead><tbody>{Rows(3)}</tbody></table><p>AB</p>"),
            Is.EqualTo(Pdf($"<table style='width:180pt'>{Columns}<thead><tr><th>A</th><th>B</th></tr></thead><tbody>{Rows(3)}</tbody>{footer}</table><p>AB</p>")));
    }

    // Over-correction: moving the footer must not move its group's own paint or cascade with it
    // to the wrong place, nor reorder the body rows.
    [Test]
    public void AFooterBeforeTheBodyKeepsItsOwnStylingAndTheBodyRowsKeepTheirOrder()
    {
        const string css = "<style>td{border:1pt solid #333;padding:2pt}tfoot{background-color:#eee;color:#c00}</style>";
        var footer = "<tfoot><tr><td>WAB</td><td>BW</td></tr></tfoot>";
        Assert.That(Pdf($"{css}<table style='width:180pt'>{Columns}{footer}<tbody>{Rows(4)}</tbody></table>"),
            Is.EqualTo(Pdf($"{css}<table style='width:180pt'>{Columns}<tbody>{Rows(4)}</tbody>{footer}</table>")));
        Assert.That(Pdf($"{css}<table style='width:180pt;border-collapse:collapse'>{Columns}{footer}<tbody>{Rows(4)}</tbody></table>"),
            Is.EqualTo(Pdf($"{css}<table style='width:180pt;border-collapse:collapse'>{Columns}<tbody>{Rows(4)}</tbody>{footer}</table>")));
    }

    [Test]
    public void AFooterBeforeTheBodyStillPaginatesAfterTheBodyRows()
        => Assert.That(Pdf($"<table style='width:180pt'>{Columns}<tfoot>{Rows(3, 30)}</tfoot><thead><tr><th>A</th><th>B</th></tr></thead><tbody>{Rows(30)}</tbody></table>"),
            Is.EqualTo(Pdf($"<table style='width:180pt'>{Columns}<thead><tr><th>A</th><th>B</th></tr></thead><tbody>{Rows(30)}</tbody><tfoot>{Rows(3, 30)}</tfoot></table>")));

    [TestCase("<table style='width:180pt'>" + Columns + "<thead><tr><th>A</th><th>B</th></tr></thead></table>", "FPDF1101")]
    [TestCase("<table style='width:180pt'>" + Columns + "<tbody><tr><td>A</td><td>B</td></tr></tbody><thead><tr><th>A</th><th>B</th></tr></thead><tfoot><tr><td>A</td><td>B</td></tr></tfoot></table>", "FPDF1101")]
    [TestCase("<table style='width:180pt'>" + Columns + "<tbody><tr><td>A</td><td>B</td></tr></tbody><tfoot></tfoot></table>", "FPDF1101")]
    [TestCase("<table style='width:180pt'>" + Columns + "<tbody><tr><td>A</td><td>B</td></tr></tbody><tfoot><tr><td>A</td></tr></tfoot></table>", "FPDF1101")]
    [TestCase("<table style='width:180pt'>" + Columns + "<tbody><tr><td>A</td><td>B</td></tr></tbody><tfoot><tr><td>A</td><td>B</td></tr></tfoot><tfoot><tr><td>A</td><td>B</td></tr></tfoot></table>", "FPDF1101")]
    [TestCase("<table style='width:180pt'>" + Columns + "<tbody><tr><td>A</td><td>B</td></tr></tbody><tfoot><tr><td>A</td><td>B</td></tr></tfoot><tbody><tr><td>A</td><td>B</td></tr></tbody></table>", "FPDF1101")]
    [TestCase("<table style='width:180pt'>" + Columns + "<tbody><tr><td>A</td><td>B</td></tr></tbody><tfoot><tr><td>A</td><td>B</td></tr></table>", "FPDF1101")]
    [TestCase("<p>A<tfoot></tfoot></p>", "FPDF1101")]
    [TestCase("<table style='width:180pt'>" + Columns + "<tbody><tr><td>A</td><td>B</td></tr></tbody><tfoot href='x'><tr><td>A</td><td>B</td></tr></tfoot></table>", "FPDF1103")]
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
