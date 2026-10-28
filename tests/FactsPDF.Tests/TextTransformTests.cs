using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>ASCII report-label case changes must reach glyph lookup, PDF paint and pagination.</summary>
[TestFixture]
public sealed class TextTransformTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 180, PageHeight = 240, Margin = 20 };

    private static List<LayoutPage> Layout(string html, PdfOptions? options = null)
    {
        var o = options ?? Page;
        return TextLayout.Layout(HtmlDocumentReader.ReadTree(html, o, default), o, default);
    }

    private static byte[] Pdf(string html, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, options ?? Page);
        return output.ToArray();
    }

    private static void Reject(string html, string code, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        output.Write("existing-report"u8);
        output.Position = 4;
        var exception = Assert.Throws<FactsPdfException>(
            () => PdfConverter.Convert(html, output, options ?? Page));
        Assert.That(exception!.Code, Is.EqualTo(code));
        Assert.That(output.ToArray(), Is.EqualTo("existing-report"u8.ToArray()));
        Assert.That(output.Position, Is.EqualTo(4));
    }

    [Test]
    public void NoneAndUnstyledOutputRemainByteIdentical()
    {
        const string sample = "Invoice <span>ref ab-1042</span>";
        Assert.That(Pdf("<p style='text-transform:none'>" + sample + "</p>"),
            Is.EqualTo(Pdf("<p>" + sample + "</p>")));
    }

    [Test]
    public void UpperAndLowerCaseUseTheActualPdfGlyphs()
    {
        const string label = "Invoice ref ab-1042";
        Assert.That(Pdf("<p style='text-transform:uppercase'>" + label + "</p>"),
            Is.EqualTo(Pdf("<p>INVOICE REF AB-1042</p>")));
        Assert.That(Pdf("<h2 style='text-transform:lowercase'>INVOICE REF AB-1042</h2>"),
            Is.EqualTo(Pdf("<h2>invoice ref ab-1042</h2>")));
        Assert.That(Layout("<p style='text-transform:uppercase'>" + label + "</p>")
            .Single().Runs.Single().Text, Is.EqualTo("INVOICE REF AB-1042"));
    }

    [Test]
    public void CascadeImportantInheritInitialUnsetAndInlineResetControlRuns()
    {
        const string html = "<style>div{text-transform:uppercase}p{text-transform:lowercase}" +
            ".label{text-transform:uppercase!important}.reset{text-transform:initial!important}" +
            ".unset{text-transform:unset!important}</style>" +
            "<div><p class='label'>ab<span style='text-transform:none'>Cd</span>ef</p>" +
            "<p class='reset'>Mixed</p><p class='unset'>Mixed</p></div>";
        var paragraphs = HtmlDocumentReader.Read(html, Page, default);
        Assert.That(paragraphs.Select(p => p.Style.Transform),
            Is.EqualTo(new[] { TextTransform.Uppercase, TextTransform.None, TextTransform.Uppercase }));
        Assert.That(Layout(html).SelectMany(p => p.Runs).Select(r => r.Text),
            Is.EqualTo(new[] { "AB", "Cd", "EF", "Mixed", "MIXED" }));
        Assert.That(Pdf(html), Is.Not.Empty);
        Assert.That(Pdf("<div style='text-transform:uppercase'><p style='text-transform:inherit'>ab</p></div>"),
            Is.EqualTo(Pdf("<p>AB</p>")));
    }

    [Test]
    public void ActualWrapAndPagePositionsRetainTransformedTextWithoutClipping()
    {
        var narrow = Page with { PageWidth = 102, PageHeight = 86, Margin = 20 };
        const string report = "item paid item due item sent item closed";
        var changed = Layout("<p style='text-transform:uppercase'>" + report + "</p>", narrow);
        var projected = Layout("<p>ITEM PAID ITEM DUE ITEM SENT ITEM CLOSED</p>", narrow);
        Assert.That(changed.Count, Is.GreaterThan(1));
        Assert.That(changed.SelectMany(p => p.Runs).Select(r => (r.Text, r.X, r.Baseline, r.Width)),
            Is.EqualTo(projected.SelectMany(p => p.Runs).Select(r => (r.Text, r.X, r.Baseline, r.Width))));
        Assert.That(Pdf("<p style='text-transform:uppercase'>" + report + "</p>", narrow),
            Is.EqualTo(Pdf("<p>ITEM PAID ITEM DUE ITEM SENT ITEM CLOSED</p>", narrow)));
    }

    [Test]
    public void CjkCaselessCharactersStayIntactWhileLatinGlyphsChangeInTrueType()
    {
        var options = Page with { Fonts = [PdfFont.LoadTrueType(FontFixture.Create())] };
        const string transformed = "<p style='text-transform:uppercase'>中文ab</p>";
        const string projected = "<p>中文AB</p>";
        Assert.That(Pdf(transformed, options), Is.EqualTo(Pdf(projected, options)));
        Assert.That(Layout(transformed, options).Single().Runs.Single().Text, Is.EqualTo("中文AB"));
        Assert.That(Encoding.Latin1.GetString(Pdf(transformed, options)), Does.Contain("/FontFile2"));
    }

    [Test]
    public void CaseChangeSelectsActualGlyphInsteadOfCheckingOnlyTheInputCodepoint()
    {
        var options = Page with { Fonts = [PdfFont.LoadTrueType(FontFixture.Create())] };
        // The fixture contains capital A but deliberately lacks lowercase a.
        Reject("<p style='text-transform:lowercase'>A</p>", "FPDF1504", options);
    }

    [TestCase("capitalize")]
    [TestCase("full-width")]
    [TestCase("uppercase lowercase")]
    [TestCase("auto")]
    [TestCase("none !oops")]
    public void UnsupportedValuesDoNotOverwriteAnExistingPdf(string value)
        => Reject("<p style='text-transform:" + value + "'>Invoice</p>", "FPDF1202");

    [Test]
    public void UnmatchedUnsupportedValueIsNotSilentlyIgnored()
        => Reject("<style>.missing{text-transform:capitalize}</style><p>Invoice</p>", "FPDF1202");

    [Test]
    public void NonAsciiCasedLettersAreExplicitlyOutsideThisAsciiOnlyFeature()
        => Reject("<p style='text-transform:uppercase'>caf\u00e9</p>", "FPDF1305");
}
