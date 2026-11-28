using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

// canvas, video, iframe, embed and object are replaced elements whose content FactsPDF never loads.
// Where a browser's print geometry does not depend on the resource it is reproduced: canvas, video
// and iframe are 300 by 150 CSS px boxes with nothing drawn (an iframe with its 2px inset border),
// an embed with no src and an audio-less page add nothing, an object prints its fallback content.
// Wherever the resource or a player would change the output, the element fails instead. Before, each
// failed FPDF1102. One CSS px is 0.75pt.
[TestFixture]
public sealed class ReplacedBoxElementTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 400, PageHeight = 500, Margin = 20 };

    private static PlacedImage Box(string html) => TableLayoutTests.Layout(html, Page).Single().Images.Single();

    private static byte[] Pdf(string html) => TableLayoutTests.Pdf(html, Page);

    private static FactsPdfException Fails(string html)
    {
        using var output = new MemoryStream();
        var ex = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, Page))!;
        Assert.That(output.Length, Is.Zero);
        return ex;
    }

    private static void AssertSize(string html, double width, double height)
    {
        var box = Box(html);
        Assert.Multiple(() =>
        {
            Assert.That(box.Width, Is.EqualTo(width).Within(0.0001), html + " width");
            Assert.That(box.Height, Is.EqualTo(height).Within(0.0001), html + " height");
        });
    }

    [TestCase("<p>A <canvas></canvas> B</p>", 225, 112.5)]
    [TestCase("<p>A <canvas width='100' height='40'></canvas> B</p>", 75, 30)]
    [TestCase("<p>A <canvas width='100'></canvas> B</p>", 75, 112.5)]
    [TestCase("<p>A <canvas height='60'></canvas> B</p>", 225, 45)]
    [TestCase("<p>A <canvas width='100' height='40' style='width:200px'></canvas> B</p>", 150, 60)]
    [TestCase("<p>A <canvas width='100' height='40' style='height:100px'></canvas> B</p>", 187.5, 75)]
    public void CanvasIsAsLargeAsItsWidthAndHeightAndKeepsThatRatioWhenCssSetsOne(string html, double width, double height)
        => AssertSize(html, width, height);

    [TestCase("<p>A <video></video> B</p>", 225, 112.5)]
    [TestCase("<p>A <video width='100'></video> B</p>", 75, 112.5)]
    [TestCase("<p>A <video height='60'></video> B</p>", 225, 45)]
    [TestCase("<p>A <video width='100' height='60'></video> B</p>", 75, 45)]
    [TestCase("<p>A <video style='width:200px'></video> B</p>", 150, 112.5)]
    [TestCase("<p>A <video style='height:100px'></video> B</p>", 225, 75)]
    [TestCase("<p>A <video width='100' style='height:100px'></video> B</p>", 75, 75)]
    [TestCase("<p>A <video style='width:50%'></video> B</p>", 174, 112.5)]
    [TestCase("<p>A <iframe></iframe> B</p>", 225, 112.5)]
    [TestCase("<p>A <iframe width='100' height='60'></iframe> B</p>", 75, 45)]
    [TestCase("<p>A <iframe width='50%'></iframe> B</p>", 174, 112.5)]
    [TestCase("<p>A <iframe style='width:200px'></iframe> B</p>", 150, 112.5)]
    public void VideoAndIframeTakeEachDimensionIndependentlyFromAttributesAndCss(string html, double width, double height)
        => AssertSize(html, width, height);

    [Test]
    public void AnIframeAddsItsInsetBorderToTheBox()
    {
        var box = Box("<p>A <iframe></iframe> B</p>");
        Assert.That(box.Decoration!.Width, Is.EqualTo(228).Within(0.0001));
        Assert.That(box.Decoration.Height, Is.EqualTo(115.5).Within(0.0001));
        Assert.That(Box("<p>A <iframe frameborder='0'></iframe> B</p>").Decoration, Is.Null);
        Assert.That(Box("<p>A <iframe style='border:0'></iframe> B</p>").Decoration, Is.Null);
        Assert.That(Pdf("<p>A <iframe frameborder='no'></iframe> B</p>"), Is.EqualTo(Pdf("<p>A <iframe style='border:0'></iframe> B</p>")));
        Assert.That(Box("<p>A <iframe style='border-width:1px'></iframe> B</p>").Decoration!.Width, Is.EqualTo(226.5).Within(0.0001));
    }

    [Test]
    public void AnIframeBorderIsPaintedInTwoTonesOfChromesInsetColour()
    {
        var content = Encoding.Latin1.GetString(Pdf("<p>A <iframe></iframe> B</p>"));
        // Chrome prints currentcolor black as #eeeeee on the right and bottom and its darker shade
        // (floor(238/255 * (v - 0.33) / v * 256) = 154, #9a9a9a) on the top and left.
        Assert.That(content, Does.Contain("0.933 0.933 0.933 rg"));
        Assert.That(content, Does.Contain("0.604 0.604 0.604 rg"));
        var solid = Encoding.Latin1.GetString(Pdf("<p>A <iframe style='border:1.5pt solid #eee'></iframe> B</p>"));
        Assert.That(solid, Does.Not.Contain("0.604 0.604 0.604 rg"));
    }

    [TestCase("<p>A <iframe style='color:#c00'></iframe></p>")]
    [TestCase("<p>A <iframe style='border-color:#c00'></iframe></p>")]
    public void AnIframeInsetBorderWithAnotherColourFails(string html)
        => Assert.That(Fails(html).Code, Is.EqualTo("FPDF1201"));

    [Test]
    public void AnIframeWithAnAuthorBorderStyleAndColourIsDrawnLikeAnyBox()
        => Assert.That(Box("<p>A <iframe style='border:2px solid #ccc;color:#c00'></iframe></p>").Decoration!.Width,
            Is.EqualTo(228).Within(0.0001));

    [Test]
    public void CanvasVideoAndIframeContentIsNeverRendered()
    {
        Assert.That(Pdf("<p>A <canvas>Fallback <u>x</u><select></select></canvas> B</p>"), Is.EqualTo(Pdf("<p>A <canvas></canvas> B</p>")));
        Assert.That(Pdf("<p>A <video>Fallback<track kind='captions'></video> B</p>"), Is.EqualTo(Pdf("<p>A <video></video> B</p>")));
        Assert.That(Pdf("<p>A <iframe><p>Fallback</p><b>x</iframe> B</p>"), Is.EqualTo(Pdf("<p>A <iframe></iframe> B</p>")));
    }

    [Test]
    public void ABlockCanvasFollowsTheParagraphAboveIt()
    {
        var page = TableLayoutTests.Layout("<div>A</div><canvas style='display:block'></canvas><div>B</div>", Page).Single();
        var image = page.Images.Single();
        Assert.That(page.Runs[0].Baseline - page.Runs[1].Baseline, Is.EqualTo(14.4 + 112.5).Within(0.0001));
        Assert.That(image.Height, Is.EqualTo(112.5).Within(0.0001));
    }

    [Test]
    public void ABorderOnACanvasEnlargesTheBox()
        => Assert.That(Box("<p>A <canvas style='border:3px solid red'></canvas></p>").Decoration!.Width, Is.EqualTo(229.5).Within(0.0001));

    [TestCase("<video controls></video>", "FPDF1103")]
    [TestCase("<video src='a.mp4'></video>", "FPDF1103")]
    [TestCase("<video poster='p.png'></video>", "FPDF1103")]
    [TestCase("<video><source src='a.mp4'></video>", "FPDF1103")]
    [TestCase("<iframe src='a.html'></iframe>", "FPDF1103")]
    [TestCase("<iframe srcdoc='x'></iframe>", "FPDF1103")]
    [TestCase("<iframe scrolling='yes'></iframe>", "FPDF1103")]
    [TestCase("<iframe frameborder='2'></iframe>", "FPDF1103")]
    [TestCase("<iframe width='abc'></iframe>", "FPDF1103")]
    [TestCase("<canvas width='0'></canvas>", "FPDF1103")]
    [TestCase("<canvas width='12px'></canvas>", "FPDF1103")]
    [TestCase("<canvas width='-5'></canvas>", "FPDF1103")]
    [TestCase("<embed src='a.swf'>", "FPDF1103")]
    [TestCase("<embed type='application/pdf'>", "FPDF1103")]
    [TestCase("<object data='a.png'>x</object>", "FPDF1103")]
    [TestCase("<object type='image/png'>x</object>", "FPDF1103")]
    [TestCase("<canvas></canvas><p>x", null)]
    public void WhatNeedsAResourceOrAPlayerFails(string html, string? code)
    {
        if (code is null) Assert.That(Pdf(html), Is.Not.Empty);
        else Assert.That(Fails(html).Code, Is.EqualTo(code));
    }

    [Test]
    public void AnEmbedWithoutSrcIsNotRendered()
    {
        Assert.That(Pdf("<p>A <embed width='100' height='50' title='t'> B</p>"), Is.EqualTo(Pdf("<p>A B</p>")));
        Assert.That(Pdf("<style>embed{display:block}</style><p>A <embed> B</p>"), Is.EqualTo(Pdf("<p>A B</p>")));
    }

    [Test]
    public void AnObjectPrintsItsFallbackContentInline()
    {
        Assert.That(Pdf("<p>A <object name='o' width='100' height='50'>B <u>c</u></object> D</p>"), Is.EqualTo(Pdf("<p>A <span>B <u>c</u></span> D</p>")));
        Assert.That(Pdf("<style>object{color:#c00}</style><p>A <object>B</object></p>"), Is.EqualTo(Pdf("<style>span{color:#c00}</style><p>A <span>B</span></p>")));
    }

    [Test]
    public void ReplacedBoxesTakeVerticalAlignAndDisplayLikeAnImage()
    {
        Assert.That(Pdf("<p>A <canvas style='vertical-align:middle'></canvas></p>"), Is.Not.EqualTo(Pdf("<p>A <canvas></canvas></p>")));
        Assert.That(Pdf("<p>A <video style='display:inline-block'></video></p>"), Is.EqualTo(Pdf("<p>A <video></video></p>")));
    }

    [Test]
    public void ReplacedBoxesGenerateNoPseudoElements()
        => Assert.That(Pdf("<style>canvas::before{content:'x'}video::after{content:'y'}</style><p>A <canvas></canvas><video></video></p>"),
            Is.EqualTo(Pdf("<p>A <canvas></canvas><video></video></p>")));

    [Test]
    public void ACanvasInsideATableCellIsPlacedInline()
        => Assert.That(TableLayoutTests.Layout("<table><tr><td>A <canvas width='100' height='40'></canvas></td></tr></table>", Page).Single().Images.Single().Width,
            Is.EqualTo(75).Within(0.0001));
}
