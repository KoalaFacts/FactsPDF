using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

[TestFixture]
public sealed class LastLineAlignmentReportTests
{
    private static List<LayoutPage> Layout(string html, PdfOptions? options = null)
    {
        var settings = options ?? new PdfOptions();
        return TextLayout.Layout(HtmlDocumentReader.ReadTree(html, settings, default), settings, default);
    }

    private static byte[] Pdf(string html, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, options);
        return output.ToArray();
    }

    private static string Register(bool control)
    {
        var html = new StringBuilder("<style>p{margin:0;font-size:10pt;line-height:2}</style><article>");
        for (var id = 1; id <= 44; id++)
        {
            html.Append("<p>R").Append(id.ToString("D3", System.Globalization.CultureInfo.InvariantCulture))
                .Append("</p>");
            if (control)
                html.Append("<p style='width:60pt'>Alpha Beta</p>")
                    .Append("<p style='width:60pt;text-align:center'>Gamma</p>");
            else
                html.Append("<p style='width:60pt;text-align-last:center'>Alpha Beta Gamma</p>");
        }
        return html.Append("</article>").ToString();
    }

    [Test]
    public void RealMultiPageLastLineAlignmentMatchesIndependentExplicitLines()
    {
        var source = Register(false);
        var control = Register(true);
        var pages = Layout(source);
        Assert.That(pages.Count, Is.GreaterThan(1));
        var placed = pages.SelectMany(p => p.Runs).ToArray();
        Assert.That(placed.Length, Is.EqualTo(44 * 3));
        Assert.That(placed.Take(3).Select(r => r.Text), Is.EqualTo(new[] { "R001", "Alpha Beta", "Gamma" }));
        Assert.That(placed[1].X, Is.EqualTo(36).Within(0.000001));
        Assert.That(placed[2].X, Is.EqualTo(51).Within(0.000001));
        Assert.That(placed[^3].Text, Is.EqualTo("R044"));
        Assert.That(Pdf(source), Is.EqualTo(Pdf(control)));
        Assert.That(Pdf(source), Is.Not.Empty);
    }

    [Test]
    public void AutoValuePreservesLegacyBytesAndForcedBreaksUseLastLineAlignment()
    {
        const string baseHtml = "<p style='width:90pt;font-size:10pt;margin:0'>Alpha Beta<br>Gamma</p>";
        var original = Pdf(baseHtml);
        var automatic = Pdf(baseHtml.Replace("margin:0'", "margin:0;text-align-last:auto'", StringComparison.Ordinal));
        Assert.That(automatic, Is.EqualTo(original));
        var changed = Layout(baseHtml.Replace("margin:0'", "margin:0;text-align-last:right'", StringComparison.Ordinal))
            .SelectMany(p => p.Runs).ToArray();
        Assert.That(changed, Has.Length.EqualTo(2));
        Assert.That(changed[0].X, Is.EqualTo(36 + 90 - 60).Within(0.000001));
        Assert.That(changed[1].X, Is.EqualTo(36 + 90 - 30).Within(0.000001));
    }

    [Test]
    public void CascadeWideKeywordsAndImportantChangeComputedGeometry()
    {
        const string html = "<style>section{text-align-last:right}p.note{text-align-last:center!important}" +
            "p.note{text-align-last:left}</style><section>" +
            "<p class='note' style='width:60pt;font-size:10pt;margin:0'>Alpha Beta Gamma</p>" +
            "<p style='width:60pt;font-size:10pt;margin:0;text-align-last:unset'>Alpha Beta Gamma</p>" +
            "<p style='width:60pt;font-size:10pt;margin:0;text-align-last:initial'>Alpha Beta Gamma</p>" +
            "</section>";
        var runs = Layout(html).SelectMany(p => p.Runs).ToArray();
        Assert.That(runs.Length, Is.EqualTo(6));
        Assert.That(runs[1].X, Is.EqualTo(51).Within(0.000001));
        Assert.That(runs[3].X, Is.EqualTo(66).Within(0.000001));
        Assert.That(runs[5].X, Is.EqualTo(36).Within(0.000001));
    }

    [Test]
    public void FlatTableCellFinalLineMovesWithoutChangingTrackOrRowGeometry()
    {
        const string table = "<table style='width:80pt;font-size:10pt;line-height:2'>" +
            "<colgroup><col style='width:100%'></colgroup><tbody><tr>" +
            "<td style='padding:0;text-align-last:center'>Alpha Beta Gamma</td>" +
            "</tr></tbody></table>";
        var page = Layout(table).Single();
        Assert.That(page.Runs.Select(r => r.Text), Is.EqualTo(new[] { "Alpha Beta", "Gamma" }));
        Assert.That(page.Runs[0].X, Is.EqualTo(36).Within(0.000001));
        Assert.That(page.Runs[1].X, Is.EqualTo(61).Within(0.000001)); 
        Assert.That(Pdf(table).Length, Is.GreaterThan(100));
    }

    [Test]
    public void ExplicitBreaksAndLastLinesCanJustifyActualWordSpaceAdvances()
    {
        const string original = "<p style='width:90pt;font-size:10pt;margin:0'>Alpha Beta<br>Gamma</p>";
        var justified = original.Replace("margin:0'", "margin:0;text-align-last:justify'", StringComparison.Ordinal);
        var baseline = Layout(original).Single().Runs;
        var aligned = Layout(justified).Single().Runs;
        Assert.That(baseline.Select(r => r.Text).ToArray(), Is.EqualTo(new[] { "Alpha Beta", "Gamma" }));
        Assert.That(aligned.SelectMany(r => r.Text).Aggregate("", (a, b) => a + b), Is.EqualTo("Alpha BetaGamma"));
        Assert.That(aligned[^1].Text, Is.EqualTo("Gamma"));
        // The explicitly broken first line contains one measurable U+0020
        // gap; the final line has no space and is never stretched.
        Assert.That(aligned.First().X, Is.EqualTo(36).Within(0.000001));
        var lastRunOnFirstLine = aligned.Take(aligned.Count - 1).Last();
        Assert.That(lastRunOnFirstLine.X + lastRunOnFirstLine.Width,
            Is.EqualTo(126).Within(0.000001));
        Assert.That(aligned[^1].X, Is.EqualTo(36).Within(0.000001));
        Assert.That(Pdf(justified), Is.Not.EqualTo(Pdf(original)));
    }

    [Test]
    public void ExplicitTrueTypeRetainsActualFontAndMeasuredLastLineGeometry()
    {
        var font = PdfFont.FromBytes(FontFixture.Create());
        var options = new PdfOptions { Fonts = [font], PageWidth = 300, Margin = 30 };
        const string html = "<p style='width:110pt;font-size:10pt;margin:0;text-align-last:center'>AA<br>B</p>";
        var runs = Layout(html, options).Single().Runs;
        Assert.That(runs, Has.Count.EqualTo(2));
        Assert.That(runs.All(r => ReferenceEquals(r.Font, font)), Is.True);
        Assert.That(runs[1].X, Is.GreaterThan(runs[0].X));
        Assert.That(Pdf(html, options).Length, Is.GreaterThan(100));
    }

    [TestCase("<p style='text-align-last:stretch'>Text</p>", "FPDF1202")]
    [TestCase("<p style='text-align-last:20%'>Text</p>", "FPDF1202")]
    [TestCase("<style>.unused{text-align-last:stretch}</style><p>Text</p>", "FPDF1202")]
    [TestCase("<p><span style='text-align-last:center'>Text</span></p>", "FPDF1201")]
    public void UnsupportedInputDoesNotReplaceExistingPdf(string html, string code)
    {
        using var output = new MemoryStream();
        output.Write("protected-report"u8);
        output.Position = 4;
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output));
        Assert.That(error!.Code, Is.EqualTo(code));
        Assert.That(output.ToArray(), Is.EqualTo("protected-report"u8.ToArray()));
        Assert.That(output.Position, Is.EqualTo(4));
        Assert.That(Pdf("<p>Recovery</p>").Length, Is.GreaterThan(100));
    }
}
