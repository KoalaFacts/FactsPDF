using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// &lt;html&gt; has no margin or padding, so html{margin:0;padding:0} and its CSS-wide keyword forms
/// render byte-identical to the page without them. &lt;body&gt; has the 8px (6pt) user-agent margin,
/// so body{margin:0} is a real reset that moves the content, and a body margin is laid out like
/// any other block margin. A non-zero &lt;html&gt; inset still fails with FPDF1201 and writes nothing.
/// </summary>
[TestFixture]
public sealed class RootZeroInsetTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 400, Margin = 20 };

    private const string Content =
        "<h2>Invoice</h2><div style='border:1pt solid #333;padding:4pt'><p>Paid in full</p></div><p>Thanks</p>";

    private static byte[] Pdf(string html)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, Page);
        return output.ToArray();
    }

    private static string Page_(string css) =>
        $"<!doctype html><html><head><style>{css}</style></head><body>{Content}</body></html>";

    [TestCase("html{margin:0;padding:0}")]
    [TestCase("html{margin:0rem;padding:0em}")]
    [TestCase("html{margin:initial;padding:unset}")]
    [TestCase("html{margin:0!important;padding:-0pt}")]
    [TestCase("HTML{MARGIN:0}")]
    // body{margin:8px} is the user-agent value itself.
    [TestCase("body{margin:8px}")]
    [TestCase("body{margin:revert}")]
    [TestCase("html{margin:0}body{margin:inherit;margin:8px 6pt}")]
    public void RootResetsThatChangeNothingRenderLikeTheUndeclaredPage(string css)
    {
        Assert.That(Pdf(Page_(css)), Is.EqualTo(Pdf(Page_(""))), css);
    }

    [TestCase("body{margin:0}")]
    [TestCase("body { margin: 0; }")]
    [TestCase("html,body{margin:0;padding:0}")]
    [TestCase("body{margin:0px 0pt 0mm 0%}")]
    [TestCase("body{margin-top:0;margin-right:0;margin-bottom:0;margin-left:0}")]
    [TestCase("body{margin-block:0;margin-inline:0}")]
    [TestCase("body{margin:0!important}")]
    [TestCase("BODY{MARGIN:0}")]
    [TestCase("html{margin:0rem;padding:0em}body{margin:0em}")]
    [TestCase("html{margin:initial;padding:unset}body{margin:initial}")]
    [TestCase("body{margin:-0pt}")]
    public void BodyMarginResetRemovesTheUserAgentMargin(string css)
    {
        // Every spelling of a zero body margin equals the one canonical reset, and the reset
        // moves the content (the 6pt margin is gone).
        var reset = Pdf(Page_("body{margin:0}"));
        Assert.That(Pdf(Page_(css)), Is.EqualTo(reset), css);
        Assert.That(reset, Is.Not.EqualTo(Pdf(Page_(""))));
    }

    [Test]
    public void InlineStyleOnBodyIsAccepted()
    {
        var html = $"<!doctype html><html style='margin:0;padding:0'><body style='margin:0'>{Content}</body></html>";
        Assert.That(Pdf(html), Is.EqualTo(Pdf(Page_("body{margin:0}"))));
    }

    [Test]
    public void ZeroRootMarginDoesNotDisturbBodyPadding()
    {
        Assert.That(Pdf(Page_("html{margin:0}body{margin:6pt;padding:10pt}")), Is.EqualTo(Pdf(Page_("body{padding:10pt}"))));
    }

    [TestCase("body{margin:0 auto}")]
    // 20pt exceeds the h2's own top margin it collapses with; a smaller value would be hidden by it.
    [TestCase("body{margin-top:20pt}")]
    [TestCase("body{margin-left:1rem}")]
    [TestCase("html{margin:0}body{margin:5%}")]
    public void BodyMarginIsLaidOutAsARealMargin(string css)
    {
        Assert.That(Pdf(Page_(css)), Is.Not.EqualTo(Pdf(Page_(""))), css);
    }

    [TestCase("html{margin:2pt}", "2pt")]
    [TestCase("html{padding:4pt}", "4pt")]
    public void NonZeroRootInsetStillFailsWithoutOutput(string css, string at)
    {
        var html = Page_(css);
        using var output = new MemoryStream();
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, Page));
        Assert.That(error!.Code, Is.EqualTo("FPDF1201"), css);
        Assert.That(output.Length, Is.Zero);
        Assert.That(error.SourceOffset, Is.EqualTo(html.IndexOf(at, StringComparison.Ordinal)), css);
    }

    [Test]
    public void TheSdkAcceptsTheReset()
    {
        var reset = FactsPDF.Pdf.Write().ToBytes(Page_("html,body{margin:0;padding:0}"));
        Assert.That(reset, Is.EqualTo(FactsPDF.Pdf.Write().ToBytes(Page_("body{margin:0}"))));
    }
}
