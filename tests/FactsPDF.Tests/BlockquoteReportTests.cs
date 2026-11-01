using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>Source-only HTML quoted-report panels, never a general browser quote model.</summary>
[TestFixture]
public sealed class BlockquoteReportTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 320, PageHeight = 240, Margin = 20 };

    private static byte[] Render(string html, PdfOptions? settings = null)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, settings ?? Page);
        return output.ToArray();
    }

    private static List<LayoutPage> Layout(string html, PdfOptions? settings = null)
    {
        var options = settings ?? Page;
        return TextLayout.Layout(HtmlDocumentReader.ReadTree(html, options, default), options, default);
    }

    private static void Reject(string html, string code, PdfOptions? options = null)
    {
        using var stream = new MemoryStream();
        stream.Write("retained-pdf"u8);
        stream.Position = 3;
        var ex = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, stream, options ?? Page));
        Assert.That(ex!.Code, Is.EqualTo(code));
        Assert.That(stream.ToArray(), Is.EqualTo("retained-pdf"u8.ToArray()));
        Assert.That(stream.Position, Is.EqualTo(3));
    }

    [Test]
    public void DefaultQuoteInsertsTrueOuterInsetsAndVerticalGaps()
    {
        const string start = "<p style='margin:0'>BEFORE</p>";
        const string finish = "<p style='margin:0'>AFTER</p>";
        const string content = "<p style='margin:0'>A quoted service statement</p>";
        var actual = start + "<blockquote>" + content + "</blockquote>" + finish;
        var oracle = start + "<div style='margin:8pt 24pt'>" + content + "</div>" + finish;
        Assert.That(Render(actual), Is.EqualTo(Render(oracle)));
        var page = Layout(actual).Single();
        var quote = page.Runs.Single(run => run.Text.Contains("quoted"));
        Assert.That(quote.X, Is.EqualTo(44).Within(0.000001));
        Assert.That(page.Runs.Select(run => run.Text),
            Is.EquivalentTo(new[] { "BEFORE", "A quoted service statement", "AFTER" }));
    }

    [Test]
    public void StylesheetCascadesPaintAndAutoCentresTheActualPdfQuoteBox()
    {
        const string css = "<style>blockquote.quote{width:170pt;margin:7pt auto 11pt;" +
            "box-sizing:border-box;padding:6pt 8pt;border-left:3pt solid #225588;background:#edf4f8}" +
            "blockquote.quote p{margin:0}blockquote.quote{margin-top:10pt!important}</style>";
        const string actual = css + "<blockquote class='quote' style='margin-top:1pt'>" +
            "<p>Audited quote</p></blockquote>";
        const string oracle = "<div style='width:170pt;margin:10pt auto 11pt;" +
            "box-sizing:border-box;padding:6pt 8pt;border-left:3pt solid #225588;background:#edf4f8'>" +
            "<p style='margin:0'>Audited quote</p></div>";
        Assert.That(Render(actual), Is.EqualTo(Render(oracle)));
        var page = Layout(actual).Single();
        Assert.That(page.PaintBoxes, Has.Count.EqualTo(1));
        Assert.That(page.PaintBoxes[0].X, Is.EqualTo(75).Within(0.000001));
        Assert.That(page.Runs.Single().X, Is.EqualTo(86).Within(0.000001));
    }

    [Test]
    public void LongQuoteContinuesAcrossPagesWithBorderPaintAndSearchableText()
    {
        var sentences = string.Concat(Enumerable.Range(1, 42)
            .Select(i => $"<p style='margin:0 0 4pt'>Quoted service item {i:00}: check completed.</p>"));
        var content = "<p style='margin:0'>QUOTATION OPEN</p>" + sentences +
            "<p style='margin:0'>QUOTATION CLOSED</p>";
        var actual = "<blockquote style='margin:9pt 15pt 11pt;padding:8pt;" +
            "border-left:2pt solid #345678;background:#eeeeee'>" + content + "</blockquote>";
        var oracle = "<div style='margin:9pt 15pt 11pt;padding:8pt;" +
            "border-left:2pt solid #345678;background:#eeeeee'>" + content + "</div>";
        Assert.That(Render(actual), Is.EqualTo(Render(oracle)));
        var pages = Layout(actual);
        Assert.That(pages.Count, Is.GreaterThan(2));
        Assert.That(pages.SelectMany(p => p.PaintBoxes).Count(), Is.GreaterThan(2));
        var text = string.Concat(pages.SelectMany(page => page.Runs).Select(run => run.Text));
        Assert.That(text, Does.Contain("Quoted service item 01"));
        Assert.That(text, Does.Contain("Quoted service item 42"));
        Assert.That(text, Does.Contain("QUOTATION CLOSED"));
    }

    [Test]
    public void RoundedQuoteBackgroundAndUniformBorderMatchAnIndependentlyStyledDiv()
    {
        const string card = "style='margin:8pt 24pt;padding:5pt;background:#edf5fa;" +
            "border:2pt solid #23446b;border-radius:12pt'";
        var quote = "<blockquote " + card + "><p style='margin:0'>Quoted receipt</p></blockquote>";
        var reference = "<div " + card + "><p style='margin:0'>Quoted receipt</p></div>";
        Assert.That(Render(quote), Is.EqualTo(Render(reference)),
            "Scoped quote radii must shape actual PDF paths, not merely parse.");
        Assert.That(Layout(quote).Single().PaintBoxes.Single().CommandCount, Is.EqualTo(2));
        Reject("<blockquote style='border-radius:12pt;border:2pt dashed red'>No</blockquote>", "FPDF1201");
    }

    [Test]
    public void QuoteCanInheritAParentMarginOrResetDefaultWithoutFakingInheritance()
    {
        const string actual = "<section style='margin-top:13pt'>" +
            "<blockquote style='margin-top:inherit;margin-right:initial;margin-left:0'>" +
            "<p style='margin:0'>Inherited quote</p></blockquote></section>";
        const string oracle = "<section style='margin-top:13pt'>" +
            "<div style='margin:13pt 0 8pt 0'><p style='margin:0'>Inherited quote</p></div></section>";
        Assert.That(Render(actual), Is.EqualTo(Render(oracle)));
    }

    [TestCase("<ol><blockquote>quote</blockquote></ol>", "FPDF1101")]
    [TestCase("<div><span><blockquote>quote</blockquote></span></div>", "FPDF1101")]
    [TestCase("<blockquote><p>unclosed</p>", "FPDF1101")]
    [TestCase("<blockquote cite='https://example.com'>quote</blockquote>", "FPDF1103")]
    [TestCase("<blockquote style='margin-top:-2pt'>quote</blockquote>", "FPDF1202")]
    [TestCase("<blockquote style='margin-top:10%'>quote</blockquote>", "FPDF1202")]
    [TestCase("<blockquote style='margin-left:calc(1pt + 2pt)'>quote</blockquote>", "FPDF1202")]
    [TestCase("<blockquote style='border-radius:6%'>quote</blockquote>", "FPDF1202")]
    [TestCase("<blockquote style='width:310pt'>quote</blockquote>", "FPDF1302")]
    public void InvalidNestingStylesAndGeometryPreserveExistingOutput(string html, string code)
        => Reject(html, code);

    [Test]
    public void ElementDepthAndUnmatchedStylesheetDeclarationsRetainStrictBudgets()
    {
        Reject("<div><blockquote><p>deep</p></blockquote></div>", "FPDF1003",
            Page with { MaxDepth = 2 });
        Reject("<style>blockquote{margin:5pt 0;margin:4pt 0}</style>" +
            "<blockquote>X</blockquote>", "FPDF1205", Page with { MaxCssDeclarations = 3 });
    }
}
