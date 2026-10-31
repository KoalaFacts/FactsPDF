using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

[TestFixture]
public sealed class ListMarkerStyleTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 140, Margin = 20 };

    private static List<LayoutPage> Layout(string html, PdfOptions? options = null)
    {
        var actual = options ?? Page;
        return TextLayout.Layout(HtmlDocumentReader.ReadTree(html, actual, default), actual, default);
    }

    private static byte[] Pdf(string html, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, options ?? Page);
        return output.ToArray();
    }

    private static void Fails(string html, string code, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        output.Write("sentinel"u8);
        output.Position = 2;
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, options ?? Page));
        Assert.That(error!.Code, Is.EqualTo(code));
        Assert.That(error.SourceOffset, Is.GreaterThanOrEqualTo(0));
        Assert.That(output.ToArray(), Is.EqualTo("sentinel"u8.ToArray()));
        Assert.That(output.Position, Is.EqualTo(2));
    }

    [TestCase("lower-alpha", "a.", "z.", "aa.")]
    [TestCase("upper-alpha", "A.", "Z.", "AA.")]
    public void AlphabeticMarkersAreRealMeasuredPdfGlyphs(string kind, string first, string twentySixth, string twentySeventh)
    {
        var html = "<ol style='list-style-type:" + kind + "'>" +
            string.Concat(Enumerable.Range(1, 27).Select(i => "<li>Entry" + i + "</li>")) + "</ol>";
        var page = Layout(html, Page with { PageHeight = 3000 })[0];
        var runs = page.Runs;
        var markers = runs.Where(r => r.Text.EndsWith(".", StringComparison.Ordinal)).ToArray();
        Assert.That(markers, Has.Length.EqualTo(27));
        Assert.That(markers[0].Text, Is.EqualTo(first));
        Assert.That(markers[25].Text, Is.EqualTo(twentySixth));
        Assert.That(markers[26].Text, Is.EqualTo(twentySeventh));
        Assert.That(runs.Single(r => r.Text == "Entry27").X, Is.EqualTo(47.6).Within(0.000001));
        Assert.That(markers[26].X, Is.EqualTo(20).Within(0.000001));
        Assert.That(Pdf(html, Page with { PageHeight = 3000 }).AsSpan(0, 4).SequenceEqual("%PDF"u8), Is.True);
    }

    [TestCase("lower-roman", "i.", "iv.", "ix.", "xiv.")]
    [TestCase("upper-roman", "I.", "IV.", "IX.", "XIV.")]
    public void RomanMarkersUseSubtractiveNumberingAndKeepOneMarkerPerItem(
        string kind, string one, string four, string nine, string fourteen)
    {
        var html = "<ol style='list-style-type:" + kind + "'>" +
            string.Concat(Enumerable.Range(1, 14).Select(i => "<li>Row" + i + "</li>")) + "</ol>";
        var pages = Layout(html, Page with { PageHeight = 95 });
        var markers = pages.SelectMany(p => p.Runs).Where(r => r.Text.EndsWith(".", StringComparison.Ordinal))
            .Select(r => r.Text).ToArray();
        Assert.That(pages.Count, Is.GreaterThan(2));
        Assert.That(markers, Has.Length.EqualTo(14));
        Assert.That(markers[0], Is.EqualTo(one));
        Assert.That(markers[3], Is.EqualTo(four));
        Assert.That(markers[8], Is.EqualTo(nine));
        Assert.That(markers[13], Is.EqualTo(fourteen));
        Assert.That(Pdf(html, Page with { PageHeight = 95 }).Length, Is.GreaterThan(300));
    }

    [Test]
    public void MarkerTextTransformMeasuresThePaintedProportionalGlyphs()
    {
        // A and a have deliberately different advances (600 vs 200).
        // Previously a. was measured before casing, and then A. could not
        // fit into the marker's reserved width: FPDF1302 on a valid report.
        var fonts = Page with
        {
            Fonts = [PdfFont.LoadTrueType(FontFixture.Create(listCaseMarkers: true))]
        };
        const string transform = "<ol style='list-style-type:lower-alpha;text-transform:uppercase'><li>A</li></ol>";
        const string literal = "<ol style='list-style-type:upper-alpha'><li>A</li></ol>";
        var rendered = Layout(transform, fonts);
        var expected = Layout(literal, fonts);
        Assert.That(rendered.Single().Runs.Select(run => (run.Text, run.X, run.Width)),
            Is.EqualTo(expected.Single().Runs.Select(run => (run.Text, run.X, run.Width))));
        Assert.That(rendered.Single().Runs.Any(run => run.Text == "A."), Is.True);
        Assert.That(Pdf(transform, fonts), Is.EqualTo(Pdf(literal, fonts)));
    }

    [Test]
    public void MarkerLowercaseTransformUsesFinalGlyphAdvance()
    {
        var fonts = Page with
        {
            Fonts = [PdfFont.LoadTrueType(FontFixture.Create(listCaseMarkers: true))]
        };
        const string transform = "<ol style='list-style-type:upper-roman;text-transform:lowercase'><li>i</li></ol>";
        const string literal = "<ol style='list-style-type:lower-roman'><li>i</li></ol>";
        Assert.That(Layout(transform, fonts).Single().Runs.Select(run => (run.Text, run.X, run.Width)),
            Is.EqualTo(Layout(literal, fonts).Single().Runs.Select(run => (run.Text, run.X, run.Width))));
        Assert.That(Pdf(transform, fonts), Is.EqualTo(Pdf(literal, fonts)));
    }

    [Test]
    public void DefaultDecimalAndDiscRemainByteIdenticalToExplicitStyle()
    {
        Assert.That(Pdf("<ol style='list-style-type:decimal'><li>A</li><li>B</li></ol>"),
            Is.EqualTo(Pdf("<ol><li>A</li><li>B</li></ol>")));
        Assert.That(Pdf("<ul style='list-style-type:disc'><li>A</li><li>B</li></ul>"),
            Is.EqualTo(Pdf("<ul><li>A</li><li>B</li></ul>")));
    }

    [Test]
    public void CascadeImportantAndInlineStyleProduceSameRealPdf()
    {
        var source = "<style>ol {list-style-type:upper-alpha!important}</style>" +
            "<ol style='list-style-type:lower-alpha'><li>First</li><li>Second</li></ol>";
        var expected = "<ol style='list-style-type:upper-alpha'><li>First</li><li>Second</li></ol>";
        Assert.That(Pdf(source), Is.EqualTo(Pdf(expected)));
        Assert.That(Layout(source)[0].Runs.Where(r => r.Text.EndsWith(".", StringComparison.Ordinal))
            .Select(r => r.Text), Is.EqualTo(new[] { "A.", "B." }));
    }

    [Test]
    public void ItemsCanOverrideInheritedKindAndNestedListsMayInherit()
    {
        var source = "<ol style='list-style-type:lower-alpha'>" +
            "<li>A<ul style='list-style-type:inherit'><li>B</li></ul></li>" +
            "<li style='list-style-type:initial'>C</li>" +
            "<li style='list-style-type:unset'>D</li></ol>";
        var pages = Layout(source, Page with { PageHeight = 3000 });
        var runs = pages.SelectMany(p => p.Runs).Select(r => r.Text).ToArray();
        Assert.That(runs.Where(x => x.EndsWith(".", StringComparison.Ordinal)),
            Is.EqualTo(new[] { "a.", "a.", "c." }));
        Assert.That(pages.Sum(p => p.Discs.Count), Is.EqualTo(1));
        Assert.That(Pdf(source).Length, Is.GreaterThan(300));
    }

    [Test]
    public void NoneSuppressesOnlyMarkersWhilePreservingReportItemIndent()
    {
        const string source = "<ol style='list-style-type:none'><li>A</li><li>B</li></ol>";
        var pages = Layout(source);
        Assert.That(pages.SelectMany(p => p.Runs).Select(r => r.Text), Is.EqualTo(new[] { "A", "B" }));
        Assert.That(pages.SelectMany(p => p.Runs).Select(r => r.X),
            Is.All.EqualTo(44).Within(0.000001));
        Assert.That(pages.Sum(p => p.Discs.Count), Is.Zero);
        Assert.DoesNotThrow(() => Pdf(source, Page with { MaxDisplayCommands = 2 }));
        Fails("<ol><li>A</li><li>B</li></ol>", "FPDF1401", Page with { MaxDisplayCommands = 2 });
    }

    [Test]
    public void ListStyleOnOneItemDoesNotRenumberOrModifyItsSiblings()
    {
        var source = "<ol><li>A</li><li style='list-style-type:none'>B</li>" +
            "<li style='list-style-type:upper-roman'>C</li><li>D</li></ol>";
        var pages = Layout(source);
        Assert.That(pages.SelectMany(p => p.Runs).Where(r => r.Text.EndsWith(".", StringComparison.Ordinal))
            .Select(r => r.Text), Is.EqualTo(new[] { "1.", "III.", "4." }));
        Assert.That(Pdf(source).Length, Is.GreaterThan(300));
    }

    [Test]
    public void RomanMaximumIsExplicitAndDoesNotSilentlyWrap()
    {
        Assert.That(ListMarkerTypes.MarkerText(ListMarkerKind.UpperRoman, 3999, 1), Is.EqualTo("MMMCMXCIX."));
        Assert.That(ListMarkerTypes.MarkerText(ListMarkerKind.LowerAlpha, 27, 1), Is.EqualTo("aa."));
        var error = Assert.Throws<FactsPdfException>(() =>
            ListMarkerTypes.MarkerText(ListMarkerKind.UpperRoman, 4000, 20));
        Assert.That(error!.Code, Is.EqualTo("FPDF1202"));
        Assert.That(error.SourceOffset, Is.EqualTo(20));
    }

    [Test]
    public void InvalidValuesContextsAndUnmatchedRulesFailWithoutClobberingOutput()
    {
        Fails("<ol style='list-style-type:square'><li>A</li></ol>", "FPDF1202");
        Fails("<ul style='list-style-type:lower-alpha upper-alpha'><li>A</li></ul>", "FPDF1202");
        Fails("<li style='list-style-type:100'>A</li>", "FPDF1202");
        Fails("<p style='list-style-type:decimal'>A</p>", "FPDF1201");
        Fails("<span style='list-style-type:disc'>A</span>", "FPDF1201");
        Fails("<ol style='list-style:none'><li>A</li></ol>", "FPDF1201");
        Fails("<style>.unused {list-style-type:square}</style><ol><li>A</li></ol>", "FPDF1202");
    }

    [Test]
    public void StyleInheritsWithoutChangingUnstyledNestedListDefaults()
    {
        var source = "<ol style='list-style-type:lower-alpha'><li>A<ul><li>B</li></ul></li></ol>";
        var actual = Layout(source);
        Assert.That(actual.SelectMany(p => p.Runs).Where(r => r.Text.EndsWith(".", StringComparison.Ordinal))
            .Select(r => r.Text), Is.EqualTo(new[] { "a." }));
        Assert.That(actual.Sum(p => p.Discs.Count), Is.EqualTo(1));
        Assert.That(Pdf(source).Length, Is.GreaterThan(300));
    }
}
