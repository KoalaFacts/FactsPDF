using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>Source-only, paragraph-scoped pre-wrap for literal spaced business notes.</summary>
[TestFixture]
public sealed class WhiteSpacePreWrapTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 180, PageHeight = 165, Margin = 15 };

    private static byte[] Pdf(string html, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, options ?? Page);
        return output.ToArray();
    }

    private static List<LayoutPage> Layout(string html, PdfOptions? options = null)
    {
        var selected = options ?? Page;
        return TextLayout.Layout(HtmlDocumentReader.ReadTree(html, selected, default), selected, default);
    }

    private static void Reject(string html, string code, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        output.Write("existing-pdf"u8);
        output.Position = 3;
        var error = Assert.Throws<FactsPdfException>(() =>
            PdfConverter.Convert(html, output, options ?? Page));
        Assert.That(error!.Code, Is.EqualTo(code));
        Assert.That(output.ToArray(), Is.EqualTo("existing-pdf"u8.ToArray()));
        Assert.That(output.Position, Is.EqualTo(3));
    }

    [Test]
    public void ConsecutiveLeadingAndTrailingSpacesBecomeActualPdfGlyphs()
    {
        const string html = "<p style='white-space:pre-wrap;margin:0'>AB   BA\n  WA </p>";
        var lines = Layout(html).SelectMany(page => page.Runs)
            .GroupBy(run => run.Baseline)
            .Select(group => string.Concat(group.Select(run => run.Text))).ToArray();
        Assert.That(lines, Is.EqualTo(new[] { "AB   BA", "  WA " }));
        var bytes = Encoding.Latin1.GetString(Pdf(html));
        Assert.That(bytes, Does.Contain("AB   BA"));
        Assert.That(bytes, Does.Contain("  WA "));
        Assert.That(Pdf(html), Is.Not.EqualTo(Pdf(
            "<p style='white-space:pre-line;margin:0'>AB   BA\n  WA </p>")));
    }

    [Test]
    public void WithoutExtraSpacesPreWrapMatchesIndependentlyAuthoredBreaks()
    {
        const string source = "<p style='white-space:pre-wrap'>AB\nWA\nBA</p>";
        const string manual = "<p>AB<br>WA<br>BA</p>";
        Assert.That(Pdf(source), Is.EqualTo(Pdf(manual)));
    }

    [Test]
    public void AuthoredSpacesRemainSearchableThroughSoftWrapAndMultiplePages()
    {
        var single = "AB   BA  WA ";
        var html = string.Concat(Enumerable.Range(0, 75).Select(i =>
            "<p style='width:87pt;white-space:pre-wrap;margin:0'>"
            + single + "AB</p>"));
        var placed = Layout(html);
        Assert.That(placed.Count, Is.GreaterThan(2));
        var texts = placed.SelectMany(page => page.Runs).Select(run => run.Text).ToArray();
        Assert.That(string.Concat(texts), Is.EqualTo(string.Concat(
            Enumerable.Repeat(single + "AB", 75))));
        Assert.That(Pdf(html).Length, Is.GreaterThan(1000));
    }

    [Test]
    public void PreWrapCascadesAndCssWideResetWithoutChangingNormalBytes()
    {
        const string source = "<style>p{white-space:pre-wrap!important}" +
            "p.reset{white-space:initial!important}p.unset{white-space:unset!important}" +
            "</style><p>AB  BA</p><p class='reset'>AB  BA</p><p class='unset'>AB  BA</p>";
        var paragraphs = HtmlDocumentReader.Read(source, Page, default);
        Assert.That(paragraphs.Select(p => p.Style.PreserveSpaces),
            Is.EqualTo(new[] { true, false, false }));
        Assert.That(paragraphs.Select(p => p.Style.PreserveLineBreaks),
            Is.EqualTo(new[] { true, false, false }));
        const string ordinary = "<p>AB  BA</p>";
        Assert.That(Pdf("<p style='white-space:normal'>AB  BA</p>"),
            Is.EqualTo(Pdf(ordinary)));
    }

    [Test]
    public void StyledSpanDoesNotLoseCrossRunSpacesOrCrLfPair()
    {
        const string source = "<p style='white-space:pre-wrap'>" +
            "<span style='color:blue'>AB  \r</span><span style='color:blue'>\n  WA</span></p>";
        var lines = Layout(source).SelectMany(page => page.Runs)
            .GroupBy(run => run.Baseline)
            .Select(group => string.Concat(group.Select(run => run.Text))).ToArray();
        Assert.That(lines, Is.EqualTo(new[] { "AB  ", "  WA" }));
    }

    [Test]
    public void PreservedSpacesOccupyRealTableCellContent()
    {
        const string table = "<table style='width:120pt'><colgroup>" +
            "<col style='width:120pt'></colgroup><tbody><tr><td>" +
            "<p style='white-space:pre-wrap;margin:0'>   </p>" +
            "<p style='margin:0'>BA</p></td></tr></tbody></table>";
        var runs = Layout(table).SelectMany(page => page.Runs).ToArray();
        Assert.That(runs.Select(run => run.Text), Is.EqualTo(new[] { "   ", "BA" }));
        Assert.That(runs[0].Baseline, Is.LessThan(runs[1].Baseline));
    }

    [Test]
    public void TrueTypeFontMeasuresAndRendersConsecutiveSpaces()
    {
        var font = PdfFont.LoadTrueType(FontFixture.Create());
        var options = Page with { Fonts = [font] };
        const string html = "<p style='white-space:pre-wrap'>AB   WA\n  BA</p>";
        var bytes = Pdf(html, options);
        Assert.That(Encoding.Latin1.GetString(bytes), Does.Contain("/FontFile2"));
        Assert.That(Layout(html, options).SelectMany(p => p.Runs)
            .Select(x => x.Text), Is.EqualTo(new[] { "AB   WA", "  BA" }));
        Reject("<p style='white-space:pre-wrap'>AB  C</p>", "FPDF1504", options);
    }

    [TestCase("<p style='white-space:pre-wrap'>AB\tBA</p>", "FPDF1305")]
    [TestCase("<p style='white-space:pre-wrap'>\t</p>", "FPDF1305")]
    [TestCase("<p style='white-space:pre-wrap;width:15pt'>AAAAAAAAAAAA</p>", "FPDF1302")]
    [TestCase("<p style='white-space:pre'>AB</p>", "FPDF1202")]
    [TestCase("<p style='white-space:break-spaces'>AB</p>", "FPDF1202")]
    [TestCase("<span style='white-space:pre-wrap'>AB</span>", "FPDF1201")]
    [TestCase("<div style='white-space:pre-wrap'><p>AB</p></div>", "FPDF1201")]
    [TestCase("<style>.unused{white-space:pre-wrap anywhere}</style><p>AB</p>", "FPDF1202")]
    public void UnsupportedInputsRejectAndPreservePreviousPdf(string html, string code)
        => Reject(html, code);
}
