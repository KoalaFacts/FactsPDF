using NUnit.Framework;

namespace FactsPDF.Tests;

// The HTML hidden attribute is the user-agent rule [hidden]{display:none} (R5cm). On the ordinary
// report blocks that accept display:none it removes the block like that declaration; author CSS
// display:block still shows it, as in a browser. Before, any hidden attribute failed FPDF1103.
[TestFixture]
public sealed class HiddenAttributeTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 260, PageHeight = 300, Margin = 12 };

    private static byte[] Pdf(string html)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, Page);
        return output.ToArray();
    }

    [TestCase("<p>Invoice 42</p><p hidden>Draft note</p><p>Total</p>", "<p>Invoice 42</p><p>Total</p>")]
    [TestCase("<h1 hidden=\"\">Internal</h1><p>Invoice</p>", "<p>Invoice</p>")]
    [TestCase("<div HIDDEN=Hidden><p>Secret</p><p>More</p></div><p>Visible</p>", "<p>Visible</p>")]
    [TestCase("<section hidden='hidden' class=x style='margin:10pt'><h2>Audit</h2></section><p>Shown</p>", "<p>Shown</p>")]
    [TestCase("<header hidden><p>Banner</p></header><main><p>Body</p><aside hidden>Ad</aside></main>",
        "<main><p>Body</p></main>")]
    [TestCase("<div>Before <div hidden>gone</div> after</div>", "<div>Before after</div>")]
    [TestCase("<blockquote hidden>Quote</blockquote><pre hidden>code</pre><dl><dt hidden>Term</dt><dd>Def</dd></dl>",
        "<dl><dd>Def</dd></dl>")]
    public void HiddenBlocksRenderAsIfAbsent(string html, string without)
        => Assert.That(Pdf(html), Is.EqualTo(Pdf(without)));

    [TestCase("<style>.show{display:block}</style><p hidden class=show>Shown</p>", "<p>Shown</p>")]
    [TestCase("<div hidden style='display:block'><p>Shown</p></div>", "<div><p>Shown</p></div>")]
    public void AuthorDisplayBlockOverridesHidden(string html, string without)
        => Assert.That(Pdf(html), Is.EqualTo(Pdf(without)));

    [Test]
    public void HiddenElementsShareStylesWithVisibleOnesWithoutLeaking()
    {
        // Same declarations on both: a cached box must not carry display:none to a visible sibling.
        const string css = "<style>div{border:1pt solid #000;padding:4pt}</style>";
        Assert.That(Pdf(css + "<div hidden>A</div><div>B</div><div hidden>C</div><div>D</div>"),
            Is.EqualTo(Pdf(css + "<div>B</div><div>D</div>")));
        Assert.That(Pdf(css + "<div>B</div><div hidden>A</div><div>D</div>"),
            Is.EqualTo(Pdf(css + "<div>B</div><div>D</div>")));
    }

    [Test]
    public void HiddenContentIsStillValidated()
    {
        using var output = new MemoryStream();
        var ex = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert("<div hidden><blink>x</blink></div>", output, Page))!;
        Assert.That(ex.Code, Is.EqualTo("FPDF1102"));
    }

    [TestCase("<p hidden=until-found>A</p>")]
    [TestCase("<p hidden=yes>A</p>")]
    [TestCase("<p>A <span hidden>b</span></p>")]
    [TestCase("<ul><li hidden>A</li></ul>")]
    [TestCase("<table><colgroup><col style='width:236pt'></colgroup><tbody><tr><td hidden>A</td></tr></tbody></table>")]
    [TestCase("<table><colgroup><col style='width:236pt'></colgroup><tbody><tr><td><p hidden>A</p></td></tr></tbody></table>")]
    [TestCase("<ul><li><div hidden>A</div></li></ul>")]
    [TestCase("<body hidden><p>A</p></body>")]
    [TestCase("<img hidden src=a alt=''>")]
    [TestCase("<hr hidden>")]
    public void HiddenOutsideReportBlocksFails(string html)
    {
        using var output = new MemoryStream();
        var ex = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, Page))!;
        Assert.Multiple(() =>
        {
            Assert.That(ex.Code, Is.EqualTo("FPDF1103"));
            Assert.That(output.Length, Is.Zero);
        });
    }
}
