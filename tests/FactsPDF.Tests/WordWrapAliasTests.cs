using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// word-wrap is the legacy name of overflow-wrap (CSS Text): same values, cascade slot and
/// applicability, so it renders byte-identical and fails exactly where overflow-wrap fails.
/// </summary>
[TestFixture]
public sealed class WordWrapAliasTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 180, PageHeight = 400, Margin = 20 };
    private const string Code = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    private static byte[] Pdf(string html)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, Page);
        return output.ToArray();
    }

    [TestCase("break-word")]
    [TestCase("normal")]
    [TestCase("BREAK-WORD")]
    [TestCase("inherit")]
    public void WordWrapRendersLikeOverflowWrap(string value)
    {
        string Page(string property) =>
            $"<style>h2{{width:120pt;{property}:{value}}}</style><h2 style='font-weight:normal'>Due now</h2>" +
            $"<div style='width:120pt;{property}:break-word'><p style='{property}:{value}'>Paid in full</p></div>" +
            "<table style='width:60pt'><colgroup><col style='width:60pt'></colgroup>" +
            $"<tbody><tr><td style='{property}:break-word'>{Code}</td></tr></tbody></table>";
        Assert.That(Pdf(Page("word-wrap")), Is.EqualTo(Pdf(Page("overflow-wrap"))));
    }

    [Test]
    public void WordWrapBreaksALongIdentifierThatWouldOtherwiseFail()
    {
        Assert.That(Pdf($"<p style='width:60pt;word-wrap:break-word'>{Code}</p>"), Is.Not.Empty);
        
    }

    [Test]
    public void LaterDeclarationWinsAcrossTheAliasAndItsStandardName()
    {
        Assert.That(Pdf($"<p style='width:60pt;overflow-wrap:normal;word-wrap:break-word'>{Code}</p>"),
            Is.EqualTo(Pdf($"<p style='width:60pt;overflow-wrap:break-word'>{Code}</p>")));
        
    }

    [TestCase("<p style='word-wrap:auto'>A</p>", "FPDF1202")]
    [TestCase("<p style='word-wrap:break-all'>A</p>", "FPDF1202")]
    [TestCase("<style>.unused{word-wrap:auto}</style><p>A</p>", "FPDF1202")]
    public void WordWrapFailsWhereOverflowWrapFails(string html, string code)
    {
        var standard = html.Replace("word-wrap", "overflow-wrap", StringComparison.Ordinal);
        using var output = new MemoryStream();
        var alias = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, Page));
        var expected = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(standard, new MemoryStream(), Page));
        Assert.That(alias!.Code, Is.EqualTo(code));
        Assert.That(alias.Code, Is.EqualTo(expected!.Code));
        Assert.That(output.Length, Is.Zero);
    }
}
