using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

[TestFixture]
public sealed class AttributeSelectorReportTests
{
    private static byte[] Pdf(string html, PdfOptions? options = null)
        => TableLayoutTests.Pdf(html, options ?? new PdfOptions());

    private static List<LayoutPage> Layout(string html, PdfOptions? options = null)
        => TableLayoutTests.Layout(html, options ?? new PdfOptions());

    [Test]
    public void PresenceExactValueAndCaseSensitiveValuePaintRealPdf()
    {
        const string actual = "<style>p[data-state]{color:blue}" +
            "p[data-state=\"paid\"]{color:red}</style>" +
            "<p data-state=paid>PAID</p><p data-state=PENDING>PENDING</p><p>OTHER</p>";
        const string control = "<p style='color:red'>PAID</p>" +
            "<p style='color:blue'>PENDING</p><p>OTHER</p>";
        Assert.That(Pdf(actual), Is.EqualTo(Pdf(control)));
        var runs = Layout(actual).SelectMany(page => page.Runs).ToArray();
        Assert.That(runs.Select(run => run.Style.Color), Is.EqualTo(new[]
        {
            new Rgb(1, 0, 0), new Rgb(0, 0, 1), new Rgb(0, 0, 0)
        }));
    }

    [Test]
    public void AttributeOnlyAndWhitespaceQuotedValuesWorkWithCascadeAndAncestors()
    {
        const string actual = "<style>" +
            "[data-report] > p[title = \"Overdue notice\"]{color:red}" +
            "p.notice{color:blue}" +
            "section[data-report=invoice] p[data-state=paid]{color:green!important}" +
            "</style><section data-report=invoice>" +
            "<p class=notice title='Overdue notice'>OVERDUE</p>" +
            "<div><p data-state=paid style='color:red'>PAID</p></div>" +
            "</section>";
        const string control = "<section data-report=invoice>" +
            "<p style='color:red' title='Overdue notice'>OVERDUE</p>" +
            "<div><p style='color:green' data-state=paid>PAID</p></div></section>";
        // Two attribute terms outrank one class; important outranks inline.
        Assert.That(Pdf(actual), Is.EqualTo(Pdf(control)));
    }

    [Test]
    public void FortyFourItemQuotationRetainsActualMultiPageTextAndBoxPaint()
    {
        var options = new PdfOptions { PageWidth = 300, PageHeight = 180, Margin = 12 };
        var actual = new StringBuilder("<style>p[data-band=late]{font-size:10pt;" +
            "line-height:20pt;color:#b22222;border-left:2pt solid red;padding-left:3pt}" +
            "</style>");
        var control = new StringBuilder();
        for (var i = 1; i <= 44; i++)
        {
            var text = "REPORT ITEM " + i.ToString("D2", System.Globalization.CultureInfo.InvariantCulture);
            actual.Append("<p data-band=late>").Append(text).Append("</p>");
            control.Append("<p style='font-size:10pt;line-height:20pt;color:#b22222;")
                .Append("border-left:2pt solid red;padding-left:3pt'>")
                .Append(text).Append("</p>");
        }
        var actualPdf = Pdf(actual.ToString(), options);
        Assert.That(actualPdf, Is.EqualTo(Pdf(control.ToString(), options)));
        var pages = Layout(actual.ToString(), options);
        Assert.That(pages.Count, Is.GreaterThan(2));
        Assert.That(pages.SelectMany(page => page.Runs).Count(run =>
            run.Text.StartsWith("REPORT ITEM ", StringComparison.Ordinal)), Is.EqualTo(44));
    }

    [Test]
    public void RealHtmlReportTemplateMatchesIndependentExplicitStyleOracle()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory,
            "..", "..", "..", "..", ".."));
        var actualPath = Path.Combine(repoRoot, "examples", "report-attribute-style-overrides.html");
        var oraclePath = Path.Combine(repoRoot, "examples", "report-attribute-style-overrides-oracle.html");
        var actual = File.ReadAllText(actualPath);
        var control = File.ReadAllText(oraclePath);
        var pages = Layout(actual);
        Assert.That(pages.Count, Is.GreaterThan(1));
        Assert.That(pages.SelectMany(page => page.Runs).Count(run =>
            run.Text.StartsWith("Invoice 0", StringComparison.Ordinal)), Is.EqualTo(44));
        Assert.That(Pdf(actual), Is.EqualTo(Pdf(control)));
    }

    [Test]
    public void IdEqualitySelectorChangesActualPdfInk()
    {
        const string actual = "<style>p[id=paid]{color:red}</style><p id=paid>PAID</p>";
        const string control = "<p id=paid style='color:red'>PAID</p>";
        Assert.That(Pdf(actual), Is.EqualTo(Pdf(control)));
    }

    [Test]
    public void AttributeSelectorActuallySizesAnExplicitImageResource()
    {
        var options = new PdfOptions
        {
            Images = new Dictionary<string, PdfImage>
            {
                ["chart"] = PdfImage.FromBytes(JpegResourceTests.Bytes("rgb-420"))
            }
        };
        const string actual = "<style>img[data-role=logo]{width:80pt}</style>" +
            "<img src=chart data-role=logo><p>AFTER IMAGE</p>";
        const string control = "<img src=chart style='width:80pt'><p>AFTER IMAGE</p>";
        Assert.That(Pdf(actual, options), Is.EqualTo(Pdf(control, options)));
        Assert.That(Layout(actual, options).Single().Images.Single().Width, Is.EqualTo(80));
    }

    [Test]
    public void TrueTypeAttributeStylingControlsSearchableUnicodeText()
    {
        var options = new PdfOptions { Fonts = [PdfFont.FromBytes(FontFixture.Create())] };
        const string actual = "<style>p[lang=zh]{color:red}</style><p lang=zh>中文</p>";
        const string control = "<p style='color:red'>中文</p>";
        Assert.That(Pdf(actual, options), Is.EqualTo(Pdf(control, options)));
        Assert.That(Layout(actual, options).Single().Runs.Single().Text, Is.EqualTo("中文"));
    }

    [TestCase("p[data-state^ =paid]")]
    [TestCase("p[data-state*=\"\"]")]
    [TestCase("p[data-state=paid q]")]
    [TestCase("p[data-state~=paid ii]")]
    [TestCase("p[data-state \"=\" paid]")]
    [TestCase("p[data-state \"]\"\"[\"title]")]
    [TestCase("p[unknown]")]
    public void MalformedOperatorsFlagsAndAttributesRejectWithoutReplacingOutput(string selector)
    {
        using var output = new MemoryStream();
        output.Write("previous PDF"u8);
        output.Position = 3;
        var previous = output.ToArray();
        var ex = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(
            "<style>" + selector + "{color:red}</style><p data-state=paid>TEXT</p>", output));
        Assert.That(ex!.Code, Is.EqualTo("FPDF1203"));
        Assert.That(output.ToArray(), Is.EqualTo(previous));
        Assert.That(output.Position, Is.EqualTo(3));
    }
}
