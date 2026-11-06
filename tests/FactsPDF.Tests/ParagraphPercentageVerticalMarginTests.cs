using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>Paragraph outside vertical percentages use their containing content width in real PDF layout.</summary>
[TestFixture]
public sealed class ParagraphPercentageVerticalMarginTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 500, Margin = 20 };

    private static byte[] Render(string html, PdfOptions? options = null)
    {
        using var stream = new MemoryStream();
        PdfConverter.Convert(html, stream, options ?? Page);
        return stream.ToArray();
    }

    private static List<LayoutPage> Layout(string html, PdfOptions? options = null)
    {
        var o = options ?? Page;
        return TextLayout.Layout(HtmlDocumentReader.ReadTree(html, o, default), o, default);
    }

    private static void RejectPreservingOutput(string html, string code, PdfOptions? options = null)
    {
        using var stream = new MemoryStream();
        stream.Write("previous-pdf"u8);
        stream.Position = 3;
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, stream, options ?? Page));
        Assert.That(error!.Code, Is.EqualTo(code));
        Assert.That(stream.ToArray(), Is.EqualTo("previous-pdf"u8.ToArray()));
        Assert.That(stream.Position, Is.EqualTo(3));
    }

    [TestCase("p")]
    [TestCase("h2")]
    [TestCase("pre")]
    public void VerticalPercentagesMatchResolvedPointMarginsAndActualPaint(string tag)
    {
        var actual = $"<{tag} style='margin:10% 0 5%;padding:4pt;background:#ddeeff'>Report text</{tag}>" +
            "<p style='margin:0'>Following text</p>";
        var oracle = actual.Replace("margin:10% 0 5%", "margin:26pt 0 13pt");
        Assert.That(Render(actual), Is.EqualTo(Render(oracle)));
        var painted = Layout(actual).Single().PaintBoxes.Single(b => b.Style.BackgroundColor.HasValue);
        Assert.That(painted.Top, Is.EqualTo(46).Within(0.000001));
    }

    [Test]
    public void NestedContainingWidthAndCascadeBothAffectPhysicalPdf()
    {
        const string percent = "<style>section p.audit{margin-block:5% 2.5%}" +
            "p.audit{margin-top:10%!important}</style>" +
            "<section style='width:180pt;padding:4pt'>" +
            "<p class='audit' style='margin-top:1%'>NESTED</p></section><p>AFTER</p>";
        const string points = "<section style='width:180pt;padding:4pt'>" +
            "<p style='margin-top:18pt;margin-bottom:4.5pt'>NESTED</p></section><p>AFTER</p>";
        Assert.That(Render(percent), Is.EqualTo(Render(points)));
    }

    [Test]
    public void ExplicitInheritanceFromPercentagePanelUsesChildContainingWidth()
    {
        const string percent = "<section style='width:180pt;margin-top:10%'>" +
            "<p style='margin-top:inherit;margin-bottom:unset'>INHERIT</p></section>";
        const string points = "<section style='width:180pt;margin-top:26pt'>" +
            "<p style='margin-top:18pt;margin-bottom:0'>INHERIT</p></section>";
        Assert.That(Render(percent), Is.EqualTo(Render(points)));
    }

    [Test]
    public void AcrossPagesVerticalPercentagesRetainGlyphsAndMatchPointOracle()
    {
        var page = Page with { PageHeight = 140 };
        var text = string.Join("<br>", Enumerable.Range(1, 26).Select(i => $"Entry{i:00}"));
        var actual = "<p style='margin-top:10%;margin-bottom:5%;padding:2pt'>" + text +
            "</p><p style='margin:0'>END</p>";
        var points = actual.Replace("margin-top:10%;margin-bottom:5%",
            "margin-top:26pt;margin-bottom:13pt");
        Assert.That(Render(actual, page), Is.EqualTo(Render(points, page)));
        var pages = Layout(actual, page);
        Assert.That(pages.Count, Is.GreaterThan(1));
        var runs = pages.SelectMany(p => p.Runs).Select(p => p.Text).ToArray();
        Assert.That(runs.Count(t => t.StartsWith("Entry", StringComparison.Ordinal)), Is.EqualTo(26));
        Assert.That(runs, Does.Contain("END"));
    }

    [Test]
    public void EmptyUnpaintedParagraphRetainsItsPercentageTopSpacingInActualPdf()
    {
        const string percent = "<p style='margin-top:10%;margin-bottom:5%'></p>" +
            "<p style='margin:0'>AFTER SPACER</p>";
        const string points = "<p style='margin-top:26pt;margin-bottom:13pt'></p>" +
            "<p style='margin:0'>AFTER SPACER</p>";
        const string noSpacer = "<p style='margin:0'>AFTER SPACER</p>";

        var plainBaseline = Layout(noSpacer).Single().Runs.Single().Baseline;
        var spacedBaseline = Layout(percent).Single().Runs.Single().Baseline;
        Assert.That(plainBaseline - spacedBaseline, Is.EqualTo(26).Within(0.000001),
            "A blank percentage spacer must not discard the larger top margin.");
        Assert.That(Render(percent), Is.EqualTo(Render(points)),
            "Percentage margin geometry must match the independently specified point oracle.");
        Assert.That(Render(percent), Is.Not.EqualTo(Render(noSpacer)));
    }

    [Test]
    public void UnsupportedAndOverPagePercentagesKeepOriginalPdfBytes()
    {
        RejectPreservingOutput("<ul><li><p style='margin-top:10%'>NO</p></li></ul>", "FPDF1201");
        RejectPreservingOutput("<table><colgroup><col style='width:100%'></colgroup>" +
            "<tbody><tr><td><p style='margin-top:10%'>NO</p></td></tr></tbody></table>", "FPDF1201");
        RejectPreservingOutput("<p style='margin-top:-10%'>NO</p>", "FPDF1202");
        RejectPreservingOutput("<p style='margin-top:100%'>NO</p>", "FPDF1302",
            Page with { PageHeight = 140 });
    }
}
