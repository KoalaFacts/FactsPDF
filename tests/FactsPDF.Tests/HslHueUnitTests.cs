using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>HSL hue accepts every CSS angle unit (deg, grad, rad, turn) within ±3600 degrees.</summary>
[TestFixture]
public sealed class HslHueUnitTests
{
    private static readonly PdfOptions Options = new() { PageWidth = 300, PageHeight = 500, Margin = 20 };

    private static string Render(string html)
    {
        using var stream = new MemoryStream();
        PdfConverter.Convert(html, stream, Options);
        return Encoding.ASCII.GetString(stream.ToArray());
    }

    private static string Page(string c) => $"<style>h1{{font-weight:normal;color:{c}}}</style><h1>Total</h1>" +
        $"<div style='background:{c};border:2pt dashed {c};text-decoration:underline {c}'>A</div>";

    [TestCase("hsl(0.5turn 60% 40%)", "hsl(180 60% 40%)")]
    [TestCase("hsl(0.25TURN, 60%, 40%)", "hsl(90,60%,40%)")]
    [TestCase("hsl(-1.5turn 60% 40%)", "hsl(-540 60% 40%)")]
    [TestCase("hsl(10turn 60% 40%)", "hsl(3600 60% 40%)")]
    [TestCase("hsl(200grad 60% 40%)", "hsl(180 60% 40%)")]
    [TestCase("hsl(4000grad 60% 40%)", "hsl(3600 60% 40%)")]
    [TestCase("hsl(3.14159265358979rad 60% 40%)", "hsl(180 60% 40%)")]
    [TestCase("hsla(1.5708rad, 100%, 25%, 1)", "hsl(90,100%,25%)")]
    [TestCase("hsl(0rad 60% 40%)", "hsl(0 60% 40%)")]
    public void AngleUnitsPaintLikeTheEquivalentDegrees(string angle, string degrees) =>
        Assert.That(Render(Page(angle)), Is.EqualTo(Render(Page(degrees))));

    [TestCase("color:hsl(10.01turn 60% 40%)")]
    [TestCase("color:hsl(-10.01turn 60% 40%)")]
    [TestCase("color:hsl(4001grad 60% 40%)")]
    [TestCase("color:hsl(63rad 60% 40%)")]
    [TestCase("color:hsl(1e30turn 60% 40%)")]
    [TestCase("color:hsl(1dpi 60% 40%)")]
    [TestCase("color:hsl(1ms 60% 40%)")]
    [TestCase("color:hsl(50% 60% 40%)")]
    public void OutOfRangeOrNonAngleHueStillFails(string css)
    {
        using var stream = new MemoryStream();
        var error = Assert.Throws<FactsPdfException>(
            () => PdfConverter.Convert($"<div style='{css}'>A</div>", stream, Options));
        Assert.That(error!.Code, Is.EqualTo("FPDF1202"), css);
        Assert.That(stream.Length, Is.Zero);
    }
}
