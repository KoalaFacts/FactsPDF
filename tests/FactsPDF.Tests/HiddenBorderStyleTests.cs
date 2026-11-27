using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// border-style: hidden on a border edge behaves exactly like none (FactsPDF has no collapsing
/// table borders, where hidden would differ): no width, no paint. Outlines still reject it.
/// </summary>
[TestFixture]
public sealed class HiddenBorderStyleTests
{
    private static readonly PdfOptions Options = new() { PageWidth = 300, PageHeight = 500, Margin = 20 };

    private static string Render(string html)
    {
        using var stream = new MemoryStream();
        PdfConverter.Convert(html, stream, Options);
        return Encoding.ASCII.GetString(stream.ToArray());
    }

    [TestCase("border:4pt hidden red")]
    [TestCase("border:4pt solid red;border-style:hidden")]
    [TestCase("border:4pt solid red;border-top-style:hidden;border-right-style:hidden;border-bottom-style:hidden;border-left-style:hidden")]
    [TestCase("border-width:4pt;border-color:red;border-style:hidden solid;border-style:hidden")]
    public void HiddenEdgesRenderLikeNone(string css)
        => Assert.That(Render($"<div style='background:#abc;{css}'>A</div>"),
            Is.EqualTo(Render("<div style='background:#abc;border:4pt none red'>A</div>")));

    [Test]
    // The table content width is 300 - 2*20 page margin - 2*6 body margin = 248pt.
    public void OneHiddenSideMatchesOneNoneSide()
    {
        Assert.That(Render("<div style='border:4pt solid red;border-left-style:hidden'>A</div>"),
            Is.EqualTo(Render("<div style='border:4pt solid red;border-left-style:none'>A</div>")));
        Assert.That(Render("<style>td{border:1pt hidden #000}</style><table><colgroup><col style='width:248pt'></colgroup><tbody><tr><td>A</td></tr></tbody></table>"),
            Is.EqualTo(Render("<style>td{border:1pt none #000}</style><table><colgroup><col style='width:248pt'></colgroup><tbody><tr><td>A</td></tr></tbody></table>")));
    }

    [TestCase("outline:1pt hidden red")]
    [TestCase("outline-style:hidden")]
    [TestCase("border-style:double")]
    public void OutlinesAndOtherStylesStillFail(string css)
    {
        using var stream = new MemoryStream();
        var error = Assert.Throws<FactsPdfException>(
            () => PdfConverter.Convert($"<div style='{css}'>A</div>", stream, Options));
        Assert.That(error!.Code, Is.EqualTo("FPDF1202"));
        Assert.That(stream.Length, Is.Zero);
    }
}
