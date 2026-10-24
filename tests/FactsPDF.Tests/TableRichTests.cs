using NUnit.Framework;

namespace FactsPDF.Tests;

public sealed class TableRichTests
{
    private const string Cols = "<colgroup><col style='width:40pt'><col style='width:60pt'><col style='width:100pt'></colgroup>";
    private const string Edges = "padding:4pt;border:1pt solid red";
    internal static string Rich(string content, string cellStyle = Edges, string span = "2", string suffix = "")
        => "<table style='width:200pt;font-size:10pt;line-height:1.2'>" + Cols + "<tbody><tr><td colspan='" + span + "' style='" + cellStyle + "'>" + content + "</td><td>Z</td></tr>" + suffix + "</tbody></table>";

    [TestCase("2")][TestCase("02")][TestCase("  \t0002\r\n")]
    public void SpanUsesCoveredTracksAndPaintsOneFullWidthCell(string span)
    {
        var page = TableLayoutTests.Layout(Rich("A", span: span, suffix: "<tr><td style='border:1pt solid blue'>B</td><td>C</td><td>D</td></tr>")).Single();
        Assert.That(page.PaintBoxes.Select(b => b.Width), Is.EqualTo(new[] { 100d, 40 }));
        Assert.That(page.PaintBoxes[0].Height, Is.EqualTo(22));
        Assert.That(page.PaintBoxes[1].Top, Is.EqualTo(58));
        Assert.That(page.PaintBoxes[1].X + page.PaintBoxes[1].Width, Is.EqualTo(76));
        Assert.That(page.Runs.Select(r => r.X), Is.EqualTo(new[] { 41d, 136, 37, 76, 136 }));
        Assert.That(841.89 - page.Runs[0].Baseline, Is.EqualTo(50).Within(0.000001));
    }
    [TestCase("")][TestCase("0")][TestCase("000")][TestCase("4")][TestCase("+2")][TestCase("-1")]
    [TestCase("2.0")][TestCase("２")][TestCase("٢")][TestCase("2 0")][TestCase("\u00a02")]
    [TestCase("999999999999999999999999999999999999999")]
    public void InvalidSpanFailsAtCellAndPreservesOutput(string span)
    {
        var html = Rich("A", span: span); TableStructureTests.Fails(html, "FPDF1103");
        var error = Assert.Throws<FactsPdfException>(() => HtmlDocumentReader.ReadTree(html, new(), default))!;
        Assert.That(error.SourceOffset, Is.EqualTo(html.IndexOf("<td", StringComparison.Ordinal)));
    }
    [TestCase("table")][TestCase("colgroup")][TestCase("tbody")][TestCase("tr")][TestCase("p")][TestCase("span")][TestCase("img")]
    public void SpanIsOnlyApplicableToCells(string tag)
    {
        var html = tag is "p" or "span" ? $"<{tag} colspan='1'>A</{tag}>" : tag == "img" ? "<img src='logo' colspan='1'>"
            : TableStructureTests.One.Replace("<" + tag + ">", "<" + tag + " colspan='1'>");
        TableStructureTests.Fails(html, "FPDF1103", TableImageTests.Options());
    }
    [Test]
    public void OverfilledRowFailsAtCellStartAndUnderfilledAtRowEnd()
    {
        var over = Rich("A", span: "3"); var under = Rich("A", span: "1");
        var error = Assert.Throws<FactsPdfException>(() => HtmlDocumentReader.ReadTree(over, new(), default))!;
        Assert.That(error.Code, Is.EqualTo("FPDF1101"));
        Assert.That(error.SourceOffset, Is.EqualTo(over.IndexOf("<td>Z", StringComparison.Ordinal)));
        var underflow = Assert.Throws<FactsPdfException>(() => HtmlDocumentReader.ReadTree(under, new(), default))!;
        Assert.That(underflow.SourceOffset, Is.EqualTo(under.IndexOf("</tr>", StringComparison.Ordinal)));
        TableStructureTests.Fails(over, "FPDF1101"); TableStructureTests.Fails(under, "FPDF1101");
    }
    [Test]
    public void OrderedFlowKeepsParagraphsInsideCellAndAnonymousCellStyle()
    {
        var options = TableImageTests.Options();
        var html = "<p>BEFORE</p>" + Rich("<span style='text-align:left;color:red'>A</span><img src='logo'><p style='text-align:center'>B <span style='color:blue'>C</span></p>D<img src='logo'>E", Edges + ";text-align:right") + "<p>AFTER</p>";
        var root = HtmlDocumentReader.ReadTree(html, options, default); Assert.That(root.Children, Has.Count.EqualTo(3));
        var paragraphs = BlockTreeTraversal.Paragraphs(root, default).ToArray();
        Assert.That(paragraphs.Select(p => string.Concat(p.Runs.Select(r => r.Text))), Is.EqualTo(new[] { "BEFORE", "A", "B C", "D", "E", "Z", "AFTER" }));
        Assert.That(paragraphs[1].Style.Alignment, Is.EqualTo(TextAlignment.Right));
        Assert.That(paragraphs[1].Style.Color, Is.EqualTo(new Rgb(0, 0, 0)));
        Assert.That(paragraphs[1].Runs.Single().Style.Color, Is.EqualTo(new Rgb(1, 0, 0)));
        Assert.That(paragraphs[2].Style.Alignment, Is.EqualTo(TextAlignment.Center));
        var page = TextLayout.Layout(root, options, default).Single(); Assert.That(page.Images, Has.Count.EqualTo(2));
        Assert.That(page.Images.All(i => ReferenceEquals(i.Image, options.Images["logo"])), Is.True);
        Assert.That(page.Runs.Select(r => r.Text), Is.EqualTo(new[] { "BEFORE", "A", "B ", "C", "D", "E", "Z", "AFTER" }));
        Assert.That(page.Images[0].Top, Is.LessThan(841.89 - page.Runs[2].Baseline));
        Assert.That(page.Images[1].Top, Is.GreaterThan(841.89 - page.Runs[4].Baseline));
    }
    [Test]
    public void ParagraphMarginsCollapseWithLiteralFortyFivePointRow()
    {
        var page = TableLayoutTests.Layout(Rich("<p style='margin-top:2pt;margin-bottom:6pt'>A</p><p style='margin-top:4pt;margin-bottom:3pt'>B</p>")).Single();
        Assert.That(page.PaintBoxes.Single().Height, Is.EqualTo(45));
        Assert.That(page.Runs.Take(2).Select(r => 841.89 - r.Baseline), Is.EqualTo(new[] { 52d, 70 }).Within(0.000001));
    }
    [TestCase("left", 44)][TestCase("center", 63.5)][TestCase("right", 83)]
    public void ParagraphImageParagraphUsesSpanPercentagesAndLiteralGeometry(string align, double pixelX)
    {
        var page = TableLayoutTests.Layout(Rich("<p>A</p><img src='logo' style='width:50%;padding:2pt;border:1pt solid blue;text-align:left'><p>B</p>", Edges + ";text-align:" + align), TableImageTests.Options()).Single();
        Assert.That(page.PaintBoxes.Select(b => b.Height), Is.EqualTo(new[] { 78.5, 28.5 }));
        Assert.That(page.PaintBoxes.Select(b => b.Width), Is.EqualTo(new[] { 100d, 51 }));
        var image = page.Images.Single(); Assert.That(image.X, Is.EqualTo(pixelX)); Assert.That(image.Top, Is.EqualTo(64));
        Assert.That(image.Width, Is.EqualTo(45)); Assert.That(image.Height, Is.EqualTo(22.5));
        Assert.That(page.Runs.Take(2).Select(r => 841.89 - r.Baseline), Is.EqualTo(new[] { 50d, 98.5 }).Within(0.000001));
        Assert.That(page.PaintBoxes[0].Order, Is.LessThan(page.PaintBoxes[1].Order));
    }
    [TestCase("<p></p>", 18)][TestCase("<p> \n </p><p></p>", 18)]
    [TestCase("<p style='margin-top:11pt;margin-bottom:7pt'></p>", 21)]
    [TestCase(" \n ", 10)][TestCase("<span></span><b> </b>", 10)][TestCase("<br>", 22)]
    [TestCase("<p>A</p><p style='margin-top:20pt;margin-bottom:7pt'></p><p>B</p>", 62)]
    public void EmptyParagraphMarginsFoldWithoutPhantomLines(string content, double height)
        => Assert.That(TableLayoutTests.Layout(Rich(content).Replace("<td>Z</td>", "<td></td>")).Single().PaintBoxes.Single().Height, Is.EqualTo(height));
    [TestCase("<p><p>A</p></p>")][TestCase("<p>A</td>")][TestCase("<span><p>A</p></span>")]
    [TestCase("<p><img src='logo'></p>")][TestCase("<span><img src='logo'></span>")]
    [TestCase("<h1>A</h1>")][TestCase("<div>A</div>")][TestCase("<section>A</section>")]
    [TestCase("<ul><li>A</li></ul>")][TestCase(TableStructureTests.One)]
    public void ParentRulesRejectWithoutOrdinaryParagraphRepair(string content)
        => TableStructureTests.Fails(Rich(content), "FPDF1101", TableImageTests.Options());
    [TestCase("width:auto")][TestCase("padding:initial")][TestCase("border:inherit")]
    [TestCase("background-color:unset")][TestCase("break-before:auto")][TestCase("break-after:initial")]
    [TestCase("break-after:inherit")][TestCase("break-after:unset")]
    public void InapplicableParagraphPropertiesFailBeforeWinner(string declaration)
        => TableStructureTests.Fails("<style>td p{" + declaration + "}td p{color:red!important}</style>" + TableLayoutTests.Table("<p style='color:blue'>A</p>"), "FPDF1201");
    [Test]
    public void ParagraphSelectorsAndGlobalMarginsUseActualAncestry()
    {
        var html = "<style>table>tbody>tr>td>p{color:red;margin-bottom:initial}td p span{color:blue}p{margin-top:inherit}</style>" + Rich("<p>A<span>B</span></p>") + "<p style='width:100pt;padding:1pt;break-before:auto'>OUT</p>";
        var paragraphs = HtmlDocumentReader.Read(html, new(), default);
        Assert.That(paragraphs[0].Style.MarginAfter, Is.Zero);
        Assert.That(paragraphs[0].Runs.Select(r => r.Style.Color), Is.EqualTo(new[] { new Rgb(1, 0, 0), new Rgb(0, 0, 1) }));
        Assert.That(TableLayoutTests.Pdf(html), Is.Not.Empty);
    }
    [Test]
    public void EmptyBoldHeaderParagraphsAndImageOnlyCellsHaveNoFontDemand()
    {
        var options = TableImageTests.Options() with { Fonts = [PdfFont.LoadTrueType(FontFixture.Create())] };
        var html = "<table style='width:200pt;font-size:10pt;line-height:1.2'>" + Cols + "<thead><tr><th colspan='2' style='" + Edges + "'><p> </p><p></p><img src='logo'></th><th></th></tr></thead><tbody><tr><td colspan='3'>A</td></tr></tbody></table>";
        Assert.That(TableLayoutTests.Layout(html, options).Single().PaintBoxes.Single().Height, Is.EqualTo(30));
        Assert.That(TableLayoutTests.Pdf(html, options), Is.Not.Empty);
        Assert.That(HtmlDocumentReader.Read(TableLayoutTests.Table(""), new(), default), Has.Count.EqualTo(1));
        Assert.That(HtmlDocumentReader.Read(TableLayoutTests.Table("<img src='logo'>"), options, default), Is.Empty);
    }
    [Test]
    public void DirectParagraphWithoutSpanIsCapturedInCell()
    {
        var paragraphs = HtmlDocumentReader.Read(TableLayoutTests.Table("A<p>B</p>C"), new(), default);
        Assert.That(paragraphs.Select(p => string.Concat(p.Runs.Select(r => r.Text))), Is.EqualTo(new[] { "A", "B", "C" }));
    }
    [Test]
    public void OversizeRowsUnbreakableParagraphsAndNarrowSpansPreserveOutput()
    {
        var options = TableImageTests.Options() with { PageHeight = 100, Margin = 10 };
        TableStructureTests.Fails(Rich("<p>A</p><img src='logo' style='width:80pt'><p>B</p>"), "FPDF1302", options);
        TableStructureTests.Fails(Rich("<p>UNBREAKABLEUNBREAKABLE</p>"), "FPDF1302");
        TableStructureTests.Fails(Rich("A", "padding:50pt"), "FPDF1302");
        Assert.That(TableLayoutTests.Pdf(Rich("<p>A</p>"), options), Is.Not.Empty);
    }
    [Test]
    public void OmittedAndExplicitUnitSpanAreByteEquivalent()
    {
        var html = TableLayoutTests.Table("<span style='text-align:right'>A</span>");
        Assert.That(TableLayoutTests.Pdf(html.Replace("<td ", "<td colspan='0001' ")), Is.EqualTo(TableLayoutTests.Pdf(html)));
    }

    [Test]
    public void FirstLastAndAllSixteenColumnsCanBeCovered()
    {
        var start = "<table style='width:160pt;font-size:10pt;line-height:1.2'><colgroup>"
            + string.Concat(Enumerable.Repeat("<col style='width:6.25%'>", 16)) + "</colgroup>";
        var html = start + "<thead><tr><th colspan='16' style='font-weight:normal'>H</th></tr></thead><tbody>"
            + "<tr><td colspan='15'>A</td><td>B</td></tr><tr><td>A</td><td colspan='15'>B</td></tr></tbody></table>";
        var page = TableLayoutTests.Layout(html).Single();
        Assert.That(page.Runs.Select(r => r.Text), Is.EqualTo(new[] { "H", "A", "B", "A", "B" }));
        Assert.That(page.Runs.Select(r => r.X), Is.EqualTo(new[] { 36d, 36, 186, 36, 46 }));
        TableStructureTests.Fails(html.Replace("colspan='15'", "colspan='16'"), "FPDF1101");
    }

    [Test]
    public void SpannedPercentPaddingUsesCompleteTrackWidth()
    {
        var page = TableLayoutTests.Layout(Rich("A", "padding:4%;border:1pt solid red")).Single();
        Assert.That(page.Runs[0].X, Is.EqualTo(41));
        Assert.That(page.PaintBoxes.Single().Height, Is.EqualTo(22));
    }

    [Test]
    public void EmptyWrappersAndFormattingWhitespaceDoNotSplitOrEscapeFlow()
    {
        var options = TableImageTests.Options();
        var html = TableLayoutTests.Table("<span></span> <!-- a --> <img src='logo'><b> </b><img src='logo'> A<br>B") + "<p>AFTER</p>";
        var paragraphs = HtmlDocumentReader.Read(html, options, default);
        Assert.That(paragraphs.Select(p => string.Concat(p.Runs.Select(r => r.Text))), Is.EqualTo(new[] { " AB", "AFTER" }));
        var page = TableLayoutTests.Layout(html, options).Single();
        Assert.That(page.Images.Select(i => i.Top), Is.EqualTo(new[] { 36d, 48 }));
        Assert.That(page.Runs.Select(r => r.Text), Is.EqualTo(new[] { "A", "B", "AFTER" }));
    }

    [Test]
    public void NonBreakingSpaceIsContentAndBreakOnlyPreservesExistingFontBehavior()
    {
        var font = PdfFont.LoadTrueType(FontFixture.Create());
        var options = new PdfOptions { Fonts = [font] };
        var nbsp = HtmlDocumentReader.Read(TableLayoutTests.Table("<p>&nbsp;</p>"), options, default).Single();
        Assert.That(nbsp.Runs.Single().Text, Is.EqualTo("\u00a0"));
        var html = "<table style='width:100pt'>" + TableStructureTests.Columns
            + "<thead><tr><th><p><br></p></th></tr></thead><tbody><tr><td>A</td></tr></tbody></table>";
        TableStructureTests.Fails(html, "FPDF1507", options);
    }

    private static string RichHeader()
        => "<style>td,th{padding:2pt;border:1pt solid red;background-color:blue}</style>"
        + "<table style='width:100pt;font-size:10pt;line-height:1.2'><colgroup><col style='width:40pt'><col style='width:60pt'></colgroup>"
        + "<thead><tr><th colspan='2' style='font-weight:normal'><p style='margin-bottom:2pt'>H</p><img src='logo' style='width:16pt'><p style='margin-bottom:0;margin-top:2pt'>N</p></th></tr></thead><tbody>"
        + string.Concat(Enumerable.Range(1, 5).Select(i => $"<tr><td colspan='2'>R{i}</td></tr>")) + "</tbody></table>";

    [Test]
    public void RichHeaderRepeatsWholeRowsAndChargesEveryRepeatedCommand()
    {
        var options = TableImageTests.Options() with { PageWidth = 240, PageHeight = 100, Margin = 10 };
        var html = RichHeader(); var pages = TableLayoutTests.Layout(html, options);
        Assert.That(pages, Has.Count.EqualTo(3));
        Assert.That(pages.SelectMany(p => p.Runs).Select(r => r.Text), Is.EqualTo(new[] { "H", "N", "R1", "R2", "H", "N", "R3", "R4", "H", "N", "R5" }));
        Assert.That(pages.SelectMany(p => p.Images).Select(i => i.Top), Is.EqualTo(new[] { 27d, 27, 27 }));
        Assert.That(pages.SelectMany(p => p.Images).All(i => ReferenceEquals(i.Image, options.Images["logo"])), Is.True);
        // Each cell paints background+four borders; three header and five body cells.
        // Eleven text runs and three repeated image draws complete the exact budget.
        Assert.That(pages.Sum(p => p.Runs.Count + p.Images.Count + p.PaintBoxes.Sum(b => b.CommandCount)), Is.EqualTo(54));
        Assert.That(TableLayoutTests.Pdf(html, options with { MaxDisplayCommands = 54 }), Is.Not.Empty);
        TableStructureTests.Fails(html, "FPDF1401", options with { MaxDisplayCommands = 53 });
        using var output = new MemoryStream(); output.Write("sentinel"u8); output.Position = 3;
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, options with { MaxPages = 2 }))!;
        Assert.That(error.Code, Is.EqualTo("FPDF1303"));
        Assert.That(output.ToArray(), Is.EqualTo("sentinel"u8.ToArray())); Assert.That(output.Position, Is.EqualTo(3));
        TableStructureTests.Fails(html.Replace("width:16pt", "width:80pt"), "FPDF1302", options);
        Assert.That(TableLayoutTests.Pdf(html, options), Is.Not.Empty);
    }

    [Test]
    public void CommandLimitPrecedesWrappingNextRichParagraph()
    {
        var html = TableLayoutTests.Table("<p style='margin-bottom:0'>A</p><p>UNBREAKABLEUNBREAKABLE</p>", "width:40pt");
        TableStructureTests.Fails(html, "FPDF1401", new() { MaxDisplayCommands = 1 });
    }

    [Test]
    public void RichCellMeasureAndProjectionObserveCancellationBetweenItems()
    {
        var options = TableImageTests.Options();
        var root = HtmlDocumentReader.ReadTree(TableLayoutTests.Table("<p>A</p><img src='logo'><p>B</p>"), options, default);
        using var source = new CancellationTokenSource();
        using var paragraphs = BlockTreeTraversal.Paragraphs(root, source.Token).GetEnumerator();
        Assert.That(paragraphs.MoveNext(), Is.True); source.Cancel();
        Assert.Throws<OperationCanceledException>(() => paragraphs.MoveNext());
        using var measuring = new CancellationTokenSource();
        var table = (LayoutTable)BlockLayout.Steps(root, options, default).Single();
        Assert.Throws<OperationCanceledException>(() => TableLayout.Measure(table, options, new(), measuring.Token,
            (_, _) => measuring.Cancel(), (_, _) => { }));
    }

    [Test]
    public void PendingMarginsAreBoundedBeforeFollowingFontResolution()
    {
        var html = Rich("<p style='margin-top:81pt'></p><p><b>A</b></p>");
        TableStructureTests.Fails(html, "FPDF1302", new() { PageHeight = 100, Margin = 10 });
        var unbreakable = Rich("<p>UNBREAKABLEUNBREAKABLE</p>");
        var error = Assert.Throws<FactsPdfException>(() => TableLayoutTests.Layout(unbreakable))!;
        Assert.That(error.SourceOffset, Is.EqualTo(unbreakable.IndexOf("<p>", StringComparison.Ordinal)));
    }

}
