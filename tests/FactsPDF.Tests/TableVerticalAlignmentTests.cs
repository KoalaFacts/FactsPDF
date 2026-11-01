using System.Text;
using FactsPDF.CommandLine;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>Scoped vertical placement inside whole fixed-column report table rows.</summary>
[TestFixture]
public sealed class TableVerticalAlignmentTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 220, Margin = 10 };

    private static string TwoCells(string leftStyle = "", string left = "SHORT",
        string right = "FIRST<br>SECOND<br>THIRD")
        => "<table style='width:200pt;font-size:10pt;line-height:1.2'>" +
           "<colgroup><col style='width:100pt'><col style='width:100pt'></colgroup>" +
           "<tbody><tr><td style='padding:4pt;background-color:#dce9f4;" + leftStyle + "'>" + left + "</td>" +
           "<td style='padding:4pt;background-color:#f2eee2'>" + right + "</td></tr></tbody></table>";

    private static List<LayoutPage> Layout(string html, PdfOptions? options = null)
        => TableLayoutTests.Layout(html, options ?? Page);

    private static byte[] Pdf(string html, PdfOptions? options = null)
        => TableLayoutTests.Pdf(html, options ?? Page);

    private static void Reject(string html, string code, PdfOptions? options = null)
    {
        using var target = new MemoryStream();
        target.Write("PROTECTED PDF"u8);
        target.Position = 4;
        var error = Assert.Throws<FactsPdfException>(() =>
            PdfConverter.Convert(html, target, options ?? Page));
        Assert.That(error!.Code, Is.EqualTo(code));
        Assert.That(target.ToArray(), Is.EqualTo("PROTECTED PDF"u8.ToArray()));
        Assert.That(target.Position, Is.EqualTo(4));
        Assert.That(Pdf(TwoCells()), Is.Not.Empty,
            "A failed conversion must not poison the next independent report.");
    }

    [Test]
    public void WholeRowAlignsActualGlyphsAndPreservesPaintedCellGeometry()
    {
        var top = Layout(TwoCells("vertical-align:top")).Single();
        var middle = Layout(TwoCells("vertical-align:middle")).Single();
        var bottom = Layout(TwoCells("vertical-align:bottom")).Single();

        var first = top.Runs.Single(r => r.Text == "FIRST");
        var second = top.Runs.Single(r => r.Text == "SECOND");
        var third = top.Runs.Single(r => r.Text == "THIRD");
        Assert.That(top.Runs.Single(r => r.Text == "SHORT").Baseline,
            Is.EqualTo(first.Baseline).Within(0.000001));
        Assert.That(middle.Runs.Single(r => r.Text == "SHORT").Baseline,
            Is.EqualTo(second.Baseline).Within(0.000001));
        Assert.That(bottom.Runs.Single(r => r.Text == "SHORT").Baseline,
            Is.EqualTo(third.Baseline).Within(0.000001));

        static (double X, double Top, double Width, double Height)[] Boxes(LayoutPage page)
            => page.PaintBoxes.Select(b => (b.X, b.Top, b.Width, b.Height)).ToArray();
        Assert.That(Boxes(middle), Is.EqualTo(Boxes(top)));
        Assert.That(Boxes(bottom), Is.EqualTo(Boxes(top)));
        Assert.That(Pdf(TwoCells()), Is.EqualTo(Pdf(TwoCells("vertical-align:top"))),
            "Default table-cell top-alignment must retain existing PDF bytes.");
        Assert.That(Pdf(TwoCells("vertical-align:middle")), Is.Not.EqualTo(Pdf(TwoCells())));
        Assert.That(Pdf(TwoCells("vertical-align:bottom")), Is.Not.EqualTo(Pdf(TwoCells())));
    }

    [Test]
    public void StylesheetImportantAndCssWideValuesRespectWholeCellCascade()
    {
        var important = TwoCells("vertical-align:middle");
        important = "<style>td.priority{vertical-align:bottom!important}</style>" +
            important.Replace("<td style=", "<td class='priority' style=");
        var expected = "<style>td.priority{vertical-align:bottom!important}</style>" +
            TwoCells("vertical-align:bottom").Replace("<td style=", "<td class='priority' style=");
        Assert.That(Pdf(important), Is.EqualTo(Pdf(expected)));
        foreach (var keyword in new[] { "initial", "unset", "inherit" })
            Assert.That(Pdf(TwoCells("vertical-align:" + keyword)),
                Is.EqualTo(Pdf(TwoCells("vertical-align:top"))), keyword);
    }

    [Test]
    public void ShorterBrandedImageAndItsDecorationMoveWithinWholeRow()
    {
        var image = PdfImage.LoadImage(PngFixture.Image(new byte[32 * 129], 32, 32));
        var options = Page with { Images = new Dictionary<string, PdfImage> { ["mark"] = image } };
        const string child = "<img src='mark' style='width:12pt;padding:1pt;border:1pt solid red'>";
        var top = Layout(TwoCells("vertical-align:top", child), options).Single();
        var middle = Layout(TwoCells("vertical-align:middle", child), options).Single();
        var bottom = Layout(TwoCells("vertical-align:bottom", child), options).Single();
        var totalFree = bottom.Images.Single().Top - top.Images.Single().Top;
        Assert.That(totalFree, Is.GreaterThan(0));
        Assert.That(middle.Images.Single().Top - top.Images.Single().Top,
            Is.EqualTo(totalFree / 2).Within(0.000001));
        var topBorder = top.PaintBoxes.Single(b => b.Width == 16);
        var midBorder = middle.PaintBoxes.Single(b => b.Width == 16);
        var bottomBorder = bottom.PaintBoxes.Single(b => b.Width == 16);
        Assert.That(midBorder.Top - topBorder.Top, Is.EqualTo(totalFree / 2).Within(0.000001));
        Assert.That(bottomBorder.Top - topBorder.Top, Is.EqualTo(totalFree).Within(0.000001));
        Assert.That(top.Images.Single().Image, Is.SameAs(image));
        Assert.That(Pdf(TwoCells("vertical-align:bottom", child), options), Is.Not.Empty);
    }

    [Test]
    public void ManyWholeRowsRepeatTableHeaderWithoutDuplicatingOrDroppingAlignedLabels()
    {
        var rows = string.Concat(Enumerable.Range(1, 10).Select(i =>
            $"<tr><td style='vertical-align:bottom;border:1pt solid blue'>R{i:00}</td>" +
            $"<td style='border:1pt solid red'>A{i:00}<br>B{i:00}<br>C{i:00}</td></tr>"));
        var html = "<style>th{font-weight:normal;vertical-align:middle}</style>" +
            "<table style='width:180pt;font-size:10pt;line-height:1.2'><colgroup>" +
            "<col style='width:90pt'><col style='width:90pt'></colgroup>" +
            "<thead><tr><th>ID</th><th>READING</th></tr></thead><tbody>" +
            rows + "</tbody></table>";
        var options = Page with { PageHeight = 135 };
        var pages = Layout(html, options);
        Assert.That(pages.Count, Is.GreaterThan(2));
        Assert.That(pages.All(p => p.Runs.Count(r => r.Text == "ID") == 1), Is.True);
        for (var i = 1; i <= 10; i++)
        {
            var label = pages.SelectMany(p => p.Runs).Single(r => r.Text == $"R{i:00}");
            var terminal = pages.SelectMany(p => p.Runs).Single(r => r.Text == $"C{i:00}");
            Assert.That(label.Baseline, Is.EqualTo(terminal.Baseline).Within(0.000001));
        }
        Assert.That(Pdf(html, options), Is.Not.Empty);
    }

    [Test]
    public void ManagedCliAndLibraryRenderIdenticalVerticallyAlignedReport()
    {
        var html = TwoCells("vertical-align:middle");
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(html));
        using var output = new MemoryStream();
        using var diagnostic = new StringWriter();
        var result = CliApplication.Run(["-", "-"], input, output, diagnostic);
        Assert.That(result, Is.Zero, diagnostic.ToString());
        Assert.That(output.ToArray(), Is.EqualTo(Pdf(html, new PdfOptions())));
    }

    [TestCase("vertical-align:baseline", "FPDF1202")]
    [TestCase("vertical-align:sub", "FPDF1202")]
    [TestCase("vertical-align:middle bottom", "FPDF1202")]
    [TestCase("vertical-align:calc(10pt)", "FPDF1202")]
    public void UnsupportedCellAlignmentsRemainStrictAndOutputPreserving(string css, string code)
        => Reject(TwoCells(css), code);

    [TestCase("<p style='vertical-align:middle'>TEXT</p>")]
    [TestCase("<div style='vertical-align:bottom'>TEXT</div>")]
    [TestCase("<span style='vertical-align:middle'>TEXT</span>")]
    [TestCase("<table style='vertical-align:middle'><colgroup><col style='width:100%'></colgroup><tbody><tr><td>T</td></tr></tbody></table>")]
    public void OutsideTableCellsNeverAcceptIgnoredVerticalAlign(string html)
        => Reject(html, "FPDF1201");

    [Test]
    public void UnmatchedInvalidCssRuleStillRejectsAndProtectsDestination()
        => Reject("<style>.never{vertical-align:baseline}</style>" + TwoCells(), "FPDF1202");

    [TestCase("middle")]
    [TestCase("bottom")]
    public void RowsRequiringIndependentPageFragmentationRejectInsteadOfFakingTopAlignment(string value)
    {
        var html = TableRowFragmentationTests.Pair("BRIEF",
            TableRowFragmentationTests.Lines("CHECK", 30),
            leftStyle: $"vertical-align:{value}", header: false);
        Reject(html, "FPDF1302", new PdfOptions { PageWidth = 300, PageHeight = 100, Margin = 10 });
    }
}
