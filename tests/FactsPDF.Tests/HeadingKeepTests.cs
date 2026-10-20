using NUnit.Framework;

namespace FactsPDF.Tests;

public sealed class HeadingKeepTests
{
    internal static readonly PdfOptions Page = new() { PageWidth = 240, PageHeight = 100, Margin = 10 };
    internal const string Css = "<style>p,h1,h2,h3,h4,h5,h6{font-size:10pt;line-height:2;margin-top:0;margin-bottom:0;font-weight:normal}</style>";
    internal static string Before(int lines) => "<p>" + string.Join("<br>", Enumerable.Repeat("BEFORE", lines)) + "</p>";
    internal static string Heading(string text = "TITLE", string extra = "") => $"<h2 style='break-after:avoid;{extra}'>{text}</h2>";
    private static string[] Pages(string html, PdfOptions? options = null) => TableLayoutTests.Layout(Css + html, options ?? Page)
        .Select(p => string.Join(',', p.Runs.Select(r => r.Text))).ToArray();

    [Test]
    public void InsufficientTailMovesCompleteHeadingAndFirstParagraph()
        => Assert.That(Pages(Before(2) + Heading() + "<p>A<br>B</p><p>AFTER</p>"),
            Is.EqualTo(new[] { "BEFORE,BEFORE", "TITLE,A,B,AFTER" }));

    [TestCase("TITLE")] [TestCase("A<span>B</span><span style='color:red'>C</span><span style='font-size:9pt'>D</span>")]
    public void FitsHereProducesIdenticalPdfBytes(string title)
    {
        var html = Css + Before(1) + Heading(title) + "<p>A<br>B</p>";
        Assert.That(TableLayoutTests.Pdf(html, Page), Is.EqualTo(TableLayoutTests.Pdf(html.Replace("break-after:avoid;", ""), Page)));
    }

    [Test]
    public void OversizedCombinationRelaxesWithoutEmptyPages()
    {
        var html = Before(2) + Heading() + "<p>A<br>B<br>C<br>D</p>";
        Assert.That(Pages(html), Is.EqualTo(new[] { "BEFORE,BEFORE,TITLE,A", "B,C,D" }));
    }

    [TestCase("break-before:page", "")]
    [TestCase("", "break-after:page")]
    public void InternalExplicitBreakWins(string paragraph, string heading)
        => Assert.That(Pages(Before(1) + Heading(extra: heading) + $"<p style='{paragraph}'>A<br>B</p>"),
            Is.EqualTo(new[] { "BEFORE,TITLE", "A,B" }));

    [Test]
    public void FirstBeforeAndFinalAfterBreaksKeepExistingPlacement()
        => Assert.That(Pages(Before(1) + Heading(extra: "break-before:page") + "<p style='break-after:page'>A</p><p>AFTER</p>"),
            Is.EqualTo(new[] { "BEFORE", "TITLE,A", "AFTER" }));

    [TestCase("")] [TestCase("<p></p><p>  </p>")]
    public void ConsecutiveMarkedHeadingsKeepWholeChain(string empty)
        => Assert.That(Pages(Before(2) + Heading("ONE") + empty + Heading("TWO") + "<p>A</p>"),
            Is.EqualTo(new[] { "BEFORE,BEFORE", "ONE,TWO,A" }));

    [Test]
    public void BreakOnlyParagraphContributesLineGeometry()
        => Assert.That(Pages(Before(2) + Heading() + "<p><br></p><p>A</p>"),
            Is.EqualTo(new[] { "BEFORE,BEFORE", "TITLE,A" }));

    [TestCase("<h2>OTHER</h2><p>A</p>")]
    [TestCase("<div><p>A<br>B</p></div>")]
    [TestCase("")]
    public void BoundaryDoesNotSearchForAnotherParagraph(string suffix)
    {
        var html = Css + Before(3) + Heading() + suffix;
        Assert.That(TableLayoutTests.Pdf(html, Page), Is.EqualTo(TableLayoutTests.Pdf(html.Replace("break-after:avoid;", ""), Page)));
    }

    [TestCase("auto")] [TestCase("initial")] [TestCase("unset")] [TestCase("inherit")]
    public void CascadeCanResetKeep(string value)
    {
        var html = Css + "<style>h2{break-after:avoid}</style>" + Before(2) + $"<h2 style='break-after:{value}'>TITLE</h2><p>A<br>B</p>";
        Assert.That(TableLayoutTests.Pdf(html, Page), Is.EqualTo(TableLayoutTests.Pdf(html.Replace("h2{break-after:avoid}", ""), Page)));
    }

    [Test]
    public void ImportantWinsAndDoesNotImplicitlyInherit()
        => Assert.That(Pages("<style>h2{break-after:avoid!important}</style>" + Before(2) + "<h2 style='break-after:auto'>TITLE</h2><p>A<br>B</p>"),
            Is.EqualTo(new[] { "BEFORE,BEFORE", "TITLE,A,B" }));

    [TestCase("<p style='break-after:avoid'>A</p>")]
    [TestCase("<ul><li><div><h2 style='break-after:avoid'>A</h2><p>B</p></div></li></ul>")]
    [TestCase("<style>p{break-after:avoid}</style><p style='break-after:auto'>A</p>")]
    public void UnsupportedApplicabilityFailsEvenWhenOverridden(string html)
        => Fails(Css + html, "FPDF1201", Page);

    [Test]
    public void ExactResourceLimitsAndAtomicFailureRemain()
    {
        var html = Css + Before(2) + Heading() + "<p>A<br>B</p>";
        var expected = TableLayoutTests.Pdf(html, Page);
        Assert.That(TableLayoutTests.Pdf(html, Page with { MaxDisplayCommands = 5, MaxPages = 2, MaxOutputBytes = expected.Length }), Is.EqualTo(expected));
        Fails(html, "FPDF1401", Page with { MaxDisplayCommands = 4 });
        Fails(html, "FPDF1303", Page with { MaxPages = 1 });
        Fails(html, "FPDF1401", Page with { MaxOutputBytes = expected.Length - 1 });
    }

    [TestCase("", 1)] [TestCase("background-color:red", 2)]
    public void LookaheadDoesNotPreemptBudgetFailureWithLaterMissingGlyph(string decoration, int budget)
        => Fails(Css + Heading(extra: decoration) + "<p>中文</p>", "FPDF1401", Page with { MaxDisplayCommands = budget });

    [Test]
    public void OversizedChainDoesNotRetryShorterSuffix()
        => Assert.That(Pages(Before(1) + Heading("ONE") + Heading("TWO") + Heading("THREE") + "<p>A<br>B</p>"),
            Is.EqualTo(new[] { "BEFORE,ONE,TWO,THREE", "A,B" }));
    private static void Fails(string html, string code, PdfOptions options)
    {
        using var output = new MemoryStream(); output.Write("sentinel"u8); output.Position = 3;
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, options));
        Assert.That(error!.Code, Is.EqualTo(code));
        Assert.That(output.ToArray(), Is.EqualTo("sentinel"u8.ToArray()));
        Assert.That(output.Position, Is.EqualTo(3));
    }

    [Test]
    public void ParagraphPaddingBordersAndInternalMarginsMoveWithText()
    {
        var html = Before(2) + Heading(extra: "padding:2pt;border:1pt solid red;margin-bottom:3pt")
            + "<p style='padding:2pt;border:1pt solid blue;margin-top:5pt'>A<br>B</p>";
        var pages = TableLayoutTests.Layout(Css + html, Page);
        Assert.That(pages[0].PaintBoxes, Is.Empty);
        Assert.That(pages[1].Runs.Select(r => r.Text), Is.EqualTo(new[] { "TITLE", "A", "B" }));
        Assert.That(pages[1].PaintBoxes.Select(b => b.Top), Is.EqualTo(new[] { 10d, 41d }));
        Assert.That(pages[1].PaintBoxes.Select(b => b.Height), Is.EqualTo(new[] { 26d, 46d }));
    }

    [Test]
    public void EmptyParagraphSpacingAndPaintAreMeasuredWithoutTextCommands()
    {
        var pages = TableLayoutTests.Layout(Css + Before(2) + Heading()
            + "<p style='padding:2pt;background-color:red;margin-top:3pt;margin-bottom:5pt'></p><p>A</p>", Page);
        Assert.That(pages[0].Runs, Has.Count.EqualTo(2));
        Assert.That(pages[1].PaintBoxes.Single().Height, Is.EqualTo(4));
        Assert.That(pages[1].Runs[0].Baseline - pages[1].Runs[1].Baseline, Is.EqualTo(32));
    }

    [Test]
    public void RequiredAncestorTopMovesOnceAndDoesNotStrandPaint()
    {
        var pages = TableLayoutTests.Layout(Css + Before(2) + "<div style='padding-top:5pt;border:1pt solid red'>"
            + Heading(extra: "margin-top:8pt") + "<p>A<br>B</p></div>", Page);
        Assert.That(pages[0].PaintBoxes, Is.Empty);
        Assert.That(pages[1].PaintBoxes.Single().Top, Is.EqualTo(10));
        Assert.That(pages[1].Runs[0].Baseline, Is.EqualTo(71));
    }

    [TestCase("break-before:page")] [TestCase("break-after:page")]
    public void EmptyParagraphExplicitPageCancelsGroup(string property)
        => Assert.That(Pages(Before(2) + Heading() + $"<p style='{property}'></p><p>A<br>B</p>"),
            Is.EqualTo(new[] { "BEFORE,BEFORE,TITLE", "A,B" }));

    [Test]
    public void FinalParagraphExternalMarginDoesNotEnlargeGroup()
        => Assert.That(Pages(Before(2) + Heading() + "<p style='margin-bottom:100pt'>A<br>B</p>"),
            Is.EqualTo(new[] { "BEFORE,BEFORE", "TITLE,A,B" }));

    [Test]
    public void OversizedIndividualLineRetainsOriginalError()
        => Fails(Css + Before(1) + Heading() + "<p style='line-height:9'>A</p>", "FPDF1302", Page);

    [Test]
    public void PreCanceledConversionPreservesDestination()
    {
        using var output = new MemoryStream(); output.Write("sentinel"u8);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => PdfConverter.Convert(Css + Heading() + "<p>A</p>", output, Page, cancellation.Token));
        Assert.That(output.ToArray(), Is.EqualTo("sentinel"u8.ToArray()));
    }

    [Test]
    public void FailedAndOversizedChainsEmitAtMostOneLookahead()
    {
        var chain = string.Concat(Enumerable.Repeat(Heading(), 1000));
        foreach (var suffix in new[] { "", "<p>A</p>", "<p style='break-before:page'>A</p>" })
        {
            var tree = HtmlDocumentReader.ReadTree(Css + chain + suffix, Page, default);
            Assert.That(BlockLayout.Steps(tree, Page, default).OfType<LayoutHeadingGroup>().Count(), Is.LessThanOrEqualTo(1));
        }
    }

    [Test]
    public void RealWrappingUsesParagraphContentWidth()
        => Assert.That(Pages(Before(2) + Heading() + "<p style='width:30pt'>A A A A A A</p>"),
            Is.EqualTo(new[] { "BEFORE,BEFORE", "TITLE,A A A,A A A" }));

    [Test]
    public void ExplicitFontRolesAndInlineRunsRemainByteIdenticalWhenGroupFits()
    {
        var font = PdfFont.LoadTrueType(FontFixture.Create());
        var options = Page with { Fonts = [font], BoldFonts = [font], ItalicFonts = [font], BoldItalicFonts = [font] };
        var html = Css + Heading("A<span>A</span><b>A</b><i>A</i><b><i>A</i></b>") + "<p>A</p>";
        Assert.That(TableLayoutTests.Pdf(html, options), Is.EqualTo(TableLayoutTests.Pdf(html.Replace("break-after:avoid;", ""), options)));
    }

}
