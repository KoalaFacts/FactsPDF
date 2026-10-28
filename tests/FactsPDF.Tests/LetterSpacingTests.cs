using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>Fixed positive tracking is applied to actual PDF glyph advances, not just parsed CSS.</summary>
[TestFixture]
public sealed class LetterSpacingTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 180, PageHeight = 240, Margin = 20 };

    private static List<LayoutPage> Layout(string html, PdfOptions? options = null)
    {
        var o = options ?? Page;
        return TextLayout.Layout(HtmlDocumentReader.ReadTree(html, o, default), o, default);
    }

    private static byte[] Pdf(string html, PdfOptions? options = null)
    {
        using var stream = new MemoryStream();
        PdfConverter.Convert(html, stream, options ?? Page);
        return stream.ToArray();
    }

    private static void Rejected(string html, string expected)
    {
        using var output = new MemoryStream();
        output.Write("existing-report"u8);
        output.Position = 4;
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, Page));
        Assert.That(error!.Code, Is.EqualTo(expected));
        Assert.That(output.ToArray(), Is.EqualTo("existing-report"u8.ToArray()));
        Assert.That(output.Position, Is.EqualTo(4));
    }

    [Test]
    public void NormalAndZeroPreserveLegacyPdfBytes()
    {
        const string text = "Annual <span>service</span> review";
        var legacy = Pdf("<p>" + text + "</p>");
        foreach (var declaration in new[] { "normal", "0", "0pt", "0px", "0mm" })
            Assert.That(Pdf("<p style='letter-spacing:" + declaration + "'>" + text + "</p>"),
                Is.EqualTo(legacy), declaration);
    }

    [Test]
    public void ActualCourierAdvanceAndPdfTextCommandMatch()
    {
        const string plain = "<p>ABCD</p>";
        const string tracked = "<p style='letter-spacing:2pt'>ABCD</p>";
        var normal = Layout(plain).Single().Runs.Single();
        var spaced = Layout(tracked).Single().Runs.Single();
        Assert.That(normal.Width, Is.EqualTo(28.8).Within(0.001));
        Assert.That(spaced.Width, Is.EqualTo(36.8).Within(0.001));
        Assert.That(spaced.X, Is.EqualTo(normal.X));
        var bytes = Encoding.Latin1.GetString(Pdf(tracked));
        Assert.That(bytes, Does.Contain("2 Tc"));
        Assert.That(bytes, Does.Contain(" Tj ET"));
        Assert.That(Pdf(tracked), Is.Not.EqualTo(Pdf(plain)));
        Assert.That(Pdf("<p style='letter-spacing:0.1in'>ABCD</p>"),
            Is.EqualTo(Pdf("<p style='letter-spacing:7.2pt'>ABCD</p>")));
    }

    [Test]
    public void InlineTrackingInheritsAndResetsWithoutLosingRunWidthOrText()
    {
        var html = "<p style='letter-spacing:1pt'>AB<span style='letter-spacing:3pt'>CD</span>EF</p>";
        var runs = Layout(html).Single().Runs;
        Assert.That(runs.Select(r => r.Text), Is.EqualTo(new[] { "AB", "CD", "EF" }));
        Assert.That(runs.Select(r => r.Width), Is.EqualTo(new[] { 16.4, 20.4, 16.4 }).Within(0.001));
        Assert.That(runs[1].X, Is.EqualTo(runs[0].X + runs[0].Width).Within(0.001));
        Assert.That(runs[2].X, Is.EqualTo(runs[1].X + runs[1].Width).Within(0.001));
        Assert.That(Encoding.Latin1.GetString(Pdf(html)), Does.Contain("3 Tc"));
    }

    [Test]
    public void TrackedSpanFollowedByNormalTextResetsPersistentPdfTextState()
    {
        // Tc is not reset by BT/ET. A missing explicit 0 Tc after AB would
        // paint CD too wide even though layout measured the normal advance.
        const string html = "<p><span style='letter-spacing:3pt'>AB</span>CD</p>";
        var runs = Layout(html).Single().Runs;
        Assert.That(runs.Select(run => run.Text), Is.EqualTo(new[] { "AB", "CD" }));
        Assert.That(runs.Select(run => run.Width), Is.EqualTo(new[] { 20.4, 14.4 }).Within(0.001));
        Assert.That(runs[1].X, Is.EqualTo(runs[0].X + runs[0].Width).Within(0.001));
        var text = Encoding.Latin1.GetString(Pdf(html));
        var tracked = text.IndexOf("3 Tc", StringComparison.Ordinal);
        var restored = text.IndexOf("0 Tc", StringComparison.Ordinal);
        Assert.That(tracked, Is.GreaterThanOrEqualTo(0));
        Assert.That(restored, Is.GreaterThan(tracked));
        Assert.That(Pdf("<p>CD</p>"), Is.EqualTo(Pdf("<p style='letter-spacing:normal'>CD</p>")));
    }

    [Test]
    public void TrueTypeTrackedSpanAlsoResetsTcBeforeUntrackedGlyphs()
    {
        var font = PdfFont.LoadTrueType(FontFixture.Create());
        var options = Page with { Fonts = [font] };
        var html = "<p><span style='letter-spacing:2pt'>中文</span>中文</p>";
        var runs = Layout(html, options).Single().Runs;
        Assert.That(runs.Select(r => r.Text), Is.EqualTo(new[] { "中文", "中文" }));
        Assert.That(runs[0].Width - runs[1].Width, Is.EqualTo(4).Within(0.001));
        var bytes = Encoding.Latin1.GetString(Pdf(html, options));
        var tracked = bytes.IndexOf("2 Tc", StringComparison.Ordinal);
        var restored = bytes.IndexOf("0 Tc", StringComparison.Ordinal);
        Assert.That(tracked, Is.GreaterThanOrEqualTo(0));
        Assert.That(restored, Is.GreaterThan(tracked));
    }

    [Test]
    public void TrackingChangesRealWrapAndRetainsAllTextAcrossPages()
    {
        var narrow = Page with { PageWidth = 120, PageHeight = 68, Margin = 20 };
        const string text = "AB CD EF GH";
        var normal = Layout("<p>" + text + "</p>", narrow);
        var tracked = Layout("<p style='letter-spacing:3pt'>" + text + "</p>", narrow);
        Assert.That(normal.SelectMany(p => p.Runs).ToArray(), Has.Length.EqualTo(1));
        Assert.That(tracked.SelectMany(p => p.Runs).Count(), Is.GreaterThan(1));
        Assert.That(tracked.SelectMany(p => p.Runs).Select(r => r.Text).Aggregate("", (a,b) => a+b)
            .Replace(" ", ""), Is.EqualTo(text.Replace(" ", "")));
        Assert.That(tracked.Count, Is.GreaterThanOrEqualTo(1));
        Assert.That(Pdf("<p style='letter-spacing:3pt'>" + text + "</p>", narrow), Is.Not.Empty);
    }

    [Test]
    public void CascadeImportantWideKeywordsAndInheritedInlineTrackingAreComputed()
    {
        const string html = "<style>p{letter-spacing:2pt!important}.other{letter-spacing:4pt}" +
            ".reset{letter-spacing:initial!important}.unset{letter-spacing:unset!important}</style>" +
            "<p class=other>AB</p><p class=reset>CD</p>" +
            "<div style='letter-spacing:1pt'><p class=unset>EF<span>GH</span></p></div>";
        var items = HtmlDocumentReader.Read(html, Page, default);
        Assert.That(items.Select(p => p.Style.LetterSpacing), Is.EqualTo(new[] { 2d, 0d, 1d }));
        Assert.That(items[2].Runs.Last().Style.LetterSpacing, Is.EqualTo(1));
        Assert.That(Pdf(html), Is.Not.Empty);
    }

    [Test]
    public void RealChineseTrueTypeUsesTheSameMeasuredAdvanceAndCidPdfCommand()
    {
        var font = PdfFont.LoadTrueType(FontFixture.Create());
        var options = Page with { Fonts = [font] };
        const string content = "中文中文";
        var normal = Layout("<p>" + content + "</p>", options).Single().Runs.Single();
        var tracked = Layout("<p style='letter-spacing:2pt'>" + content + "</p>", options).Single().Runs.Single();
        Assert.That(tracked.Width - normal.Width, Is.EqualTo(8).Within(0.001));
        var bytes = Encoding.Latin1.GetString(Pdf("<p style='letter-spacing:2pt'>" + content + "</p>", options));
        Assert.That(bytes, Does.Contain("2 Tc"));
        Assert.That(bytes, Does.Contain("/FontFile2"));
    }

    [Test]
    public void InheritedListTrackingReservesTheActuallyRenderedMarkerWidth()
    {
        var runs = Layout("<ol style='letter-spacing:3pt'><li>A</li></ol>")[0].Runs;
        var marker = runs.Single(run => run.Text == "1.");
        var item = runs.Single(run => run.Text == "A");
        Assert.That(marker.Width, Is.EqualTo(20.4).Within(0.001));
        Assert.That(marker.X, Is.EqualTo(20).Within(0.001));
        Assert.That(item.X - (marker.X + marker.Width), Is.EqualTo(6).Within(0.001),
            "An inherited tracked decimal marker must not intrude into item content.");
        Assert.That(Encoding.Latin1.GetString(Pdf("<ol style='letter-spacing:3pt'><li>A</li></ol>")),
            Does.Contain("3 Tc"));
    }

    [Test]
    public void NowrapCountsTrackingAndRejectsAnOtherwiseFittingReference()
    {
        var options = Page with { PageWidth = 140, Margin = 20 };
        const string reference = "INVOICE-2026";
        var noTracking = Pdf("<p style='white-space:nowrap'>" + reference + "</p>", options);
        Assert.That(noTracking, Is.Not.Empty);
        using var output = new MemoryStream();
        output.Write("safe"u8);
        output.Position = 1;
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(
            "<p style='white-space:nowrap;letter-spacing:3pt'>" + reference + "</p>", output, options));
        Assert.That(error!.Code, Is.EqualTo("FPDF1302"));
        Assert.That(output.ToArray(), Is.EqualTo("safe"u8.ToArray()));
    }

    [TestCase("letter-spacing:-1pt", "FPDF1202")]
    [TestCase("letter-spacing:24.01pt", "FPDF1202")]
    [TestCase("letter-spacing:1em", "FPDF1202")]
    [TestCase("letter-spacing:10%", "FPDF1202")]
    [TestCase("letter-spacing:calc(1pt + 1pt)", "FPDF1202")]
    [TestCase("letter-spacing:2pt 3pt", "FPDF1202")]
    [TestCase("word-spacing:2pt", "FPDF1201")]
    public void UnsupportedTrackingAndUnimplementedWordSpacingAreExplicit(string css, string code)
        => Rejected("<p style='" + css + "'>ABCD</p>", code);

    [Test]
    public void UnmatchedBadTrackingIsAlsoRejected()
        => Rejected("<style>.missing{letter-spacing:-1pt}</style><p>Actual report</p>", "FPDF1202");
}
