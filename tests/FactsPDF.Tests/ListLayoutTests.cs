using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace FactsPDF.Tests;

[TestFixture]
public sealed class ListLayoutTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 140, Margin = 20 };
    private static bool Marker(PlacedText run) => Regex.IsMatch(run.Text, @"^\d+\.$");
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
    private static void Fails(string html, string code, PdfOptions? options = null, bool source = false)
    {
        using var output = new MemoryStream(); output.Write("sentinel"u8); output.Position = 2;
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, options ?? Page));
        Assert.That(error!.Code, Is.EqualTo(code));
        Assert.That(output.ToArray(), Is.EqualTo("sentinel"u8.ToArray()));
        Assert.That(output.Position, Is.EqualTo(2));
        if (source) Assert.That(error.SourceOffset, Is.GreaterThanOrEqualTo(0));
    }

    [TestCase(12, 27.6)]
    [TestCase(100, 34.8)]
    public void LaterWideMarkerSetsUniformSiblingGutter(int count, double gutter)
    {
        var html = "<ol>" + string.Concat(Enumerable.Range(1, count).Select(i => $"<li>Row{i}</li>")) + "</ol>";
        var runs = Layout(html, Page with { PageHeight = 3000 }).SelectMany(p => p.Runs).ToArray();
        Assert.That(runs.Where(Marker).Select(r => r.Text), Is.EqualTo(Enumerable.Range(1, count).Select(i => $"{i}.")));
        Assert.That(runs.Where(r => !Marker(r)).Select(r => r.X), Is.All.EqualTo(20 + gutter).Within(0.000001));
        foreach (var marker in runs.Where(Marker))
            Assert.That(marker.X + marker.Text.Length * 7.2, Is.EqualTo(20 + gutter - 6).Within(0.000001));
    }

    [Test]
    public void WrappedLinesAndLaterParagraphsKeepItemContentIndent()
    {
        var runs = Layout("<ol><li style='width:80pt'>alpha beta gamma delta epsilon<p>later</p></li></ol>")
            .SelectMany(p => p.Runs).ToArray();
        Assert.That(runs.Count(Marker), Is.EqualTo(1));
        Assert.That(runs.Where(r => !Marker(r)).Count(), Is.GreaterThan(2));
        Assert.That(runs.Where(r => !Marker(r)).Select(r => r.X), Is.All.EqualTo(44).Within(0.000001));
    }

    [Test]
    public void NestedListsHaveIndependentCountersAndAddTheirOwnGutter()
    {
        var runs = Layout("<ol><li>A<ol><li>B</li><li>C</li></ol>D</li><li>E</li></ol><ol><li>F</li></ol>")
            .SelectMany(p => p.Runs).ToArray();
        Assert.That(runs.Where(Marker).Select(r => r.Text), Is.EqualTo(new[] { "1.", "1.", "2.", "2.", "1." }));
        Assert.That(runs.Single(r => r.Text == "B").X, Is.EqualTo(68));
        Assert.That(runs.Where(r => r.Text is "A" or "D" or "E" or "F").Select(r => r.X), Is.All.EqualTo(44));
    }

    [TestCase("left", 55)]
    [TestCase("center", 106.8)]
    [TestCase("right", 158.6)]
    public void PercentItemWidthUsesReducedContainingWidthAndMarkerStaysInGutter(string align, double x)
    {
        var runs = Layout($"<ol><li style='width:50%;padding-left:8pt;border:3pt solid red;text-align:{align}'>AA</li></ol>")[0].Runs;
        Assert.That(runs.Single(r => r.Text == "AA").X, Is.EqualTo(x).Within(0.000001));
        Assert.That(runs.Single(Marker).X, Is.EqualTo(23.6).Within(0.000001));
    }

    [Test]
    public void MarkerAndFirstLineMoveToNextPageTogether()
    {
        var pages = Layout("<p style='line-height:4'>Lead</p><ol><li>Alpha</li><li>Beta</li></ol>", Page with { PageHeight = 100 });
        Assert.That(pages, Has.Count.EqualTo(2));
        Assert.That(pages[0].Runs.Select(r => r.Text), Is.EqualTo(new[] { "Lead" }));
        Assert.That(pages[1].Runs.Select(r => r.Text), Is.EqualTo(new[] { "1.", "Alpha", "2.", "Beta" }));
        Assert.That(pages[1].Runs[0].Baseline, Is.EqualTo(pages[1].Runs[1].Baseline));
    }

    [Test]
    public void LongItemSpansPagesWithoutRepeatedMarker()
    {
        var words = string.Join(" ", Enumerable.Repeat("alpha", 50));
        var pages = Layout($"<ol><li style='width:90pt'>{words}</li><li>End</li></ol>", Page with { PageHeight = 100 });
        Assert.That(pages.Count, Is.GreaterThan(2));
        Assert.That(pages.SelectMany(p => p.Runs).Where(Marker).Select(r => r.Text), Is.EqualTo(new[] { "1.", "2." }));
        Assert.That(pages.Skip(1).Take(pages.Count - 2).SelectMany(p => p.Runs).Any(Marker), Is.False);
        Assert.That(pages.All(p => p.Runs.Count > 0), Is.True);
    }

    [Test]
    public void EmptyFirstParagraphGetsLocalStrutLineWithoutStealingLaterBaseline()
    {
        var runs = Layout("<ol><li><p></p><p>A</p></li></ol>")[0].Runs;
        Assert.That(runs.Select(r => r.Text), Is.EqualTo(new[] { "1.", "A" }));
        Assert.That(runs[0].Baseline - runs[1].Baseline, Is.EqualTo(22.4).Within(0.000001));
    }

    [Test]
    public void EmptyMarkedParagraphKeepsExplicitPageBreak()
    {
        var pages = Layout("<p>Lead</p><ol><li><p style='break-before:page'></p><p>A</p></li><li>B</li></ol>");
        Assert.That(pages, Has.Count.EqualTo(2));
        Assert.That(pages[0].Runs.Any(Marker), Is.False);
        Assert.That(pages[1].Runs.Where(Marker).Select(r => r.Text), Is.EqualTo(new[] { "1.", "2." }));
    }

    [Test]
    public void NestedOnlyAndEmptyItemsHaveTheirOwnMarkerLine()
    {
        var runs = Layout("<ol><li><ol><li>A</li></ol></li><li></li><li>B</li></ol>")[0].Runs;
        Assert.That(runs.Where(Marker).Select(r => r.Text), Is.EqualTo(new[] { "1.", "1.", "2.", "3." }));
        Assert.That(runs[0].Baseline, Is.GreaterThan(runs[1].Baseline));
        Assert.That(runs[1].Baseline, Is.EqualTo(runs[2].Baseline));
    }

    [Test]
    public void LargeMarkerContributesToFirstLineStrut()
    {
        var runs = Layout("<ol><li style='font-size:36pt'><p style='font-size:12pt'>A<br>B</p></li></ol>")[0].Runs;
        var marker = runs.Single(Marker);
        Assert.That(marker.Style.FontSize, Is.EqualTo(36));
        Assert.That(marker.Baseline, Is.EqualTo(87.6).Within(0.000001));
        Assert.That(marker.Baseline, Is.EqualTo(runs.Single(r => r.Text == "A").Baseline));
        Assert.That(runs.Single(r => r.Text == "B").Baseline, Is.EqualTo(66).Within(0.000001));
    }

    [Test]
    public void EmptyListDoesNotChangeOrdinaryPdfBytes()
        => Assert.That(Pdf("<ul></ul><ol></ol><p>A</p>"), Is.EqualTo(Pdf("<p>A</p>")));

    [Test]
    public void RepeatedLayoutDoesNotMutateParsedTree()
    {
        var tree = HtmlDocumentReader.ReadTree("<ol><li></li><li><ul><li>A</li></ul></li></ol>", Page, default);
        var before = BlockTreeTraversal.Paragraphs(tree, default).Count();
        var a = TextLayout.Layout(tree, Page, default);
        var b = TextLayout.Layout(tree, Page, default);
        Assert.That(PdfSerializer.Build(a, Page, default).ToArray(), Is.EqualTo(PdfSerializer.Build(b, Page, default).ToArray()));
        Assert.That(BlockTreeTraversal.Paragraphs(tree, default).Count(), Is.EqualTo(before));
        Assert.That(a.SelectMany(p => p.Runs).Count(Marker), Is.EqualTo(2));
    }

    [Test]
    public void FontlessUnorderedListUsesTwoVectorDiscs()
    {
        var pdf = Encoding.ASCII.GetString(Pdf("<ul><li>A</li><li>B</li></ul>"));
        Assert.That(Regex.Matches(pdf, @" c(?:\s|$)").Count, Is.EqualTo(8));
        Assert.That(pdf, Does.Contain("/BaseFont /Courier"));
    }

    [TestCase("ol")]
    [TestCase("ul")]
    public void EachMarkerCountsTowardsExactCommandLimit(string tag)
    {
        var html = $"<{tag}><li>A</li><li>B</li></{tag}>";
        Assert.DoesNotThrow(() => Pdf(html, Page with { MaxDisplayCommands = 4 }));
        Fails(html, "FPDF1401", Page with { MaxDisplayCommands = 3 });
    }

    [Test]
    public void ListMarkerMissingDigitUsesItemSourceAndProtectsOutput()
        => Fails("<ol><li>A</li></ol>", "FPDF1504", Page with { Fonts = [PdfFont.LoadTrueType(FontFixture.Create())] }, source: true);

    [Test]
    public void ListMarkerMissingStyleDoesNotBorrowNormalChain()
        => Fails("<ol><li style='font-weight:bold'><p style='font-weight:normal'>A</p></li></ol>", "FPDF1507", source: true);

    [Test]
    public void ExplicitFontsMeasureDigitsAndFallbackSeparatelyFromBody()
    {
        var body = PdfFont.LoadTrueType(FontFixture.Create());
        var digits = PdfFont.LoadTrueType(FontFixture.Create(listDigits: true));
        var html = "<ol>" + string.Concat(Enumerable.Repeat("<li>A</li>", 100)) + "</ol>";
        var runs = Layout(html, Page with { Fonts = [body, digits], PageHeight = 3000 }).SelectMany(p => p.Runs).ToArray();
        Assert.That(runs.Where(Marker).All(r => ReferenceEquals(r.Font, digits)), Is.True);
        Assert.That(runs.Where(r => r.Text == "A").All(r => ReferenceEquals(r.Font, body)), Is.True);
        // Actual marker advance: (3*600 + 200)/1000*12 = 24pt; plus 6pt gap.
        Assert.That(runs.Where(r => r.Text == "A").Select(r => r.X), Is.All.EqualTo(50));
        Assert.That(runs.Single(r => r.Text == "100.").X, Is.EqualTo(20));
    }

    [Test]
    public void DecimalMarkerKeepsItemRoleWhenFirstParagraphOverridesIt()
    {
        var normal = PdfFont.LoadTrueType(FontFixture.Create(listDigits: true));
        var bold = PdfFont.LoadTrueType(FontFixture.Create(listDigits: true));
        var runs = Layout("<ol><li style='font-weight:bold'><p style='font-weight:normal'>A</p></li></ol>",
            Page with { Fonts = [normal], BoldFonts = [bold] })[0].Runs;
        Assert.That(runs.Single(Marker).Font, Is.SameAs(bold));
        Assert.That(runs.Single(r => r.Text == "A").Font, Is.SameAs(normal));
    }

    [Test]
    public void DiscFitsInsideLineEvenWithNegativeFontDescentAfterLeading()
    {
        var bytes = FontFixture.Create();
        FontFixture.U16(bytes, FontFixture.Table(bytes, "hhea") + 4, 2000);
        FontFixture.U16(bytes, FontFixture.Table(bytes, "hhea") + 6, 0);
        FontFixture.U16(bytes, FontFixture.Table(bytes, "OS/2") + 68, 2000);
        FontFixture.U16(bytes, FontFixture.Table(bytes, "OS/2") + 70, 0);
        var font = PdfFont.LoadTrueType(bytes);
        Assert.That(font.Ascent1000, Is.EqualTo(2000));
        Assert.That(font.Descent1000, Is.EqualTo(0));
        var page = Layout("<ul style='line-height:1'><li></li><li></li></ul>", Page with { Fonts = [font] })[0];
        // Strut ascent=18, descent=-6; disc needs descent >= -1.8.
        Assert.That(page.Discs[0].Y - page.Discs[1].Y, Is.EqualTo(16.2).Within(0.000001));
        Assert.That(page.Discs[0].Y - page.Discs[0].Radius, Is.EqualTo(Page.PageHeight - Page.Margin - 16.2).Within(0.000001));
    }

    [Test]
    public void CancelledListConversionLeavesOutputAndPositionUnchanged()
    {
        using var output = new MemoryStream(); output.Write("sentinel"u8); output.Position = 2;
        Assert.Throws<OperationCanceledException>(() => PdfConverter.Convert("<ol><li>A</li></ol>", output,
            Page, new CancellationToken(true)));
        Assert.That(output.ToArray(), Is.EqualTo("sentinel"u8.ToArray()));
        Assert.That(output.Position, Is.EqualTo(2));
    }

    [Test]
    public void GutterCannotOverflowNarrowList()
        => Fails("<ol style='width:20pt'><li>A</li></ol>", "FPDF1302", source: true);

    [Test]
    public void ListPaginationAndOutputBudgetsProtectOutput()
    {
        var words = string.Join(" ", Enumerable.Repeat("alpha", 50));
        var html = $"<ol><li style='width:90pt'>{words}</li></ol>";
        Fails(html, "FPDF1303", Page with { MaxPages = 1 });
        Fails("<ul><li>A</li></ul>", "FPDF1401", Page with { MaxOutputBytes = 100 });
    }

    [Test]
    public void DecoratedListAndItemsUseExistingSliceEdges()
    {
        var pages = Layout("<ol style='border:1pt solid red'><li style='border:1pt solid blue'><p>A</p>" +
            "<p style='break-before:page'>B</p></li><li>C</li></ol>");
        Assert.That(pages, Has.Count.EqualTo(2));
        Assert.That(pages.SelectMany(p => p.Runs).Where(Marker).Select(r => r.Text), Is.EqualTo(new[] { "1.", "2." }));
        Assert.That(pages[0].PaintBoxes.All(b => b.IsFirstFragment && !b.IsLastFragment), Is.True);
        Assert.That(pages[1].PaintBoxes.All(b => !b.IsFirstFragment && b.IsLastFragment), Is.True);
        Assert.That(pages[0].PaintBoxes.Single(b => b.Style.BorderTop.Color == new Rgb(1, 0, 0)).X, Is.EqualTo(20));
        Assert.That(pages[0].PaintBoxes.Single(b => b.Style.BorderTop.Color == new Rgb(0, 0, 1)).X, Is.EqualTo(45));
    }
}
