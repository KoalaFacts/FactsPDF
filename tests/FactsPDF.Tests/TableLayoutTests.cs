using NUnit.Framework;

namespace FactsPDF.Tests;

public sealed class TableLayoutTests
{
    internal static List<LayoutPage> Layout(string html, PdfOptions? options = null)
    {
        options ??= new();
        return TextLayout.Layout(HtmlDocumentReader.ReadTree(html, options, default), options, default);
    }
    internal static byte[] Pdf(string html, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, options);
        return output.ToArray();
    }
    internal static string Fixture(string name) => File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "tables-e1", name + ".html"));
    internal static string Table(string content = "A", string tableStyle = "width:100pt;font-size:10pt;line-height:2", string cellStyle = "", string colStyle = "width:100%")
        => $"<table style='{tableStyle}'><colgroup><col style='{colStyle}'></colgroup><tbody><tr><td style='{cellStyle}'>{content}</td></tr></tbody></table>";

    [Test]
    public void AuthoredPointAndPercentFixturesHaveExactGeometryAndIdenticalPdf()
    {
        var page = Layout(Fixture("points")).Single();
        var boxes = page.PaintBoxes;
        Assert.That(boxes, Has.Count.EqualTo(9));
        Assert.That(boxes.Select(b => b.X), Is.EqualTo(new double[] { 36, 116, 256, 36, 116, 256, 36, 116, 256 }));
        Assert.That(boxes.Select(b => b.Top), Is.EqualTo(new double[] { 36, 36, 36, 64, 64, 64, 92, 92, 92 }));
        Assert.That(boxes.Select(b => b.Width), Is.EqualTo(new double[] { 80, 140, 260, 80, 140, 260, 80, 140, 260 }));
        Assert.That(boxes.Select(b => b.Height), Is.EqualTo(new double[] { 28, 28, 28, 28, 28, 28, 46, 46, 46 }));
        Assert.That(page.Runs.Select(r => r.Text), Is.EqualTo(new[] { "ID", "Category", "Description", "R001", "Overview", "One complete row", "R002", "Detail", "First line", "Second line" }));
        Assert.That(page.Runs.Select(r => r.X), Is.EqualTo(new double[] { 41, 121, 261, 41, 121, 261, 41, 121, 261, 261 }));
        Assert.That(page.Runs[0].Baseline, Is.EqualTo(841.89 - 41 - 12.6).Within(0.000001));
        Assert.That(page.Runs[^2].Baseline - page.Runs[^1].Baseline, Is.EqualTo(18).Within(0.000001));
        Assert.That(Pdf(Fixture("percentages")), Is.EqualTo(Pdf(Fixture("points"))));
    }

    [TestCase("width:100pt", "width:100pt")]
    [TestCase("width:133.33333333333333px", "width:100%")]
    [TestCase("width:50%", "width:100%")]
    public void TableWidthUsesParentContentAndCellsUseExplicitTracks(string tableStyle, string colStyle)
    {
        var page = Layout("<div style='width:200pt;padding:3pt;border:2pt solid red'>"
            + Table(tableStyle: tableStyle, cellStyle: "padding:4pt;border:1pt solid blue", colStyle: colStyle) + "</div>").Single();
        var cell = page.PaintBoxes.Single(b => b.X == 41);
        Assert.That(cell.Width, Is.EqualTo(100).Within(0.000001));
        Assert.That(page.Runs.Single().X, Is.EqualTo(46));
    }

    [Test]
    public void AutoTableOuterBoxFillsParentWhileExplicitWidthIsContentWidth()
    {
        var automatic = Layout("<div style='width:200pt'>" + Table(tableStyle: "padding:5pt;border:1pt solid red;background-color:white", cellStyle: "background-color:blue") + "</div>").Single();
        Assert.That(automatic.PaintBoxes.Select(b => b.Width), Is.EqualTo(new double[] { 200, 188 }));
        Assert.That(automatic.Runs.Single().X, Is.EqualTo(42));
        var explicitWidth = Layout("<div style='width:200pt'>" + Table(tableStyle: "width:100pt;padding:5pt;border:1pt solid red;background-color:white", cellStyle: "background-color:blue") + "</div>").Single();
        Assert.That(explicitWidth.PaintBoxes.Select(b => b.Width), Is.EqualTo(new double[] { 112, 100 }));
    }

    [TestCase("left", 42)] [TestCase("center", 83)] [TestCase("right", 124)]
    public void CellPaddingPercentagesAndTextAlignmentUseTrackContent(string alignment, double x)
    {
        var page = Layout(Table(tableStyle: "width:100pt;font-size:10pt;line-height:2", cellStyle: $"padding:5%;border:1pt solid red;text-align:{alignment}")).Single();
        Assert.That(page.Runs.Single().X, Is.EqualTo(x)); // track100 - decoration12 - glyph6 = 82
        Assert.That(page.PaintBoxes.Single().Height, Is.EqualTo(32));
    }

    [Test]
    public void RowUsesTallestWrappedCellAndKeepsShorterTextAtTop()
    {
        const string html = "<style>td{padding:2pt;border:1pt solid red}</style><table style='width:100pt;font-size:10pt;line-height:2'><colgroup><col style='width:50pt'><col style='width:50pt'></colgroup><tbody><tr><td>A B C D E</td><td>Z</td></tr></tbody></table>";
        var page = Layout(html).Single();
        Assert.That(page.Runs.Select(r => r.Text), Is.EqualTo(new[] { "A B C D", "E", "Z" }));
        Assert.That(page.PaintBoxes.Select(b => b.Height), Is.EqualTo(new double[] { 46, 46 }));
        Assert.That(page.Runs[0].Baseline, Is.EqualTo(page.Runs[2].Baseline));
        Assert.That(page.PaintBoxes[0].X + page.PaintBoxes[0].Width, Is.EqualTo(page.PaintBoxes[1].X));
        Assert.That(page.PaintBoxes.Select(b => b.CommandCount), Is.EqualTo(new[] { 4, 4 }));
    }

    [TestCase("", 6)] [TestCase("  \n ", 6)] [TestCase("<br>", 26)] [TestCase("<br><br>", 46)]
    public void EmptyAndExplicitBreakCellsRetainExistingLineSemantics(string content, double height)
    {
        var page = Layout(Table(content, cellStyle: "padding:2pt;border:1pt solid red")).Single();
        Assert.That(page.PaintBoxes.Single().Height, Is.EqualTo(height));
        Assert.That(page.Runs, Is.Empty);
    }

    [TestCase("")] [TestCase("width:auto")] [TestCase("width:0")] [TestCase("width:99pt")] [TestCase("width:100.0011pt")]
    public void MissingZeroAndMismatchedColumnWidthsFailBeforeWriting(string width)
        => TableStructureTests.Fails(Table(colStyle: width), "FPDF1302");

    [TestCase("width:99.9995pt")] [TestCase("width:100.0005pt")]
    public void AcceptedWidthToleranceDoesNotRedistributeTracks(string width)
    {
        var cell = Layout(Table(colStyle: width, cellStyle: "background-color:red")).Single().PaintBoxes.Single();
        Assert.That(cell.Width, Is.EqualTo(double.Parse(width[6..^2], System.Globalization.CultureInfo.InvariantCulture)));
    }

    [TestCase("missing-width")] [TestCase("sum-mismatch")] [TestCase("zero-content-width")]
    public void AuthoredNegativeGeometryFixturesFailBeforeWriting(string name)
        => TableStructureTests.Fails(Fixture(name), "FPDF1302");

    [TestCase("padding:50pt")] [TestCase("padding:49pt;border:1pt solid red")]
    public void CellMustHavePositiveContentWidth(string cellStyle)
        => TableStructureTests.Fails(Table(cellStyle: cellStyle), "FPDF1302");

    [Test]
    public void TableOuterWidthAndUnbreakableCellTextMustFit()
    {
        TableStructureTests.Fails(Table(tableStyle: "width:200pt;padding:1pt"), "FPDF1302", new() { PageWidth = 220, Margin = 10 });
        TableStructureTests.Fails(Table("UNBREAKABLE", tableStyle: "width:20pt"), "FPDF1302");
    }

    [Test]
    public void EntireTableMovesOnceAndExactEmptyPageFitSucceeds()
    {
        var options = new PdfOptions { PageHeight = 122, Margin = 10 };
        var pages = Layout("<p style='margin-top:0;margin-bottom:0;font-size:10pt;line-height:2'>BEFORE</p>" + Fixture("points"), options);
        Assert.That(pages, Has.Count.EqualTo(2));
        Assert.That(pages[0].Runs.Select(r => r.Text), Is.EqualTo(new[] { "BEFORE" }));
        Assert.That(pages[0].PaintBoxes, Is.Empty);
        Assert.That(pages[1].PaintBoxes.Select(b => b.Top).Min(), Is.EqualTo(10));
        Assert.That(pages[1].PaintBoxes.Select(b => b.Top + b.Height).Max(), Is.EqualTo(112));
        Assert.That(Layout(Fixture("points"), options), Has.Count.EqualTo(1));
        TableStructureTests.Fails(Fixture("points"), "FPDF1302", options with { PageHeight = 121.99 });
    }

    [Test]
    public void NewAncestorLeadingDecorationMovesWithTableAndMustFit()
    {
        var o = new PdfOptions { PageHeight = 100, Margin = 10 };
        var table = Table("A<br>B<br>C", cellStyle: "border:1pt solid blue"); // 62pt
        var pages = Layout("<p style='margin-top:0;margin-bottom:0;font-size:10pt;line-height:2'>BEFORE</p><div style='padding:3pt;border:1pt solid red'>" + table + "</div>", o);
        Assert.That(pages, Has.Count.EqualTo(2));
        Assert.That(pages[1].PaintBoxes.Single(b => b.X == 14).Top, Is.EqualTo(14));
        Assert.That(pages[1].PaintBoxes.Single(b => b.X == 10).Top, Is.EqualTo(10));
        TableStructureTests.Fails("<div style='padding-top:19pt'>" + table + "</div>", "FPDF1302", o);
    }

    [Test]
    public void BreakAfterPreviousParagraphAndBeforeFollowingParagraphRespectWholeTable()
    {
        var pages = Layout("<p style='break-after:page'>BEFORE</p>" + Table() + "<p style='break-before:page'>AFTER</p>");
        Assert.That(pages, Has.Count.EqualTo(3));
        Assert.That(pages.Select(p => p.Runs.Single().Text), Is.EqualTo(new[] { "BEFORE", "A", "AFTER" }));
    }

    [Test]
    public void HeaderAndInlineRolesUseExplicitChainsAndRoleLocalFallback()
    {
        var normal = PdfFont.LoadTrueType(FontFixture.Create());
        var bold = PdfFont.LoadTrueType(FontFixture.Create(latinOnly: true));
        var fallback = PdfFont.LoadTrueType(FontFixture.Create());
        var italic = PdfFont.LoadTrueType(FontFixture.Create());
        var boldItalic = PdfFont.LoadTrueType(FontFixture.Create());
        var o = new PdfOptions { Fonts = [normal], BoldFonts = [bold, fallback], ItalicFonts = [italic], BoldItalicFonts = [boldItalic] };
        var html = "<table style='width:100pt'><colgroup><col style='width:100%'></colgroup><thead><tr><th>A中</th></tr></thead><tbody><tr><td>A<i>A</i><b><i>A</i></b></td></tr></tbody></table>";
        Assert.That(Layout(html, o).Single().Runs.Select(r => r.Font), Is.EqualTo(new[] { bold, fallback, normal, italic, boldItalic }));
        TableStructureTests.Fails(html, "FPDF1507", o with { BoldFonts = [] });
        TableStructureTests.Fails(html, "FPDF1504", o with { BoldFonts = [bold] });
        Assert.That(Pdf(html, o), Is.Not.Empty);
    }

    [Test]
    public void CjkWrapAndMixedInlineHeightShareParagraphMetrics()
    {
        var o = new PdfOptions { Fonts = [PdfFont.LoadTrueType(FontFixture.Create())] };
        var content = "中文中文<br>A<span style='font-size:20pt;line-height:1.5'>A</span>";
        const string style = "width:30pt;font-size:10pt;line-height:2";
        var cell = Layout(Table(content, style, "background-color:red"), o).Single();
        var paragraph = Layout($"<p style='{style};margin-top:0;margin-bottom:0'>{content}</p>", o).Single();
        Assert.That(cell.Runs, Is.EqualTo(paragraph.Runs));
        Assert.That(cell.PaintBoxes.Single().Height, Is.EqualTo(70)); // two20pt CJK lines, then30pt mixed line
    }
}
