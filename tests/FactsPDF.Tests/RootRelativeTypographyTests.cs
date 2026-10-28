using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>Root-relative type sizes must change real PDF glyph and line geometry.</summary>
[TestFixture]
public sealed class RootRelativeTypographyTests
{
    private static readonly PdfOptions Options = new() { PageWidth = 300, PageHeight = 230, Margin = 20 };

    private static List<Paragraph> Read(string html, PdfOptions? options = null)
        => HtmlDocumentReader.Read(html, options ?? Options, CancellationToken.None);

    private static byte[] Pdf(string html, PdfOptions? options = null)
    {
        using var stream = new MemoryStream();
        PdfConverter.Convert(html, stream, options ?? Options);
        return stream.ToArray();
    }

    private static void Reject(string html)
    {
        using var stream = new MemoryStream();
        stream.Write("existing-pdf"u8);
        stream.Position = 3;
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, stream, Options));
        Assert.That(error!.Code, Is.EqualTo("FPDF1202"));
        Assert.That(stream.ToArray(), Is.EqualTo("existing-pdf"u8.ToArray()));
        Assert.That(stream.Position, Is.EqualTo(3));
    }

    [Test]
    public void HtmlComputedRootControlsDescendantsRatherThanBodyOrParent()
    {
        var paragraphs = Read("<style>html{font-size:15pt}body{font-size:11pt}" +
            "h1{font-size:1.6rem;line-height:2rem}p{font-size:1rem;line-height:1.5rem}" +
            "p span{font-size:1.2rem}</style>" +
            "<html><body><h1>AB</h1><p>AB <span>WA</span></p></body></html>");
        Assert.That(paragraphs[0].Style.FontSize, Is.EqualTo(24).Within(0.000001));
        Assert.That(paragraphs[0].Style.AbsoluteLineHeight, Is.EqualTo(30).Within(0.000001));
        Assert.That(paragraphs[1].Style.FontSize, Is.EqualTo(15).Within(0.000001));
        Assert.That(paragraphs[1].Style.AbsoluteLineHeight, Is.EqualTo(22.5).Within(0.000001));
        Assert.That(paragraphs[1].Runs.Single(r => r.Text == "WA").Style.FontSize,
            Is.EqualTo(18).Within(0.000001));
        Assert.That(paragraphs[1].Runs.Single(r => r.Text == "WA").Style.AbsoluteLineHeight,
            Is.EqualTo(22.5).Within(0.000001));
    }

    [Test]
    public void RootSelfReferenceUsesInitialSizeAndMissingHtmlDoesNotPromoteBody()
    {
        var root = Read("<html style='font-size:2rem'><body>" +
            "<p style='font-size:1rem;line-height:1rem'>AB</p></body></html>");
        Assert.That(root[0].Style.FontSize, Is.EqualTo(24));
        Assert.That(root[0].Style.AbsoluteLineHeight, Is.EqualTo(24));
        var missing = Read("<body style='font-size:20pt'><p style='font-size:1rem'>AB</p></body>");
        Assert.That(missing[0].Style.FontSize, Is.EqualTo(12));
        var configured = Read("<p style='font-size:1rem'>AB</p>", new PdfOptions { FontSize = 16 });
        Assert.That(configured[0].Style.FontSize, Is.EqualTo(16));
    }

    [Test]
    public void ImportantAndWideKeywordUseWinningRootRelativeSize()
    {
        var paragraphs = Read("<style>html{font-size:20pt}p{font-size:0.75rem!important}" +
            "p span{font-size:inherit}</style><html><body>" +
            "<p style='font-size:120%'>AB <span>WA</span></p></body></html>");
        Assert.That(paragraphs[0].Style.FontSize, Is.EqualTo(15));
        Assert.That(paragraphs[0].Runs.Single(r => r.Text == "WA").Style.FontSize, Is.EqualTo(15));
    }

    [Test]
    public void RemAndExplicitPointsMakeIdenticalMultiPageFontlessAndTrueTypePdfs()
    {
        var body = string.Concat(Enumerable.Repeat("<p>AB WA <span>AB</span><br>BA WA</p>", 24));
        var relative = "<html><style>html{font-size:15pt}body{font-size:11pt}" +
            "p{font-size:1rem;line-height:1.5rem;margin-bottom:0}" +
            "p span{font-size:1.2rem}</style><body>";
        var projected = "<html><style>html{font-size:15pt}body{font-size:11pt}" +
            "p{font-size:15pt;line-height:22.5pt;margin-bottom:0}" +
            "p span{font-size:18pt}</style><body>";
        var paragraphs = Read(relative + body + "</body></html>");
        Assert.That(TextLayout.Layout(paragraphs, Options, CancellationToken.None).Count,
            Is.GreaterThan(1), "Real text must cross a page boundary.");
        foreach (var options in new[]
        {
            Options,
            new PdfOptions { PageWidth = 300, PageHeight = 230, Margin = 20,
                Fonts = [PdfFont.LoadTrueType(FontFixture.Create())] }
        })
        {
            var actual = Pdf(relative + body + "</body></html>", options);
            Assert.That(actual, Is.EqualTo(Pdf(projected + body + "</body></html>", options)));
        }
        Assert.That(Pdf(relative + body + "</body></html>"), Is.Not.EqualTo(Pdf("<p>AB WA</p>")));
    }

    [TestCase("font-size:0rem")]
    [TestCase("font-size:-1rem")]
    [TestCase("font-size:1000rem")]
    [TestCase("font-size:0.001rem")]
    [TestCase("line-height:0rem")]
    [TestCase("line-height:-1rem")]
    [TestCase("line-height:2000rem")]
    [TestCase("line-height:0.001rem")]
    [TestCase("font-size:2vh")]
    [TestCase("width:1rem")]
    [TestCase("padding:2rem")]
    public void UnsupportedOrUnboundedInputPreservesExistingDestination(string css)
        => Reject("<html style='font-size:15pt'><body><p style='" + css + "'>AB</p></body></html>");

    [Test]
    public void UnmatchedInvalidRootRelativeRulesStillFailClosed()
    {
        Reject("<style>#missing{font-size:-2rem}</style><p>AB</p>");
        Reject("<style>#missing{line-height:2000rem}</style><p>AB</p>");
    }

    [Test]
    public void DefaultFontAndLineBoxesPreserveLegacyPdfBytes()
        => Assert.That(Pdf("<p style='font-size:12pt'>AB WA</p>"), Is.EqualTo(Pdf("<p>AB WA</p>")));
}
