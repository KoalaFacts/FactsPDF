using System.Text;
using FactsPDF.CommandLine;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>Bounded HTML pre and CSS pre: literal spaces and breaks with no soft wrapping.</summary>
[TestFixture]
public sealed class PreformattedReportTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 200, PageHeight = 154, Margin = 16 };

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
        output.Position = 4;
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, options ?? Page));
        Assert.That(error!.Code, Is.EqualTo(code));
        Assert.That(output.ToArray(), Is.EqualTo("existing-pdf"u8.ToArray()));
        Assert.That(output.Position, Is.EqualTo(4));
    }

    [Test]
    public void PreElementMatchesExplicitCssAndPreviousPreWrapWhenAllLinesFit()
    {
        const string body = "AB   BA\r\n  WA\nAB  ";
        const string element = "<pre>" + body + "</pre>";
        const string explicitPre = "<p style='white-space:pre'>" + body + "</p>";
        const string priorMode = "<p style='white-space:pre-wrap'>" + body + "</p>";
        Assert.That(Pdf(element), Is.EqualTo(Pdf(explicitPre)));
        Assert.That(Pdf(element), Is.EqualTo(Pdf(priorMode)));
        var lines = Layout(element).SelectMany(page => page.Runs).GroupBy(run => run.Baseline)
            .Select(group => string.Concat(group.Select(run => run.Text))).ToArray();
        Assert.That(lines, Is.EqualTo(new[] { "AB   BA", "  WA", "AB  " }));
        var style = HtmlDocumentReader.Read(element, Page, default).Single().Style;
        Assert.That((style.NoWrap, style.PreserveLineBreaks, style.PreserveSpaces),
            Is.EqualTo((true, true, true)));
    }

    [Test]
    public void LiteralSpacesAndCrossSpanCrLfAffectActualGlyphAdvance()
    {
        const string html = "<pre><span style='color:blue'>AB  \r</span><span style='color:blue'>\n  WA</span></pre>";
        var text = Layout(html).SelectMany(p => p.Runs).ToArray();
        Assert.That(text.Select(x => x.Text), Is.EqualTo(new[] { "AB  ", "  WA" }));
        var many = Layout("<pre>AB   BA</pre>").SelectMany(p => p.Runs).Single();
        var one = Layout("<pre>AB BA</pre>").SelectMany(p => p.Runs).Single();
        Assert.That(many.Width - one.Width, Is.EqualTo(14.4).Within(0.0001),
            "Two extra Courier spaces at 12pt must advance 14.4pt.");
    }

    [Test]
    public void AuthoredBreaksAloneDrivePaginationAcrossSeventyTwoAuditRows()
    {
        var text = string.Concat(Enumerable.Range(1, 72).Select(i =>
            "R" + i.ToString("000") + "   OK\n")) + "FINAL";
        var html = "<pre>" + text + "</pre>";
        var oracle = "<p style='white-space:pre-wrap'>" + text + "</p>";
        var pages = Layout(html);
        Assert.That(pages.Count, Is.GreaterThan(2));
        Assert.That(Pdf(html), Is.EqualTo(Pdf(oracle)));
        var lines = pages.SelectMany(page => page.Runs).Select(run => run.Text).ToArray();
        Assert.That(lines.Length, Is.EqualTo(73));
        Assert.That(lines[0], Is.EqualTo("R001   OK"));
        Assert.That(lines[^1], Is.EqualTo("FINAL"));
    }

    [Test]
    public void CascadeImportantAndWideResetsOverridePreWithoutInfectingOtherParagraphs()
    {
        const string html = "<style>pre{white-space:normal}pre.audit{white-space:pre!important}" +
            "pre.reset{white-space:initial!important}pre.inherited{white-space:inherit!important}" +
            "pre.unset{white-space:unset!important}</style>" +
            "<pre class='audit' style='white-space:normal'>A  B\nWA</pre>" +
            "<pre class='reset'>A  B\nWA</pre><pre class='inherited'>A  B\nWA</pre>" +
            "<pre class='unset'>A  B\nWA</pre><p>A  B\nWA</p>";
        var paragraphs = HtmlDocumentReader.Read(html, Page, default);
        Assert.That(paragraphs.Select(p => p.Style.NoWrap),
            Is.EqualTo(new[] { true, false, false, false, false }));
        Assert.That(paragraphs.Select(p => p.Style.PreserveSpaces),
            Is.EqualTo(new[] { true, false, false, false, false }));
        Assert.That(paragraphs.Select(p => p.Style.PreserveLineBreaks),
            Is.EqualTo(new[] { true, false, false, false, false }));
        Assert.That(Pdf("<pre style='white-space:normal'>AB</pre>"), Is.EqualTo(Pdf("<p>AB</p>")));
    }


    [Test]
    public void PreUsesActualParagraphMarginAndPositionEvenBetweenOtherReportBlocks()
    {
        const string html = "<div><p>AB</p><pre>AB  WA\nBA</pre><p>WA</p></div>";
        const string control = "<div><p>AB</p><p style='white-space:pre'>AB  WA\nBA</p><p>WA</p></div>";
        Assert.That(Pdf(html), Is.EqualTo(Pdf(control)),
            "A pre block must have exactly the same outside margin and baseline positioning as a paragraph.");
        var actual = Layout(html).SelectMany(page => page.Runs).ToArray();
        var reference = Layout(control).SelectMany(page => page.Runs).ToArray();
        Assert.That(actual.Select(run => run.Baseline), Is.EqualTo(reference.Select(run => run.Baseline)));
        Assert.That(actual.Select(run => run.X), Is.EqualTo(reference.Select(run => run.X)));
    }

    [Test]
    public void ExplicitTrueTypeAndManagedCliUseRealPdfPath()
    {
        var font = PdfFont.LoadTrueType(FontFixture.Create());
        var options = Page with { Fonts = [font] };
        const string html = "<pre>AB  WA\n  BA</pre>";
        var bytes = Pdf(html, options);
        Assert.That(Encoding.Latin1.GetString(bytes), Does.Contain("/FontFile2"));
        Assert.That(bytes, Is.EqualTo(Pdf("<p style='white-space:pre-wrap'>AB  WA\n  BA</p>", options)));
        Reject("<pre>AB  C</pre>", "FPDF1504", options);
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(html));
        using var stdout = new MemoryStream();
        using var stderr = new StringWriter();
        var exit = CliApplication.Run(["-", "-"], input, stdout, stderr);
        Assert.That(exit, Is.Zero, stderr.ToString());
        using var expected = new MemoryStream();
        PdfConverter.Convert(html, expected);
        Assert.That(stdout.ToArray(), Is.EqualTo(expected.ToArray()));
    }

    [TestCase("<pre>AB\tBA</pre>", "FPDF1305")]
    [TestCase("<p style='white-space:pre'>AB\tBA</p>", "FPDF1305")]
    [TestCase("<pre>AB\vBA</pre>", "FPDF1305")]
    [TestCase("<pre>ABCDEFGHIJKLMNOPQRSTUVWXYZABCDEFGHIJKLMNOPQRSTUVWXYZ</pre>", "FPDF1302")]
    [TestCase("<p style='white-space:pre;width:20pt'>AB  WA</p>", "FPDF1302")]
    [TestCase("<pre style='white-space:pre-wrap nowrap'>AB</pre>", "FPDF1202")]
    [TestCase("<pre style='white-space:break-spaces'>AB</pre>", "FPDF1202")]
    [TestCase("<pre><span style='white-space:pre'>AB</span></pre>", "FPDF1201")]
    [TestCase("<div style='white-space:pre'><pre>AB</pre></div>", "FPDF1201")]
    [TestCase("<ul><li><pre>AB</pre></li></ul>", "FPDF1101")]
    [TestCase("<table style='width:100pt'><colgroup><col style='width:100pt'></colgroup><tbody><tr><td><pre>AB</pre></td></tr></tbody></table>", "FPDF1101")]
    [TestCase("<pre><p>AB</p></pre>", "FPDF1101")]
    [TestCase("<pre/>", "FPDF1101")]
    public void InvalidAndUnsupportedInputsPreserveOriginalPdf(string html, string code) => Reject(html, code);
}
