using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// position, float, clear, overflow(-x/-y), visibility, z-index, opacity, box-shadow,
/// text-shadow, transform, filter and background-image are accepted at
/// the one value the report renderer already lays out (their initial value, or a CSS-wide
/// keyword that computes to it): the page renders byte-identical to the page without them.
/// Every other value still fails with FPDF1202, and nothing is written.
/// </summary>
[TestFixture]
public sealed class InitialValueNoOpTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 400, Margin = 20 };

    private static byte[] Pdf(string html)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, Page);
        return output.ToArray();
    }

    private static string Report(string declarations) =>
        $"<style>h2{{{declarations}}} .box{{width:200pt;border:1pt solid #333;{declarations}}}</style>" +
        "<h2>Invoice</h2>" +
        $"<div class='box'><p style='{declarations}'>Paid in full</p></div>" +
        $"<table style='width:200pt'><colgroup><col style='width:200pt'></colgroup><tbody><tr><td style='{declarations}'>Total</td></tr></tbody></table>" +
        $"<ul><li style='{declarations}'>Line <span style='{declarations}'>item</span></li></ul>";

    [TestCase("position:static")]
    [TestCase("float:none")]
    [TestCase("clear:none")]
    [TestCase("overflow:visible")]
    [TestCase("overflow-x:visible;overflow-y:visible")]
    [TestCase("visibility:visible")]
    [TestCase("z-index:auto")]
    [TestCase("opacity:1")]
    [TestCase("opacity:1.0")]
    [TestCase("opacity:100%")]
    [TestCase("box-shadow:none;text-shadow:none")]
    [TestCase("transform:none;filter:none;background-image:none")]
    [TestCase("box-shadow:inherit;transform:initial;background-image:unset")]
    [TestCase("POSITION:STATIC;Float:None")]
    [TestCase("position:inherit;float:initial;clear:unset;visibility:inherit;opacity:initial")]
    [TestCase("position:static!important;z-index:auto !important")]
    public void InitialValueRendersLikeTheUndeclaredPage(string declarations)
    {
        Assert.That(Pdf(Report(declarations)), Is.EqualTo(Pdf(Report(""))));
    }

    [Test]
    public void InitialValueDoesNotDisturbTheCascade()
    {
        // Interleaving no-op declarations leaves the later colour winning, exactly as without them.
        Assert.That(Pdf("<p style='color:#c00;position:static;color:#00c;opacity:1'>Due</p>"),
            Is.EqualTo(Pdf("<p style='color:#c00;color:#00c'>Due</p>")));
        Assert.That(Pdf("<style>p{float:none;color:#00c}</style><p>Due</p>"),
            Is.EqualTo(Pdf("<style>p{color:#00c}</style><p>Due</p>")));
    }

    [TestCase("position:absolute")]
    [TestCase("position:relative")]
    [TestCase("float:left")]
    [TestCase("clear:both")]
    [TestCase("overflow:hidden")]
    [TestCase("overflow-x:auto")]
    [TestCase("visibility:hidden")]
    [TestCase("z-index:1")]
    [TestCase("opacity:0.5")]
    [TestCase("opacity:50%")]
    [TestCase("opacity:visible")]
    [TestCase("box-shadow:0 0 2pt #000")]
    [TestCase("text-shadow:1px 1px #ccc")]
    [TestCase("transform:rotate(90deg)")]
    [TestCase("filter:grayscale(1)")]
    [TestCase("background-image:url(a.png)")]
    [TestCase("background-image:none none")]
    [TestCase("position:'static'")]
    [TestCase("float:none none")]
    [TestCase("overflow:visible visible")]
    [TestCase("position:")]
    public void AnyOtherValueFailsWithoutOutput(string declaration)
    {
        foreach (var html in new[] { $"<p style=\"{declaration}\">A</p>", $"<style>.unused{{{declaration}}}</style><p>A</p>" })
        {
            using var output = new MemoryStream();
            var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, Page));
            Assert.That(error!.Code, Is.EqualTo("FPDF1202"), html);
            Assert.That(output.Length, Is.Zero);
        }
    }

    [Test]
    public void RejectedValueNamesTheOnlySupportedValueAndItsSourceOffset()
    {
        const string html = "<p style='float:left'>A</p>";
        var error = Assert.Throws<FactsPdfException>(() => Pdf(html));
        Assert.That(error!.Message, Does.Contain("float supports only none"));
        Assert.That(error.SourceOffset, Is.EqualTo(html.IndexOf("left", StringComparison.Ordinal)));
    }

    [Test]
    public void OtherUnsupportedPropertiesStillFail()
    {
        var error = Assert.Throws<FactsPdfException>(() => Pdf("<p style='top:0'>A</p>"));
        Assert.That(error!.Code, Is.EqualTo("FPDF1201"));
    }

    [Test]
    public void TheSdkAcceptsTheInitialValues()
    {
        var bytes = FactsPDF.Pdf.Write().ToBytes("<p style='position:static;overflow:visible;opacity:1'>Paid</p>");
        Assert.That(bytes, Is.EqualTo(FactsPDF.Pdf.Write().ToBytes("<p>Paid</p>")));
    }
}
