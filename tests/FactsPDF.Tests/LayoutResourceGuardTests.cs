using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

[TestFixture]
public sealed class LayoutResourceGuardTests
{
    private static readonly PdfOptions Page = new()
    {
        PageWidth = 300, PageHeight = 140, Margin = 20
    };

    private static List<LayoutPage> Layout(string html, int limit)
    {
        var options = Page with { MaxDisplayCommands = limit };
        return TextLayout.Layout(HtmlDocumentReader.ReadTree(html, options, default), options, default);
    }

    private static void Limit(Action action)
    {
        var error = Assert.Throws<FactsPdfException>(() => action());
        Assert.That(error!.Code, Is.EqualTo("FPDF1401"));
    }

    [Test]
    public void TextRunsAreLimitedInsideLayoutNotOnlyInTheConverter()
        => Limit(() => Layout("<p>A<span style='color:red'>B</span></p>", 1));

    [Test]
    public void PaintRectanglesAndTextShareOneLayoutBudget()
        => Limit(() => Layout("<div style='background-color:red;border:1pt solid blue;padding:2pt'>A</div>", 5));

    [Test]
    public void BudgetAggregatesAllPageFragments()
        => Limit(() => Layout("<div style='background-color:red'><p>A</p>" +
            "<p style='break-before:page'>B</p></div>", 3));

    [Test]
    public void ExceededTextBudgetStopsBeforeLaterUnsupportedGlyphLayout()
        => Limit(() => Layout("<p>A<span style='color:red'>B</span></p><p>中文</p>", 1));

    [Test]
    public void ExceededPaintBudgetStopsBeforeLaterUnsupportedGlyphLayout()
        => Limit(() => Layout("<div style='background-color:red;padding:2pt'></div>" +
            "<div style='background-color:blue;padding:2pt'></div><p>中文</p>", 1));

    [Test]
    public void CompatibilityParagraphEntryPointHasTheSameBudget()
    {
        var style = new TextStyle(12, 1.2, new Rgb(0, 0, 0), TextAlignment.Left);
        var one = new Paragraph(style);
        one.Runs.Add(new TextRun("A", style));
        var two = new Paragraph(style);
        two.Runs.Add(new TextRun("B", style));
        Limit(() => TextLayout.Layout(new List<Paragraph> { one, two },
            Page with { MaxDisplayCommands = 1 }, default));
    }

    [Test]
    public void ExactMixedBudgetSucceedsWithoutChargingAFragmentTwice()
    {
        var pages = Layout("<div style='background-color:red;border:1pt solid blue;padding:2pt'>A</div>", 6);
        Assert.That(pages.Sum(p => p.Runs.Count + p.PaintBoxes.Sum(b => b.CommandCount)), Is.EqualTo(6));
    }

    [Test]
    public void ExactCrossPageBudgetSucceeds()
    {
        var pages = Layout("<div style='background-color:red'><p>A</p>" +
            "<p style='break-before:page'>B</p></div>", 4);
        Assert.That(pages, Has.Count.EqualTo(2));
        Assert.That(pages.Sum(p => p.Runs.Count + p.PaintBoxes.Sum(b => b.CommandCount)), Is.EqualTo(4));
    }

    [Test]
    public void ZeroAreaBoxesDoNotConsumeDisplayCommands()
    {
        var html = string.Concat(Enumerable.Repeat("<div style='background-color:red'></div>", 10)) + "<p>A</p>";
        var pages = Layout(html, 1);
        Assert.That(pages.Sum(p => p.Runs.Count), Is.EqualTo(1));
        Assert.That(pages.Sum(p => p.PaintBoxes.Sum(b => b.CommandCount)), Is.Zero);
    }

    [Test]
    public void ConverterRetainsCallerBytesWhenIncrementalLimitFails()
    {
        using var output = new MemoryStream();
        output.Write("keep"u8);
        Limit(() => PdfConverter.Convert("<p>A<span style='color:red'>B</span></p>",
            output, Page with { MaxDisplayCommands = 1 }));
        Assert.That(Encoding.ASCII.GetString(output.ToArray()), Is.EqualTo("keep"));
    }

    [Test]
    public void CountersArePerConversionAndFailuresDoNotLeak()
    {
        for (var i = 0; i < 3; i++)
        {
            Limit(() => Layout("<p>A<span style='color:red'>B</span></p>", 1));
            Assert.That(Layout("<p>A</p>", 1)[0].Runs, Has.Count.EqualTo(1));
        }
    }

    [Test]
    public void HighBudgetAndExactBudgetProduceIdenticalPdfBytes()
    {
        const string html = "<div style='background-color:red'><p>A</p>" +
            "<p style='break-before:page'>B</p></div>";
        using var exact = new MemoryStream();
        using var high = new MemoryStream();
        PdfConverter.Convert(html, exact, Page with { MaxDisplayCommands = 4 });
        PdfConverter.Convert(html, high, Page with { MaxDisplayCommands = int.MaxValue });
        Assert.That(exact.ToArray(), Is.EqualTo(high.ToArray()));
    }
}
