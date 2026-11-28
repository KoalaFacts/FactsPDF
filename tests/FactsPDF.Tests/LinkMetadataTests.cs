using NUnit.Framework;

namespace FactsPDF.Tests;

// Links that only name a related resource or a connection hint (icon, canonical, alternate,
// preconnect, manifest, preload) are never fetched or painted by a static render, so they are
// accepted in head and change nothing (R5cl). Pages exported from CMSs and web apps carry a
// favicon and a canonical link, and either failed the whole page with FPDF1102. Stylesheet links
// still fail (FPDF1204, like @import): external resources are never loaded, and ignoring one
// would change the render.
[TestFixture]
public sealed class LinkMetadataTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 260, PageHeight = 300, Margin = 12 };

    private static byte[] Pdf(string html)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, Page);
        return output.ToArray();
    }

    private const string Body = "<body><h1 style='font-weight:normal'>Invoice 42</h1><p>Total due: $120</p></body></html>";

    [TestCase("<link rel=icon href=/favicon.ico>")]
    [TestCase("<link rel=\"shortcut icon\" href=\"/favicon.ico\" type=\"image/x-icon\">")]
    [TestCase("<link rel=apple-touch-icon sizes=180x180 href=/apple.png><link rel=mask-icon href=/m.svg color=#c00>")]
    [TestCase("<link rel=canonical href=\"https://example.com/invoices/42\"><link rel=manifest href=/site.webmanifest>")]
    [TestCase("<link rel=alternate type=application/rss+xml title=Feed href=/feed.xml><link rel=alternate hreflang=de href=/de/>")]
    [TestCase("<link rel=preconnect href=https://fonts.example crossorigin><link rel=dns-prefetch href=//cdn.example>")]
    [TestCase("<link rel=preload href=/app.css as=style><link rel=modulepreload href=/app.js integrity=sha384-x referrerpolicy=no-referrer>")]
    [TestCase("<LINK REL=ICON HREF=/f.ico /><link rel=' next  prev ' href=/2 id=l class=x data-x=1 lang=en dir=ltr>")]
    public void MetadataLinksRenderAsIfAbsent(string link)
    {
        var with = Pdf("<!doctype html><html><head><meta charset=utf-8>" + link + "<title>T</title></head>" + Body);
        var without = Pdf("<!doctype html><html><head><meta charset=utf-8><title>T</title></head>" + Body);
        Assert.That(with, Is.EqualTo(without));
    }

    [TestCase("<head><link rel=stylesheet href=/site.css></head><p>A</p>", "FPDF1204")]
    [TestCase("<head><link rel='alternate stylesheet' href=/print.css></head><p>A</p>", "FPDF1204")]
    [TestCase("<head><link rel='icon STYLESHEET' href=/a></head><p>A</p>", "FPDF1204")]
    [TestCase("<body><link rel=stylesheet href=/a.css><p>A</p></body>", "FPDF1204")]
    [TestCase("<ul><link rel=stylesheet href=/a.css><li>A</li></ul>", "FPDF1204")]
    [TestCase("<table><tbody><link rel=stylesheet href=/a.css><tr><td>A</td></tr></tbody></table>", "FPDF1204")]
    [TestCase("<table><tbody><link rel=icon href=/a><tr><td>A</td></tr></tbody></table>", "FPDF1101")]
    [TestCase("<head><link rel='unknown stylesheet' href=/a></head><p>A</p>", "FPDF1204")]
    [TestCase("<head><link rel=import href=/a></head><p>A</p>", "FPDF1103")]
    [TestCase("<head><link href=/a></head><p>A</p>", "FPDF1103")]
    [TestCase("<head><link rel='  ' href=/a></head><p>A</p>", "FPDF1103")]
    [TestCase("<head><link rel=icon href=/a media=print></head><p>A</p>", "FPDF1103")]
    [TestCase("<body><link rel=icon href=/a><p>A</p></body>", "FPDF1101")]
    [TestCase("<p>A<link rel=icon href=/a></p>", "FPDF1101")]
    [TestCase("<head><link rel=icon href=/a></link></head><p>A</p>", "FPDF1101")]
    public void UnsupportedLinksStillFail(string html, string code)
    {
        using var output = new MemoryStream();
        var ex = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, Page))!;
        Assert.Multiple(() =>
        {
            Assert.That(ex.Code, Is.EqualTo(code));
            Assert.That(output.Length, Is.Zero);
        });
    }

    [Test]
    public void StylesheetFailureReportsTheLinkOffset()
    {
        const string html = "<head><title>T</title><link rel=stylesheet href=/a.css></head><p>A</p>";
        var ex = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, new MemoryStream(), Page))!;
        Assert.Multiple(() =>
        {
            Assert.That(ex.Code, Is.EqualTo("FPDF1204"));
            Assert.That(ex.SourceOffset, Is.EqualTo(html.IndexOf("<link", StringComparison.Ordinal)));
        });
    }

    [Test]
    public void LinksCountTowardTheElementLimit()
    {
        var html = "<head>" + string.Concat(Enumerable.Repeat("<link rel=icon href=/a>", 8)) + "</head><p>A</p>";
        var ex = Assert.Throws<FactsPdfException>(() =>
            PdfConverter.Convert(html, new MemoryStream(), Page with { MaxElements = 5 }))!;
        Assert.That(ex.Code, Is.EqualTo("FPDF1002"));
    }
}
