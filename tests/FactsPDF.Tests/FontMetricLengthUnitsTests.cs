using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>ex, ch, lh and rlh resolve against the element's font and line height, as Chrome does.</summary>
[TestFixture]
public sealed class FontMetricLengthUnitsTests
{
    // The built-in font has Courier's metrics: x-height 0.426 em, "0" advance 0.6 em, normal line 1.2 em.
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 200, Margin = 20 };

    private static byte[] Pdf(string html)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, Page);
        return output.ToArray();
    }

    [TestCase("width:10ex", "width:42.6pt")]
    [TestCase("width:10ch", "width:60pt")]
    [TestCase("max-width:5ch", "max-width:30pt")]
    [TestCase("min-width:20ch", "min-width:120pt")]
    [TestCase("padding:1ch 2ch", "padding:6pt 12pt")]
    [TestCase("margin-left:3ch", "margin-left:18pt")]
    [TestCase("margin-top:2ex;margin-bottom:1ch", "margin-top:8.52pt;margin-bottom:6pt")]
    [TestCase("width:4lh", "width:60pt")]
    [TestCase("width:2rlh", "width:40pt")]
    [TestCase("text-indent:2ch", "text-indent:12pt")]
    [TestCase("letter-spacing:0.1ch", "letter-spacing:0.6pt")]
    [TestCase("word-spacing:1ex", "word-spacing:4.26pt")]
    public void FontRelativeUnitsPrintLikeTheirPointValues(string relative, string absolute)
    {
        const string head = "<html style='line-height:20pt'><body style='margin:0'>";
        string Body(string declarations) =>
            head + "<p style='font-size:10pt;line-height:15pt;background:#cde;" + declarations + "'>AB AB</p></body></html>";
        Assert.That(Pdf(Body(relative)), Is.EqualTo(Pdf(Body(absolute))));
    }

    [Test]
    public void LineHeightUnitUsesTheElementsOwnLineHeightAndTheRootUnitTheRoots()
    {
        const string html = "<html style='line-height:20pt'><body style='margin:0'><p style='line-height:10pt;width:2lh'>AB</p>" +
            "<p style='line-height:10pt;width:2rlh'>AB</p></body></html>";
        const string points = "<html style='line-height:20pt'><body style='margin:0'><p style='line-height:10pt;width:20pt'>AB</p>" +
            "<p style='line-height:10pt;width:40pt'>AB</p></body></html>";
        Assert.That(Pdf(html), Is.EqualTo(Pdf(points)));
    }

    [Test]
    public void NormalLineHeightIsOnePointTwoEmForTheBuiltInFont()
    {
        Assert.That(Pdf("<p style='font-size:10pt;width:5lh'>AB</p>"), Is.EqualTo(Pdf("<p style='font-size:10pt;width:60pt'>AB</p>")));
    }

    [Test]
    public void SiblingsWithDifferentFontsDoNotShareAFontRelativeBox()
    {
        // The computed-box cache must not give the second paragraph the first one's resolved width.
        var mixed = Pdf("<p style='font-size:10pt;width:10ch'>AB</p><p style='font-size:20pt;width:10ch'>AB</p>");
        var points = Pdf("<p style='font-size:10pt;width:60pt'>AB</p><p style='font-size:20pt;width:120pt'>AB</p>");
        Assert.That(mixed, Is.EqualTo(points));
    }

    [TestCase("width:10rex")]
    [TestCase("width:10rch")]
    public void OtherFontRelativeUnitsStillFailExplicitly(string declaration)
    {
        using var output = new MemoryStream();
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert($"<p style='{declaration}'>A</p>", output, Page));
        Assert.That(error!.Code, Is.EqualTo("FPDF1202"));
        Assert.That(output.Length, Is.Zero);
    }
}
