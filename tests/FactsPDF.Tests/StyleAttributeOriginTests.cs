using FactsPDF.CssSyntax;
using NUnit.Framework;

namespace FactsPDF.Tests;

// Style attributes without character references map every CSS code unit to its own place in
// the page, so they need no per-character origin array, and CSS text that needs no
// normalization needs no boundary array either. Diagnostics must point at the same places.
[TestFixture]
public sealed class StyleAttributeOriginTests
{
    private static HtmlToken Start(string html) =>
        HtmlTokens.Read(html, default).First(token => token.Kind == HtmlTokenKind.Start);

    private static FactsPdfException Failure(string html, string expectedCode)
    {
        var error = Assert.Throws<FactsPdfException>(() => Pdf.Write().ToBytes(html));
        Assert.That(error!.Code, Is.EqualTo(expectedCode));
        return error;
    }

    [TestCase("<p style='color:red'>A</p>", "color:red")]
    [TestCase("<p style=\"color:red\">A</p>", "color:red")]
    [TestCase("<p style=color:red>A</p>", "color:red")]
    [TestCase("<p style=''>A</p>", "''")]
    public void PlainStyleAttributeRecordsOnlyWhereItsValueStarts(string html, string value)
    {
        var token = Start(html);
        Assert.That(token.StyleOriginOffsets, Is.Null);
        var expected = html.IndexOf(value, StringComparison.Ordinal) + (value == "''" ? 1 : 0);
        Assert.That(token.StyleValueOffset, Is.EqualTo(expected));
    }

    [Test]
    public void StyleAttributeWithCharacterReferencesKeepsItsOriginMap()
    {
        const string html = "<p style='color:r&#101;d'>A</p>";
        var token = Start(html);
        Assert.That(token.Attributes!["style"], Is.EqualTo("color:red"));
        Assert.That(token.StyleOriginOffsets, Is.Not.Null);
        Assert.That(token.StyleOriginOffsets!, Has.Length.EqualTo("color:red".Length + 1));
        Assert.That(token.StyleOriginOffsets![7], Is.EqualTo(html.IndexOf("&#101;", StringComparison.Ordinal)));
    }

    [Test]
    public void ElementWithoutStyleHasNoStyleOrigin()
    {
        var token = Start("<p class='a'>A</p>");
        Assert.That(token.StyleOriginOffsets, Is.Null);
        Assert.That(token.StyleValueOffset, Is.EqualTo(-1));
    }

    [Test]
    public void CssTextThatNeedsNoNormalizationHasNoBoundaryArray()
    {
        var text = CssSourceText.Create("color:red", 40, new CssSyntaxLimits());
        Assert.That(text.HasBoundaryMap, Is.False);
        Assert.That(text.Span(6, 3), Is.EqualTo(new CssSourceSpan(46, 3)));
        Assert.That(text.Span(9, 0), Is.EqualTo(new CssSourceSpan(49, 0)));
        Assert.That(text.Span(0, 9), Is.EqualTo(new CssSourceSpan(40, 9)));
        Assert.That(() => text.Span(9, 1), Throws.TypeOf<ArgumentOutOfRangeException>());
        Assert.That(CssSourceText.Create("a\r\nb", 0, new CssSyntaxLimits()).HasBoundaryMap, Is.True);
        Assert.That(CssSourceText.Create("red", 0, new CssSyntaxLimits(), [100, 101, 107, 108]).HasBoundaryMap, Is.True);
    }

    [TestCase("<p style='font-size:banana'>A</p>")]
    [TestCase("<p style=\"color:red; font-size:banana\">A</p>")]
    [TestCase("<p style=font-size:banana>A</p>")]
    [TestCase("<div><p   STYLE = 'color:red;font-size:banana'>A</p></div>")]
    [TestCase("<p style='font-size:banana; font-family:\"a & b\"'>A</p>")]
    [TestCase("<p style='color:r&#101;d; font-size:banana'>A</p>")]
    [TestCase("<p style='font-size:banana; color:&quot;red&quot;'>A</p>")]
    public void BadStyleAttributeValueIsReportedWhereItIsWritten(string html)
    {
        var error = Failure(html, "FPDF1202");
        Assert.That(error.SourceOffset, Is.EqualTo(html.IndexOf("banana", StringComparison.Ordinal)));
    }

    [TestCase("<p style='color:red /* open'>A</p>")]
    [TestCase("<p style='color:r&#101;d /* open'>A</p>")]
    public void UnterminatedCommentInStyleAttributeIsReportedAtTheComment(string html)
    {
        var error = Failure(html, "FPDF1203");
        Assert.That(error.SourceOffset, Is.EqualTo(html.IndexOf("/*", StringComparison.Ordinal)));
    }

    [Test]
    public void StyleAttributesStillApplyAfterTheChange()
    {
        var plain = Pdf.Write().ToBytes("<p style='color:#f00;font-size:20pt'>A</p>");
        var referenced = Pdf.Write().ToBytes("<p style='color:#f&#48;0;font-size:20pt'>A</p>");
        Assert.That(referenced, Is.EqualTo(plain));
        Assert.That(plain, Is.Not.EqualTo(Pdf.Write().ToBytes("<p>A</p>")));
    }
}
