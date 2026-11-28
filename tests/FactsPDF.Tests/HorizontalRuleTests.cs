using NUnit.Framework;

namespace FactsPDF.Tests;

[TestFixture]
public sealed class HorizontalRuleTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 240, Margin = 20 };
    // The user-agent sheet gives body an 8px margin and hr four 1px borders; tests that size or place one
    // authored border measure without them. A document that starts with its own html element opts out.
    private const string Plain = "<style>html,body{margin:0}hr{border:0}</style>";
    private static string Neutral(string html)
        => html.StartsWith("<html", StringComparison.Ordinal) || html.StartsWith(Margin0, StringComparison.Ordinal) ? html : Plain + html;
    // Only the body margin zeroed, for the test that measures the default rule itself.
    private const string Margin0 = "<style>html,body{margin:0}</style>";

    private static byte[] Render(string html, PdfOptions? options = null)
    {
        using var stream = new MemoryStream();
        PdfConverter.Convert(Neutral(html), stream, options ?? Page);
        return stream.ToArray();
    }
    private static List<LayoutPage> Layout(string html, PdfOptions? options = null)
    {
        var settings = options ?? Page;
        return TextLayout.Layout(HtmlDocumentReader.ReadTree(Neutral(html), settings, default), settings, default);
    }
    private static void Reject(string html, string code, PdfOptions? options = null)
    {
        using var stream = new MemoryStream();
        stream.Write("existing-pdf"u8);
        stream.Position = 4;
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, stream, options ?? Page));
        Assert.That(error!.Code, Is.EqualTo(code));
        Assert.That(stream.ToArray(), Is.EqualTo("existing-pdf"u8.ToArray()));
        Assert.That(stream.Position, Is.EqualTo(4));
    }

    [Test]
    public void DefaultRulePaintsOnePixelBordersAndHalfEmGaps()
    {
        // Chrome's hr: 0.5em (6pt) margins and a 1px border on every side, light top/left and lighter
        // bottom/right, so the rule is 2px (1.5pt) tall.
        const string actual = "<p style='margin:0'>BEFORE</p><hr><p style='margin:0'>AFTER</p>";
        const string oracle = "<p style='margin:0'>BEFORE</p>" +
            "<div style='margin:6pt 0;border:1px solid;border-color:#9a9a9a #eee #eee #9a9a9a'></div>" +
            "<p style='margin:0'>AFTER</p>";
        var page = Layout(Margin0 + actual).Single();
        Assert.That(Render(Margin0 + actual), Is.EqualTo(Render(Margin0 + oracle)));
        var box = page.PaintBoxes.Single();
        Assert.That(box.Width, Is.EqualTo(260).Within(0.000001));
        Assert.That(box.Height, Is.EqualTo(1.5).Within(0.000001));
        Assert.That(page.Runs.Select(r => r.Text), Is.EquivalentTo(new[] { "BEFORE", "AFTER" }));
    }

    [Test]
    public void ImportantCascadeAndAutocentredWidthMatchExistingPaintedBlock()
    {
        const string rules = "<style>hr.sep{border-top:2pt solid #245587!important;" +
            "width:120pt;margin:9pt auto 4pt;padding:2pt}</style>";
        const string actual = rules + "<p style='margin:0'>A</p>" +
            "<hr class='sep' style='border-top:1pt solid red'><p style='margin:0'>B</p>";
        const string oracle = "<p style='margin:0'>A</p>" +
            "<div style='width:120pt;margin:9pt auto 4pt;padding:2pt;" +
            "border-top:2pt solid #245587'></div><p style='margin:0'>B</p>";
        Assert.That(Render(actual), Is.EqualTo(Render(oracle)));
        var box = Layout(actual).Single().PaintBoxes.Single();
        Assert.That(box.X, Is.EqualTo(88).Within(0.000001));
        Assert.That(box.Width, Is.EqualTo(124).Within(0.000001));
        Assert.That(box.Height, Is.EqualTo(6).Within(0.000001));
    }

    [Test]
    public void RootRemAndOwnFinalEmScaleVisibleBoxGeometry()
    {
        const string open = "<html style='font-size:10pt'><body style='font-size:15pt'>";
        const string tail = "<p style='margin:0'>FOLLOWING</p></body></html>";
        var actual = open + "<hr style='font-size:20pt;width:6em;" +
            "margin:1rem auto 0.5em;border:0;border-top:2pt solid blue'>" + tail;
        var oracle = open + "<div style='width:120pt;" +
            "margin:10pt auto 10pt;border-top:2pt solid blue'></div>" + tail;
        Assert.That(Render(actual), Is.EqualTo(Render(oracle)));
    }

    [Test]
    public void RuleMovesIntactToNewPageWithRealSearchableReportText()
    {
        var options = Page with { PageHeight = 150 };
        var lines = string.Join("<br>", Enumerable.Range(1, 7).Select(n => $"ROW-{n:00}"));
        var html = "<p style='margin:0'>" + lines + "</p>" +
            "<hr style='margin:15pt 0 3pt;border-top:2pt solid blue'>" +
            "<p style='margin:0'>AFTER-RULE</p>";
        var pages = Layout(html, options);
        Assert.That(pages, Has.Count.EqualTo(2));
        Assert.That(pages[0].PaintBoxes, Is.Empty);
        Assert.That(pages[1].PaintBoxes, Has.Count.EqualTo(1));
        // The rule's 15pt top margin adjoins the unforced page break and is truncated.
        Assert.That(pages[1].PaintBoxes[0].Top, Is.EqualTo(20).Within(0.000001));
        Assert.That(pages[1].PaintBoxes[0].Height, Is.EqualTo(2).Within(0.000001));
        Assert.That(pages.SelectMany(p => p.Runs).Any(r => r.Text == "AFTER-RULE"), Is.True);
        Assert.That(Render(html, options), Has.Length.GreaterThan(100));
    }

    [Test]
    public void NewPageThatCannotContainSeparatorFailsWithoutReplacingPdf()
        => Reject("<hr style='margin-top:30pt;border-top:5pt solid red'>",
            "FPDF1302", Page with { PageHeight = 72, Margin = 20, MaxPages = 2 });

    [Test]
    public void InheritedPanelMarginAndCurrentColourMatchExplicitFixedOracle()
    {
        // hr has its own gray colour in the UA sheet, so inherit the panel colour for currentcolor to be blue.
        const string actual = "<section style='color:blue;margin-top:7pt'>" +
            "<hr style='color:inherit;margin-top:inherit;border:0 solid;border-top-width:1pt;border-top-color:currentcolor'></section>";
        const string oracle = "<section style='color:blue;margin-top:7pt'>" +
            "<div style='margin:7pt 0 6pt;border-top:1pt solid blue'></div></section>"; // bottom margin: UA 0.5em
        Assert.That(Render(actual), Is.EqualTo(Render(oracle)));
        Assert.That(Layout("<hr style='border-top:initial'>").Single().PaintBoxes, Is.Empty);
    }

    [Test]
    public void VoidClosingSlashDoesNotCreateASpuriousFrame()
    {
        const string a = "<p style='margin:0'>A</p><hr id='divider' /><p style='margin:0'>B</p>";
        const string b = "<p style='margin:0'>A</p><hr id='divider'><p style='margin:0'>B</p>";
        Assert.That(Render(a), Is.EqualTo(Render(b)));
        Assert.That(Layout(b).Single().Runs, Has.Count.EqualTo(2));
    }

    [TestCase("<p>text<hr></p>", "FPDF1101")]
    [TestCase("<ul><hr></ul>", "FPDF1101")]
    [TestCase("<div><span><hr></span></div>", "FPDF1101")]
    [TestCase("<hr></hr>", "FPDF1101")]
    [TestCase("<hr src='https://example.com/secret'>", "FPDF1103")]
    [TestCase("<hr style='margin-top:auto'>", "FPDF1202")]
    [TestCase("<hr style='margin-top:-5%'>", "FPDF1202")]
    [TestCase("<hr style='margin-top:-2pt'>", "FPDF1202")]
    [TestCase("<hr style='border-radius:8pt'>", "FPDF1201")]
    [TestCase("<hr style='text-align:center'>", "FPDF1201")]
    [TestCase("<hr style='width:100rex'>", "FPDF1202")]
    [TestCase("<style>hr{border-top:3pt double red}</style><hr>", "FPDF1202")]
    [TestCase("<style>hr{margin-top:-2pt;margin-top:0}</style><hr>", "FPDF1202")]
    public void UnsupportedValuesAndNestingPreserveTarget(string html, string code)
        => Reject(html, code);

    [Test]
    public void VoidRulesStillConsumeActualResourceBudgets()
    {
        Reject("<hr><hr>", "FPDF1401", Page with { MaxDisplayCommands = 1 });
        Reject("<hr><hr>", "FPDF1002", Page with { MaxElements = 1 });
    }
}
