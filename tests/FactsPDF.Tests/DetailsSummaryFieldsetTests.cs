using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

// details is a block, its first summary is a list item whose inside marker is a disclosure triangle
// (right-pointing while closed, down-pointing while open) and a closed details prints that summary
// only; fieldset is a block with a 2px groove border, 2px side margins and em padding (its legend is
// not supported). Before, each failed FPDF1102.
[TestFixture]
public sealed class DetailsSummaryFieldsetTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 260, PageHeight = 300, Margin = 12 };

    private static byte[] Pdf(string html) => TableLayoutTests.Pdf(html, Page);

    private static FactsPdfException Fails(string html)
    {
        using var output = new MemoryStream();
        var ex = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, Page))!;
        Assert.That(output.Length, Is.Zero);
        return ex;
    }

    private static string Content(string html) => Encoding.Latin1.GetString(Pdf(html));

    [Test]
    public void AClosedDetailsPrintsOnlyItsSummary()
    {
        Assert.That(Pdf("<p>A</p><details><summary>S</summary><p>Hidden</p>text<u>x</u></details><p>B</p>"),
            Is.EqualTo(Pdf("<p>A</p><details><summary>S</summary></details><p>B</p>")));
        Assert.That(Pdf("<details><summary>S1</summary><summary>S2</summary>C</details>"), Is.EqualTo(Pdf("<details><summary>S1</summary></details>")));
    }

    [Test]
    public void AnOpenDetailsPrintsItsContentAfterTheSummary()
    {
        Assert.That(Pdf("<details open><summary>S</summary><p>C</p></details>"), Is.Not.EqualTo(Pdf("<details><summary>S</summary></details>")));
        Assert.That(Pdf("<details open><summary style='list-style:none'>S</summary><p>C</p></details><p>B</p>"),
            Is.EqualTo(Pdf("<div><div>S</div><p>C</p></div><p>B</p>")));
        Assert.That(Pdf("<details open><summary style='list-style:none'>S1</summary><summary>S2</summary>T</details>"),
            Is.EqualTo(Pdf("<div><div>S1</div><div>S2</div>T</div>")));
        Assert.That(Pdf("<details name='g' open='open'><summary style='list-style-type:none'>S</summary>C</details>"),
            Is.EqualTo(Pdf("<div><div>S</div>C</div>")));
    }

    [Test]
    public void AClosedDetailsWithoutAMarkerIsItsSummaryAlone()
        => Assert.That(Pdf("<p>A</p><details><summary style='list-style:none'>S</summary>Hidden</details><p>B</p>"),
            Is.EqualTo(Pdf("<p>A</p><div><div>S</div></div><p>B</p>")));

    [Test]
    public void TheMarkerIsATriangleThatPointsRightClosedAndDownOpen()
    {
        var closed = Content("<details><summary>S</summary></details>");
        var open = Content("<details open><summary>S</summary></details>");
        var plain = Content("<details><summary style='list-style:none'>S</summary></details>");
        Assert.That(closed, Is.Not.EqualTo(open));
        Assert.That(closed.Length, Is.GreaterThan(plain.Length));
        // The triangle is a filled path of three vertices in the summary's text colour.
        var triangle = new System.Text.RegularExpressions.Regex(@" m [\d. -]+ l [\d. -]+ l h f ");
        Assert.That(triangle.IsMatch(closed), Is.True);
        Assert.That(triangle.IsMatch(open), Is.True);
        Assert.That(triangle.IsMatch(plain), Is.False);
    }

    [Test]
    public void TheMarkerTakesTheSummarysTextColourAndTheSummaryTextStartsAfterIt()
    {
        var page = TableLayoutTests.Layout("<details><summary style='color:#c00;font-size:24pt'>S</summary></details>", Page).Single();
        Assert.That(page.Images, Has.Count.EqualTo(1));
        Assert.That(page.Images[0].Width, Is.EqualTo(1.059 * 24).Within(0.0001));
        Assert.That(page.Runs.Single().X, Is.EqualTo(page.Images[0].X + 1.059 * 24).Within(0.0001));
        Assert.That(Content("<details><summary style='color:#c00'>S</summary></details>"), Does.Contain("0.8 0 0 rg"));
    }

    [Test]
    public void WhiteSpaceRightAfterTheMarkerStartsTheLine()
        => Assert.That(Pdf("<details><summary>  <u> S</u></summary></details>"), Is.EqualTo(Pdf("<details><summary><u>S</u></summary></details>")));

    [Test]
    public void ASummaryWithBlockContentPutsTheMarkerOnALineOfItsOwn()
    {
        var page = TableLayoutTests.Layout("<details><summary><div>D</div></summary></details>", Page).Single();
        Assert.That(page.Images, Has.Count.EqualTo(1));
        Assert.That(page.Images[0].Top + page.Images[0].Height, Is.LessThan(page.Runs.Single().Baseline - 1));
    }

    [Test]
    public void NestedDetailsKeepTheirOwnState()
    {
        Assert.That(Pdf("<details open><summary>A</summary><details><summary>B</summary>Hidden</details></details>"),
            Is.EqualTo(Pdf("<details open><summary>A</summary><details><summary>B</summary></details></details>")));
        Assert.That(Pdf("<details><summary>A</summary><details open><summary>B</summary>Hidden</details></details>"),
            Is.EqualTo(Pdf("<details><summary>A</summary></details>")));
    }

    [Test]
    public void DetailsStyleFollowsTheBlockRules()
        => Assert.That(Pdf("<details open style='padding:4pt;border:1pt solid #000'><summary style='list-style:none;margin:3pt 0'>S</summary>C</details>"),
            Is.EqualTo(Pdf("<div style='padding:4pt;border:1pt solid #000'><div style='margin:3pt 0'>S</div>C</div>")));

    [TestCase("<details>Text<summary>S</summary></details>")]
    [TestCase("<details><p>C</p><summary>S</summary></details>")]
    [TestCase("<details></details>")]
    [TestCase("<details open>Content</details>")]
    public void ADetailsThatDoesNotStartWithASummaryFails(string html)
        => Assert.That(Fails(html).Code, Is.EqualTo("FPDF1101"));

    [TestCase("<details><summary style='list-style-type:disc'>S</summary></details>", "FPDF1201")]
    [TestCase("<details><summary style='list-style-position:outside'>S</summary></details>", "FPDF1201")]
    [TestCase("<style>details::before{content:'x'}</style><details><summary>S</summary></details>", "FPDF1201")]
    [TestCase("<details><summary>S</summary></details>x<details><summary foo=1>S</summary></details>", "FPDF1103")]
    [TestCase("<details foo><summary>S</summary></details>", "FPDF1103")]
    public void AMarkerOrAttributeThatIsNotSupportedFails(string html, string code)
        => Assert.That(Fails(html).Code, Is.EqualTo(code));

    [Test]
    public void ASummaryOutsideADetailsIsAPlainBlock()
        => Assert.That(Pdf("<summary>S</summary><p>B</p>"), Is.EqualTo(Pdf("<div>S</div><p>B</p>")));

    [Test]
    public void TheMarkerIsDrawnOnTheSummarysFirstLineOnly()
    {
        var page = TableLayoutTests.Layout("<details><summary>" + string.Join(' ', Enumerable.Repeat("WORD", 12)) + "</summary></details>", Page).Single();
        Assert.That(page.Images, Has.Count.EqualTo(1));
        Assert.That(page.Runs.Select(r => r.Baseline).Distinct().Count(), Is.GreaterThan(1));
    }

    [Test]
    public void FieldsetIsABlockWithAGrooveBorderSideMarginsAndEmPadding()
    {
        var page = TableLayoutTests.Layout("<style>body{margin:0}</style><div>A</div><fieldset><div>F</div></fieldset><div>B</div>", Page).Single();
        // 12pt text: margin 1.5pt, border 1.5pt and padding 0.75em = 9pt put the content 12pt in.
        var inner = page.Runs.Single(r => r.Text == "F");
        Assert.That(inner.X, Is.EqualTo(12 + 1.5 + 1.5 + 9).Within(0.0001));
        Assert.That(page.Runs.Single(r => r.Text == "A").X, Is.EqualTo(12).Within(0.0001));
        // The content starts 1.5pt border + 0.35em padding below the box top, and the box ends
        // 0.625em padding + 1.5pt border below the content.
        var above = page.Runs.Single(r => r.Text == "A").Baseline;
        var below = page.Runs.Single(r => r.Text == "B").Baseline;
        Assert.That(above - inner.Baseline, Is.EqualTo(14.4 + 1.5 + 0.35 * 12).Within(0.0001));
        Assert.That(inner.Baseline - below, Is.EqualTo(14.4 + 0.625 * 12 + 1.5).Within(0.0001));
    }

    [Test]
    public void FieldsetPaddingUsesTheFieldsetsOwnFontSizeAndAuthorValuesWin()
    {
        var big = TableLayoutTests.Layout("<style>body{margin:0}</style><fieldset style='font-size:20pt'><p style='font-size:12pt'>F</p></fieldset>", Page).Single();
        Assert.That(big.Runs.Single().X, Is.EqualTo(12 + 1.5 + 1.5 + 0.75 * 20).Within(0.0001));
        var author = TableLayoutTests.Layout("<style>body{margin:0}</style><fieldset style='padding:2pt 3pt'><p>F</p></fieldset>", Page).Single();
        Assert.That(author.Runs.Single().X, Is.EqualTo(12 + 1.5 + 1.5 + 3).Within(0.0001));
    }

    [Test]
    public void FieldsetBorderIsTwoBandsPerEdgeAndAuthorBordersReplaceIt()
    {
        var groove = Content("<fieldset><p>F</p></fieldset>");
        var rectangles = new System.Text.RegularExpressions.Regex(@" re f ").Count(groove);
        Assert.That(rectangles, Is.EqualTo(8));
        // Chrome prints ThreeDFace as #f0f0f0 and the outer band of a top or left edge in its darker
        // shade, floor(240/255 * (v - 0.33) / v * 256) = 156, #9c9c9c.
        Assert.That(groove, Does.Contain("0.941 0.941 0.941 rg"));
        Assert.That(groove, Does.Contain("0.612 0.612 0.612 rg"));
        var solid = Content("<fieldset style='border:1pt solid #ccc'><p>F</p></fieldset>");
        Assert.That(new System.Text.RegularExpressions.Regex(@" re f ").Count(solid), Is.EqualTo(4));
        Assert.That(Pdf("<fieldset style='border:none'><p>F</p></fieldset>"),
            Is.EqualTo(Pdf("<div style='margin:0 1.5pt;padding:4.2pt 9pt 7.5pt'><p>F</p></div>")));
    }

    [Test]
    public void FieldsetKeepsItsGrooveColourWhenOnlyTheStyleChanges()
        => Assert.That(Content("<fieldset style='border-style:solid'><p>F</p></fieldset>"), Does.Contain("0.941 0.941 0.941 rg"));

    [Test]
    public void FieldsetAttributesChangeNothingAndLegendIsNotSupported()
    {
        Assert.That(Pdf("<fieldset disabled form='f' name='n'><p>F</p></fieldset>"), Is.EqualTo(Pdf("<fieldset><p>F</p></fieldset>")));
        Assert.That(Fails("<fieldset><legend>L</legend><p>F</p></fieldset>").Code, Is.EqualTo("FPDF1102"));
    }

    [Test]
    public void FieldsetsNestAndSplitAcrossPages()
    {
        Assert.That(Pdf("<fieldset><p>A</p><fieldset><p>B</p></fieldset></fieldset>"), Is.Not.Empty);
        var many = string.Concat(Enumerable.Repeat("<p>line</p>", 40));
        var pages = TableLayoutTests.Layout($"<fieldset>{many}</fieldset>", Page);
        Assert.That(pages.Count, Is.GreaterThan(1));
    }

    [Test]
    public void ARoundedGrooveBorderFails()
        => Assert.That(Fails("<fieldset style='border-radius:4pt'><p>F</p></fieldset>").Code, Is.EqualTo("FPDF1201"));
}
