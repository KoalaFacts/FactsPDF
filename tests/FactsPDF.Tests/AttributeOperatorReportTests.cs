using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

[TestFixture]
public sealed class AttributeOperatorReportTests
{
    private static byte[] Pdf(string html, PdfOptions? options = null)
        => TableLayoutTests.Pdf(html, options ?? new PdfOptions());

    private static List<LayoutPage> Layout(string html, PdfOptions? options = null)
        => TableLayoutTests.Layout(html, options ?? new PdfOptions());

    [TestCase("p[data-code^=INV-]", "INV-007", "XINV-007")]
    [TestCase("p[data-code$=\"-PAID\"]", "INV-PAID", "PAID-INV")]
    [TestCase("p[data-code*=\"X-4\"]", "INV-X-42", "INV-42")]
    [TestCase("p[data-code~=priority]", "normal priority urgent", "priorities")]
    [TestCase("p[lang|=en]", "en-AU", "english")]
    [TestCase("p[lang|=en]", "en", "enAU")]
    [TestCase("p[data-code^=\"inv-\" i]", "INV-007", "XINV-007")]
    [TestCase("p[data-code$=\"-paid\" I]", "INV-PAID", "PAID-INV")]
    [TestCase("p[data-code*=\"x-4\" i]", "INV-X-42", "INV-42")]
    [TestCase("p[data-code~=priority i]", "normal PRIORITY urgent", "priorities")]
    [TestCase("p[lang|=en i]", "EN-au", "english")]
    [TestCase("p[data-code=paid s]", "paid", "PAID")]
    [TestCase("p[data-code=paid i]", "PAID", "PENDING")]
    [TestCase("p[data-code^=INV-]", "INV-007", "inv-007")]
    public void AttributeOperatorMatchesRealPdfInkAndRejectsNonmatch(string selector, string match, string miss)
    {
        var attr = selector.Contains("[lang", StringComparison.Ordinal) ? "lang" : "data-code";
        var actual = "<style>" + selector + "{color:red}</style>" +
            "<p " + attr + "='" + match + "'>MATCH</p>" +
            "<p " + attr + "='" + miss + "'>MISS</p>" +
            "<p>ABSENT</p>";
        const string control = "<p style='color:red'>MATCH</p><p>MISS</p><p>ABSENT</p>";
        Assert.That(Pdf(actual), Is.EqualTo(Pdf(control)));
        Assert.That(Layout(actual).SelectMany(page => page.Runs)
            .Select(run => run.Style.Color), Is.EqualTo(new[]
            {
                new Rgb(1, 0, 0), new Rgb(0, 0, 0), new Rgb(0, 0, 0)
            }));
    }

    [Test]
    public void EmptyWhitespaceAndAsciiFlagsRespectSelectorsSemantics()
    {
        const string actual = "<style>" +
            "p[data-tags~=urgent]{color:red}" +
            "p[data-tags*=\"PRIORITY\" i]{background:#ffeecc}" +
            "p[data-tags~=urgent s]{border-left:2pt solid red}" +
            "p[title=\"é\" i]{padding-left:3pt}" +
            "</style>" +
            "<p data-tags='info&#9;urgent&#10;PRIORITY' title='É'>CHECK</p>" +
            "<p data-tags='urgently'>OTHER</p>";
        const string control = "<p style='color:red;background:#ffeecc;" +
            "border-left:2pt solid red'>CHECK</p><p>OTHER</p>";
        Assert.That(Pdf(actual), Is.EqualTo(Pdf(control)));
    }

    [Test]
    public void CompoundCascadeImportantAndAncestorsStyleActualPageGeometry()
    {
        const string actual = "<style>" +
            "article[data-kind|=invoice] > p[data-state^=late i]{padding-left:9pt;color:blue}" +
            "p[data-state*=late]{color:red}" +
            "article[data-kind|=invoice] p[data-state$=late i]{color:green!important}" +
            "</style><article data-kind=invoice-2026>" +
            "<p data-state=LATE style='color:red'>LATE</p>" +
            "<div><p data-state=late>INNER</p></div></article>";
        const string control = "<article data-kind=invoice-2026>" +
            "<p style='padding-left:9pt;color:green'>LATE</p>" +
            "<div><p style='color:green'>INNER</p></div></article>";
        Assert.That(Pdf(actual), Is.EqualTo(Pdf(control)));
    }

    [Test]
    public void FortyFourEntryReportChangesActualMultiPageTextAndBorderGeometry()
    {
        var options = new PdfOptions { PageWidth = 300, PageHeight = 190, Margin = 12 };
        var actual = new StringBuilder("<style>" +
            "article[data-kind|=invoice]{padding:4pt;background:#f5f5ff}" +
            "article[data-kind|=invoice] p[data-state^=overdue i]{" +
            "color:#b22222;border-left:2pt solid #b22222;padding-left:4pt}" +
            "</style><article data-kind=invoice-2026>");
        var oracle = new StringBuilder("<article style='padding:4pt;background:#f5f5ff'>");
        for (var i = 1; i <= 44; i++)
        {
            var number = i.ToString("D2", System.Globalization.CultureInfo.InvariantCulture);
            var overdue = i % 3 == 0;
            var status = overdue ? i % 2 == 0 ? "OVERDUE" : "overdue" : "pending";
            var text = "INVOICE ITEM " + number;
            actual.Append("<p data-state=").Append(status).Append('>').Append(text).Append("</p>");
            oracle.Append(overdue ? "<p style='color:#b22222;border-left:2pt solid #b22222;padding-left:4pt'>" : "<p>")
                .Append(text).Append("</p>");
        }
        actual.Append("</article>");
        oracle.Append("</article>");
        Assert.That(Pdf(actual.ToString(), options), Is.EqualTo(Pdf(oracle.ToString(), options)));
        var pages = Layout(actual.ToString(), options);
        Assert.That(pages, Has.Count.GreaterThan(2));
        Assert.That(pages.SelectMany(page => page.Runs)
            .Count(run => run.Text.StartsWith("INVOICE ITEM ", StringComparison.Ordinal)), Is.EqualTo(44));
        Assert.That(pages.SelectMany(page => page.PaintBoxes).Any(), Is.True);
    }

    [Test]
    public void SubstringAttributeSelectorSizesExplicitImage()
    {
        var options = new PdfOptions
        {
            Images = new Dictionary<string, PdfImage>
            {
                ["chart"] = PdfImage.FromBytes(JpegResourceTests.Bytes("rgb-420"))
            }
        };
        const string actual = "<style>img[data-role$=\"-mark\"]{width:80pt}</style>" +
            "<img src=chart data-role=brand-mark><p>IMAGE</p>";
        const string oracle = "<img src=chart style='width:80pt'><p>IMAGE</p>";
        Assert.That(Pdf(actual, options), Is.EqualTo(Pdf(oracle, options)));
        Assert.That(Layout(actual, options).Single().Images.Single().Width, Is.EqualTo(80));
    }

    [Test]
    public void CaseInsensitiveSelectorCanStyleSearchableTrueTypeGlyphs()
    {
        var options = new PdfOptions { Fonts = [PdfFont.FromBytes(FontFixture.Create())] };
        const string actual = "<style>p[lang|=zh i]{color:red}</style><p lang=ZH-Hans>中文</p>";
        const string oracle = "<p style='color:red'>中文</p>";
        Assert.That(Pdf(actual, options), Is.EqualTo(Pdf(oracle, options)));
        Assert.That(Layout(actual, options).Single().Runs.Single().Text, Is.EqualTo("中文"));
    }

    [TestCase("p[data-state^ =overdue]")]
    [TestCase("p[data-state * =due]")]
    [TestCase("p[data-state^=\"\"]")]
    [TestCase("p[data-state~=\"\"]")]
    [TestCase("p[data-state$=due q]")]
    [TestCase("p[data-state*=due i s]")]
    [TestCase("p[data-state*=due ii]")]
    [TestCase("p[data-state!=due]")]
    [TestCase("p[unknown|=en]")]
    [TestCase("p[data-state=\"[\" \"i\"]")]
    public void InvalidAttributeOperatorsAndFlagsDoNotReplacePdfOrMoveOutput(string selector)
    {
        using var output = new MemoryStream();
        output.Write("original PDF"u8);
        output.Position = 3;
        var saved = output.ToArray();
        var ex = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(
            "<style>" + selector + "{color:red}</style><p data-state=overdue>TEXT</p>", output));
        Assert.That(ex!.Code, Is.EqualTo("FPDF1203"));
        Assert.That(output.ToArray(), Is.EqualTo(saved));
        Assert.That(output.Position, Is.EqualTo(3));
    }
}
