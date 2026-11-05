using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using FactsPDF.CommandLine;
using NUnit.Framework;

namespace FactsPDF.Tests;

[TestFixture]
public sealed class ReportHyperlinksTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 280, PageHeight = 170, Margin = 12 };

    private static byte[] Pdf(string html, PdfOptions? options = null)
    {
        using var stream = new MemoryStream();
        PdfConverter.Convert(html, stream, options ?? Page);
        return stream.ToArray();
    }

    private static void Reject(string html, string code, PdfOptions? options = null)
    {
        using var original = new MemoryStream();
        original.Write("customer-original"u8);
        original.Position = 3;
        var ex = Assert.Throws<FactsPdfException>(() =>
            PdfConverter.Convert(html, original, options ?? Page));
        Assert.Multiple(() =>
        {
            Assert.That(ex!.Code, Is.EqualTo(code));
            Assert.That(original.Position, Is.EqualTo(3));
            Assert.That(original.ToArray(), Is.EqualTo("customer-original"u8.ToArray()));
        });
    }

    [Test]
    public void LiteralHttpsAndHtmlEntityQueryBecomeSafePdfLinkObjects()
    {
        const string href = "https://example.com/order?id=7&print=1";
        var bytes = Pdf("<p>Order: <a href='https://example.com/order?id=7&amp;print=1'>View order</a></p>");
        var text = Encoding.Latin1.GetString(bytes);
        Assert.That(text, Does.Contain("/Subtype /Link"));
        Assert.That(text, Does.Contain("/Annots ["));
        Assert.That(text, Does.Contain("/S /URI"));
        Assert.That(text, Does.Contain("/URI <" +
            Convert.ToHexString(Encoding.ASCII.GetBytes(href)) + ">"));
        Assert.That(text, Does.Not.Contain(href), "A URI must not be interpolated as PDF syntax.");
        Assert.That(text, Does.Contain("56696577206F72646572"), "Selectable Courier text remains in the content stream.");
        Assert.That(Encoding.Latin1.GetString(Pdf("<p>Order: View order</p>")), Does.Not.Contain("/Annots"));

        var tree = HtmlDocumentReader.ReadTree(
            "<p>Order: <a href='https://example.com/order'>View order</a></p>", Page, default);
        var run = TextLayout.Layout(tree, Page, default).Single().Runs.Single(x => x.Style.LinkUri is not null);
        Assert.That(run.Text, Is.EqualTo("View order"));
        Assert.That(run.Style.Color, Is.EqualTo(new Rgb(0, 0, 0.8)));
        Assert.That(run.Style.Decoration, Is.EqualTo(TextDecoration.Underline));
        Assert.That(run.Width, Is.GreaterThan(0));
    }

    [Test]
    public void ExplicitCssImportantOverridesLinkInkAndDecorationButNotDestination()
    {
        var source = "<style>a.ref{color:red!important;text-decoration:none!important}</style>" +
            "<p><a class=ref href='https://example.com/report' style='color:blue;text-decoration:underline'>Report</a></p>";
        var pages = TextLayout.Layout(HtmlDocumentReader.ReadTree(source, Page, default), Page, default);
        var run = pages.Single().Runs.Single();
        Assert.That(run.Style.Color, Is.EqualTo(new Rgb(1, 0, 0)));
        Assert.That(run.Style.Decoration, Is.EqualTo(TextDecoration.None));
        Assert.That(run.Style.LinkUri, Is.EqualTo("https://example.com/report"));
        var pdf = Encoding.Latin1.GetString(Pdf(source));
        Assert.That(pdf, Does.Contain("/Subtype /Link"));
        Assert.That(pdf, Does.Not.Contain(" re f Q"), "No synthetic underline when author CSS removed it.");
    }

    [Test]
    public void RealPaginatedReportRetainsPerPageAnnotationRectsAndText()
    {
        var rows = string.Join("", Enumerable.Range(1, 40).Select(i =>
            $"<p style='margin:0 0 3pt'>Order {i:D2}: <a href='https://example.com/orders/{i:D2}'>Track {i:D2}</a></p>"));
        var source = "<section>" + rows + "</section>";
        var pages = TextLayout.Layout(HtmlDocumentReader.ReadTree(source, Page, default), Page, default);
        Assert.That(pages.Count, Is.GreaterThan(1));
        var bytes = Pdf(source);
        var text = Encoding.Latin1.GetString(bytes);
        Assert.That(Regex.Count(text, "/Subtype /Link"), Is.EqualTo(40));
        Assert.That(Regex.Count(text, "/Annots \\["), Is.EqualTo(pages.Count));
        Assert.That(text, Does.Contain(Convert.ToHexString(Encoding.ASCII.GetBytes(
            "https://example.com/orders/40"))));
        foreach (Match m in Regex.Matches(text, @"/Rect \[([\d.]+) ([\d.]+) ([\d.]+) ([\d.]+)\]"))
        {
            var numbers = Enumerable.Range(1, 4).Select(i => double.Parse(m.Groups[i].Value,
                System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            Assert.That(numbers[0], Is.LessThan(numbers[2]));
            Assert.That(numbers[1], Is.LessThan(numbers[3]));
            Assert.That(numbers[0], Is.InRange(0, Page.PageWidth));
            Assert.That(numbers[2], Is.InRange(0, Page.PageWidth));
            Assert.That(numbers[1], Is.InRange(0, Page.PageHeight));
            Assert.That(numbers[3], Is.InRange(0, Page.PageHeight));
        }
        Assert.That(Regex.Count(text, "/Rect \\["), Is.EqualTo(40));
        Reject(source, "FPDF1303", Page with { MaxPages = 1 });
    }

    [Test]
    public void TableCellsAndTrueTypePdfUseSameRealUriAnnotations()
    {
        var font = PdfFont.FromBytes(FontFixture.Create());
        var options = Page with { Fonts = [font] };
        const string source = "<table style='width:240pt'><colgroup><col style='width:240pt'></colgroup>" +
            "<tbody><tr><td><p>AB <a href='mailto:sales@example.com'>AB</a></p></td></tr>" +
            "</tbody></table>";
        var text = Encoding.Latin1.GetString(Pdf(source, options));
        Assert.That(text, Does.Contain("/Subtype /Type0"));
        Assert.That(text, Does.Contain("/Subtype /Link"));
        Assert.That(text, Does.Contain(Convert.ToHexString(
            Encoding.ASCII.GetBytes("mailto:sales@example.com"))));
        Assert.That(Encoding.Latin1.GetString(Pdf(
            "<p><a href='mailto:help+orders@example.co.uk'>AB</a></p>", options)),
            Does.Contain(Convert.ToHexString(Encoding.ASCII.GetBytes("mailto:help+orders@example.co.uk"))));
    }

    [Test]
    public void LinkedTrueTypeGlyphVerticalBoundsContainTallInkAndDescenders()
    {
        // Synthetic test font only: explicitly raise one mapped glyph A's
        // glyf header bounds above typical accent height and below descenders.
        // This is not an external font or a redistributed asset.
        var sourceFont = FontFixture.Create();
        var location = FontFixture.Table(sourceFont, "loca");
        var glyph2Offset = (int)BinaryPrimitives.ReadUInt32BigEndian(
            sourceFont.AsSpan(location + 2 * 4, 4));
        var glyphOffset = FontFixture.Table(sourceFont, "glyf") + glyph2Offset;
        FontFixture.U16(sourceFont, glyphOffset + 4, unchecked((ushort)-350));
        FontFixture.U16(sourceFont, glyphOffset + 8, 1200);
        var head = FontFixture.Table(sourceFont, "head");
        FontFixture.U16(sourceFont, head + 38, unchecked((ushort)-350));
        FontFixture.U16(sourceFont, head + 42, 1200);
        var font = PdfFont.FromBytes(sourceFont);
        var options = Page with { Fonts = [font] };
        const string html = "<p><a href='https://example.com/ink'>A</a></p>";
        var pages = TextLayout.Layout(HtmlDocumentReader.ReadTree(html, options, default), options, default);
        var run = pages.Single().Runs.Single(x => x.Style.LinkUri is not null);
        var pdf = Encoding.Latin1.GetString(Pdf(html, options));
        var match = Regex.Match(pdf, @"/Subtype /Link /Rect \[([\d.]+) ([\d.]+) ([\d.]+) ([\d.]+)\]");
        Assert.That(match.Success, Is.True, "Real PDF contains a link rectangle.");
        var bottom = double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        var top = double.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture);
        Assert.That(top, Is.GreaterThanOrEqualTo(run.Baseline + 1.2 * run.Style.FontSize - 0.00001),
            "Even tall TrueType accent ink must be clickable.");
        Assert.That(bottom, Is.LessThanOrEqualTo(run.Baseline - 0.35 * run.Style.FontSize + 0.00001),
            "The linked glyph's descender must also be clickable.");
    }

    [Test]
    public void CliProducesActualLinkAndFailureKeepsPriorDestination()
    {
        const string html = "<p><a href='https://example.com/invoice'>Invoice</a></p>";
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(html));
        using var output = new MemoryStream();
        using var error = new StringWriter();
        Assert.That(CliApplication.Run(["-", "-"], input, output, error), Is.Zero);
        Assert.That(Encoding.Latin1.GetString(output.ToArray()), Does.Contain("/Subtype /Link"));

        var directory = Directory.CreateTempSubdirectory("factspdf-links-");
        try
        {
            var outputPath = Path.Combine(directory.FullName, "existing.pdf");
            File.WriteAllText(outputPath, "keep existing");
            using var invalid = new MemoryStream(Encoding.UTF8.GetBytes(
                "<p><a href='javascript:alert(1)'>Bad link</a></p>"));
            using var stdout = new MemoryStream();
            using var errors = new StringWriter();
            Assert.That(CliApplication.Run(["-", outputPath, "--overwrite"], invalid, stdout, errors), Is.EqualTo(3));
            Assert.That(errors.ToString(), Does.Contain("FPDF1103"));
            Assert.That(File.ReadAllText(outputPath), Is.EqualTo("keep existing"));
            Assert.That(Directory.GetFiles(directory.FullName), Has.Length.EqualTo(1));
        }
        finally { directory.Delete(recursive: true); }
    }

    [TestCase("<a>Missing destination</a>", "FPDF1103")]
    [TestCase("<a href='http://example.com'>Insecure</a>", "FPDF1103")]
    [TestCase("<a href='javascript:alert(1)'>Script</a>", "FPDF1103")]
    [TestCase("<a href='file:///secret'>Local path</a>", "FPDF1103")]
    [TestCase("<a href='#page'>Local fragment</a>", "FPDF1103")]
    [TestCase("<a href='https://user:pass@example.com'>Credentials</a>", "FPDF1103")]
    [TestCase("<a href='https://example.com/%ZZ'>Broken escape</a>", "FPDF1103")]
    [TestCase("<a href='https://example.com/orders#section'>Unsupported fragment</a>", "FPDF1103")]
    [TestCase("<a href='mailto:sales@example.com%0Asubject'>Encoded mailto injection</a>", "FPDF1103")]
    [TestCase("<a href='mailto:sales@example.com?subject=hi'>Email query</a>", "FPDF1103")]
    [TestCase("<a href='mailto:alice@example.com/not-an-address'>Mailbox path</a>", "FPDF1103")]
    [TestCase("<a href='mailto:alice@example.com:443'>Mailbox port</a>", "FPDF1103")]
    [TestCase("<a href='mailto:alice@-example.com'>Invalid domain label</a>", "FPDF1103")]
    [TestCase("<a href='mailto:alice..bob@example.com'>Invalid local dots</a>", "FPDF1103")]
    [TestCase("<a href='https://example.com'><a href='https://example.com'>Nested</a></a>", "FPDF1101")]
    [TestCase("<a href='https://example.com'><div>Block</div></a>", "FPDF1101")]
    public void UnsupportedLinksRejectAtomically(string html, string error)
        => Reject("<p>" + html + "</p>", error);

    [Test]
    public void LinkAnnotationConsumesDisplayBudgetAndOutputLimit()
    {
        const string source = "<p><a href='https://example.com'>AB</a></p>";
        var baseline = TextLayout.Layout(HtmlDocumentReader.ReadTree(source, Page, default), Page, default);
        Assert.That(baseline.Single().Runs.Count, Is.EqualTo(1));
        Reject(source, "FPDF1401", Page with { MaxDisplayCommands = 2 });
        Assert.That(Pdf(source, Page with { MaxDisplayCommands = 3 }).Length, Is.GreaterThan(0));
        Reject(source, "FPDF1401", Page with { MaxOutputBytes = 60 });
    }
}
