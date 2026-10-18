using NUnit.Framework;

namespace FactsPDF.Tests;

public sealed class TableBoundaryTests
{
    [Test]
    public void TablesRetainInputCssAndFontResourceLimits()
    {
        var html = TableLayoutTests.Table();
        Fails(html, new() { MaxInputCharacters = html.Length - 1 }, "FPDF1001");
        Fails(html, new() { MaxCssDeclarations = 1 }, "FPDF1205");
        var font = PdfFont.LoadTrueType(FontFixture.Create());
        Fails(html, new() { Fonts = [font], MaxTotalFontBytes = font.ByteLength - 1 }, "FPDF1503");
    }

    private static void Fails(string html, PdfOptions options, string code)
    {
        using var output = new MemoryStream(); output.Write("sentinel"u8); output.Position = 2;
        Assert.That(Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, options))!.Code, Is.EqualTo(code));
        Assert.That(output.ToArray(), Is.EqualTo("sentinel"u8.ToArray()));
        Assert.That(output.Position, Is.EqualTo(2));
    }

    [Test]
    public void ExactCommandBoundaryIncludesEveryTextCellAndTablePaint()
    {
        var html = TableLayoutTests.Table(tableStyle: "width:100pt;padding:1pt;border:1pt solid red;background-color:white",
            cellStyle: "padding:1pt;border:1pt solid blue;background-color:white");
        var expected = TableLayoutTests.Pdf(html);
        Assert.That(TableLayoutTests.Pdf(html, new() { MaxDisplayCommands = 11 }), Is.EqualTo(expected)); //1 text + 5 cell + 5 table
        Fails(html, new() { MaxDisplayCommands = 10 }, "FPDF1401");
        var fixture = TableLayoutTests.Fixture("points"); //10 text +9*4 border rectangles
        Assert.That(TableLayoutTests.Pdf(fixture, new() { MaxDisplayCommands = 46 }), Is.EqualTo(TableLayoutTests.Pdf(fixture)));
        Fails(fixture, new() { MaxDisplayCommands = 45 }, "FPDF1401");
    }

    [TestCase(false)] [TestCase(true)]
    public void ExactSerializedByteBoundaryIncludesFontsAndTablePaint(bool embedded)
    {
        var o = new PdfOptions { Fonts = embedded ? [PdfFont.LoadTrueType(FontFixture.Create())] : [], SubsetFonts = embedded };
        var html = TableLayoutTests.Table(embedded ? "中文" : "A", cellStyle: "border:1pt solid blue");
        var pdf = TableLayoutTests.Pdf(html, o);
        Assert.That(TableLayoutTests.Pdf(html, o with { MaxOutputBytes = pdf.Length }), Is.EqualTo(pdf));
        Fails(html, o with { MaxOutputBytes = pdf.Length - 1 }, "FPDF1401");
    }

    [Test]
    public void ExactPageBoundaryAndRecoveryDoNotSplitTheTable()
    {
        var html = "<p style='margin-top:0;margin-bottom:0'>BEFORE</p>" + TableLayoutTests.Fixture("points");
        var o = new PdfOptions { PageHeight = 122, Margin = 10, MaxPages = 2 };
        var first = TableLayoutTests.Pdf(html, o);
        Fails(html, o with { MaxPages = 1 }, "FPDF1303");
        Assert.That(TableLayoutTests.Pdf(html, o), Is.EqualTo(first));
    }

    [TestCase("中文")] [TestCase("<br>中文")] [TestCase("UNBREAKABLELONGTEXT")]
    public void SpentBudgetRejectsNextCellBeforeGlyphOrWidthChecks(string next)
    {
        var html = "<table style='width:20pt'><colgroup><col style='width:10pt'><col style='width:10pt'></colgroup><tbody><tr><td>A</td><td>" + next + "</td></tr></tbody></table>";
        Fails(html, new() { MaxDisplayCommands = 1 }, "FPDF1401");
    }

    [Test]
    public void SpentBudgetDoesNotMaterializeLargeTableCellGlyphs()
    {
        var o = new PdfOptions { MaxDisplayCommands = 1 };
        var html = "<p>A</p>" + TableLayoutTests.Table(string.Concat(Enumerable.Repeat("A ", 100000)));
        var tree = HtmlDocumentReader.ReadTree(html, o, default);
        var before = GC.GetAllocatedBytesForCurrentThread();
        FactsPdfException? failure = null;
        try { TextLayout.Layout(tree, o, default); }
        catch (FactsPdfException error) { failure = error; }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.That(failure?.Code, Is.EqualTo("FPDF1401"));
        Assert.That(allocated, Is.LessThan(256 * 1024), "No second cell/paragraph glyph representation after the allowance is spent.");
    }

    [Test]
    public void EmptyZeroAreaPaintStaysFreeButDecoratedEmptyCellsAreCharged()
    {
        var empty = "<p>A</p>" + TableLayoutTests.Table("", cellStyle: "background-color:red");
        Assert.That(TableLayoutTests.Pdf(empty, new() { MaxDisplayCommands = 1 }), Is.Not.Empty);
        var painted = TableLayoutTests.Table("", cellStyle: "padding:2pt;background-color:red;border:1pt solid blue");
        Assert.That(TableLayoutTests.Pdf(painted, new() { MaxDisplayCommands = 5 }), Is.Not.Empty);
        Fails(painted, new() { MaxDisplayCommands = 4 }, "FPDF1401");
    }

    [Test]
    public void TableMovesAfterListAndImageWithinContinuedAncestorWithoutRepeatingPadding()
    {
        var o = new PdfOptions { PageWidth = 220, PageHeight = 130, Margin = 10,
            Images = new Dictionary<string, PdfImage> { ["chart"] = PdfImage.LoadImage(JpegResourceTests.Bytes("rgb-420")) } };
        var html = "<body style='font-size:10pt;line-height:2'><section style='padding:4pt;border:1pt solid red'>"
            + "<ol><li>BEFORE</li></ol><img src='chart' style='width:120pt'>"
            + TableLayoutTests.Table("A<br>B<br>C", cellStyle: "border:1pt solid blue")
            + "<ul><li>AFTER</li></ul></section></body>";
        var pages = TableLayoutTests.Layout(html, o);
        Assert.That(pages, Has.Count.EqualTo(2));
        Assert.That(pages[0].Runs.Select(r => r.Text), Is.EqualTo(new[] { "1.", "BEFORE" }));
        Assert.That(pages[0].Images.Single().Top, Is.EqualTo(35));
        Assert.That(pages[1].Runs.Select(r => r.Text), Is.EqualTo(new[] { "A", "B", "C", "AFTER" }));
        var cell = pages[1].PaintBoxes.Single(b => b.X == 15);
        Assert.That((cell.Top, cell.Height), Is.EqualTo((10d, 62d)));
        Assert.That(pages[1].Runs[^1].Baseline, Is.EqualTo(45).Within(0.000001));
        var first = pages[0].PaintBoxes.Single(); var last = pages[1].PaintBoxes.Single(b => b.X == 10);
        Assert.That((first.Top, first.Height, first.IsFirstFragment, first.IsLastFragment), Is.EqualTo((10d, 110d, true, false)));
        Assert.That((last.Top, last.Height, last.IsFirstFragment, last.IsLastFragment), Is.EqualTo((10d, 87d, false, true)));
        Assert.That(last.Order, Is.LessThan(cell.Order));
        Assert.That(TableLayoutTests.Pdf(html, o), Is.Not.Empty);
    }

    [Test]
    public void TextFreeTableCanOccupyEntireMiddlePage()
    {
        var o = new PdfOptions { PageHeight = 100, Margin = 10 };
        var html = "<p style='break-after:page'>BEFORE</p>" + TableLayoutTests.Table("<br><br><br><br>", cellStyle: "background-color:red") + "<p>AFTER</p>";
        var pages = TableLayoutTests.Layout(html, o);
        Assert.That(pages, Has.Count.EqualTo(3));
        Assert.That(pages[1].Runs, Is.Empty);
        Assert.That((pages[1].PaintBoxes.Single().Top, pages[1].PaintBoxes.Single().Height), Is.EqualTo((10d, 80d)));
    }

    [Test]
    public void CancellationIsObservedDuringMeasuredCommandRetention()
    {
        var o = new PdfOptions();
        var tree = HtmlDocumentReader.ReadTree(TableLayoutTests.Table("A<br>B<br>C"), o, default);
        var step = (LayoutTable)BlockLayout.Steps(tree, o, default).Single();
        using var cts = new CancellationTokenSource();
        var retained = 0;
        Assert.Throws<OperationCanceledException>(() => TableLayout.Measure(step, o, new(), cts.Token,
            (count, _) => { retained += count; cts.Cancel(); }, (_, _) => { }));
        Assert.That(retained, Is.EqualTo(1));
    }

    [Test]
    public void CancelledPublicConversionLeavesDestinationAndLaterConversionsUnchanged()
    {
        var html = TableLayoutTests.Fixture("points");
        var expected = TableLayoutTests.Pdf(html);
        using var output = new MemoryStream(); output.Write("sentinel"u8); output.Position = 2;
        Assert.Throws<OperationCanceledException>(() => PdfConverter.Convert(html, output, cancellationToken: new(true)));
        Assert.That(output.ToArray(), Is.EqualTo("sentinel"u8.ToArray())); Assert.That(output.Position, Is.EqualTo(2));
        var results = new byte[8][];
        Parallel.For(0, results.Length, i => results[i] = TableLayoutTests.Pdf(html));
        Assert.That(results, Is.All.EqualTo(expected));
    }
}
