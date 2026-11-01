using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>Actual PDF dash strokes for report boxes (not parser-only CSS support).</summary>
[TestFixture]
public sealed class ReportPatternedBorderTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 240, Margin = 20 };

    private static byte[] Pdf(string html, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, options ?? Page);
        return output.ToArray();
    }

    private static LayoutPage[] Layout(string html)
        => TextLayout.Layout(HtmlDocumentReader.ReadTree(html, Page, default), Page, default).ToArray();

    private static void FailsAndRetains(string html, string code, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        output.Write("ORIGINAL"u8);
        var error = Assert.Throws<FactsPdfException>(() =>
            PdfConverter.Convert(html, output, options ?? Page));
        Assert.That(error!.Code, Is.EqualTo(code));
        Assert.That(output.ToArray(), Is.EqualTo("ORIGINAL"u8.ToArray()));
        Assert.That(Pdf("<p>Recovery</p>").Length, Is.GreaterThan(100));
    }

    [TestCase("dashed", "1 0 0 RG", "[6 6] 0 d")]
    [TestCase("dotted", "1 0 0 RG", "1 J [0.001 4] 0 d")]
    public void ShorthandPaintsRealPdfStrokesWithIndependentColour(string kind, string colour, string pattern)
    {
        var html = "<section style='width:120pt;border:2pt " + kind +
            " red;background:#e5f1f6;padding:5pt'>INVOICE 22</section>";
        var box = Layout(html)[0].PaintBoxes.Single();
        Assert.That(box.CommandCount, Is.EqualTo(5), "One background and four stroke edges.");
        Assert.That(box.Width, Is.EqualTo(134).Within(0.00001));
        var pdf = Encoding.ASCII.GetString(Pdf(html));
        Assert.That(pdf, Does.Contain(colour));
        Assert.That(pdf, Does.Contain(pattern));
        Assert.That(pdf.Split(" l S Q\n").Length - 1, Is.EqualTo(4));
        Assert.That(pdf, Does.Contain("INVOICE").Or.Contain("Tm <"));
    }

    [Test]
    public void DifferentSidesAndCascadeKeepComputedGeometryAndLegacySolidOutput()
    {
        var stylesheet = "<style>section.report{border-style:dashed dotted solid none;" +
            "border-width:1pt 2pt 3pt 4pt;border-color:blue!important}</style>";
        var html = stylesheet + "<section class=report style='width:120pt;" +
            "border-left:5pt dashed red;border-top-style:dotted'>TEXT</section>";
        var box = ((BlockNode)HtmlDocumentReader.ReadTree(html, Page, default).Children.Single()).BoxStyle;
        Assert.That(box.BorderTop.Pattern, Is.EqualTo(BorderPattern.Dotted));
        Assert.That(box.BorderRight.Pattern, Is.EqualTo(BorderPattern.Dotted));
        Assert.That(box.BorderBottom.Pattern, Is.EqualTo(BorderPattern.Solid));
        Assert.That(box.BorderLeft.Pattern, Is.EqualTo(BorderPattern.Dashed));
        Assert.That(box.BorderLeft.EffectiveWidth, Is.EqualTo(5));
        Assert.That(box.BorderTop.Color, Is.EqualTo(new Rgb(0, 0, 1)));
        Assert.That(Pdf("<div style='border:2pt solid blue'>TEXT</div>"),
            Is.EqualTo(Pdf("<div style='border-width:2pt;border-style:solid;border-color:blue'>TEXT</div>")),
            "Original solid-filled rectangle bytes must stay exactly identical.");
        var dashed = Layout("<div style='width:100pt;border:2pt dashed blue'>TEXT</div>")[0];
        var solid = Layout("<div style='width:100pt;border:2pt solid blue'>TEXT</div>")[0];
        Assert.That(dashed.Runs.Single().X, Is.EqualTo(solid.Runs.Single().X).Within(0.00001));
        Assert.That(dashed.PaintBoxes.Single().Width, Is.EqualTo(solid.PaintBoxes.Single().Width));
    }

    [Test]
    public void CssWideInheritanceAndImportanceApplyToEachBorderSide()
    {
        const string html = "<style>section{border:2pt dashed blue}" +
            "article{border-style:inherit;border-top-style:dotted!important}</style>" +
            "<section><article style='border-style:solid'>NOTICE</article></section>";
        var root = (BlockNode)HtmlDocumentReader.ReadTree(html, Page, default).Children.Single();
        var child = (BlockNode)root.Children.Single();
        Assert.That(child.BoxStyle.BorderTop.Pattern, Is.EqualTo(BorderPattern.Dotted));
        Assert.That(child.BoxStyle.BorderRight.Pattern, Is.EqualTo(BorderPattern.Solid));
        Assert.That(child.BoxStyle.BorderRight.EffectiveWidth, Is.Zero,
            "Inheriting a style does not inherit its border width.");
    }

    [Test]
    public void PaginationKeepsTopAndBottomStrokesOnTheirOwnFragments()
    {
        var paragraphs = string.Concat(Enumerable.Range(1, 48).Select(i => "<p>Facility item " +
            i.ToString("D2") + ": inspected</p>"));
        var html = "<section style='border:2pt dashed #23446b;background:#f0f6fa;padding:5pt'>" +
            paragraphs + "</section>";
        var pages = Layout(html);
        Assert.That(pages.Length, Is.GreaterThan(2));
        var first = pages[0].PaintBoxes.Single();
        var last = pages[^1].PaintBoxes.Single();
        Assert.That(first.IsFirstFragment, Is.True);
        Assert.That(first.IsLastFragment, Is.False);
        Assert.That(last.IsFirstFragment, Is.False);
        Assert.That(last.IsLastFragment, Is.True);
        Assert.That(first.TopBorderHeight, Is.EqualTo(2));
        Assert.That(first.BottomBorderHeight, Is.Zero);
        Assert.That(last.TopBorderHeight, Is.Zero);
        Assert.That(last.BottomBorderHeight, Is.EqualTo(2));
        var pdf = Encoding.ASCII.GetString(Pdf(html));
        Assert.That(pdf.Split(" l S Q\n").Length - 1, Is.GreaterThan(pages.Length * 2));
        Assert.That(pdf, Does.Contain("Tm <"));
    }

    [Test]
    public void ZeroAndNoneDoNotPaintOrConsumeCommands()
    {
        var html = "<div style='border:2pt none red'>EMPTY</div>";
        Assert.That(Pdf(html), Is.EqualTo(Pdf("<div>EMPTY</div>")));
        var zero = Layout("<div style='border:0 dashed blue'>EMPTY</div>");
        Assert.That(zero[0].PaintBoxes, Is.Empty);
    }

    [Test]
    public void UnsupportedStylesResourcesAndCommandLimitsRetainOriginalOutput()
    {
        FailsAndRetains("<div style='border:2pt double blue'>X</div>", "FPDF1202");
        FailsAndRetains("<div style='border-style:groove'>X</div>", "FPDF1202");
        FailsAndRetains("<div style='border:2pt dashed dotted'>X</div>", "FPDF1202");
        FailsAndRetains("<span style='border:2pt dashed blue'>X</span>", "FPDF1201");
        FailsAndRetains("<div style='width:260pt;border:3pt dotted blue'>X</div>", "FPDF1302");
        FailsAndRetains("<div style='border:2pt dashed red'>X</div>", "FPDF1401",
            Page with { MaxDisplayCommands = 2 });
    }
}
