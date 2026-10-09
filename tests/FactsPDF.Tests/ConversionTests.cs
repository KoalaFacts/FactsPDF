using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace FactsPDF.Tests;

[TestFixture]
public sealed class ConversionTests
{
    private static (PdfConversionResult Result, string Pdf) Render(string html, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        var result = PdfConverter.Convert(html, output, options);
        Assert.That(result.BytesWritten, Is.EqualTo(output.Length));
        Assert.That(output.CanWrite, Is.True, "Caller owns the output stream.");
        return (result, Encoding.ASCII.GetString(output.ToArray()));
    }

    private static string Text(string pdf) => string.Concat(
        Regex.Matches(pdf, @"<([0-9A-F]+)> Tj").Select(m =>
            Encoding.ASCII.GetString(System.Convert.FromHexString(m.Groups[1].Value))));

    [Test]
    public void WritesARealPdfWithTextAndPageTree()
    {
        var (result, pdf) = Render("<h1>FactsPDF</h1><p>Hello, world!</p>");
        Assert.That(result.PageCount, Is.EqualTo(1));
        Assert.That(pdf, Does.StartWith("%PDF-1.7\n"));
        Assert.That(pdf, Does.Contain("/Type /Catalog"));
        Assert.That(pdf, Does.Contain("/Type /Pages /Count 1"));
        Assert.That(pdf, Does.EndWith("%%EOF\n"));
        Assert.That(Text(pdf), Does.Contain("FactsPDFHello, world!"));
    }

    [Test]
    public void XrefOffsetsPointAtEveryDeclaredObject()
    {
        var (_, pdf) = Render("<p>one</p><p style='break-before: page'>two</p>");
        var match = Regex.Match(pdf, @"xref\n0 (\d+)\n0000000000 65535 f \n((?:\d{10} 00000 n \n)+)");
        Assert.That(match.Success, Is.True);
        var offsets = Regex.Matches(match.Groups[2].Value, @"(\d{10}) 00000 n");
        Assert.That(offsets.Count, Is.EqualTo(int.Parse(match.Groups[1].Value) - 1));
        for (var i = 0; i < offsets.Count; i++)
        {
            var offset = int.Parse(offsets[i].Groups[1].Value);
            Assert.That(pdf[offset..], Does.StartWith($"{i + 1} 0 obj\n"));
        }
        var start = Regex.Match(pdf, @"startxref\n(\d+)\n");
        Assert.That(pdf[int.Parse(start.Groups[1].Value)..], Does.StartWith("xref\n"));
    }

    [Test]
    public void EveryContentStreamLengthIsItsByteLength()
    {
        var (_, pdf) = Render("<p>(parentheses) \\ backslash &lt;tag&gt;</p>");
        var streams = Regex.Matches(pdf, @"/Length (\d+) >>\nstream\n(.*?)\nendstream", RegexOptions.Singleline);
        Assert.That(streams.Count, Is.GreaterThan(0));
        foreach (Match stream in streams)
            Assert.That(int.Parse(stream.Groups[1].Value), Is.EqualTo(Encoding.ASCII.GetByteCount(stream.Groups[2].Value)));
        Assert.That(Text(pdf), Is.EqualTo("(parentheses) \\ backslash <tag>"));
    }

    [Test]
    public void HandlesHtmlCaseQuotedAttributesAndReferences()
    {
        var (_, pdf) = Render("<P TITLE='a > b' STYLE='COLOR: #f00'>A &amp; &#66; &#x43;</P>");
        Assert.That(Text(pdf), Is.EqualTo("A & B C"));
        Assert.That(pdf, Does.Contain("1 0 0 rg"));
    }

    [Test]
    public void IgnoresCommentsAndNonRenderingMetadata()
    {
        var (_, pdf) = Render("<!DOCTYPE html><html lang=en><head><meta charset=utf-8><title>Hidden</title></head><body><!-- hidden --><p>Visible</p></body></html>");
        Assert.That(Text(pdf), Is.EqualTo("Visible"));
    }

    [Test]
    public void CollapsesWhitespaceAcrossInlineNodesAndKeepsBr()
    {
        var (_, pdf) = Render("<p> A  <span>B</span>\n C<br>D </p>");
        Assert.That(Text(pdf), Is.EqualTo("A B CD"));
    }

    [Test]
    public void SupportsOmittedParagraphEndTags()
    {
        var (_, pdf) = Render("<p>First<p>Second");
        Assert.That(Text(pdf), Is.EqualTo("FirstSecond"));
    }

    [Test]
    public void RendersMixedInlineSizesAndColors()
    {
        var (_, pdf) = Render("<p>A <span style='font-size:20px;color:#00f'>B</span> C</p>");
        Assert.That(Text(pdf), Is.EqualTo("A B C"));
        Assert.That(pdf, Does.Contain("/F1 15 Tf"));
        Assert.That(pdf, Does.Contain("0 0 1 rg"));
    }

    [Test]
    public void LongParagraphWrapsAndPaginates()
    {
        var (result, _) = Render("<p>" + string.Concat(Enumerable.Repeat("alpha beta gamma ", 80)) + "</p>",
            new PdfOptions { PageWidth = 180, PageHeight = 144, Margin = 18 });
        Assert.That(result.PageCount, Is.GreaterThan(1));
    }

    [Test]
    public void ExplicitBreakDoesNotCreateLeadingOrTrailingBlankPages()
    {
        var (result, _) = Render("<p style='break-before:page'>A</p><p style='break-before:page;break-after:page'>B</p>");
        Assert.That(result.PageCount, Is.EqualTo(2));
    }

    [Test]
    public void EmptyInputCreatesOneBlankPage()
    {
        var (result, pdf) = Render("");
        Assert.That(result.PageCount, Is.EqualTo(1));
        Assert.That(Text(pdf), Is.Empty);
    }

    [Test]
    public void PdfNumbersDoNotDependOnCurrentCulture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var first = Render("<p style='font-size:10.5pt'>hello</p>").Pdf;
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            Assert.That(Render("<p style='font-size:10.5pt'>hello</p>").Pdf, Is.EqualTo(first));
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    // These positive contracts replace the former rejection cases for now-supported features.
    [Test]
    public void EmbeddedStylesheetRendersTextWithItsDeclaredColor()
    {
        var (result, pdf) = Render("<style>p{color:red}</style><p>A</p>");
        Assert.That(result.PageCount, Is.EqualTo(1));
        Assert.That(Text(pdf), Is.EqualTo("A"));
        Assert.That(pdf, Is.EqualTo(Render("<p style='color:red'>A</p>").Pdf));
    }

    [Test]
    public void ImportantInlineFontSizeProducesTheExpectedPdf()
    {
        var (result, pdf) = Render("<p style='font-size:12pt !important'>A</p>");
        Assert.That(result.PageCount, Is.EqualTo(1));
        Assert.That(Text(pdf), Is.EqualTo("A"));
        Assert.That(pdf, Is.EqualTo(Render("<p style='font-size:12pt'>A</p>").Pdf));
    }

    [TestCase("<script>alert(1)</script>", "FPDF1102")]
    [TestCase("<img src='https://example.invalid/image.png'>", "FPDF1102")]
    [TestCase("<table><tr><td>A</td></tr></table>", "FPDF1102")]
    [TestCase("<svg></svg>", "FPDF1102")]
    [TestCase("<p onclick='x()'>A</p>", "FPDF1103")]
    [TestCase("<p style='display:flex'>A</p>", "FPDF1201")]
    [TestCase("<p style='font-size:banana'>A</p>", "FPDF1202")]
    [TestCase("<p style='font-size:-1pt'>A</p>", "FPDF1202")]
    [TestCase("<p style='font-size:NaNpt'>A</p>", "FPDF1202")]
    [TestCase("<p style='line-height:0'>A</p>", "FPDF1202")]
    [TestCase("<p style='color:potato'>A</p>", "FPDF1202")]
    [TestCase("<p>\u4e2d\u6587</p>", "FPDF1301")]
    [TestCase("<div><span>A</div>", "FPDF1101")]
    [TestCase("<p title='unfinished>A</p>", "FPDF1101")]
    [TestCase("<p/>", "FPDF1101")]
    [TestCase("<p>&NotEqualTilde;</p>", "FPDF1104")]
    public void UnsupportedOrMalformedInputFailsWithoutWritingOutput(string html, string code)
    {
        using var output = new MemoryStream();
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output));
        Assert.That(error!.Code, Is.EqualTo(code));
        Assert.That(output.Length, Is.Zero);
    }

    [Test]
    public void EnforcesInputLimitBeforeRendering()
        => Fails("<p>long</p>", new PdfOptions { MaxInputCharacters = 3 }, "FPDF1001");
    [Test]
    public void EnforcesElementLimit()
        => Fails("<p>A</p><p>B</p>", new PdfOptions { MaxElements = 1 }, "FPDF1002");
    [Test]
    public void EnforcesNestingLimit()
        => Fails("<div><div><p>A</p></div></div>", new PdfOptions { MaxDepth = 2 }, "FPDF1003");
    [Test]
    public void EnforcesPageLimit()
        => Fails("<p>A</p><p style='break-before:page'>B</p>", new PdfOptions { MaxPages = 1 }, "FPDF1303");
    [Test]
    public void EnforcesOutputLimitWithoutPartialOutput()
        => Fails("<p>A</p>", new PdfOptions { MaxOutputBytes = 64 }, "FPDF1401");
    [Test]
    public void RejectsUnbreakableWordsThatCannotFit()
        => Fails("<p>" + new string('A', 100) + "</p>", new PdfOptions { PageWidth = 100, Margin = 20 }, "FPDF1302");

    private static void Fails(string html, PdfOptions options, string code)
    {
        using var output = new MemoryStream();
        var ex = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, options));
        Assert.That(ex!.Code, Is.EqualTo(code));
        Assert.That(output.Length, Is.Zero);
    }

    [Test]
    public void RejectsInvalidPageGeometry()
    {
        using var output = new MemoryStream();
        Assert.Throws<ArgumentOutOfRangeException>(() => PdfConverter.Convert("A", output, new PdfOptions { PageWidth = double.NaN }));
        Assert.Throws<ArgumentOutOfRangeException>(() => PdfConverter.Convert("A", output, new PdfOptions { Margin = 500 }));
    }

    [Test]
    public void PreCancelledConversionWritesNothing()
    {
        using var output = new MemoryStream();
        Assert.Throws<OperationCanceledException>(() => PdfConverter.Convert("<p>A</p>", output, cancellationToken: new CancellationToken(true)));
        Assert.That(output.Length, Is.Zero);
    }

    [Test]
    public void RejectsNullArguments()
    {
        using var output = new MemoryStream();
        Assert.Throws<ArgumentNullException>(() => PdfConverter.Convert(null!, output));
        Assert.Throws<ArgumentNullException>(() => PdfConverter.Convert("A", null!));
    }

    [Test]
    public void RejectsNonWritableOutput()
    {
        using var output = new MemoryStream([], writable: false);
        Assert.Throws<ArgumentException>(() => PdfConverter.Convert("A", output));
    }
}
