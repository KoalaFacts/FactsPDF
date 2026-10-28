using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>Element-relative em lengths must move actual PDF boxes, text and page breaks.</summary>
[TestFixture]
public sealed class ElementRelativeBoxLengthTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 320, PageHeight = 150, Margin = 20 };

    private static byte[] Pdf(string html)
    {
        using var destination = new MemoryStream();
        PdfConverter.Convert(html, destination, Page);
        return destination.ToArray();
    }

    private static List<LayoutPage> Layout(string html)
        => TextLayout.Layout(HtmlDocumentReader.ReadTree(html, Page, default), Page, default);

    private static void Reject(string html, string code)
    {
        using var destination = new MemoryStream();
        destination.Write("existing-report"u8);
        destination.Position = 4;
        var failure = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, destination, Page));
        Assert.That(failure!.Code, Is.EqualTo(code));
        Assert.That(destination.ToArray(), Is.EqualTo("existing-report"u8.ToArray()));
        Assert.That(destination.Position, Is.EqualTo(4));
    }

    [Test]
    public void DifferentOwnComputedFontSizesDriveActualNestedPaintAndTextGeometry()
    {
        const string open = "<html style='font-size:15pt'><body style='font-size:9pt'>";
        const string relative =
            "<section style='font-size:20pt;box-sizing:border-box;width:10em;padding:0.25em;" +
            "border:1pt solid #23446b;background:#f0f6fa'>" +
            "<p style='font-size:10pt;width:12em;padding:0.2em;margin:0.5em 1em;" +
            "background:#dbeaf0'>Service report</p></section>";
        const string absolute =
            "<section style='font-size:20pt;box-sizing:border-box;width:200pt;padding:5pt;" +
            "border:1pt solid #23446b;background:#f0f6fa'>" +
            "<p style='font-size:10pt;width:120pt;padding:2pt;margin:5pt 10pt;" +
            "background:#dbeaf0'>Service report</p></section>";
        var html = open + relative + "</body></html>";
        Assert.That(Pdf(html), Is.EqualTo(Pdf(open + absolute + "</body></html>")));
        var paints = Layout(html).SelectMany(p => p.PaintBoxes)
            .Where(p => p.Style.BackgroundColor.HasValue).ToArray();
        Assert.That(paints.Select(p => p.Width), Is.EquivalentTo(new[] { 200d, 124d }));
        Assert.That(paints.Single(p => Math.Abs(p.Width - 200) < 0.00001).X,
            Is.EqualTo(20).Within(0.00001));
    }

    [Test]
    public void FinalCascadeFontAndImportantWinnerAreUsedBeforeResolvingEm()
    {
        const string relative = "<html style='font-size:15pt'><style>" +
            "p.audit{font-size:10pt;width:6em!important;padding:0.25em;margin:0.25em 0.5em}" +
            "p.audit{font-size:12pt!important}</style><body>" +
            "<p class=audit style='width:9em;background:#dbeaf0'>APPROVED</p></body></html>";
        const string absolute = "<html style='font-size:15pt'><style>" +
            "p.audit{font-size:10pt;width:72pt!important;padding:3pt;margin:3pt 6pt}" +
            "p.audit{font-size:12pt!important}</style><body>" +
            "<p class=audit style='width:108pt;background:#dbeaf0'>APPROVED</p></body></html>";
        Assert.That(Pdf(relative), Is.EqualTo(Pdf(absolute)));
    }

    [TestCase("width:8em", "width:80pt")]
    [TestCase("min-width:12em;width:8em", "min-width:120pt;width:80pt")]
    [TestCase("max-width:8em;width:12em", "max-width:80pt;width:120pt")]
    [TestCase("padding:0.5em", "padding:5pt")]
    [TestCase("margin:0.5em auto", "margin:5pt auto")]
    [TestCase("margin:0.25em 0.5em 0.75em 1em", "margin:2.5pt 5pt 7.5pt 10pt")]
    public void BoxAndMarginComponentsMatchIndependentlySpecifiedPoints(string relative, string absolute)
    {
        const string start = "<html style='font-size:15pt'><body style='font-size:9pt'>" +
            "<p style='font-size:10pt;background:#f0f6fa;box-sizing:border-box;";
        const string end = "'>Report line</p></body></html>";
        Assert.That(Pdf(start + relative + end), Is.EqualTo(Pdf(start + absolute + end)));
    }

    [Test]
    public void EmIsElementRelativeWhileRemRemainsRootRelative()
    {
        const string before = "<html style='font-size:15pt'><body style='font-size:9pt'>" +
            "<p style='font-size:10pt;background:#f0f6fa;width:";
        const string after = "'>Service entry</p></body></html>";
        Assert.That(Pdf(before + "8em" + after), Is.EqualTo(Pdf(before + "80pt" + after)));
        Assert.That(Pdf(before + "8rem" + after), Is.EqualTo(Pdf(before + "120pt" + after)));
        Assert.That(Pdf(before + "8em" + after), Is.Not.EqualTo(Pdf(before + "8rem" + after)));
    }

    [Test]
    public void MultiPageRowsKeepEmMarginsPaddingTextAndPaint()
    {
        var relative = string.Concat(Enumerable.Range(1, 22).Select(i =>
            $"<p style='font-size:10pt;margin:0.2em 0.5em 0.3em;" +
            $"padding:0.1em;background:#f0f6fa'>Service entry {i:00}</p>"));
        var points = string.Concat(Enumerable.Range(1, 22).Select(i =>
            $"<p style='font-size:10pt;margin:2pt 5pt 3pt;" +
            $"padding:1pt;background:#f0f6fa'>Service entry {i:00}</p>"));
        const string open = "<html style='font-size:15pt'><body>";
        const string close = "</body></html>";
        var html = open + relative + close;
        Assert.That(Pdf(html), Is.EqualTo(Pdf(open + points + close)));
        var pages = Layout(html);
        Assert.That(pages.Count, Is.GreaterThan(1));
        Assert.That(pages.SelectMany(p => p.Runs).Select(r => r.Text),
            Is.EqualTo(Enumerable.Range(1, 22).Select(i => $"Service entry {i:00}")));
        Assert.That(pages.SelectMany(p => p.PaintBoxes)
            .Where(p => p.Style.BackgroundColor.HasValue)
            .All(p => Math.Abs(p.X - 25) < 0.00001), Is.True);
    }

    [Test]
    public void ExplicitTrueTypeGlyphsAndPaginatedBoxesMatchIndependentPointLengths()
    {
        // A generated test-only font resource exercises the real TrueType path.
        // Neither an external font nor browser rendering is needed.
        var options = Page with { Fonts = [PdfFont.LoadTrueType(FontFixture.Create())] };
        var body = string.Concat(Enumerable.Repeat("<p>AB WA BA</p>", 18));
        const string before = "<html style='font-size:15pt'><body>";
        const string relative = "<section style='font-size:12pt;width:12em;padding:0.5em;" +
            "background:#e0e9f1'><p style='font-size:12pt;margin:0.25em'>AB WA BA</p>";
        const string absolute = "<section style='font-size:12pt;width:144pt;padding:6pt;" +
            "background:#e0e9f1'><p style='font-size:12pt;margin:3pt'>AB WA BA</p>";
        const string end = "</section></body></html>";
        // Nested p and a repeated body both use the same own computed font.
        var em = before + relative + body + end;
        var pt = before + absolute + body + end;
        using var actual = new MemoryStream();
        using var expected = new MemoryStream();
        PdfConverter.Convert(em, actual, options);
        PdfConverter.Convert(pt, expected, options);
        Assert.That(actual.ToArray(), Is.EqualTo(expected.ToArray()));
        var pages = TextLayout.Layout(HtmlDocumentReader.ReadTree(em, options, default),
            options, default);
        Assert.That(pages.Count, Is.GreaterThan(1));
        Assert.That(pages.SelectMany(p => p.Runs).Select(r => r.Text)
            .Count(s => s == "AB WA BA"), Is.EqualTo(19));
    }

    [Test]
    public void ZeroEmMaintainsDefaultPdfBytes()
        => Assert.That(Pdf("<p>Report</p>"), Is.EqualTo(Pdf("<p style='padding:0em'>Report</p>")));

    [TestCase("<p style='width:-1em'>A</p>", "FPDF1202")]
    [TestCase("<p style='padding:20000em'>A</p>", "FPDF1202")]
    [TestCase("<html style='font-size:144pt'><body><p style='width:101em'>A</p></body></html>", "FPDF1202")]
    [TestCase("<style>.unused{margin:-1em}</style><p>A</p>", "FPDF1202")]
    [TestCase("<p style='margin:calc(1em + 1pt)'>A</p>", "FPDF1202")]
    [TestCase("<p style='text-indent:-1em'>A</p>", "FPDF1202")]
    [TestCase("<p style='border-width:1em'>A</p>", "FPDF1202")]
    [TestCase("<img src='logo' style='width:1em'>", "FPDF1201")]
    [TestCase("<table style='padding:1em'>", "FPDF1201")]
    [TestCase("<table><colgroup><col style='width:1em'></colgroup><tbody><tr><td>A</td></tr></tbody></table>", "FPDF1201")]
    [TestCase("<table><colgroup><col style='width:100pt'></colgroup><tbody><tr><td><p style='margin-left:1em'>A</p></td></tr></tbody></table>", "FPDF1201")]
    [TestCase("<p style='font-size:10pt;width:4em'>IMPORTANT</p>", "FPDF1302")]
    public void UnsupportedOrOverconstrainedEmDoesNotAlterExistingDestination(string html, string code)
        => Reject(html, code);
}
