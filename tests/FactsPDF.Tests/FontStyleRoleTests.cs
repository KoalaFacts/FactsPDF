using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

[TestFixture]
public sealed class FontStyleRoleTests
{
    private static PdfFont Font(int advance = 600, bool latinOnly = false, int ascent = 800, int descent = -200)
    {
        var bytes = FontFixture.Create(latinOnly: latinOnly);
        FontFixture.U16(bytes, FontFixture.Table(bytes, "hmtx") + 8, advance);
        FontFixture.U16(bytes, FontFixture.Table(bytes, "OS/2") + 62, 0);
        FontFixture.U16(bytes, FontFixture.Table(bytes, "hhea") + 4, ascent);
        FontFixture.U16(bytes, FontFixture.Table(bytes, "hhea") + 6, descent);
        return PdfFont.LoadTrueType(bytes);
    }

    private static PdfOptions AllRoles() => new()
    {
        Fonts = [Font(200)], BoldFonts = [Font(400)],
        ItalicFonts = [Font(600)], BoldItalicFonts = [Font(800)]
    };

    private static List<LayoutPage> Layout(string html, PdfOptions options)
        => TextLayout.Layout(HtmlDocumentReader.ReadTree(html, options, default), options, default);

    private static byte[] Pdf(string html, PdfOptions options)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, options);
        return output.ToArray();
    }

    private static FactsPdfException Fails(string html, PdfOptions options, string code)
    {
        using var output = new MemoryStream();
        output.Write([10, 20, 30]); output.Position = 1;
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, options))!;
        Assert.Multiple(() =>
        {
            Assert.That(error.Code, Is.EqualTo(code));
            Assert.That(output.ToArray(), Is.EqualTo(new byte[] { 10, 20, 30 }));
            Assert.That(output.Position, Is.EqualTo(1));
        });
        return error;
    }

    [TestCase("BoldFonts")]
    [TestCase("ItalicFonts")]
    [TestCase("BoldItalicFonts")]
    public void StyleChainsDefaultToEmpty(string name)
    {
        var property = typeof(PdfOptions).GetProperty(name);
        Assert.That(property, Is.Not.Null);
        Assert.That(property!.PropertyType, Is.EqualTo(typeof(IReadOnlyList<PdfFont>)));
        Assert.That(property.GetValue(new PdfOptions()), Is.Empty);
    }

    [Test]
    public void SameScalarUsesEachRoleAndItsActualAdvanceWithoutPairingByName()
    {
        var options = AllRoles();
        var runs = Layout("<p>A<b>A</b><i>A</i><strong><em>A</em></strong>A</p>", options)[0].Runs;
        Assert.That(runs.Select(r => r.Font), Is.EqualTo(new[] {
            options.Fonts[0], options.BoldFonts[0], options.ItalicFonts[0], options.BoldItalicFonts[0], options.Fonts[0] }));
        Assert.That(runs.Select(r => r.X), Is.EqualTo(new[] { 36d, 38.4, 43.2, 50.4, 60d }).Within(0.00001));
        Assert.That(runs.Select(r => r.Font!.PostScriptName).Distinct().Count(), Is.EqualTo(1),
            "Identical PostScript names must not merge distinct explicit font objects.");
        var pdf = Encoding.Latin1.GetString(Pdf("<p>A<b>A</b><i>A</i><b><i>A</i></b></p>", options));
        Assert.That(System.Text.RegularExpressions.Regex.Matches(pdf, "/Subtype /Type0").Count, Is.EqualTo(4));
    }

    [Test]
    public void TagAliasesAndCssWeightStyleHaveIdenticalOutput()
    {
        var options = AllRoles();
        var tags = Pdf("<p>A<b>A</b><i>A</i><b><i>A</i></b></p>", options);
        Assert.That(Pdf("<p>A<strong>A</strong><em>A</em><em><strong>A</strong></em></p>", options), Is.EqualTo(tags));
        Assert.That(Pdf("<p>A<span style='font-weight:700.0'>A</span><span style='font-style:italic'>A</span><span style='font-style:italic;font-weight:bold'>A</span></p>", options), Is.EqualTo(tags));
    }

    [Test]
    public void CascadeResetsIndependentDimensionsAndCoalescesAcrossProvenance()
    {
        var options = AllRoles();
        var html = "<style>.reset{font-weight:normal!important} .inherit{font-weight:inherit;font-style:unset}</style>" +
            "<p><b><i>A<span class=reset style='font-weight:bold'>A</span>" +
            "<span style='font-style:initial'>A</span><span class=inherit>A</span></i></b>" +
            "<span>A</span><span>A</span></p>";
        var runs = Layout(html, options)[0].Runs;
        Assert.That(runs.Select(r => r.Font), Is.EqualTo(new[] {
            options.BoldItalicFonts[0], options.ItalicFonts[0], options.BoldFonts[0], options.BoldItalicFonts[0], options.Fonts[0] }));
        Assert.That(runs[^1].Text, Is.EqualTo("AA"));
        Assert.That(Pdf("<p><b><b>A</b></b><i><i>A</i></i></p>", options),
            Is.EqualTo(Pdf("<p><b>A</b><i>A</i></p>", options)));
    }

    [TestCase("b", "bold")]
    [TestCase("i", "italic")]
    [TestCase("b><i", "bold-italic")]
    public void MissingRoleCannotBorrowNormalOrAnotherRole(string tag, string role)
    {
        var options = new PdfOptions { Fonts = [Font()] };
        var html = tag == "b><i" ? "<p><b><i>A</i></b></p>" : $"<p><{tag}>A</{tag}></p>";
        var error = Fails(html, options, "FPDF1507");
        Assert.That(error.Message, Does.Contain(role));
        Assert.That(error.SourceOffset, Is.EqualTo(tag == "b><i" ? 6 : 3));
        if (role == "bold-italic")
            Fails(html, options with { BoldFonts = [Font()], ItalicFonts = [Font()] }, "FPDF1507");
    }

    [TestCase("b")][TestCase("i")][TestCase("b><i")]
    public void EachRoleHasAnIndependentOrderedGlyphFallback(string tag)
    {
        var first = Font(400, latinOnly: true); var second = Font(800);
        var options = tag switch {
            "b" => new PdfOptions { BoldFonts = [first, second] },
            "i" => new PdfOptions { ItalicFonts = [first, second] },
            _ => new PdfOptions { BoldItalicFonts = [first, second] }
        };
        var html = tag == "b><i" ? "<p><b><i>A中A</i></b></p>" : $"<p><{tag}>A中A</{tag}></p>";
        var runs = Layout(html, options)[0].Runs;
        Assert.That(runs.Select(r => r.Font), Is.EqualTo(new[] { first, second, first }));
        Assert.That(Pdf(html, options), Is.Not.Empty);
    }

    [TestCase(false)][TestCase(true)]
    public void SoleStyledChainCanMixWithFontlessCourier(bool subset)
    {
        var options = new PdfOptions { ItalicFonts = [Font()], SubsetFonts = subset };
        var pdf = Encoding.Latin1.GetString(Pdf("<p>Plain <i>中文</i> end</p>", options));
        Assert.That(pdf, Does.Contain("/BaseFont /Courier"));
        Assert.That(pdf, Does.Contain("/Subtype /Type0"));
        Assert.That(pdf, Does.Contain("<506C61696E20> Tj"));
        Assert.That(pdf, Does.Contain("<20656E64> Tj"));
        Fails("<p>中文</p>", options, "FPDF1301");
    }

    [Test]
    public void StyledMissingGlyphDoesNotBorrowNormalAndNormalDiagnosticStaysCompatible()
    {
        var options = new PdfOptions { Fonts = [Font()], ItalicFonts = [Font(latinOnly: true)] };
        var styled = Fails("<p><em>中</em></p>", options, "FPDF1504");
        Assert.That(styled.Message, Does.Contain("italic").And.Contain("U+4E2D"));
        Assert.That(styled.SourceOffset, Is.EqualTo(3));
        var normal = Fails("<p>中</p>", options with { Fonts = options.ItalicFonts }, "FPDF1504");
        Assert.That(normal.Message, Is.EqualTo("FPDF1504: No supplied font contains U+4E2D."));
        Assert.That(normal.SourceOffset, Is.EqualTo(-1));
    }

    [Test]
    public void EmptyInlineDoesNotRequireFontsButParagraphStrutDoes()
    {
        Pdf("<p>A<b></b><em> </em></p>", new PdfOptions { ItalicFonts = [Font()] });
        Pdf("<p style='font-weight:bold'></p>", new());
        var error = Fails("<p style='font-weight:bold'><span style='font-weight:normal'>A</span></p>", new(), "FPDF1507");
        Assert.That(error.SourceOffset, Is.Zero);
        Fails("<p style='font-style:italic'><br></p>", new(), "FPDF1507");
        Fails("<p>A<i> </i></p>", new(), "FPDF1507"); // legacy pending-space validation
    }

    [Test]
    public void RoleMetricsUseFirstFontWhileParagraphStrutAndInlineStylesContribute()
    {
        var regular = Font(); var italic = Font(latinOnly: true, ascent: 1200, descent: -200);
        var fallback = Font(ascent: 2000, descent: -1000);
        var options = new PdfOptions { Fonts = [regular], ItalicFonts = [italic, fallback] };
        var plain = Layout("<p><i>A</i></p><p>A</p>", options)[0].Runs;
        var mixed = Layout("<p><i>A中</i></p><p>A</p>", options)[0].Runs;
        Assert.That(mixed[0].Baseline, Is.EqualTo(plain[0].Baseline).Within(0.00001));
        Assert.That(mixed[1].Baseline, Is.EqualTo(plain[0].Baseline).Within(0.00001));
        Assert.That(mixed[2].Baseline, Is.EqualTo(plain[1].Baseline).Within(0.00001));
        Assert.That(mixed[0].Baseline, Is.EqualTo(options.PageHeight - options.Margin - 13.2).Within(0.00001));
    }

    [Test]
    public void ActualRoleAdvancesControlWrappingAndDoNotCreateWordBreaks()
    {
        var options = AllRoles() with { PageWidth = 72, Margin = 12 };
        Fails("<p>AAAA<b>AAAAAAAAA</b></p>", options, "FPDF1302");
        var runs = Layout("<p>A <b>AAAAAAAAA</b></p>", options)[0].Runs;
        Assert.That(runs[0].Baseline, Is.GreaterThan(runs[^1].Baseline));
        Fails("<p>A&#160;<b>AAAAAAAAA</b></p>", options, "FPDF1302");
    }

    [Test]
    public void StyledLongRunPaginatesWithoutLossAndRespectsLimits()
    {
        var options = AllRoles() with { PageWidth = 72, PageHeight = 72, Margin = 12 };
        var html = "<p style='font-weight:bold;font-style:italic'>" + string.Join(' ', Enumerable.Repeat("A", 90)) + "</p>";
        var pages = Layout(html, options);
        Assert.That(pages.Count, Is.GreaterThan(1));
        Assert.That(pages.SelectMany(p => p.Runs).All(r => ReferenceEquals(r.Font, options.BoldItalicFonts[0])), Is.True);
        Assert.That(string.Concat(pages.SelectMany(p => p.Runs).Select(r => r.Text)).Count(c => c == 'A'), Is.EqualTo(90));
        Fails(html, options with { MaxPages = 1 }, "FPDF1303");
        Fails(html, options with { MaxDisplayCommands = 1 }, "FPDF1401");
        Fails(html, options with { MaxOutputBytes = 100 }, "FPDF1401");
    }

    [TestCase("font-weight:650")][TestCase("font-weight:bolder")][TestCase("font-weight:400%")]
    [TestCase("font-style:oblique")][TestCase("font-style:oblique 10deg")]
    [TestCase("font-style:bold")][TestCase("font-weight:lighter")]
    public void UnsupportedValuesAreRejectedEvenInUnmatchedRules(string css)
        => Fails($"<style>.absent{{{css}}}</style><p>A</p>", AllRoles(), "FPDF1202");

    [TestCase("p")][TestCase(".absent")]
    public void NumericLookingIdentifiersAreNotWeightNumbers(string selector)
        => Fails($"<style>{selector}{{font-weight:\\37 00}}</style><p>A</p>", AllRoles(), "FPDF1202");

    [TestCase("p")][TestCase(".absent")]
    public void EscapedStyleIdentifierCannotIntroduceAnotherDeclaration(string selector)
        => Fails($"<style>{selector}{{font-style:italic\\3b color\\3a red}}</style><p>A</p>", AllRoles(), "FPDF1202");

    [TestCase("4e2", "400")][TestCase("7e+2", "700")][TestCase(".7e3", "700")]
    [TestCase(@"b\6fld", "700")][TestCase(@"\6e ormal", "400")]
    public void EquivalentNumberTokensAndEscapedKeywordsAreAccepted(string value, string canonical)
    {
        var options = AllRoles();
        Assert.That(Pdf($"<p style='font-weight:{value}'>A</p>", options),
            Is.EqualTo(Pdf($"<p style='font-weight:{canonical}'>A</p>", options)));
    }

    [TestCase("599.99999999999999")]
    [TestCase("900.00000000000001")]
    [TestCase("100.0000000000000000000000000000000000001")]
    [TestCase("99.9999999999999999999999999999999999999")]
    [TestCase("6.0000000000000000000000000000000000001e2")]
    [TestCase("899999999999999999999999999999999999999e-36")]
    [TestCase("6e2147483647")]
    [TestCase("6e-2147483648")]
    public void InexactNumericWeightRejectsWithoutChangingOutput(string value)
    {
        Fails($"<p style='font-weight:{value}'>A</p>", AllRoles(), "FPDF1202");
        Fails($"<style>.unused{{font-weight:{value}}}</style><p>A</p>", AllRoles(), "FPDF1202");
    }

    [TestCase("+000600.000", "600")]
    [TestCase(".006e5", "600")]
    [TestCase("60000e-2", "600")]
    [TestCase("0009.000E+00002", "900")]
    [TestCase("100.0000000000000000000000000000000000000", "100")]
    [TestCase("0.000000000000000000000000000000000000006e41", "600")]
    [TestCase("600000000000000000000000000000000000000e-36", "600")]
    public void ExactNumericWeightRetainsDecimalAndExponentSpellings(string value, string canonical)
    {
        var options = AllRoles();
        Assert.That(Pdf($"<p style='font-weight:{value}'>A</p>", options),
            Is.EqualTo(Pdf($"<p style='font-weight:{canonical}'>A</p>", options)));
    }

    [TestCase("b")][TestCase("strong")][TestCase("i")][TestCase("em")]
    public void StyleContainersKeepInlineNestingConstraints(string tag)
        => Fails($"<{tag}><div>A</div></{tag}>", AllRoles(), "FPDF1101");

    [Test]
    public void UnusedRoleResourcesKeepOrdinaryPdfBytesAndDoNotInferFontMetadata()
    {
        var options = new PdfOptions { Fonts = [Font()] };
        var bytes = Pdf("<p>A<span>B</span></p>", options);
        Assert.That(Pdf("<p>A<span>B</span></p>", options with { ItalicFonts = [Font()] }), Is.EqualTo(bytes));
        Assert.That(Pdf("<p>ASCII</p>", new() { BoldFonts = [Font()] }), Is.EqualTo(Pdf("<p>ASCII</p>", new())));
        var italicBytes = FontFixture.Create();
        FontFixture.U32(italicBytes, FontFixture.Table(italicBytes, "post") + 4, unchecked((uint)(-12 * 65536)));
        var explicitNormal = PdfFont.LoadTrueType(italicBytes);
        Assert.That(Layout("<p>A</p>", new() { Fonts = [explicitNormal] })[0].Runs[0].Font, Is.SameAs(explicitNormal));
    }

    [Test]
    public void FourRolesShareExistingCountAndUniqueObjectByteBudgets()
    {
        var font = Font(); var options = new PdfOptions {
            Fonts = [font, font], BoldFonts = [font, font], ItalicFonts = [font, font], BoldItalicFonts = [font, font],
            MaxTotalFontBytes = font.ByteLength
        };
        Pdf("<p>A<b>A<i>A</i></b><i>A</i></p>", options);
        Assert.Throws<ArgumentException>(() => Pdf("A", options with { BoldFonts = [font, font, font] }));
        Fails("A", options with { MaxTotalFontBytes = font.ByteLength - 1 }, "FPDF1503");
        Fails("A", options with { ItalicFonts = [Font()] }, "FPDF1503");
        Assert.Throws<ArgumentException>(() => Pdf("A", options with { BoldFonts = null! }));
        Assert.Throws<ArgumentException>(() => Pdf("A", options with { ItalicFonts = [null!] }));
        Assert.Throws<ArgumentException>(() => Pdf("A", options with { BoldItalicFonts = null! }));
    }

    [TestCase(false)][TestCase(true)]
    public void InvalidOriginalFontListKeepsItsExistingArgumentDiagnostic(bool oversized)
    {
        var options = new PdfOptions { Fonts = oversized ? Enumerable.Repeat(Font(), 9).ToArray() : null! };
        var error = Assert.Throws<ArgumentException>(() => Pdf("A", options))!;
        Assert.That(error.Message, Does.StartWith("Supply a non-null font list with at most eight entries."));
        Assert.That(error.ParamName, Is.EqualTo("options"));
    }

    [Test]
    public void ParallelConversionsDoNotShareRoleCacheOrFailureState()
    {
        var a = AllRoles(); var b = AllRoles() with { BoldFonts = [Font(950)] };
        const string html = "<p>A<b>A</b><em>A</em></p>";
        var first = Pdf(html, a); var second = Pdf(html, b);
        Assert.That(first, Is.Not.EqualTo(second));
        Parallel.For(0, 16, i => {
            var options = i % 2 == 0 ? a : b;
            Assert.That(Pdf(html, options), Is.EqualTo(i % 2 == 0 ? first : second));
            Fails("<p><b><i>A</i></b></p>", options with { BoldItalicFonts = [] }, "FPDF1507");
            Assert.That(Pdf(html, options), Is.EqualTo(i % 2 == 0 ? first : second));
        });
    }

    [TestCase(false)][TestCase(true)]
    public void SecondPageMissingGlyphPreservesOutputAndRecoversWithSameOptions(bool subset)
    {
        var options = new PdfOptions {
            Fonts = [Font()], ItalicFonts = [Font(latinOnly: true), Font()], SubsetFonts = subset
        };
        const string first = "<p>A中<b></b><strong></strong><em></em></p>";
        const string second = "<p style='break-before:page'><i>A中</i></p>";
        var html = first + second;
        using var before = new MemoryStream();
        Assert.That(PdfConverter.Convert(html, before, options).PageCount, Is.EqualTo(2));
        Assert.That(before.ToArray(), Is.EqualTo(Pdf("<p>A中</p>" + second, options)),
            "Empty style nodes must neither require an unused bold chain nor change the output.");
        var pages = Layout(html, options);
        Assert.That(pages[1].Runs.Select(r => r.Font), Is.EqualTo(options.ItalicFonts));
        Assert.That(string.Concat(pages[1].Runs.Select(r => r.Text)), Is.EqualTo("A中"));

        var invalid = first + second.Replace("A中", "A中&#x20001;");
        var error = Fails(invalid, options, "FPDF1504");
        Assert.Multiple(() =>
        {
            Assert.That(error.Message, Does.Contain("italic").And.Contain("U+20001"));
            Assert.That(error.SourceOffset, Is.EqualTo(invalid.IndexOf("<i>", StringComparison.Ordinal)));
        });
        // Reuse exactly the same caller options/font instances after the failure.
        // The resolver itself is conversion-local, not a reusable public object.
        using var recovered = new MemoryStream();
        Assert.That(PdfConverter.Convert(html, recovered, options).PageCount, Is.EqualTo(2));
        Assert.That(recovered.ToArray(), Is.EqualTo(before.ToArray()));
    }
}
