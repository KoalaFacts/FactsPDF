using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// CSS font-family against caller-registered families: name matching (ASCII case-insensitive,
/// quoted or not), generic family mapping, the CSS Fonts 4 matching order for stretch, style and
/// weight, the explicit default chains as the final fallback, and FPDF1507 wherever Chrome would
/// synthesize a bold or italic face.
/// </summary>
[TestFixture]
public sealed class FontFamilyMatchingTests
{
    private const string Neutral = "<style>body{margin:0}p{margin:0}</style>";

    internal static PdfFont Face(int advance, int weight = 400, bool italic = false, int widthClass = 5, string? family = null, bool latinOnly = false)
    {
        var bytes = FontFixture.Create(family: family, latinOnly: latinOnly);
        FontFixture.U16(bytes, FontFixture.Table(bytes, "hmtx") + 8, advance);
        var os2 = FontFixture.Table(bytes, "OS/2");
        FontFixture.U16(bytes, os2 + 4, weight);
        FontFixture.U16(bytes, os2 + 6, widthClass);
        FontFixture.U16(bytes, os2 + 62, italic ? 1 : 0);
        return PdfFont.FromBytes(bytes);
    }

    private static readonly PdfFont Regular = Face(200), Bold = Face(250, 700), Italic = Face(260, italic: true);
    private static readonly PdfFont Brand = Face(400), BrandBold = Face(700, 700), BrandItalic = Face(600, italic: true);

    private static PdfWriter Writer(Action<PdfFontSetup>? more = null) => Pdf.Write().WithFonts(f =>
    {
        f.Regular(Regular).Bold(Bold).Italic(Italic).Family("Report Sans", Brand, BrandBold, BrandItalic);
        more?.Invoke(f);
    });

    internal static List<PlacedText> Runs(PdfWriter writer, string html)
    {
        var options = FontRoles.Snapshot(writer.Options);
        return TextLayout.Layout(HtmlDocumentReader.ReadTree(Neutral + html, options, default), options, default)[0].Runs;
    }

    private static PdfFont[] Fonts(PdfWriter writer, string html) => [.. Runs(writer, html).Select(r => r.Font!)];

    internal static FactsPdfException Fails(PdfWriter writer, string html, string code)
    {
        var error = Assert.Throws<FactsPdfException>(() => writer.ToBytes(html))!;
        Assert.That(error.Code, Is.EqualTo(code), error.Message);
        return error;
    }

    [Test]
    public void ARegisteredFamilyIsSelectedByNameCaseInsensitively()
        => Assert.That(Fonts(Writer(), "<p>A<span style=\"font-family:'report sans'\">A</span><span style='font-family:Report   SANS'>A</span>"
            + "<span style='font-family:Unknown'>A</span></p>"), Is.EqualTo(new[] { Regular, Brand, Brand, Regular }));

    // Chrome skips a family that is not installed, but always resolves a generic family: the
    // explicit default chain stands in for an unmapped one, so later entries are never reached.
    [TestCase("Missing, 'Report Sans'", "brand")]
    [TestCase("monospace, 'Report Sans'", "default")]
    [TestCase("'monospace', 'Report Sans'", "brand")]
    [TestCase("serif", "default")]
    public void UnknownFamiliesAreSkippedButAnUnmappedGenericEndsTheList(string families, string expected)
        => Assert.That(Fonts(Writer(), $"<p style=\"font-family:{families}\">A</p>")[0], Is.SameAs(expected == "brand" ? Brand : Regular));

    [Test]
    public void GenericFamiliesMapToRegisteredFamilies()
    {
        var writer = Writer(f => f.Generic("sans-serif", "Report Sans"));
        Assert.That(Fonts(writer, "<p style='font-family:sans-serif'>A<span style='font-family:serif'>A</span></p>"),
            Is.EqualTo(new[] { Brand, Regular }));
    }

    [TestCase(100, 300)]
    [TestCase(350, 300)]
    [TestCase(400, 400)]
    [TestCase(450, 400)]
    [TestCase(500, 400)]
    [TestCase(550, 700)]
    [TestCase(600, 700)]
    [TestCase(800, 900)]
    [TestCase(1000, 900)]
    public void WeightMatchingFollowsTheCssFontsOrder(int weight, int advance)
    {
        var writer = Pdf.Write().WithFonts(f => f.Regular(Regular)
            .Family("Weights", Face(300, 300), Face(400, 400), Face(700, 700), Face(900, 900)));
        Assert.That(Fonts(writer, $"<p style='font-family:Weights;font-weight:{weight}'>A</p>")[0].Width1000(2), Is.EqualTo(advance));
    }

    [Test]
    public void AFamilyWithoutABoldFaceFailsInsteadOfSynthesizing()
    {
        var writer = Pdf.Write().WithFonts(f => f.Regular(Regular).Bold(Bold).Family("Plain", Brand));
        Assert.That(Fails(writer, "<p style='font-family:Plain'><b>A</b></p>", "FPDF1507").Message, Does.Contain("synthesize bold"));
        Fails(writer, "<h1 style='font-family:Plain'>A</h1>", "FPDF1507");
        Fails(writer, "<p style='font-family:Plain;font-weight:600'>A</p>", "FPDF1507");
        // Over-correction guard: a slightly heavier request is drawn with the regular face, as Chrome draws it.
        Assert.That(Fonts(writer, "<p style='font-family:Plain;font-weight:500'>A</p>")[0], Is.SameAs(Brand));
    }

    [Test]
    public void ItalicAndObliqueUseTheItalicFaceAndAnUprightFamilyFails()
    {
        Assert.That(Fonts(Writer(), "<p style=\"font-family:'Report Sans'\"><i>A</i><span style='font-style:oblique'>A</span>"
            + "<span style='font-style:oblique 20deg'>A</span><span style='font-style:oblique 0deg'>A</span></p>"),
            Is.EqualTo(new[] { BrandItalic, BrandItalic, BrandItalic, Brand }));
        var upright = Pdf.Write().WithFonts(f => f.Regular(Regular).Italic(Italic).Family("Plain", Brand));
        Assert.That(Fails(upright, "<p style='font-family:Plain'><em>A</em></p>", "FPDF1507").Message, Does.Contain("synthesize italic"));
    }

    // Stretch narrows first: a condensed bold face wins a condensed request even at weight 400
    // (drawn as it is, since it is heavier rather than lighter than requested).
    [TestCase("condensed", 700)]
    [TestCase("80%", 700)]
    [TestCase("normal", 400)]
    [TestCase("semi-expanded", 400)]
    public void StretchNarrowsBeforeStyleAndWeight(string stretch, int advance)
    {
        var writer = Pdf.Write().WithFonts(f => f.Regular(Regular).Family("Widths", Face(400), Face(700, 700, widthClass: 3)));
        Assert.That(Fonts(writer, $"<p style='font-family:Widths;font-stretch:{stretch}'>A</p>")[0].Width1000(2), Is.EqualTo(advance));
    }

    [Test]
    public void TheDefaultChainStillDrawsCharactersTheFamilyLacks()
    {
        var latin = Face(400, latinOnly: true);
        var writer = Pdf.Write().WithFonts(f => f.Regular(Regular).Family("Latin", latin));
        Assert.That(Fonts(writer, "<p style='font-family:Latin'>A中</p>"), Is.EqualTo(new[] { latin, Regular }));
    }

    [Test]
    public void FacesRegisterUnderTheirNameTableFamily()
    {
        var named = Face(400, family: "Fixture Sans");
        Assert.That(named.FamilyName, Is.EqualTo("Fixture Sans"));
        var writer = Pdf.Write().WithFonts(f => f.Regular(Regular).Faces(named));
        Assert.That(Fonts(writer, "<p style='font-family:\"FIXTURE sans\"'>A</p>")[0], Is.SameAs(named));
    }

    [Test]
    public void RegistrationIsValidated()
    {
        Assert.Throws<ArgumentException>(() => Pdf.Write().WithFonts(f => f.Generic("handwriting", "x")));
        Assert.Throws<ArgumentException>(() => Pdf.Write().WithFonts(f => f.Family("serif", Brand)));
        var unmapped = Pdf.Write().WithFonts(f => f.Regular(Regular).Generic("monospace", "Nowhere"));
        Assert.Throws<ArgumentException>(() => unmapped.ToBytes("<p>A</p>"));
        Assert.Throws<ArgumentException>(() => Pdf.Write().WithFontResource("has space", Brand));
    }

    [Test]
    public void UnregisteredFamilyListsKeepTheExactPage()
    {
        var writer = Pdf.Write().WithFonts(f => f.Regular(Regular));
        const string body = "<p>Total <code>x</code></p><pre>y</pre>";
        Assert.That(writer.ToBytes("<style>body{font-family:'Helvetica Neue',Arial,sans-serif;font-stretch:condensed}</style>" + body),
            Is.EqualTo(writer.ToBytes(body)));
    }

    // Chrome sizes a lone generic monospace from its 13px monospace default unless the size is
    // absolute, which FactsPDF does not implement; the user-agent code, kbd, samp and pre use it.
    [Test]
    public void AMappedMonospaceAloneNeedsAnAbsoluteFontSize()
    {
        var mono = Face(500);
        var writer = Pdf.Write().WithFonts(f => f.Regular(Regular).Family("Mono", mono).Generic("monospace", "Mono"));
        Fails(writer, "<p>A <code>B</code></p>", "FPDF1202");
        Fails(writer, "<p style='font-family:monospace'>A</p>", "FPDF1202");
        Assert.That(Fonts(writer, "<style>code{font-family:monospace,monospace}</style><p><code>A</code></p>")[0], Is.SameAs(mono));
        Assert.That(Fonts(writer, "<style>body{font-size:12pt}</style><p><code>A</code></p>")[0], Is.SameAs(mono));
        Fails(writer, "<style>body{font-size:12pt}</style><p><code style='font-size:medium'>A</code></p>", "FPDF1202");
        // Unmapped, monospace keeps the default chain at the inherited size, as before.
        Assert.That(Fonts(Pdf.Write().WithFonts(f => f.Regular(Regular)), "<p><code>A</code></p>")[0], Is.SameAs(Regular));
    }

    [Test]
    public void PageMarginBoxesRefuseARegisteredFamily()
    {
        Fails(Writer(), "<style>@page{@top-center{content:'A'}}html{font-family:'Report Sans'}</style><p>A</p>", "FPDF1201");
        Assert.DoesNotThrow(() => Writer().ToBytes("<style>@page{@top-center{content:'A';font-family:Unknown}}html{font-family:'Report Sans'}</style><p>A</p>"));
    }

    // font-weight is any number from 1 to 1000; the default chains are bold above 500. bolder and
    // lighter follow the CSS Fonts 4 table, so lighter than 900 is 700 (still bold).
    [TestCase("450", "normal")]
    [TestCase("500.5", "bold")]
    [TestCase("550", "bold")]
    [TestCase("1", "normal")]
    [TestCase("1000", "bold")]
    public void NumericWeightsSelectTheDefaultChainsByMatching(string weight, string role)
        => Assert.That(Fonts(Writer(), $"<p style='font-weight:{weight}'>A</p>")[0], Is.SameAs(role == "bold" ? Bold : Regular));

    [TestCase("900", "lighter", "bold")]
    [TestCase("700", "lighter", "normal")]
    [TestCase("300", "bolder", "normal")]
    [TestCase("400", "bolder", "bold")]
    public void RelativeWeightsFollowTheCssFontsTable(string parent, string relative, string role)
        => Assert.That(Fonts(Writer(), $"<p style='font-weight:{parent}'><span style='font-weight:{relative}'>A</span></p>")[0],
            Is.SameAs(role == "bold" ? Bold : Regular));

    [TestCase("font-weight:0")]
    [TestCase("font-weight:1001")]
    [TestCase("font-style:oblique -10deg")]
    [TestCase("font-style:oblique 91deg")]
    [TestCase("font-stretch:-5%")]
    [TestCase("font-stretch:wide")]
    public void OutOfRangeAndBackslantValuesFail(string css) => Fails(Writer(), $"<p style='{css}'>A</p>", "FPDF1202");
}
