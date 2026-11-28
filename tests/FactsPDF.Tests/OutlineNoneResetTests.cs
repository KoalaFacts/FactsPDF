using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// Outlines are painted only on ordinary report panels. Everywhere else outline-style can only
/// be none, so resets such as a{outline:none}, img{outline:0} or p{outline-style:none} remove
/// an outline that never exists: they are dropped and the PDF bytes equal the page without them.
/// Any outline that could paint outside a panel still fails FPDF1201.
/// </summary>
[TestFixture]
public sealed class OutlineNoneResetTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 400, Margin = 20 };

    private const string Body =
        "<h1 style='font-weight:normal'>Invoice</h1><p>Total <span>due</span> <span>now</span></p>" +
        "<ul><li>One</li></ul>" +
        "<table style='width:120pt'><colgroup><col style='width:120pt'></colgroup><tbody><tr><td>Cell</td></tr></tbody></table>" +
        "<div><p>Panel text</p></div>";

    private static byte[] Pdf(string html)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, Page);
        return output.ToArray();
    }

    [TestCase("outline:none")]
    [TestCase("outline:0")]
    [TestCase("outline:0 none")]
    [TestCase("outline:none !important")]
    [TestCase("OUTLINE:NONE")]
    [TestCase("outline-style:none")]
    [TestCase("outline-style:initial")]
    [TestCase("outline-style:unset")]
    [TestCase("outline-width:0")]
    [TestCase("outline:initial")]
    [TestCase("outline:unset")]
    public void ResetOnEveryNonPanelElementRendersLikeThePageWithoutIt(string declaration)
    {
        var rule = $"<style>h1,p,span,ul,li,table,tbody,tr,td{{{declaration}}}</style>";
        Assert.That(Pdf(rule + Body), Is.EqualTo(Pdf(Body)));
    }

    [TestCase("<p style='outline:none'>A</p>")]
    [TestCase("<p>A <span style='outline:0'>B</span></p>")]
    [TestCase("<table style='width:120pt'><colgroup><col style='width:120pt'></colgroup><tbody><tr><td style='outline:none'>A</td></tr></tbody></table>")]
    public void InlineResetRendersLikeThePageWithoutIt(string html)
    {
        var plain = html.Replace(" style='outline:none'", "", StringComparison.Ordinal)
            .Replace(" style='outline:0'", "", StringComparison.Ordinal);
        Assert.That(Pdf(html), Is.EqualTo(Pdf(plain)));
    }

    [Test]
    public void PanelOutlinesStillPaint()
    {
        const string panel = "<div style='outline:2pt solid #234;padding:6pt'><p>Panel</p></div>";
        Assert.That(Pdf(panel), Is.Not.EqualTo(Pdf("<div style='padding:6pt'><p>Panel</p></div>")));
        Assert.That(Pdf("<style>p{outline:none}</style>" + panel), Is.EqualTo(Pdf(panel)));
    }

    [TestCase("<p style='outline:1pt solid red'>A</p>")]
    [TestCase("<p style='outline-style:dashed'>A</p>")]
    [TestCase("<p style='outline-style:inherit'>A</p>")]
    [TestCase("<p style='outline:inherit'>A</p>")]
    [TestCase("<p>A <span style='outline:thin dotted'>B</span></p>")]
    [TestCase("<style>td{outline:solid}</style><table style='width:120pt'><colgroup><col style='width:120pt'></colgroup><tbody><tr><td>A</td></tr></tbody></table>")]
    [TestCase("<style>p{outline:none} p{outline-style:solid}</style><p>A</p>")]
    public void OutlineThatCouldPaintOutsideAPanelStillFails(string html)
    {
        var error = Assert.Throws<FactsPdfException>(() => Pdf(html));
        Assert.That(error!.Code, Is.EqualTo("FPDF1201"));
        Assert.That(error.Message, Does.Contain("outline:none"));
    }

    [TestCase("<p style='outline:none none'>A</p>")]
    [TestCase("<p style='outline:red blue'>A</p>")]
    [TestCase("<p style='outline-style:double'>A</p>")]
    [TestCase("<p style='outline:url(x)'>A</p>")]
    public void MalformedOutlineStillFailsValidation(string html)
    {
        var error = Assert.Throws<FactsPdfException>(() => Pdf(html));
        Assert.That(error!.Code, Is.EqualTo("FPDF1202"));
    }
}
