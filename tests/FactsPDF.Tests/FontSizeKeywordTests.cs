using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>Bounded CSS font-size keywords must change rendered PDF text, not merely parse.</summary>
[TestFixture]
public sealed class FontSizeKeywordTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 245, PageHeight = 175, Margin = 15 };

    private static byte[] Pdf(string html, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, options ?? Page);
        return output.ToArray();
    }

    [TestCase("xx-small", 7.2)]
    [TestCase("x-small", 9d)]
    [TestCase("small", 10d)]
    [TestCase("medium", 12d)]
    [TestCase("large", 14.4)]
    [TestCase("x-large", 18d)]
    [TestCase("xx-large", 24d)]
    public void AbsoluteKeywordUsesDocumentedPointScale(string keyword, double points)
    {
        var html = "<p style='font-size:" + keyword + "'>Business report</p>";
        var paragraph = HtmlDocumentReader.Read(html, Page, default).Single();
        Assert.That(paragraph.Style.FontSize, Is.EqualTo(points).Within(0.00000001));
        Assert.That(Pdf(html), Is.EqualTo(Pdf("<p style='font-size:" +
            points.ToString(System.Globalization.CultureInfo.InvariantCulture) +
            "pt'>Business report</p>")));
    }

    [Test]
    public void RelativeKeywordsUseComputedParentAndCascadeWinners()
    {
        const string keywords = "<style>section{font-size:15pt}p{font-size:smaller!important}" +
            "p span{font-size:larger}</style><section><p style='font-size:large'>" +
            "Service <span>complete</span></p></section>";
        const string points = "<style>section{font-size:15pt}p{font-size:12.5pt!important}" +
            "p span{font-size:15pt}</style><section><p style='font-size:large'>" +
            "Service <span>complete</span></p></section>";
        var paragraph = HtmlDocumentReader.Read(keywords, Page, default).Single();
        Assert.That(paragraph.Style.FontSize, Is.EqualTo(12.5).Within(0.000001));
        Assert.That(paragraph.Runs.Single(r => r.Text == "complete").Style.FontSize,
            Is.EqualTo(15).Within(0.000001));
        Assert.That(Pdf(keywords), Is.EqualTo(Pdf(points)));
    }

    [Test]
    public void InheritInitialAndUnsetKeepExistingCascadeSemantics()
    {
        const string actual = "<section style='font-size:x-large'>" +
            "<p style='font-size:inherit'>Inherited</p>" +
            "<p style='font-size:initial'>Initial</p>" +
            "<p style='font-size:unset'>Unset</p></section>";
        const string points = "<section style='font-size:18pt'>" +
            "<p style='font-size:18pt'>Inherited</p>" +
            "<p style='font-size:12pt'>Initial</p>" +
            "<p style='font-size:18pt'>Unset</p></section>";
        Assert.That(Pdf(actual), Is.EqualTo(Pdf(points)));
    }

    [Test]
    public void FortyEntryReportPaginatesAndPreservesTrueTypePdfBytes()
    {
        var actual = ReadExample("report-font-size-keywords.html");
        var control = ReadExample("report-font-size-keywords-oracle.html");
        Assert.That(TextLayout.Layout(
            HtmlDocumentReader.ReadTree(actual, Page, default), Page, default).Count,
            Is.GreaterThan(1));
        Assert.That(Pdf(actual), Is.EqualTo(Pdf(control)),
            "The full 40-entry service report must preserve every PDF glyph and page.");
        Assert.That(Pdf(actual), Is.Not.EqualTo(Pdf("<p>Service entry 01</p>")));

        // The synthetic FontFixture deliberately supports AB/W but not Q;
        // use only its actual glyph repertoire rather than weakening FPDF1504.
        const string prefix = "<section style='font-size:15pt'>";
        var keywordRows = string.Concat(Enumerable.Repeat(
            "<p style='font-size:smaller;margin:0'>AB <span style='font-size:larger'>WA</span></p>", 40));
        var explicitRows = string.Concat(Enumerable.Repeat(
            "<p style='font-size:12.5pt;margin:0'>AB <span style='font-size:15pt'>WA</span></p>", 40));
        const string suffix = "</section>";
        var typeOptions = Page with { Fonts = [PdfFont.FromBytes(FontFixture.Create())] };
        var typePages = TextLayout.Layout(
            HtmlDocumentReader.ReadTree(prefix + keywordRows + suffix, typeOptions, default),
            typeOptions, default);
        Assert.That(typePages.Count, Is.GreaterThan(1));
        Assert.That(Pdf(prefix + keywordRows + suffix, typeOptions),
            Is.EqualTo(Pdf(prefix + explicitRows + suffix, typeOptions)));
    }

    [TestCase("xxx-large")]
    [TestCase("math")]
    [TestCase("largerr")]
    [TestCase("calc(10pt + 2pt)")]
    public void UnsupportedValuesFailWithoutChangingOriginalPdf(string value)
        => Reject("<p style='font-size:" + value + "'>Never written</p>");

    [Test]
    public void RelativeOverflowAndUnmatchedBadRulesFailWithoutDataLoss()
    {
        Reject("<section style='font-size:140pt'><p style='font-size:larger'>Overflow</p></section>");
        Reject("<style>.unused{font-size:xxx-large}</style><p>Visible</p>");
    }

    private static string ReadExample(string name)
    {
        // Resolve fixtures from the checkout regardless of the test working directory.
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "FactsPDF.slnx")))
            folder = folder.Parent;
        Assert.That(folder, Is.Not.Null, "A source checkout is required for report fixtures.");
        return File.ReadAllText(Path.Combine(folder!.FullName, "examples", name));
    }

    private static void Reject(string html)
    {
        using var output = new MemoryStream();
        output.Write("approved-report"u8);
        output.Position = 2;
        var error = Assert.Throws<FactsPdfException>(() =>
            PdfConverter.Convert(html, output, Page));
        Assert.That(error!.Code, Is.EqualTo("FPDF1202"));
        Assert.That(output.ToArray(), Is.EqualTo("approved-report"u8.ToArray()));
        Assert.That(output.Position, Is.EqualTo(2));
    }
}
