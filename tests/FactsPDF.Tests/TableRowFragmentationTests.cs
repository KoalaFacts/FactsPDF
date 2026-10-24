using NUnit.Framework;

namespace FactsPDF.Tests;

public sealed class TableRowFragmentationTests
{
    private static readonly PdfOptions Page = TablePaginationTests.Page;
    internal static string Lines(string prefix, int count) => string.Join("<br>", Enumerable.Range(1, count).Select(i => $"{prefix}{i:00}"));
    internal static string ThreeCells(int count = 12) => "<style>td,th{font-weight:normal;border:1pt solid red;background-color:blue}</style>"
        + "<table style='width:200pt;font-size:10pt;line-height:1.2;background-color:#eeeeee'><colgroup><col style='width:40pt'><col style='width:120pt'><col style='width:40pt'></colgroup><thead><tr><th colspan='3'>HEAD</th></tr></thead><tbody><tr><td>ID</td><td>" + Lines("L", count) + "</td><td>9</td></tr></tbody></table>";
    internal static string Pair(string left, string right, string leftStyle = "", string rightStyle = "", bool header = true)
        => "<style>th{font-weight:normal}td p{margin-top:0;margin-bottom:0}</style><table style='width:200pt;font-size:10pt;line-height:1.2'><colgroup><col style='width:100pt'><col style='width:100pt'></colgroup>"
        + (header ? "<thead><tr><th colspan='2'>HEAD</th></tr></thead>" : "") + "<tbody><tr><td style='" + leftStyle + "'>" + left + "</td><td style='" + rightStyle + "'>" + right + "</td></tr></tbody></table>";
    private static int Commands(IEnumerable<LayoutPage> pages) => pages.Sum(p => p.Runs.Count + p.Images.Count + p.PaintBoxes.Sum(b => b.CommandCount));
    private static double[] Baselines(LayoutPage page, string prefix) => page.Runs.Where(r => r.Text.StartsWith(prefix, StringComparison.Ordinal)).Select(r => 100 - r.Baseline).ToArray();

    [Test]
    public void S1IndependentShortCellsExactGeometryPaintAndBudget()
    {
        var html = ThreeCells(); var pages = TableLayoutTests.Layout(html, Page);
        Assert.That(pages, Has.Count.EqualTo(3));
        Assert.That(pages.Select(p => p.Runs.Count(r => r.Text.StartsWith('L'))), Is.EqualTo(new[] { 5, 5, 2 }));
        Assert.That(Baselines(pages[0], "L"), Is.EqualTo(new double[] { 34, 46, 58, 70, 82 }));
        Assert.That(Baselines(pages[1], "L"), Is.EqualTo(new double[] { 33, 45, 57, 69, 81 }));
        Assert.That(Baselines(pages[2], "L"), Is.EqualTo(new double[] { 33, 45 }));
        Assert.That(pages.SelectMany(p => p.Runs).Where(r => r.Text.StartsWith('L')).Select(r => r.Text),
            Is.EqualTo(Enumerable.Range(1, 12).Select(i => $"L{i:00}")));
        Assert.That(pages.SelectMany(p => p.Runs).Count(r => r.Text == "ID"), Is.EqualTo(1));
        Assert.That(pages.SelectMany(p => p.Runs).Count(r => r.Text == "9"), Is.EqualTo(1));
        Assert.That(pages.SelectMany(p => p.Runs).Where(r => r.Text.StartsWith('L')).Select(r => r.X), Is.All.EqualTo(51));
        Assert.That(pages.Select(p => p.Runs.Single(r => r.Text == "HEAD").Baseline), Is.All.EqualTo(80));
        var cells = pages.Select(p => p.PaintBoxes.Where(b => b.Width == 120).Single()).ToArray();
        Assert.That(cells.Select(b => b.Top), Is.EqualTo(new double[] { 24, 24, 24 }));
        Assert.That(cells.Select(b => b.Top + b.Height), Is.EqualTo(new double[] { 85, 84, 49 }));
        Assert.That(cells.Select(b => b.IsFirstFragment), Is.EqualTo(new[] { true, false, false }));
        Assert.That(cells.Select(b => b.IsLastFragment), Is.EqualTo(new[] { false, false, true }));
        Assert.That(Commands(pages), Is.EqualTo(68));
        var bytes = TableLayoutTests.Pdf(html, Page);
        Assert.That(TableLayoutTests.Pdf(html, Page with { MaxDisplayCommands = 68, MaxPages = 3 }), Is.EqualTo(bytes));
        Fails(html, "FPDF1401", Page with { MaxDisplayCommands = 67 });
        Fails(html, "FPDF1303", Page with { MaxPages = 2 });
        Assert.That(TableLayoutTests.Pdf(html, Page with { MaxOutputBytes = bytes.Length }), Is.EqualTo(bytes));
        Fails(html, "FPDF1401", Page with { MaxOutputBytes = bytes.Length - 1 });
    }

    [Test]
    public void S2UnequalLineHeightsAdvanceIndependentPrefixes()
    {
        var pages = TableLayoutTests.Layout(Pair(Lines("A", 4), Lines("B", 9), "line-height:2;background-color:red", "background-color:blue"), Page);
        Assert.That(pages, Has.Count.EqualTo(2));
        Assert.That(Baselines(pages[0], "A"), Is.EqualTo(new double[] { 35, 55, 75 }));
        Assert.That(Baselines(pages[1], "A"), Is.EqualTo(new double[] { 35 }));
        Assert.That(Baselines(pages[0], "B"), Is.EqualTo(new double[] { 31, 43, 55, 67, 79 }));
        Assert.That(Baselines(pages[1], "B"), Is.EqualTo(new double[] { 31, 43, 55, 67 }));
        Assert.That(pages.Select(p => p.PaintBoxes.First().Height), Is.EqualTo(new double[] { 60, 48 }));
    }

    [Test]
    public void S3ImageWaitsAsOneAtomicDecoratedUnit()
    {
        var image = PdfImage.LoadImage(PngFixture.Image(new byte[32 * 129], 32, 32));
        var options = Page with { Images = new Dictionary<string, PdfImage> { ["sample"] = image } };
        var html = Pair(Lines("A", 4) + "<img src='sample' style='width:24pt;padding:2pt;border:1pt solid red'>TAIL", Lines("B", 7));
        var pages = TableLayoutTests.Layout(html, options);
        Assert.That(pages, Has.Count.EqualTo(2));
        Assert.That(pages[0].Images, Is.Empty);
        var placed = pages[1].Images.Single();
        Assert.That(placed.Image, Is.SameAs(image)); Assert.That(placed.X, Is.EqualTo(13));
        Assert.That(placed.Top, Is.EqualTo(25)); Assert.That(placed.Width, Is.EqualTo(24)); Assert.That(placed.Height, Is.EqualTo(24));
        Assert.That(pages[1].PaintBoxes.Single().Top, Is.EqualTo(22));
        Assert.That(pages[1].PaintBoxes.Single().Height, Is.EqualTo(30));
        Assert.That(pages[1].Runs.Single(r => r.Text == "TAIL").Baseline, Is.EqualTo(39));
    }

    [TestCase(19, 2, 16)] [TestCase(20, 3, 20)]
    public void S4TerminalBottomReservesActualLastLine(double bottom, int count, int commands)
    {
        var html = TableLayoutTests.Table(Lines("L", 6), $"width:100pt;font-size:10pt;line-height:2;background-color:blue;padding-bottom:{bottom}pt", "border:1pt solid red;background-color:white");
        var pages = TableLayoutTests.Layout(html, Page);
        Assert.That(pages, Has.Count.EqualTo(count));
        Assert.That(pages.Select(p => p.Runs.Count), Is.EqualTo(bottom == 19 ? new[] { 3, 3 } : new[] { 3, 2, 1 }));
        Assert.That(pages[^1].PaintBoxes.Single(b => b.Width == 100 && b.Style.BackgroundColor == new Rgb(1, 1, 1)).Top + pages[^1].PaintBoxes.Last().Height, Is.EqualTo(bottom == 19 ? 71 : 31));
        Assert.That(Commands(pages), Is.EqualTo(commands));
        Assert.That(TableLayoutTests.Pdf(html, Page with { MaxDisplayCommands = commands }), Is.EqualTo(TableLayoutTests.Pdf(html, Page)));
        Fails(html, "FPDF1401", Page with { MaxDisplayCommands = commands - 1 });
    }

    [TestCase(false, 3)] [TestCase(true, 2)]
    public void S5HeaderAndRequiredAncestorDetermineFreshEligibility(bool ancestor, int firstLines)
    {
        var html = Pair(Lines("A", 4), "Z", "line-height:2");
        if (ancestor) html = "<div style='padding-top:10pt'>" + html + "</div>";
        var pages = TableLayoutTests.Layout(html, Page);
        Assert.That(pages, Has.Count.EqualTo(2));
        Assert.That(pages[0].Runs.Count(r => r.Text.StartsWith('A')), Is.EqualTo(firstLines));
        Assert.That(Baselines(pages[0], "A"), Is.EqualTo(ancestor ? new double[] { 45, 65 } : new double[] { 35, 55, 75 }));
    }

    [Test]
    public void S7DeferredFirstCellPreservesOriginalGapOnContinuation()
    {
        var html = "<p style='font-size:5pt;line-height:1;margin-top:0;margin-bottom:0'>P</p>" + Pair("<p style='margin-top:70pt;line-height:1'>A</p>", Lines("B", 3), rightStyle: "line-height:5", header: false);
        var pages = TableLayoutTests.Layout(html, Page);
        Assert.That(pages, Has.Count.EqualTo(3));
        Assert.That(pages[0].Runs.Any(r => r.Text == "A"), Is.False);
        Assert.That(pages[1].Runs.Single(r => r.Text == "A").Baseline, Is.EqualTo(12));
        Assert.That(pages.Select(p => p.Runs.Single(r => r.Text.StartsWith('B')).Baseline), Is.EqualTo(new double[] { 57, 62, 62 }));
    }

    [TestCase(false, 4)] [TestCase(true, 6)]
    public void TrimmedGapCanMakeActualBorderSlicesCheaperThanWholeBox(bool background, int commands)
    {
        var html = Pair("<p>A</p><p style='margin-top:100pt'>B</p>", "", rightStyle: "border-top:20pt solid red;border-bottom:20pt solid red;border-left:1pt solid red;border-right:1pt solid red;" + (background ? "background-color:blue" : ""), header: false);
        var pages = TableLayoutTests.Layout(html, Page);
        Assert.That(pages, Has.Count.EqualTo(2));
        Assert.That(pages.Select(p => p.PaintBoxes.Single().Height), Is.EqualTo(new double[] { 20, 20 }));
        Assert.That(Commands(pages), Is.EqualTo(commands));
        Assert.That(TableLayoutTests.Pdf(html, Page with { MaxDisplayCommands = commands }), Is.Not.Empty);
        Fails(html, "FPDF1401", Page with { MaxDisplayCommands = commands - 1 });
    }

    [Test]
    public void ImpossibleInitialGapFailsBeforeNeighborCanProgressAndShortTableStaysIndivisible()
    {
        Fails(Pair("<p style='margin-top:69pt'>A</p>", Lines("B", 40)), "FPDF1302", Page);
        Fails(Pair("A", Lines("B", 40), "padding-top:69pt"), "FPDF1302", Page);
        Fails("<div style='padding-top:1pt'>" + TableLayoutTests.Table(Lines("A", 4)) + "</div>", "FPDF1302", Page);
    }

    [Test]
    public void InitialAndTerminalCellEdgesMayBelongToDifferentFragments()
    {
        var html = Pair("<p style='margin-top:70pt'>A<br>B</p>", "", "line-height:1;padding-bottom:20pt", header: false);
        var pages = TableLayoutTests.Layout(html, Page);
        Assert.That(pages, Has.Count.EqualTo(2));
        Assert.That(pages.Select(p => string.Join(',', p.Runs.Select(r => r.Text))), Is.EqualTo(new[] { "A", "B" }));
        Assert.That(pages[0].Runs.Single().Baseline, Is.EqualTo(12));
        Assert.That(pages[1].Runs.Single().Baseline, Is.EqualTo(82));
    }

    [Test]
    public void A1DefaultPageHasLiteralCountsAnd186CommandBoundary()
    {
        var html = ThreeCells(130); var pages = TableLayoutTests.Layout(html);
        Assert.That(pages, Has.Count.EqualTo(3));
        Assert.That(pages.Select(p => p.Runs.Count(r => r.Text.StartsWith('L'))), Is.EqualTo(new[] { 62, 62, 6 }));
        Assert.That(pages.Select(p => p.PaintBoxes.Single(b => b.Width == 120).Top + p.PaintBoxes.Single(b => b.Width == 120).Height), Is.EqualTo(new double[] { 795, 794, 123 }));
        Assert.That(Commands(pages), Is.EqualTo(186));
        Assert.That(TableLayoutTests.Pdf(html, new() { MaxDisplayCommands = 186 }), Is.EqualTo(TableLayoutTests.Pdf(html)));
        Fails(html, "FPDF1401", new() { MaxDisplayCommands = 185 });
    }

    [Test]
    public void MixedWholeSplitWholeRowsShareFinalDescriptionPageWithOneHeader()
    {
        var html = Pair(Lines("A", 8), "ID").Replace("<tbody>", "<tbody><tr><td>FIRST</td><td>1</td></tr>")
            .Replace("</tbody>", "<tr><td>TOTAL</td><td>9</td></tr></tbody>");
        var pages = TableLayoutTests.Layout(html, Page);
        Assert.That(pages, Has.Count.EqualTo(2));
        Assert.That(pages[0].Runs.Select(r => r.Text), Is.EqualTo(new[] { "HEAD", "FIRST", "1", "A01", "A02", "A03", "A04", "ID" }));
        Assert.That(pages[1].Runs.Select(r => r.Text), Is.EqualTo(new[] { "HEAD", "A05", "A06", "A07", "A08", "TOTAL", "9" }));
    }

    [Test]
    public void BlankLinesHaveRealHeightWithoutInventedRunsAndEmptyParagraphsHaveNoUnits()
    {
        var html = Pair("A<br><br>B<br><br>C<br><br>D", " <p></p><p> </p>", "background-color:red");
        var pages = TableLayoutTests.Layout(html, Page);
        Assert.That(pages, Has.Count.EqualTo(2));
        Assert.That(Baselines(pages[0], "A"), Is.EqualTo(new double[] { 31 }));
        Assert.That(Baselines(pages[0], "B"), Is.EqualTo(new double[] { 55 }));
        Assert.That(Baselines(pages[0], "C"), Is.EqualTo(new double[] { 79 }));
        Assert.That(Baselines(pages[1], "D"), Is.EqualTo(new double[] { 43 }));
        Assert.That(pages.Select(p => p.PaintBoxes.Single().Height), Is.EqualTo(new double[] { 60, 24 }));
        var blanks = TableLayoutTests.Layout(Pair("<br><br><br><br><br><br><br>", "", "background-color:red"), Page);
        Assert.That(blanks, Has.Count.EqualTo(2)); Assert.That(blanks.SelectMany(p => p.Runs).Select(r => r.Text), Is.EqualTo(new[] { "HEAD", "HEAD" }));
    }

    [Test]
    public void NoninitialParagraphGapTrimsOnlyAtContinuationStart()
    {
        var html = Pair("<p style='margin-bottom:8pt'>A</p><p style='margin-top:4pt'>B</p><p style='margin-top:100pt'>C</p>", "");
        var pages = TableLayoutTests.Layout(html, Page);
        Assert.That(pages, Has.Count.EqualTo(2));
        Assert.That(pages[0].Runs.Single(r => r.Text == "A").Baseline - pages[0].Runs.Single(r => r.Text == "B").Baseline, Is.EqualTo(20));
        Assert.That(pages[1].Runs.Single(r => r.Text == "C").Baseline, Is.EqualTo(69));
    }

    [Test]
    public void TerminalCompletionUsesMaximumAcrossFinishedCellsAndTrailingEmptyParagraphs()
    {
        var html = Pair(Lines("A", 6), "Z<p style='margin-top:30pt'></p>", "line-height:2;background-color:red;padding-bottom:5pt", "background-color:blue;padding-bottom:10pt", header: false)
            .Replace("</tbody>", "<tr><td></td><td></td></tr></tbody>");
        var pages = TableLayoutTests.Layout(html, Page);
        Assert.That(pages, Has.Count.EqualTo(2));
        Assert.That(pages.Select(p => p.PaintBoxes.First().Height), Is.EqualTo(new double[] { 80, 45 }));
        Assert.That(pages[1].PaintBoxes.Select(b => b.Height), Is.All.EqualTo(45));
        Assert.That(pages.SelectMany(p => p.Runs).Count(r => r.Text == "Z"), Is.EqualTo(1));
    }

    [Test]
    public void TerminalRollbackMovesCompleteLastImageAndNeverCreatesEmptyTerminalPage()
    {
        var image = PdfImage.LoadImage(PngFixture.Image(new byte[32 * 129], 32, 32));
        var options = Page with { Images = new Dictionary<string, PdfImage> { ["sample"] = image } };
        var html = TableLayoutTests.Table(Lines("A", 4) + "<img src='sample' style='width:24pt;padding:2pt;border:1pt solid red'>", "width:100pt;font-size:10pt;line-height:1.2;padding-bottom:10pt");
        var pages = TableLayoutTests.Layout(html, options);
        Assert.That(pages, Has.Count.EqualTo(2)); Assert.That(pages[0].Images, Is.Empty);
        Assert.That(pages[1].Images.Single().Top, Is.EqualTo(13));
        Assert.That(pages[1].PaintBoxes.Single().Height, Is.EqualTo(30));
        Assert.That(pages[1].Runs, Is.Empty);
    }

    [Test]
    public void CertainlyWholeCellPaintStillExhaustsAllowanceBeforeFollowingWrap()
    {
        var html = TableLayoutTests.Table("A", "width:40pt", "padding:1pt;border:1pt solid red;background-color:blue")
            .Replace("</tbody>", "<tr><td>UNBREAKABLEUNBREAKABLE</td></tr></tbody>");
        using var output = new MemoryStream();
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, new() { MaxDisplayCommands = 2 }))!;
        Assert.That(error.Code, Is.EqualTo("FPDF1401"));
        Assert.That(error.SourceOffset, Is.EqualTo(html.IndexOf("<td", StringComparison.Ordinal)));
    }

    [Test]
    public void TerminalAmbiguityResolvesBeforePaintAndKeepsExactPositiveBudget()
    {
        var html = TableLayoutTests.Table("A<br>B<br>C", "width:100pt;font-size:10pt;line-height:2;padding-bottom:30pt", "background-color:red")
            .Replace("</tbody>", "<tr><td>D</td></tr><tr><td></td></tr></tbody>");
        var pages = TableLayoutTests.Layout(html, Page);
        Assert.That(pages, Has.Count.EqualTo(2)); Assert.That(Commands(pages), Is.EqualTo(5));
        Assert.That(TableLayoutTests.Pdf(html, Page with { MaxDisplayCommands = 5 }), Is.Not.Empty);
        Fails(html, "FPDF1401", Page with { MaxDisplayCommands = 4 });
        var overwide = html.Replace(">D<", ">UNBREAKABLEUNBREAKABLEUNBREAKABLE<");
        Fails(overwide, "FPDF1302", Page with { MaxDisplayCommands = 4 });
    }

    [Test]
    public void AtomicHeaderLineImageAndEdgeOnlyRowsRemainLocatedFailures()
    {
        foreach (var html in new[] { Pair("A", Lines("B", 20), "line-height:8"), Pair("A", Lines("B", 20)).Replace(">HEAD<", ">" + Lines("H", 7) + "<"), Pair("", "", "padding-top:81pt", header: false) })
        {
            var error = Assert.Throws<FactsPdfException>(() => TableLayoutTests.Layout(html, Page))!;
            Assert.That(error.Code, Is.EqualTo("FPDF1302")); Assert.That(error.SourceOffset, Is.GreaterThanOrEqualTo(0));
        }
        var options = TableImageTests.Options() with { PageWidth = 240, PageHeight = 100, Margin = 10 };
        Fails(Pair("<img src='logo' style='width:100pt;padding-top:20pt'>", Lines("B", 20)), "FPDF1302", options);
    }

    [Test]
    public void SplitSpansKeepCoveredWidthAndPercentageImageResolution()
    {
        var image = PdfImage.LoadImage(PngFixture.Image(new byte[32 * 129], 32, 32));
        var options = Page with { Images = new Dictionary<string, PdfImage> { ["sample"] = image } };
        var html = TableRichTests.Rich(Lines("A", 7) + "<img src='sample' style='width:50%'>", "padding:5%;border:1pt solid red;background-color:blue");
        var pages = TableLayoutTests.Layout(html, options);
        Assert.That(pages, Has.Count.EqualTo(2));
        Assert.That(pages.Select(p => p.PaintBoxes.Single().Width), Is.All.EqualTo(100));
        Assert.That(pages.SelectMany(p => p.Runs).Where(r => r.Text.StartsWith('A')).Select(r => r.X), Is.All.EqualTo(16));
        Assert.That(pages.SelectMany(p => p.Images).Single().Width, Is.EqualTo(44));
        Assert.That(pages.SelectMany(p => p.Images).Single().X, Is.EqualTo(16));
    }

    [Test]
    public void CandidateSelectionDoesNotMutateCursorsAndObservesCancellation()
    {
        var tree = HtmlDocumentReader.ReadTree(ThreeCells(), Page, default);
        var step = (LayoutTable)BlockLayout.Steps(tree, Page, default).Single();
        var measured = TableLayout.Measure(step, Page, new(), default, (_, _) => { }, (_, _) => { });
        var row = measured.Rows[1]; var indices = new int[3]; var cursor = new RowContinuation(indices, true);
        var fit = TableRowFragmentation.Fit(row, cursor, 66, 0, true, default);
        Assert.That(indices, Is.EqualTo(new[] { 0, 0, 0 }));
        Assert.That(fit.Fragment!.Cells.Select(c => c.EndUnit), Is.EqualTo(new[] { 1, 5, 1 }));
        Assert.Throws<OperationCanceledException>(() => TableRowFragmentation.Fit(row, cursor, 66, 0, true, new(true)));
        Assert.Throws<OperationCanceledException>(() => TablePagination.FitWithContinuations(measured, new(1), 80, new(80, 0), true, new(true)));
        using var output = new MemoryStream(); output.Write("sentinel"u8); output.Position = 2;
        Assert.Throws<OperationCanceledException>(() => PdfConverter.Convert(ThreeCells(), output, Page, new(true)));
        Assert.That(output.ToArray(), Is.EqualTo("sentinel"u8.ToArray())); Assert.That(output.Position, Is.EqualTo(2));
        Assert.That(TableLayoutTests.Pdf(ThreeCells(), Page), Is.Not.Empty);
    }

    [Test]
    public void ExactFirstGapCapacitySucceedsAndCopiedStyledRunRangesSurviveWrapping()
    {
        var html = Pair("<p style='margin-top:56pt'>A</p>", Lines("B", 10));
        var pages = TableLayoutTests.Layout(html, Page);
        Assert.That(pages[0].Runs.Single(r => r.Text == "A").Baseline, Is.EqualTo(13));
        var styled = Pair(string.Join("<br>", Enumerable.Range(1, 10).Select(i => $"L{i:00}<span style='color:red'>X</span>")), "ID");
        var colored = TableLayoutTests.Layout(styled, Page);
        Assert.That(colored, Has.Count.EqualTo(2));
        Assert.That(colored.SelectMany(p => p.Runs).Count(r => r.Text == "X"), Is.EqualTo(10));
        Assert.That(colored.SelectMany(p => p.Runs).Where(r => r.Text == "X").Select(r => r.Style.Color), Is.All.EqualTo(new Rgb(1, 0, 0)));
        Assert.That(colored.SelectMany(p => p.Runs).Where(r => r.Text.StartsWith('L')).Select(r => r.Text), Is.EqualTo(Enumerable.Range(1, 10).Select(i => $"L{i:00}")));
        var count = Commands(colored);
        Assert.That(TableLayoutTests.Pdf(styled, Page with { MaxDisplayCommands = count }), Is.Not.Empty);
        Fails(styled, "FPDF1401", Page with { MaxDisplayCommands = count - 1 });
    }

    [TestCase("0.000000000000000001")]
    [TestCase("0.1")]
    [TestCase("0")]
    public void FirstBodyGroupIdentityUsesPositiveRowsInsteadOfRoundedCoordinates(string precedingHeight)
    {
        var html = Pair("<p style='margin-top:70pt;margin-bottom:0'>A</p>", "B1<br>B2<br>B3", "line-height:1", "line-height:5", header: false)
            .Replace("line-height:1.2'", "line-height:1;padding-top:10pt'")
            .Replace("<tbody>", $"<tbody><tr><td style='padding-top:{precedingHeight}pt'></td><td></td></tr>");
        if (precedingHeight == "0")
        {
            // A zero-height predecessor stays in the first group: the table's
            // ten-point top leaves only seventy points for the eighty-point A.
            Fails(html, "FPDF1302", Page);
            return;
        }
        // Every positive row consumes the first-group identity, including one
        // too small to change a table-local double coordinate at top ten.
        var pages = TableLayoutTests.Layout(html, Page);
        Assert.That(pages, Has.Count.EqualTo(3));
        Assert.That(pages[0].Runs.Select(r => r.Text), Is.EqualTo(new[] { "B1" }));
        Assert.That(pages[1].Runs.Select(r => r.Text), Is.EqualTo(new[] { "A", "B2" }));
        Assert.That(pages[2].Runs.Select(r => r.Text), Is.EqualTo(new[] { "B3" }));
        Assert.That(pages[1].Runs.Single(r => r.Text == "A").Baseline, Is.EqualTo(12));
        Assert.That(TableLayoutTests.Pdf(html, Page), Is.Not.Empty);
    }

    private static void Fails(string html, string code, PdfOptions options)
    {
        using var output = new MemoryStream(); output.Write("sentinel"u8); output.Position = 2;
        Assert.That(Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, options))!.Code, Is.EqualTo(code));
        Assert.That(output.ToArray(), Is.EqualTo("sentinel"u8.ToArray())); Assert.That(output.Position, Is.EqualTo(2));
    }
}
