using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>Bounded inherited list marker placement in searchable, paginated report PDFs.</summary>
[TestFixture]
public sealed class ListMarkerPositionTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 150, PageHeight = 145, Margin = 20 };

    private static List<LayoutPage> Layout(string html, PdfOptions? options = null)
    {
        var o = options ?? Page;
        return TextLayout.Layout(HtmlDocumentReader.ReadTree(html, o, default), o, default);
    }

    private static byte[] Pdf(string html, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, options ?? Page);
        return output.ToArray();
    }

    [Test]
    public void InsideMarkerReservesFirstLineAndWrappedContinuationReturnsToContentEdge()
    {
        const string inside = "<ol style='list-style-position:inside'><li>Alpha Beta Gamma Delta Epsilon</li></ol>";
        var lines = Layout(inside).SelectMany(p => p.Runs).ToArray();
        var marker = lines.Single(r => r.Text == "1.");
        var body = lines.Where(r => r.Text != "1.").ToArray();
        Assert.That(body.Length, Is.GreaterThan(1));
        Assert.That(marker.Baseline, Is.EqualTo(body[0].Baseline).Within(0.000001));
        Assert.That(marker.X + marker.Width, Is.LessThan(body[0].X));
        Assert.That(body[0].X, Is.GreaterThan(body[1].X + 5));
        Assert.That(marker.X, Is.GreaterThanOrEqualTo(Page.Margin));
        Assert.That(Pdf(inside).AsSpan(0, 4).SequenceEqual("%PDF"u8), Is.True);
    }

    [Test]
    public void FirstWordCanFollowAnInsideMarkerOnTheNextLineWithoutTruncation()
    {
        const string html = "<div style='width:100pt'><ol style='list-style-position:inside'><li>ABCDEFGHIJKL</li></ol></div>";
        var runs = Layout(html).SelectMany(p => p.Runs).ToArray();
        var marker = runs.Single(r => r.Text == "1.");
        var word = runs.Single(r => r.Text == "ABCDEFGHIJKL");
        Assert.That(marker.Baseline, Is.GreaterThan(word.Baseline));
        Assert.That(word.X, Is.EqualTo(marker.X).Within(0.000001));
        Assert.That(Pdf(html).AsSpan(0, 4).SequenceEqual("%PDF"u8), Is.True);
    }

    [Test]
    public void InsideMarkerPrecedesBlockChildInsteadOfEnteringItsNarrowContentBox()
    {
        const string html = "<ol style='list-style-position:inside'><li><p style='width:15pt'>A</p></li></ol>";
        var runs = Layout(html).SelectMany(p => p.Runs).ToArray();
        var marker = runs.Single(r => r.Text == "1.");
        var word = runs.Single(r => r.Text == "A");
        Assert.That(marker.Baseline, Is.GreaterThan(word.Baseline));
        Assert.That(word.X, Is.EqualTo(marker.X).Within(0.000001));
        Assert.That(Pdf(html).AsSpan(0, 4).SequenceEqual("%PDF"u8), Is.True);
    }

    [Test]
    public void AuthoredTextIndentStillRejectsFirstWordThatCannotFit()
    {
        const string html = "<p style='width:100pt;text-indent:20pt'>ABCDEFGHIJKL</p>";
        var error = Assert.Throws<FactsPdfException>(() => Pdf(html));
        Assert.That(error!.Code, Is.EqualTo("FPDF1302"));
    }

    [Test]
    public void OutsideIsDefaultAndRetainsIdenticalPdfBytes()
    {
        const string body = "<li>Alpha Beta Gamma Delta</li><li>Small details</li>";
        foreach (var tag in new[] { "ol", "ul" })
        {
            var implicitHtml = "<" + tag + ">" + body + "</" + tag + ">";
            var explicitHtml = "<" + tag + " style='list-style-position:outside'>" + body + "</" + tag + ">";
            Assert.That(Pdf(explicitHtml), Is.EqualTo(Pdf(implicitHtml)), tag);
        }
    }

    [Test]
    public void ShorthandAndLonghandGenerateSameActualPdfIncludingInheritanceAndImportant()
    {
        const string items = "<li>Alpha Beta Gamma Delta</li><li>Second example</li>";
        const string actual = "<style>ol{list-style:upper-alpha inside!important}li{list-style-position:inherit}</style>" +
            "<ol style='list-style:decimal outside'>" + items + "</ol>";
        const string reference = "<ol style='list-style-type:upper-alpha;list-style-position:inside'>" + items + "</ol>";
        Assert.That(Pdf(actual), Is.EqualTo(Pdf(reference)));
        Assert.That(Pdf("<ol style='list-style:inside'>" + items + "</ol>"),
            Is.EqualTo(Pdf("<ol style='list-style-type:disc;list-style-position:inside'>" + items + "</ol>")));
        Assert.That(Pdf("<ol style='list-style:upper-roman inside'>" + items + "</ol>"),
            Is.EqualTo(Pdf("<ol style='list-style-type:upper-roman;list-style-position:inside'>" + items + "</ol>")));
        Assert.That(Pdf("<ul style='list-style:circle inside'>" + items + "</ul>"),
            Is.EqualTo(Pdf("<ul style='list-style-type:circle;list-style-position:inside'>" + items + "</ul>")));
    }

    [Test]
    public void MixedInsideAndOutsideItemsHaveDistinctRealGeometryAndOrderedMarkers()
    {
        const string html = "<ol style='list-style-position:inside'>" +
            "<li>Alpha Beta Gamma Delta</li>" +
            "<li style='list-style-position:outside'>Second</li>" +
            "<li>Third</li></ol>";
        var runs = Layout(html).SelectMany(p => p.Runs).ToArray();
        Assert.That(runs.Where(r => r.Text.EndsWith('.')).Select(r => r.Text),
            Is.EqualTo(new[] { "1.", "2.", "3." }));
        var body = runs.Where(r => !r.Text.EndsWith('.')).ToArray();
        Assert.That(body.Length, Is.GreaterThanOrEqualTo(3));
        Assert.That(Pdf(html).Length, Is.GreaterThan(300));
    }

    [Test]
    public void FortyFourRecordReportSpansRealPagesAndMatchesIndependentLonghandControl()
    {
        var items = string.Concat(Enumerable.Range(1, 44).Select(i =>
            "<li>Inspection record " + i + " checked and approved</li>"));
        var shorthand = "<ol style='list-style:decimal inside'>" + items + "</ol>";
        var longhand = "<ol style='list-style-type:decimal;list-style-position:inside'>" + items + "</ol>";
        var pages = Layout(shorthand);
        Assert.That(pages.Count, Is.GreaterThan(2));
        Assert.That(pages.SelectMany(p => p.Runs).Count(r => r.Text.EndsWith('.')), Is.EqualTo(44));
        Assert.That(Pdf(shorthand), Is.EqualTo(Pdf(longhand)));
    }

    [TestCase("<ol style='list-style-position:middle'><li>A</li></ol>", "FPDF1202")]
    [TestCase("<ol style='list-style:inside outside'><li>A</li></ol>", "FPDF1202")]
    [TestCase("<ol style='list-style:decimal circle'><li>A</li></ol>", "FPDF1202")]
    [TestCase("<ol style='list-style:disc url(x.png)'><li>A</li></ol>", "FPDF1202")]
    [TestCase("<p style='list-style-position:inside'>A</p>", "FPDF1201")]
    [TestCase("<style>.unmatched{list-style-position:centre}</style><ol><li>A</li></ol>", "FPDF1202")]
    public void InvalidPositionsAndContextsPreserveDestination(string html, string code)
    {
        using var output = new MemoryStream();
        output.Write("original"u8);
        output.Position = 2;
        var ex = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, Page));
        Assert.That(ex!.Code, Is.EqualTo(code));
        Assert.That(output.ToArray(), Is.EqualTo("original"u8.ToArray()));
        Assert.That(output.Position, Is.EqualTo(2));
    }
}
