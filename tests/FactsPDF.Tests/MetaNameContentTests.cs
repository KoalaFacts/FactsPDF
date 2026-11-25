using NUnit.Framework;

namespace FactsPDF.Tests;

// Named and Open Graph meta elements (viewport, description, author, generator, og:*) are
// metadata a static render never reads, so they are accepted in head and change nothing (R5ck).
// Every page exported from a web app or CMS carries a viewport meta, and it failed FPDF1103.
// color-scheme still fails: it changes the canvas and text colours a browser paints.
[TestFixture]
public sealed class MetaNameContentTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 260, PageHeight = 300, Margin = 12 };

    private static byte[] Pdf(string html)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, Page);
        return output.ToArray();
    }

    private const string Body = "<body><h1>Invoice 42</h1><p>Total due: $120</p></body></html>";

    [TestCase("<meta name=viewport content=\"width=device-width, initial-scale=1\">")]
    [TestCase("<meta charset=utf-8><meta name=\"description\" content=\"Invoice for March\"><meta name=author content='Acme'>")]
    [TestCase("<meta NAME=Generator CONTENT=\"Acme CMS 4.2\"><meta name=robots content=noindex>")]
    [TestCase("<meta property=\"og:title\" content=\"Invoice 42\"><meta property=og:type content=article>")]
    [TestCase("<meta name=theme-color content=#c00><meta name=keywords content=\"invoice, march\">")]
    [TestCase("<meta name=\"viewport\" content=\"width=device-width\" />")]
    [TestCase("<meta name=format-detection content=\"telephone=no\" lang=en id=m class=x data-x=1 dir=ltr>")]
    public void NamedMetadataRendersAsIfAbsent(string meta)
    {
        var with = Pdf("<!doctype html><html><head>" + meta + "<title>T</title></head>" + Body);
        var without = Pdf("<!doctype html><html><head><title>T</title></head>" + Body);
        Assert.That(with, Is.EqualTo(without));
    }

    [Test]
    public void MetadataDoesNotChangeTheDocumentTitle()
    {
        var with = Pdf("<html><head><meta name=title content=Other><meta property=og:title content=Other><title>Real</title></head>" + Body);
        var without = Pdf("<html><head><title>Real</title></head>" + Body);
        Assert.That(with, Is.EqualTo(without));
    }

    [TestCase("<head><meta name=color-scheme content=dark></head><p>A</p>", "FPDF1103")]
    [TestCase("<head><meta name=COLOR-SCHEME content=\"light dark\"></head><p>A</p>", "FPDF1103")]
    [TestCase("<head><meta name=supported-color-schemes content=\"light dark\"></head><p>A</p>", "FPDF1103")]
    [TestCase("<head><meta http-equiv=refresh content=5></head><p>A</p>", "FPDF1103")]
    [TestCase("<head><meta itemprop=name content=A></head><p>A</p>", "FPDF1103")]
    [TestCase("<head><meta name=viewport content=x style='color:red'></head><p>A</p>", "FPDF1103")]
    [TestCase("<head><meta charset=latin1 name=viewport content=x></head><p>A</p>", "FPDF1103")]
    [TestCase("<p>A<meta name=viewport content=x></p>", "FPDF1101")]
    [TestCase("<body><meta name=viewport content=x><p>A</p></body>", "FPDF1101")]
    public void UnsupportedMetaStillFails(string html, string code)
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
    public void FailureReportsTheMetaOffset()
    {
        const string html = "<head><title>T</title><meta name=color-scheme content=dark></head><p>A</p>";
        var ex = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, new MemoryStream(), Page))!;
        Assert.That(ex.SourceOffset, Is.EqualTo(html.IndexOf("<meta", StringComparison.Ordinal)));
    }
}
