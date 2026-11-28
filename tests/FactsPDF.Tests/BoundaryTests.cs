using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

[TestFixture]
public sealed class BoundaryTests
{
    [TestCase("&nbsp;", "FPDF1301")]
    [TestCase("\u2003", "FPDF1301")]
    [TestCase("<body><head id=h><title>bad position</title></head><p>A</p></body>", "FPDF1101")]
    public void UnsupportedBoundaryInputCannotDisappearSilently(string html, string code)
    {
        using var output = new MemoryStream();
        var exception = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output));
        Assert.That(exception!.Code, Is.EqualTo(code));
        Assert.That(output.Length, Is.Zero);
    }

    // Text in head ends head and starts the body, as the HTML parser (and Chrome) does, so it is
    // rendered rather than lost.
    [Test]
    public void TextInHeadStartsTheBody()
    {
        using var output = new MemoryStream();
        PdfConverter.Convert("<html><head>lost text</head><body><p>A</p></body></html>", output);
        using var control = new MemoryStream();
        PdfConverter.Convert("<html><head></head><body>lost text<p>A</p></body></html>", control);
        Assert.That(output.ToArray(), Is.EqualTo(control.ToArray()));
    }

    [Test]
    public void PageBreakOnEmptyParagraphAppliesToFollowingContent()
    {
        using var output = new MemoryStream();
        var result = PdfConverter.Convert("<p>A</p><p style='break-before:page'></p><p>B</p>", output);
        Assert.That(result.PageCount, Is.EqualTo(2));
    }

    [Test]
    public void CultureComparisonChecksRealNonemptyPdf()
    {
        using var output = new MemoryStream();
        PdfConverter.Convert("<p style='font-size:10.5pt'>hello</p>", output);
        var pdf = Encoding.ASCII.GetString(output.ToArray());
        Assert.That(pdf, Does.StartWith("%PDF-1.7"));
        Assert.That(pdf, Does.Contain("/F1 10.5 Tf"));
        Assert.That(pdf, Does.Contain("<68656C6C6F> Tj"));
    }
}
