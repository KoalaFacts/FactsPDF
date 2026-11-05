using System.Globalization;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>Inherited report text-flow declarations must actually change PDF glyph wrapping and coordinates.</summary>
[TestFixture]
public sealed class InheritedReportTextFlowTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 240, PageHeight = 180, Margin = 16 };

    private static byte[] Pdf(string html, PdfOptions? options = null)
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

    private static void RejectWithoutChangingDestination(string html, string code, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        output.Write("existing-report"u8);
        output.Position = 4;
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, options ?? Page));
        Assert.That(error!.Code, Is.EqualTo(code));
        Assert.That(output.ToArray(), Is.EqualTo("existing-report"u8.ToArray()));
        Assert.That(output.Position, Is.EqualTo(4));
        Assert.That(Pdf("<p>Valid after failure</p>"), Is.Not.Empty);
    }

    [TestCase("overflow-wrap", "break-word")]
    [TestCase("word-break", "break-all")]
    [TestCase("text-indent", "12pt")]
    public void ScopedPanelDeclarationEqualsExplicitChildStylesInRealPdf(string property, string value)
    {
        var rule = property + ":" + value;
        const string first = "PAID REFERENCE INVOICE";
        const string second = "NEXT MONTHLY INSPECTION";
        var actual = "<section style='" + rule + "'><p style='width:90pt'>" + first +
            "</p><article><h3 style='width:110pt'>" + second + "</h3></article></section>";
        var oracle = "<section><p style='width:90pt;" + rule + "'>" + first +
            "</p><article><h3 style='width:110pt;" + rule + "'>" + second + "</h3></article></section>";
        Assert.That(Pdf(actual), Is.EqualTo(Pdf(oracle)),
            "Inherited computed CSS must generate the same searchable, positioned PDF as explicit child CSS.");
        Assert.That(Layout(actual).SelectMany(p => p.Runs).Select(x => x.Text),
            Is.EqualTo(Layout(oracle).SelectMany(p => p.Runs).Select(x => x.Text)));
    }

    [TestCase("html")]
    [TestCase("body")]
    public void DocumentRootStyleFlowsThroughNestedOrdinaryReportContainers(string target)
    {
        const string css = "overflow-wrap:break-word;word-break:break-all;text-indent:8pt";
        const string inner = "<section><p style='width:65pt'>PAID REFERENCE2026</p>" +
            "<blockquote><h4 style='width:90pt'>OPEN INSPECTION2026</h4></blockquote></section>";
        var actual = "<html" + (target == "html" ? " style='" + css + "'" : "") +
            "><body" + (target == "body" ? " style='" + css + "'" : "") +
            ">" + inner + "</body></html>";
        const string explicitInner = "<section><p style='width:65pt;" + css + "'>PAID REFERENCE2026</p>" +
            "<blockquote><h4 style='width:90pt;" + css + "'>OPEN INSPECTION2026</h4></blockquote></section>";
        Assert.That(Pdf(actual), Is.EqualTo(Pdf("<html><body>" + explicitInner + "</body></html>")));
    }

    [Test]
    public void ChildResetWideKeywordsAndImportantWinnerProduceTheExpectedComputedStyles()
    {
        const string html = "<style>.flow{word-break:break-all!important;" +
            "overflow-wrap:break-word!important;text-indent:9pt!important}</style>" +
            "<div class='flow' style='word-break:normal;overflow-wrap:normal;text-indent:0'>" +
            "<p>FIRST</p>" +
            "<section style='word-break:initial;overflow-wrap:initial;text-indent:initial'><p>SECOND</p></section>" +
            "<article style='word-break:unset;overflow-wrap:unset;text-indent:unset'><p>THIRD</p></article>" +
            "<blockquote style='word-break:inherit;overflow-wrap:inherit;text-indent:inherit'><h3>FOURTH</h3></blockquote>" +
            "</div><p>OUTSIDE</p>";
        var styles = HtmlDocumentReader.Read(html, Page, default).Select(x => x.Style).ToArray();
        Assert.That(styles.Select(s => (s.BreakAll, s.BreakWords, s.FirstLineIndent.Value)),
            Is.EqualTo(new[] { (true, true, 9d), (false, false, 0d),
                (true, true, 9d), (true, true, 9d), (false, false, 0d) }));
    }

    [Test]
    public void TextIndentPercentKeepsChildContainingWidthAndEmUsesHostComputedFont()
    {
        const string content = "Inspection notes with a short first line";
        var actual = "<section style='font-size:10pt;text-indent:1em'><p style='width:100pt'>" +
            content + "</p></section>";
        var oracle = "<section style='font-size:10pt'><p style='width:100pt;text-indent:10pt'>" +
            content + "</p></section>";
        Assert.That(Pdf(actual), Is.EqualTo(Pdf(oracle)));
        const string child = "<p style='width:100pt'>Inspection notes</p>";
        Assert.That(Pdf("<section style='text-indent:20%'>" + child + "</section>"),
            Is.EqualTo(Pdf("<section><p style='width:100pt;text-indent:20%'>Inspection notes</p></section>")));
        var positions = Layout("<section style='text-indent:20%'>" + child + "</section>")
            .SelectMany(p => p.Runs).ToArray();
        Assert.That(positions[0].X, Is.EqualTo(36).Within(0.001));
    }

    [Test]
    public void InheritedBreakingMethodsHaveDistinctRealLineBehaviours()
    {
        const string content = "PAID EXAMPLE";
        var breakAll = Layout("<div style='word-break:break-all'><p style='width:60pt'>" +
            content + "</p></div>").SelectMany(p => p.Runs).ToArray();
        var normal = Layout("<div style='word-break:normal'><p style='width:60pt'>" +
            content + "</p></div>").SelectMany(p => p.Runs).ToArray();
        Assert.That(breakAll.Select(x => x.Text), Is.EqualTo(new[] { "PAID EXA", "MPLE" }));
        Assert.That(normal.Select(x => x.Text), Is.EqualTo(new[] { "PAID", "EXAMPLE" }));
        var wrapped = Layout("<div style='overflow-wrap:break-word'><p style='width:60pt'>" +
            "ABCDEFGHIJKLMNOPQRSTUVWXYZ</p></div>").SelectMany(p => p.Runs).ToArray();
        Assert.That(wrapped.Length, Is.GreaterThan(1));
        Assert.That(string.Concat(wrapped.Select(x => x.Text)), Is.EqualTo("ABCDEFGHIJKLMNOPQRSTUVWXYZ"));
    }

    [Test]
    public void FortyEightRowReportDoesNotLoseCharactersOrChangePaginationAgainstExplicitOracle()
    {
        var records = Enumerable.Range(1, 48).Select(i =>
            "REF-" + i.ToString("00", CultureInfo.InvariantCulture) + "-LONGREFERENCE2026");
        var rows = string.Concat(records.Select(v => "<p style='width:90pt'>" + v + "</p>"));
        var oracleRows = string.Concat(records.Select(v =>
            "<p style='width:90pt;word-break:break-all;text-indent:6pt'>" + v + "</p>"));
        var actual = "<div style='word-break:break-all;text-indent:6pt'>" + rows + "</div>";
        var oracle = "<div>" + oracleRows + "</div>";
        var pages = Layout(actual);
        Assert.That(pages.Count, Is.GreaterThan(2));
        Assert.That(Pdf(actual), Is.EqualTo(Pdf(oracle)));
        Assert.That(string.Concat(pages.SelectMany(p => p.Runs).Select(x => x.Text)).Replace(" ", ""),
            Does.Contain("REF-48-LONGREFERENCE2026"));
    }

    [TestCase("left", 12d)]
    [TestCase("center", 6d)]
    [TestCase("right", 0d)]
    public void InheritedTableCellIndentAlignsTheActualFirstLineWithoutMovingContinuation(
        string alignment, double firstLineShift)
    {
        var table = "<table style='width:100pt'><colgroup><col style='width:100pt'></colgroup>" +
            "<tbody><tr><td><p style='text-align:" + alignment +
            "'>FIRST<br>SECOND<br>THIRD</p></td></tr></tbody></table>";
        var inherited = "<section style='text-indent:12pt'>" + table + "</section>";
        var unindented = "<section>" + table + "</section>";
        var placed = Layout(inherited).SelectMany(page => page.Runs).ToArray();
        var baseline = Layout(unindented).SelectMany(page => page.Runs).ToArray();

        Assert.That(placed.Select(run => run.Text),
            Is.EqualTo(new[] { "FIRST", "SECOND", "THIRD" }));
        Assert.That(placed[0].X - baseline[0].X,
            Is.EqualTo(firstLineShift).Within(0.001),
            "An inherited indent must change the printed first-line X coordinate, not just wrapping.");
        for (var i = 1; i < placed.Length; i++)
            Assert.That(placed[i].X, Is.EqualTo(baseline[i].X).Within(0.001),
                "Explicit breaks must not inherit a second indentation.");
        Assert.That(placed.Select(run => run.Baseline),
            Is.EqualTo(baseline.Select(run => run.Baseline)).Within(0.001));
        Assert.That(Pdf(inherited), Is.Not.Empty);
        if (alignment != "right")
            Assert.That(Pdf(inherited), Is.Not.EqualTo(Pdf(unindented)),
                "Correct coordinates must survive actual PDF drawing.");
    }

    [Test]
    public void InheritedTableIndentWrapsLongCellTextAndProtectsUnfitOutput()
    {
        const string content = "LONGREFERENCEINVOICENUMBER2026";
        const string table = "<table style='width:75pt'><colgroup><col style='width:75pt'></colgroup>" +
            "<tbody><tr><td><p style='word-break:break-all'>" + content +
            "</p></td></tr></tbody></table>";
        var actual = "<div style='text-indent:9pt'>" + table + "</div>";
        var lines = Layout(actual).SelectMany(page => page.Runs).ToArray();
        var noIndent = Layout("<div>" + table + "</div>").SelectMany(page => page.Runs).ToArray();
        Assert.That(lines.Length, Is.GreaterThan(1));
        Assert.That(lines[0].X - noIndent[0].X, Is.EqualTo(9).Within(0.001));
        Assert.That(string.Concat(lines.Select(line => line.Text)), Is.EqualTo(content));
        Assert.That(Pdf(actual), Is.Not.EqualTo(Pdf("<div>" + table + "</div>")));
        RejectWithoutChangingDestination("<section style='text-indent:120pt'>" + table +
            "</section>", "FPDF1302");
    }

    [Test]
    public void RootRemIndentUsesWinningHtmlFontAndMatchesAnExplicitPointOracle()
    {
        const string content = "<body><p>Alpha is the first invoice detail.</p></body>";
        var relative = "<html style='font-size:24pt;text-indent:1rem'>" + content + "</html>";
        var pointOracle = "<html style='font-size:24pt;text-indent:24pt'>" + content + "</html>";
        var bodyOracle = "<html style='font-size:24pt'><body style='text-indent:1rem'>" +
            "<p>Alpha is the first invoice detail.</p></body></html>";

        var runs = Layout(relative).SelectMany(page => page.Runs).ToArray();
        Assert.That(runs[0].X, Is.EqualTo(Page.Margin + 24).Within(0.001),
            "Root rem must resolve against the final 24pt html font, not the initial 12pt.");
        Assert.That(Pdf(relative), Is.EqualTo(Pdf(pointOracle)));
        Assert.That(Pdf(relative), Is.EqualTo(Pdf(bodyOracle)));
    }

    [TestCase("outside")]
    [TestCase("inside")]
    public void InheritedIndentDoesNotMakeGeneratedOrderedListMarkersUnfit(string position)
    {
        var list = "<ol style='list-style-position:" + position +
            "'><li>Alpha</li><li>Beta</li></ol>";
        var inherited = "<div style='text-indent:6pt'>" + list + "</div>";
        var normal = "<div>" + list + "</div>";
        var runs = Layout(inherited).SelectMany(page => page.Runs).ToArray();
        var labels = runs.Where(run => run.Text is "1." or "2.").ToArray();
        var baseline = Layout(normal).SelectMany(page => page.Runs).ToArray();

        Assert.That(labels.Select(run => run.Text), Is.EqualTo(new[] { "1.", "2." }));
        Assert.That(labels.Select(run => run.Width),
            Is.EqualTo(baseline.Where(run => run.Text is "1." or "2.")
                .Select(run => run.Width)).Within(0.001));
        Assert.That(runs.Select(run => run.Text), Does.Contain("Alpha"));
        Assert.That(runs.Select(run => run.Text), Does.Contain("Beta"));
        Assert.That(Pdf(inherited), Is.Not.Empty,
            "Synthetic list marker glyphs must not inherit the paragraph's first-line indent.");
        Assert.That(Pdf(normal), Is.Not.Empty,
            "The original no-indent list layout must remain valid.");
    }

    [TestCase("<span style='overflow-wrap:break-word'>A</span>", "FPDF1201")]
    [TestCase("<ul style='word-break:break-all'><li>A</li></ul>", "FPDF1201")]
    [TestCase("<table style='text-indent:10pt'><colgroup><col style='width:80pt'></colgroup><tbody><tr><td>A</td></tr></tbody></table>", "FPDF1201")]
    [TestCase("<div style='overflow-wrap:anywhere'><p>A</p></div>", "FPDF1202")]
    [TestCase("<section style='word-break:keep-all'><p>A</p></section>", "FPDF1202")]
    [TestCase("<article style='text-indent:-2pt'><p>A</p></article>", "FPDF1202")]
    [TestCase("<div style='text-indent:500pt'><p>LONG</p></div>", "FPDF1302")]
    [TestCase("<section style='white-space:nowrap;overflow-wrap:break-word'><p style='width:40pt'>ABCDEFGHIJKLMN</p></section>", "FPDF1302")]
    public void UnsupportedTargetsValuesAndUnfitGeometryKeepExistingPdfAndStreamPosition(string html, string code)
        => RejectWithoutChangingDestination(html, code);
}
