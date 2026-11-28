using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// ::before and ::after (and the legacy :before and :after) with content:"" or a string generate
/// a box in their element: inline text, or a block box in a flex container or with display:block.
/// The built-in Courier font advances 7.2pt per glyph at 12pt with a 14.4pt line.
/// </summary>
[TestFixture]
public sealed class GeneratedContentTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 500, Margin = 20 };

    // The expected positions start at the page margin, so the user-agent body and paragraph margins are reset.
    private const string Neutral = "<style>body{margin:0}p{margin:0}</style>";

    private static List<LayoutPage> Layout(string html)
        => TextLayout.Layout(HtmlDocumentReader.ReadTree(Neutral + html, Page, default), Page, default);

    private static string Text(LayoutPage page) => string.Concat(page.Runs.Select(run => run.Text));

    private static byte[] Pdf(string html)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, Page);
        return output.ToArray();
    }

    private static void Fails(string html, string code)
    {
        using var output = new MemoryStream();
        output.Write("keep"u8);
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, Page));
        Assert.That(error!.Code, Is.EqualTo(code), error.Message);
        Assert.That(Encoding.ASCII.GetString(output.ToArray()), Is.EqualTo("keep"));
    }

    [Test]
    public void DividerLinesGrowAroundACentredTitle()
    {
        const string html = "<style>.divider{display:flex;align-items:center;gap:12pt}" +
            ".divider::before,.divider::after{content:\"\";flex-grow:1;height:1px;border-top:1px solid #000000}</style>" +
            "<div class='divider'>TITLE</div><p>NEXT</p>";
        var page = Layout(html)[0];
        // 260pt less two 12pt gaps and the 36pt title leaves 100pt for each line.
        var lines = page.PaintBoxes.OrderBy(box => box.X).ToArray();
        Assert.That(lines, Has.Length.EqualTo(2));
        Assert.That(lines[0].X, Is.EqualTo(20).Within(1e-9));
        Assert.That(lines[0].Width, Is.EqualTo(100).Within(1e-9));
        Assert.That(lines[1].X, Is.EqualTo(180).Within(1e-9));
        Assert.That(lines[1].Width, Is.EqualTo(100).Within(1e-9));
        // A 1px height and a 1px top border (1.5pt) centred in the 14.4pt line.
        Assert.That(lines[0].Height, Is.EqualTo(1.5).Within(1e-9));
        Assert.That(lines[0].Top, Is.EqualTo(20 + (14.4 - 1.5) / 2).Within(1e-9));
        Assert.That(page.Runs.Single(run => run.Text == "TITLE").X, Is.EqualTo(132).Within(1e-9));
        Assert.That(page.Runs.Single(run => run.Text == "NEXT").Baseline, Is.EqualTo(500 - 20 - 14.4 - 10.8).Within(1e-9));
    }

    [TestCase("::before", "::after")]
    [TestCase(":before", ":after")]
    [TestCase("::BEFORE", "::After")]
    public void InlineContentWrapsTheElementText(string before, string after)
    {
        var page = Layout($"<style>p{before}{{content:\"B\"}}p{after}{{content:\"C\"}}</style><p>A</p>")[0];
        Assert.That(Text(page), Is.EqualTo("BAC"));
        Assert.That(page.Runs.Min(run => run.X), Is.EqualTo(20).Within(1e-9));
    }

    [Test]
    public void StringsAreJoinedAndEscapesDecoded()
        => Assert.That(Text(Layout("<style>p::before{content:\"\\41\" 'B' \" \"}</style><p>C</p>")[0]), Is.EqualTo("AB C"));

    [Test]
    public void GeneratedTextInheritsFromItsElementAndTakesItsOwnRules()
    {
        var runs = Layout("<style>.n{color:#ff0000}.n::before{content:\"X\";text-decoration:underline}</style><p class='n'>Y</p>")[0].Runs;
        var generated = runs.Single(run => run.Text == "X");
        Assert.That(generated.Style.Color, Is.EqualTo(new Rgb(1, 0, 0)));
        Assert.That(generated.Style.Decoration, Is.EqualTo(TextDecoration.Underline));
        Assert.That(runs.Single(run => run.Text == "Y").Style.Decoration, Is.EqualTo(TextDecoration.None));
    }

    [Test]
    public void ABlockPseudoElementIsABoxBeforeTheContent()
    {
        var page = Layout("<style>.box::before{content:\"HEAD\";display:block;background:#eeeeee;padding:2pt}</style>" +
            "<div class='box'>BODY</div>")[0];
        var box = page.PaintBoxes.Single();
        Assert.That(box.Top, Is.EqualTo(20).Within(1e-9));
        Assert.That(box.Height, Is.EqualTo(14.4 + 4).Within(1e-9));
        Assert.That(page.Runs.Single(run => run.Text == "HEAD").X, Is.EqualTo(22).Within(1e-9));
        Assert.That(page.Runs.Single(run => run.Text == "BODY").Baseline, Is.EqualTo(500 - 20 - 18.4 - 10.8).Within(1e-9));
    }

    [Test]
    public void InAFlexContainerPseudoElementsAreFlexItems()
    {
        var pages = Layout("<style>.row{display:flex;justify-content:space-between}.row::after{content:\"END\"}</style>" +
            "<div class='row'>START</div>");
        Assert.That(pages[0].Runs.Single(run => run.Text == "END").X, Is.EqualTo(280 - 21.6).Within(1e-9));
    }

    [TestCase("content:none")]
    [TestCase("content:normal")]
    [TestCase("content:\"x\";display:none")]
    public void NoBoxIsGeneratedWithoutContentOrWithDisplayNone(string declarations)
        => Assert.That(Pdf($"<style>p::before{{{declarations}}}</style><p>A</p>"), Is.EqualTo(Pdf("<p>A</p>")));

    [Test]
    public void PseudoElementRulesWithoutContentChangeNothing()
        => Assert.That(Pdf("<style>*,*::before,*::after{box-sizing:border-box}</style><p>A</p>"),
            Is.EqualTo(Pdf("<style>*{box-sizing:border-box}</style><p>A</p>")));

    [Test]
    public void ContentOnAnElementChangesNothing()
        => Assert.That(Pdf("<style>p{content:\"x\"}</style><p>A</p>"), Is.EqualTo(Pdf("<p>A</p>")));

    [Test]
    public void AHiddenElementTakesItsPseudoElementsWithIt()
        => Assert.That(Pdf("<style>.d::before{content:\"X\"}</style><p>A</p><div class='d' style='display:none'>B</div>"),
            Is.EqualTo(Pdf("<p>A</p>")));

    [TestCase("<style>p::before{content:counter(item,disc)}</style><p>A</p>", "FPDF1202")]
    [TestCase("<style>p::before{content:attr(title)}</style><p>A</p>", "FPDF1202")]
    [TestCase("<style>p::before{content:url(a.png)}</style><p>A</p>", "FPDF1202")]
    [TestCase("<style>p::before{content:open-quote}</style><p>A</p>", "FPDF1202")]
    [TestCase("<style>.unused::after{content:\"a\" counters(x)}</style><p>A</p>", "FPDF1202")]
    [TestCase("<style>p::before{content:\"x\";display:inline-block}</style><p>A</p>", "FPDF1202")]
    [TestCase("<style>p::first-line{color:red}</style><p>A</p>", "FPDF1203")]
    [TestCase("<style>p::marker{color:red}</style><p>A</p>", "FPDF1203")]
    [TestCase("<style>p::before::after{content:\"x\"}</style><p>A</p>", "FPDF1203")]
    [TestCase("<style>p::before span{color:red}</style><p>A</p>", "FPDF1203")]
    [TestCase("<style>p::before{content:\"x\";display:block}</style><p>A</p>", "FPDF1201")]
    [TestCase("<style>p::before{content:\"x\";padding:2pt}</style><p>A</p>", "FPDF1201")]
    [TestCase("<style>hr::before{content:\"x\"}</style><hr>", "FPDF1201")]
    public void UnsupportedGeneratedContentFailsWithoutOutput(string html, string code) => Fails(html, code);
}
