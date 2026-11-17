using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// #rgba and #rrggbbaa are accepted only when fully opaque (alpha f / ff), like rgba(..., 1),
/// and paint exactly as their #rgb / #rrggbb form. Any other alpha still fails FPDF1202.
/// </summary>
[TestFixture]
public sealed class OpaqueAlphaHexColorTests
{
    private static readonly PdfOptions Options = new() { PageWidth = 300, PageHeight = 500, Margin = 20 };

    private static string Render(string html)
    {
        using var stream = new MemoryStream();
        PdfConverter.Convert(html, stream, Options);
        return Encoding.ASCII.GetString(stream.ToArray());
    }

    [TestCase("#abcf", "#abc")]
    [TestCase("#ABCF", "#abc")]
    [TestCase("#23446bff", "#23446b")]
    [TestCase("#23446BFF", "#23446b")]
    public void OpaqueAlphaPaintsLikeTheOpaqueForm(string withAlpha, string opaque)
    {
        string Page(string c) => $"<style>h1{{color:{c}}}</style><h1>Total</h1>" +
            $"<div style='background:{c};border:2pt dashed {c};text-decoration:underline {c}'>A</div>" +
            $"<p style='color:{c}'>B</p>";
        Assert.That(Render(Page(withAlpha)), Is.EqualTo(Render(Page(opaque))));
    }

    [TestCase("color:#abc8")]
    [TestCase("color:#23446b80")]
    [TestCase("background:#23446b00")]
    [TestCase("border:1pt solid #abce")]
    [TestCase("color:#23446")]
    [TestCase("color:#2344")]
    [TestCase("color:#23446bfff")]
    public void TranslucentOrMalformedHexStillFails(string css)
    {
        using var stream = new MemoryStream();
        var error = Assert.Throws<FactsPdfException>(
            () => PdfConverter.Convert($"<div style='{css}'>A</div>", stream, Options));
        Assert.That(error!.Code, Is.EqualTo("FPDF1202"), css);
        Assert.That(stream.Length, Is.Zero);
    }
}
