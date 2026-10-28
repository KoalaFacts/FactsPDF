using System.Text;
using FactsPDF.CommandLine;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>Opaque CSS colour functions must paint through the existing RGB PDF path.</summary>
[TestFixture]
public sealed class OpaqueRgbCssTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 500, Margin = 20 };

    private static byte[] Pdf(string html, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, options ?? Page);
        return output.ToArray();
    }

    private static void Reject(string css)
    {
        using var output = new MemoryStream();
        output.Write("retained"u8);
        output.Position = 2;
        var error = Assert.Throws<FactsPdfException>(() =>
            PdfConverter.Convert("<div style='" + css + "'>Protected report</div>", output, Page));
        Assert.That(error!.Code, Is.EqualTo("FPDF1202"));
        Assert.That(output.ToArray(), Is.EqualTo("retained"u8.ToArray()));
        Assert.That(output.Position, Is.EqualTo(2));
    }

    [TestCase("rgb(35, 68, 107)")]
    [TestCase("RGB(35 68 107)")]
    [TestCase("rgba(35,68,107,1)")]
    [TestCase("rgba(35, 68, 107, 1.0)")]
    public void OpaqueTextRgbHasExactlyTheSamePdfBytesAsExistingHex(string rgb)
    {
        var actual = Pdf("<p style='color:" + rgb + "'>Invoice total</p>");
        var expected = Pdf("<p style='color:#23446b'>Invoice total</p>");
        Assert.That(actual, Is.EqualTo(expected));
        Assert.That(Encoding.Latin1.GetString(actual), Does.Contain("0.137 0.267 0.42 rg"));
    }

    [Test]
    public void OpaqueRgbPaintsBackgroundBorderAndAllFourIndividualSides()
    {
        var rgb = "<div style='background:rgb(240 246 250);border:2pt solid rgb(35,68,107);" +
            "border-top-color:rgba(0, 128, 0, 1);border-right-color:rgb(255,0,0);" +
            "border-bottom-color:rgb(0 0 255);border-left-color:rgb(35,68,107)'>" +
            "<p style='color:rgb(35,68,107)'>Report</p></div>";
        var hex = "<div style='background:#f0f6fa;border:2pt solid #23446b;" +
            "border-top-color:#008000;border-right-color:#ff0000;" +
            "border-bottom-color:#0000ff;border-left-color:#23446b'>" +
            "<p style='color:#23446b'>Report</p></div>";
        Assert.That(Pdf(rgb), Is.EqualTo(Pdf(hex)));
        Assert.That(Encoding.Latin1.GetString(Pdf(rgb)), Does.Contain(" re f Q"));
    }

    [Test]
    public void ShorthandWithMultipleRgbColorsPreservesTheExistingCascade()
    {
        const string rgb = "<style>.report{border:2pt solid rgb(35, 68, 107);" +
            "border-color:rgb(255 0 0) rgb(0 128 0) rgb(0 0 255) rgba(35,68,107,1)!important}</style>" +
            "<section class=report style='border-left-color:red'>Client</section>";
        const string hex = "<style>.report{border:2pt solid #23446b;" +
            "border-color:#ff0000 #008000 #0000ff #23446b!important}</style>" +
            "<section class=report style='border-left-color:red'>Client</section>";
        Assert.That(Pdf(rgb), Is.EqualTo(Pdf(hex)));
    }

    [Test]
    public void RgbAcrossPaginatedReportUsesExistingBoxPainting()
    {
        const string rgb = "<section style='background:rgb(240,246,250);border:1pt solid rgb(35 68 107)'>" +
            "<p>First page</p><p style='break-before:page'>Second page</p></section>";
        const string hex = "<section style='background:#f0f6fa;border:1pt solid #23446b'>" +
            "<p>First page</p><p style='break-before:page'>Second page</p></section>";
        Assert.That(Pdf(rgb), Is.EqualTo(Pdf(hex)));
    }

    [TestCase("background:rgb(256,1,2)")]
    [TestCase("background:rgb(-1,1,2)")]
    [TestCase("background:rgb(1.5,2,3)")]
    [TestCase("background:rgb(1%,2%,3%)")]
    [TestCase("background:rgb(1,2)")]
    [TestCase("background:rgb(1 2, 3)")]
    [TestCase("background:rgba(1,2,3,0.5)")]
    [TestCase("background:rgba(1,2,3,0)")]
    [TestCase("background:rgb(1 2 3 / 1)")]
    [TestCase("background:rgb(calc(1),2,3)")]
    [TestCase("background:hsl(0,0%,0%)")]
    [TestCase("background:linear-gradient(red,blue)")]
    public void UnsupportedFunctionsAndAlphaFailBeforeReplacingOutput(string css) => Reject(css);

    [Test]
    public void UnsupportedFunctionFailsEvenInUnmatchedRule()
    {
        using var output = new MemoryStream();
        output.Write("keep"u8);
        var error = Assert.Throws<FactsPdfException>(() =>
            PdfConverter.Convert("<style>.unmatched{color:rgb(12%,0%,50%)}</style><p>Safe</p>", output));
        Assert.That(error!.Code, Is.EqualTo("FPDF1202"));
        Assert.That(output.ToArray(), Is.EqualTo("keep"u8.ToArray()));
    }

    [Test]
    public void ManagedCliAndApiEmitIdenticalRgbPaint()
    {
        const string html = "<div style='background:rgb(240 246 250);border:2pt solid rgb(35,68,107)'>" +
            "<p style='color:rgba(0,128,0,1)'>Report status</p></div>";
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(html));
        using var output = new MemoryStream();
        using var diagnostics = new StringWriter();
        Assert.That(CliApplication.Run(["-", "-"], input, output, diagnostics), Is.Zero, diagnostics.ToString());
        Assert.That(output.ToArray(), Is.EqualTo(Pdf(html, new())));
    }
}
