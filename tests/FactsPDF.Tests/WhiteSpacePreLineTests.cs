using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>Preserve authored report note lines without enabling preformatted CSS layout.</summary>
[TestFixture]
public sealed class WhiteSpacePreLineTests
{
    private static readonly PdfOptions SmallPage = new()
    {
        PageWidth = 180, PageHeight = 190, Margin = 18
    };

    private static byte[] Pdf(string html, PdfOptions? options = null)
    {
        using var stream = new MemoryStream();
        PdfConverter.Convert(html, stream, options ?? SmallPage);
        return stream.ToArray();
    }

    private static List<LayoutPage> Pages(string html, PdfOptions? options = null)
    {
        var o = options ?? SmallPage;
        return TextLayout.Layout(HtmlDocumentReader.ReadTree(html, o, default), o, default);
    }

    private static void Reject(string html, string code, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        output.Write("original-pdf"u8);
        output.Position = 4;
        var error = Assert.Throws<FactsPdfException>(() =>
            PdfConverter.Convert(html, output, options ?? SmallPage));
        Assert.That(error!.Code, Is.EqualTo(code));
        Assert.That(output.ToArray(), Is.EqualTo("original-pdf"u8.ToArray()));
        Assert.That(output.Position, Is.EqualTo(4));
    }

    [Test]
    public void AuthoredNewlinesPaintExactlyLikeExplicitHtmlBreaks()
    {
        const string text = "<p style='white-space:pre-line'>  PAID    IN FULL  \r\n  NEXT\n  READY  </p>";
        const string explicitBreaks = "<p>PAID IN FULL<br>NEXT<br>READY</p>";
        Assert.That(Pdf(text), Is.EqualTo(Pdf(explicitBreaks)));
        var runs = Pages(text).SelectMany(p => p.Runs).ToArray();
        Assert.That(runs.Select(x => x.Text),
            Is.EqualTo(new[] { "PAID IN FULL", "NEXT", "READY" }));
        Assert.That(runs.Select(x => x.Baseline).Distinct().Count(), Is.EqualTo(3));
    }

    [Test]
    public void CrLfAcrossStyledSpansAndFormFeedAreNormalizedWithoutLostText()
    {
        const string source = "<p style='white-space:pre-line'>" +
            "<span style='color:blue'>AB\r</span><span style='color:blue'>\nBA</span>\fWA</p>";
        const string oracle = "<p><span style='color:blue'>AB</span><br>" +
            "<span style='color:blue'>BA</span><br>WA</p>";
        Assert.That(Pdf(source), Is.EqualTo(Pdf(oracle)));
    }

    [Test]
    public void TrailingBreakIsDiscardedButInteriorBlankLinesAndBrRemain()
    {
        Assert.That(Pdf("<p style='white-space:pre-line'>AB\n\nBA\n</p>"),
            Is.EqualTo(Pdf("<p>AB<br><br>BA</p>")));
        Assert.That(Pdf("<p style='white-space:pre-line'>AB\n<br>BA</p>"),
            Is.EqualTo(Pdf("<p>AB<br><br>BA</p>")));
        Assert.That(Pdf("<p style='white-space:pre-line'>AB&#10;BA</p>"),
            Is.EqualTo(Pdf("<p>AB<br>BA</p>")));
    }

    [Test]
    public void NormalAndNoWrapKeepLegacyWhitespaceAndPdfBytes()
    {
        const string body = "PAID  IN\n FULL";
        Assert.That(Pdf("<p style='white-space:normal'>" + body + "</p>"),
            Is.EqualTo(Pdf("<p>" + body + "</p>")));
        Assert.That(Pdf("<p style='white-space:nowrap'>" + body + "</p>"),
            Is.EqualTo(Pdf("<p>PAID IN FULL</p>")));
        Assert.That(Pdf("<p style='white-space:pre-line'>PAID IN FULL</p>"),
            Is.EqualTo(Pdf("<p>PAID IN FULL</p>")));
    }

    [Test]
    public void PreLineStillAutomaticallyWrapsAndPaginatesRealReportNotes()
    {
        var content = string.Concat(Enumerable.Range(1, 72).Select(i =>
            "<p style='white-space:pre-line'>REF" + i.ToString("000") +
            "\n  INSPECTION   COMPLETE  </p>"));
        var oracle = string.Concat(Enumerable.Range(1, 72).Select(i =>
            "<p>REF" + i.ToString("000") + "<br>INSPECTION COMPLETE</p>"));
        Assert.That(Pdf(content), Is.EqualTo(Pdf(oracle)));
        var pages = Pages(content);
        Assert.That(pages.Count, Is.GreaterThan(2));
        Assert.That(pages.SelectMany(p => p.Runs).Count(t => t.Text.StartsWith("REF", StringComparison.Ordinal)),
            Is.EqualTo(72));
    }

    [Test]
    public void StylesheetImportantAndCssWideValuesRetainPreLineSemantics()
    {
        const string source = "<style>p{white-space:pre-line!important}" +
            ".reset{white-space:initial!important}.unset{white-space:unset!important}" +
            ".inherit{white-space:inherit!important}</style>" +
            "<p>AB\nBA</p><p class='reset'>AB\nBA</p>" +
            "<p class='unset'>AB\nBA</p><p class='inherit'>AB\nBA</p>";
        var paragraphs = HtmlDocumentReader.Read(source, SmallPage, default);
        Assert.That(paragraphs.Select(p => p.Style.PreserveLineBreaks),
            Is.EqualTo(new[] { true, false, false, false }));
        Assert.That(paragraphs.All(p => !p.Style.NoWrap), Is.True);
    }

    [Test]
    public void TrueTypeFontsAndPageGeometryMatchAnIndependentExplicitBreakTemplate()
    {
        var font = PdfFont.LoadTrueType(FontFixture.Create());
        var options = SmallPage with { Fonts = [font], PageHeight = 130 };
        var source = "<p style='font-size:12pt;white-space:pre-line'>AB\nWA\nBA\nAB</p>";
        var oracle = "<p style='font-size:12pt'>AB<br>WA<br>BA<br>AB</p>";
        var bytes = Pdf(source, options);
        Assert.That(bytes, Is.EqualTo(Pdf(oracle, options)));
        Assert.That(Encoding.Latin1.GetString(bytes), Does.Contain("/FontFile2"));
        Assert.That(Pages(source, options).Count, Is.GreaterThan(1));
        Reject("<p style='white-space:pre-line'>AB\nC</p>", "FPDF1504", options);
    }

    [TestCase("<p style='white-space:pre'>A</p>", "FPDF1202")]
    [TestCase("<p style='white-space:pre-wrap'>A</p>", "FPDF1202")]
    [TestCase("<p style='white-space:break-spaces'>A</p>", "FPDF1202")]
    [TestCase("<p style='white-space:pre-line nowrap'>A</p>", "FPDF1202")]
    [TestCase("<style>.unused{white-space:pre-wrap}</style><p>A</p>", "FPDF1202")]
    [TestCase("<div style='white-space:pre-line'><p>A</p></div>", "FPDF1201")]
    [TestCase("<span style='white-space:pre-line'>A</span>", "FPDF1201")]
    [TestCase("<p style='width:15pt;white-space:pre-line'>ABCDEFGHIJKLMNOPQRSTUVWXYZ</p>", "FPDF1302")]
    [TestCase("<p style='white-space:pre-line'>AB\n中文</p>", "FPDF1301")]
    public void UnsupportedModesElementsAndUnbreakableContentProtectExistingPdf(string html, string code)
        => Reject(html, code);
}
