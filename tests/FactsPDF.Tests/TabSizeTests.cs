using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// tab-size (and the -moz-tab-size spelling Tailwind preflight and modern-normalize write
/// first) only sets the width of a tab stop. No tab stop reaches a page: collapsed tabs become
/// spaces and preserved tabs fail with FPDF1305, so a non-negative number or length is
/// validated and dropped and the page renders byte-identical to the page without it.
/// </summary>
[TestFixture]
public sealed class TabSizeTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 400, Margin = 20 };

    private static byte[] Pdf(string html)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, Page);
        return output.ToArray();
    }

    private static string Report(string declarations) =>
        $"<style>h2,th{{font-weight:normal}} html{{{declarations}}} h2{{{declarations}}} .box{{width:200pt;border:1pt solid #333;{declarations}}} .unused{{{declarations}}}</style>" +
        "<h2>Invoice</h2>" +
        $"<div class='box'><p style='{declarations}'>Paid\tin full</p></div>" +
        $"<table style='width:200pt'><colgroup><col style='width:200pt'></colgroup><tbody><tr><td style='{declarations}'>Total</td></tr></tbody></table>" +
        $"<ul><li style='{declarations}'>Line <span style='{declarations}'>item</span></li></ul>";

    [TestCase("tab-size:4")]
    [TestCase("-moz-tab-size:4;tab-size:4")]
    [TestCase("tab-size:0")]
    [TestCase("tab-size:2.5")]
    [TestCase("tab-size:16px")]
    [TestCase("tab-size:1.5em")]
    [TestCase("-moz-tab-size:12pt")]
    [TestCase("TAB-SIZE:4")]
    [TestCase("tab-size:inherit;-moz-tab-size:initial")]
    [TestCase("tab-size:8 !important")]
    public void ValidWidthsRenderLikeThePageWithoutThem(string declarations)
    {
        Assert.That(Pdf(Report(declarations)), Is.EqualTo(Pdf(Report(""))));
    }

    [TestCase("tab-size:-1")]
    [TestCase("tab-size:-2px")]
    [TestCase("tab-size:50%")]
    [TestCase("tab-size:4s")]
    [TestCase("tab-size:auto")]
    [TestCase("tab-size:4 4")]
    [TestCase("tab-size:'4'")]
    [TestCase("tab-size:calc(4)")]
    [TestCase("-moz-tab-size:wide")]
    public void OtherValuesFailWithoutOutput(string declaration)
    {
        foreach (var html in new[] { $"<p style=\"{declaration}\">A</p>", $"<style>.unused{{{declaration}}}</style><p>A</p>" })
        {
            using var output = new MemoryStream();
            var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, Page));
            Assert.That(error!.Code, Is.EqualTo("FPDF1202"), html);
            Assert.That(error.Message, Does.Contain("tab-size supports only a non-negative number or length"));
            Assert.That(output.Length, Is.Zero);
        }
    }

    [Test]
    public void RejectedValueReportsItsSourceOffset()
    {
        const string html = "<p style='-moz-tab-size:-4'>A</p>";
        var error = Assert.Throws<FactsPdfException>(() => Pdf(html));
        Assert.That(error!.SourceOffset, Is.EqualTo(html.IndexOf("-4", StringComparison.Ordinal)));
    }

    [Test]
    public void PreservedTabsStillFailWhateverTheTabSize()
    {
        var error = Assert.Throws<FactsPdfException>(() => Pdf("<p style='white-space:pre;tab-size:4'>A\tB</p>"));
        Assert.That(error!.Code, Is.EqualTo("FPDF1305"));
    }

    [TestCase("-o-tab-size:4")]
    [TestCase("-webkit-tab-size:4")]
    public void UnaliasedPrefixedNamesStillFail(string declaration)
    {
        var error = Assert.Throws<FactsPdfException>(() => Pdf($"<p style='{declaration}'>A</p>"));
        Assert.That(error!.Code, Is.EqualTo("FPDF1201"));
    }

    [Test]
    public void TheSdkAcceptsTabSize()
    {
        var bytes = FactsPDF.Pdf.Write().ToBytes("<p style='-moz-tab-size:4;tab-size:4'>Paid</p>");
        Assert.That(bytes, Is.EqualTo(FactsPDF.Pdf.Write().ToBytes("<p>Paid</p>")));
    }
}
