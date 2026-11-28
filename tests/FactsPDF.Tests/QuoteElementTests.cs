using NUnit.Framework;

namespace FactsPDF.Tests;

// q is inline text wrapped in the English quotation marks of the HTML user-agent sheet
// (quotes:auto): curly double quotes outside, curly single quotes for every nested q. Normal
// text in the built-in ASCII-only font gets the ASCII marks instead (R5cf). Before, q failed
// FPDF1102.
[TestFixture]
public sealed class QuoteElementTests
{
    private static readonly PdfOptions BuiltIn = new() { PageWidth = 260, PageHeight = 300, Margin = 12 };

    private static readonly PdfOptions Supplied = BuiltIn with
    {
        Fonts = [PdfFont.FromBytes(FontFixture.Create(quotationMarks: true))],
        BoldFonts = [PdfFont.FromBytes(FontFixture.Create(quotationMarks: true))],
    };

    private static byte[] Pdf(string html, PdfOptions options)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, options);
        return output.ToArray();
    }

    [TestCase("<p>AB <q>WA</q> B</p>", "<p>AB <span>“WA”</span> B</p>")]
    [TestCase("<p><q>A <q>B</q> W</q></p>", "<p>“A ‘B’ W”</p>")]
    [TestCase("<p><q>A <q>B <q>W</q></q></q></p>", "<p>“A ‘B ‘W’’”</p>")]
    [TestCase("<q>AB</q> A", "<span>“AB”</span> A")]
    [TestCase("<p><q cite='https://example.com/x' id=a class=b lang=en>AB</q></p>", "<p>“AB”</p>")]
    [TestCase("<ul><li><q>AB</q></li></ul>", "<ul><li>“AB”</li></ul>")]
    [TestCase("<p><q></q></p>", "<p>“”</p>")]
    // The marks join the q's own text runs, so an oracle spells the q as a span holding them.
    public void SuppliedFontsGetCurlyMarks(string html, string oracle)
        => Assert.That(Pdf(html, Supplied), Is.EqualTo(Pdf(oracle, Supplied)));

    [TestCase("<p>She said <q>pay by Friday</q>.</p>", "<p>She said <span>\"pay by Friday\"</span>.</p>")]
    [TestCase("<p><q>Outer <q>inner</q> end</q></p>", "<p>\"Outer 'inner' end\"</p>")]
    [TestCase("<div><q>Anonymous</q> text</div>", "<div><span>\"Anonymous\"</span> text</div>")]
    public void TheBuiltInFontGetsAsciiMarks(string html, string oracle)
        => Assert.That(Pdf(html, BuiltIn), Is.EqualTo(Pdf(oracle, BuiltIn)));

    [Test]
    public void MarksTakeTheQuoteElementsStyle()
        => Assert.That(Pdf("<style>q{color:#c00;font-weight:bold}</style><p>AB <q>W</q> A</p>", Supplied),
            Is.EqualTo(Pdf("<p>AB <span style='color:#c00;font-weight:bold'>“W”</span> A</p>", Supplied)));

    [Test]
    public void TableCellQuotesRenderLikeText()
    {
        static string Table(string cell) =>
            // The 236pt column fills the page content box, so the user-agent body margin is zeroed.
            $"<style>body{{margin:0}}</style><table><colgroup><col style='width:236pt'></colgroup><tbody><tr><td>{cell}</td></tr></tbody></table>";
        Assert.That(Pdf(Table("<q>AB</q>"), Supplied), Is.EqualTo(Pdf(Table("“AB”"), Supplied)));
    }

    [TestCase("<q><p>Block</p></q>", "FPDF1101")]
    [TestCase("<p><q><em>Open</p>B", "FPDF1101")]
    [TestCase("<q href='x'>A</q>", "FPDF1103")]
    [TestCase("<mark>A</mark>", "FPDF1102")]
    public void UnsupportedUsesStillFail(string html, string code)
    {
        using var output = new MemoryStream();
        var ex = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, BuiltIn))!;
        Assert.Multiple(() =>
        {
            Assert.That(ex.Code, Is.EqualTo(code));
            Assert.That(output.Length, Is.Zero);
        });
    }
}
