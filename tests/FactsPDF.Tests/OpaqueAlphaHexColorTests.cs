using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// A fully opaque #rgba / #rrggbbaa (alpha f / ff) paints exactly as its #rgb / #rrggbb form;
/// translucent hex alpha is covered by TranslucentColourTests. Malformed hex still fails FPDF1202.
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
        string Page(string c) => $"<style>h1{{font-weight:normal;color:{c}}}</style><h1>Total</h1>" +
            $"<div style='background:{c};border:2pt dashed {c};text-decoration:underline {c}'>A</div>" +
            $"<p style='color:{c}'>B</p>";
        Assert.That(Render(Page(withAlpha)), Is.EqualTo(Render(Page(opaque))));
    }

    [TestCase("color:#23446")]
    [TestCase("color:#23446b4")]
    [TestCase("color:#23446bfff")]
    public void MalformedHexStillFails(string css)
    {
        using var stream = new MemoryStream();
        var error = Assert.Throws<FactsPdfException>(
            () => PdfConverter.Convert($"<div style='{css}'>A</div>", stream, Options));
        Assert.That(error!.Code, Is.EqualTo("FPDF1202"), css);
        Assert.That(stream.Length, Is.Zero);
    }
}
