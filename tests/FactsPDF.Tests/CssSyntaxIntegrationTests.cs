using System.Text;
using FactsPDF;
using NUnit.Framework;

namespace FactsPDF.Tests;

[TestFixture]
public sealed class CssSyntaxIntegrationTests
{
    private static Rgb Color(string html)
    {
        var result = HtmlDocumentReader.Read(html, new PdfOptions(), CancellationToken.None);
        return result.Single().Runs.Single().Style.Color;
    }

    private static FactsPdfException Failure(string html, string expectedCode)
    {
        using var stream = new MemoryStream();
        stream.Write("keep"u8);
        var ex = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, stream));
        Assert.That(ex!.Code, Is.EqualTo(expectedCode));
        Assert.That(Encoding.ASCII.GetString(stream.ToArray()), Is.EqualTo("keep"));
        return ex;
    }

    [Test]
    public void EscapedCssPropertyNameUsesDecodedIdentifier()
    {
        Assert.That(Color("<style>p{col\\6fr:red}</style><p>A</p>"),
            Is.EqualTo(new Rgb(1, 0, 0)));
    }

    [Test]
    public void EscapedClassSelectorCanUseTheExistingClassMatcher()
    {
        Assert.That(Color("<style>.\\6eote{color:blue}</style><p class=note>A</p>"),
            Is.EqualTo(new Rgb(0, 0, 1)));
    }

    [Test]
    public void ValidUnknownAtRuleIsRejectedAsUnsupportedRenderingNotInvalidSyntax()
    {
        Failure("<style>@media print{p{color:red}}</style><p>A</p>", "FPDF1204");
    }

    [Test]
    public void UnknownOrUnimplementedValueRemainsAnExplicitStrictError()
    {
        Failure("<style>p{color:calc(1)}</style><p>A</p>", "FPDF1202");
        Failure("<style>p{display:grid}</style><p>A</p>", "FPDF1201");
    }

    [Test]
    public void InvalidCssRecoveriesCannotProduceSilentPartialPdfs()
    {
        Failure("<style>p{color:url(a b)}</style><p>A</p>", "FPDF1203");
    }

    [Test]
    public void CssSourcePositionsPointToTheActualBadValueNotTheTagStart()
    {
        var html = "<style>p{col\\6fr:banana}</style><p>A</p>";
        var result = Failure(html, "FPDF1202");
        Assert.That(result.SourceOffset, Is.EqualTo(html.IndexOf("banana", StringComparison.Ordinal)));
    }

    [Test]
    public void InlineDecodedEntitiesPreserveOriginalSourceLocations()
    {
        var html = "<p style='color:r&#101;d; font-size:banana'>A</p>";
        var result = Failure(html, "FPDF1202");
        Assert.That(result.SourceOffset, Is.EqualTo(html.IndexOf("banana", StringComparison.Ordinal)));
    }

    [Test]
    public void StrictModeStillRejectsUnterminatedCssComments()
    {
        Failure("<style>p{color:red}/*unfinished</style><p>A</p>", "FPDF1203");
    }
}
