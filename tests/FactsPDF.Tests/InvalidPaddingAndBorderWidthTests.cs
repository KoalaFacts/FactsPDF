using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// Padding never takes auto and a border width is never a percentage: browsers drop such a declaration
/// as invalid CSS and keep the cascade below it. Strict mode fails FPDF1202 and says the CSS is invalid;
/// non-strict mode drops it with the same message as a warning, and the page equals the page without
/// the declaration. Valid neighbours (a percentage padding, an auto margin, a percentage radius) stay valid.
/// </summary>
[TestFixture]
public sealed class InvalidPaddingAndBorderWidthTests
{
    private static readonly PdfWriter Writer = Pdf.Write().WithPage(p => p.Size(300, 400).Margin(20));

    private static string Doc(string style) =>
        $"<!doctype html><html><head><style>div{{background:#cde;border:2pt solid #333;padding:4pt}}</style></head><body><div style=\"{style}\">A</div></body></html>";

    private static (byte[] Pdf, PdfWarnings Warnings) NonStrict(string html)
    {
        PdfWarnings? warnings = null;
        var pdf = Writer.WithStrict(false).WithWarnings(report => warnings = report).ToBytes(html);
        return (pdf, warnings!);
    }

    [TestCase("padding:auto")]
    [TestCase("padding:4pt auto")]
    [TestCase("padding:1pt 2pt auto 3pt")]
    [TestCase("padding-top:auto")]
    [TestCase("padding-right:auto")]
    [TestCase("padding-bottom:auto")]
    [TestCase("padding-left:auto")]
    [TestCase("padding-inline:auto")]
    [TestCase("padding-block:auto")]
    [TestCase("padding-inline-start:auto")]
    [TestCase("border-width:5%")]
    [TestCase("border-width:2pt 5%")]
    [TestCase("border-top-width:5%")]
    [TestCase("border-left-width:10%")]
    [TestCase("border:5% solid red")]
    [TestCase("border-top:5% solid red")]
    [TestCase("border-bottom:1pt solid red 5%")]
    [TestCase("border-block-width:5%")]
    [TestCase("border-inline-start-width:5%")]
    public void TheInvalidDeclarationFailsStrictAndIsDroppedNonStrict(string declaration)
    {
        var html = Doc(declaration);
        var strict = Assert.Throws<FactsPdfException>(() => Writer.ToBytes(html))!;
        Assert.That(strict.Code, Is.EqualTo("FPDF1202"));
        Assert.That(strict.Message, Does.Contain("Invalid CSS"));
        var (pdf, warnings) = NonStrict(html);
        Assert.That(pdf, Is.EqualTo(Writer.ToBytes(Doc(""))));
        Assert.That(warnings.Items, Has.Count.EqualTo(1));
        Assert.That(warnings.Items[0].Code, Is.EqualTo("FPDF1202"));
        Assert.That(warnings.Items[0].ToString(), Is.EqualTo(strict.Message));
        Assert.That(warnings.Items[0].SourceOffset, Is.EqualTo(strict.SourceOffset));
    }

    [Test]
    public void TheCascadeBelowTheDroppedDeclarationStays()
    {
        // The author padding and border of the rule remain; an earlier valid declaration in the same
        // attribute also stays (over-correction guard: only the invalid declaration is dropped).
        var withEarlierValid = Doc("padding-top:10pt;padding:auto");
        Assert.That(NonStrict(withEarlierValid).Pdf, Is.EqualTo(Writer.ToBytes(Doc("padding-top:10pt"))));
        Assert.That(NonStrict(withEarlierValid).Pdf, Is.Not.EqualTo(Writer.ToBytes(Doc(""))));
    }

    [TestCase("padding:10%")]
    [TestCase("padding:2pt 5%")]
    [TestCase("margin:0 auto")]
    [TestCase("border-width:thin")]
    [TestCase("border-width:2pt")]
    [TestCase("border:1pt dashed red")]
    public void ValidNeighbouringValuesStayValid(string declaration)
        => Assert.DoesNotThrow(() => Writer.ToBytes(Doc(declaration)));
}
