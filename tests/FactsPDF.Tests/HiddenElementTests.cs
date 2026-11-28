using NUnit.Framework;

namespace FactsPDF.Tests;

// noscript (display:none !important with scripting enabled, as when a browser prints), datalist and
// a closed dialog (display:none) and an audio without controls (display:none !important) print
// nothing, whatever they hold. Before, each failed FPDF1102.
[TestFixture]
public sealed class HiddenElementTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 260, PageHeight = 300, Margin = 12 };

    private static byte[] Pdf(string html) => TableLayoutTests.Pdf(html, Page);

    private static FactsPdfException Fails(string html, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        var ex = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, options ?? Page))!;
        Assert.That(output.Length, Is.Zero);
        return ex;
    }

    [TestCase("<p>A</p><noscript><p>Enable scripts</p><img src='x'></noscript><p>B</p>")]
    [TestCase("<p>A <noscript><b>x</b> <select></select></noscript>B</p>")]
    [TestCase("<html><head><title>T</title><noscript><link rel='stylesheet' href='a.css'><style>p{color:red}</style></noscript></head><body><p>A</p></body></html>")]
    [TestCase("<noscript style='display:block' class='n'>x</noscript><p>A</p><NOSCRIPT >y</NoScript >")]
    public void NoscriptRendersAsIfAbsent(string html)
        => Assert.That(Pdf(html), Is.EqualTo(Pdf(Strip(html))));

    private static string Strip(string html)
    {
        var without = System.Text.RegularExpressions.Regex.Replace(html, "<noscript[^>]*>.*?</noscript\\s*>", "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);
        return without;
    }

    [Test]
    public void NoscriptContentIsRawTextSoMarkupAndStylesInsideItNeverApply()
        => Assert.That(Pdf("<noscript><style>p{color:red}</style><p class=a></noscript><p>B</p>"), Is.EqualTo(Pdf("<p>B</p>")));

    [Test]
    public void NoscriptIgnoresAnAuthorDisplayBecauseTheUserAgentRuleIsImportant()
        => Assert.That(Pdf("<style>noscript{display:block;color:red}</style><p>A</p><noscript>x</noscript>"), Is.EqualTo(Pdf("<p>A</p>")));

    [Test]
    public void NoscriptCountsTowardTheElementLimitAndNeedsItsEndTag()
    {
        var limited = new PdfOptions { PageWidth = 260, PageHeight = 300, Margin = 12, MaxElements = 2 };
        Assert.That(Fails("<p>A</p><noscript></noscript><noscript></noscript>", limited).Code, Is.EqualTo("FPDF1002"));
        Assert.That(Fails("<p>A</p><noscript>x").Code, Is.EqualTo("FPDF1101"));
        Assert.That(Fails("<p>A</p></noscript>").Code, Is.EqualTo("FPDF1101"));
    }

    [TestCase("<p>A</p><datalist id='d'><option value='x'>One</option><option>Two</datalist><p>B</p>", "<p>A</p><p>B</p>")]
    [TestCase("<p>A <datalist id='d'><option value='x'></datalist>B</p>", "<p>A B</p>")]
    [TestCase("<p>A</p><datalist><optgroup label='g'><option>One</optgroup><select></select></datalist><p>B</p>", "<p>A</p><p>B</p>")]
    public void DatalistAndItsOptionsAreNotRendered(string html, string without)
        => Assert.That(Pdf(html), Is.EqualTo(Pdf(without)));

    [TestCase("<p>A</p><dialog><p>Hidden</p><b>x</b></dialog><p>B</p>", "<p>A</p><p>B</p>")]
    [TestCase("<p>A</p><dialog class='m' id='d' closedby='none'>Hidden<dialog>nested</dialog></dialog><p>B</p>", "<p>A</p><p>B</p>")]
    public void AClosedDialogIsNotRendered(string html, string without)
        => Assert.That(Pdf(html), Is.EqualTo(Pdf(without)));

    [Test]
    public void ADialogClosesAnOpenParagraphLikeABlock()
        => Assert.That(Pdf("<p>A<dialog>x</dialog><p>B</p>"), Is.EqualTo(Pdf("<p>A</p><p>B</p>")));

    [Test]
    public void AnOpenDialogFailsBecauseItIsPositionedAbsolutely()
        => Assert.That(Fails("<p>A</p><dialog open>x</dialog>").Code, Is.EqualTo("FPDF1103"));

    [TestCase("<style>dialog{display:block}</style><p>A</p><dialog>x</dialog>")]
    [TestCase("<style>.shown{display:flex}</style><p>A</p><dialog class='shown'>x</dialog>")]
    [TestCase("<p>A</p><dialog style='display:block'>x</dialog>")]
    [TestCase("<style>datalist{display:block}</style><p>A</p><datalist><option>x</option></datalist>")]
    public void AnAuthorDisplayThatShowsADialogOrDatalistFails(string html)
        => Assert.That(Fails(html).Code, Is.EqualTo("FPDF1201"));

    [Test]
    public void AnAuthorDisplayNoneOnADialogChangesNothing()
        => Assert.That(Pdf("<style>dialog{display:none;margin:0;padding:0;border:0}</style><p>A</p><dialog>x</dialog>"), Is.EqualTo(Pdf("<p>A</p>")));

    [Test]
    public void ASubtreeIsNeverReadSoAnUnsupportedElementInsideDoesNotFail()
        => Assert.That(Pdf("<p>A</p><dialog><select><option>x</option></select><mark>m</mark></dialog><p>B</p>"), Is.EqualTo(Pdf("<p>A</p><p>B</p>")));

    [Test]
    public void AnAudioWithoutControlsIsNotRendered()
    {
        Assert.That(Pdf("<p>A <audio src='a.mp3' preload='none' loop muted autoplay><source src='a.ogg'><track kind='captions'>Fallback <b>text</b></audio>B</p>"),
            Is.EqualTo(Pdf("<p>A B</p>")));
        Assert.That(Pdf("<style>audio{display:block;width:100pt}</style><p>A</p><audio src='a'></audio><p>B</p>"), Is.EqualTo(Pdf("<p>A</p><p>B</p>")));
    }

    [Test]
    public void AnAudioWithControlsFailsBecauseItDrawsAPlayer()
        => Assert.That(Fails("<audio controls></audio>").Code, Is.EqualTo("FPDF1103"));

    [TestCase("<p>A</p><datalist>x</p><p>B</p>")]
    [TestCase("<p>A</p><dialog>x<p>B</p>")]
    [TestCase("<p>A</p><audio>x<p>B</p>")]
    public void AnUnclosedHiddenElementFails(string html)
        => Assert.That(Fails(html).Code, Is.EqualTo("FPDF1101"));

    [Test]
    public void ElementsInsideAHiddenSubtreeStillCountTowardTheElementLimit()
    {
        var limited = new PdfOptions { PageWidth = 260, PageHeight = 300, Margin = 12, MaxElements = 3 };
        Assert.That(Fails("<datalist><option></option><option></option><option></option></datalist>", limited).Code, Is.EqualTo("FPDF1002"));
    }

    [Test]
    public void TheHiddenAttributeChangesNothingOnElementsThatAreAlreadyNotShown()
        => Assert.That(Pdf("<p>A</p><datalist hidden></datalist><dialog hidden>x</dialog><audio hidden></audio><track hidden><p>B</p>"), Is.EqualTo(Pdf("<p>A</p><p>B</p>")));

    [Test]
    public void SiblingPositionsCountHiddenElements()
        => Assert.That(Pdf("<style>p:nth-child(4){color:#c00}</style><p>Z</p><noscript>x</noscript><datalist></datalist><p>A</p>"),
            Is.EqualTo(Pdf("<style>p:nth-child(2){color:#c00}</style><p>Z</p><p>A</p>")));
}
