using System.Text;
using FactsPDF.CommandLine;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>Actual PDF, cascade, native-consumer and protected failure contract
/// for fixed-height business report cards. No general CSS sizing claim.</summary>
[TestFixture]
public sealed class ReportPanelFixedHeightTests
{
    private static readonly PdfOptions Page = new()
    {
        PageWidth = 300, PageHeight = 500, Margin = 20
    };

    private static byte[] Render(string html, PdfOptions? options = null)
    {
        using var result = new MemoryStream();
        PdfConverter.Convert(html, result, options ?? Page);
        return result.ToArray();
    }

    private static List<LayoutPage> Layout(string html, PdfOptions? options = null)
    {
        var page = options ?? Page;
        return TextLayout.Layout(HtmlDocumentReader.ReadTree(html, page, default), page, default);
    }

    private static void Reject(string html, string code, PdfOptions? options = null)
    {
        using var destination = new MemoryStream();
        destination.Write("PREVIOUS-CUSTOMER-PDF"u8);
        destination.Position = 5;
        var error = Assert.Throws<FactsPdfException>(() =>
            PdfConverter.Convert(html, destination, options ?? Page));
        Assert.That(error!.Code, Is.EqualTo(code));
        Assert.That(destination.ToArray(), Is.EqualTo("PREVIOUS-CUSTOMER-PDF"u8.ToArray()));
        Assert.That(destination.Position, Is.EqualTo(5));
        Assert.That(Render("<p>Recovered</p>").Length, Is.GreaterThan(100));
    }

    [TestCase("div")]
    [TestCase("section")]
    [TestCase("article")]
    [TestCase("blockquote")]
    public void ShortFixedHeightPanelReallyPaintsAndPositionsFollowingText(string tag)
    {
        var fixedHtml = "<" + tag + " style='height:90pt;background:#edf5fa;" +
            "border:2pt solid #23446b;padding:5pt'><p style='margin:0'>FIRST</p></" +
            tag + "><p style='margin:0'>AFTER</p>";
        var minimum = fixedHtml.Replace("height:90pt", "min-height:90pt");
        var pages = Layout(fixedHtml);
        Assert.That(pages, Has.Count.EqualTo(1));
        Assert.That(pages[0].PaintBoxes.Single().Height, Is.EqualTo(104).Within(0.00001));
        Assert.That(pages[0].Runs.Select(x => x.Text), Is.EqualTo(new[] { "FIRST", "AFTER" }));
        Assert.That(Render(fixedHtml), Is.EqualTo(Render(minimum)),
            "Equivalent short fixed/minimum-height cards must produce identical PDF glyph and paint positions.");
    }

    [Test]
    public void BorderBoxAndContentBoxProduceDifferentMeasuredPDFHeights()
    {
        const string html = "<section style='height:100pt;background:blue;" +
            "border:2pt solid red;padding:5pt;box-sizing:KIND'>" +
            "<p style='margin:0'>ITEM</p></section><p style='margin:0'>FOLLOW</p>";
        var content = Layout(html.Replace("KIND", "content-box"));
        var border = Layout(html.Replace("KIND", "border-box"));
        Assert.That(content.Single().PaintBoxes.Single().Height, Is.EqualTo(114).Within(0.00001));
        Assert.That(border.Single().PaintBoxes.Single().Height, Is.EqualTo(100).Within(0.00001));
        Assert.That(content.Single().Runs[0].Baseline, Is.EqualTo(border.Single().Runs[0].Baseline));
        Assert.That(content.Single().Runs[1].Baseline - border.Single().Runs[1].Baseline,
            Is.EqualTo(-14).Within(0.00001));
        Assert.That(Render(html.Replace("KIND", "content-box")),
            Is.Not.EqualTo(Render(html.Replace("KIND", "border-box"))));
    }

    [Test]
    public void ElementAndRootRelativeLengthsMatchPhysicalByteOracle()
    {
        const string prefix = "<html style='font-size:10pt'><body>" +
            "<section style='font-size:16pt;height:";
        const string suffix = ";background:blue;padding:4pt'>" +
            "<p style='margin:0'>REPORT</p></section></body></html>";
        Assert.That(Render(prefix + "2em" + suffix), Is.EqualTo(Render(prefix + "32pt" + suffix)));
        Assert.That(Render(prefix + "3.2rem" + suffix), Is.EqualTo(Render(prefix + "32pt" + suffix)));
        Assert.That(Render(prefix + "96px" + suffix), Is.EqualTo(Render(prefix + "1in" + suffix)));
        Assert.That(Render(prefix + "1in" + suffix), Is.EqualTo(Render(prefix + "72pt" + suffix)));
    }

    [Test]
    public void CascadeImportantAndCssWideValuesResolveOnlyWinningPanelHeight()
    {
        const string html = "<style>section.card{height:90pt!important}" +
            ".card{height:40pt}</style>" +
            "<section class=card style='height:20pt'>" +
            "<div style='height:inherit'>A</div>" +
            "<article style='height:unset'>B</article>" +
            "<blockquote style='height:initial'>C</blockquote>" +
            "</section>";
        var node = (BlockNode)HtmlDocumentReader.ReadTree(html, Page, default).Children.Single();
        Assert.That(node.BoxStyle.Height!.Value.Value, Is.EqualTo(90));
        var nested = node.Children.OfType<BlockNode>().ToArray();
        Assert.That(nested[0].BoxStyle.Height!.Value.Value, Is.EqualTo(90));
        Assert.That(nested[1].BoxStyle.Height, Is.Null);
        Assert.That(nested[2].BoxStyle.Height, Is.Null);
        Assert.That(Render("<section style='height:auto'><p style='margin:0'>A</p></section>"),
            Is.EqualTo(Render("<section><p style='margin:0'>A</p></section>")));
    }

    [Test]
    public void ExplicitMinHeightWinsWhenLargerThanHeight()
    {
        const string before = "<section style='background:blue;";
        const string after = "'><p style='margin:0'>A</p></section><p style='margin:0'>AFTER</p>";
        Assert.That(Render(before + "height:70pt;min-height:110pt" + after),
            Is.EqualTo(Render(before + "min-height:110pt" + after)));
        Assert.That(Render(before + "height:110pt;min-height:70pt" + after),
            Is.EqualTo(Render(before + "height:110pt" + after)));
    }

    [Test]
    public void FixedCardPreflightsAsOnePaintedFragmentOnTheNextPage()
    {
        var shortPage = Page with { PageHeight = 180 };
        const string html = "<p style='margin:0'>BEFORE</p>" +
            "<section style='height:130pt;background:#f0f6fa'>" +
            "<p style='margin:0'>AFTER</p></section>";
        var pages = Layout(html, shortPage);
        Assert.That(pages, Has.Count.EqualTo(2));
        Assert.That(pages[0].Runs.Single().Text, Is.EqualTo("BEFORE"));
        Assert.That(pages[1].Runs.Single().Text, Is.EqualTo("AFTER"));
        Assert.That(pages[1].PaintBoxes.Single().Top, Is.EqualTo(20).Within(0.00001));
        Assert.That(pages[1].PaintBoxes.Single().Height, Is.EqualTo(130).Within(0.00001));
        Assert.That(Encoding.ASCII.GetString(Render(html, shortPage)), Does.Contain("/Count 2"));
    }

    [Test]
    public void UnpaintedEmptyFixedHeightAdvancesTheFollowingText()
    {
        const string fixedHtml = "<div style='height:60pt'></div><p style='margin:0'>AFTER</p>";
        const string minimum = "<div style='min-height:60pt'></div><p style='margin:0'>AFTER</p>";
        Assert.That(Render(fixedHtml), Is.EqualTo(Render(minimum)));
        Assert.That(Layout(fixedHtml)[0].Runs.Single().Text, Is.EqualTo("AFTER"));
    }

    [Test]
    public void OverfullTextAndCrossPageContentFailRatherThanClippingOrMovingTheNextCard()
    {
        Reject("<section style='height:5pt'><p style='margin:0'>Too tall</p></section>",
            "FPDF1302");
        var paragraphs = string.Concat(Enumerable.Range(0, 40)
            .Select(index => "<p style='margin:0'>LINE " + index + "</p>"));
        Reject("<section style='height:80pt;background:blue'>" +
            paragraphs + "</section>", "FPDF1302");
    }

    [TestCase("<section style='height:30%'>A</section>", "FPDF1202")]
    [TestCase("<section style='height:-5pt'>A</section>", "FPDF1202")]
    [TestCase("<section style='height:calc(90pt)'>A</section>", "FPDF1202")]
    [TestCase("<section style='height:30000pt'>A</section>", "FPDF1202")]
    [TestCase("<p style='height:90pt'>A</p>", "FPDF1201")]
    [TestCase("<ul style='height:90pt'><li>A</li></ul>", "FPDF1201")]
    [TestCase("<style>.absent{height:30%}</style><section>A</section>", "FPDF1202")]
    public void UnsupportedInputPreservesTheExistingPdf(string html, string code)
        => Reject(html, code);

    [Test]
    public void ActualManagedCliAndDirectApiProduceIdenticalPDF()
    {
        const string html = "<section style='height:90pt;background:#cee8fb;" +
            "border:2pt solid #23446b;padding:3pt'><p style='margin:0'>REPORT</p></section>";
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(html));
        using var output = new MemoryStream();
        using var diagnostics = new StringWriter();
        Assert.That(CliApplication.Run(["-", "-"], input, output, diagnostics),
            Is.Zero, diagnostics.ToString());
        using var oracle = new MemoryStream();
        PdfConverter.Convert(html, oracle);
        Assert.That(output.ToArray(), Is.EqualTo(oracle.ToArray()));
    }
}
