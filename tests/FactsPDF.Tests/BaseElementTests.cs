using NUnit.Framework;

namespace FactsPDF.Tests;

// Server-side frameworks put <base href="/"> in head. base only sets the URL that relative URLs
// resolve against and the default link target; FactsPDF resolves no relative URL (links must be
// absolute https or mailto, images are named resources), so a base in head changes nothing (R5cq).
// Before, any base failed the whole page with FPDF1102.
[TestFixture]
public sealed class BaseElementTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 260, PageHeight = 300, Margin = 12 };

    private static byte[] Pdf(string html)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, Page);
        return output.ToArray();
    }

    private const string Body = "<body><h1 style=\"font-weight:normal\">Statement 42</h1><p>Total due: $120. <a href=\"https://example.com/pay\">Pay online</a></p></body></html>";

    private static string Document(string head) =>
        "<!doctype html><html><head><meta charset=utf-8>" + head + "<title>T</title><style>p{margin:0 0 4pt}</style></head>" + Body;

    [TestCase("<base href=\"/\" />")]
    [TestCase("<base href=\"https://example.com/app/\">")]
    [TestCase("<base href=\"\">")]
    [TestCase("<base href=\"/über/ x?y#z\">")]
    [TestCase("<base target=\"_blank\">")]
    [TestCase("<base href=\"/portal/\" target=_self>")]
    [TestCase("<BASE HREF=\"./\" TARGET=reports id=b class=x data-x=1 lang=en dir=ltr title=t>")]
    public void BaseInHeadRendersAsIfAbsent(string element)
        => Assert.That(Pdf(Document(element)), Is.EqualTo(Pdf(Document(""))));

    // Before any body content the HTML parser puts base into the implied head, as Chrome does.
    [Test]
    public void BaseBeforeBodyContentGoesIntoTheImpliedHead()
        => Assert.That(Pdf("<base href=/><p>A</p>"), Is.EqualTo(Pdf("<p>A</p>")));

    [TestCase("<body><base href=/><p>A</p></body>", "FPDF1101")]
    [TestCase("<p>A<base href=/></p>", "FPDF1101")]
    [TestCase("<head><title>T<base href=/></title></head><p>A</p>", "FPDF1101")]
    [TestCase("<head><base href=/></base></head><p>A</p>", "FPDF1101")]
    [TestCase("<head><base href=/><base target=_blank></head><p>A</p>", "FPDF1101")]
    [TestCase("<head><base href=/></head><body><base href=/><p>A</p></body>", "FPDF1101")]
    [TestCase("<head><base></head><p>A</p>", "FPDF1103")]
    [TestCase("<head><base id=b class=x></head><p>A</p>", "FPDF1103")]
    [TestCase("<head><base href=\"/\" media=print></head><p>A</p>", "FPDF1103")]
    [TestCase("<head><base href=\"/\" style=\"color:red\"></head><p>A</p>", "FPDF1103")]
    [TestCase("<head><base href=\"/\" hidden></head><p>A</p>", "FPDF1103")]
    [TestCase("<head><base href=\"/\" dir=rtl></head><p>A</p>", "FPDF1103")]
    [TestCase("<head><base target=\"\"></head><p>A</p>", "FPDF1103")]
    [TestCase("<head><base target=_new></head><p>A</p>", "FPDF1103")]
    public void UnsupportedBaseStillFails(string html, string code)
    {
        using var output = new MemoryStream();
        var ex = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, Page))!;
        Assert.Multiple(() =>
        {
            Assert.That(ex.Code, Is.EqualTo(code));
            Assert.That(output.Length, Is.Zero);
        });
    }

    // A relative href resolves against an absolute http(s) base href, as Chrome resolves it (R5x-resources-links-media).
    [TestCase("/pay", "https://example.com/pay")]
    [TestCase("pay", "https://example.com/app/pay")]
    [TestCase("../pay?id=7", "https://example.com/pay?id=7")]
    [TestCase("#total", "https://example.com/app/#total")]
    public void RelativeLinksResolveAgainstAnAbsoluteBase(string href, string expected)
    {
        var body = "<p><a href=\"" + href + "\">Pay</a></p>";
        var text = System.Text.Encoding.Latin1.GetString(
            Pdf("<head><base href=\"https://example.com/app/\"><title>T</title></head>" + body));
        Assert.That(text, Does.Contain("/URI <" + Convert.ToHexString(System.Text.Encoding.ASCII.GetBytes(expected)) + ">"));
    }

    // Over-correction: without a base, with a relative base (which resolves against the unknown document
    // URL), and for scheme-relative references, relative links keep failing exactly as before.
    [TestCase("<head><title>T</title></head>", "/pay")]
    [TestCase("<head><base href=\"/\"><title>T</title></head>", "/pay")]
    [TestCase("<head><base href=\"./app/\"><title>T</title></head>", "pay")]
    [TestCase("<head><base href=\"https://example.com/app/\"><title>T</title></head>", "//example.com/pay")]
    [TestCase("<head><base href=\"https://example.com/app/\"><title>T</title></head>", "%70ay")]
    [TestCase("<head><title>T</title></head>", "#total")]
    public void RelativeLinksWithoutAnAbsoluteBaseStillFail(string head, string href)
    {
        var ex = Assert.Throws<FactsPdfException>(() => Pdf(head + "<p><a href=\"" + href + "\">Pay</a></p>"))!;
        Assert.That(ex.Code, Is.EqualTo("FPDF1103"));
    }

    [Test]
    public void SecondBaseReportsItsOwnOffset()
    {
        const string html = "<head><base href=/><title>T</title><base href=/x/></head><p>A</p>";
        var ex = Assert.Throws<FactsPdfException>(() => Pdf(html))!;
        Assert.Multiple(() =>
        {
            Assert.That(ex.Code, Is.EqualTo("FPDF1101"));
            Assert.That(ex.SourceOffset, Is.EqualTo(html.LastIndexOf("<base", StringComparison.Ordinal)));
        });
    }

    [Test]
    public void BaseCountsTowardTheElementLimit()
    {
        var limit = Page with { MaxElements = 4 };
        PdfConverter.Convert("<head><meta charset=utf-8><title>T</title></head><p>A</p>", new MemoryStream(), limit);
        var ex = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(
            "<head><base href=/><meta charset=utf-8><title>T</title></head><p>A</p>", new MemoryStream(), limit))!;
        Assert.That(ex.Code, Is.EqualTo("FPDF1002"));
    }
}
