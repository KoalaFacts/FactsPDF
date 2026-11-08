using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>Fixed-column table cell em/rem padding must reach real PDF geometry and pagination.</summary>
[TestFixture]
public sealed class TableCellRelativePaddingTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 240, PageHeight = 150, Margin = 10 };

    private static string Cell(string style, string tag = "td", string content = "AB")
    {
        var element = "<" + tag + " style='font-size:10pt;background:#e4f0f8;border:1pt solid #315f82;" +
            style + "'>" + content + "</" + tag + ">";
        var rows = tag == "th"
            ? "<thead><tr>" + element + "</tr></thead><tbody><tr><td>TAIL</td></tr></tbody>"
            : "<tbody><tr>" + element + "</tr></tbody>";
        return "<html style='font-size:16pt'><body><table style='width:100pt;font-size:10pt;line-height:1.2'>" +
            "<colgroup><col style='width:100pt'></colgroup>" + rows + "</table></body></html>";
    }

    private static byte[] Pdf(string html, PdfOptions? options = null)
        => TableLayoutTests.Pdf(html, options ?? Page);

    private static List<LayoutPage> Layout(string html, PdfOptions? options = null)
        => TableLayoutTests.Layout(html, options ?? Page);

    private static void Reject(string html, string code)
    {
        using var destination = new MemoryStream();
        destination.Write("previous invoice PDF"u8);
        var prior = destination.ToArray();
        destination.Position = 5;
        var error = Assert.Throws<FactsPdfException>(() =>
            PdfConverter.Convert(html, destination, Page));
        Assert.That(error!.Code, Is.EqualTo(code));
        Assert.That(destination.ToArray(), Is.EqualTo(prior));
        Assert.That(destination.Position, Is.EqualTo(5));
        Assert.That(Pdf(Cell("padding:2pt")), Is.Not.Empty,
            "A rejected conversion must not poison a subsequent invoice.");
    }

    [TestCase("padding:0.2em", "padding:2pt")]
    [TestCase("padding:0.5rem", "padding:8pt")]
    [TestCase("padding:0.25em 0.5rem 0.75em 1rem",
        "padding:2.5pt 8pt 7.5pt 16pt")]
    [TestCase("padding-top:0.2em;padding-right:0.5rem;padding-bottom:0.3em;padding-left:0.25rem",
        "padding-top:2pt;padding-right:8pt;padding-bottom:3pt;padding-left:4pt")]
    [TestCase("padding-inline:0.2em 0.25rem;padding-block:0.5rem 0.3em",
        "padding-inline:2pt 4pt;padding-block:8pt 3pt")]
    [TestCase("padding:0em", "padding:0pt")]
    public void EmRemPaddingOnBoundedTableCellMatchesPointPdf(string relative, string physical)
    {
        var actual = Cell(relative);
        Assert.That(Pdf(actual), Is.EqualTo(Pdf(Cell(physical))));
        var relativeLayout = Layout(actual).Single();
        var physicalLayout = Layout(Cell(physical)).Single();
        Assert.That(relativeLayout.Runs.Select(r => (r.Text, r.X, r.Baseline)),
            Is.EqualTo(physicalLayout.Runs.Select(r => (r.Text, r.X, r.Baseline))));
        Assert.That(relativeLayout.PaintBoxes.Select(b => (b.X, b.Top, b.Width, b.Height)),
            Is.EqualTo(physicalLayout.PaintBoxes.Select(b => (b.X, b.Top, b.Width, b.Height))));
    }

    [Test]
    public void HeaderUsesOwnFontForEmButDocumentRootForRem()
    {
        var relative = Cell("padding:0.2em 0.25rem;font-size:12pt;font-weight:normal", "th");
        var physical = Cell("padding:2.4pt 4pt;font-size:12pt;font-weight:normal", "th");
        Assert.That(Pdf(relative), Is.EqualTo(Pdf(physical)));
        Assert.That(Layout(relative).Single().Runs.Single(r => r.Text == "AB").X,
            Is.EqualTo(Layout(physical).Single().Runs.Single(r => r.Text == "AB").X).Within(0.000001));
    }

    [Test]
    public void ImportantStylesheetPaddingAndFontWinnersReachMeasuredCell()
    {
        const string prefix = "<html style='font-size:16pt'><body><style>" +
            "td.invoice{font-size:12pt!important;padding:0.25em!important}" +
            "</style><table style='width:100pt;font-size:10pt;line-height:1.2'>" +
            "<colgroup><col style='width:100pt'></colgroup><tbody><tr>";
        const string suffix = "</tr></tbody></table></body></html>";
        var relative = prefix + "<td class='invoice' style='font-size:20pt;padding:1rem'>AB</td>" + suffix;
        var oracle = "<html style='font-size:16pt'><body>" +
            "<table style='width:100pt;font-size:10pt;line-height:1.2'>" +
            "<colgroup><col style='width:100pt'></colgroup><tbody><tr>" +
            "<td style='font-size:12pt;padding:3pt'>AB</td>" +
            "</tr></tbody></table></body></html>";
        Assert.That(Pdf(relative), Is.EqualTo(Pdf(oracle)));
    }

    [Test]
    public void SuppliedFontStillProducesIdenticalMeasuredPdf()
    {
        var options = Page with { Fonts = [PdfFont.FromBytes(FontFixture.Create(listDigits: true))] };
        Assert.That(Pdf(Cell("padding:0.25rem 0.2em"), options),
            Is.EqualTo(Pdf(Cell("padding:4pt 2pt"), options)));
    }

    private static string Fixture(string filename)
        => File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory,
            "examples", filename));

    [Test]
    public void UnmodifiedMultiPageInvoiceMatchesIndependentPhysicalCellPaddingOracle()
    {
        var actual = Fixture("report-cell-relative-padding.html");
        var oracle = Fixture("report-cell-relative-padding-oracle.html");
        var options = Page with { PageHeight = 130 };
        Assert.That(Pdf(actual, options), Is.EqualTo(Pdf(oracle, options)));
        var pages = Layout(actual, options);
        Assert.That(pages.Count, Is.GreaterThan(2));
        var runs = pages.SelectMany(p => p.Runs).Select(r => r.Text).ToArray();
        for (var i = 1; i <= 24; i++)
            Assert.That(runs.Count(s => s == $"R{i:00}"), Is.EqualTo(1),
                "Every invoice line must remain searchable exactly once.");
        Assert.That(pages.All(p => p.Runs.Any(r => r.Text == "CODE")), Is.True,
            "The repeated fixed-table header must survive pagination.");
    }

    [TestCase("padding:-1em", "FPDF1202")]
    [TestCase("padding:calc(1em + 2pt)", "FPDF1202")]
    [TestCase("padding:10000rem", "FPDF1202")]
    [TestCase("padding:10rem", "FPDF1302")]
    public void UnsupportedOrOverwideCellPaddingPreservesDestination(string style, string code)
        => Reject(Cell(style), code);

    [Test]
    public void TableTracksAndParagraphBoxGeometryStillRejectRelativeInsets()
    {
        Reject(Cell("padding:2pt").Replace("<tbody><tr>",
            "<tbody><tr style='padding:1em'>", StringComparison.Ordinal), "FPDF1201");
        Reject(Cell("padding:2pt").Replace("<td style='font-size:10pt;", "<td style='width:1em;font-size:10pt;", StringComparison.Ordinal),
            "FPDF1201");
        // The flat paragraph is structurally valid within the cell, but its
        // independent padding box must be rejected by CSS applicability.
        Reject(Cell("padding:2pt", content: "<p style='padding:1em'>AB</p>"), "FPDF1201");
    }

    [Test]
    public void InvalidUnmatchedRuleRemainsStrict()
        => Reject("<style>.not-used{padding:-1em}</style>" + Cell("padding:2pt"), "FPDF1202");
}
