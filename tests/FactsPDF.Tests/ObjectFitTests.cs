using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// object-fit and object-position place an img's pixels inside its content box and clip them to it,
/// as Chrome prints them; every other element has nothing to place.
/// </summary>
[TestFixture]
public sealed partial class ObjectFitTests
{
    // An 80x40 pixel image is 60x30pt at 0.75pt per pixel.
    private static readonly PdfImage Image = PdfImage.FromBytes(PngFixture.Image(
        [.. Enumerable.Range(0, 40).SelectMany(_ => new byte[] { 0 }.Concat(Enumerable.Repeat((byte)200, 80 * 3)))],
        width: 80, height: 40, type: 2));

    private static string Render(string html)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert("<style>body{margin:0}</style>" + html, output, new PdfOptions { PageWidth = 300, PageHeight = 500, Margin = 20, Images = new Dictionary<string, PdfImage> { ["logo"] = Image } });
        return Encoding.Latin1.GetString(output.ToArray());
    }

    private static FactsPdfException Failure(string html)
    {
        using var output = new MemoryStream();
        return Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html,
            output, new PdfOptions { Images = new Dictionary<string, PdfImage> { ["logo"] = Image } }))!;
    }

    [GeneratedRegex(@"q ([\d.-]+) ([\d.-]+) ([\d.-]+) ([\d.-]+) re W n ([\d.-]+) 0 0 ([\d.-]+) ([\d.-]+) ([\d.-]+) cm /Im1 Do Q")]
    private static partial Regex Placement();

    // The clip box and the placed image as (clip x, clip top, clip width, clip height, image x, image top, image width, image height),
    // with tops measured down from the top of the 500pt page.
    private static double[] Placed(string style, string extra = "")
    {
        var pdf = Render($"<img src='logo' style='display:block;width:100pt;height:100pt;{style}'{extra}>");
        var match = Placement().Match(pdf);
        Assert.That(match.Success, Is.True, style);
        var n = match.Groups.Cast<Group>().Skip(1).Select(g => double.Parse(g.Value, CultureInfo.InvariantCulture)).ToArray();
        return [n[0], 500 - n[1] - n[3], n[2], n[3], n[6], 500 - n[7] - n[5], n[4], n[5]];
    }

    private static void Expect(string style, double x, double top, double width, double height)
    {
        var p = Placed(style);
        Assert.That(p[0], Is.EqualTo(20).Within(0.01), style);
        Assert.That(p[2], Is.EqualTo(100).Within(0.01), style);
        Assert.That(p[3], Is.EqualTo(100).Within(0.01), style);
        Assert.That(p[4], Is.EqualTo(20 + x).Within(0.01), style + " x");
        Assert.That(p[5] - p[1], Is.EqualTo(top).Within(0.01), style + " top");
        Assert.That(p[6], Is.EqualTo(width).Within(0.01), style + " width");
        Assert.That(p[7], Is.EqualTo(height).Within(0.01), style + " height");
    }

    [Test]
    public void ContainScalesToFitAndCentres() => Expect("object-fit:contain", 0, 25, 100, 50);

    [Test]
    public void CoverScalesToFillAndClips() => Expect("object-fit:cover", -50, 0, 200, 100);

    [Test]
    public void NoneKeepsTheNaturalSizeCentred() => Expect("object-fit:none", 20, 35, 60, 30);

    [Test]
    public void ScaleDownTakesTheSmallerOfNoneAndContain()
    {
        Expect("object-fit:scale-down", 20, 35, 60, 30);
        var small = Render("<img src='logo' style='display:block;width:30pt;height:30pt;object-fit:scale-down'>");
        Assert.That(small, Does.Contain(" 30 0 0 15 "));
    }

    [TestCase("object-fit:cover;object-position:left top", 0, 0, 200, 100)]
    [TestCase("object-fit:cover;object-position:right bottom", -100, 0, 200, 100)]
    [TestCase("object-fit:none;object-position:left top", 0, 0, 60, 30)]
    [TestCase("object-fit:none;object-position:top left", 0, 0, 60, 30)]
    [TestCase("object-fit:none;object-position:right bottom", 40, 70, 60, 30)]
    [TestCase("object-fit:none;object-position:10% 90%", 4, 63, 60, 30)]
    [TestCase("object-fit:none;object-position:10pt 5pt", 10, 5, 60, 30)]
    [TestCase("object-fit:none;object-position:-20pt -10pt", -20, -10, 60, 30)]
    [TestCase("object-fit:none;object-position:right 10pt bottom 5pt", 30, 65, 60, 30)]
    [TestCase("object-fit:none;object-position:left 10% top 20%", 4, 14, 60, 30)]
    [TestCase("object-fit:none;object-position:center top", 20, 0, 60, 30)]
    [TestCase("object-fit:none;object-position:top", 20, 0, 60, 30)]
    [TestCase("object-fit:none;object-position:right", 40, 35, 60, 30)]
    [TestCase("object-fit:none;object-position:25%", 10, 35, 60, 30)]
    [TestCase("object-fit:contain;object-position:right bottom", 0, 50, 100, 50)]
    [TestCase("object-fit:none;object-position:2em 1em;font-size:10pt", 20, 10, 60, 30)]
    public void PositionPlacesTheContent(string style, double x, double top, double width, double height) =>
        Expect(style, x, top, width, height);

    [Test]
    public void FillStretchesAndIgnoresThePosition()
    {
        var plain = Render("<img src='logo' style='width:100pt;height:100pt'>");
        Assert.That(Render("<img src='logo' style='width:100pt;height:100pt;object-fit:fill'>"), Is.EqualTo(plain));
        Assert.That(Render("<img src='logo' style='width:100pt;height:100pt;object-position:left top'>"), Is.EqualTo(plain));
        Assert.That(Render("<img src='logo' style='width:100pt;height:100pt;object-fit:cover;object-fit:fill'>"), Is.EqualTo(plain));
    }

    [Test]
    public void TheContentBoxExcludesPaddingAndBorder()
    {
        var pdf = Render("<img src='logo' style='display:block;width:100pt;height:100pt;padding:6pt;border:2pt solid #000;object-fit:cover'>");
        var match = Placement().Match(pdf);
        Assert.That(match.Success, Is.True);
        // The clip is the 100pt content box inside 8pt of padding and border.
        Assert.That(double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture), Is.EqualTo(28).Within(0.01));
        Assert.That(double.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture), Is.EqualTo(100).Within(0.01));
    }

    [Test]
    public void InheritTakesTheParentAndInitialIsFill()
    {
        var inherited = Render("<div style='object-fit:cover;object-position:left top'><img src='logo' style='display:block;width:100pt;height:100pt;object-fit:inherit;object-position:inherit'></div>");
        Assert.That(inherited, Is.EqualTo(Render("<img src='logo' style='display:block;width:100pt;height:100pt;object-fit:cover;object-position:left top'>")));
        // object-fit is not inherited without the keyword.
        var plain = Render("<img src='logo' style='display:block;width:100pt;height:100pt'>");
        Assert.That(Render("<div style='object-fit:cover'><img src='logo' style='display:block;width:100pt;height:100pt'></div>"), Is.EqualTo(plain));
        Assert.That(Render("<img src='logo' style='display:block;width:100pt;height:100pt;object-fit:cover;object-fit:initial'>"), Is.EqualTo(plain));
    }

    [Test]
    public void OtherElementsHaveNothingToFit()
    {
        const string body = "<p>x</p><ul><li>a</li></ul><table><tr><td>b</td></tr></table><svg width='40' height='20'><rect width='40' height='20' fill='red'/></svg>";
        Assert.That(Render("<style>*{object-fit:cover;object-position:left top}</style>" + body), Is.EqualTo(Render(body)));
    }

    [Test]
    public void ImagesInLinesCellsAndFlexItemsAreFittedToo()
    {
        Assert.That(Render("<p>aa <img src='logo' style='width:50pt;height:50pt;object-fit:cover'> bb</p>"), Does.Contain(" re W n "));
        Assert.That(Render("<table><tr><td><img src='logo' style='width:50pt;height:50pt;object-fit:none'></td></tr></table>"), Does.Contain(" re W n "));
        Assert.That(Render("<div style='display:flex'><img src='logo' style='width:50pt;height:50pt;object-fit:contain'></div>"), Does.Contain(" re W n "));
    }

    [TestCase("object-fit:stretch")]
    [TestCase("object-fit:cover contain")]
    [TestCase("object-position:left right")]
    [TestCase("object-position:10px left")]
    [TestCase("object-position:top 10px")]
    [TestCase("object-position:left 10px 20px")]
    [TestCase("object-position:1px 2px 3px 4px 5px")]
    [TestCase("object-position:center 10px top")]
    public void InvalidValuesFail(string css)
    {
        Assert.That(Failure($"<img src='logo' style='{css}'>").Code, Is.EqualTo("FPDF1202"));
    }
}
