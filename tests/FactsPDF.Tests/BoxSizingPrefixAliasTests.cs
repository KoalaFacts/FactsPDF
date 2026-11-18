using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// -webkit-box-sizing and -moz-box-sizing are the legacy engine spellings of box-sizing that
/// Bootstrap 3 and older resets still write first: same values, cascade slot and applicability,
/// so they render byte-identical and fail exactly where box-sizing fails.
/// </summary>
[TestFixture]
public sealed class BoxSizingPrefixAliasTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 400, Margin = 20 };

    private static byte[] Pdf(string html)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, Page);
        return output.ToArray();
    }

    private static string Report(string property, string value) =>
        $"<style>.panel{{{property}:{value};width:160pt;padding:12pt;border:2pt solid #234}}</style>" +
        "<div class='panel'><p>Invoice total</p></div>" +
        $"<div style='{property}:{value};width:120pt;min-height:60pt;padding:10pt;background:#eef'><p>Paid</p></div>" +
        $"<p style='{property}:{value};width:140pt;padding:8pt;border:1pt solid #999'>Due now</p>";

    [TestCase("-webkit-box-sizing", "border-box")]
    [TestCase("-moz-box-sizing", "border-box")]
    [TestCase("-webkit-box-sizing", "content-box")]
    [TestCase("-moz-box-sizing", "content-box")]
    [TestCase("-WEBKIT-BOX-SIZING", "BORDER-BOX")]
    [TestCase("-webkit-box-sizing", "inherit")]
    [TestCase("-moz-box-sizing", "initial")]
    [TestCase("-webkit-box-sizing", "border-box !important")]
    public void PrefixedSpellingRendersLikeBoxSizing(string alias, string value)
    {
        Assert.That(Pdf(Report(alias, value)), Is.EqualTo(Pdf(Report("box-sizing", value))));
    }

    [Test]
    public void BorderBoxThroughThePrefixChangesTheLayout()
    {
        Assert.That(Pdf(Report("-webkit-box-sizing", "border-box")),
            Is.Not.EqualTo(Pdf(Report("box-sizing", "content-box"))));
    }

    [Test]
    public void BootstrapResetTripleRendersLikeTheStandardDeclaration()
    {
        const string body = "<div style='width:160pt;padding:12pt;border:2pt solid #234'><p>Invoice total</p></div>";
        Assert.That(
            Pdf("<style>*{-webkit-box-sizing:border-box;-moz-box-sizing:border-box;box-sizing:border-box}</style>" + body),
            Is.EqualTo(Pdf("<style>*{box-sizing:border-box}</style>" + body)));
    }

    [Test]
    public void LaterDeclarationWinsAcrossThePrefixAndTheStandardName()
    {
        Assert.That(Pdf(Report("box-sizing", "content-box;-webkit-box-sizing:border-box")),
            Is.EqualTo(Pdf(Report("box-sizing", "border-box"))));
        Assert.That(Pdf(Report("-moz-box-sizing", "border-box;box-sizing:content-box")),
            Is.EqualTo(Pdf(Report("box-sizing", "content-box"))));
    }

    [TestCase("<p style='-webkit-box-sizing:padding-box'>A</p>", "FPDF1202")]
    [TestCase("<p style='-moz-box-sizing:border-box content-box'>A</p>", "FPDF1202")]
    [TestCase("<style>.unused{-moz-box-sizing:auto}</style><p>A</p>", "FPDF1202")]
    [TestCase("<p>A <span style='-webkit-box-sizing:border-box'>B</span></p>", null)]
    public void PrefixedSpellingFailsWhereBoxSizingFails(string html, string? code)
    {
        var standard = html.Replace("-webkit-box-sizing", "box-sizing", StringComparison.Ordinal)
            .Replace("-moz-box-sizing", "box-sizing", StringComparison.Ordinal);
        var expected = Catch(standard);
        var alias = Catch(html);
        Assert.That(alias?.Code, Is.EqualTo(expected?.Code));
        if (code is not null) Assert.That(alias?.Code, Is.EqualTo(code));
        if (alias is not null) Assert.That(alias.Message, Is.EqualTo(expected!.Message));
    }

    [TestCase("-o-box-sizing")]
    [TestCase("-ms-box-sizing")]
    [TestCase("-webkit-box-sizing-x")]
    public void OtherPrefixesStillFail(string name)
    {
        var error = Assert.Throws<FactsPdfException>(() => Pdf($"<p style='{name}:border-box'>A</p>"));
        Assert.That(error!.Code, Is.EqualTo("FPDF1201"));
    }

    private static FactsPdfException? Catch(string html)
    {
        try { _ = Pdf(html); return null; }
        catch (FactsPdfException error) { return error; }
    }
}
