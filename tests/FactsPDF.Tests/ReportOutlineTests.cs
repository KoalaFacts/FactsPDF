using System.Text;
using FactsPDF.CommandLine;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>Real paginated PDF outlines for bounded report panels, not parse-only CSS.</summary>
[TestFixture]
public sealed class ReportOutlineTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 160, Margin = 20 };

    private static byte[] Pdf(string html, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, options ?? Page);
        return output.ToArray();
    }

    private static List<LayoutPage> Layout(string html, PdfOptions? options = null)
    {
        var o = options ?? Page;
        return TextLayout.Layout(HtmlDocumentReader.ReadTree(html, o, default), o, default);
    }

    private static void Reject(string html, string code)
    {
        using var output = new MemoryStream();
        output.Write("original-report"u8);
        output.Position = 4;
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, Page));
        Assert.That(error!.Code, Is.EqualTo(code));
        Assert.That(output.ToArray(), Is.EqualTo("original-report"u8.ToArray()));
        Assert.That(output.Position, Is.EqualTo(4));
        Assert.That(Pdf("<p>Recovered AB</p>").Length, Is.GreaterThan(100));
    }

    [Test]
    public void OutlineShorthandAndLonghandsProduceIdenticalActualPdfBytes()
    {
        const string a = "<section style='width:160pt;padding:8pt;background:#edf4fa;" +
            "outline:2pt dotted #285d91;outline-offset:3pt'><p>AB</p></section>";
        const string b = "<section style='width:160pt;padding:8pt;background:#edf4fa;" +
            "outline-width:2pt;outline-style:dotted;outline-color:#285d91;" +
            "outline-offset:3pt'><p>AB</p></section>";
        Assert.That(Pdf(a), Is.EqualTo(Pdf(b)));
        var box = Layout(a).Single().PaintBoxes.Single();
        Assert.That(box.Style.Outline.EffectiveWidth, Is.EqualTo(2));
        Assert.That(box.Style.OutlineOffset, Is.EqualTo(3));
        var content = Encoding.Latin1.GetString(Pdf(a));
        Assert.That(content, Does.Contain("1 J [0.001 4] 0 d"));
        Assert.That(content, Does.Contain(" S Q"));
        Assert.That(content, Does.Contain(" Tj ET"));
    }

    [Test]
    public void OutlineDoesNotAlterBorderBoxTextCoordinatesOrPagination()
    {
        const string body = "<p style='margin:0'>AB</p><p style='break-before:page;margin:0'>AB</p>";
        var plain = "<section style='width:130pt;padding:4pt;border:1pt solid blue'>" + body + "</section>";
        var outlined = "<section style='width:130pt;padding:4pt;border:1pt solid blue;" +
            "outline:2pt dashed red;outline-offset:2pt'>" + body + "</section>";
        var p = Layout(plain);
        var o = Layout(outlined);
        Assert.That(o.Count, Is.EqualTo(2));
        Assert.That(o.SelectMany(x => x.Runs).Select(x => (x.X, x.Baseline, x.Text)),
            Is.EqualTo(p.SelectMany(x => x.Runs).Select(x => (x.X, x.Baseline, x.Text))));
        Assert.That(o.SelectMany(x => x.PaintBoxes).Select(x => (x.X, x.Width, x.Height)),
            Is.EqualTo(p.SelectMany(x => x.PaintBoxes).Select(x => (x.X, x.Width, x.Height))));
        Assert.That(o[0].PaintBoxes.Single().IsFirstFragment, Is.True);
        Assert.That(o[0].PaintBoxes.Single().IsLastFragment, Is.False);
        Assert.That(o[1].PaintBoxes.Single().IsFirstFragment, Is.False);
        Assert.That(o[1].PaintBoxes.Single().IsLastFragment, Is.True);
        var bytes = Encoding.Latin1.GetString(Pdf(outlined));
        Assert.That(bytes, Does.Contain("[6 6] 0 d"));
    }

    [Test]
    public void OutlineInkDoesNotConsumeUnpaintedAncestorPaddingAtForcedPageBreak()
    {
        const string body = "<p style='margin:0'>A</p>" +
            "<div style='padding-top:10pt'><div style='padding-top:30pt'></div>" +
            "<p style='break-before:page;margin:0'>B</p></div>";
        var withOutline = body.Replace("padding-top:10pt'", "padding-top:10pt;outline:2pt solid red'",
            StringComparison.Ordinal);
        var original = Layout(body);
        var outlined = Layout(withOutline);
        Assert.That(original.Count, Is.GreaterThan(1));
        Assert.That(outlined.Count, Is.EqualTo(original.Count));
        Assert.That(outlined.SelectMany(p => p.Runs).Select(r => (r.Text, r.X, r.Baseline)),
            Is.EqualTo(original.SelectMany(p => p.Runs).Select(r => (r.Text, r.X, r.Baseline))));
    }

    [Test]
    public void OutlineOnlyEmptyPanelDoesNotTriggerLayoutPaintPreflightOrPhantomPage()
    {
        const string plain = "<p style='margin:0'>A</p>" +
            "<div style='padding-top:110pt'></div>" +
            "<p style='break-before:page;margin:0'>B</p>";
        var outlined = plain.Replace("padding-top:110pt'",
            "padding-top:110pt;outline:2pt solid red'", StringComparison.Ordinal);
        var a = Layout(plain);
        var b = Layout(outlined);
        Assert.That(a.Count, Is.EqualTo(2));
        Assert.That(b.Count, Is.EqualTo(a.Count));
        Assert.That(b.SelectMany(p => p.Runs).Select(r => (r.Text, r.X, r.Baseline)),
            Is.EqualTo(a.SelectMany(p => p.Runs).Select(r => (r.Text, r.X, r.Baseline))));
        var painted = b.SelectMany(p => p.PaintBoxes).Single(x =>
            x.Style.Outline.EffectiveWidth == 2);
        Assert.That(painted.Top + painted.Height + painted.Style.OutlineOffset +
            painted.Style.Outline.EffectiveWidth, Is.LessThan(Page.PageHeight));
        Assert.That(Pdf(outlined).Length, Is.GreaterThan(100));
        // An outline too tall even for the physical sheet must fail, not
        // silently clip or spill into the next page.
        Reject("<div style='padding-top:200pt;outline:2pt solid red'></div>", "FPDF1302");
    }

    [Test]
    public void SolidOutlineConnectsOuterCornersWithClosedMiterJoinedPdfPath()
    {
        const string single = "<section style='outline:6pt solid red'>AB</section>";
        var content = Encoding.Latin1.GetString(Pdf(single));
        Assert.That(content, Does.Contain("RG 6 w 0 j "));
        Assert.That(content, Does.Contain(" l h S Q"));
        var fragmented = "<section style='outline:6pt solid red'>" +
            "<p style='margin:0'>A</p><p style='break-before:page;margin:0'>B</p></section>";
        var pages = Layout(fragmented);
        Assert.That(pages.Count, Is.EqualTo(2));
        Assert.That(pages[0].PaintBoxes.Single().IsLastFragment, Is.False);
        Assert.That(pages[1].PaintBoxes.Single().IsFirstFragment, Is.False);
        var splitPdf = Encoding.Latin1.GetString(Pdf(fragmented));
        Assert.That(splitPdf, Does.Not.Contain(" l h S Q"));
        Assert.That(splitPdf, Does.Contain("RG 6 w 0 j "));
    }

    [Test]
    public void CssCascadeAndImportantCanOverrideOneOutlineComponent()
    {
        const string source = "<style>.report{outline-width:2pt!important;" +
            "outline-color:blue}</style><section class=report " +
            "style='outline:1pt solid red;outline-offset:1pt'>AB</section>";
        const string oracle = "<section style='outline:2pt solid red;outline-offset:1pt'>AB</section>";
        Assert.That(Pdf(source), Is.EqualTo(Pdf(oracle)));
        const string reset = "<section style='outline-color:red;outline:solid'>AB</section>";
        Assert.That(Pdf(reset), Is.EqualTo(Pdf("<section style='outline:medium solid currentcolor'>AB</section>")));
        Assert.That(Pdf("<section style='outline:2pt none red'>AB</section>"),
            Is.EqualTo(Pdf("<section>AB</section>")));
    }

    [Test]
    public void ExplicitInheritanceAndInitialResetAreResolvedPerOutlineSide()
    {
        const string source = "<section style='outline:2pt solid blue'>" +
            "<div style='outline-width:inherit;outline-style:inherit;outline-color:inherit'>AB</div>" +
            "<div style='outline:initial'>AB</div></section>";
        var root = (BlockNode)HtmlDocumentReader.ReadTree(source, Page, default).Children.Single();
        var children = root.Children.OfType<BlockNode>().ToArray();
        Assert.That(children[0].BoxStyle.Outline, Is.EqualTo(root.BoxStyle.Outline));
        Assert.That(children[1].BoxStyle.Outline.EffectiveWidth, Is.Zero);
    }

    [Test]
    public void FortyFourRecordPrintableReportMatchesIndependentPhysicalLonghandOracle()
    {
        var source = File.ReadAllText(Example("report-outlines.html"));
        var oracle = File.ReadAllText(Example("report-outlines-oracle.html"));
        var a4 = new PdfOptions();
        var layout = Layout(source, a4);
        Assert.That(layout.Count, Is.GreaterThan(1));
        Assert.That(layout.SelectMany(p => p.Runs).Count(r =>
            r.Text.StartsWith("Service record ", StringComparison.Ordinal)), Is.EqualTo(44));
        Assert.That(Pdf(source, a4), Is.EqualTo(Pdf(oracle, a4)));
        Assert.That(layout.SelectMany(p => p.PaintBoxes).Any(b =>
            b.Style.Outline.EffectiveWidth == 2), Is.True);
        // Actual page geometry must leave text clear of the *outside* outline:
        // without explicit clearance the last dotted stroke overlapped this
        // footer even though the engine correctly kept outline out of layout.
        var lastPanelPage = layout.FindLastIndex(p => p.PaintBoxes.Any(b =>
            b.Style.Outline.EffectiveWidth == 2));
        var footerPage = layout.FindIndex(p => p.Runs.Any(r =>
            r.Text.Contains("REPORT COMPLETE", StringComparison.Ordinal)));
        Assert.That(footerPage, Is.EqualTo(lastPanelPage));
        var lastPanel = layout[footerPage].PaintBoxes.Single(b =>
            b.Style.Outline.EffectiveWidth == 2);
        var footer = layout[footerPage].Runs.Single(r =>
            r.Text.Contains("REPORT COMPLETE", StringComparison.Ordinal));
        var bottomStrokeEdge = lastPanel.Top + lastPanel.Height +
            lastPanel.Style.OutlineOffset + lastPanel.Style.Outline.EffectiveWidth;
        var footerTop = a4.PageHeight - footer.Baseline - footer.Style.FontSize;
        Assert.That(footerTop, Is.GreaterThan(bottomStrokeEdge + 3));
    }

    [Test]
    public void TrueTypeAndManagedCliUseSamePaintWithSearchableText()
    {
        var fonts = new PdfOptions { Fonts = [PdfFont.LoadTrueType(FontFixture.Create())] };
        const string html = "<section style='outline:2pt dashed #285d91;outline-offset:1pt'>" +
            "<p>AB</p></section>";
        var ttf = Encoding.Latin1.GetString(Pdf(html, fonts));
        Assert.That(ttf, Does.Contain("/ToUnicode"));
        Assert.That(ttf, Does.Contain("[6 6] 0 d"));
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(html));
        using var output = new MemoryStream();
        using var diagnostics = new StringWriter();
        Assert.That(CliApplication.Run(["-", "-"], input, output, diagnostics), Is.Zero, diagnostics.ToString());
        Assert.That(output.ToArray(), Is.EqualTo(Pdf(html, new PdfOptions())));
    }

    [TestCase("<section style='outline:12pt solid red'>AB</section>", "FPDF1202")]
    [TestCase("<section style='outline-offset:20pt'>AB</section>", "FPDF1202")]
    [TestCase("<section style='outline-width:-1pt'>AB</section>", "FPDF1202")]
    [TestCase("<section style='outline-offset:5%'>AB</section>", "FPDF1202")]
    [TestCase("<section style='outline:2pt dashed dotted'>AB</section>", "FPDF1202")]
    [TestCase("<section style='outline:2pt 3pt solid'>AB</section>", "FPDF1202")]
    [TestCase("<section style='outline:2pt wavy red'>AB</section>", "FPDF1202")]
    [TestCase("<section style='border-radius:3pt;outline:2pt solid red'>AB</section>", "FPDF1201")]
    [TestCase("<p style='outline:2pt solid red'>AB</p>", "FPDF1201")]
    [TestCase("<table style='outline:2pt solid red'></table>", "FPDF1201")]
    [TestCase("<style>.unmatched{outline:2pt groove red}</style><section>AB</section>", "FPDF1202")]
    public void InvalidDeclarationsDoNotReplaceDestination(string html, string code)
        => Reject(html, code);

    [Test]
    public void OutlineUsesDisplayBudgetAndPreservesOriginalOutputOnFailure()
    {
        using var output = new MemoryStream();
        output.Write("original-report"u8);
        var badBudget = Page with { MaxDisplayCommands = 1 };
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(
            "<section style='outline:2pt solid red'>AB</section>", output, badBudget));
        Assert.That(error!.Code, Is.EqualTo("FPDF1401"));
        Assert.That(output.ToArray(), Is.EqualTo("original-report"u8.ToArray()));
    }

    private static string Example(string name)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var file = Path.Combine(directory.FullName, "examples", name);
            if (File.Exists(file)) return file;
            directory = directory.Parent;
        }
        throw new FileNotFoundException("Report outline example is missing.", name);
    }
}
