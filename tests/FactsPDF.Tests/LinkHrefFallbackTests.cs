using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// An a href that is not an absolute HTTPS URL or one mailto address (free text, a relative path, a
/// fragment, http:, javascript:, file:). Strict mode, the default, fails FPDF1103. Non-strict mode draws
/// the text exactly like an ordinary link (blue, underlined), writes no link annotation and reports
/// the same FPDF1103 as a warning, so a template whose data puts free text into href still prints.
/// </summary>
[TestFixture]
public sealed class LinkHrefFallbackTests
{
    private static readonly PdfWriter Writer = Pdf.Write().WithTaggedPdf(false).WithPage(p => p.Size(300, 400).Margin(20));

    private static (byte[] Pdf, PdfWarnings? Warnings) NonStrict(string html)
    {
        PdfWarnings? warnings = null;
        var pdf = Writer.WithStrict(false).WithWarnings(report => warnings = report).ToBytes(html);
        return (pdf, warnings);
    }

    [TestCase("Some text")]
    [TestCase("/orders/7")]
    [TestCase("orders/7?id=1")]
    [TestCase("../orders")]
    [TestCase("#section")]
    [TestCase("")]
    [TestCase("http://example.com/insecure")]
    [TestCase("javascript:alert(1)")]
    [TestCase("file:///secret")]
    [TestCase("data:text/html,x")]
    [TestCase("https://user:pass@example.com/")]
    [TestCase("https://example.com/page#fragment")]
    [TestCase("mailto:sales@example.com?subject=hi")]
    public void StrictModeFailsAndNonStrictModeDrawsTheTextWithoutAnAnnotation(string href)
    {
        var html = $"<p>See <a href=\"{href}\">the text</a> now</p>";
        var error = Assert.Throws<FactsPdfException>(() => Writer.ToBytes(html))!;
        Assert.That(error.Code, Is.EqualTo("FPDF1103"));
        Assert.That(Assert.Throws<FactsPdfException>(() => Writer.WithStrict(true).ToBytes(html))!.Code, Is.EqualTo("FPDF1103"));

        var (pdf, warnings) = NonStrict(html);
        var text = Encoding.Latin1.GetString(pdf);
        // Never an annotation, and the unsafe target is never written anywhere.
        Assert.That(text, Does.Not.Contain("/Annots"));
        Assert.That(text, Does.Not.Contain("/Subtype /Link"));
        Assert.That(text, Does.Not.Contain("/URI"));
        // The text looks exactly like an ordinary link: the bytes equal those of blue underlined text.
        var plain = Writer.ToBytes("<p>See <span style=\"color:#0000ee;text-decoration:underline\">the text</span> now</p>");
        Assert.That(pdf, Is.EqualTo(plain));
        Assert.That(warnings!.Items, Has.Count.EqualTo(1));
        var warning = warnings.Items[0];
        Assert.That(warning.Code, Is.EqualTo("FPDF1103"));
        Assert.That(warning.SourceOffset, Is.EqualTo(html.IndexOf("<a ", StringComparison.Ordinal)));
        Assert.That(warning.Message, Is.EqualTo(error.Message[("FPDF1103: ".Length)..]));
    }

    // Over-correction: only the unusable target is dropped; a valid link next to it keeps its annotation.
    [Test]
    public void ValidLinksKeepTheirAnnotationNextToAnInvalidOne()
    {
        var (pdf, warnings) = NonStrict(
            "<p><a href=\"Some text\">one</a> <a href=\"https://example.com/two\">two</a> <a href=\"mailto:a@example.com\">three</a></p>");
        var text = Encoding.Latin1.GetString(pdf);
        Assert.That(Regex.Count(text, "/Subtype /Link"), Is.EqualTo(2));
        Assert.That(text, Does.Contain(Convert.ToHexString(Encoding.ASCII.GetBytes("https://example.com/two"))));
        Assert.That(warnings!.Items, Has.Count.EqualTo(1));
    }

    [Test]
    public void ValidLinksReportNoWarningAndStayIdenticalInBothModes()
    {
        const string html = "<p><a href=\"https://example.com/two\">two</a></p>";
        var (pdf, warnings) = NonStrict(html);
        Assert.That(pdf, Is.EqualTo(Writer.ToBytes(html)));
        Assert.That(warnings!.Items, Is.Empty);
    }

    // A link without any href is still no link in either mode: there is no target to drop.
    [Test]
    public void AnAnchorWithoutHrefStillFailsInNonStrictMode()
        => Assert.That(Assert.Throws<FactsPdfException>(() => Writer.WithStrict(false).ToBytes("<p><a>text</a></p>"))!.Code, Is.EqualTo("FPDF1103"));

    [Test]
    public void ANestedLinkStillFailsWhateverItsHref()
        => Assert.That(Assert.Throws<FactsPdfException>(() => Writer.WithStrict(false)
            .ToBytes("<p><a href=\"x\"><a href=\"https://example.com\">t</a></a></p>"))!.Code, Is.EqualTo("FPDF1101"));

    [Test]
    public void AnInvalidHrefInATableCellAndInAListIsDrawnToo()
    {
        var (pdf, warnings) = NonStrict(
            "<table style=\"width:200pt\"><tr><td><a href=\"Some text\">cell</a></td></tr></table><ul><li><a href=\"/x\">item</a></li></ul>");
        Assert.That(Encoding.Latin1.GetString(pdf), Does.Not.Contain("/Annots"));
        Assert.That(warnings!.Items, Has.Count.EqualTo(2));
    }
}
