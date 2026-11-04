using System.Text;
using FactsPDF.CommandLine;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>Strict opaque HSL print colours must resolve to actual PDF ink and box paint.</summary>
[TestFixture]
public sealed class OpaqueHslCssTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 250, Margin = 18 };

    private static byte[] Pdf(string html, PdfOptions? options = null)
    {
        using var stream = new MemoryStream();
        PdfConverter.Convert(html, stream, options ?? Page);
        return stream.ToArray();
    }

    private static void Reject(string css)
    {
        using var output = new MemoryStream();
        output.Write("approved"u8);
        output.Position = 3;
        var error = Assert.Throws<FactsPdfException>(() =>
            PdfConverter.Convert("<style>.unused{" + css + "}</style><p>Safe text</p>", output, Page));
        Assert.That(error!.Code, Is.EqualTo("FPDF1202"));
        Assert.That(output.ToArray(), Is.EqualTo("approved"u8.ToArray()));
        Assert.That(output.Position, Is.EqualTo(3));
    }

    [TestCase("hsl(0,100%,50%)", "#ff0000")]
    [TestCase("hsl(120 100% 50%)", "#00ff00")]
    [TestCase("HSL(240deg 100% 50%)", "#0000ff")]
    [TestCase("hsl(210,60%,40%)", "#2966a3")]
    [TestCase("hsl(180 50% 50%)", "#40bfbf")]
    [TestCase("hsl(0,100%,5%)", "#1a0000")]
    [TestCase("hsl(0,100%,95%)", "#ffe6e6")]
    [TestCase("hsl(60 100% 5%)", "#1a1a00")]
    [TestCase("hsl(0,100%,55%)", "#ff1a1a")]
    [TestCase("hsl(-120,100%,50%)", "#0000ff")]
    [TestCase("hsl(480,100%,50%)", "#00ff00")]
    [TestCase("hsla(120,100%,25%,1.0)", "#008000")]
    public void HslColourMatchesExplicitHexInRenderedPdf(string hsl, string hex)
    {
        var source = "<div style='background:" + hsl + ";border:2pt solid " + hsl +
            "'><p style='color:" + hsl + "'>Inspection approved</p></div>";
        var control = "<div style='background:" + hex + ";border:2pt solid " + hex +
            "'><p style='color:" + hex + "'>Inspection approved</p></div>";
        Assert.That(Pdf(source), Is.EqualTo(Pdf(control)));
    }

    [Test]
    public void CascadeShorthandAndPaginatedBusinessRegisterMatchIndependentRgbHex()
    {
        var actual = ReadExample("report-hsl-colours.html");
        var expected = ReadExample("report-hsl-colours-oracle.html");
        Assert.That(Pdf(actual), Is.EqualTo(Pdf(expected)));
        Assert.That(TextLayout.Layout(HtmlDocumentReader.ReadTree(actual, Page, default), Page, default).Count,
            Is.GreaterThan(1));
        Assert.That(Encoding.Latin1.GetString(Pdf(actual)), Does.Contain(" rg"));
    }

    [TestCase("color:hsl(12,50%,101%)")]
    [TestCase("color:hsl(12,-1%,50%)")]
    [TestCase("color:hsl(12,50,50%)")]
    [TestCase("color:hsl(12,50%,50)")]
    [TestCase("color:hsl(12,50%)")]
    [TestCase("color:hsl(12 50%, 50%)")]
    [TestCase("color:hsl(12 50% 50% / 1)")]
    [TestCase("color:hsla(12,50%,50%,.5)")]
    [TestCase("color:hsla(12,50%,50%,0.99999999999999999)")]
    [TestCase("color:hsla(12 50% 50%)")]
    [TestCase("color:hsl(1turn,50%,50%)")]
    [TestCase("color:hsl(3601,50%,50%)")]
    [TestCase("color:hsl(calc(12),50%,50%)")]
    [TestCase("color:hsl(12,50%,50%,1)")]
    public void UnsupportedHslSyntaxIsRejectedEvenInUnmatchedRulesAndPreservesOutput(string css)
        => Reject(css);

    [TestCase("<p style='color:hsl(210,60%,40%)'>漢</p>", "FPDF1301")]
    [TestCase("<p style='color:hsl(210,60%,40%)'>Record</p><img src='missing.png'>", "FPDF1604")]
    public void HslWithoutAlphaCannotMaskMissingFontOrResourceFailures(string html, string expectedCode)
    {
        // Unbound Unicode uses the core font diagnostic (FPDF1301); an absent
        // explicit image uses the resource diagnostic (FPDF1604). Valid HSL
        // must not hide either, replace the existing PDF, or move its cursor.
        using var output = new MemoryStream();
        output.Write("unchanged"u8);
        output.Position = 3;
        var error = Assert.Throws<FactsPdfException>(() =>
            PdfConverter.Convert(html, output, Page));
        Assert.That(error!.Code, Is.EqualTo(expectedCode));
        Assert.That(output.ToArray(), Is.EqualTo("unchanged"u8.ToArray()));
        Assert.That(output.Position, Is.EqualTo(3));
    }

    [Test]
    public void ManagedCliMatchesApiAndPreservesBinaryStdout()
    {
        var html = ReadExample("report-hsl-colours.html");
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(html));
        using var output = new MemoryStream();
        using var diagnostics = new StringWriter();
        Assert.That(CliApplication.Run(["-", "-"], input, output, diagnostics), Is.Zero,
            diagnostics.ToString());
        Assert.That(output.ToArray(), Is.EqualTo(Pdf(html, new())));
    }

    private static string ReadExample(string name)
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FactsPDF.slnx")))
            directory = directory.Parent;
        Assert.That(directory, Is.Not.Null, "FactsPDF source root not found.");
        return File.ReadAllText(Path.Combine(directory!.FullName, "examples", name));
    }
}
