using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>Computed border lengths must change real PDF paint, wrapping and pagination.</summary>
[TestFixture]
public sealed class FontRelativeBorderWidthTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 340, PageHeight = 170, Margin = 18 };

    private static byte[] Pdf(string html)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, Page);
        return output.ToArray();
    }

    private static List<LayoutPage> Layout(string html)
        => TextLayout.Layout(HtmlDocumentReader.ReadTree(html, Page, default), Page, default);

    private static void Reject(string html, string code)
    {
        using var output = new MemoryStream();
        output.Write("KEEP CUSTOMER PDF"u8);
        output.Position = 5;
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, Page));
        Assert.That(error!.Code, Is.EqualTo(code));
        Assert.That(Encoding.ASCII.GetString(output.ToArray()), Is.EqualTo("KEEP CUSTOMER PDF"));
        Assert.That(output.Position, Is.EqualTo(5));
    }

    [Test]
    public void ComputedFontAndRootRelativeBordersMatchPhysicalGeometryAndPdfBytes()
    {
        const string before = "<html style='font-size:16pt'><body>";
        const string after = "<p style='margin:0'>Service status confirmed</p></section></body></html>";
        const string relative = "<section style='font-size:10pt;width:240pt;padding:3pt;background:#edf4fa;" +
            "border:0.2em solid #24445a;border-right-width:0.125rem;" +
            "border-bottom-width:0.15em'>";
        const string absolute = "<section style='font-size:10pt;width:240pt;padding:3pt;background:#edf4fa;" +
            "border:2pt solid #24445a;border-right-width:2pt;" +
            "border-bottom-width:1.5pt'>";
        Assert.That(Pdf(before + relative + after), Is.EqualTo(Pdf(before + absolute + after)));
        var box = Layout(before + relative + after).SelectMany(page => page.PaintBoxes)
            .Single(p => p.Style.BackgroundColor.HasValue);
        Assert.Multiple(() =>
        {
            Assert.That(box.Style.BorderTop.EffectiveWidth, Is.EqualTo(2).Within(0.000001));
            Assert.That(box.Style.BorderRight.EffectiveWidth, Is.EqualTo(2).Within(0.000001));
            Assert.That(box.Style.BorderBottom.EffectiveWidth, Is.EqualTo(1.5).Within(0.000001));
            Assert.That(box.Style.BorderLeft.EffectiveWidth, Is.EqualTo(2).Within(0.000001));
        });
    }

    [Test]
    public void FourSideAndLogicalShorthandsRespectImportantAndCascade()
    {
        const string before = "<html style='font-size:16pt'><style>" +
            "article.card{font-size:10pt;border:0.1em solid #224466;" +
            "border-width:0.1em 0.125rem 0.15em 0.0625rem;" +
            "border-inline-start-width:0.2em!important}</style><body>";
        const string controlBefore = "<html style='font-size:16pt'><style>" +
            "article.card{font-size:10pt;border:1pt solid #224466;" +
            "border-width:1pt 2pt 1.5pt 1pt;" +
            "border-inline-start-width:2pt!important}</style><body>";
        const string after = "<article class=card style='border-left-width:0'>"+
            "<p style='margin:0'>APPROVAL</p></article></body></html>";
        Assert.That(Pdf(before + after), Is.EqualTo(Pdf(controlBefore + after)));
    }

    [Test]
    public void RelativeCellBordersKeepMultiPageInvoiceAndRepeatedHeadersByteEquivalent()
    {
        var rows = string.Concat(Enumerable.Range(1, 30).Select(i =>
            $"<tr><td>S{i:00}</td><td>Approved service item {i:00}</td></tr>"));
        const string before = "<html style='font-size:16pt'><body><style>" +
            "table{font-size:10pt} th{font-weight:normal} ";
        const string after = "</style><table style='width:240pt'><colgroup>" +
            "<col style='width:75pt'><col style='width:165pt'></colgroup>" +
            "<thead><tr><th>Code</th><th>Service</th></tr></thead><tbody>";
        const string end = "</tbody></table></body></html>";
        var relative = before + "td,th{padding:2pt;border:0.1em solid #334455}" + after + rows + end;
        var points = before + "td,th{padding:2pt;border:1pt solid #334455}" + after + rows + end;
        Assert.That(Pdf(relative), Is.EqualTo(Pdf(points)));
        var pages = Layout(relative);
        Assert.That(pages.Count, Is.GreaterThan(1));
        Assert.That(pages.SelectMany(p => p.Runs).Count(r => r.Text == "Code"),
            Is.EqualTo(pages.Count));
        Assert.That(pages.SelectMany(p => p.Runs).Count(r =>
            r.Text.StartsWith("Approved service item", StringComparison.Ordinal)), Is.EqualTo(30));
    }

    [TestCase("border-width:-1em", "FPDF1202")]
    [TestCase("border:2000rem solid red", "FPDF1202")]
    [TestCase("border-width:calc(1em + 1pt)", "FPDF1202")]
    [TestCase("border-left-width:10%", "FPDF1202")]
    [TestCase("outline:1em solid blue", "FPDF1202")]
    public void InvalidRelativeWidthsKeepExistingOutputAndStreamPosition(string css, string code)
        => Reject("<html style='font-size:16pt'><body><section style='" + css +
            "'><p>SAFE</p></section></body></html>", code);

    [Test]
    public void UnsupportedElementCannotAcquireBorderByRelativeWidth()
        => Reject("<p>Before<span style='border:0.1em solid red'>After</span></p>", "FPDF1201");

    [Test]
    public void UnstyledDocumentPreservesDefaultPdfBytes()
        => Assert.That(Pdf("<p>Report</p>"), Is.EqualTo(Pdf("<p style='border-width:0em'>Report</p>")));
}
