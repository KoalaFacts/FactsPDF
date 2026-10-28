using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>Only whole paragraph/heading nowrap is supported; never truncate a report identifier.</summary>
[TestFixture]
public sealed class WhiteSpaceNowrapTests
{
    private static readonly PdfOptions Page = new()
    {
        PageWidth = 180, PageHeight = 230, Margin = 20
    };

    private static List<PlacedText> Placed(string html, PdfOptions? options = null)
    {
        var o = options ?? Page;
        return TextLayout.Layout(HtmlDocumentReader.ReadTree(html, o, default), o, default)
            .SelectMany(page => page.Runs).ToList();
    }

    private static byte[] Pdf(string html, PdfOptions? options = null)
    {
        using var stream = new MemoryStream();
        PdfConverter.Convert(html, stream, options ?? Page);
        return stream.ToArray();
    }

    private static FactsPdfException Rejected(string html, string code, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        output.Write("protected-pdf"u8);
        output.Position = 3;
        var error = Assert.Throws<FactsPdfException>(
            () => PdfConverter.Convert(html, output, options ?? Page));
        Assert.That(error!.Code, Is.EqualTo(code));
        Assert.That(output.ToArray(), Is.EqualTo("protected-pdf"u8.ToArray()));
        Assert.That(output.Position, Is.EqualTo(3));
        return error;
    }

    [Test]
    public void ExplicitParagraphAndHeadingAcceptNowrapWithoutChangingFontOrBoxStyle()
    {
        var paragraphs = HtmlDocumentReader.Read(
            "<style>p,h1{white-space:nowrap}</style><h1>Invoice</h1>" +
            "<p>Reference: INV-2026-1042</p>", Page, default);
        Assert.That(paragraphs, Has.Count.EqualTo(2));
        Assert.That(paragraphs.All(p => p.Style.NoWrap), Is.True);
        Assert.That(paragraphs[1].Runs.All(r => r.Style.NoWrap), Is.True);
    }

    [Test]
    public void FittingNowrapCollapsesSpacesAndPaintsByteIdenticallyToNormal()
    {
        const string body = "PAID   IN FULL<br>READY";
        var nowrap = "<p style='white-space:nowrap'>" + body + "</p>";
        var normal = "<p style='white-space:normal'>" + body + "</p>";
        Assert.That(Placed(nowrap).Select(run => run.Text),
            Is.EqualTo(new[] { "PAID IN FULL", "READY" }));
        Assert.That(Pdf(nowrap), Is.EqualTo(Pdf(normal)));
        Assert.That(Pdf(normal), Is.EqualTo(Pdf("<p>" + body + "</p>")));
    }

    [Test]
    public void NormalWrapsAcrossSpacesWhileNowrapRejectsTheSameOverwideSegment()
    {
        var narrow = Page with { PageWidth = 100 };
        var normal = Placed("<p style='white-space:normal'>PAID IN FULL</p>", narrow);
        Assert.That(normal.Select(run => run.Text),
            Is.EqualTo(new[] { "PAID IN", "FULL" }));
        Rejected("<p style='white-space:nowrap'>PAID IN FULL</p>", "FPDF1302", narrow);
        Rejected("<p style='width:60pt;white-space:nowrap'>PAID IN FULL</p>",
            "FPDF1302", Page);
    }

    [Test]
    public void ExplicitBreaksStillPaginateWithoutTruncatingContent()
    {
        var shortPage = Page with { PageHeight = 80 };
        var html = "<p style='white-space:nowrap'>ALPHA<br>BETA<br>GAMMA</p>";
        var layout = TextLayout.Layout(HtmlDocumentReader.ReadTree(html, shortPage, default), shortPage, default);
        Assert.That(layout, Has.Count.GreaterThan(1));
        Assert.That(layout.SelectMany(page => page.Runs).Select(run => run.Text),
            Is.EqualTo(new[] { "ALPHA", "BETA", "GAMMA" }));
        Assert.That(Pdf(html, shortPage), Is.Not.Empty);
    }

    [Test]
    public void StylesheetCascadeImportantAndCssWideKeywordsRemainExplicit()
    {
        var paragraphs = HtmlDocumentReader.Read(
            "<style>p{white-space:nowrap!important}.normal{white-space:normal}" +
            ".reset{white-space:initial!important}.unset{white-space:unset!important}" +
            "</style><p class=normal>First</p><p class=reset>Second</p>" +
            "<p class=unset>Third</p><h1 style='white-space:inherit'>Fourth</h1>",
            Page, default);
        Assert.That(paragraphs.Select(p => p.Style.NoWrap),
            Is.EqualTo(new[] { true, false, false, false }));
    }

    [TestCase("<span style='white-space:nowrap'>A</span>", "FPDF1201")]
    [TestCase("<div style='white-space:nowrap'><p>A</p></div>", "FPDF1201")]
    [TestCase("<style>span{white-space:nowrap}</style><p><span>A</span></p>", "FPDF1201")]
    [TestCase("<p style='white-space:pre'>A</p>", "FPDF1202")]
    [TestCase("<p style='white-space:pre-wrap'>A</p>", "FPDF1202")]
    [TestCase("<p style='white-space:pre-line'>A</p>", "FPDF1202")]
    [TestCase("<p style='white-space:break-spaces'>A</p>", "FPDF1202")]
    [TestCase("<style>#absent{white-space:pre}</style><p>A</p>", "FPDF1202")]
    [TestCase("<p style='white-space:nowrap normal'>A</p>", "FPDF1202")]
    [TestCase("<p style='white-space:calc(1)'>A</p>", "FPDF1202")]
    public void UnsupportedElementsAndValuesNeverSilentlyFallBack(string html, string code)
        => Rejected(html, code);

    [Test]
    public void LaterUnsupportedFontStillPrecedesEarlierNoWrapWidthError()
    {
        // The whole paragraph must resolve before an earlier width failure:
        // this preserves the existing diagnostic priority and resource safety.
        Rejected("<p style='white-space:nowrap'>PAID IN FULL 中文</p>",
            "FPDF1301", Page with { PageWidth = 100 });
    }

    [Test]
    public void AMatchingInlineNormalDeclarationOverridesOrdinaryStylesheetNowrap()
    {
        Assert.That(Pdf("<style>p{white-space:nowrap}</style>" +
                        "<p style='white-space:normal'>PAID IN FULL</p>",
                        Page with { PageWidth = 100 }),
            Is.EqualTo(Pdf("<p>PAID IN FULL</p>", Page with { PageWidth = 100 })));
    }
}
