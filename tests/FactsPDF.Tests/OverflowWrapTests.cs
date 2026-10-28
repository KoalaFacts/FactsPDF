using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>Scoped whole-paragraph report identifiers can emergency-wrap without clipping.</summary>
[TestFixture]
public sealed class OverflowWrapTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 180, PageHeight = 220, Margin = 20 };

    private static List<PlacedText> Lines(string html, PdfOptions? page = null)
    {
        var options = page ?? Page;
        return TextLayout.Layout(HtmlDocumentReader.ReadTree(html, options, default), options, default)
            .SelectMany(p => p.Runs).ToList();
    }

    private static byte[] Pdf(string html, PdfOptions? page = null)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, page ?? Page);
        return output.ToArray();
    }

    private static FactsPdfException Reject(string html, string code, PdfOptions? page = null)
    {
        using var output = new MemoryStream();
        output.Write("existing output"u8);
        output.Position = 4;
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, page ?? Page));
        Assert.That(error!.Code, Is.EqualTo(code));
        Assert.That(output.ToArray(), Is.EqualTo("existing output"u8.ToArray()));
        Assert.That(output.Position, Is.EqualTo(4));
        return error;
    }

    [Test]
    public void LongIdentifierSplitsIntoMeasuredEightGlyphLinesWithoutDroppingText()
    {
        const string code = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        var lines = Lines("<p style='width:60pt;overflow-wrap:break-word'>" + code + "</p>");
        Assert.That(lines.Select(run => run.Text),
            Is.EqualTo(new[] { "ABCDEFGH", "IJKLMNOP", "QRSTUVWX", "YZ" }));
        Assert.That(string.Concat(lines.Select(run => run.Text)), Is.EqualTo(code));
        Assert.That(lines.All(run => run.Width <= 60.000001), Is.True);
        Assert.That(lines.Select(run => run.Baseline).Distinct().Count(), Is.EqualTo(4));
        Assert.That(Pdf("<p style='width:60pt;overflow-wrap:break-word'>" + code + "</p>"), Is.Not.Empty);
    }

    [Test]
    public void NarrowFixedTableCellParagraphAlsoBreaksLongIdentifiersWithoutDroppingGlyphs()
    {
        // Only the p declaration opts in; td/table CSS sizing remains the
        // existing fixed-column contract, not general intrinsic table layout.
        const string html = "<table style='width:60pt'><colgroup><col style='width:60pt'></colgroup>" +
            "<tbody><tr><td><p style='overflow-wrap:break-word'>ABCDEFGHIJKLMNOPQRSTUVWXYZ</p></td></tr></tbody></table>";
        var page = Page with { PageWidth = 220, PageHeight = 400 };
        var lines = Lines(html, page);
        Assert.That(lines.Count, Is.GreaterThan(1));
        Assert.That(string.Concat(lines.Select(run => run.Text)), Does.Contain("ABCDEFGHIJKLMNOPQRSTUVWXYZ"));
        Assert.That(Pdf(html, page), Is.Not.Empty);
    }

    [Test]
    public void DefaultAndFittingTextRetainLegacyPdfBytes()
    {
        const string text = "Paid in full";
        Assert.That(Pdf("<p style='overflow-wrap:break-word'>" + text + "</p>"),
            Is.EqualTo(Pdf("<p style='overflow-wrap:normal'>" + text + "</p>")));
        Assert.That(Pdf("<p style='overflow-wrap:normal'>" + text + "</p>"),
            Is.EqualTo(Pdf("<p>" + text + "</p>")));
        Assert.That(Pdf("<p style='width:60pt;overflow-wrap:break-word'>PAID IN FULL</p>"),
            Is.EqualTo(Pdf("<p style='width:60pt'>PAID IN FULL</p>")));
    }

    [Test]
    public void WordThatFitsNextLineMovesWholeRatherThanBreakingMidWord()
        => Assert.That(Lines("<p style='width:60pt;overflow-wrap:break-word'>PAID EXAMPLE</p>")
                .Select(run => run.Text), Is.EqualTo(new[] { "PAID", "EXAMPLE" }));

    [Test]
    public void FirstLineIndentReducesOnlyTheFirstLineCapacity()
    {
        var lines = Lines("<p style='width:60pt;text-indent:24pt;overflow-wrap:break-word'>ABCDEFGHIJKLMNO</p>");
        Assert.That(lines.Select(run => run.Text), Is.EqualTo(new[] { "ABCDE", "FGHIJKLM", "NO" }));
        Assert.That(lines[0].X - lines[1].X, Is.EqualTo(24).Within(0.001));
        Reject("<p style='width:20pt;text-indent:19pt;overflow-wrap:break-word'>A</p>", "FPDF1302");
    }

    [Test]
    public void ExplicitTrueTypeFontIsEmbeddedAndEveryGlyphSurvivesEmergencyWrapping()
    {
        var font = PdfFont.LoadTrueType(FontFixture.Create());
        var options = Page with { Fonts = [font], PageHeight = 100, Margin = 10 };
        const string text = "ABWABWABWABWABW";
        const string html = "<p style='width:40pt;overflow-wrap:break-word'>" + text + "</p>";
        var laidOut = TextLayout.Layout(HtmlDocumentReader.ReadTree(html, options, default), options, default);
        var placed = laidOut.SelectMany(page => page.Runs).ToList();
        Assert.That(placed.Count, Is.GreaterThan(1));
        Assert.That(string.Concat(placed.Select(x => x.Text)), Is.EqualTo(text));
        Assert.That(placed.All(x => x.Font is not null && x.Width <= 40.000001), Is.True);
        Assert.That(System.Text.Encoding.Latin1.GetString(Pdf(html, options)), Does.Contain("/FontFile2"));
        // A supplied font is not bypassed merely because an emergency break
        // could make an otherwise-unsupported character fit the line.
        Reject("<p style='width:40pt;overflow-wrap:break-word'>ABWC</p>",
            "FPDF1504", options);
    }

    [Test]
    public void ExplicitBreaksAndNoWrapKeepTheirExistingSemantics()
    {
        Assert.That(Lines("<p style='width:60pt;overflow-wrap:break-word'>ABCDEFGH<br>IJKLMNOP</p>")
            .Select(x => x.Text), Is.EqualTo(new[] { "ABCDEFGH", "IJKLMNOP" }));
        Reject("<p style='width:60pt;white-space:nowrap;overflow-wrap:break-word'>ABCDEFGHIJ</p>", "FPDF1302");
    }

    [Test]
    public void RealMultipageReportMatchesIndependentlyAuthoredExplicitBreakOracle()
    {
        var report = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "examples", "report-overflow-wrap.html"));
        var oracle = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "examples", "report-overflow-wrap-oracle.html"));
        Assert.That(Pdf(report), Is.EqualTo(Pdf(oracle)));
        var options = new PdfOptions();
        var pages = TextLayout.Layout(HtmlDocumentReader.ReadTree(report, options, default), options, default);
        Assert.That(pages.Count, Is.GreaterThan(1));
        var rendered = string.Concat(pages.SelectMany(page => page.Runs).Select(run => run.Text));
        Assert.That(rendered, Does.Contain("INV-01-"));
        Assert.That(rendered, Does.Contain("INV-32-"));
    }

    [Test]
    public void ImportantAndWideKeywordsResolveOnTheWholeParagraph()
    {
        var items = HtmlDocumentReader.Read(
            "<style>p{overflow-wrap:break-word!important}.normal{overflow-wrap:normal}" +
            ".initial{overflow-wrap:initial!important}.unset{overflow-wrap:unset!important}" +
            "</style><p class='normal'>A</p><p class='initial'>B</p>" +
            "<p class='unset'>C</p><h1 style='overflow-wrap:inherit'>D</h1>", Page, default);
        Assert.That(items.Select(p => p.Style.BreakWords),
            Is.EqualTo(new[] { true, false, false, false }));
    }

    [TestCase("<span style='overflow-wrap:break-word'>A</span>", "FPDF1201")]
    [TestCase("<div style='overflow-wrap:break-word'><p>A</p></div>", "FPDF1201")]
    [TestCase("<p style='overflow-wrap:anywhere'>A</p>", "FPDF1202")]
    [TestCase("<p style='overflow-wrap:break-all'>A</p>", "FPDF1202")]
    [TestCase("<style>#absent{overflow-wrap:anywhere}</style><p>A</p>", "FPDF1202")]
    [TestCase("<p style='overflow-wrap:break-word normal'>A</p>", "FPDF1202")]
    [TestCase("<p style='overflow-wrap:calc(1)'>A</p>", "FPDF1202")]
    public void UnsupportedContextsAndValuesFailExplicitly(string html, string code)
        => Reject(html, code);

    [Test]
    public void IndividualGlyphWiderThanColumnCannotBeEmergencyWrapped()
        => Reject("<p style='font-size:100pt;width:20pt;overflow-wrap:break-word'>W</p>", "FPDF1302");

    [Test]
    public void MissingFontFailsBeforeAnyEmergencyWidthRejection()
        => Reject("<p style='width:20pt;overflow-wrap:break-word'>ABCDE 中文</p>", "FPDF1301");
}
