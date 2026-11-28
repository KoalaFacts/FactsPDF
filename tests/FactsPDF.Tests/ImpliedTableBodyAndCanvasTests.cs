using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// Rows written directly in a table are put in a tbody the HTML parser implies, as Chrome does, and the
/// html element's background-color is the page canvas, which Chrome prints over each whole sheet.
/// </summary>
[TestFixture]
public sealed class ImpliedTableBodyAndCanvasTests
{
    private const string Columns = "<colgroup><col style='width:100pt'></colgroup>";

    private static byte[] Pdf(string html, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, options ?? new PdfOptions { Tagged = false });
        return output.ToArray();
    }

    [TestCase("<table style='width:100pt'>" + Columns + "<tr><td>A</td></tr><tr><td>B</td></tr></table>",
        "<table style='width:100pt'>" + Columns + "<tbody><tr><td>A</td></tr><tr><td>B</td></tr></tbody></table>")]
    [TestCase("<table><tr><td>A</td><td>B</td></tr></table>",
        "<table><tbody><tr><td>A</td><td>B</td></tr></tbody></table>")]
    [TestCase("<table style='width:100pt'>" + Columns + "<thead><tr><th style='font-weight:normal'>H</th></tr></thead><tr><td>A</td></tr></table>",
        "<table style='width:100pt'>" + Columns + "<thead><tr><th style='font-weight:normal'>H</th></tr></thead><tbody><tr><td>A</td></tr></tbody></table>")]
    public void RowsDirectlyInATableRenderLikeAnExplicitTbody(string implied, string explicitBody)
    {
        Assert.That(Pdf(implied), Is.EqualTo(Pdf(explicitBody)));
    }

    [Test]
    public void ImpliedTbodyTakesPartInStructuralSelectors()
    {
        const string style = "<style>tbody > tr:nth-of-type(even) td{color:#c00000}</style>";
        Assert.That(Pdf(style + "<table style='width:100pt'>" + Columns + "<tr><td>A</td></tr><tr><td>B</td></tr></table>"),
            Is.EqualTo(Pdf(style + "<table style='width:100pt'>" + Columns + "<tbody><tr><td>A</td></tr><tr><td>B</td></tr></tbody></table>")));
    }

    [Test]
    public void HtmlBackgroundPaintsEveryWholeSheet()
    {
        var options = new PdfOptions { Tagged = false, PageWidth = 200, PageHeight = 100, Margin = 10 };
        var lines = string.Concat(Enumerable.Range(1, 12).Select(i => $"<p style='margin:0'>L{i}</p>"));
        var text = Encoding.Latin1.GetString(Pdf("<style>html{background-color:#eef3f7}</style>" + lines, options));
        // One full-sheet rectangle in the canvas colour on each of the pages.
        var sheets = text.Split("0 0 200 100 re f").Length - 1;
        Assert.That(sheets, Is.GreaterThan(1));
        Assert.That(sheets, Is.EqualTo(text.Split("/Type /Page ").Length - 1));
    }

    [TestCase("html{background-color:#fff}")]
    [TestCase("html{background-color:white}")]
    [TestCase("html{background-color:transparent}")]
    [TestCase("html{background-color:#cecece}@media print{html{background-color:white}}")]
    public void AWhiteOrTransparentCanvasPaintsNothing(string css)
        => Assert.That(Pdf($"<style>{css}</style><p>A</p>"), Is.EqualTo(Pdf("<p>A</p>")));

    [Test]
    public void OtherHtmlBoxPropertiesStillFail()
    {
        using var output = new MemoryStream();
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert("<style>html{padding:4pt}</style><p>A</p>", output));
        Assert.That(error!.Code, Is.EqualTo("FPDF1201"));
    }

    // html{display:flex} makes body a flex item: one wider than the page shrinks to it (flex-shrink 1),
    // as Chrome prints it, so it lays out like an auto-width body.
    [Test]
    public void AnOverWideBodyInAFlexHtmlShrinksToThePage()
    {
        var options = new PdfOptions { Tagged = false, PageWidth = 300, PageHeight = 300, Margin = 20 };
        const string content = "<table style='width:100%'><tr><td>A</td><td>B</td></tr></table><p>Text</p>";
        Assert.That(Pdf("<style>html{display:flex;justify-content:center}body{width:210mm;margin:0}</style>" + content, options),
            Is.EqualTo(Pdf("<style>body{margin:0}</style>" + content, options)));
    }

    [Test]
    public void AnOverWideBodyThatCannotShrinkStillFails()
    {
        using var output = new MemoryStream();
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(
            "<style>html{display:flex}body{width:210mm;flex-shrink:0}</style><p>A</p>", output,
            new PdfOptions { Tagged = false, PageWidth = 300, PageHeight = 300, Margin = 20 }));
        Assert.That(error!.Code, Is.EqualTo("FPDF1201"));
    }
}
