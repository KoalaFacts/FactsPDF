using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>Scoped first-line text indentation changes actual glyph positions/wrapping, not painted box width.</summary>
[TestFixture]
public sealed class TextIndentTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 220, PageHeight = 260, Margin = 20 };

    private static List<LayoutPage> Layout(string html, PdfOptions? page = null)
    {
        var options = page ?? Page;
        return TextLayout.Layout(HtmlDocumentReader.ReadTree(html, options, default), options, default);
    }

    private static byte[] Pdf(string html, PdfOptions? page = null)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, page ?? Page);
        return output.ToArray();
    }

    private static void Rejected(string html, string code, PdfOptions? page = null)
    {
        using var output = new MemoryStream();
        output.Write("old-report"u8);
        output.Position = 3;
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, page ?? Page));
        Assert.That(error!.Code, Is.EqualTo(code));
        Assert.That(output.ToArray(), Is.EqualTo("old-report"u8.ToArray()));
        Assert.That(output.Position, Is.EqualTo(3));
    }

    [Test]
    public void FirstLineMovesAndWrapsWithoutIndentingTheNextLineOrItsPaintedBox()
    {
        const string html = "<div style='width:120pt'><p style='text-indent:18pt;background:#eef2f5'>" +
            "Alpha beta gamma delta epsilon zeta eta theta</p></div>";
        var layout = Layout(html).Single();
        Assert.That(layout.Runs.Count, Is.GreaterThan(1));
        Assert.That(layout.Runs[0].X, Is.EqualTo(38).Within(0.001));
        Assert.That(layout.Runs[1].X, Is.EqualTo(20).Within(0.001));
        Assert.That(string.Concat(layout.Runs.Select(r => r.Text)).Replace(" ", ""),
            Is.EqualTo("Alphabetagammadeltaepsilonzetaetatheta"));
        var paint = layout.PaintBoxes.Single(b => b.Style.BackgroundColor.HasValue);
        Assert.That(paint.X, Is.EqualTo(20).Within(0.001));
        Assert.That(paint.Width, Is.EqualTo(120).Within(0.001));
        Assert.That(Pdf(html), Is.Not.Empty);
    }

    [Test]
    public void PercentageUsesActualParagraphContentWidthAndMatchesPointsInPdfBytes()
    {
        const string prefix = "<div style='width:140pt;padding:10pt'><p style='width:100pt;";
        const string content = "'>Inspection result for equipment and seals</p></div>";
        Assert.That(Pdf(prefix + "text-indent:20%" + content),
            Is.EqualTo(Pdf(prefix + "text-indent:20pt" + content)));
        Assert.That(Pdf(prefix + "text-indent:0" + content),
            Is.EqualTo(Pdf(prefix + "text-indent:0pt" + content)));
        Assert.That(Pdf(prefix + "text-indent:0" + content),
            Is.EqualTo(Pdf(prefix[..^1] + content)));
    }

    [Test]
    public void IndentAppliesOnlyOnceAcrossExplicitBreaksAndPageFragments()
    {
        const string html = "<p style='font-size:10pt;line-height:2;text-indent:12pt'>" +
            "FIRST<br>SECOND<br>THIRD<br>FOURTH</p>";
        var pages = Layout(html, Page with { PageHeight = 78, Margin = 10 });
        Assert.That(pages.Count, Is.GreaterThan(1));
        var runs = pages.SelectMany(p => p.Runs).ToArray();
        Assert.That(runs.Select(r => r.Text), Is.EqualTo(new[] { "FIRST", "SECOND", "THIRD", "FOURTH" }));
        Assert.That(runs.Select(r => r.X),
            Is.EqualTo(new[] { 22d, 10d, 10d, 10d }).Within(0.001));
    }

    [Test]
    public void CentreAndRightAlignUseRemainingFirstLineTextSpace()
    {
        const string left = "<p style='width:120pt;text-indent:12pt;text-align:left'>FIRST<br>SECOND</p>";
        const string center = "<p style='width:120pt;text-indent:12pt;text-align:center'>FIRST<br>SECOND</p>";
        const string right = "<p style='width:120pt;text-indent:12pt;text-align:right'>FIRST<br>SECOND</p>";
        var l = Layout(left).Single().Runs; var c = Layout(center).Single().Runs;
        var r = Layout(right).Single().Runs;
        Assert.That(l[0].X, Is.EqualTo(32).Within(0.001));
        Assert.That(l[1].X, Is.EqualTo(20).Within(0.001));
        Assert.That(c[0].X - c[1].X, Is.GreaterThan(0));
        Assert.That(r[0].X + r[0].Width, Is.EqualTo(140).Within(0.001));
        Assert.That(r[1].X + r[1].Width, Is.EqualTo(140).Within(0.001));
    }

    [Test]
    public void CascadeAndCssWideKeywordsSetTheActualWinningIndent()
    {
        const string html = "<style>p{text-indent:5pt!important}.override{text-indent:9pt}" +
            ".reset{text-indent:initial!important}.unset{text-indent:unset!important}</style>" +
            "<p class=override>FIRST</p><p class=reset>SECOND</p>" +
            "<p class=unset>THIRD</p><h2 style='text-indent:inherit'>FOURTH</h2>";
        var paragraphs = HtmlDocumentReader.Read(html, Page, default);
        Assert.That(paragraphs.Select(p => p.Style.FirstLineIndent.Value),
            Is.EqualTo(new[] { 5d, 0d, 0d, 0d }));
    }

    [TestCase("text-indent:-1pt", "FPDF1202")]
    [TestCase("text-indent:2em", "FPDF1202")]
    [TestCase("text-indent:calc(1pt + 2pt)", "FPDF1202")]
    [TestCase("text-indent:2pt 3pt", "FPDF1202")]
    [TestCase("text-indent:100%", "FPDF1302")]
    [TestCase("text-indent:200pt", "FPDF1302")]
    public void UnsafeInputsAreNotIgnoredAndLeaveExistingPdfUntouched(string declaration, string code)
        => Rejected("<p style='" + declaration + "'>SHORT</p>", code,
            Page with { PageWidth = 110 });

    [Test]
    public void AFirstWordThatFitsTheBoxButNotIndentedFirstLineCannotBeClipped()
        => Rejected("<p style='text-indent:50pt'>LONG</p>", "FPDF1302",
            Page with { PageWidth = 100 });

    [TestCase("<div style='text-indent:10pt'><p>A</p></div>")]
    [TestCase("<p><span style='text-indent:10pt'>A</span></p>")]
    [TestCase("<table><col style='width:80pt'><tr><td><p style='text-indent:5pt'>A</p></td></tr></table>")]
    public void UnsupportedElementScopesFailRatherThanSilentlyDiscard(string html)
        => Rejected(html, "FPDF1201");

    [Test]
    public void UnmatchedRuleStillValidatesInvalidValue()
        => Rejected("<style>.missing{text-indent:-2pt}</style><p>Ready</p>", "FPDF1202");
}
