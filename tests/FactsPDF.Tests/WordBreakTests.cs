using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>Real glyph-breaking and protected output for scoped business report identifiers.</summary>
[TestFixture]
public sealed class WordBreakTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 180, PageHeight = 220, Margin = 20 };

    private static List<PlacedText> Placed(string html, PdfOptions? page = null)
    {
        var options = page ?? Page;
        return TextLayout.Layout(HtmlDocumentReader.ReadTree(html, options, default), options, default)
            .SelectMany(p => p.Runs).ToList();
    }

    private static byte[] Pdf(string html, PdfOptions? page = null)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, page ?? Page);
        return output.ToArray();
    }

    private static void Reject(string html, string code, PdfOptions? page = null)
    {
        using var output = new MemoryStream();
        output.Write("existing-pdf"u8);
        output.Position = 3;
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, page ?? Page));
        Assert.That(error!.Code, Is.EqualTo(code));
        Assert.That(output.ToArray(), Is.EqualTo("existing-pdf"u8.ToArray()));
        Assert.That(output.Position, Is.EqualTo(3));
        Assert.That(Pdf("<p>Recovery</p>").Length, Is.GreaterThan(100));
    }

    [Test]
    public void BreakAllFillsTheRemainingLineInsteadOfMovingAWordThatFitsFresh()
    {
        const string text = "PAID EXAMPLE";
        var actual = "<p style='width:60pt;word-break:break-all'>" + text + "</p>";
        var normal = "<p style='width:60pt;word-break:normal'>" + text + "</p>";
        Assert.That(Placed(actual).Select(x => x.Text), Is.EqualTo(new[] { "PAID EXA", "MPLE" }));
        Assert.That(Placed(normal).Select(x => x.Text), Is.EqualTo(new[] { "PAID", "EXAMPLE" }));
        Assert.That(Pdf(actual), Is.EqualTo(Pdf("<p style='width:60pt'>PAID EXA<br>MPLE</p>")),
            "The measured PDF must equal independently authored manual line breaks.");
        Assert.That(Pdf(normal), Is.EqualTo(Pdf("<p style='width:60pt;overflow-wrap:break-word'>" + text + "</p>")));
        Assert.That(Placed(actual).All(x => x.Width <= 60.000001), Is.True);
    }

    [Test]
    public void DefaultAndFittingTextRetainHistoricalPDFBytes()
    {
        const string plain = "<p>PAID IN FULL</p>";
        Assert.That(Pdf("<p style='word-break:normal'>PAID IN FULL</p>"), Is.EqualTo(Pdf(plain)));
        Assert.That(Pdf("<p style='word-break:break-all'>PAID IN FULL</p>"), Is.EqualTo(Pdf(plain)));
    }

    [Test]
    public void CascadeImportantAndCssWideValuesDoNotLeakIntoOtherParagraphs()
    {
        const string html = "<style>p{word-break:break-all!important}" +
            ".reset{word-break:normal!important}.initial{word-break:initial!important}" +
            ".unset{word-break:unset!important}</style>" +
            "<p>ALPHA</p><p class=reset>BRAVO</p><p class=initial>CHARLIE</p>" +
            "<p class=unset>DELTA</p><h1 style='word-break:inherit'>ECHO</h1>";
        var paragraphs = HtmlDocumentReader.Read(html, Page, default);
        Assert.That(paragraphs.Select(p => p.Style.BreakAll),
            Is.EqualTo(new[] { true, false, false, false, false }));
    }

    [Test]
    public void FlatTableCellParagraphRespectsResolvedColumnWidth()
    {
        const string html = "<table style='width:60pt'><colgroup><col style='width:60pt'></colgroup>" +
            "<tbody><tr><td><p style='word-break:break-all'>PAID EXAMPLE</p></td></tr></tbody></table>";
        var page = Page with { PageWidth = 220, PageHeight = 400 };
        Assert.That(Placed(html, page).Select(x => x.Text), Is.EqualTo(new[] { "PAID EXA", "MPLE" }));
        Assert.That(Pdf(html, page), Is.Not.Empty);
    }

    [Test]
    public void FortyEightRecordPDFEqualsIndependentManualBreakDocumentAcrossPages()
    {
        var report = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "examples", "report-word-break-identifiers.html"));
        var oracle = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "examples", "report-word-break-identifiers-oracle.html"));
        var page = new PdfOptions();
        Assert.That(Pdf(report, page), Is.EqualTo(Pdf(oracle, page)));
        var pages = TextLayout.Layout(HtmlDocumentReader.ReadTree(report, page, default), page, default);
        Assert.That(pages.Count, Is.GreaterThan(1));
        var text = string.Concat(pages.SelectMany(p => p.Runs).Select(x => x.Text));
        Assert.That(text, Does.Contain("REC-01"));
        Assert.That(text, Does.Contain("REC-48"));
        Assert.That(pages.SelectMany(p => p.Runs).Count(x => x.Text == "PAID EXA"), Is.EqualTo(48));
    }

    [Test]
    public void NoWrapAndOversizedGlyphsStillFailBeforeReplacingCustomerOutput()
    {
        Reject("<p style='width:60pt;white-space:nowrap;word-break:break-all'>PAID EXAMPLE</p>", "FPDF1302");
        Reject("<p style='width:20pt;font-size:100pt;word-break:break-all'>W</p>", "FPDF1302");
        Reject("<p style='width:20pt;word-break:break-all'>ABCDE 中文</p>", "FPDF1301");
    }

    [TestCase("<span style='word-break:break-all'>A</span>", "FPDF1201")]
    [TestCase("<div style='word-break:break-all'><p>A</p></div>", "FPDF1201")]
    [TestCase("<p style='word-break:keep-all'>A</p>", "FPDF1202")]
    [TestCase("<p style='word-break:break-word'>A</p>", "FPDF1202")]
    [TestCase("<p style='word-break:normal break-all'>A</p>", "FPDF1202")]
    [TestCase("<p style='word-break:calc(1)'>A</p>", "FPDF1202")]
    [TestCase("<style>.absent{word-break:keep-all}</style><p>A</p>", "FPDF1202")]
    public void UnsupportedContextsAndValuesKeepExistingPDFIntact(string html, string code)
        => Reject(html, code);
}
