using NUnit.Framework;

namespace FactsPDF.Tests;

// wbr is a soft wrap opportunity with no glyph: it reads as U+200B, which the line breaker may wrap
// after and which is never drawn. Before, wbr failed FPDF1102. The built-in font advances 7.2pt
// per character at the default 12pt, so the 236pt line below holds 32 characters.
[TestFixture]
public sealed class WordBreakElementTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 260, PageHeight = 300, Margin = 12 };
    private static readonly string Long = new('A', 30);

    private static List<LayoutPage> Layout(string html) => TableLayoutTests.Layout(html, Page);

    private static FactsPdfException Fails(string html)
    {
        using var output = new MemoryStream();
        return Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, Page))!;
    }

    [Test]
    public void AWordTooLongForTheLineWrapsAtTheOpportunity()
    {
        var runs = Layout($"<p>{Long}<wbr>{Long}</p>").Single().Runs;
        Assert.That(runs.Select(r => r.Text), Is.EqualTo(new[] { Long, Long }));
        Assert.That(runs[0].Baseline - runs[1].Baseline, Is.EqualTo(14.4).Within(0.000001));
        Assert.That(runs[1].X, Is.EqualTo(runs[0].X));
        Assert.That(Fails($"<p>{Long}{Long}</p>").Code, Is.EqualTo("FPDF1302"));
    }

    [Test]
    public void TheWordStaysOnOneLineWhenItFits()
    {
        var runs = Layout("<p>AAA<wbr>BBB</p>").Single().Runs;
        Assert.That(runs.Select(r => r.Text), Is.EqualTo(new[] { "AAA", "BBB" }));
        Assert.That(runs[0].Baseline, Is.EqualTo(runs[1].Baseline));
        Assert.That(runs[1].X, Is.EqualTo(runs[0].X + 3 * 7.2).Within(0.000001));
    }

    [Test]
    public void ABreakOpportunityDrawsNoGlyphAndAddsNoAdvance()
    {
        var page = Layout("<p>AAA<wbr>BBB<wbr><wbr>CCC</p>").Single();
        Assert.That(page.Runs.Select(r => r.Text), Is.EqualTo(new[] { "AAA", "BBB", "CCC" }));
        Assert.That(page.Runs[2].X, Is.EqualTo(page.Runs[0].X + 6 * 7.2).Within(0.000001));
        Assert.That(string.Concat(page.Runs.Select(r => r.Text)).Contains('\u200B'), Is.False);
    }

    [Test]
    public void ALiteralZeroWidthSpaceIsTheSameOpportunity()
        => Assert.That(TableLayoutTests.Pdf($"<p>{Long}\u200B{Long}</p>", Page), Is.EqualTo(TableLayoutTests.Pdf($"<p>{Long}<wbr>{Long}</p>", Page)));

    [Test]
    public void OpportunitiesInDifferentlyStyledRunsWrapAtTheBoundary()
    {
        var runs = Layout($"<p>{Long}<u>B<wbr>{Long}</u></p>").Single().Runs;
        Assert.That(runs.Select(r => r.Text), Is.EqualTo(new[] { Long, "B", Long }));
        Assert.That(runs[1].Baseline, Is.EqualTo(runs[0].Baseline));
        Assert.That(runs[2].Baseline, Is.LessThan(runs[1].Baseline));
    }

    [Test]
    public void ABreakOpportunityWorksInsideInlineElementsListsAndHeadings()
    {
        var twenty = new string('A', 20);
        var twelve = new string('A', 12);
        Assert.That(Layout($"<ul><li>{twenty}<wbr>{twenty}</li></ul>").Single().Runs.Count, Is.EqualTo(2));
        Assert.That(Layout($"<h2>x<span>{twelve}<wbr>{twelve}</span></h2>").Single().Runs.Count, Is.EqualTo(3));
    }

    [Test]
    public void WbrWhereTextCannotWrapFails()
    {
        Assert.That(Fails("<p style='white-space:nowrap'>A<wbr>B</p>").Code, Is.EqualTo("FPDF1201"));
        Assert.That(Fails("<pre>A<wbr>B</pre>").Code, Is.EqualTo("FPDF1201"));
    }

    [TestCase("<p>A</wbr></p>", "FPDF1101")]
    [TestCase("<p>A<wbr/>B</p>", null)]
    [TestCase("<p>A<wbr class='x' id='y' title='t'>B</p>", null)]
    public void WbrIsAVoidElement(string html, string? code)
    {
        if (code is null) Assert.That(Layout(html), Is.Not.Empty);
        else Assert.That(Fails(html).Code, Is.EqualTo(code));
    }

    [Test]
    public void AnAuthorBoxOnWbrFailsAndTextStylesDoNot()
    {
        Assert.That(Fails("<style>wbr{padding:2pt}</style><p>A<wbr>B</p>").Code, Is.EqualTo("FPDF1201"));
        Assert.That(Layout("<style>wbr{color:#c00}</style><p>A<wbr>B</p>"), Is.Not.Empty);
    }
}
