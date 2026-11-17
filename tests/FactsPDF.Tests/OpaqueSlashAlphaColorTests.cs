using System.Text;
using FactsPDF.CommandLine;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// CSS Color 4 syntax for opaque colours: rgba()/hsla() are aliases of rgb()/hsl(), the
/// space form takes an optional "/ alpha", and the comma form an optional fourth alpha.
/// Alpha must be exactly 1 or 100%; any other alpha still fails FPDF1202.
/// </summary>
[TestFixture]
public sealed class OpaqueSlashAlphaColorTests
{
    private static readonly PdfOptions Options = new() { PageWidth = 300, PageHeight = 500, Margin = 20 };

    private static string Render(string html)
    {
        using var stream = new MemoryStream();
        PdfConverter.Convert(html, stream, Options);
        return Encoding.ASCII.GetString(stream.ToArray());
    }

    private static string Page(string c) => $"<style>h1{{color:{c}}}</style><h1>Total</h1>" +
        $"<div style='background:{c};border:2pt dashed {c};text-decoration:underline {c}'>A</div>" +
        $"<p style='color:{c}'>B</p>";

    [TestCase("rgb(35 68 107 / 1)", "#23446b")]
    [TestCase("rgb(35 68 107/1)", "#23446b")]
    [TestCase("rgb(35 68 107 / 100%)", "#23446b")]
    [TestCase("rgb(35 68 107 / 1.0)", "#23446b")]
    [TestCase("rgb(35 68 107 / 100.00%)", "#23446b")]
    [TestCase("rgb(35 68 107 / 1e0)", "#23446b")]
    [TestCase("rgba(35 68 107)", "#23446b")]
    [TestCase("rgba(35 68 107 / 1)", "#23446b")]
    [TestCase("rgb(35, 68, 107, 1)", "#23446b")]
    [TestCase("rgba(35, 68, 107, 100%)", "#23446b")]
    [TestCase("RGB(0% 50% 100% / 1)", "#0080ff")]
    [TestCase("hsl(210 50% 40% / 1)", "hsl(210,50%,40%)")]
    [TestCase("hsl(210deg 50% 40% / 100%)", "hsl(210,50%,40%)")]
    [TestCase("hsla(210 50% 40%)", "hsl(210,50%,40%)")]
    [TestCase("hsl(210, 50%, 40%, 1)", "hsl(210,50%,40%)")]
    [TestCase("hsla(210, 50%, 40%, 100%)", "hsl(210,50%,40%)")]
    public void OpaqueModernSyntaxPaintsLikeTheClassicForm(string modern, string classic) =>
        Assert.That(Render(Page(modern)), Is.EqualTo(Render(Page(classic))));

    [TestCase("color:rgb(35 68 107 / 0.5)")]
    [TestCase("color:rgb(35 68 107 / 0)")]
    [TestCase("color:rgb(35 68 107 / 99%)")]
    [TestCase("color:rgb(35 68 107 / 0.99999999999999999999999999999)")]
    [TestCase("color:rgb(35 68 107 / 10)")]
    [TestCase("color:rgb(35 68 107 / 1000%)")]
    [TestCase("color:rgb(35 68 107 / -1)")]
    [TestCase("color:rgb(35 68 107 / 1px)")]
    [TestCase("color:rgb(35 68 107 / none)")]
    [TestCase("color:rgb(35 68 107 /)")]
    [TestCase("color:rgb(35 68 107 1)")]
    [TestCase("color:rgb(35 68 107 , 1)")]
    [TestCase("color:rgb(35, 68, 107 / 1)")]
    [TestCase("color:rgb(35 68 / 1)")]
    [TestCase("color:rgba(35, 68, 107, 50%)")]
    [TestCase("color:hsl(210 50% 40% / .5)")]
    [TestCase("color:hsla(210, 50%, 40%, 0)")]
    [TestCase("color:hsl(210 50% 40% / 1 / 1)")]
    public void TranslucentOrMalformedAlphaStillFails(string css)
    {
        using var stream = new MemoryStream();
        var error = Assert.Throws<FactsPdfException>(
            () => PdfConverter.Convert($"<div style='{css}'>A</div>", stream, Options));
        Assert.That(error!.Code, Is.EqualTo("FPDF1202"), css);
        Assert.That(stream.Length, Is.Zero);
    }

    [Test]
    public void ManagedCliMatchesTheSdkForSlashAlpha()
    {
        const string html = "<div style='background:rgb(240 246 250 / 1);border:2pt solid hsl(210 50% 40% / 100%)'>" +
            "<p style='color:rgba(0 128 0)'>Report status</p></div>";
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(html));
        using var output = new MemoryStream();
        using var diagnostics = new StringWriter();
        Assert.That(CliApplication.Run(["-", "-"], input, output, diagnostics), Is.Zero, diagnostics.ToString());
        using var expected = new MemoryStream();
        PdfConverter.Convert(html, expected);
        Assert.That(output.ToArray(), Is.EqualTo(expected.ToArray()));
    }
}
