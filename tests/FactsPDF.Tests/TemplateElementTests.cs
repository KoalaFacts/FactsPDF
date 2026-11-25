using NUnit.Framework;

namespace FactsPDF.Tests;

// A template's contents are an inert document fragment that HTML never renders, so FactsPDF drops
// the element with everything inside it, including style elements, style attributes and elements
// it does not otherwise support (R5ch). Before, every template failed FPDF1102.
[TestFixture]
public sealed class TemplateElementTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 260, PageHeight = 300, Margin = 12 };

    private static byte[] Pdf(string html, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, options ?? Page);
        return output.ToArray();
    }

    [TestCase("<p>Invoice 42</p><template id=row><tr><td>SKU</td></tr></template><p>Total</p>",
        "<p>Invoice 42</p><p>Total</p>")]
    [TestCase("<html><head><title>T</title><template><p>Draft</p></template></head><body><p>A</p></body></html>",
        "<html><head><title>T</title></head><body><p>A</p></body></html>")]
    [TestCase("<table><colgroup><col style='width:236pt'></colgroup><template><col></template><tbody><template><tr><td>x</td></tr></template><tr><td>Row</td></tr></tbody></table>",
        "<table><colgroup><col style='width:236pt'></colgroup><tbody><tr><td>Row</td></tr></tbody></table>")]
    [TestCase("<ul><template><li>Hidden</li></template><li>Shown</li></ul>", "<ul><li>Shown</li></ul>")]
    [TestCase("<div><template><button>Buy</button><form><input></form><x-card>C</x-card></template>Text</div>",
        "<div>Text</div>")]
    [TestCase("<template><template><video></video></template><p>Outer</p></template><p>After</p>", "<p>After</p>")]
    [TestCase("<template/><p>Self</p></template><p>Body</p>", "<p>Body</p>")]
    public void RendersAsIfAbsent(string html, string without)
        => Assert.That(Pdf(html), Is.EqualTo(Pdf(without)));

    [Test]
    public void StylesInsideATemplateDoNotApply()
    {
        const string page = "<p class=a>Paid</p>";
        Assert.Multiple(() =>
        {
            Assert.That(Pdf("<template><style>.a{color:red;font-size:30pt}</style></template>" + page), Is.EqualTo(Pdf(page)));
            // Declarations the engine would reject outside a template are inert inside one.
            Assert.That(Pdf("<template><style>.a{position:absolute}</style><p style='float:left'>x</p></template>" + page),
                Is.EqualTo(Pdf(page)));
        });
    }

    [Test]
    public void TextAroundAnInlineTemplateRendersAsOneRun()
        => Assert.That(Pdf("<p>Order <template><b>draft</b></template>confirmed</p>"),
            Is.EqualTo(Pdf("<p>Order confirmed</p>")));

    [Test]
    public void TemplateContentsCountTowardTheElementLimit()
    {
        var limited = new PdfOptions { PageWidth = 260, PageHeight = 300, Margin = 12, MaxElements = 4 };
        Assert.That(Pdf("<p>A</p><template><i>1</i></template>", limited), Is.Not.Empty);
        using var output = new MemoryStream();
        var ex = Assert.Throws<FactsPdfException>(() =>
            PdfConverter.Convert("<p>A</p><template><i>1</i><i>2</i><i>3</i></template>", output, limited))!;
        Assert.That(ex.Code, Is.EqualTo("FPDF1002"));
    }

    [TestCase("<p>A</p><template><p>B</p>", "FPDF1101", 8)]
    [TestCase("<p>A</p></template>", "FPDF1101", 8)]
    [TestCase("<template><p>B</p></template></template>", "FPDF1101", 29)]
    [TestCase("<p>A</p><template shadowrootmode=open><p>B</p></template>", "FPDF1103", 8)]
    [TestCase("<head><title>Ti<template>x</template>tle</title></head>", "FPDF1101", 15)]
    [TestCase("<p>A</p><template><template ShadowRootMode='closed'></template></template>", "FPDF1103", 18)]
    public void UnbalancedOrShadowRootTemplatesFail(string html, string code, int offset)
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
