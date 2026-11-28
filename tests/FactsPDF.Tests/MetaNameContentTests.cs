using NUnit.Framework;

namespace FactsPDF.Tests;

// Named and Open Graph meta elements (viewport, description, author, generator, og:*) are
// metadata a static render never reads, so they are accepted in head and change nothing (R5ck).
// Every page exported from a web app or CMS carries a viewport meta, and it failed FPDF1103.
// A dark colour-scheme still fails: it changes the canvas and text colours a browser paints.
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

    private const string Body = "<body><h1 style=\"font-weight:normal\">Invoice 42</h1><p>Total due: $120</p></body></html>";

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

    // Unsupported semantics: another character set, a navigation, a policy that can block styles.
    [TestCase("<head><meta charset=latin1 name=viewport content=x></head><p>A</p>")]
    [TestCase("<head><meta http-equiv=Content-Type content='text/html; charset=iso-8859-1'></head><p>A</p>")]
    [TestCase("<head><meta http-equiv=refresh content=5></head><p>A</p>")]
    [TestCase("<head><meta http-equiv=Content-Security-Policy content=\"default-src 'none'\"></head><p>A</p>")]
    public void UnsupportedMetaStillFails(string html)
    {
        using var output = new MemoryStream();
        var ex = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, Page))!;
        Assert.Multiple(() =>
        {
            Assert.That(ex.Code, Is.EqualTo("FPDF1103"));
            Assert.That(output.Length, Is.Zero);
        });
    }

    // In print Chrome prefers the light scheme, so every colour-scheme that allows light, is
    // normal, is invalid or is not the name color-scheme prints exactly as the page without it.
    [TestCase("<meta name=color-scheme content=light>")]
    [TestCase("<meta name=color-scheme content='light dark'>")]
    [TestCase("<meta name=color-scheme content='dark light'>")]
    [TestCase("<meta name=color-scheme content='only light'>")]
    [TestCase("<meta name=color-scheme content='light dark only'>")]
    [TestCase("<meta name=color-scheme content=normal>")]
    [TestCase("<meta name=color-scheme content='normal dark'>")]
    [TestCase("<meta name=color-scheme content='dark only light'>")]
    [TestCase("<meta name=color-scheme content='dark;'>")]
    [TestCase("<meta name=color-scheme content='light,dark'>")]
    [TestCase("<meta name=color-scheme content=''>")]
    [TestCase("<meta name=color-scheme content=foo>")]
    [TestCase("<meta name=' color-scheme' content=dark>")]
    [TestCase("<meta name=supported-color-schemes content='light dark'>")]
    [TestCase("<meta name=supported-color-schemes content=dark>")]
    [TestCase("<meta itemprop=name content=A>")]
    [TestCase("<meta http-equiv=X-UA-Compatible content='IE=edge'>")]
    [TestCase("<meta http-equiv=content-type content='text/html; charset=utf-8'>")]
    [TestCase("<meta http-equiv=Content-Security-Policy-Report-Only content=\"default-src 'none'\">")]
    [TestCase("<meta charset=UTF8>")]
    public void LightOrIrrelevantMetaRendersAsIfAbsent(string meta)
    {
        var with = Pdf("<!doctype html><html><head>" + meta + "<title>T</title></head>" + Body);
        var without = Pdf("<!doctype html><html><head><title>T</title></head>" + Body);
        Assert.That(with, Is.EqualTo(without));
    }

    // A browser accepts meta anywhere in the document and renders none of it.
    [TestCase("<p>A<meta name=viewport content=x></p>", "<p>A</p>")]
    [TestCase("<meta itemprop=name content=x><p>A</p>", "<p>A</p>")]
    [TestCase("<p>A</p><meta name=description content=y>", "<p>A</p>")]
    [TestCase("<div><meta name=author content=z><p>A</p></div>", "<div><p>A</p></div>")]
    public void MetaInBodyRendersAsIfAbsent(string body, string without)
    {
        Assert.That(Pdf("<!doctype html><html><head><title>T</title></head><body>" + body + "</body></html>"),
            Is.EqualTo(Pdf("<!doctype html><html><head><title>T</title></head><body>" + without + "</body></html>")));
    }

    [Test]
    public void MetaInBodyIsStillJudged()
    {
        var ex = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(
            "<p>A</p><meta http-equiv=refresh content=5>", new MemoryStream(), Page))!;
        Assert.That(ex.Code, Is.EqualTo("FPDF1103"));
    }

    [Test]
    public void FailureReportsTheMetaOffset()
    {
        const string html = "<head><title>T</title><meta http-equiv=refresh content=5></head><p>A</p>";
        var ex = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, new MemoryStream(), Page))!;
        Assert.That(ex.SourceOffset, Is.EqualTo(html.IndexOf("<meta", StringComparison.Ordinal)));
    }
}
