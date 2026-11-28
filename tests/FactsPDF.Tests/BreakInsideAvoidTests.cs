using NUnit.Framework;

namespace FactsPDF.Tests;

// break-inside:avoid on block boxes. Every expected split below is what Chrome printed for the same
// synthetic page (300pt by 200pt, 20pt margins, 20pt lines, so eight lines a page): the text on each
// printed page, pages separated by '|'. A box that does not fit the rest of the page moves whole to
// the next page, even when it is taller than a page (it then breaks normally there); a box that
// already starts a page, or whose first break is a forced one, stays where it is.
[TestFixture]
public sealed class BreakInsideAvoidTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 200, Margin = 20 };

    private const string Base = "html,body{margin:0;padding:0}body{font-size:10pt;line-height:20pt}"
        + "p,ul,ol,li,table,div,h2,h3,section{margin:0;padding:0}"
        + "table{border-collapse:separate;border-spacing:0}td,th{padding:0}";

    private static string Doc(string body, string css = "")
        => $"<!doctype html><html><head><style>{Base}{css}</style></head><body>{body}</body></html>";

    private static List<LayoutPage> Layout(string html, PdfOptions? options = null)
        => TableLayoutTests.Layout(html, options ?? Page);

    private static string Split(string html) => string.Join(" | ", Layout(html)
        .Select(page => string.Join(' ', page.Runs.Select(run => run.Text))));

    private static byte[] Render(string html, PdfOptions? options = null)
        => TableLayoutTests.Pdf(html, options ?? Page);

    private static string Lines(int count, string label)
        => string.Concat(Enumerable.Range(1, count).Select(i => $"<p>{label}{i}</p>"));

    private static void RejectAndRecover(string html, string code, int? offset = null)
    {
        using var stream = new MemoryStream();
        stream.Write("ORIGINAL-CUSTOMER-PDF"u8);
        stream.Position = 3;
        var failure = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, stream, Page));
        Assert.That(failure!.Code, Is.EqualTo(code));
        if (offset.HasValue) Assert.That(failure.SourceOffset, Is.EqualTo(offset.Value));
        Assert.That(stream.ToArray(), Is.EqualTo("ORIGINAL-CUSTOMER-PDF"u8.ToArray()));
        Assert.That(stream.Position, Is.EqualTo(3));
        Assert.That(Render(Doc("<p>RECOVERED</p>")).Length, Is.GreaterThan(100));
    }

    public static IEnumerable<TestCaseData> ChromeSplits()
    {
        static TestCaseData Case(string name, string body, string chrome)
            => new TestCaseData(body, chrome).SetName(name);
        yield return Case("MovesWholeToTheNextPage",
            "<p>F1</p><p>F2</p><p>F3</p><p>F4</p><p>F5</p><p>F6</p><div style='break-inside:avoid'><p>A1</p><p>A2</p><p>A3</p></div><p>Z1</p>",
            "F1 F2 F3 F4 F5 F6 | A1 A2 A3 Z1");
        yield return Case("StaysWhenItFits",
            "<p>F1</p><p>F2</p><p>F3</p><p>F4</p><div style='break-inside:avoid'><p>A1</p><p>A2</p><p>A3</p></div><p>Z1</p>",
            "F1 F2 F3 F4 A1 A2 A3 Z1");
        yield return Case("StaysWhenItFitsExactly",
            "<p>F1</p><p>F2</p><p>F3</p><p>F4</p><p>F5</p><div style='break-inside:avoid'><p>A1</p><p>A2</p><p>A3</p></div><p>Z1</p>",
            "F1 F2 F3 F4 F5 A1 A2 A3 | Z1");
        yield return Case("TallerThanAPageMovesThenBreaksNormally",
            "<p>F1</p><p>F2</p><p>F3</p><div style='break-inside:avoid'><p>A1</p><p>A2</p><p>A3</p><p>A4</p><p>A5</p><p>A6</p><p>A7</p><p>A8</p><p>A9</p><p>A10</p></div><p>Z1</p>",
            "F1 F2 F3 | A1 A2 A3 A4 A5 A6 A7 A8 | A9 A10 Z1");
        yield return Case("ExactlyAPageTallMovesToAFreshPage",
            "<p>F1</p><p>F2</p><p>F3</p><div style='break-inside:avoid'><p>A1</p><p>A2</p><p>A3</p><p>A4</p><p>A5</p><p>A6</p><p>A7</p><p>A8</p></div><p>Z1</p>",
            "F1 F2 F3 | A1 A2 A3 A4 A5 A6 A7 A8 | Z1");
        yield return Case("FirstOnThePageNeverMoves",
            "<div style='break-inside:avoid'><p>A1</p><p>A2</p><p>A3</p><p>A4</p><p>A5</p><p>A6</p><p>A7</p><p>A8</p><p>A9</p><p>A10</p></div><p>Z1</p>",
            "A1 A2 A3 A4 A5 A6 A7 A8 | A9 A10 Z1");
        yield return Case("ForcedBreakInsideIsHonouredWhereItFalls",
            "<p>F1</p><p>F2</p><div style='break-inside:avoid'><p>A1</p><p style='break-after:page'>A2</p><p>B1</p></div><p>Z1</p>",
            "F1 F2 A1 A2 | B1 Z1");
        yield return Case("ForcedBreakBeforeALateChildKeepsTheFirstPart",
            "<p>F1</p><p>F2</p><p>F3</p><p>F4</p><p>F5</p><div style='break-inside:avoid'><p>A1</p><p style='break-before:page'>A2</p><p>B1</p></div><p>Z1</p>",
            "F1 F2 F3 F4 F5 A1 | A2 B1 Z1");
        yield return Case("ForcedBreakAfterAFirstPartThatDoesNotFitMovesTheBox",
            "<p>F1</p><p>F2</p><p>F3</p><p>F4</p><p>F5</p><p>F6</p><div style='break-inside:avoid'><p>A1</p><p>A2</p><p style='break-after:page'>A3</p><p>B1</p></div><p>Z1</p>",
            "F1 F2 F3 F4 F5 F6 | A1 A2 A3 | B1 Z1");
        yield return Case("UnforcedBreakAfterAForcedBreakDoesNotMoveTheBox",
            "<p>F1</p><p>F2</p><div style='break-inside:avoid'><p style='break-after:page'>A1</p><p>B1</p><p>B2</p><p>B3</p><p>B4</p><p>B5</p><p>B6</p><p>B7</p><p>B8</p><p>B9</p><p>B10</p></div><p>Z1</p>",
            "F1 F2 A1 | B1 B2 B3 B4 B5 B6 B7 B8 | B9 B10 Z1");
        yield return Case("BreakBeforeOnTheBoxItselfStartsAPage",
            "<p>F1</p><p>F2</p><div style='break-inside:avoid;break-before:page'><p>A1</p><p>A2</p></div><p>Z1</p>",
            "F1 F2 | A1 A2 Z1");
        yield return Case("BreakAfterOnTheBoxItselfEndsThePage",
            "<p>F1</p><p>F2</p><div style='break-inside:avoid;break-after:page'><p>A1</p><p>A2</p></div><p>Z1</p>",
            "F1 F2 A1 A2 | Z1");
        yield return Case("NestedBoxesMoveTogether",
            "<p>F1</p><p>F2</p><p>F3</p><p>F4</p><p>F5</p><div style='break-inside:avoid'><p>A1</p><div style='break-inside:avoid'><p>B1</p><p>B2</p><p>B3</p></div></div><p>Z1</p>",
            "F1 F2 F3 F4 F5 | A1 B1 B2 B3 Z1");
        yield return Case("InnerBoxMovesAloneInAPlainParent",
            "<p>F1</p><p>F2</p><p>F3</p><p>F4</p><p>F5</p><div><p>A1</p><div style='break-inside:avoid'><p>B1</p><p>B2</p><p>B3</p></div></div><p>Z1</p>",
            "F1 F2 F3 F4 F5 A1 | B1 B2 B3 Z1");
        yield return Case("OuterTallerThanThePageKeepsTheInnerBoxWhole",
            "<p>F1</p><p>F2</p><div style='break-inside:avoid'><p>A1</p><p>A2</p><p>A3</p><p>A4</p><p>A5</p><div style='break-inside:avoid'><p>B1</p><p>B2</p><p>B3</p></div><p>C1</p><p>C2</p><p>C3</p></div><p>Z1</p>",
            "F1 F2 | A1 A2 A3 A4 A5 B1 B2 B3 | C1 C2 C3 Z1");
        yield return Case("InnerBoxMovesWithinABoxThatBreaksAnyway",
            "<p>F1</p><p>F2</p><div style='break-inside:avoid'><p>A1</p><p>A2</p><p>A3</p><p>A4</p><p>A5</p><p>A6</p><div style='break-inside:avoid'><p>B1</p><p>B2</p><p>B3</p></div><p>C1</p><p>C2</p><p>C3</p></div><p>Z1</p>",
            "F1 F2 | A1 A2 A3 A4 A5 A6 | B1 B2 B3 C1 C2 C3 Z1");
        yield return Case("BottomPaddingThatDoesNotFitMovesTheBox",
            "<p>F1</p><p>F2</p><p>F3</p><p>F4</p><p>F5</p><p>F6</p><div style='break-inside:avoid;padding-bottom:30pt'><p>A1</p></div><p>Z1</p>",
            "F1 F2 F3 F4 F5 F6 | A1 Z1");
        yield return Case("MinimumHeightThatDoesNotFitMovesTheBox",
            "<p>F1</p><p>F2</p><p>F3</p><p>F4</p><p>F5</p><div style='break-inside:avoid;min-height:80pt'><p>A1</p></div><p>Z1</p>",
            "F1 F2 F3 F4 F5 | A1 Z1");
        yield return Case("EmptyBoxAddsNoPage",
            "<p>F1</p><p>F2</p><p>F3</p><p>F4</p><p>F5</p><p>F6</p><p>F7</p><p>F8</p><div style='break-inside:avoid'></div><p>Z1</p>",
            "F1 F2 F3 F4 F5 F6 F7 F8 | Z1");
        yield return Case("SiblingBoxesAreKeptSeparately",
            "<p>F1</p><p>F2</p><p>F3</p><p>F4</p><p>F5</p><div style='break-inside:avoid'><p>A1</p><p>A2</p></div><div style='break-inside:avoid'><p>B1</p><p>B2</p></div><p>Z1</p>",
            "F1 F2 F3 F4 F5 A1 A2 | B1 B2 Z1");
        yield return Case("SecondBoxStaysWhenBothFit",
            "<p>F1</p><p>F2</p><p>F3</p><div style='break-inside:avoid'><p>A1</p><p>A2</p></div><div style='break-inside:avoid'><p>B1</p><p>B2</p></div><p>Z1</p>",
            "F1 F2 F3 A1 A2 B1 B2 Z1");
        yield return Case("ContentAfterAMovedBoxFollowsIt",
            "<p>F1</p><p>F2</p><p>F3</p><p>F4</p><p>F5</p><p>F6</p><div style='break-inside:avoid'><p>A1</p><p>A2</p><p>A3</p></div><p>Z1</p><p>Z2</p>",
            "F1 F2 F3 F4 F5 F6 | A1 A2 A3 Z1 Z2");
        yield return Case("ParagraphIsKeptWhole",
            "<p>F1</p><p>F2</p><p>F3</p><p>F4</p><p>F5</p><p>F6</p><p style='break-inside:avoid'>w1<br>w2<br>w3</p><p>Z1</p>",
            "F1 F2 F3 F4 F5 F6 | w1 w2 w3 Z1");
        yield return Case("ParagraphThatCannotLeaveItsOrphansIsKeptWhole",
            "<p>F1</p><p>F2</p><p>F3</p><p>F4</p><p>F5</p><p>F6</p><p style='break-inside:avoid'>w1<br>w2</p><p>Z1</p>",
            "F1 F2 F3 F4 F5 F6 w1 w2 | Z1");
        yield return Case("ParagraphTallerThanAPageMovesThenBreaks",
            "<p>F1</p><p>F2</p><p style='break-inside:avoid'>w1<br>w2<br>w3<br>w4<br>w5<br>w6<br>w7<br>w8<br>w9<br>w10</p><p>Z1</p>",
            "F1 F2 | w1 w2 w3 w4 w5 w6 w7 w8 | w9 w10 Z1");
        yield return Case("ListIsKeptWhole",
            "<p>F1</p><p>F2</p><p>F3</p><p>F4</p><p>F5</p><p>F6</p><ul style='break-inside:avoid'><li>L1</li><li>L2</li><li>L3</li></ul><p>Z1</p>",
            "F1 F2 F3 F4 F5 F6 | L1 L2 L3 Z1");
        yield return Case("OrderedListWithMarginsIsKeptWhole",
            "<p>F1</p><p>F2</p><p>F3</p><p>F4</p><p>F5</p><p>F6</p><ol style='break-inside:avoid;margin:10pt 0 10pt 20pt'><li>L1</li><li>L2</li></ol><p>Z1</p>",
            "F1 F2 F3 F4 F5 F6 | 1. L1 2. L2 Z1");
        yield return Case("ListItemIsKeptWhole",
            "<p>F1</p><p>F2</p><p>F3</p><p>F4</p><p>F5</p><ul><li>L1</li><li style='break-inside:avoid'>M1<br>M2<br>M3</li><li>L3</li></ul><p>Z1</p>",
            "F1 F2 F3 F4 F5 L1 | M1 M2 M3 L3 Z1");
        yield return Case("ListItemWithParagraphsIsKeptWhole",
            "<p>F1</p><p>F2</p><p>F3</p><p>F4</p><p>F5</p><p>F6</p><ul><li>L1</li><li style='break-inside:avoid'><p>M1</p><p>M2</p><p>M3</p></li></ul><p>Z1</p>",
            "F1 F2 F3 F4 F5 F6 L1 | M1 M2 M3 Z1");
        yield return Case("FlexItemsListIsKeptWhole",
            "<p>F1</p><p>F2</p><p>F3</p><p>F4</p><p>F5</p><p>F6</p><ul style='break-inside:avoid'><li style='display:flex'><span>L1</span><span>V1</span></li><li style='display:flex'><span>L2</span><span>V2</span></li></ul><p>Z1</p>",
            "F1 F2 F3 F4 F5 F6 L1 V1 L2 V2 | Z1");
        yield return Case("TableIsKeptWhole",
            "<p>F1</p><p>F2</p><p>F3</p><p>F4</p><p>F5</p><p>F6</p><table style='break-inside:avoid'><tr><td>T1</td></tr><tr><td>T2</td></tr><tr><td>T3</td></tr></table><p>Z1</p>",
            "F1 F2 F3 F4 F5 F6 | T1 T2 T3 Z1");
        yield return Case("TableThatFitsStays",
            "<p>F1</p><p>F2</p><table style='break-inside:avoid'><tr><td>T1</td></tr><tr><td>T2</td></tr><tr><td>T3</td></tr></table><p>Z1</p>",
            "F1 F2 T1 T2 T3 Z1");
        yield return Case("TableTallerThanAPageMovesThenRepeatsItsHeader",
            "<p>F1</p><p>F2</p><table style='break-inside:avoid'><thead><tr><th style='font-weight:normal'>H1</th></tr></thead><tr><td>T1</td></tr><tr><td>T2</td></tr><tr><td>T3</td></tr><tr><td>T4</td></tr><tr><td>T5</td></tr><tr><td>T6</td></tr><tr><td>T7</td></tr><tr><td>T8</td></tr><tr><td>T9</td></tr><tr><td>T10</td></tr></table><p>Z1</p>",
            "F1 F2 | H1 T1 T2 T3 T4 T5 T6 T7 | H1 T8 T9 T10 Z1");
        yield return Case("TableWithACaptionIsKeptWhole",
            "<p>F1</p><p>F2</p><p>F3</p><p>F4</p><p>F5</p><p>F6</p><table style='break-inside:avoid;width:100pt'><caption>C1</caption><tr><td>T1</td></tr><tr><td>T2</td></tr></table><p>Z1</p>",
            "F1 F2 F3 F4 F5 F6 | C1 T1 T2 Z1");
        yield return Case("HeadingAndTableAreKeptTogether",
            "<p>F1</p><p>F2</p><p>F3</p><p>F4</p><p>F5</p><p>F6</p><div style='break-inside:avoid'><h3 style='font-weight:normal'>H1</h3><table><tr><td>T1</td></tr><tr><td>T2</td></tr></table></div><p>Z1</p>",
            "F1 F2 F3 F4 F5 F6 | H1 T1 T2 Z1");
        yield return Case("HeadingAndTableThatFitStay",
            "<p>F1</p><p>F2</p><p>F3</p><div style='break-inside:avoid'><h3 style='font-weight:normal'>H1</h3><table><tr><td>T1</td></tr><tr><td>T2</td></tr></table></div><p>Z1</p>",
            "F1 F2 F3 H1 T1 T2 Z1");
        yield return Case("ParagraphAndListAreKeptTogether",
            "<p>F1</p><p>F2</p><p>F3</p><p>F4</p><p>F5</p><p>F6</p><div style='break-inside:avoid'><p>A1</p><ul><li>L1</li><li>L2</li></ul></div><p>Z1</p>",
            "F1 F2 F3 F4 F5 F6 | A1 L1 L2 Z1");
        yield return Case("HeadingParagraphsListAndParagraphsAreKeptTogether",
            "<p>F1</p><p>F2</p><p>F3</p><p>F4</p><div style='break-inside:avoid'><h3 style='font-weight:normal'>H1</h3><p>A1</p><p>A2</p><ul><li>L1</li><li>L2</li></ul><p>B1</p><p>B2</p></div><p>Z1</p>",
            "F1 F2 F3 F4 | H1 A1 A2 L1 L2 B1 B2 Z1");
        yield return Case("InlineRunsAreKeptTogether",
            "<p>F1</p><p>F2</p><p>F3</p><p>F4</p><p>F5</p><p>F6</p><div style='break-inside:avoid'><p><span>A1</span> x</p><p>A2 <span>y</span></p><p>A3</p></div><p>Z1</p>",
            "F1 F2 F3 F4 F5 F6 | A1 x A2 y A3 Z1");
        yield return Case("FlexRowInsideMovesWithTheBox",
            "<p>F1</p><p>F2</p><p>F3</p><p>F4</p><p>F5</p><p>F6</p><div style='break-inside:avoid'><p>A1</p><div style='display:flex'><p>X1</p><p>X2</p></div></div><p>Z1</p>",
            "F1 F2 F3 F4 F5 F6 A1 X1 X2 | Z1");
        yield return Case("FlexRowThatFitsStays",
            "<p>F1</p><p>F2</p><p>F3</p><p>F4</p><p>F5</p><div style='break-inside:avoid'><p>A1</p><div style='display:flex'><p>X1</p><p>X2</p></div><p>A2</p></div><p>Z1</p>",
            "F1 F2 F3 F4 F5 A1 X1 X2 A2 | Z1");
        yield return Case("BoxInsideAPaddedParentMovesAndTheParentContinues",
            "<p>F1</p><p>F2</p><p>F3</p><p>F4</p><div style='padding:10pt;border:0.75pt solid #000'><p>A1</p><div style='break-inside:avoid'><p>B1</p><p>B2</p><p>B3</p></div></div><p>Z1</p>",
            "F1 F2 F3 F4 A1 | B1 B2 B3 Z1");
        yield return Case("BoxInsideAListItemMoves",
            "<p>F1</p><p>F2</p><p>F3</p><p>F4</p><p>F5</p><ul><li><p>A1</p><div style='break-inside:avoid'><p>B1</p><p>B2</p><p>B3</p></div></li></ul><p>Z1</p>",
            "F1 F2 F3 F4 F5 A1 | B1 B2 B3 Z1");
        yield return Case("ChildMarginDoesNotCountWhenTheBoxMoves",
            "<p>F1</p><p>F2</p><p>F3</p><p>F4</p><p>F5</p><p>F6</p><div style='break-inside:avoid'><p style='margin-top:10pt'>A1</p><p>A2</p></div><p>Z1</p>",
            "F1 F2 F3 F4 F5 F6 | A1 A2 Z1");
        yield return Case("ChildMarginCountsWhenTheBoxStays",
            "<p>F1</p><p>F2</p><p>F3</p><p>F4</p><div style='break-inside:avoid'><p style='margin-top:10pt'>A1</p><p>A2</p></div><p>Z1</p>",
            "F1 F2 F3 F4 A1 A2 Z1");
        yield return Case("ImageFreeBoxWithBorderAndPaddingMoves",
            "<p>F1</p><p>F2</p><p>F3</p><p>F4</p><p>F5</p><div style='break-inside:avoid;padding:10pt;border:1.5pt solid #000;background:#eee'><p>A1</p><p>A2</p></div><p>Z1</p>",
            "F1 F2 F3 F4 F5 | A1 A2 Z1");
        yield return Case("FlexRowThatDoesNotFitMovesTheBoxInsteadOfFailing",
            "<p>F1</p><p>F2</p><p>F3</p><p>F4</p><p>F5</p><p>F6</p><p>F7</p><div style='break-inside:avoid'><p>A1</p><div style='display:flex'><p>X1</p><p>X2</p></div></div><p>Z1</p>",
            "F1 F2 F3 F4 F5 F6 F7 | A1 X1 X2 Z1");
        yield return Case("GridRowThatDoesNotFitMovesTheBoxInsteadOfFailing",
            "<p>F1</p><p>F2</p><p>F3</p><p>F4</p><p>F5</p><p>F6</p><p>F7</p><div style='break-inside:avoid'><p>A1</p><div style='display:grid;grid-template-columns:100pt 100pt'><p>X1</p><p>X2</p></div></div><p>Z1</p>",
            "F1 F2 F3 F4 F5 F6 F7 | A1 X1 X2 Z1");
        yield return Case("FlexRowAloneAtThePageEndMovesTheBox",
            "<p>F1</p><p>F2</p><p>F3</p><p>F4</p><p>F5</p><p>F6</p><p>F7</p><div style='break-inside:avoid'><div style='display:flex'><p>X1</p><p>X2</p></div><p>A1</p></div><p>Z1</p>",
            "F1 F2 F3 F4 F5 F6 F7 | X1 X2 A1 Z1");
        yield return Case("ImageInTheBoxMovesWithIt",
            "<p>F1</p><p>F2</p><p>F3</p><p>F4</p><p>F5</p><p>F6</p><div style='break-inside:avoid'><p>A1</p><svg width='100' height='40' viewBox='0 0 100 40'><rect width='100' height='40' fill='#369'/></svg></div><p>Z1</p>",
            "F1 F2 F3 F4 F5 F6 | A1 Z1");
        yield return Case("TableKeptWithItsMargins",
            "<p>F1</p><p>F2</p><p>F3</p><p>F4</p><p>F5</p><p>F6</p><table style='break-inside:avoid;margin-top:12pt;margin-bottom:12pt'><tr><td>T1</td></tr><tr><td>T2</td></tr></table><p>Z1</p>",
            "F1 F2 F3 F4 F5 F6 | T1 T2 Z1");
        yield return Case("DescriptionListIsKeptWhole",
            "<p>F1</p><p>F2</p><p>F3</p><p>F4</p><p>F5</p><p>F6</p><dl style='break-inside:avoid;margin:0'><dt>D1</dt><dd style='margin:0'>E1</dd><dt>D2</dt><dd style='margin:0'>E2</dd></dl><p>Z1</p>",
            "F1 F2 F3 F4 F5 F6 | D1 E1 D2 E2 Z1");
        yield return Case("BlockquoteIsKeptWhole",
            "<p>F1</p><p>F2</p><p>F3</p><p>F4</p><p>F5</p><p>F6</p><blockquote style='break-inside:avoid;margin:0'><p>A1</p><p>A2</p><p>A3</p></blockquote><p>Z1</p>",
            "F1 F2 F3 F4 F5 F6 | A1 A2 A3 Z1");
    }

    [TestCaseSource(nameof(ChromeSplits))]
    public void BoxesBreakWhereChromeBreaksThem(string body, string chrome)
        => Assert.That(Split(Doc(body)), Is.EqualTo(chrome));

    [Test]
    public void MovedBoxPagesMatchAManualPageBreakAndFittingBoxesMatchNoDeclaration()
    {
        var box = "<div style='break-inside:avoid;background:#eaf2fa;border:0.75pt solid #223344;padding:6pt'>"
            + Lines(2, "A") + "</div>";
        var moved = Lines(6, "F") + box + "<p>Z</p>";
        Assert.That(Render(Doc(moved)), Is.EqualTo(Render(Doc(moved.Replace("break-inside:avoid", "break-before:page")))));
        var fits = Lines(2, "F") + box + "<p>Z</p>";
        Assert.That(Render(Doc(fits)), Is.EqualTo(Render(Doc(fits.Replace("break-inside:avoid", "break-inside:auto")))));
        Assert.That(Layout(Doc(fits)), Has.Count.EqualTo(1));
    }

    [Test]
    public void BoxThatStartsAPageNeverLeavesAnEmptyPageBehind()
    {
        var pages = Layout(Doc("<div style='break-inside:avoid'>" + Lines(10, "A") + "</div>"));
        Assert.That(pages, Has.Count.EqualTo(2));
        Assert.That(pages[0].Runs, Has.Count.EqualTo(8));
    }

    [Test]
    public void MarginsAtTheUnforcedBreakAreTruncatedAndTheClosingMarginIsKept()
    {
        const string box = "<div style='break-inside:avoid;margin-top:15pt;margin-bottom:12pt'>";
        var pages = Layout(Doc(Lines(6, "F") + box + Lines(3, "A") + "</div><p>Z</p>"));
        var top = Layout(Doc(Lines(1, "A")))[0].Runs.Single().Baseline;
        Assert.That(pages[1].Runs.First(r => r.Text == "A1").Baseline, Is.EqualTo(top).Within(0.000001));
        var last = pages[1].Runs.First(r => r.Text == "A3").Baseline;
        Assert.That(pages[1].Runs.First(r => r.Text == "Z").Baseline, Is.EqualTo(last - 32).Within(0.000001));
        // Where the box stays, the margin above it is kept.
        var stays = Layout(Doc(Lines(4, "F") + box + Lines(3, "A") + "</div>"))[0].Runs;
        Assert.That(stays.First(r => r.Text == "F4").Baseline - stays.First(r => r.Text == "A1").Baseline,
            Is.EqualTo(35).Within(0.000001));
    }

    [Test]
    public void MovedBoxChargesItsDisplayCommandsOnce()
    {
        var html = Doc(Lines(6, "F") + "<div style='break-inside:avoid'>" + Lines(3, "A") + "</div>");
        var exact = Page with { MaxDisplayCommands = 9 };
        Assert.That(Layout(html, exact), Has.Count.EqualTo(2));
        var ex = Assert.Throws<FactsPdfException>(() => Layout(html, Page with { MaxDisplayCommands = 8 }));
        Assert.That(ex!.Code, Is.EqualTo("FPDF1401"));
    }

    [Test]
    public void LegacyAliasMatchesModernAvoidAndCascadeRespectsImportantAndWideReset()
    {
        var body = Lines(6, "F") + "<section style='break-inside:avoid'>" + Lines(3, "A") + "</section>";
        Assert.That(Render(Doc(body)), Is.EqualTo(Render(Doc(body.Replace("break-inside", "page-break-inside")))));
        const string html = "<style>section{break-inside:avoid!important}" +
            ".card{page-break-inside:auto}</style>" +
            "<section class='card' style='break-inside:initial'>" +
            "<p>ONE</p></section>";
        var node = HtmlDocumentReader.ReadTree(html, Page, default)
            .Children.OfType<BlockNode>().Single(n => n.Name == "body").Children.OfType<BlockNode>().Single(n => n.Name == "section");
        Assert.That(node.BoxStyle.AvoidBreakInside, Is.True);
        var reset = html.Replace("break-inside:avoid!important", "break-inside:avoid");
        var resetNode = HtmlDocumentReader.ReadTree(reset, Page, default)
            .Children.OfType<BlockNode>().Single(n => n.Name == "body").Children.OfType<BlockNode>().Single(n => n.Name == "section");
        Assert.That(resetNode.BoxStyle.AvoidBreakInside, Is.False);
    }

    [Test]
    public void CompleteHtmlBodyAndNestedBoxesAreAccepted()
    {
        var html = "<!doctype html><html><head><style>" + Base + "</style></head><body>"
            + Lines(6, "F") + "<div><section style='break-inside:avoid'>" + Lines(3, "A") + "</section></div>"
            + "</body></html>";
        Assert.That(Split(html), Is.EqualTo("F1 F2 F3 F4 F5 F6 | A1 A2 A3"));
    }

    [TestCase("<p>F</p><span style='break-inside:avoid'>NO</span>")]
    [TestCase("<table><tr><td><p style='break-inside:avoid'>NO</p></td></tr></table>")]
    [TestCase("<div style='display:flex;break-inside:avoid'><div>NO</div></div>")]
    [TestCase("<div style='display:grid;break-inside:avoid'><div>NO</div></div>")]
    [TestCase("<table><tbody style='break-inside:avoid'><tr><td>NO</td></tr></tbody></table>")]
    public void HostsWhoseBreaksCannotBeKeptWholeFail(string body)
        => RejectAndRecover(Doc(body), "FPDF1201");

    [Test]
    public void InvalidValuesFail() => RejectAndRecover(
        Doc("<section style='break-inside:always'><p>NO</p></section>"), "FPDF1202");

    [Test]
    public void KeptBoxWhoseFixedHeightCannotFitAPageStillFails()
        => RejectAndRecover(Doc(Lines(2, "F") + "<div style='break-inside:avoid;height:400pt'><p>NO</p></div>"), "FPDF1302");
}
