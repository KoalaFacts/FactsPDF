using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// background-clip moves the edge the background colour is painted to; background-repeat,
/// -position and -size place an image that is never painted, so they change nothing.
/// </summary>
[TestFixture]
public sealed class BackgroundLayerPropertyTests
{
    private static readonly PdfOptions Options = new() { PageWidth = 300, PageHeight = 500, Margin = 20 };

    private static string Render(string html)
    {
        using var stream = new MemoryStream();
        PdfConverter.Convert(html, stream, Options);
        return Encoding.ASCII.GetString(stream.ToArray());
    }

    private static FactsPdfException Failure(string html)
    {
        using var stream = new MemoryStream();
        return Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, stream, Options))!;
    }

    private const string Box = "width:100pt;padding:10pt;border:8pt solid #00f;background:#f00";

    [TestCase("", "20 429.6 136 50.4 re f")]
    [TestCase("background-clip:border-box", "20 429.6 136 50.4 re f")]
    [TestCase("background-clip:padding-box", "28 437.6 120 34.4 re f")]
    [TestCase("background-clip:content-box", "38 447.6 100 14.4 re f")]
    [TestCase("background-clip:padding-box,content-box", "28 437.6 120 34.4 re f")]
    [TestCase("background-clip:content-box,text", "38 447.6 100 14.4 re f")]
    public void ClipMovesTheEdgeTheColourIsPaintedTo(string clip, string fill)
    {
        var pdf = Render($"<div style='{Box};{clip}'>X</div>");
        Assert.That(pdf, Does.Contain("1 0 0 rg " + fill));
    }

    [Test]
    public void TheBackgroundShorthandResetsAnEarlierClipAndALaterClipSticks()
    {
        var reset = Render("<div style='width:100pt;padding:10pt;border:8pt solid #00f;background-clip:content-box;background:#f00'>X</div>");
        Assert.That(reset, Does.Contain("1 0 0 rg 20 429.6 136 50.4 re f"));
        var after = Render("<div style='width:100pt;padding:10pt;border:8pt solid #00f;background:#f00;background-clip:content-box'>X</div>");
        Assert.That(after, Does.Contain("1 0 0 rg 38 447.6 100 14.4 re f"));
    }

    [Test]
    public void InheritTakesTheParentClipAndInitialTheBorderBox()
    {
        var inherited = Render($"<div style='background-clip:padding-box'><div style='{Box};background-clip:inherit'>X</div></div>");
        Assert.That(inherited, Does.Contain("1 0 0 rg 28 437.6 120 34.4 re f"));
        var initial = Render($"<div style='{Box};background-clip:padding-box;background-clip:initial'>X</div>");
        Assert.That(initial, Does.Contain("1 0 0 rg 20 429.6 136 50.4 re f"));
        // The clip is not inherited: a child that does not ask for it keeps the border box.
        var child = Render($"<div style='background-clip:content-box'><div style='{Box}'>X</div></div>");
        Assert.That(child, Does.Contain("1 0 0 rg 20 429.6 136 50.4 re f"));
    }

    [Test]
    public void AnEmptyClipPaintsNothing()
    {
        var pdf = Render("<div style='width:0;height:0;padding:10pt;border:8pt solid #00f;background:#f00;background-clip:content-box'></div>");
        Assert.That(pdf, Does.Not.Contain("1 0 0 rg"));
    }

    [Test]
    public void RoundedBoxesShrinkTheirCornersWithTheClipEdge()
    {
        var border = Render($"<div style='{Box};border-radius:20pt'>X</div>");
        var padding = Render($"<div style='{Box};border-radius:20pt;background-clip:padding-box'>X</div>");
        var content = Render($"<div style='{Box};border-radius:20pt;background-clip:content-box'>X</div>");
        Assert.That(padding, Is.Not.EqualTo(border));
        Assert.That(content, Is.Not.EqualTo(padding));
        // Unequal padding would need elliptical corners.
        Assert.That(Failure($"<div style='{Box};padding:10pt 4pt;border-radius:20pt;background-clip:content-box'>X</div>").Code, Is.EqualTo("FPDF1201"));
    }

    [Test]
    public void ASlicedBoxClipsTheSidesOnEveryFragment()
    {
        var lines = string.Concat(Enumerable.Repeat("line<br>", 60));
        var pdf = Render($"<div style='width:100pt;padding:10pt;border:8pt solid #00f;background:#f00;background-clip:padding-box'>{lines}</div>");
        Assert.That(pdf, Does.Contain("1 0 0 rg 28 "));
        Assert.That(pdf, Does.Not.Contain("1 0 0 rg 20 "));
    }

    [TestCase("li", "<ul style='margin:0'><li style='background-clip:content-box'>x</li></ul>")]
    [TestCase("td", "<table><tr><td style='background-clip:padding-box'>x</td></tr></table>")]
    [TestCase("table", "<table style='background-clip:content-box'><tr><td>x</td></tr></table>")]
    public void BoxesTheClipCannotReachRejectPaddingAndContentBox(string host, string html)
    {
        Assert.That(Failure(html).Code, Is.EqualTo("FPDF1201"), host);
    }

    [TestCase("background-clip:text")]
    [TestCase("background-clip:border-area")]
    [TestCase("background-clip:text,border-box")]
    public void TextAndBorderAreaClipsFailExplicitly(string css)
    {
        Assert.That(Failure($"<div style='{css}'>x</div>").Code, Is.EqualTo("FPDF1202"));
    }

    [Test]
    public void ContentBoxWithPercentagePaddingFails()
    {
        Assert.That(Failure("<div style='padding:5%;background:#f00;background-clip:content-box'>x</div>").Code, Is.EqualTo("FPDF1201"));
    }

    [Test]
    public void BorderBoxClipIsAcceptedEverywhere()
    {
        const string body = "<ul><li>a</li></ul><table><tr><td>b</td></tr></table><p>c <span>d</span></p>";
        Assert.That(Render("<style>*{background-clip:border-box}</style>" + body), Is.EqualTo(Render(body)));
        // Inline text paints no background, so any clip there changes nothing.
        Assert.That(Render("<p>c <span style='background-clip:content-box'>d</span></p>"), Is.EqualTo(Render("<p>c <span>d</span></p>")));
    }

    [TestCase("background-repeat:no-repeat")]
    [TestCase("background-repeat:repeat-x")]
    [TestCase("background-repeat:space round")]
    [TestCase("background-repeat:repeat,no-repeat")]
    [TestCase("background-position:center")]
    [TestCase("background-position:left 10px top 20%")]
    [TestCase("background-position:10px,50% 50%")]
    [TestCase("background-position:center top 10px")]
    [TestCase("background-size:cover")]
    [TestCase("background-size:contain")]
    [TestCase("background-size:auto 10px")]
    [TestCase("background-size:50%,10px 20px")]
    [TestCase("background-size:initial")]
    public void ImagePlacementPropertiesChangeNothingWithoutAnImage(string css)
    {
        var plain = Render("<div style='background:#cee8fb;border:2pt solid #000'><p>Service summary</p></div>");
        var styled = Render($"<div style='background:#cee8fb;border:2pt solid #000;{css}'><p>Service summary</p></div>");
        Assert.That(styled, Is.EqualTo(plain));
    }

    [Test]
    public void ImagePlacementPropertiesAreAcceptedOnEveryElementAndThroughVariables()
    {
        const string css = "*{background-repeat:no-repeat;background-position:center;background-size:cover}";
        const string body = "<ul><li>a</li></ul><table><tr><td>b</td></tr></table><p>c <span>d</span></p><hr>";
        Assert.That(Render($"<style>{css}</style>{body}"), Is.EqualTo(Render(body)));
        Assert.That(Render("<style>:root{--p:center top}div{background-position:var(--p)}</style><div>x</div>"),
            Is.EqualTo(Render("<div>x</div>")));
    }

    [TestCase("background-repeat:repeat-x no-repeat")]
    [TestCase("background-repeat:diagonal")]
    [TestCase("background-position:left right")]
    [TestCase("background-position:10px 20px 30px 40px 50px")]
    [TestCase("background-size:-5px")]
    [TestCase("background-size:cover cover")]
    [TestCase("background-size:1px 2px 3px")]
    public void InvalidImagePlacementValuesFail(string css)
    {
        Assert.That(Failure($"<div style='{css}'>x</div>").Code, Is.EqualTo("FPDF1202"));
    }
}
