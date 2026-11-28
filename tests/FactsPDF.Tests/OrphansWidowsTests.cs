using NUnit.Framework;

namespace FactsPDF.Tests;

// CSS Fragmentation orphans and widows. Every expected page split below is what Chrome printed for
// the same synthetic page (200pt square, 20pt margins, 20pt lines, so eight lines a page): the
// numbered lines on each printed page, pages separated by '|'. Without a declaration every
// paragraph, list item and table cell paragraph uses the CSS initial value orphans:2;widows:2.
public sealed class OrphansWidowsTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 200, PageHeight = 200, Margin = 20 };

    private const string Base = "p,h2,h3,li,pre{margin:0;font-size:10pt;line-height:20pt}h2,h3{font-weight:normal}"
        + "ul{margin:0;padding-left:20pt}";

    private static string P(string label, int lines, string style = "", string tag = "p")
        => $"<{tag}{(style.Length == 0 ? "" : $" style='{style}'")}>"
            + string.Join("<br>", Enumerable.Range(1, lines).Select(i => $"{label}{i}")) + $"</{tag}>";

    private static string Doc(string css, string body)
        => $"<!doctype html><html><head><style>{Base}{css}</style></head><body>{body}</body></html>";

    private static string Split(string html) => string.Join(" | ", TableLayoutTests.Layout(html, Page)
        .Select(page => string.Join(' ', page.Runs.Select(run => run.Text))));

    private static byte[] Pdf(string html) => TableLayoutTests.Pdf(html, Page);

    private static void RejectWithoutOutput(string html, string code, int? offset = null)
    {
        using var output = new MemoryStream();
        output.Write("ORIGINAL"u8);
        output.Position = 3;
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, Page));
        Assert.That(error!.Code, Is.EqualTo(code));
        if (offset.HasValue) Assert.That(error.SourceOffset, Is.EqualTo(offset.Value));
        Assert.That(output.ToArray(), Is.EqualTo("ORIGINAL"u8.ToArray()));
        Assert.That(output.Position, Is.EqualTo(3));
    }

    public static IEnumerable<TestCaseData> ChromeSplits()
    {
        TestCaseData Case(string name, string css, string body, string chrome)
            => new TestCaseData(css, body, chrome).SetName(name);
        yield return Case("OneAndOneBreaksWhereThePageEnds", "", P("F", 7) + P("T", 5, "orphans:1;widows:1"),
            "F1 F2 F3 F4 F5 F6 F7 T1 | T2 T3 T4 T5");
        yield return Case("OneAndOneLeavesASingleWidow", "", P("F", 4) + P("T", 5, "orphans:1;widows:1"),
            "F1 F2 F3 F4 T1 T2 T3 T4 | T5");
        yield return Case("WidowsMoveTheBreakEarlier", "", P("F", 3) + P("T", 7, "orphans:3;widows:3"),
            "F1 F2 F3 T1 T2 T3 T4 | T5 T6 T7");
        yield return Case("OrphansWinOverWidows", "", P("F", 5) + P("T", 4, "orphans:3;widows:3"),
            "F1 F2 F3 F4 F5 T1 T2 T3 | T4");
        yield return Case("ShortParagraphKeepsItsOrphans", "", P("F", 6) + P("T", 3, "orphans:2;widows:2"),
            "F1 F2 F3 F4 F5 F6 T1 T2 | T3");
        yield return Case("WidowsAtTheTopOfAPage", "", P("T", 9, "widows:3"), "T1 T2 T3 T4 T5 T6 | T7 T8 T9");
        yield return Case("WidowsBeyondTheParagraphKeepOnlyTheOrphans", "", P("T", 9, "orphans:2;widows:9"),
            "T1 T2 | T3 T4 T5 T6 T7 T8 T9");
        yield return Case("WidowsLargerThanTheRestKeepTheInitialOrphans", "", P("F", 5) + P("T", 4, "widows:5"),
            "F1 F2 F3 F4 F5 T1 T2 | T3 T4");
        yield return Case("OrphansTallerThanAPageAtTheTopBreakWhereThePageEnds", "", P("T", 12, "orphans:10"),
            "T1 T2 T3 T4 T5 T6 T7 T8 | T9 T10 T11 T12");
        yield return Case("OrphansTallerThanAPageMoveOnceThenBreakWhereEachPageEnds", "", P("F", 2) + P("T", 20, "orphans:10"),
            "F1 F2 | T1 T2 T3 T4 T5 T6 T7 T8 | T9 T10 T11 T12 T13 T14 T15 T16 | T17 T18 T19 T20");
        yield return Case("WidowsApplyOnEveryContinuedPage", "", P("F", 2) + P("T", 15, "widows:3"),
            "F1 F2 T1 T2 T3 T4 T5 T6 | T7 T8 T9 T10 T11 T12 | T13 T14 T15");
        yield return Case("WidowsOnTheLastOfThreePages", "", P("T", 17, "widows:3"),
            "T1 T2 T3 T4 T5 T6 T7 T8 | T9 T10 T11 T12 T13 T14 | T15 T16 T17");
        yield return Case("TooFewOrphansMoveTheWholeParagraph", "", P("F", 6) + P("T", 11, "orphans:3;widows:3"),
            "F1 F2 F3 F4 F5 F6 | T1 T2 T3 T4 T5 T6 T7 T8 | T9 T10 T11");
        yield return Case("WidowsAloneStillKeepTwoOrphans", "", P("F", 7) + P("T", 5, "widows:1"),
            "F1 F2 F3 F4 F5 F6 F7 | T1 T2 T3 T4 T5");
        yield return Case("InitialIsTwo", "p.t{orphans:initial}", P("F", 7) + P("T", 5).Replace("<p>", "<p class='t'>"),
            "F1 F2 F3 F4 F5 F6 F7 | T1 T2 T3 T4 T5");
        yield return Case("InheritWithNothingAboveIsTwo", "", P("F", 4) + "<div>" + P("T", 5, "widows:inherit") + "</div>",
            "F1 F2 F3 F4 T1 T2 T3 | T4 T5");
        yield return Case("InheritedFromBody", "body{orphans:3;widows:3}", P("F", 3) + P("T", 7),
            "F1 F2 F3 T1 T2 T3 T4 | T5 T6 T7");
        yield return Case("MarginsBetweenParagraphs", "p.t{orphans:2;widows:2;margin-top:10pt}",
            P("F", 5) + P("T", 4).Replace("<p>", "<p class='t'>"), "F1 F2 F3 F4 F5 T1 T2 | T3 T4");
        yield return Case("MultiLineHeading", "p,h2,h3{orphans:3;widows:3}", P("F", 6) + P("H", 4, tag: "h2"),
            "F1 F2 F3 F4 F5 F6 | H1 H2 H3 H4");
        yield return Case("PreformattedText", "pre{orphans:2}", P("F", 7) + "<pre>T1\nT2\nT3\nT4\nT5</pre>",
            "F1 F2 F3 F4 F5 F6 F7 | T1 T2 T3 T4 T5");
        yield return Case("AnonymousTextInAPanel", "div.t{orphans:3}", P("F", 6) + "<div class='t'>T1<br>T2<br>T3<br>T4<br>T5</div>",
            "F1 F2 F3 F4 F5 F6 | T1 T2 T3 T4 T5");
        yield return Case("PaddedBorderedPanelMovesWithItsParagraph", "div.panel{padding:10pt;border:1pt solid #000}p{orphans:2}",
            P("F", 6) + "<div class='panel'>" + P("T", 5) + "</div>", "F1 F2 F3 F4 F5 F6 | T1 T2 T3 T4 T5");
        yield return Case("ListItemMovesWithItsMarker", "ul{orphans:3}", P("F", 6) + "<ul>" + P("T", 5, tag: "li") + "</ul>",
            "F1 F2 F3 F4 F5 F6 | T1 T2 T3 T4 T5");
        yield return Case("ListItemWidows", "li{orphans:3}",
            P("F", 5) + "<ul>" + P("A", 4, tag: "li") + P("B", 2, tag: "li") + "</ul>", "F1 F2 F3 F4 F5 A1 A2 A3 | A4 B1 B2");
        yield return Case("ForcedBreakComesFirst", "p{orphans:3;widows:3}",
            P("F", 3) + "<p style='break-before:page'>T1<br>T2</p>" + P("U", 9), "F1 F2 F3 | T1 T2 U1 U2 U3 U4 U5 U6 | U7 U8 U9");
        yield return Case("KeptHeadingMovesWithAParagraphThatCannotKeepItsOrphans", "h2{break-after:avoid}",
            P("F", 5) + P("H", 1, tag: "h2") + P("T", 10, "orphans:3"), "F1 F2 F3 F4 F5 | H1 T1 T2 T3 T4 T5 T6 T7 | T8 T9 T10");
        yield return Case("KeptHeadingChainMovesTogether",
            "h2,h3{break-after:avoid;margin:0;font-size:10pt;line-height:20pt}p.t{orphans:3}",
            P("F", 4) + P("H", 1, tag: "h2") + P("G", 1, tag: "h3") + P("T", 6).Replace("<p>", "<p class='t'>"),
            "F1 F2 F3 F4 | H1 G1 T1 T2 T3 T4 T5 T6");
        yield return Case("KeptHeadingAtTheTopIsNotSeparatedFromItsParagraph", "h2{break-after:avoid}",
            P("H", 1, tag: "h2") + P("T", 10, "orphans:9"), "H1 T1 T2 T3 T4 T5 T6 T7 | T8 T9 T10");
        yield return Case("UndeclaredParagraphMovesWhenOneLineFits", "", P("F", 7) + P("T", 5),
            "F1 F2 F3 F4 F5 F6 F7 | T1 T2 T3 T4 T5");
        yield return Case("UndeclaredParagraphNeverLeavesOneLastLine", "", P("F", 4) + P("T", 5),
            "F1 F2 F3 F4 T1 T2 T3 | T4 T5");
        yield return Case("UndeclaredListItemMovesWithItsMarker", "", P("F", 7) + "<ul>" + P("T", 5, tag: "li") + "</ul>",
            "F1 F2 F3 F4 F5 F6 F7 | T1 T2 T3 T4 T5");
        yield return Case("KeptHeadingStaysWithTheLinesThatKeepItsOrphans", "h2{break-after:avoid}p.t{orphans:2}",
            P("F", 3) + P("H", 1, tag: "h2") + P("T", 6).Replace("<p>", "<p class='t'>"), "F1 F2 F3 H1 T1 T2 T3 T4 | T5 T6");
        yield return Case("KeptHeadingWithoutDeclarationsBreaksInsideItsParagraph", "h2{break-after:avoid}",
            P("F", 3) + P("H", 1, tag: "h2") + P("T", 6), "F1 F2 F3 H1 T1 T2 T3 T4 | T5 T6");
        yield return Case("UnkeptHeadingStaysBehind", "", P("F", 5) + P("H", 1, tag: "h2") + P("T", 10, "orphans:3"),
            "F1 F2 F3 F4 F5 H1 | T1 T2 T3 T4 T5 T6 T7 T8 | T9 T10");
    }

    [TestCaseSource(nameof(ChromeSplits))]
    public void BreaksWhereChromeBreaks(string css, string body, string chrome)
        => Assert.That(Split(Doc(css, body)), Is.EqualTo(chrome));

    // orphans:2;widows:2 is the initial value, so declaring it changes nothing.
    [TestCase(7, 5)]
    [TestCase(4, 5)]
    public void DeclaringTheInitialValueChangesNothing(int filler, int lines)
        => Assert.That(Pdf(Doc("", P("F", filler) + P("T", lines))),
            Is.EqualTo(Pdf(Doc("", P("F", filler) + P("T", lines, "orphans:2;widows:2")))));

    [Test]
    public void MovedListItemDrawsItsMarkerOnTheNextPage()
    {
        var pages = TableLayoutTests.Layout(Doc("ul{orphans:3}", P("F", 6) + "<ul>" + P("T", 5, tag: "li") + "</ul>"), Page);
        Assert.That(pages.Select(page => page.Discs.Count), Is.EqualTo(new[] { 0, 1 }));
    }

    [Test]
    public void EveryLineIsPlacedOnceInOrder()
    {
        for (var filler = 0; filler <= 8; filler++)
            for (var lines = 1; lines <= 18; lines++)
                for (var orphans = 1; orphans <= 5; orphans++)
                    for (var widows = 1; widows <= 5; widows++)
                    {
                        var body = (filler == 0 ? "" : P("F", filler)) + P("T", lines, $"orphans:{orphans};widows:{widows}");
                        var pages = TableLayoutTests.Layout(Doc("", body), Page);
                        var placed = pages.SelectMany(page => page.Runs.Select(run => run.Text)).Where(text => text[0] == 'T');
                        Assert.That(placed, Is.EqualTo(Enumerable.Range(1, lines).Select(i => $"T{i}")),
                            $"{filler} filler, {lines} lines, orphans {orphans}, widows {widows}");
                        Assert.That(pages.All(page => page.Runs.Count > 0), Is.True, "no empty page");
                    }
    }

    [Test]
    public void ParagraphsThatFitRenderByteIdenticalToTheUndeclaredPage()
    {
        var body = P("F", 3) + P("T", 4);
        Assert.That(Pdf(Doc("p{orphans:3;widows:3}", body)), Is.EqualTo(Pdf(Doc("", body))));
    }

    [Test]
    public void KeptCardMovesWholeAsWithoutTheDeclaration()
    {
        var body = P("F", 6) + "<div class='card'>" + P("T", 4) + "</div>";
        Assert.That(Split(Doc("div.card{break-inside:avoid}p{orphans:2}", body)), Is.EqualTo("F1 F2 F3 F4 F5 F6 | T1 T2 T3 T4"));
        Assert.That(Pdf(Doc("div.card{break-inside:avoid}p{orphans:2}", body)),
            Is.EqualTo(Pdf(Doc("div.card{break-inside:avoid}", body))));
    }

    [TestCase("99999999999")]
    [TestCase("40000")]
    public void ValuesAboveChromesLimitComputeTo32767(string value)
        => Assert.That(Pdf(Doc($"p{{orphans:{value}}}", P("F", 2) + P("T", 12))),
            Is.EqualTo(Pdf(Doc("p{orphans:32767}", P("F", 2) + P("T", 12)))));

    [TestCase("p{orphans:+3}", "p{orphans:3}")]
    [TestCase("p{orphans:03}", "p{orphans:3}")]
    [TestCase("p{ORPHANS:3 !important}", "p{orphans:3}")]
    [TestCase("div{widows:3}p{widows:unset}", "p{widows:3}")]
    [TestCase("p{orphans:3;orphans:inherit}", "p{orphans:2}")]
    public void SpellingsComputeTheSameValue(string css, string same)
    {
        var body = "<div>" + P("F", 5) + P("T", 4) + "</div>";
        Assert.That(Pdf(Doc(css, body)), Is.EqualTo(Pdf(Doc(same, body))), css);
    }

    [Test]
    public void InlineStyleAttributeMatchesTheStylesheet()
        => Assert.That(Pdf(Doc("", P("F", 4) + P("T", 5, "orphans:3;widows:3"))),
            Is.EqualTo(Pdf(Doc("p.t{orphans:3;widows:3}", P("F", 4) + P("T", 5).Replace("<p>", "<p class='t'>")))));

    // Inline text, br, img, hr and col hold no line box and no block can nest in them, so the value
    // reaches no paragraph: Chrome ignores it there and FactsPDF validates and drops it.
    [TestCase("span{orphans:5}")]
    [TestCase("u,br,hr{widows:4}")]
    [TestCase("span,u,img,col{orphans:1;widows:9}")]
    public void DeclarationsWithoutLineBoxesChangeNothing(string css)
    {
        var body = P("F", 6) + "<p>T1<br><span>T2</span><br><u>T3</u><br>T4</p><hr>" + P("U", 3);
        Assert.That(Pdf(Doc(css, body)), Is.EqualTo(Pdf(Doc("", body))), css);
    }

    [Test]
    public void TheSdkAcceptsTheDeclaration()
    {
        var html = Doc("p{orphans:3;widows:3}", P("F", 3) + P("T", 7));
        Assert.That(FactsPDF.Pdf.Write().ToBytes(html), Is.Not.Empty);
    }

    [TestCase("0")]
    [TestCase("-1")]
    [TestCase("3.0")]
    [TestCase("3e0")]
    [TestCase("3px")]
    [TestCase("auto")]
    public void OtherValuesFailFpdf1202(string value)
    {
        foreach (var property in new[] { "orphans", "widows" })
        {
            var html = Doc($"p{{{property}:{value}}}", P("T", 3));
            RejectWithoutOutput(html, "FPDF1202", html.IndexOf(property + ":", StringComparison.Ordinal) + property.Length + 1);
        }
    }

    [TestCase("p{orphans:2 3}")]
    [TestCase("p{widows:env(x)}")]
    [TestCase("p{orphans:}")]
    public void MalformedDeclarationsFailFpdf1202(string css)
        => RejectWithoutOutput(Doc(css, P("T", 3)), "FPDF1202");

    private static string L(string label, int lines)
        => string.Join("<br>", Enumerable.Range(1, lines).Select(i => $"{label}{i}"));

    private static string CellP(string label, int lines) => $"<p style='margin:0'>{L(label, lines)}</p>";

    private static string[] Row(params string[] cells) => cells;

    // One row per array; each cell's HTML in a column of equal width.
    private static string Table(params string[][] rows)
    {
        var columns = rows[0].Length;
        var width = 160 / columns;
        return $"<table style='width:{width * columns}pt;font-size:10pt;line-height:20pt'><colgroup>"
            + string.Concat(Enumerable.Repeat($"<col style='width:{width}pt'>", columns)) + "</colgroup><tbody>"
            + string.Concat(rows.Select(row => "<tr>" + string.Concat(row.Select(cell => $"<td>{cell}</td>")) + "</tr>"))
            + "</tbody></table>";
    }

    private const string TableCss = "table{border-spacing:0}td{padding:0;vertical-align:top}";

    // Rows that split across pages continue each cell independently; every cell paragraph keeps
    // its orphans and widows as Chrome prints them. With orphans:1;widows:1 the rows split
    // exactly as before (Chrome agrees), so the other cases are what the initial value changes.
    public static IEnumerable<TestCaseData> ChromeTableSplits()
    {
        TestCaseData Case(string name, string css, string body, string chrome)
            => new TestCaseData(css, body, chrome).SetName(name);
        yield return Case("CellLinesAsBeforeWithOneAndOne", "td{orphans:1;widows:1}", P("F", 7) + Table(Row(L("C", 12))),
            "F1 F2 F3 F4 F5 F6 F7 C1 | C2 C3 C4 C5 C6 C7 C8 C9 | C10 C11 C12");
        yield return Case("RowWhoseCellCannotKeepItsOrphansMovesWhole", "", P("F", 7) + Table(Row(L("C", 12))),
            "F1 F2 F3 F4 F5 F6 F7 | C1 C2 C3 C4 C5 C6 C7 C8 | C9 C10 C11 C12");
        yield return Case("CellWidowsAtTheTopOfAPage", "", Table(Row(L("C", 9))), "C1 C2 C3 C4 C5 C6 C7 | C8 C9");
        yield return Case("OneViolatingCellMovesTheRow", "", P("F", 7) + Table(Row(L("A", 9), L("B", 13))),
            "F1 F2 F3 F4 F5 F6 F7 | A1 A2 A3 A4 A5 A6 A7 B1 B2 B3 B4 B5 B6 B7 B8 | A8 A9 B9 B10 B11 B12 B13");
        yield return Case("ViolatingCellMovesTheRowEvenWhenAnotherCellCouldBreak", "",
            P("F", 7) + Table(Row(L("A", 9), CellP("X", 1) + CellP("B", 12))),
            "F1 F2 F3 F4 F5 F6 F7 | A1 A2 A3 A4 A5 A6 A7 X1 B1 B2 B3 B4 B5 B6 B7 | A8 A9 B8 B9 B10 B11 B12");
        yield return Case("ContinuedCellWidows", "", P("F", 1) + Table(Row(L("A", 4)), Row(L("C", 12))),
            "F1 A1 A2 A3 A4 C1 C2 C3 | C4 C5 C6 C7 C8 C9 C10 | C11 C12");
        yield return Case("DeclaredCellOrphansAndWidows", "td{orphans:3;widows:3}", P("F", 1) + Table(Row(L("A", 4)), Row(L("C", 12))),
            "F1 A1 A2 A3 A4 C1 C2 C3 | C4 C5 C6 C7 C8 C9 | C10 C11 C12");
        yield return Case("DeclaredCellOrphansMoveTheRow", "td{orphans:3;widows:3}", P("F", 6) + Table(Row(L("C", 12))),
            "F1 F2 F3 F4 F5 F6 | C1 C2 C3 C4 C5 C6 C7 C8 | C9 C10 C11 C12");
        yield return Case("LaterCellParagraphMovesWithinItsCell", "", P("F", 5) + Table(Row(CellP("P", 2) + CellP("Q", 9), L("B", 12))),
            "F1 F2 F3 F4 F5 P1 P2 B1 B2 B3 | Q1 Q2 Q3 Q4 Q5 Q6 Q7 B4 B5 B6 B7 B8 B9 B10 | Q8 Q9 B11 B12");
        yield return Case("LaterParagraphOfASingleCell", "", P("F", 5) + Table(Row(CellP("P", 2) + CellP("Q", 9))),
            "F1 F2 F3 F4 F5 P1 P2 | Q1 Q2 Q3 Q4 Q5 Q6 Q7 | Q8 Q9");
        yield return Case("CellWidowsPullBackOneCellOnly", "", P("F", 4) + Table(Row(L("A", 5), L("B", 12))),
            "F1 F2 F3 F4 A1 A2 A3 B1 B2 B3 B4 | A4 A5 B5 B6 B7 B8 B9 B10 B11 B12");
        yield return Case("EachCellKeepsItsOwnWidows", "", Table(Row(L("A", 9), L("B", 17))),
            "A1 A2 A3 A4 A5 A6 A7 B1 B2 B3 B4 B5 B6 B7 B8 | A8 A9 B9 B10 B11 B12 B13 B14 B15 | B16 B17");
        yield return Case("ShortCellsKeepTheirLines", "", P("F", 6) + Table(Row(L("A", 12), "B1")),
            "F1 F2 F3 F4 F5 F6 A1 A2 B1 | A3 A4 A5 A6 A7 A8 A9 A10 | A11 A12");
    }

    [TestCaseSource(nameof(ChromeTableSplits))]
    public void TableCellsBreakWhereChromeBreaks(string css, string body, string chrome)
        => Assert.That(Split(Doc(TableCss + css, body)), Is.EqualTo(chrome));

    [TestCase("td{orphans:3;widows:3}")]
    [TestCase("*{orphans:3;widows:3}")]
    public void TableRowsThatDoNotSplitRenderAsWithoutTheDeclaration(string css)
        => Assert.That(Pdf(Doc(TableCss + css, Table(Row(L("C", 3)), Row("D1<br>D2")))),
            Is.EqualTo(Pdf(Doc(TableCss, Table(Row(L("C", 3)), Row("D1<br>D2"))))));

    [Test]
    public void FragmentEndUsesEachLineHeight()
    {
        double[] heights = [10, 30, 10, 10, 10];
        Assert.That(LineBreakControl.Fitting(heights, 0, 0, 50), Is.EqualTo(3));
        Assert.That(LineBreakControl.FragmentEnd(heights, 0, 0, 50, (2, 2)), Is.EqualTo(3));
        Assert.That(LineBreakControl.FragmentEnd(heights, 0, 0, 50, (2, 3)), Is.EqualTo(2));
        Assert.That(LineBreakControl.FragmentEnd(heights, 0, 0, 50, (4, 1)), Is.EqualTo(3), "orphans cannot hold: break where the page ends");
        Assert.That(LineBreakControl.FragmentEnd(heights, 2, 0, 50, (2, 2)), Is.EqualTo(int.MaxValue), "the rest fits");
        Assert.That(LineBreakControl.FragmentEnd(heights, 1, 20, 50, (1, 1)), Is.EqualTo(2));
    }
}
