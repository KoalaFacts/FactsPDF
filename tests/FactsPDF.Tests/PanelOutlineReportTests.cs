using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>Outside-border report outlines are PDF paint, not layout insets.</summary>
[TestFixture]
public sealed class PanelOutlineReportTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 240, Margin = 20 };

    private static byte[] Pdf(string html, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, options ?? Page);
        return output.ToArray();
    }

    private static LayoutPage[] Layout(string html, PdfOptions? options = null)
        => TextLayout.Layout(HtmlDocumentReader.ReadTree(html, options ?? Page, default),
            options ?? Page, default).ToArray();

    private static void FailsAndRetains(string html, string code, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        output.Write("ORIGINAL"u8);
        var error = Assert.Throws<FactsPdfException>(() =>
            PdfConverter.Convert(html, output, options ?? Page));
        Assert.That(error!.Code, Is.EqualTo(code));
        Assert.That(output.ToArray(), Is.EqualTo("ORIGINAL"u8.ToArray()));
        Assert.That(Pdf("<p>Recovery</p>").Length, Is.GreaterThan(100));
    }

    [TestCase("solid", "2 w", "1 0 0 RG")]
    [TestCase("dashed", "[6 6] 0 d", "1 0 0 RG")]
    [TestCase("dotted", "1 J [0.001 4] 0 d", "1 0 0 RG")]
    public void OutlineDrawsOutsideTheRealBorderBoxWithoutMovingText(
        string kind, string expected, string colour)
    {
        var ordinary = "<section style='width:120pt;padding:5pt;border:1pt solid blue'>PAID</section>";
        var styled = "<section style='width:120pt;padding:5pt;border:1pt solid blue;" +
            "outline:2pt " + kind + " red'>PAID</section>";
        var plain = Layout(ordinary)[0];
        var decorated = Layout(styled)[0];
        Assert.That(decorated.PaintBoxes.Single().Width,
            Is.EqualTo(plain.PaintBoxes.Single().Width));
        Assert.That(decorated.Runs.Single().X, Is.EqualTo(plain.Runs.Single().X));
        Assert.That(decorated.Runs.Single().Baseline, Is.EqualTo(plain.Runs.Single().Baseline));
        Assert.That(decorated.PaintBoxes.Single().CommandCount,
            Is.EqualTo(plain.PaintBoxes.Single().CommandCount + 1));
        var stroke = decorated.PaintBoxes.Single();
        var left = stroke.X - 1;
        Assert.That(left, Is.EqualTo(19).Within(0.00001));
        var pdf = Encoding.ASCII.GetString(Pdf(styled));
        Assert.That(pdf, Does.Contain(expected));
        Assert.That(pdf, Does.Contain(colour));
        Assert.That(pdf, Does.Contain("19 ").And.Contain(" S Q\n"),
            "The exterior outline path must be painted as an actual PDF stroke.");
        Assert.That(pdf, Does.Contain("Tm <"), "Searchable text is retained.");
    }

    [Test]
    public void ShorthandAndLonghandCascadeImportantAndCssWideWorkPerComponent()
    {
        const string html = "<style>section.card{outline:1pt dashed blue;" +
            "outline-width:2pt!important}article{outline:inherit}</style>" +
            "<section class=card style='outline:3pt dotted red;color:#225588'>" +
            "<article>INHERITED</article>" +
            "<article style='outline:unset'>RESET</article></section>";
        var parent = (BlockNode)HtmlDocumentReader.ReadTree(html, Page, default).Children.Single();
        Assert.That(parent.BoxStyle.Outline.Width, Is.EqualTo(2));
        Assert.That(parent.BoxStyle.Outline.Pattern, Is.EqualTo(BorderPattern.Dotted));
        Assert.That(parent.BoxStyle.Outline.Color, Is.EqualTo(new Rgb(1, 0, 0)));
        var children = parent.Children.OfType<BlockNode>().ToArray();
        Assert.That(children[0].BoxStyle.Outline, Is.EqualTo(parent.BoxStyle.Outline));
        Assert.That(children[1].BoxStyle.Outline.EffectiveWidth, Is.Zero);
        Assert.That(Pdf("<section style='outline:2pt dotted currentColor;color:red'>A</section>"),
            Is.EqualTo(Pdf("<section style='outline-width:2pt;outline-style:dotted;" +
                "outline-color:red;color:red'>A</section>")));
    }

    [Test]
    public void ZeroNoneAndMissingOutlineKeepOldBytesAndCommands()
    {
        var plain = Pdf("<div>Invoice amount: 200</div>");
        Assert.That(Pdf("<div style='outline:none'>Invoice amount: 200</div>"), Is.EqualTo(plain));
        Assert.That(Pdf("<div style='outline:0 solid red'>Invoice amount: 200</div>"), Is.EqualTo(plain));
        Assert.That(Layout("<div style='outline:0 solid red'>Invoice amount: 200</div>")[0].PaintBoxes,
            Is.Empty);
    }

    [Test]
    public void MultiPageReportKeepsSideStrokesAndOnlyTerminalHorizontalEdges()
    {
        var records = string.Concat(Enumerable.Range(1, 44).Select(i =>
            "<p style='margin:0;line-height:20pt'>Record " + i.ToString("D2") + ": verified</p>"));
        var plain = "<section style='padding:5pt'>" + records + "</section>";
        var outlined = "<section style='padding:5pt;outline:2pt dashed #285d91'>" +
            records + "</section>";
        var original = Layout(plain);
        var pages = Layout(outlined);
        Assert.That(pages.Length, Is.GreaterThan(2));
        Assert.That(pages.Length, Is.EqualTo(original.Length),
            "An outline must not consume page layout height.");
        for (var page = 0; page < pages.Length; page++)
        {
            var box = pages[page].PaintBoxes.Single();
            Assert.That(box.IsFirstFragment, Is.EqualTo(page == 0));
            Assert.That(box.IsLastFragment, Is.EqualTo(page == pages.Length - 1));
            Assert.That(box.CommandCount, Is.EqualTo(1));
            Assert.That(pages[page].Runs.Select(r => r.Text),
                Is.EquivalentTo(original[page].Runs.Select(r => r.Text)));
        }
        var pdf = Encoding.ASCII.GetString(Pdf(outlined));
        Assert.That(pdf.Split("[6 6] 0 d").Length - 1, Is.EqualTo(pages.Length));
        Assert.That(pdf.Split(" S Q\n").Length - 1, Is.EqualTo(pages.Length));
    }

    [Test]
    public void ExplicitFontAndStyleKeepGlyphSearchability()
    {
        var options = Page with { Fonts = [PdfFont.LoadTrueType(FontFixture.Create())] };
        var pdf = Encoding.Latin1.GetString(Pdf(
            "<section style='color:#345678;outline:1pt solid currentColor'>" +
            "<p>A中文B</p></section>", options));
        Assert.That(pdf, Does.Contain("/ToUnicode"));
        Assert.That(pdf, Does.Contain("/Subtype /Type0"));
        Assert.That(pdf, Does.Contain(" RG 1 w "));
    }

    [Test]
    public void InvalidOrInapplicableOutlinesPreserveDestinationAndCanRecover()
    {
        FailsAndRetains("<div style='outline:5pt solid blue'>A</div>", "FPDF1202");
        FailsAndRetains("<div style='outline-width:5pt'>A</div>", "FPDF1202");
        FailsAndRetains("<div style='outline:2pt double red'>A</div>", "FPDF1202");
        FailsAndRetains("<div style='outline:2pt solid red;outline-offset:3pt'>A</div>", "FPDF1201");
        FailsAndRetains("<style>.unmatched{outline:5pt solid blue}</style><p>A</p>", "FPDF1202");
        FailsAndRetains("<span style='outline:2pt solid blue'>A</span>", "FPDF1201");
        FailsAndRetains("<p style='outline:2pt solid blue'>A</p>", "FPDF1201");
        FailsAndRetains("<section style='outline:2pt solid blue;border-radius:5pt'>A</section>", "FPDF1201");
        FailsAndRetains("<section style='outline:2pt dashed red'>A</section>", "FPDF1401",
            Page with { MaxDisplayCommands = 0 });
    }
}
