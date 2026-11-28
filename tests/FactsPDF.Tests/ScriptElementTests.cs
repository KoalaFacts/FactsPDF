using NUnit.Framework;

namespace FactsPDF.Tests;

// FactsPDF runs no scripts and HTML never renders a script element, so a script is dropped with
// its data and attributes and the page renders as if it were absent (R5cj). Before, every script
// failed FPDF1102.
[TestFixture]
public sealed class ScriptElementTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 260, PageHeight = 300, Margin = 12 };

    // Neutralise the Chrome user-agent defaults (body margin, paragraph and list spacing) so the
    // exact coordinates below stay measured against the page content box.
    private const string Reset = "<style>html,body{margin:0}p{margin:0 0 8pt}ul,ol{margin:0;padding:0}</style>";

    private static string Neutral(string html)
    {
        if (!html.StartsWith("<html", StringComparison.Ordinal))
        {
            return Reset + html;
        }

        var end = html.IndexOf('>') + 1;
        return html[..end] + Reset + html[end..];
    }

    private static byte[] Pdf(string html, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(Neutral(html), output, options ?? Page);
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
    // The escaped and double-escaped script data states (Chrome 158 prints A and B for each): inside
    // "<!--" a "<script" hides the next "</script", and "-->" returns to the plain state.
    [TestCase("<p>A</p><script><!--<script>x</script>--></script><p>B</p>", "<p>A</p><p>B</p>")]
    [TestCase("<p>A</p><script><!-- <SCRIPT\n> </script> </script><p>B</p>", "<p>A</p><p>B</p>")]
    [TestCase("<p>A</p><script><!-- x </script><p>B</p>", "<p>A</p><p>B</p>")]
    [TestCase("<p>A</p><script><!-- <script> --> </script><p>B</p>", "<p>A</p><p>B</p>")]
    [TestCase("<p>A</p><script><!--> <script> </script><p>B</p>", "<p>A</p><p>B</p>")]
    [TestCase("<p>A</p><script><!-- <scripty> </script><p>B</p>", "<p>A</p><p>B</p>")]
    [TestCase("<p>A</p><script><!-- <script/> </script/> </script ><p>B</p>", "<p>A</p><p>B</p>")]
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
        using var output = new MemoryStream();
        PdfConverter.Convert("<p>A</p><script></script>", output, limited);
        Assert.That(output.Length, Is.GreaterThan(0));
        output.SetLength(0);
        var ex = Assert.Throws<FactsPdfException>(() =>
            PdfConverter.Convert("<p>A</p><script></script><script></script>", output, limited))!;
        Assert.That(ex.Code, Is.EqualTo("FPDF1002"));
    }

    [TestCase("<p>A</p><script>x()", "FPDF1101", 8)]
    [TestCase("<p>A</p><script>x()</scriptx>", "FPDF1101", 8)]
    [TestCase("<p>A</p></script>", "FPDF1101", 8)]
    [TestCase("<p>A</p><script><!-- <script> </script><p>B</p>", "FPDF1101", 8)]
    [TestCase("<table><colgroup><script></script><col style='width:50pt'><col style='width:186pt'></colgroup><tbody><tr><td>A</td><td>B</td></tr></tbody></table>", "FPDF1101", 17)]
    [TestCase("<table><colgroup><col style='width:50pt'><script></script><col style='width:186pt'><tbody><tr><td>A</td><td>B</td></tr></tbody></table>", "FPDF1101", 41)]
    public void UnclosedStrayOrDoubleEscapedScriptsFail(string html, string code, int offset)
    {
        using var output = new MemoryStream();
        var ex = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(Neutral(html), output, Page))!;
        Assert.Multiple(() =>
        {
            Assert.That(ex.Code, Is.EqualTo(code));
            Assert.That(ex.SourceOffset, Is.EqualTo(Reset.Length + offset));
            Assert.That(output.Length, Is.Zero);
        });
    }
}
