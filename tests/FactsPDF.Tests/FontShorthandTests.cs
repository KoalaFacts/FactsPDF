using NUnit.Framework;

namespace FactsPDF.Tests;

// The font shorthand (R5du) expands into font-style, font-weight, font-size and line-height at the
// declaration's own position in the cascade and resets the ones it does not mention, so a later font
// overrides an earlier font-weight as in Chrome. Every longhand is compiled by the code of that
// longhand: values the renderer cannot draw (small-caps, a stretch keyword, oblique, a weight that is
// not a hundred, a system font) fail as the longhand does, and font-variant, font-stretch and
// font-family, which keep no state, are validated and dropped. Before this change every font
// declaration failed FPDF1201.
[TestFixture]
public sealed class FontShorthandTests
{
    private static PdfFont Font(int advance)
    {
        var bytes = FontFixture.Create();
        FontFixture.U16(bytes, FontFixture.Table(bytes, "hmtx") + 8, advance);
        return PdfFont.FromBytes(bytes);
    }

    private static readonly PdfOptions Options = new()
    {
        PageWidth = 300, PageHeight = 400, Margin = 20,
        Fonts = [Font(200)], BoldFonts = [Font(400)], ItalicFonts = [Font(600)], BoldItalicFonts = [Font(800)]
    };

    private static string Doc(string css, string body = "<div><p class=a>A</p><p>B</p></div>") =>
        $"<!doctype html><html><head><style>{css}</style></head><body>{body}</body></html>";

    private static byte[] Pdf(string html)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, Options);
        return output.ToArray();
    }

    private static List<PlacedText> Runs(string html) =>
        [.. TextLayout.Layout(HtmlDocumentReader.ReadTree(html, Options, default), Options, default).SelectMany(page => page.Runs)];

    private static string Face(PlacedText run) =>
        run.Font == Options.Fonts[0] ? "regular"
        : run.Font == Options.BoldFonts[0] ? "bold"
        : run.Font == Options.ItalicFonts[0] ? "italic" : "bold-italic";

    private static PlacedText Run(string html, string text) => Runs(html).Single(run => run.Text == text);

    // The shorthand and the longhands it stands for produce the same page bytes, in a context where
    // percentages, em and keywords have a parent size to resolve against.
    [TestCase(".a{font:12pt F}", ".a{font-style:normal;font-weight:normal;font-size:12pt;line-height:normal}")]
    [TestCase(".a{font:bold italic 14px/2 F}", ".a{font-style:italic;font-weight:bold;font-size:14px;line-height:2}")]
    [TestCase(".a{font:italic 150% F}", ".a{font-style:italic;font-weight:normal;font-size:150%;line-height:normal}")]
    [TestCase(".a{font:600 2em/20pt F}", ".a{font-style:normal;font-weight:600;font-size:2em;line-height:20pt}")]
    [TestCase(".a{font:bolder larger/1.5 F}", ".a{font-style:normal;font-weight:bolder;font-size:larger;line-height:1.5}")]
    [TestCase(".a{font:normal normal normal normal 12pt/normal F}", ".a{font:12pt F}")]
    [TestCase(".a{font:12pt/150% 'Foo Bar', Georgia, serif}", ".a{font-style:normal;font-weight:normal;font-size:12pt;line-height:150%}")]
    [TestCase(".a{font:12pt F !important}", ".a{font-style:normal!important;font-weight:normal!important;font-size:12pt!important;line-height:normal!important}")]
    [TestCase(".a{font:inherit}", ".a{font-style:inherit;font-weight:inherit;font-size:inherit;line-height:inherit}")]
    [TestCase(".a{font:initial}", ".a{font-style:initial;font-weight:initial;font-size:initial;line-height:initial}")]
    [TestCase(".a{font:unset}", ".a{font-style:unset;font-weight:unset;font-size:unset;line-height:unset}")]
    [TestCase(".a{font:italic 11pt F}.a{font:inherit}", ".a{font-style:inherit;font-weight:inherit;font-size:inherit;line-height:inherit}")]
    public void ShorthandRendersLikeItsLonghands(string shorthand, string longhands)
    {
        const string body = "<div class=outer><p class=a>A</p><p>B</p></div>";
        var outer = ".outer{font:italic bold 10pt/16pt F}";
        Assert.That(Pdf(Doc(outer + shorthand, body)), Is.EqualTo(Pdf(Doc(outer + longhands, body))), shorthand);
    }

    [Test]
    public void SizesFacesAndLineHeightComeFromTheShorthand()
    {
        var html = Doc(".a{font:bold italic 14px/2 F}");
        var run = Run(html, "A");
        Assert.That(Face(run), Is.EqualTo("bold-italic"));
        Assert.That(run.Style.FontSize, Is.EqualTo(10.5).Within(1e-9));
        var next = Run(html, "B");
        Assert.That(Face(next), Is.EqualTo("regular"));
        var tight = Doc(".a{font:bold italic 14px F}");
        Assert.That(run.Baseline - next.Baseline, Is.GreaterThan(Run(tight, "A").Baseline - Run(tight, "B").Baseline + 3),
            "line-height 2 is twice the 10.5pt size, well above the normal line height");
    }

    // The reset (over-correction) boundary: a shorthand that does not mention a longhand restores its
    // initial value, so what an earlier declaration set is gone, while a later longhand still wins.
    [Test]
    public void FontAfterWeightResetsTheWeight()
    {
        Assert.That(Face(Run(Doc(".a{font-weight:bold;font:12pt F}"), "A")), Is.EqualTo("regular"));
        Assert.That(Pdf(Doc(".a{font-weight:bold;font:12pt F}")), Is.EqualTo(Pdf(Doc(".a{font:12pt F}"))));
        Assert.That(Face(Run(Doc(".a{font-style:italic;font:12pt F}"), "A")), Is.EqualTo("regular"));
        Assert.That(Face(Run(Doc(".a{font-weight:bold;font-style:italic;font:12pt F}"), "A")), Is.EqualTo("regular"));
    }

    [Test]
    public void FontAfterSizeAndLineHeightRestoresTheirInitialValues()
    {
        var plain = Doc("");
        var reset = Doc(".a{font-size:20pt;line-height:40pt;font:12pt F}");
        Assert.That(Pdf(reset), Is.EqualTo(Pdf(plain)));
        Assert.That(Run(reset, "B").Baseline, Is.EqualTo(Run(plain, "B").Baseline).Within(1e-9));
        Assert.That(Run(Doc(".a{line-height:40pt}"), "B").Baseline, Is.Not.EqualTo(Run(plain, "B").Baseline), "the 40pt line height would have moved B");
    }

    [Test]
    public void LongerLonghandAfterFontWins()
    {
        Assert.That(Face(Run(Doc(".a{font:12pt F;font-weight:bold}"), "A")), Is.EqualTo("bold"));
        var run = Run(Doc(".a{font:bold 12pt F;font-size:20pt}"), "A");
        Assert.That(Face(run), Is.EqualTo("bold"));
        Assert.That(run.Style.FontSize, Is.EqualTo(20));
        var spaced = Doc(".a{font:16px/20px F;font-weight:bold}");
        Assert.That(Face(Run(spaced, "A")), Is.EqualTo("bold"));
        Assert.That(Pdf(spaced), Is.EqualTo(Pdf(Doc(".a{font-style:normal;font-weight:bold;font-size:16px;line-height:20px}"))));
    }

    [Test]
    public void ShorthandCompetesInTheCascadeLikeItsLonghands()
    {
        // The lower-specificity bold is reset by the later class rule; the higher-specificity one is kept.
        Assert.That(Face(Run(Doc("div p{font-weight:bold}.a{font:12pt F}"), "A")), Is.EqualTo("regular"));
        Assert.That(Face(Run(Doc("div p.a{font-weight:bold}.a{font:12pt F}"), "A")), Is.EqualTo("bold"));
        // !important on the shorthand covers every longhand it expands to.
        Assert.That(Face(Run(Doc(".a{font:12pt F !important;font-weight:bold}"), "A")), Is.EqualTo("regular"));
        Assert.That(Face(Run(Doc(".a{font-weight:bold!important;font:12pt F}"), "A")), Is.EqualTo("bold"));
        Assert.That(Face(Run(Doc(".a{font-weight:bold!important}.a{font:12pt F!important}"), "A")), Is.EqualTo("regular"));
    }

    [Test]
    public void InlineStyleAndCustomPropertiesExpandTheShorthand()
    {
        var inline = Doc("", "<div><p class=a style='font:bold 14pt/20pt F'>A</p><p>B</p></div>");
        Assert.That(Face(Run(inline, "A")), Is.EqualTo("bold"));
        Assert.That(Run(inline, "A").Style.FontSize, Is.EqualTo(14));
        Assert.That(Pdf(Doc("", "<div><p class=a style='font:bold 14pt/20pt F;line-height:40pt'>A</p><p>B</p></div>")),
            Is.EqualTo(Pdf(Doc("", "<div><p class=a style='font-style:normal;font-weight:bold;font-size:14pt;line-height:40pt'>A</p><p>B</p></div>"))));
        var variable = Doc(":root{--f:bold 14pt/20pt F}.a{font:var(--f)}");
        Assert.That(Pdf(variable), Is.EqualTo(Pdf(Doc(".a{font:bold 14pt/20pt F}"))));
        Assert.That(Pdf(Doc(":root{--s:14pt}.a{font:italic var(--s)/20pt F}")), Is.EqualTo(Pdf(Doc(".a{font:italic 14pt/20pt F}"))));
    }

    [Test]
    public void TablesAndListsTakeTheShorthand()
    {
        // th is bold by default; font:12pt resets it to regular, td keeps its own declaration.
        const string table = "<table><tr><th>A</th><td>B</td></tr></table>";
        Assert.That(Face(Run(Doc("", table), "A")), Is.EqualTo("bold"));
        var styled = Doc("th{font:12pt F}td{font:italic 10pt/20pt F}", table);
        Assert.That(Face(Run(styled, "A")), Is.EqualTo("regular"));
        Assert.That(Face(Run(styled, "B")), Is.EqualTo("italic"));
        Assert.That(Run(styled, "B").Style.FontSize, Is.EqualTo(10));
        Assert.That(Pdf(styled), Is.EqualTo(Pdf(Doc(
            "th{font-style:normal;font-weight:normal;font-size:12pt;line-height:normal}td{font-style:italic;font-weight:normal;font-size:10pt;line-height:20pt}", table))));
        const string list = "<ul><li>A</li><li>B</li></ul>";
        var items = Doc("li{font:italic 11pt/16pt F}", list);
        Assert.That(Runs(items).Where(run => run.Text is "A" or "B").Select(Face), Is.All.EqualTo("italic"));
    }

    [Test]
    public void FontRevertAndFontOnAHeadingBehaveLikeTheirLonghands()
    {
        const string heading = "<h2 class=a>A</h2><p>B</p>";
        Assert.That(Pdf(Doc("h2{font:revert}", heading)), Is.EqualTo(Pdf(Doc("", heading))));
        Assert.That(Pdf(Doc("h2{font:12pt F}", heading)),
            Is.EqualTo(Pdf(Doc("h2{font-style:normal;font-weight:normal;font-size:12pt;line-height:normal}", heading))));
        Assert.That(Face(Run(Doc("h2{font:12pt F}", heading), "A")), Is.EqualTo("regular"));
    }

    // Values no longhand keeps state for are validated and dropped, so the page does not change.
    [TestCase("font:normal normal normal normal 12pt F")]
    [TestCase("font:12pt \"Helvetica Neue\", Arial, sans-serif")]
    [TestCase("font:12pt 'Foo' , bar baz")]
    public void StatelessPartsAreAcceptedAndChangeNothing(string declaration)
    {
        Assert.That(Pdf(Doc($".a{{{declaration}}}")), Is.EqualTo(Pdf(Doc(".a{font:12pt F}"))));
    }

    private static FactsPdfException Fails(string css, string at, string code = "FPDF1202")
    {
        var html = Doc(css);
        using var output = new MemoryStream();
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, Options))!;
        Assert.That(error.Code, Is.EqualTo(code), css);
        Assert.That(output.Length, Is.Zero);
        Assert.That(error.SourceOffset, Is.EqualTo(html.IndexOf(at, StringComparison.Ordinal)), css);
        return error;
    }

    // Valid CSS the renderer cannot draw fails exactly as the longhand does, at the offending token.
    [TestCase(".a{font:small-caps 12pt F}", "small-caps", "font-variant supports only normal")]
    [TestCase(".a{font:oblique -10deg 12pt F}", "-10deg", "negative angle")]
    [TestCase(".a{font:0 F}", "0 F", "font-size must be between 1pt and 144pt")]
    [TestCase(".a{font:12pt \"\"}", "\"\"", "font-family supports only")]
    [TestCase(".a{font:12pt/0 F}", "0 F", "line-height")]
    public void UndrawableValuesFailLikeTheLonghand(string css, string at, string message)
    {
        Assert.That(Fails(css, at).Message, Does.Contain(message));
        // Valid per the specifications, so non-strict mode keeps failing too.
        using var output = new MemoryStream();
        var lenient = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(Doc(css), output, Options with { Strict = false }))!;
        Assert.That(lenient.Code, Is.EqualTo("FPDF1202"));
        Assert.That(output.Length, Is.Zero);
    }

    [TestCase("caption")]
    [TestCase("icon")]
    [TestCase("menu")]
    [TestCase("message-box")]
    [TestCase("small-caption")]
    [TestCase("status-bar")]
    [TestCase("STATUS-BAR")]
    public void SystemFontKeywordsNeedAPlatformFontAndFail(string keyword)
    {
        Assert.That(Fails($".a{{font:{keyword}}}", keyword).Message, Does.Contain("platform font"));
        Assert.That(Fails($".a{{font:{keyword} !important}}", keyword).Message, Does.Contain("platform font"));
    }

    // Invalid per the specifications (each verdict checked against Chrome's CSSOM): strict mode fails
    // with the location of the declaration, non-strict mode drops it with a warning carrying that error.
    [TestCase("12pt")]
    [TestCase("bold")]
    [TestCase("12pt/2")]
    [TestCase("12pt/ F")]
    [TestCase("/2 12pt F")]
    [TestCase("bold bold 12pt F")]
    [TestCase("italic italic 12pt F")]
    [TestCase("small-caps small-caps 12pt F")]
    [TestCase("bold 400 12pt F")]
    [TestCase("normal normal normal normal normal 12pt F")]
    [TestCase("1001 12pt F")]
    [TestCase("12 F")]
    [TestCase("-12pt F")]
    [TestCase("12pt/-2 F")]
    [TestCase("50% 12pt F")]
    [TestCase("12pt F,")]
    [TestCase("12pt ,F")]
    [TestCase("12pt F,,G")]
    [TestCase("12pt inherit")]
    [TestCase("12pt serif, initial")]
    [TestCase("12pt default")]
    [TestCase("inherit 12pt F")]
    [TestCase("bold inherit")]
    [TestCase("caption 12pt F")]
    [TestCase("bold caption")]
    [TestCase("none")]
    [TestCase("12pt F/2")]
    [TestCase("12pt/2/3 F")]
    [TestCase("left 12pt F")]
    [TestCase("12pt 'F' G")]
    [TestCase("oblique 10deg 20deg 12pt F")]
    public void InvalidShorthandFailsStrictAndIsDroppedNonStrict(string value)
    {
        var html = Doc($".a{{font-weight:bold;font:{value}}}");
        using var rejected = new MemoryStream();
        var strict = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, rejected, Options))!;
        Assert.That(strict.Code, Is.EqualTo("FPDF1202"), value);
        Assert.That(strict.SourceOffset, Is.GreaterThanOrEqualTo(html.IndexOf("font:", StringComparison.Ordinal)), value);
        Assert.That(rejected.Length, Is.Zero);

        PdfWarnings? warnings = null;
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, Options with { Strict = false, WarningHandler = report => warnings = report });
        // The dropped declaration leaves the earlier font-weight in force, as in a browser.
        Assert.That(output.ToArray(), Is.EqualTo(Pdf(Doc(".a{font-weight:bold}"))), value);
        Assert.That(warnings!.Items, Has.Count.EqualTo(1), value);
        Assert.That(warnings.Items[0].Code, Is.EqualTo(strict.Code));
        Assert.That(warnings.Items[0].SourceOffset, Is.EqualTo(strict.SourceOffset));
    }

    [Test]
    public void FontStaysUnsupportedInAPageMarginBox()
    {
        var html = "<!doctype html><style>@page{@bottom-center{content:'A';font:12pt F}}</style><p>B</p>";
        using var output = new MemoryStream();
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, new PdfOptions()))!;
        Assert.That(error.Code, Is.EqualTo("FPDF1201"));
    }
}
