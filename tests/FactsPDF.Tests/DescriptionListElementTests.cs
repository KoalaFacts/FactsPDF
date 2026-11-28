using NUnit.Framework;

namespace FactsPDF.Tests;

// dl, dt and dd are blocks like div; the HTML user-agent sheet gives dl the block margins
// (1em, 12pt, top and bottom as for p) and dd the start indent (40px, 30pt). Before, each tag failed FPDF1102.
[TestFixture]
public sealed class DescriptionListElementTests
{
    private static readonly PdfOptions Page = new()
    {
        PageWidth = 260, PageHeight = 200, Margin = 12,
        Fonts = [PdfFont.FromBytes(FontFixture.Create())], BoldFonts = [PdfFont.FromBytes(FontFixture.Create())],
    };

    private static byte[] Pdf(string html)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, Page);
        return output.ToArray();
    }

    [Test]
    public void DescriptionListRendersLikeDivsWithTheUserAgentMargins()
        => Assert.That(Pdf("<p>A</p><dl><dt>AB</dt><dd>BA</dd><dt>W</dt><dd>WA</dd></dl><p>B</p>"),
            Is.EqualTo(Pdf("<style>.l{margin:12pt 0}.d{margin-left:30pt}</style><p>A</p><div class=l><div>AB</div>" +
                "<div class=d>BA</div><div>W</div><div class=d>WA</div></div><p>B</p>")));

    [Test]
    public void DtIsAPlainBlock()
        => Assert.That(Pdf("<dt>AB <b>W</b></dt><dt>A</dt>"), Is.EqualTo(Pdf("<div>AB <b>W</b></div><div>A</div>")));

    [Test]
    public void TheMarginsAreNotThoseOfAPlainDiv()
        => Assert.Multiple(() =>
        {
            // The dl's 12pt margin wins over the body's 6pt where a plain div has only the body margin.
            Assert.That(Pdf("<dl><dt>A</dt></dl>"), Is.Not.EqualTo(Pdf("<div><div>A</div></div>")));
            Assert.That(Pdf("<dd>A</dd>"), Is.Not.EqualTo(Pdf("<div>A</div>")));
        });

    [Test]
    public void ParagraphsInsideARenderAsInADiv()
        => Assert.That(Pdf("<dl><dd><p>AB</p><p>W</p></dd></dl>"),
            Is.EqualTo(Pdf("<style>.l{margin:12pt 0}.d{margin-left:30pt}</style><div class=l><div class=d><p>AB</p><p>W</p></div></div>")));

    [Test]
    public void AuthorCssOverridesTheDefaultsAsForADiv()
    {
        const string css = "<style>dl{margin:0;border:1pt solid #333;padding:4pt}dd{margin:0 0 4pt 0}" +
            "footer dt.k{font-weight:bold;color:#c00}</style>";
        const string oracle = "<style>div.l{border:1pt solid #333;padding:4pt}div.d{margin:0 0 4pt 0}" +
            "footer div.k{font-weight:bold;color:#c00}</style>";
        Assert.That(Pdf($"{css}<footer><dl id=x><dt class=k>AB</dt><dd>W</dd></dl></footer>"),
            Is.EqualTo(Pdf($"{oracle}<footer><div class=l id=x><div class=k>AB</div><div class=d>W</div></div></footer>")));
    }

    [Test]
    public void LongListsPaginateLikeDivs()
    {
        var dl = string.Concat(Enumerable.Range(0, 40).Select(i => $"<dt>A{new string('B', i % 5)}</dt><dd>W{new string('A', i % 3)}</dd>"));
        var div = string.Concat(Enumerable.Range(0, 40).Select(i => $"<div>A{new string('B', i % 5)}</div><div class=d>W{new string('A', i % 3)}</div>"));
        Assert.That(Pdf($"<dl>{dl}</dl>"),
            Is.EqualTo(Pdf($"<style>.l{{margin:12pt 0}}.d{{margin-left:30pt}}</style><div class=l>{div}</div>")));
    }

    [TestCase("<ul><li><dl><dt>A</dt></dl></li></ul>", "FPDF1101")]
    [TestCase("<table><tr><td><dd>A</dd></td></tr></table>", "FPDF1101")]
    [TestCase("<p><span><dt>A</dt></span></p>", "FPDF1101")]
    [TestCase("<dl><dt>A</dl>", "FPDF1101")]
    [TestCase("<dd href='x'>A</dd>", "FPDF1103")]
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
