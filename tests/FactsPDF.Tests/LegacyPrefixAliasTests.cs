using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// The engine-prefixed spellings of border-radius, box-shadow, transform, filter, clip-path and
/// backdrop-filter that Bootstrap 3, Font Awesome 4 and older themes still write: same values,
/// cascade slot and applicability as the standard name, so they render byte-identical and fail
/// exactly where the standard name fails. The -webkit- text-decoration shorthand and longhands
/// (Bootstrap 4's reboot) are aliases in the same way.
/// </summary>
[TestFixture]
public sealed class LegacyPrefixAliasTests
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
        $"<div style='{property}:{value};width:120pt;padding:10pt;background:#eef'><p>Paid</p></div>";

    private static string Standard(string alias) => alias.ToLowerInvariant() switch
    {
        "-webkit-border-radius" or "-moz-border-radius" => "border-radius",
        "-webkit-box-shadow" or "-moz-box-shadow" => "box-shadow",
        "-webkit-transform" or "-moz-transform" or "-ms-transform" or "-o-transform" => "transform",
        "-webkit-filter" => "filter",
        "-webkit-clip-path" => "clip-path",
        "-webkit-backdrop-filter" => "backdrop-filter",
        _ => throw new ArgumentOutOfRangeException(nameof(alias))
    };

    [TestCase("-webkit-border-radius", "4pt")]
    [TestCase("-moz-border-radius", "4pt 8pt")]
    [TestCase("-webkit-border-radius", "0")]
    [TestCase("-WEBKIT-BORDER-RADIUS", "6PT")]
    [TestCase("-webkit-box-shadow", "none")]
    [TestCase("-moz-box-shadow", "none")]
    [TestCase("-webkit-transform", "none")]
    [TestCase("-moz-transform", "none")]
    [TestCase("-ms-transform", "none")]
    [TestCase("-o-transform", "none")]
    [TestCase("-webkit-filter", "none")]
    [TestCase("-webkit-clip-path", "none")]
    [TestCase("-webkit-backdrop-filter", "none")]
    [TestCase("-webkit-transform", "inherit")]
    [TestCase("-webkit-box-shadow", "none !important")]
    public void PrefixedSpellingRendersLikeTheStandardName(string alias, string value)
    {
        Assert.That(Pdf(Report(alias, value)), Is.EqualTo(Pdf(Report(Standard(alias), value))));
    }

    [Test]
    public void RadiusThroughThePrefixChangesThePaint()
    {
        Assert.That(Pdf(Report("-webkit-border-radius", "8pt")), Is.Not.EqualTo(Pdf(Report("border-radius", "0"))));
    }

    [Test]
    public void BootstrapPairRendersLikeTheStandardDeclaration()
    {
        const string body = "<div class='well' style='width:160pt;padding:12pt;border:1pt solid #ccc'><p>Total</p></div>";
        Assert.That(
            Pdf("<style>.well{-webkit-border-radius:4pt;-moz-border-radius:4pt;border-radius:4pt;" +
                "-webkit-box-shadow:none;box-shadow:none;-webkit-transform:none;-ms-transform:none;transform:none}</style>" + body),
            Is.EqualTo(Pdf("<style>.well{border-radius:4pt;box-shadow:none;transform:none}</style>" + body)));
    }

    [Test]
    public void LaterDeclarationWinsAcrossThePrefixAndTheStandardName()
    {
        Assert.That(Pdf(Report("border-radius", "2pt;-webkit-border-radius:8pt")),
            Is.EqualTo(Pdf(Report("border-radius", "8pt"))));
        Assert.That(Pdf(Report("-moz-border-radius", "8pt;border-radius:2pt")),
            Is.EqualTo(Pdf(Report("border-radius", "2pt"))));
    }

    [TestCase("<p style='-webkit-box-shadow:0 1px 2px #000'>A</p>", "FPDF1202")]
    [TestCase("<p style='-moz-box-shadow:inset 0 1px 1px rgba(0,0,0,.075)'>A</p>", "FPDF1202")]
    [TestCase("<p style='-webkit-transform:perspective(9px) rotateY(9deg)'>A</p>", "FPDF1202")]
    [TestCase("<p style='-ms-transform:translateX(4ex)'>A</p>", "FPDF1202")]
    [TestCase("<p style='-webkit-filter:blur(2px)'>A</p>", "FPDF1202")]
    [TestCase("<p style='-webkit-clip-path:circle(50%)'>A</p>", "FPDF1202")]
    [TestCase("<p style='-webkit-backdrop-filter:blur(4px)'>A</p>", "FPDF1202")]
    [TestCase("<div style='-webkit-border-radius:10%;border:1pt solid #000'><p>A</p></div>", "FPDF1202")]
    [TestCase("<ul><li style='-moz-border-radius:4pt'>A</li></ul>", null)]
    public void PrefixedSpellingFailsWhereTheStandardNameFails(string html, string? code)
    {
        var standard = html;
        foreach (var alias in new[] { "-webkit-border-radius", "-moz-border-radius", "-webkit-box-shadow", "-moz-box-shadow",
                     "-webkit-transform", "-ms-transform", "-webkit-filter", "-webkit-clip-path", "-webkit-backdrop-filter" })
            standard = standard.Replace(alias, Standard(alias), StringComparison.Ordinal);
        var expected = Catch(standard);
        var actual = Catch(html);
        Assert.That(actual?.Code, Is.EqualTo(expected?.Code));
        if (code is not null) Assert.That(actual?.Code, Is.EqualTo(code));
        if (actual is not null) Assert.That(actual.Message, Is.EqualTo(expected!.Message));
    }

    private static string Decorated(string declarations) =>
        $"<style>h2{{{declarations}}} .unused{{{declarations}}}</style><h2 style='font-weight:normal'>Invoice</h2>" +
        $"<p style='{declarations}'>Paid <span style='{declarations}'>in full</span> " +
        $"<a href='https://example.com/' style='{declarations}'>link</a></p>";

    [TestCase("-webkit-text-decoration:underline", "text-decoration:underline")]
    [TestCase("-webkit-text-decoration:underline dotted #c00", "text-decoration:underline dotted #c00")]
    [TestCase("-WEBKIT-TEXT-DECORATION:LINE-THROUGH", "text-decoration:line-through")]
    [TestCase("-webkit-text-decoration:none", "text-decoration:none")]
    [TestCase("-webkit-text-decoration-line:overline", "text-decoration-line:overline")]
    [TestCase("text-decoration-line:underline;-webkit-text-decoration-color:red",
        "text-decoration-line:underline;text-decoration-color:red")]
    [TestCase("text-decoration-line:underline;-webkit-text-decoration-style:dashed",
        "text-decoration-line:underline;text-decoration-style:dashed")]
    [TestCase("-webkit-text-decoration:underline;text-decoration:none", "text-decoration:none")]
    [TestCase("text-decoration:none;-webkit-text-decoration:underline !important", "text-decoration:underline !important")]
    public void PrefixedTextDecorationRendersLikeTheStandardName(string prefixed, string standard)
    {
        Assert.That(Pdf(Decorated(prefixed)), Is.EqualTo(Pdf(Decorated(standard))));
    }

    [Test]
    public void PrefixedUnderlineChangesThePaint()
    {
        Assert.That(Pdf(Decorated("-webkit-text-decoration:underline dotted")), Is.Not.EqualTo(Pdf(Decorated("text-decoration:none"))));
    }

    [TestCase("-webkit-text-decoration:blink", "text-decoration:blink")]
    [TestCase("-webkit-text-decoration-style:double", "text-decoration-style:double")]
    [TestCase("-webkit-text-decoration-color:url(a)", "text-decoration-color:url(a)")]
    public void PrefixedTextDecorationFailsLikeTheStandardName(string prefixed, string standard)
    {
        var expected = Catch($"<p style='{standard}'>A</p>");
        var actual = Catch($"<p style='{prefixed}'>A</p>");
        Assert.That(expected, Is.Not.Null);
        Assert.That(actual?.Code, Is.EqualTo(expected!.Code));
        Assert.That(actual!.Message, Is.EqualTo(expected.Message));
    }

    [TestCase("-o-box-shadow")]
    [TestCase("-ms-filter")]
    [TestCase("-khtml-border-radius")]
    [TestCase("-webkit-transform-x")]
    public void OtherPrefixesStillFail(string name)
    {
        var error = Assert.Throws<FactsPdfException>(() => Pdf($"<p style='{name}:none'>A</p>"));
        Assert.That(error!.Code, Is.EqualTo("FPDF1201"));
    }

    private static FactsPdfException? Catch(string html)
    {
        try { _ = Pdf(html); return null; }
        catch (FactsPdfException error) { return error; }
    }
}
