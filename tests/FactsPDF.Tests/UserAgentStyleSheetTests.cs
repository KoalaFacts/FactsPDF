using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// The user-agent stylesheet is the lowest cascade origin and gives every supported element Chrome's
/// default style. The fixture font has hhea ascent 800 and descent -200, so at 12pt (16px) its
/// normal line is 13px + 3px = 12pt with the baseline 9.75pt below the line top.
/// </summary>
[TestFixture]
public sealed class UserAgentStyleSheetTests
{
    private const double Top = 400, Ascent = 9.75, Line = 12;
    private static readonly PdfFont Regular = PdfFont.FromBytes(FontFixture.Create(listDigits: true));
    private static readonly PdfFont Bold = PdfFont.FromBytes(FontFixture.Create(listDigits: true));

    private static PdfOptions Options(bool boldChain = false, double height = Top)
        => new() { PageWidth = 300, PageHeight = height, Margin = 0, Fonts = [Regular], BoldFonts = boldChain ? [Bold] : [] };

    private static List<LayoutPage> Layout(string html, PdfOptions? options = null)
    {
        var settings = options ?? Options();
        return TextLayout.Layout(HtmlDocumentReader.ReadTree(html, settings, default), settings, default);
    }

    // The distance from the top of the page to the baseline of the run that reads text.
    private static double FromTop(List<LayoutPage> pages, string text, double height = Top)
        => height - pages.SelectMany(p => p.Runs).First(r => r.Text == text).Baseline;

    [Test]
    public void BodyAndParagraphMarginsCollapseLikeChrome()
    {
        var pages = Layout("<p>A</p><p>B</p>");
        // The 6pt body margin collapses into the paragraph's 12pt; the paragraphs are 12pt apart.
        Assert.That(FromTop(pages, "A"), Is.EqualTo(12 + Ascent).Within(0.001));
        Assert.That(FromTop(pages, "B"), Is.EqualTo(12 + Line + 12 + Ascent).Within(0.001));
    }

    [Test]
    public void AuthorRulesBeatTheSheetAndRevertReturnsToIt()
    {
        Assert.That(FromTop(Layout("<style>body,p{margin:0}</style><p>A</p>"), "A"), Is.EqualTo(Ascent).Within(0.001));
        Assert.That(FromTop(Layout("<p style='margin:0'>A</p>"), "A"), Is.EqualTo(6 + Ascent).Within(0.001));
        Assert.That(FromTop(Layout("<style>p{margin:0}p{margin-top:revert}</style><p>A</p>"), "A"),
            Is.EqualTo(12 + Ascent).Within(0.001));
        // An author line-height beats the font's normal one.
        var pitch = Layout("<style>body,p{margin:0;line-height:2}</style><p>A</p><p>A</p>").SelectMany(p => p.Runs).ToList();
        Assert.That(pitch[0].Baseline - pitch[1].Baseline, Is.EqualTo(24).Within(0.001));
    }

    [Test]
    public void NormalLineHeightIsTheFontsOwnAndNotAMultiplier()
    {
        var runs = Layout("<style>body,p{margin:0}</style><p>A<br>A</p>").SelectMany(p => p.Runs).ToList();
        Assert.That(runs[0].Baseline - runs[1].Baseline, Is.EqualTo(Line).Within(0.001));
    }

    [Test]
    public void HeadingsTakeChromesSizeAndFallBackToTheRegularFaceWithoutABoldChain()
    {
        var withoutBold = Layout("<h1>A</h1>").SelectMany(p => p.Runs).Single();
        Assert.That(withoutBold.Style.FontSize, Is.EqualTo(24).Within(0.001));
        Assert.That(withoutBold.Font, Is.SameAs(Regular));
        var withBold = Layout("<h1>A</h1>", Options(boldChain: true)).SelectMany(p => p.Runs).Single();
        Assert.That(withBold.Font, Is.SameAs(Bold));
        // Only the heading's own weight is soft: bold the author or b asks for still needs its chain.
        foreach (var html in new[] { "<b>A</b>", "<h1 style='font-weight:bold'>A</h1>", "<h1>A <b>A</b></h1>" })
            Assert.That(Assert.Throws<FactsPdfException>(() => Layout(html))!.Code, Is.EqualTo("FPDF1507"), html);
    }

    [Test]
    public void ListsIndentByTheirPaddingAndTheAuthorPaddingWins()
    {
        Assert.That(Layout("<ul><li>A</li></ul>").SelectMany(p => p.Runs).Single().X, Is.EqualTo(6 + 30).Within(0.001));
        Assert.That(Layout("<style>ul{padding:0}</style><ul><li>A</li></ul>").SelectMany(p => p.Runs).Single().X,
            Is.EqualTo(6).Within(0.001));
        var nested = Layout("<ul><li>A<ul><li>B</li></ul></li></ul>").SelectMany(p => p.Runs).First(r => r.Text == "B");
        Assert.That(nested.X, Is.EqualTo(6 + 30 + 30).Within(0.001));
    }

    [Test]
    public void CellsAreCentredInAWholeRowAndTopAlignedInARowThatSplits()
    {
        const string cells = "<td>A<br>A<br>A</td><td>B</td>";
        double B(string css, double height = Top)
            => FromTop(Layout($"<style>body{{margin:0}}table{{width:200pt}}{css}</style><table><colgroup><col style='width:100pt'><col style='width:100pt'></colgroup><tbody><tr>{cells}</tr></tbody></table>",
                Options(height: height)), "B", height);
        // The shorter cell sits (3 lines - 1 line) / 2 below the top-aligned position.
        Assert.That(B("") - B("td{vertical-align:top}"), Is.EqualTo(Line).Within(0.001));

        // A row taller than a page cannot centre: the user-agent middle keeps the cell at the top ...
        var tall = string.Concat(Enumerable.Repeat("A<br>", 30)) + "A";
        string Split(string css, double height) => $"<style>body{{margin:0}}table{{width:200pt}}{css}</style><table><colgroup><col style='width:100pt'><col style='width:100pt'></colgroup><tbody><tr><td>{tall}</td><td>B</td></tr></tbody></table>";
        Assert.That(FromTop(Layout(Split("", 100), Options(height: 100)), "B", 100),
            Is.EqualTo(FromTop(Layout(Split("td{vertical-align:top}", 100), Options(height: 100)), "B", 100)).Within(0.001));
        // ... while an authored middle is still refused there.
        Assert.That(Assert.Throws<FactsPdfException>(() => Layout(Split("td{vertical-align:middle}", 100), Options(height: 100)))!.Code,
            Is.EqualTo("FPDF1302"));
    }

    [Test]
    public void BigIsLargerAndAnUntitledAbbrKeepsNoDecorationNextToATitledOne()
    {
        var big = Layout("<p>A <big>A</big></p>").SelectMany(p => p.Runs).Last(r => r.Text == "A");
        Assert.That(big.Style.FontSize, Is.EqualTo(14.4).Within(0.001));
        var abbrs = Layout("<p><abbr title='t'>A</abbr> <abbr>B</abbr></p>").SelectMany(p => p.Runs).ToList();
        Assert.That(abbrs.Single(r => r.Text == "A").Style.Decoration, Is.EqualTo(TextDecoration.Underline));
        Assert.That(abbrs.Single(r => r.Text == "B").Style.Decoration, Is.EqualTo(TextDecoration.None));
    }

    [Test]
    public void TheSheetDeclaresNoDisplayAndEveryRuleEndsInAType()
    {
        Assert.That(UserAgentStyleSheet.For("p"), Is.Not.Null);
        Assert.That(UserAgentStyleSheet.For("div"), Is.Null);
        Assert.That(UserAgentStyleSheet.Properties, Does.Not.Contain(CssProperty.Display));
    }
}
