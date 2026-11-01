using System.Text;
using FactsPDF.CommandLine;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// A real, bounded minimum-height for standalone report panels. Rendering,
/// geometry, pagination and protected-output failures are checked together.
/// </summary>
[TestFixture]
public sealed class ReportPanelMinHeightTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 500, Margin = 20 };

    private static byte[] Render(string html, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, options ?? Page);
        return output.ToArray();
    }

    private static List<LayoutPage> Layout(string html, PdfOptions? options = null)
    {
        var page = options ?? Page;
        return TextLayout.Layout(HtmlDocumentReader.ReadTree(html, page, default), page, default);
    }

    private static void Reject(string html, string code, PdfOptions? options = null)
    {
        using var destination = new MemoryStream();
        destination.Write("PREVIOUS-CUSTOMER-PDF"u8);
        destination.Position = 5;
        var ex = Assert.Throws<FactsPdfException>(() =>
            PdfConverter.Convert(html, destination, options ?? Page));
        Assert.That(ex!.Code, Is.EqualTo(code));
        Assert.That(destination.ToArray(), Is.EqualTo("PREVIOUS-CUSTOMER-PDF"u8.ToArray()));
        Assert.That(destination.Position, Is.EqualTo(5));
        Assert.That(Render("<p>Recovered</p>").Length, Is.GreaterThan(100));
    }

    [TestCase("div")]
    [TestCase("section")]
    [TestCase("article")]
    [TestCase("blockquote")]
    public void MinimumHeightCreatesRealPaintAndMovesFollowingText(string tag)
    {
        var before = "<" + tag + " style='min-height:90pt;background:#edf5fa;" +
            "border:2pt solid #23446b;padding:5pt'><p style='margin:0'>FIRST</p></" + tag + ">" +
            "<p style='margin:0'>AFTER</p>";
        var control = before.Replace("min-height:90pt;", "");
        var layout = Layout(before);
        var painted = layout.Single().PaintBoxes.Single();
        Assert.That(painted.Height, Is.EqualTo(104).Within(0.00001),
            "Content-box minimum reserves real height in addition to 14pt vertical border/padding.");
        Assert.That(painted.Top, Is.EqualTo(tag == "blockquote" ? 28 : 20).Within(0.00001),
            "Blockquotes retain their pre-existing 8pt outside top margin.");
        Assert.That(layout.Single().Runs.Select(run => run.Text), Is.EqualTo(new[] { "FIRST", "AFTER" }));
        Assert.That(layout.Single().Runs[0].X, Is.EqualTo(Layout(control).Single().Runs[0].X));
        Assert.That(layout.Single().Runs[1].Baseline,
            Is.LessThan(Layout(control).Single().Runs[1].Baseline));
        Assert.That(Render(before), Is.Not.EqualTo(Render(control)));
    }

    [Test]
    public void BorderBoxMinimumIncludesPaddingAndBorderWithoutMovingTextInside()
    {
        const string common = "<section style='background:blue;border:2pt solid red;" +
            "padding:5pt;min-height:100pt;BOX-SIZING'><p style='margin:0'>ITEM</p></section>" +
            "<p style='margin:0'>FOLLOWING</p>";
        var a = Layout(common.Replace("BOX-SIZING", "box-sizing:content-box"));
        var b = Layout(common.Replace("BOX-SIZING", "box-sizing:border-box"));
        Assert.That(a.Single().PaintBoxes.Single().Height, Is.EqualTo(114).Within(0.00001));
        Assert.That(b.Single().PaintBoxes.Single().Height, Is.EqualTo(100).Within(0.00001));
        Assert.That(a.Single().Runs[0].Baseline, Is.EqualTo(b.Single().Runs[0].Baseline));
        Assert.That(a.Single().Runs[1].Baseline - b.Single().Runs[1].Baseline,
            Is.EqualTo(-14).Within(0.00001));
    }

    [Test]
    public void UnpaintedEmptyPanelStillAdvancesTheRealFollowingTextCursor()
    {
        const string html = "<div style='min-height:60pt'></div><p style='margin:0'>AFTER</p>";
        const string oracle = "<div style='padding-top:60pt'></div><p style='margin:0'>AFTER</p>";
        Assert.That(Render(html), Is.EqualTo(Render(oracle)));
        Assert.That(Layout(html).Single().Runs.Single().Text, Is.EqualTo("AFTER"));
    }

    [Test]
    public void PrintUnitsAndFinalComputedFontUnitsEqualIndependentPointsOracle()
    {
        const string prefix = "<html style='font-size:10pt'><body><section style='font-size:16pt;" +
            "padding:4pt;background:blue;min-height:";
        const string suffix = "'><p style='margin:0'>MEASURED</p></section><p>FOLLOW</p></body></html>";
        var expected = Render(prefix + "32pt" + suffix);
        Assert.That(Render(prefix + "2em" + suffix), Is.EqualTo(expected));
        Assert.That(Render(prefix + "3.2rem" + suffix), Is.EqualTo(expected));
        var physical = Render(prefix + "72pt" + suffix);
        Assert.That(Render(prefix + "1in" + suffix), Is.EqualTo(physical));
        Assert.That(Render(prefix + "96px" + suffix), Is.EqualTo(physical));
    }

    [Test]
    public void StylesheetImportantAndWideValuesApplyPerPanelWithNonInheritanceDefault()
    {
        const string actual = "<style>section.card{min-height:70pt!important}" +
            ".card{min-height:40pt}</style><section class=card style='min-height:20pt;" +
            "background:blue'><p style='margin:0'>A</p><div style='background:red;" +
            "min-height:inherit'>B</div><article style='background:red;min-height:unset'>" +
            "C</article></section>";
        var root = (BlockNode)HtmlDocumentReader.ReadTree(actual, Page, default).Children.Single();
        Assert.That(root.BoxStyle.MinHeight!.Value.Value, Is.EqualTo(70));
        var children = root.Children.OfType<BlockNode>().ToArray();
        Assert.That(children, Has.Length.EqualTo(3));
        Assert.That(children[1].BoxStyle.MinHeight!.Value.Value, Is.EqualTo(70));
        Assert.That(children[2].BoxStyle.MinHeight, Is.Null);
        const string oracle = "<section style='min-height:70pt;background:blue'>" +
            "<p style='margin:0'>A</p><div style='background:red;min-height:70pt'>B</div>" +
            "<article style='background:red'>C</article></section>";
        Assert.That(Render(actual), Is.EqualTo(Render(oracle)));
    }

    [Test]
    public void MinimumHeightMovesWholePanelToFreshPageWithoutMovingEarlierText()
    {
        var options = Page with { PageHeight = 180 };
        const string html = "<p style='margin:0'>BEFORE</p><section style='min-height:130pt;" +
            "background:blue'><p style='margin:0'>AFTER</p></section>";
        var pages = Layout(html, options);
        Assert.That(pages, Has.Count.EqualTo(2));
        Assert.That(pages[0].Runs.Single().Text, Is.EqualTo("BEFORE"));
        Assert.That(pages[1].Runs.Single().Text, Is.EqualTo("AFTER"));
        Assert.That(pages[1].PaintBoxes.Single().Top, Is.EqualTo(20).Within(0.00001));
        Assert.That(pages[1].PaintBoxes.Single().Height, Is.EqualTo(130).Within(0.00001));
        Assert.That(Encoding.ASCII.GetString(Render(html, options)), Does.Contain("/Count 2"));
    }

    [Test]
    public void ExplicitPageBreakPrecedesUnpaintedMinimumAndKeepsReservedHeight()
    {
        const string before = "<p style='margin:0;break-after:page'>A</p>";
        const string following = "<p style='margin:0'>B</p>";
        var baseline = Layout(before + following);
        var unpaintedHtml = before + "<section style='min-height:60pt'></section>" + following;
        var paintedHtml = before +
            "<section style='min-height:60pt;background:blue'></section>" + following;
        var unpainted = Layout(unpaintedHtml);
        var painted = Layout(paintedHtml);

        Assert.That(baseline, Has.Count.EqualTo(2));
        Assert.That(unpainted, Has.Count.EqualTo(2));
        Assert.That(painted, Has.Count.EqualTo(2));
        Assert.That(unpainted[0].Runs.Select(run => run.Text), Is.EqualTo(new[] { "A" }));
        Assert.That(unpainted[1].Runs.Select(run => run.Text), Is.EqualTo(new[] { "B" }));
        Assert.That(unpainted[1].Runs.Single().Baseline,
            Is.EqualTo(baseline[1].Runs.Single().Baseline - 60).Within(0.00001),
            "The 60pt unpainted minimum belongs after the authored page break.");
        Assert.That(unpainted[1].Runs.Single().Baseline,
            Is.EqualTo(painted[1].Runs.Single().Baseline).Within(0.00001),
            "Paint must not determine whether minimum-height space survives a page break.");
        Assert.That(Encoding.ASCII.GetString(Render(unpaintedHtml)), Does.Contain("/Count 2"));
    }

    [Test]
    public void NestedEmptyMinimumDoesNotCountThePrecedingMarginTwice()
    {
        const string before = "<p style='margin:0 0 8pt'>A</p>";
        const string following = "<p style='margin:0'>B</p>";
        foreach (var paint in new[] { "", ";background:#edf5fa" })
        {
            var opening = "<section style='min-height:60pt" + paint + "'>";
            var direct = before + opening + "</section>" + following;
            var nested = before + opening +
                "<div style='min-height:60pt'></div></section>" + following;
            Assert.That(Layout(nested).Single().Runs.Last().Baseline,
                Is.EqualTo(Layout(direct).Single().Runs.Last().Baseline).Within(0.00001),
                "An equally tall inner unpainted minimum must not add a second 8pt margin.");
            Assert.That(Render(nested), Is.EqualTo(Render(direct)),
                "Nested empty minimum must preserve the actual PDF geometry and paint.");
        }
    }

    [Test]
    public void SatisfiedUnpaintedMinimumIsNotRestartedAfterAnInnerPageBreak()
    {
        const string unpainted = "<section style='min-height:60pt'>" +
            "<div style='min-height:60pt'></div>" +
            "<p style='margin:0;break-before:page'>B</p></section>" +
            "<p style='margin:0'>C</p>";
        var painted = unpainted.Replace("<section style='min-height:60pt'>",
            "<section style='min-height:60pt;background:blue'>");
        var actual = Layout(unpainted);
        var control = Layout(painted);
        Assert.That(actual, Has.Count.EqualTo(2));
        Assert.That(control, Has.Count.EqualTo(2));
        Assert.That(actual[1].Runs.Select(run => run.Text),
            Is.EqualTo(new[] { "B", "C" }));
        Assert.That(actual[1].Runs.Select(run => run.Baseline),
            Is.EqualTo(control[1].Runs.Select(run => run.Baseline))
                .Within(0.00001),
            "A previously satisfied unpainted minimum cannot reserve a second 60pt after the break.");
        Assert.That(Encoding.ASCII.GetString(Render(unpainted)), Does.Contain("/Count 2"));
    }

    [Test]
    public void OccupiedMinimumPreservesNestedUnpaintedWrapperPaddingAcrossPageBreak()
    {
        const string unpainted = "<section style='min-height:60pt'>" +
            "<div style='padding-top:5pt'><div style='min-height:60pt'></div>" +
            "<p style='margin:0;break-before:page'>B</p></div></section>" +
            "<p style='margin:0'>C</p>";
        var painted = unpainted.Replace("<section style='min-height:60pt'>",
            "<section style='min-height:60pt;background:blue'>");
        var actual = Layout(unpainted);
        var control = Layout(painted);
        Assert.That(actual, Has.Count.EqualTo(2));
        Assert.That(control, Has.Count.EqualTo(2));
        Assert.That(actual[0].Runs, Is.Empty);
        Assert.That(actual[1].Runs.Select(run => run.Text), Is.EqualTo(new[] { "B", "C" }));
        Assert.That(actual[1].Runs.Select(run => run.Baseline),
            Is.EqualTo(control[1].Runs.Select(run => run.Baseline)).Within(0.00001),
            "A consumed minimum and nested wrapper padding cannot restart on a continuation page.");
        Assert.That(Encoding.ASCII.GetString(Render(unpainted)), Does.Contain("/Count 2"));
    }

    [Test]
    public void ContentLargerThanMinimumRetainsNaturalFragmentedPages()
    {
        var options = Page with { PageHeight = 160 };
        var rows = string.Concat(Enumerable.Range(1, 45)
            .Select(i => $"<p style='margin:0'>Audit item {i:00}</p>"));
        var panel = "<section style='background:#edf5fa;border:1pt solid blue;" +
            "padding:3pt;MINIMUM'>" + rows + "</section>";
        var withMin = panel.Replace("MINIMUM", "min-height:70pt");
        var natural = panel.Replace("MINIMUM", "");
        Assert.That(Render(withMin, options), Is.EqualTo(Render(natural, options)),
            "Minimum height does not add phantom fragments to naturally tall reports.");
        var pages = Layout(withMin, options);
        Assert.That(pages, Has.Count.GreaterThan(2));
        Assert.That(pages.SelectMany(p => p.Runs).Count(), Is.EqualTo(45));
        Assert.That(pages.All(p => p.PaintBoxes.Count == 1), Is.True);
    }

    [TestCase("<section style='min-height:10%'>A</section>", "FPDF1202")]
    [TestCase("<section style='min-height:auto'>A</section>", "FPDF1202")]
    [TestCase("<section style='min-height:-1pt'>A</section>", "FPDF1202")]
    [TestCase("<section style='min-height:calc(20pt + 5pt)'>A</section>", "FPDF1202")]
    [TestCase("<section style='min-height:15000pt'>A</section>", "FPDF1202")]
    [TestCase("<style>.never{min-height:15%}</style><section>A</section>", "FPDF1202")]
    [TestCase("<style>section{min-height:15%;min-height:0}</style><section>A</section>", "FPDF1202")]
    [TestCase("<p style='min-height:20pt'>A</p>", "FPDF1201")]
    [TestCase("<body style='min-height:20pt'>A</body>", "FPDF1201")]
    [TestCase("<span style='min-height:20pt'>A</span>", "FPDF1201")]
    [TestCase("<ul style='min-height:20pt'><li>A</li></ul>", "FPDF1201")]
    [TestCase("<ol><li><section style='min-height:20pt'>A</section></li></ol>", "FPDF1201")]
    [TestCase("<table style='min-height:20pt'><colgroup><col style='width:100pt'></colgroup><tbody><tr><td>A</td></tr></tbody></table>", "FPDF1201")]
    [TestCase("<section style='height:30%'>A</section>", "FPDF1202")]
    [TestCase("<section style='max-height:30pt'>A</section>", "FPDF1201")]
    public void UnsupportedInputFailsBeforeReplacingExistingPdf(string html, string code)
        => Reject(html, code);

    [Test]
    public void UnplaceableMinimumRejectsWithoutChangingOutputOrCreatingBlankPages()
    {
        Reject("<section style='min-height:150pt;background:blue'>A</section>", "FPDF1302",
            Page with { PageHeight = 180, MaxPages = 1 });
    }

    [Test]
    public void MinimumHeightCountsTowardTheExistingCssDeclarationBudget()
    {
        Reject("<section style='min-height:20pt;background:blue'>A</section>", "FPDF1205",
            Page with { MaxCssDeclarations = 1 });
    }

    [Test]
    public void ManagedCliRendersExactlyTheSameMinimumHeightPdfAsTheApi()
    {
        const string html = "<section style='min-height:70pt;background:blue;" +
            "border:2pt solid red;padding:4pt'><p>Inspection</p></section>";
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(html));
        using var output = new MemoryStream();
        using var diagnostics = new StringWriter();
        Assert.That(CliApplication.Run(["-", "-"], input, output, diagnostics), Is.Zero, diagnostics.ToString());
        Assert.That(output.ToArray(), Is.EqualTo(Render(html, new PdfOptions())),
            "The installed CLI and typed converter use the same source rendering path.");
    }
}
