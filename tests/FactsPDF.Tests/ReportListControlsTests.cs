using NUnit.Framework;

namespace FactsPDF.Tests;

[TestFixture]
public sealed class ReportListControlsTests
{
    private static readonly PdfOptions Page = new()
    {
        PageWidth = 300, PageHeight = 145, Margin = 20
    };

    private static List<LayoutPage> Layout(string html, PdfOptions? options = null)
    {
        var effective = options ?? Page;
        return TextLayout.Layout(HtmlDocumentReader.ReadTree(html, effective, default), effective, default);
    }

    private static byte[] Pdf(string html, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, options ?? Page);
        return output.ToArray();
    }

    private static string[] TextMarkers(string html, PdfOptions? options = null)
        => Layout(html, options).SelectMany(page => page.Runs)
            .Where(run => run.Text.EndsWith(".", StringComparison.Ordinal))
            .Select(run => run.Text).ToArray();

    private static void Rejects(string html, string code)
    {
        using var output = new MemoryStream();
        output.Write("sentinel"u8);
        output.Position = 3;
        var ex = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, Page));
        Assert.That(ex!.Code, Is.EqualTo(code));
        Assert.That(ex.SourceOffset, Is.GreaterThanOrEqualTo(0));
        Assert.That(output.ToArray(), Is.EqualTo("sentinel"u8.ToArray()));
        Assert.That(output.Position, Is.EqualTo(3));
    }

    [Test]
    public void StartAndItemValueChangeRealMarkersButKeepSequenceThroughNone()
    {
        const string html = "<ol start='24'><li>A</li><li value='31'>B</li>" +
            "<li style='list-style:none'>C</li><li>D</li></ol>";
        Assert.That(TextMarkers(html), Is.EqualTo(new[] { "24.", "31.", "33." }));
        Assert.That(Layout(html).SelectMany(page => page.Runs).Count(run => run.Text == "C"), Is.EqualTo(1));
        Assert.That(Pdf(html).AsSpan(0, 4).SequenceEqual("%PDF"u8), Is.True);
    }

    [Test]
    public void ReversedDefaultAndExplicitSequenceUseSupportedMarkerTypeAndRealPagination()
    {
        const string simple = "<ol reversed style='list-style:upper-alpha'>" +
            "<li>A</li><li>B</li><li>C</li></ol>";
        Assert.That(TextMarkers(simple), Is.EqualTo(new[] { "C.", "B.", "A." }));

        const string explicitStart = "<ol reversed='reversed' start='10' style='list-style:lower-roman'>" +
            "<li>A</li><li value='6'>B</li><li>C</li></ol>";
        Assert.That(TextMarkers(explicitStart), Is.EqualTo(new[] { "x.", "vi.", "v." }));

        var multi = "<ol reversed start='42' style='list-style:decimal'>" +
            string.Concat(Enumerable.Range(1, 30).Select(i => "<li>Entry" + i + "</li>")) + "</ol>";
        var pages = Layout(multi, Page with { PageHeight = 95 });
        Assert.That(pages, Has.Count.GreaterThan(2));
        Assert.That(TextMarkers(multi, Page with { PageHeight = 95 }),
            Is.EqualTo(Enumerable.Range(13, 30).Reverse().Select(i => i + ".")));
        Assert.That(Pdf(multi, Page with { PageHeight = 95 }).Length, Is.GreaterThan(300));
    }

    [Test]
    public void NestedListsAndStandaloneListsDoNotShareCounters()
    {
        const string html = "<ol start='11'><li>A<ol reversed><li>B</li><li>C</li></ol>D</li>" +
            "<li value='20'>E</li></ol><ol><li>F</li></ol>";
        Assert.That(TextMarkers(html), Is.EqualTo(new[] { "11.", "2.", "1.", "20.", "1." }));
    }

    [TestCase("disc")]
    [TestCase("circle")]
    [TestCase("square")]
    [TestCase("decimal")]
    [TestCase("lower-alpha")]
    [TestCase("upper-roman")]
    [TestCase("none")]
    [TestCase("inherit")]
    [TestCase("initial")]
    [TestCase("unset")]
    public void SingleValueListStyleShorthandSharesTheExactExistingPdfRenderer(string type)
    {
        var actual = "<ol style='list-style:" + type + "'><li>A</li><li>B</li></ol>";
        var expected = "<ol style='list-style-type:" + type + "'><li>A</li><li>B</li></ol>";
        Assert.That(Pdf(actual), Is.EqualTo(Pdf(expected)));
    }

    [Test]
    public void ShorthandObeysStylesheetCascadeImportantAndNestedInheritance()
    {
        const string actual = "<style>ol{list-style:upper-alpha!important}li{list-style:inherit}</style>" +
            "<ol style='list-style:decimal'><li>A</li><li>B</li></ol>";
        const string reference = "<ol style='list-style-type:upper-alpha'><li>A</li><li>B</li></ol>";
        Assert.That(Pdf(actual), Is.EqualTo(Pdf(reference)));
        Assert.That(TextMarkers(actual), Is.EqualTo(new[] { "A.", "B." }));
    }

    [Test]
    public void DefaultListsRemainByteIdenticalToExplicitStartAndShorthand()
    {
        Assert.That(Pdf("<ol><li>A</li><li>B</li></ol>"),
            Is.EqualTo(Pdf("<ol start='1' style='list-style:decimal'><li>A</li><li>B</li></ol>")));
        Assert.That(Pdf("<ul><li>A</li><li>B</li></ul>"),
            Is.EqualTo(Pdf("<ul style='list-style:disc'><li>A</li><li>B</li></ul>")));
    }

    [Test]
    public void RejectsInvalidAttributesContextsValuesAndUnmatchedRulesWithRecovery()
    {
        Rejects("<ol start='0'><li>A</li></ol>", "FPDF1103");
        Rejects("<ol start='-1'><li>A</li></ol>", "FPDF1103");
        Rejects("<ol start='1.5'><li>A</li></ol>", "FPDF1103");
        Rejects("<ol start='1000000'><li>A</li></ol>", "FPDF1103");
        Rejects("<ol start='999999'><li>A</li><li>B</li></ol>", "FPDF1103");
        Rejects("<ol reversed='false'><li>A</li></ol>", "FPDF1103");
        Rejects("<ol reversed start='1'><li>A</li><li>B</li></ol>", "FPDF1103");
        Rejects("<ol><li value='0'>A</li></ol>", "FPDF1103");
        Rejects("<ul><li value='2'>A</li></ul>", "FPDF1103");
        Rejects("<ul start='2'><li>A</li></ul>", "FPDF1103");
        Rejects("<ol style='list-style:circle inside'><li>A</li></ol>", "FPDF1202");
        Rejects("<ol style='list-style:disc url(icon.png)'><li>A</li></ol>", "FPDF1202");
        Rejects("<ol style='list-style:diamond'><li>A</li></ol>", "FPDF1202");
        Rejects("<p style='list-style:none'>A</p>", "FPDF1201");
        Rejects("<style>.unused{list-style:outside}</style><ol><li>A</li></ol>", "FPDF1202");
        Assert.That(Pdf("<ol start='11'><li>A</li></ol>").Length, Is.GreaterThan(300));
    }

    [Test]
    public void CancellationAndPageBudgetProtectTheExistingDestination()
    {
        using var output = new MemoryStream();
        output.Write("sentinel"u8); output.Position = 2;
        Assert.Throws<OperationCanceledException>(() =>
            PdfConverter.Convert("<ol start='5'><li>A</li></ol>", output, Page, new CancellationToken(true)));
        Assert.That(output.ToArray(), Is.EqualTo("sentinel"u8.ToArray()));
        Assert.That(output.Position, Is.EqualTo(2));
        using var limited = new MemoryStream();
        limited.Write("sentinel"u8); limited.Position = 2;
        var html = "<ol start='30'>" +
            string.Concat(Enumerable.Range(1, 50).Select(i => "<li>Entry" + i + "</li>")) + "</ol>";
        var ex = Assert.Throws<FactsPdfException>(() =>
            PdfConverter.Convert(html, limited, Page with { MaxPages = 1 }));
        Assert.That(ex!.Code, Is.EqualTo("FPDF1303"));
        Assert.That(limited.ToArray(), Is.EqualTo("sentinel"u8.ToArray()));
        Assert.That(limited.Position, Is.EqualTo(2));
    }
}
