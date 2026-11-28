using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

// Line breaking, white-space and overflow as Chrome prints them. Each expectation was read back from Chrome
// 158 with the same advances (the built-in Courier here is 7.2pt per character at 12pt).
[TestFixture]
public sealed class OverflowAndLineBreakingTests
{
    private const string Reset = "<style>html,body{margin:0}p,div,pre{margin:0;padding:0}</style>";
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 400, Margin = 20 };

    private static List<LayoutPage> Layout(string html, PdfOptions? options = null)
    {
        var o = options ?? Page;
        return TextLayout.Layout(HtmlDocumentReader.ReadTree(Reset + html, o, default), o, default);
    }

    // The text of each line, top to bottom, with its left edge: spaces count where they are painted.
    private static List<(string Text, double X)> Lines(string html, PdfOptions? options = null) =>
        [.. Layout(html, options).SelectMany(page => page.Runs).GroupBy(run => Math.Round(run.Baseline, 1))
            .OrderByDescending(group => group.Key)
            .Select(group => (string.Concat(group.OrderBy(run => run.X).Select(run => run.Text)), group.Min(run => run.X)))];

    private static string[] Text(string html, PdfOptions? options = null) => [.. Lines(html, options).Select(line => line.Text.TrimEnd())];

    private static byte[] Pdf(string html, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(Reset + html, output, options ?? Page);
        return output.ToArray();
    }

    private static string Content(string html, PdfOptions? options = null) => Encoding.Latin1.GetString(Pdf(html, options));

    [TestCase("<div style='width:60pt'>Reference: SR-2026-0298 ok</div>", "Reference:", "SR-2026-", "0298 ok")]
    [TestCase("<div style='width:36pt'>aa -5 bb</div>", "aa -5", "bb")]
    [TestCase("<div style='width:72pt'>a/b/c/d/e/f/g/h/i</div>", "a/b/c/d/e/f/g/h/i")]
    [TestCase("<div style='width:60pt'>see a?b=c</div>", "see a?", "b=c")]
    public void LinesBreakWhereChromeBreaksThem(string html, params string[] expected) =>
        Assert.That(Text(html), Is.EqualTo(expected));

    [Test]
    public void ClosingPunctuationNeverStartsALineAndOpeningPunctuationNeverEndsOne()
    {
        // Eight full-width characters per 80pt line in a 10pt font; the font has no CJK, so check the break rules directly.
        var analyzer = new LineBreakAnalyzer();
        var text = "あい。う「え".EnumerateRunes().Select(rune => rune.Value).ToArray();
        var breaks = analyzer.Analyze(text, LineBreakStrictness.Normal, WordBreakMode.Normal).ToArray();
        Assert.That(breaks.Select(b => b != 0), Is.EqualTo(new[] { false, true, false, true, true, false }),
            "あい|。 stays, 。う breaks, う|「 breaks, 「え stays");
    }

    [Test]
    public void SmallKanaStartsALineUnlessStrict()
    {
        var text = "あぁ".EnumerateRunes().Select(rune => rune.Value).ToArray();
        Assert.That(new LineBreakAnalyzer().Analyze(text, LineBreakStrictness.Normal, WordBreakMode.Normal)[1], Is.Not.Zero);
        Assert.That(new LineBreakAnalyzer().Analyze(text, LineBreakStrictness.Strict, WordBreakMode.Normal)[1], Is.Zero);
    }

    [Test]
    public void KeepAllHoldsLettersTogetherAndBreakAllSplitsThem()
    {
        var text = "한국어 a-b".EnumerateRunes().Select(rune => rune.Value).ToArray();
        var keep = new LineBreakAnalyzer().Analyze(text, LineBreakStrictness.Normal, WordBreakMode.KeepAll).ToArray();
        Assert.That(keep[1], Is.Zero.And.EqualTo(keep[2]), "no break inside the Hangul word");
        Assert.That(new LineBreakAnalyzer().Analyze(text, LineBreakStrictness.Normal, WordBreakMode.Normal)[1], Is.Not.Zero, "normal breaks between syllables");
        Assert.That(Text("<div style='width:36pt;word-break:break-all'>abcdefghij</div>"), Is.EqualTo(new[] { "abcde", "fghij" }));
        Assert.That(Text("<div style='width:36pt;word-break:break-all'>(xy) ab</div>"), Is.EqualTo(new[] { "(xy)", "ab" }), "break-all still honours the opening bracket");
    }

    [Test]
    public void OverflowWrapBreaksAWordOnlyWhenNoEarlierOpportunityFits()
    {
        Assert.That(Text("<div style='width:60pt;overflow-wrap:break-word'>aa bbbbbbbbbbbbbbbbbb cc</div>"),
            Is.EqualTo(new[] { "aa", "bbbbbbbb", "bbbbbbbb", "bb cc" }), "the long word moves to a fresh line first, then breaks");
        Assert.That(Text("<div style='width:60pt'>aa bbbbbbbbbbbbbbbbbb cc</div>"), Is.EqualTo(new[] { "aa", "bbbbbbbbbbbbbbbbbb", "cc" }),
            "without overflow-wrap the word overflows its box instead of failing");
    }

    [Test]
    public void ABoxWithNoWidthPutsEveryWordOnItsOwnLine()
    {
        Assert.That(Text("<div style='width:0'>ab cd-ef</div>"), Is.EqualTo(new[] { "ab", "cd-", "ef" }));
    }

    [Test]
    public void NowrapTextOverflowsItsBoxWithoutFailing()
    {
        Assert.That(Text("<div style='width:36pt;white-space:nowrap'>one two three</div>"), Is.EqualTo(new[] { "one two three" }));
    }

    [Test]
    public void PreWrapLetsTrailingSpacesHangButBreakSpacesWrapsThem()
    {
        Assert.That(Text("<div style='width:36pt;white-space:pre-wrap'>aaaaa       bbb</div>"), Is.EqualTo(new[] { "aaaaa", "bbb" }),
            "the spaces hang past the box and the next word starts the second line");
        Assert.That(Text("<div style='width:36pt;white-space:break-spaces'>aaaaa   bbb</div>"), Is.EqualTo(new[] { "aaaaa", "  bbb" }),
            "break-spaces keeps the spaces in the line, so they wrap too");
    }

    [Test]
    public void PreLineCollapsesSpacesButKeepsNewlines()
    {
        Assert.That(Text("<div style='white-space:pre-line'>a   b\n  c</div>"), Is.EqualTo(new[] { "a b", "c" }));
    }

    [TestCase("", 7.2 * 8)]
    [TestCase("tab-size:4;", 7.2 * 4)]
    [TestCase("tab-size:30px;", 22.5)]
    public void TabsAdvanceToTheNextTabStop(string declaration, double stop)
    {
        var runs = Layout($"<div style='white-space:pre;{declaration}'>a\tb</div>").SelectMany(page => page.Runs).OrderBy(run => run.X).ToArray();
        Assert.That(runs.Last().X, Is.EqualTo(Page.MarginLeft + stop).Within(0.01), "b starts at the first stop after a");
        Assert.That(Text("<div style='white-space:pre;tab-size:4'>abcd\te</div>").Single().Replace(" ", ""), Is.EqualTo("abcde"));
        Assert.That(Layout("<div style='white-space:pre;tab-size:4'>abcd\te</div>").SelectMany(page => page.Runs).Last().X,
            Is.EqualTo(Page.MarginLeft + 7.2 * 8).Within(0.01), "a tab at a stop runs to the next one");
    }

    [Test]
    public void InlineWhiteSpaceAppliesToItsOwnText()
    {
        Assert.That(Text("<div style='width:60pt'>aaa <span style='white-space:nowrap'>bbb ccc</span> ddd</div>"),
            Is.EqualTo(new[] { "aaa", "bbb ccc", "ddd" }), "the nowrap span is not broken");
    }

    [Test]
    public void ZeroWidthSpaceBreaksALineAndPaintsNothing()
    {
        Assert.That(Text("<div style='width:50pt'>aaaaaa&#8203;bbbbbb</div>"), Is.EqualTo(new[] { "aaaaaa", "bbbbbb" }));
        Assert.That(Content("<div>a&#8203;b</div>"), Does.Not.Contain("200B").And.Not.Contain("E2808B"));
    }

    [Test]
    public void OverflowHiddenClipsToThePaddingBoxAndVisibleDoesNot()
    {
        static string Wide(string declaration) => "<div style='width:50pt;border:2pt solid #00f;" + declaration + "'>abcdefghijklmnopqrstuvwxyz</div><p>next</p>";
        var hidden = Content(Wide("overflow:hidden"));
        var visible = Content(Wide("overflow:visible"));
        Assert.That(hidden, Does.Contain(" re W n"));
        Assert.That(visible, Does.Not.Contain(" re W n"));
        Assert.That(Lines(Wide("overflow:hidden")), Is.EqualTo(Lines(Wide("overflow:visible"))),
            "clipping changes what is painted, never where anything goes");
        Assert.That(Content(Wide("overflow:clip")), Does.Contain(" re W n"));
    }

    [Test]
    public void ClippingOneAxisLeavesTheOtherUnclipped()
    {
        var clipped = Content("<div style='width:50pt;overflow-x:clip'>abcdefghijklmnop</div>");
        Assert.That(clipped, Does.Contain("2000000 re W n"), "the free axis gets a path wider than any page");
    }

    [Test]
    public void ScrollingBoxesFailBecauseTheirScrollbarsWouldPrint()
    {
        foreach (var html in new[] { "<div style='overflow:scroll'>x</div>", "<div style='overflow-y:scroll'>x</div>" })
            Assert.That(Assert.Throws<FactsPdfException>(() => Pdf(html))!.Code, Is.EqualTo("FPDF1201"));
        Assert.That(Assert.Throws<FactsPdfException>(() => Pdf("<div style='width:30pt;overflow:auto'>abcdefghijkl</div>"))!.Code, Is.EqualTo("FPDF1201"),
            "auto draws a scrollbar once the content overflows");
        Assert.DoesNotThrow(() => Pdf("<div style='width:200pt;overflow:auto'>fits</div>"), "auto draws nothing while the content fits");
    }

    [Test]
    public void FixedHeightClipsLinesBelowTheBoxAndTheNextBlockFollowsTheBox()
    {
        const string html = "<div style='width:72pt;height:30pt;overflow:hidden'>aaa bbb ccc ddd eee fff ggg hhh</div><p>after</p>";
        Assert.That(Text(html), Is.EqualTo(new[] { "aaa bbb", "ccc ddd", "eee fff", "after" }),
            "the third line starts inside the box and is cut by it; the fourth line starts below the box and is dropped");
        Assert.That(Layout("<div style='height:30pt;overflow:hidden'>" + string.Join(" ", Enumerable.Repeat("word", 400)) + "</div><p>after</p>").Count, Is.EqualTo(1),
            "cut-off lines never create another page");
    }

    [Test]
    public void TextOverflowEllipsisReplacesTheEndOfAnOverflowingLine()
    {
        const string style = "width:72pt;white-space:nowrap;overflow:hidden;text-overflow:ellipsis";
        Assert.That(Text($"<p style='{style}'>The quick brown fox</p>").Single(), Is.EqualTo("The qui..."), "Courier has no ellipsis glyph, so three full stops");
        Assert.That(Text($"<p style='{style}'>short</p>").Single(), Is.EqualTo("short"));
        Assert.That(Text("<p style='width:72pt;white-space:nowrap;overflow:hidden'>The quick brown fox</p>").Single(), Is.EqualTo("The quick brown fox"),
            "clip leaves the text for the clip path");
    }

    [Test]
    public void VisibilityHiddenKeepsTheRoomAndPaintsNothing()
    {
        var shown = Lines("<p>before</p><p>middle text</p><p>after</p>");
        var hidden = Lines("<p>before</p><p style='visibility:hidden;background:#f00'>middle text</p><p>after</p>");
        Assert.That(hidden.Select(line => line.Text), Is.EqualTo(new[] { "before", "after" }));
        Assert.That(hidden[1].X, Is.EqualTo(shown[2].X));
        var pages = Layout("<p>before</p><p style='visibility:hidden;background:#f00'>middle</p>");
        Assert.That(pages.SelectMany(page => page.PaintBoxes), Is.Empty, "no background either");
        // The next line sits where it would have: three lines tall, the middle one blank.
        Assert.That(Layout("<p>a</p><p style='visibility:hidden'>b</p><p>c</p>").SelectMany(p => p.Runs).Select(r => r.Baseline).Distinct().Count(), Is.EqualTo(2));
    }

    [Test]
    public void AVisibleChildOfAHiddenBoxPaints()
    {
        Assert.That(Text("<div style='visibility:hidden'><p>hid</p><p style='visibility:visible'>shown</p></div>"), Is.EqualTo(new[] { "shown" }));
        Assert.That(Text("<p>keep <span style='visibility:collapse'>gone</span> end</p>").Single().Replace("  ", " "), Is.EqualTo("keep end"));
    }

    [Test]
    public void ContentWiderThanThePagePrintsScaledDown()
    {
        var content = Content("<div style='white-space:nowrap'>" + new string('x', 52) + "</div><p>after</p>");
        // 52 characters of 7.2pt are 374.4pt on 260pt of page: scaled by 260/374.4.
        Assert.That(content, Does.Contain("q 0.6944"), "the page is printed at 260 / 374.4");
        Assert.That(content, Does.Contain("/MediaBox [0 0 300 400]"));
        Assert.That(Content("<div style='white-space:nowrap'>" + new string('x', 20) + "</div>"), Does.Not.Contain(" cm\n"), "fitting content is not scaled");
    }

    [Test]
    public void ScalingStopsAtOneAndAHalfTimesThePage()
    {
        var content = Content("<div style='white-space:nowrap'>" + new string('x', 100) + "</div>");
        Assert.That(content, Does.Contain("q 0.6666"), "Chrome never shrinks more than 1.5 times");
    }

    [Test]
    public void ABoxWiderThanItsContainerOverflowsInsteadOfFailing()
    {
        var pages = Layout("<div style='width:340pt;border:1pt solid red'>wide</div>");
        Assert.That(pages, Has.Count.EqualTo(1));
        Assert.DoesNotThrow(() => Pdf("<p style='width:400pt;padding:10pt;margin:0 auto'>x</p>"));
        Assert.DoesNotThrow(() => Pdf("<div style='margin:0 300pt'>x</div>"));
    }

    [Test]
    public void ScalingAPageWithFurnitureFailsInsteadOfApproximating()
    {
        var options = Page with { PageNumbers = true };
        Assert.That(Assert.Throws<FactsPdfException>(() => Pdf("<div style='white-space:nowrap'>" + new string('x', 52) + "</div>", options))!.Code,
            Is.EqualTo("FPDF1302"));
    }

    [Test]
    public void VisibilityOnTablePartsFailsExplicitly()
    {
        Assert.That(Assert.Throws<FactsPdfException>(() => Pdf("<table><tr><td style='visibility:hidden'>x</td></tr></table>"))!.Code, Is.EqualTo("FPDF1201"));
        Assert.That(Assert.Throws<FactsPdfException>(() => Pdf("<table><tr style='visibility:collapse'><td>x</td></tr></table>"))!.Code, Is.EqualTo("FPDF1201"));
        Assert.That(Assert.Throws<FactsPdfException>(() => Pdf("<div style='visibility:hidden'><table><tr><td style='background:#eee'>x</td></tr></table></div>"))!.Code,
            Is.EqualTo("FPDF1201"), "a hidden table would still paint its cell background");
        Assert.DoesNotThrow(() => Pdf("<div style='visibility:hidden'><table><tr><td>x</td></tr></table></div>"));
    }

    [Test]
    public void SoftHyphenBreaksWithAVisibleHyphenOnlyWhereItFitsAndHyphensNoneKeepsTheWord()
    {
        Assert.That(Text("<div style='width:36pt'>abc&#173;defgh</div>"), Is.EqualTo(new[] { "abc-", "defgh" }));
        Assert.That(Text("<div style='width:100pt'>abc&#173;defgh</div>"), Is.EqualTo(new[] { "abcdefgh" }), "no hyphen where no break is needed");
        Assert.That(Text("<div style='width:28pt'>abc&#173;defgh</div>"), Is.EqualTo(new[] { "abc-", "defgh" }),
            "where no break fits, Chrome still breaks at the soft hyphen and lets the hyphen overflow");
        Assert.That(Text("<div style='width:36pt;hyphens:none'>abc&#173;defgh</div>"), Is.EqualTo(new[] { "abcdefgh" }));
        Assert.That(Assert.Throws<FactsPdfException>(() => Pdf("<div style='hyphens:auto'>x</div>"))!.Code, Is.EqualTo("FPDF1202"),
            "automatic hyphenation needs a dictionary");
    }

    [Test]
    public void AZeroSizedImageKeepsAnEmptyBoxAndPaintsNothing()
    {
        var options = Page with { Images = new Dictionary<string, PdfImage> { ["gray"] = PdfImage.FromBytes(JpegResourceTests.Bytes("gray")) } };
        var pages = Layout("<p>a<img src='gray' style='width:0'>b</p><img src='gray' style='height:0'>", options);
        Assert.That(pages.SelectMany(page => page.Images), Is.Empty);
        Assert.That(pages.SelectMany(page => page.Runs).Select(run => run.Text), Is.EqualTo(new[] { "a", "b" }));
    }
}
