using NUnit.Framework;

namespace FactsPDF.Tests;

// Text-phase failures (glyph resolution and line wrapping) point at the element
// whose text caused them, so the SDK's SourceOffset and the CLI's
// "Location: line L, column C." name the offending element.
public sealed class TextErrorLocationTests
{
    private const string Prefix = "<p>Fine text.</p>\n";
    // Only glyphs the fixture font contains, so the failure comes from the tested element.
    private const string FontPrefix = "<p>AB BA</p>\n";

    private static FactsPdfException Fails(PdfWriter writer, string html, string code)
    {
        var error = Assert.Throws<FactsPdfException>(() => writer.ToBytes(html))!;
        Assert.That(error.Code, Is.EqualTo(code), error.Message);
        return error;
    }

    private static PdfWriter WithFixtureFont()
        => Pdf.Write().WithFonts(fonts => fonts.Regular(PdfFont.FromBytes(FontFixture.Create())));

    [Test]
    public void CharacterWithoutASuppliedFontPointsAtItsElement()
    {
        const string html = Prefix + "<p id='cjk'>中</p>";
        var error = Fails(Pdf.Write(), html, "FPDF1301");
        Assert.That(error.SourceOffset, Is.EqualTo(html.IndexOf("<p id='cjk'>", StringComparison.Ordinal)));
    }

    [Test]
    public void CharacterInsideANestedSpanPointsAtTheSpan()
    {
        const string html = Prefix + "<p>Before <span>é</span></p>";
        var error = Fails(Pdf.Write(), html, "FPDF1301");
        Assert.That(error.SourceOffset, Is.EqualTo(html.IndexOf("<span>", StringComparison.Ordinal)));
    }

    [Test]
    public void CharacterMissingFromTheRegularFontChainPointsAtItsElement()
    {
        const string html = FontPrefix + "<p id='missing'>ABC</p>";
        var error = Fails(WithFixtureFont(), html, "FPDF1504");
        Assert.That(error.SourceOffset, Is.EqualTo(html.IndexOf("<p id='missing'>", StringComparison.Ordinal)));
        Assert.That(error.Message, Is.EqualTo("FPDF1504: No supplied font contains U+0043."));
    }

    [Test]
    public void CombiningMarkPointsAtItsElement()
    {
        const string html = FontPrefix + "<p id='mark'>Á</p>";
        var error = Fails(WithFixtureFont(), html, "FPDF1305");
        Assert.That(error.SourceOffset, Is.EqualTo(html.IndexOf("<p id='mark'>", StringComparison.Ordinal)));
    }

    [Test]
    public void UnpairedSurrogatePointsAtItsElement()
    {
        const string html = Prefix + "<p id='bad'>A\uD800B</p>";
        var error = Fails(Pdf.Write(), html, "FPDF1304");
        Assert.That(error.SourceOffset, Is.EqualTo(html.IndexOf("<p id='bad'>", StringComparison.Ordinal)));
    }

    [Test]
    public void UnbreakableWordPointsAtItsElement()
    {
        var html = Prefix + "<p id='wide'>" + new string('W', 400) + "</p>";
        var error = Fails(Pdf.Write(), html, "FPDF1302");
        Assert.That(error.SourceOffset, Is.EqualTo(html.IndexOf("<p id='wide'>", StringComparison.Ordinal)));
        Assert.That(error.Message, Is.EqualTo("FPDF1302: An unbreakable text segment exceeds the usable page width."));
    }

    [Test]
    public void UnbreakableWordInsideASpanPointsAtTheSpanThatOverflows()
    {
        var html = Prefix + "<p>Short <span id='wide'>" + new string('W', 400) + "</span></p>";
        var error = Fails(Pdf.Write(), html, "FPDF1302");
        Assert.That(error.SourceOffset, Is.EqualTo(html.IndexOf("<span id='wide'>", StringComparison.Ordinal)));
    }

    [Test]
    public void NoWrapSegmentPointsAtItsElement()
    {
        var html = Prefix + "<p id='nowrap' style='white-space:nowrap'>" + string.Join(' ', Enumerable.Repeat("word", 100)) + "</p>";
        var error = Fails(Pdf.Write(), html, "FPDF1302");
        Assert.That(error.SourceOffset, Is.EqualTo(html.IndexOf("<p id='nowrap'", StringComparison.Ordinal)));
    }

    [Test]
    public void PageFurnitureTextFailureHasNoHtmlOffset()
    {
        var error = Fails(Pdf.Write().WithPageFurniture(pages => pages.FooterCenter("中")), "<p>Body</p>", "FPDF1301");
        Assert.That(error.SourceOffset, Is.EqualTo(-1));
    }

    [Test]
    public void FooterTextFailureHasNoHtmlOffset()
    {
        var error = Fails(Pdf.Write().WithFooterText("é"), "<p>Body</p>", "FPDF1301");
        Assert.That(error.SourceOffset, Is.EqualTo(-1));
    }
}
