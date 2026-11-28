using NUnit.Framework;

namespace FactsPDF.Tests;

// button, the push-button inputs and the text-field inputs print as Chrome's native controls (a rounded,
// stroked box with the label, value or placeholder on top); every other control still fails explicitly.
[TestFixture]
public sealed class FormControlTests
{
    private static readonly PdfOptions Page = new()
    {
        PageWidth = 300, PageHeight = 200, Margin = 12, Fonts = [PdfFont.FromBytes(FontFixture.Create())]
    };

    private static byte[] Pdf(string html, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, options ?? Page);
        return output.ToArray();
    }

    private static FactsPdfException Fails(string html, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        var ex = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, options ?? Page))!;
        Assert.That(output.Length, Is.Zero);
        return ex;
    }

    [Test]
    public void ButtonWidthFollowsItsLabel()
        => Assert.That(Pdf("<p>x <button>A</button></p>"), Is.Not.EqualTo(Pdf("<p>x <button>AB</button></p>")));

    [Test]
    public void SubmitInputAndButtonElementPrintTheSameBox()
        => Assert.That(Pdf("<p><input type=button value='A B'></p>"), Is.EqualTo(Pdf("<p><button>A B</button></p>")));

    [Test]
    public void ButtonLabelWhitespaceCollapses()
        => Assert.That(Pdf("<p><button>\n  A \n B </button></p>"), Is.EqualTo(Pdf("<p><button>A B</button></p>")));

    [Test]
    public void TextFieldWidthFollowsSizeNotValue()
    {
        Assert.That(Pdf("<p><input size=10></p>"), Is.Not.EqualTo(Pdf("<p><input size=20></p>")));
        Assert.That(Pdf("<p><input size=0></p>"), Is.EqualTo(Pdf("<p><input></p>")), "an invalid size is 20");
        Assert.That(Pdf("<p><input type=email></p>"), Is.EqualTo(Pdf("<p><input type=text></p>")));
    }

    [Test]
    public void PlaceholderShowsOnlyWithoutAValue()
    {
        Assert.That(Pdf("<p><input placeholder=A></p>"), Is.Not.EqualTo(Pdf("<p><input></p>")));
        Assert.That(Pdf("<p><input value=B placeholder=A></p>"), Is.EqualTo(Pdf("<p><input value=B></p>")));
    }

    [Test]
    public void HiddenInputPrintsNothing()
        => Assert.That(Pdf("<p>A<input type=hidden value=B></p>"), Is.EqualTo(Pdf("<p>A</p>")));

    [Test]
    public void ControlsWorkInTableCellsAndSitOnTheTextBaseline()
    {
        Assert.That(Pdf("<table><tbody><tr><td><button>A</button></td></tr></tbody></table>"), Is.Not.Empty);
        Assert.That(Pdf("<p>A <button>A</button> B</p>"), Is.Not.EqualTo(Pdf("<p>A <button style='vertical-align:top'>A</button> B</p>")));
    }

    [TestCase("<input type=checkbox>", "FPDF1103")]
    [TestCase("<input type=number>", "FPDF1103")]
    [TestCase("<input disabled>", "FPDF1103")]
    [TestCase("<button><b>A</b></button>", "FPDF1101")]
    [TestCase("<button></button>", "FPDF1103")]
    [TestCase("<button type=menu>A</button>", "FPDF1103")]
    [TestCase("<button style='background-color:#eee'>A</button>", "FPDF1201")]
    [TestCase("<input style='width:40pt'>", "FPDF1201")]
    [TestCase("<input style='border:1pt solid #000'>", "FPDF1201")]
    [TestCase("<input style='display:block'>", "FPDF1201")]
    public void UnsupportedControlsFailExplicitly(string control, string code)
        => Assert.That(Fails($"<p>{control}</p>").Code, Is.EqualTo(code));

    [Test]
    public void ValueWiderThanTheFieldAndMissingFontFail()
    {
        Assert.That(Fails($"<p><input size=2 value='{new string('A', 20)}'></p>").Code, Is.EqualTo("FPDF1302"));
        Assert.That(Fails("<p><button>A</button></p>", new PdfOptions { PageWidth = 300, PageHeight = 200 }).Code, Is.EqualTo("FPDF1301"));
    }
}
