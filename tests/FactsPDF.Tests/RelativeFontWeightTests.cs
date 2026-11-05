using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>Relative weight uses the explicitly supplied normal/bold font roles,
/// not simulated variable font weights or CSS-family discovery.</summary>
[TestFixture]
public sealed class RelativeFontWeightTests
{
    private static PdfFont Font(int advance)
    {
        var bytes = FontFixture.Create();
        FontFixture.U16(bytes, FontFixture.Table(bytes, "hmtx") + 8, advance);
        return PdfFont.FromBytes(bytes);
    }

    private static PdfOptions Roles() => new()
    {
        Fonts = [Font(200)],
        BoldFonts = [Font(450)],
        PageWidth = 245,
        PageHeight = 180,
        Margin = 15
    };

    private static byte[] Render(string html, PdfOptions options)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, options);
        return output.ToArray();
    }

    private static List<LayoutPage> Layout(string html, PdfOptions options)
        => TextLayout.Layout(HtmlDocumentReader.ReadTree(html, options, default), options, default);

    private static void Reject(string html, PdfOptions options, string code)
    {
        using var destination = new MemoryStream();
        destination.Write("existing-pdf"u8);
        destination.Position = 4;
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, destination, options))!;
        Assert.Multiple(() =>
        {
            Assert.That(error.Code, Is.EqualTo(code));
            Assert.That(destination.ToArray(), Is.EqualTo("existing-pdf"u8.ToArray()));
            Assert.That(destination.Position, Is.EqualTo(4));
        });
    }

    [TestCase("normal", "bolder", "bold")]
    [TestCase("bold", "bolder", "bold")]
    [TestCase("normal", "lighter", "normal")]
    [TestCase("bold", "lighter", "normal")]
    [TestCase("500", "bolder", "bold")]
    [TestCase("600", "lighter", "normal")]
    public void RelativeWeightUsesBoundedParentRoleLadder(string parent, string relative, string resolved)
    {
        var options = Roles();
        var source = $"<section style='font-weight:{parent}'><p style='font-weight:{relative}'>AB W</p></section>";
        var oracle = $"<section style='font-weight:{parent}'><p style='font-weight:{resolved}'>AB W</p></section>";
        Assert.That(Render(source, options), Is.EqualTo(Render(oracle, options)));
        var role = resolved == "bold" ? options.BoldFonts[0] : options.Fonts[0];
        Assert.That(Layout(source, options).SelectMany(p => p.Runs)
            .Where(r => !string.IsNullOrWhiteSpace(r.Text))
            .All(r => ReferenceEquals(r.Font, role)), Is.True);
    }

    [Test]
    public void CascadeImportantInlineAndCssWideValuesSelectRealRoles()
    {
        var options = Roles();
        const string source =
            "<style>section{font-weight:bold}p{font-weight:lighter!important}p span{font-weight:bolder}</style>" +
            "<section><p style='font-weight:bolder'>AB <span>W</span></p>" +
            "<p><b style='font-weight:lighter'>AB</b> <span style='font-weight:inherit'>W</span></p>" +
            "<p style='font-weight:initial'>AB <span style='font-weight:unset'>W</span></p></section>";
        const string oracle =
            "<style>section{font-weight:bold}p{font-weight:normal!important}p span{font-weight:bold}</style>" +
            "<section><p style='font-weight:bold'>AB <span>W</span></p>" +
            "<p><b style='font-weight:normal'>AB</b> <span style='font-weight:inherit'>W</span></p>" +
            "<p style='font-weight:initial'>AB <span style='font-weight:unset'>W</span></p></section>";
        Assert.That(Render(source, options), Is.EqualTo(Render(oracle, options)));
    }

    [Test]
    public void FortyEightEntryReportUsesActualBoldAndNormalGlyphsAndPages()
    {
        var options = Roles();
        // The synthetic TrueType fixture covers AB/W and space, not ASCII
        // digits; keep the role test glyph-complete rather than weakening
        // missing-glyph diagnostics. The native report checks numbered rows.
        var rows = string.Concat(Enumerable.Repeat(
            "<p class='entry'>AB <span class='status'>W</span> <span class='amount'>AB</span></p>", 48));
        var source = "<style>section{font-weight:bolder}.entry{font-weight:lighter}" +
            ".entry .status{font-weight:bolder}.entry .amount{font-weight:lighter}</style>" +
            "<section>" + rows + "</section>";
        var oracle = "<style>section{font-weight:bold}.entry{font-weight:normal}" +
            ".entry .status{font-weight:bold}.entry .amount{font-weight:normal}</style>" +
            "<section>" + rows + "</section>";
        var pages = Layout(source, options);
        Assert.That(pages.Count, Is.GreaterThan(1));
        Assert.That(pages.SelectMany(p => p.Runs).Count(r => ReferenceEquals(r.Font, options.BoldFonts[0])),
            Is.GreaterThan(0));
        Assert.That(pages.SelectMany(p => p.Runs).Count(r => ReferenceEquals(r.Font, options.Fonts[0])),
            Is.GreaterThan(0));
        Assert.That(Render(source, options), Is.EqualTo(Render(oracle, options)));
        Reject(source, options with { MaxPages = 1 }, "FPDF1303");
    }

    [Test]
    public void MissingBoldRoleAndUnmatchedInvalidDeclarationPreserveDestination()
    {
        var options = new PdfOptions { Fonts = [Font(220)] };
        Reject("<p style='font-weight:bolder'>AB</p>", options, "FPDF1507");
        Reject("<style>.unused{font-weight:semi-bold}</style><p>AB</p>", options, "FPDF1202");
        Reject("<p style='font-weight:650'>AB</p>", options, "FPDF1202");
        Reject("<p style='font-weight:bolder lighter'>AB</p>", options, "FPDF1202");
        Assert.That(Render("<p style='font-weight:lighter'>AB</p>", options),
            Is.EqualTo(Render("<p style='font-weight:normal'>AB</p>", options)));
    }
}
