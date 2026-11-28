using NUnit.Framework;

namespace FactsPDF.Tests;

// Elements that lay out as an existing element and add no rendering of their own: custom elements
// (x-card) are inline spans until CSS makes them blocks, label, output, map, picture and
// bdo with dir=ltr are spans, form is a div, slot is display:contents, and source, track and area
// draw nothing. Before, each failed FPDF1102.
[TestFixture]
public sealed class CustomAndInlineElementTests
{
    private static readonly PdfOptions Page = new()
    {
        PageWidth = 260, PageHeight = 300, Margin = 12,
        Images = new Dictionary<string, PdfImage> { ["logo"] = PdfImage.FromBytes(PngFixture.Image([0, 80, 100], type: 0)) }
    };

    private static byte[] Pdf(string html) => TableLayoutTests.Pdf(html, Page);

    private static FactsPdfException Fails(string html)
    {
        using var output = new MemoryStream();
        var ex = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, Page))!;
        Assert.That(output.Length, Is.Zero);
        return ex;
    }

    [TestCase("<p>A <x-card>B <u>c</u></x-card> D</p>")]
    [TestCase("<x-card>Alone</x-card>")]
    [TestCase("<p>A <x-a>1<x-b>2</x-b></x-a> D</p>")]
    [TestCase("<p>A <x-card variant='big' data-id='3' hidden-ish='yes'>B</x-card> D</p>")]
    public void CustomElementsRenderLikeSpans(string html)
        => Assert.That(Pdf(html), Is.EqualTo(Pdf(html.Replace("x-card", "span").Replace("x-a", "span").Replace("x-b", "span")
            .Replace(" variant='big' data-id='3' hidden-ish='yes'", ""))));

    [Test]
    public void CustomElementStyleFollowsTheTypeSelector()
        => Assert.That(Pdf("<style>x-card{color:#c00;text-decoration:underline}</style><p>A <x-card>B</x-card></p>"),
            Is.EqualTo(Pdf("<style>span{color:#c00;text-decoration:underline}</style><p>A <span>B</span></p>")));

    [Test]
    public void CustomElementAttributesCanBeSelected()
        => Assert.That(Pdf("<style>x-card[variant=big]{color:#c00}</style><p>A <x-card variant=big>B</x-card> <x-card>C</x-card></p>"),
            Is.EqualTo(Pdf("<style>.big{color:#c00}</style><p>A <span class=big>B</span> <span>C</span></p>")));

    [TestCase("display:block")]
    [TestCase("display:block;padding:4pt;border:1pt solid #000;background-color:#eee")]
    public void CustomElementMadeABlockRendersLikeADiv(string style)
        => Assert.That(Pdf($"<p>A</p><x-card style='{style}'>B</x-card><p>C</p>"),
            Is.EqualTo(Pdf($"<p>A</p><div style='{style}'>B</div><p>C</p>")));

    [Test]
    public void CustomElementBlockFromAStylesheetKeepsItsChildrenAndEndTag()
        => Assert.That(Pdf("<style>x-list,x-item{display:block}x-item{padding:3pt}</style><x-list><x-item>one</x-item><x-item>two</x-item></x-list><p>C</p>"),
            Is.EqualTo(Pdf("<div><div style='padding:3pt'>one</div><div style='padding:3pt'>two</div></div><p>C</p>")));

    [Test]
    public void CustomElementWithDisplayNoneIsRemovedWithItsSubtree()
        => Assert.That(Pdf("<style>x-card{display:none}</style><p>A</p><x-card>B<b>c</b><x-card>d</x-card></x-card><p>C</p>"),
            Is.EqualTo(Pdf("<p>A</p><p>C</p>")));

    [Test]
    public void ABlockCustomElementInsideAParagraphFails()
        => Assert.That(Fails("<style>x-card{display:block}</style><p>A <x-card>B</x-card></p>").Code, Is.EqualTo("FPDF1101"));

    [Test]
    public void DisplayInlineBlockOnACustomElementStillFails()
        => Assert.That(Fails("<style>x-card{display:inline-block}</style><p>A <x-card>B</x-card></p>").Code, Is.EqualTo("FPDF1202"));

    [TestCase("<p>A <x-card/>B</p>", "FPDF1101")]
    [TestCase("<p>A <x-card>B</p>", "FPDF1101")]
    [TestCase("<p>A <font-face>B</font-face></p>", "FPDF1102")]
    [TestCase("<p>A <xcard>B</xcard></p>", "FPDF1102")]
    public void OnlyValidCustomElementNamesAreAccepted(string html, string code)
        => Assert.That(Fails(html).Code, Is.EqualTo(code));

    [TestCase("<p>A <label for='x'>B</label> <output name='o' for='a b'>C</output> D</p>",
        "<p>A <span>B</span> <span>C</span> D</p>")]
    [TestCase("<p>A <map name='m'>B<area shape='rect' coords='0,0,1,1' href='x' alt='a'></map> C</p>",
        "<p>A <span>B</span> C</p>")]
    [TestCase("<p>A <bdo dir='ltr'>B</bdo> <bdo>C</bdo> D</p>", "<p>A <span>B</span> <span>C</span> D</p>")]
    [TestCase("<style>label{color:#c00}</style><p>A <label>B</label></p>", "<style>span{color:#c00}</style><p>A <span>B</span></p>")]
    public void LabelOutputMapAndBdoRenderLikeSpans(string html, string span)
        => Assert.That(Pdf(html), Is.EqualTo(Pdf(span)));

    [Test]
    public void BdoWithARightToLeftDirectionFails()
        => Assert.That(Fails("<p>A <bdo dir='rtl'>B</bdo></p>").Code, Is.EqualTo("FPDF1103"));

    [Test]
    public void PictureDrawsItsFallbackImageAndIgnoresSource()
        => Assert.That(Pdf("<p>A <picture><source srcset='logo.webp' type='image/webp'><source srcset='logo.png 1x' sizes='50vw'><img src='logo'></picture> B</p>"),
            Is.EqualTo(Pdf("<p>A <img src='logo'> B</p>")));

    [Test]
    public void SourceMediaConditionFails()
        => Assert.That(Fails("<p><picture><source media='(min-width: 600px)' srcset='x.png'><img src='logo'></picture></p>").Code,
            Is.EqualTo("FPDF1103"));

    [Test]
    public void SourceTrackAndAreaAreNotRendered()
        => Assert.That(Pdf("<p>A <picture><source srcset='a'><track kind='captions'><img src='logo'></picture> <map name='m'><area shape='rect' coords='0,0,1,1'></map> B</p>"),
            Is.EqualTo(Pdf("<p>A <img src='logo'> <span></span> B</p>")));

    [TestCase("<style>source{display:block}</style><p><picture><source srcset='a'><img src='logo'></picture></p>")]
    [TestCase("<style>.s{display:block}</style><p><picture><source class='s' srcset='a'><img src='logo'></picture></p>")]
    [TestCase("<p><picture><source style='display:block' srcset='a'><img src='logo'></picture></p>")]
    [TestCase("<style>area{display:block}</style><p><map name='m'><area shape='rect' coords='0,0,1,1'></map></p>")]
    public void AnAuthorDisplayThatShowsAHiddenVoidElementInsideInlineContentFails(string html)
        => Assert.That(Fails(html).Code, Is.EqualTo("FPDF1101"));

    [TestCase("source")]
    [TestCase("track")]
    [TestCase("area")]
    public void AnAuthorDisplayShowsAStandaloneHiddenVoidElementAsAnEmptyBlock(string tag)
        => Assert.That(Pdf($"<style>{tag}{{display:block;padding:4pt;border:1pt solid #000;height:20pt}}</style><div>A</div><{tag}><div>B</div>"),
            Is.EqualTo(Pdf("<div>A</div><div style='padding:4pt;border:1pt solid #000;height:20pt'></div><div>B</div>")));

    [Test]
    public void AnEmbedHasNoBoxWhateverItsDisplay()
        => Assert.That(Pdf("<style>embed{display:block;padding:4pt;border:1pt solid #000;height:20pt}</style><div>A</div><embed><div>B</div>"),
            Is.EqualTo(Pdf("<div>A</div><div>B</div>")));

    [Test]
    public void FormRendersLikeADivAndItsSubmissionAttributesChangeNothing()
        => Assert.That(Pdf("<p>A</p><form action='/x' method='post' enctype='text/plain' target='_blank' name='f' autocomplete='off' novalidate accept-charset='utf-8' style='padding:4pt;border:1pt solid #000'><p>B</p></form><p>C</p>"),
            Is.EqualTo(Pdf("<p>A</p><div style='padding:4pt;border:1pt solid #000'><p>B</p></div><p>C</p>")));

    [Test]
    public void FormClosesAnOpenParagraphLikeABlock()
        => Assert.That(Pdf("<p>A<form>B</form>"), Is.EqualTo(Pdf("<p>A</p><div>B</div>")));

    [Test]
    public void NestedFormsAndUnknownFormAttributesFail()
    {
        Assert.That(Fails("<form><form>A</form></form>").Code, Is.EqualTo("FPDF1101"));
        Assert.That(Fails("<form foo='1'>A</form>").Code, Is.EqualTo("FPDF1103"));
    }

    [Test]
    public void SlotRendersLikeASpanAndItsBlocksJoinTheBlockAroundIt()
    {
        Assert.That(Pdf("<p>A <slot name='s'>B <u>c</u></slot> D</p>"), Is.EqualTo(Pdf("<p>A <span>B <u>c</u></span> D</p>")));
        Assert.That(Pdf("<div>A<slot>B<p>C</p>D</slot>E</div>"), Is.EqualTo(Pdf("<div>AB<p>C</p>DE</div>")));
    }

    [TestCase("<style>slot{padding:2pt}</style><p>A <slot>B</slot></p>")]
    [TestCase("<style>slot{background-color:#eee}</style><p>A <slot>B</slot></p>")]
    [TestCase("<style>slot{text-decoration:underline}</style><p>A <slot>B</slot></p>")]
    public void ABoxOrADecorationOnASlotFails(string html)
        => Assert.That(Fails(html).Code, Is.EqualTo("FPDF1201"));

    [Test]
    public void ATextStyleOnASlotInheritsLikeASpan()
        => Assert.That(Pdf("<style>slot{color:#c00;font-style:normal}</style><p>A <slot>B</slot></p>"),
            Is.EqualTo(Pdf("<style>span{color:#c00;font-style:normal}</style><p>A <span>B</span></p>")));
}
