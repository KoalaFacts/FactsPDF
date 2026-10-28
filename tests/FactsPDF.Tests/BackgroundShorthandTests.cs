using System.Text;
using FactsPDF.CommandLine;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>Colour-only background shorthand: familiar authoring without broad CSS background support.</summary>
[TestFixture]
public sealed class BackgroundShorthandTests
{
    private static readonly PdfOptions Options = new() { PageWidth = 300, PageHeight = 500, Margin = 20 };

    private static byte[] Convert(string html)
    {
        using var pdf = new MemoryStream();
        PdfConverter.Convert(html, pdf, Options);
        return pdf.ToArray();
    }

    [TestCase("#cee8fb")]
    [TestCase("red")]
    [TestCase("transparent")]
    [TestCase("initial")]
    public void SingleColourShorthandMatchesExistingBackgroundColorBytes(string value)
    {
        const string before = "<style>.panel{";
        const string after = "}</style><div class=panel><p>Service summary</p></div>";
        var shorthand = Convert(before + "background:" + value + after);
        var explicitColor = Convert(before + "background-color:" + value + after);
        Assert.That(shorthand, Is.EqualTo(explicitColor));
    }

    [Test]
    public void InlineAndStylesheetShorthandUseExistingCascadeAndImportant()
    {
        const string source = "<style>.panel{background:red!important}div.panel{background-color:blue}</style>" +
            "<div class=panel style='background:green'>Customer report</div>";
        const string expected = "<div style='background-color:red'>Customer report</div>";
        Assert.That(Convert(source), Is.EqualTo(Convert(expected)));
    }

    [TestCase("url(chart.png)")]
    [TestCase("linear-gradient(red,blue)")]
    [TestCase("rgba(1,2,3,0.5)")]
    [TestCase("#abc no-repeat")]
    [TestCase("red blue")]
    public void ImageGradientFunctionAndMultipleComponentBackgroundsFailBeforeOutput(string value)
    {
        using var pdf = new MemoryStream();
        pdf.Write("retain"u8);
        var error = Assert.Throws<FactsPdfException>(() =>
            PdfConverter.Convert("<div style='background:" + value + "'>Safe report</div>", pdf, Options));
        Assert.That(error!.Code, Is.EqualTo("FPDF1202"));
        Assert.That(Encoding.ASCII.GetString(pdf.ToArray()), Is.EqualTo("retain"));
    }

    [Test]
    public void InvalidUnmatchedStylesheetBackgroundStillFailsStrictly()
    {
        using var pdf = new MemoryStream();
        var error = Assert.Throws<FactsPdfException>(() =>
            PdfConverter.Convert("<style>.unused{background:url(secret.png)}</style><p>Safe report</p>", pdf, Options));
        Assert.That(error!.Code, Is.EqualTo("FPDF1202"));
        Assert.That(pdf.Length, Is.Zero);
    }

    [Test]
    public void RealManagedCliUsesSameColourShorthandAndPreservesBinaryStdout()
    {
        const string html = "<div style='background:#cee8fb;padding:6pt'><p>Service report</p></div>";
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(html));
        using var output = new MemoryStream();
        using var diagnostics = new StringWriter();
        var code = CliApplication.Run(["-", "-"], input, output, diagnostics);
        Assert.That(code, Is.Zero, diagnostics.ToString());
        using var direct = new MemoryStream();
        PdfConverter.Convert(html, direct); // CLI uses the default page geometry.
        Assert.That(output.ToArray(), Is.EqualTo(direct.ToArray()));
        Assert.That(diagnostics.ToString(), Does.Not.Contain("Hint:"));
    }
}
