using NUnit.Framework;

namespace FactsPDF.Tests;

[TestFixture]
public sealed class NumericFontWeightTests
{
    private static PdfFont Font(int advance)
    {
        var bytes = FontFixture.Create();
        FontFixture.U16(bytes, FontFixture.Table(bytes, "hmtx") + 8, advance);
        return PdfFont.LoadTrueType(bytes);
    }

    private static PdfOptions Roles() => new()
    {
        Fonts = [Font(200)],
        BoldFonts = [Font(450)],
        ItalicFonts = [Font(650)],
        BoldItalicFonts = [Font(850)]
    };

    private static byte[] Render(string html, PdfOptions options)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, options);
        return output.ToArray();
    }

    private static List<LayoutPage> Layout(string html, PdfOptions options)
        => TextLayout.Layout(HtmlDocumentReader.ReadTree(html, options, default), options, default);

    private static FactsPdfException Reject(string html, PdfOptions options, string code)
    {
        using var output = new MemoryStream();
        output.Write("prior-pdf"u8);
        output.Position = 3;
        var ex = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, options))!;
        Assert.Multiple(() =>
        {
            Assert.That(ex.Code, Is.EqualTo(code));
            Assert.That(output.ToArray(), Is.EqualTo("prior-pdf"u8.ToArray()));
            Assert.That(output.Position, Is.EqualTo(3));
        });
        return ex;
    }

    [TestCase("100", "400")]
    [TestCase("200", "400")]
    [TestCase("300", "400")]
    [TestCase("500", "400")]
    [TestCase("600", "700")]
    [TestCase("800", "700")]
    [TestCase("900", "700")]
    public void NumericalHundredsSelectActualNormalOrBoldFontRoles(string weight, string role)
    {
        var options = Roles();
        var html = $"<p style='font-weight:{weight}'>INV-123 <span>REVIEW</span></p>";
        var oracle = $"<p style='font-weight:{role}'>INV-123 <span>REVIEW</span></p>";
        Assert.That(Render(html, options), Is.EqualTo(Render(oracle, options)));
        var expectedFont = role == "700" ? options.BoldFonts[0] : options.Fonts[0];
        Assert.That(Layout(html, options).SelectMany(p => p.Runs).All(r => ReferenceEquals(r.Font, expectedFont)),
            Is.True, "Numeric weight must change the real embedded font role, not just CSS parsing.");
    }

    [TestCase("6e2", "700")]
    [TestCase(".9e3", "700")]
    [TestCase("900.0", "700")]
    [TestCase("1e2", "400")]
    [TestCase("5e2", "400")]
    public void NumericallyEquivalentCssNumberTokensSelectMatchingRole(string token, string role)
    {
        var options = Roles();
        Assert.That(Render($"<p style='font-weight:{token}'>A</p>", options),
            Is.EqualTo(Render($"<p style='font-weight:{role}'>A</p>", options)));
    }

    [Test]
    public void StylesheetCascadeImportantAndCssWideInheritanceSelectActualRoles()
    {
        var options = Roles();
        const string css = "<style>p{font-weight:500}.badge{font-weight:600}.badge{font-weight:900!important}</style>"
            + "<p>Normal <span class=badge style='font-weight:400'>Approved</span></p>"
            + "<p style='font-weight:600'>Parent <span style='font-weight:inherit'>Strong</span>"
            + "<span style='font-weight:initial'>Regular</span></p>";
        const string oracle = "<style>p{font-weight:normal}.badge{font-weight:bold}.badge{font-weight:bold!important}</style>"
            + "<p>Normal <span class=badge style='font-weight:normal'>Approved</span></p>"
            + "<p style='font-weight:bold'>Parent <span style='font-weight:inherit'>Strong</span>"
            + "<span style='font-weight:initial'>Regular</span></p>";
        Assert.That(Render(css, options), Is.EqualTo(Render(oracle, options)));
    }

    [Test]
    public void RealBoldMetricsWrapAndPaginateLikeExplicitBold()
    {
        var options = Roles() with { PageWidth = 105, PageHeight = 105, Margin = 12 };
        var rows = string.Join(' ', Enumerable.Repeat("APPROVED", 160));
        var numeric = $"<p style='font-weight:600'>{rows}</p>";
        var explicitBold = $"<p style='font-weight:700'>{rows}</p>";
        var pages = Layout(numeric, options);
        Assert.That(pages.Count, Is.GreaterThan(1));
        Assert.That(pages.SelectMany(p => p.Runs).All(r => ReferenceEquals(r.Font, options.BoldFonts[0])), Is.True);
        Assert.That(string.Concat(pages.SelectMany(p => p.Runs).Select(r => r.Text)).Count(c => c == 'A'),
            Is.EqualTo(160));
        Assert.That(Render(numeric, options), Is.EqualTo(Render(explicitBold, options)));
        Reject(numeric, options with { MaxPages = 1 }, "FPDF1303");
    }

    [Test]
    public void ANewHeavyWeightRequiresItsOwnFontRoleButFailureDoesNotPoisonRecovery()
    {
        var options = new PdfOptions { Fonts = [Font(220)] };
        var ex = Reject("<p style='font-weight:600'>APPROVED</p>", options, "FPDF1507");
        Assert.That(ex.Message, Does.Contain("bold"));
        Assert.That(Render("<p style='font-weight:500'>Approved</p>", options),
            Is.EqualTo(Render("<p style='font-weight:400'>Approved</p>", options)));
    }

    [TestCase("0")]
    [TestCase("50")]
    [TestCase("450")]
    [TestCase("550")]
    [TestCase("650")]
    [TestCase("950")]
    [TestCase("1000")]
    [TestCase("500%")]
    [TestCase("bolder")]
    [TestCase("lighter")]
    [TestCase("700 400")]
    [TestCase("1e99")]
    public void UnsupportedNumericAndRelativeWeightsFailEvenForAnUnmatchedRule(string value)
        => Reject($"<style>.never{{font-weight:{value}}}</style><p>Previous report protected</p>",
            Roles(), "FPDF1202");
}
