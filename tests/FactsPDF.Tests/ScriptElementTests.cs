using NUnit.Framework;

namespace FactsPDF.Tests;

// FactsPDF runs no scripts and HTML never renders a script element, so a script is dropped with
// its data and attributes and the page renders as if it were absent (R5cj). Before, every script
// failed FPDF1102.
[TestFixture]
public sealed class ScriptElementTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 260, PageHeight = 300, Margin = 12 };

    private static byte[] Pdf(string html, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, options ?? Page);
        return output.ToArray();
    }

    [TestCase("<html><head><title>T</title><script src='app.js' defer></script><script type=application/ld+json>{\"@type\":\"Invoice\"}</script></head><body><p>A</p></body></html>",
        "<html><head><title>T</title></head><body><p>A</p></body></html>")]
    [TestCase("<p>Invoice 42</p><script>if (a < b && c > d) document.write('<p>x</p>');</script><p>Total</p>",
        "<p>Invoice 42</p><p>Total</p>")]
    [TestCase("<p>Order <script>track()</script>confirmed</p>", "<p>Order confirmed</p>")]
    [TestCase("<ul><script>1</script><li>Shown</li></ul>", "<ul><li>Shown</li></ul>")]
    [TestCase("<table><colgroup><col style='width:236pt'></colgroup><tbody><script></script><tr><td>Row<script>x</script></td></tr></tbody></table>",
        "<table><colgroup><col style='width:236pt'></colgroup><tbody><tr><td>Row</td></tr></tbody></table>")]
    [TestCase("<SCRIPT async nonce=abc style='position:absolute'>var s = '</scripts>'; // </script-x\n</SCRIPT ><p>After</p>",
        "<p>After</p>")]
    [TestCase("<script src=a.js /><p>Swallowed</p></script><p>Body</p>", "<p>Body</p>")]
    [TestCase("<script><!-- legacy(); //--></script><p>Body</p>", "<p>Body</p>")]
    [TestCase("<template><script>x()</script><p>T</p></template><p>Body</p>", "<p>Body</p>")]
    [TestCase("<table><colgroup><col style='width:236pt'></colgroup><script></script><tbody><tr><td>Row</td></tr></tbody></table>",
        "<table><colgroup><col style='width:236pt'></colgroup><tbody><tr><td>Row</td></tr></tbody></table>")]
    public void RendersAsIfAbsent(string html, string without)
        => Assert.That(Pdf(html), Is.EqualTo(Pdf(without)));

    [Test]
    public void MarkupAndStylesInsideScriptDataDoNotApply()
    {
        const string page = "<p class=a>Paid</p>";
        Assert.That(Pdf("<script>document.write('<style>.a{color:red}</style><video>');</script>" + page),
            Is.EqualTo(Pdf(page)));
    }

    [Test]
    public void ScriptsCountTowardTheElementLimit()
    {
        var limited = new PdfOptions { PageWidth = 260, PageHeight = 300, Margin = 12, MaxElements = 2 };
        Assert.That(Pdf("<p>A</p><script></script>", limited), Is.Not.Empty);
        using var output = new MemoryStream();
        var ex = Assert.Throws<FactsPdfException>(() =>
            PdfConverter.Convert("<p>A</p><script></script><script></script>", output, limited))!;
        Assert.That(ex.Code, Is.EqualTo("FPDF1002"));
    }

    [TestCase("<p>A</p><script>x()", "FPDF1101", 8)]
    [TestCase("<p>A</p><script>x()</scriptx>", "FPDF1101", 8)]
    [TestCase("<p>A</p></script>", "FPDF1101", 8)]
    [TestCase("<head><title>Ti<script>x</script>tle</title></head>", "FPDF1101", 15)]
    [TestCase("<p>A</p><script><!--<script>x</script>--></script>", "FPDF1101", 16)]
    [TestCase("<table><colgroup><script></script><col style='width:50pt'><col style='width:186pt'></colgroup><tbody><tr><td>A</td><td>B</td></tr></tbody></table>", "FPDF1101", 17)]
    [TestCase("<table><colgroup><col style='width:50pt'><script></script><col style='width:186pt'><tbody><tr><td>A</td><td>B</td></tr></tbody></table>", "FPDF1101", 41)]
    public void UnclosedStrayOrDoubleEscapedScriptsFail(string html, string code, int offset)
    {
        using var output = new MemoryStream();
        var ex = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, Page))!;
        Assert.Multiple(() =>
        {
            Assert.That(ex.Code, Is.EqualTo(code));
            Assert.That(ex.SourceOffset, Is.EqualTo(offset));
            Assert.That(output.Length, Is.Zero);
        });
    }
}
