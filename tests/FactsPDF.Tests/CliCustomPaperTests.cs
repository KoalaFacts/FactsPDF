using System.Globalization;
using System.Text;
using FactsPDF.CommandLine;
using NUnit.Framework;

namespace FactsPDF.Tests;

[TestFixture]
public sealed class CliCustomPaperTests
{
    private static string Report => "<h1>Business statement</h1>" +
        string.Concat(Enumerable.Range(1, 120).Select(i =>
            $"<p>Entry {i:D3}: audited invoice, delivery and settlement details.</p>"));

    private static (int Exit, byte[] Pdf, string Stderr) Run(string[] args, string? html = null)
    {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(html ?? Report));
        using var output = new MemoryStream();
        using var error = new StringWriter();
        var exit = CliApplication.Run(args, input, output, error);
        Assert.That(input.CanRead, Is.True);
        Assert.That(output.CanWrite, Is.True);
        return (exit, output.ToArray(), error.ToString());
    }

    [TestCase("148mm", "210mm", 148d * 72 / 25.4, 210d * 72 / 25.4)]
    [TestCase("8.5in", "14in", 612d, 1008d)]
    [TestCase("600px", "800px", 450d, 600d)]
    [TestCase("14cm", "20cm", 14d * 72 / 2.54, 20d * 72 / 2.54)]
    public void CustomPaperChangesActualPdfMediaBoxAndMultipageLayout(
        string width, string height, double widthPt, double heightPt)
    {
        var actual = Run(["-", "-", "--page-height", height, "--page-width", width,
            "--margin", "12mm", "--page-numbers"]);
        Assert.That(actual.Exit, Is.Zero, actual.Stderr);
        using var expected = new MemoryStream();
        var options = new PdfOptions
        {
            PageWidth = widthPt, PageHeight = heightPt,
            Margin = 12d * 72 / 25.4, PageNumbers = true
        };
        var result = PdfConverter.Convert(Report, expected, options);
        Assert.That(result.PageCount, Is.GreaterThan(1),
            "The test must measure real multipage report layout.");
        Assert.That(actual.Pdf, Is.EqualTo(expected.ToArray()),
            "Custom geometry must change actual glyphs, page breaks and footer coordinates.");
        var media = "/MediaBox [0 0 " +
            widthPt.ToString("0.###", CultureInfo.InvariantCulture) + " " +
            heightPt.ToString("0.###", CultureInfo.InvariantCulture) + "]";
        var pdfText = Encoding.ASCII.GetString(actual.Pdf);
        Assert.That(pdfText.Split(media).Length - 1, Is.EqualTo(result.PageCount),
            "Every PDF page must carry the custom physical MediaBox.");
    }

    [Test]
    public void ExplicitDefaultA4DimensionsPreserveHistoricalPdfBytes()
    {
        var baseline = Run(["-", "-"]);
        var custom = Run(["-", "-", "--page-width", "595.28pt", "--page-height", "841.89pt"]);
        Assert.That(baseline.Exit, Is.Zero, baseline.Stderr);
        Assert.That(custom.Exit, Is.Zero, custom.Stderr);
        Assert.That(custom.Pdf, Is.EqualTo(baseline.Pdf),
            "Passing dimensions equal to the public default must be byte-identical.");
    }

    [TestCase("5080mm")]
    [TestCase("508cm")]
    [TestCase("200in")]
    [TestCase("14400pt")]
    public void InclusiveMaximumPaperWidthHasIdenticalPdfInEveryUnit(string width)
    {
        const string html = "<p>A</p><p style='break-before:page'>B</p>";
        var actual = Run(["-", "-", "--page-width", width, "--page-height", "210mm"], html);
        var canonical = Run(["-", "-", "--page-width", "14400pt", "--page-height", "210mm"], html);
        Assert.That(actual.Exit, Is.Zero, actual.Stderr);
        Assert.That(canonical.Exit, Is.Zero, canonical.Stderr);
        Assert.That(actual.Pdf, Is.EqualTo(canonical.Pdf),
            "An exact physical limit must not change because of a floating unit scale.");
    }

    [TestCase("25.4mm")]
    [TestCase("2.54cm")]
    [TestCase("1in")]
    [TestCase("96px")]
    [TestCase("72pt")]
    public void InclusiveMinimumPaperWidthConvertsExactly(string width)
    {
        const string html = "<p>A</p>";
        var actual = Run(["-", "-", "--page-width", width, "--page-height", "210mm",
            "--margin", "12pt"], html);
        var canonical = Run(["-", "-", "--page-width", "72pt", "--page-height", "210mm",
            "--margin", "12pt"], html);
        Assert.That(actual.Exit, Is.Zero, actual.Stderr);
        Assert.That(actual.Pdf, Is.EqualTo(canonical.Pdf));
    }

    [TestCase("5080.0000000001mm")]
    [TestCase("508.0000000001cm")]
    [TestCase("200.0000000001in")]
    [TestCase("14400.0000000001pt")]
    public void GenuineOverLimitPaperDimensionsAreNeverRoundedIntoSupport(string width)
    {
        var actual = Run(["-", "-", "--page-width", width, "--page-height", "210mm"],
            "<p>A</p>");
        Assert.That(actual.Exit, Is.EqualTo(2));
        Assert.That(actual.Pdf, Is.Empty);
        Assert.That(actual.Stderr, Does.Contain("--page-width"));
    }

    [Test]
    public void CustomPhysicalPaperPreservesExplicitUnicodeFontPngAndPageText()
    {
        var folder = Directory.CreateTempSubdirectory("factspdf-custom-paper-");
        try
        {
            var fontBytes = FontFixture.Create(listDigits: true);
            var pngBytes = PngFixture.Image([0, 10, 20, 30, 0, 40, 50, 60, 128]);
            var fontFile = Path.Combine(folder.FullName, "trusted.ttf");
            var imageFile = Path.Combine(folder.FullName, "logo.png");
            File.WriteAllBytes(fontFile, fontBytes);
            File.WriteAllBytes(imageFile, pngBytes);
            const string html = "<p>中文 A</p><img src='logo'>" +
                "<p style='break-before:page'>中文 B</p>";
            var actual = Run(["-", "-", "--page-width", "6in", "--page-height", "9in",
                "--margin", "18pt", "--font", fontFile, "--image", "logo=" + imageFile,
                "--page-numbers"], html);
            Assert.That(actual.Exit, Is.Zero, actual.Stderr);
            using var expected = new MemoryStream();
            var result = PdfConverter.Convert(html, expected, new PdfOptions
            {
                PageWidth = 432, PageHeight = 648, Margin = 18, PageNumbers = true,
                Fonts = [PdfFont.LoadTrueType(fontBytes)],
                Images = new Dictionary<string, PdfImage> { ["logo"] = PdfImage.LoadImage(pngBytes) }
            });
            Assert.That(result.PageCount, Is.EqualTo(2));
            Assert.That(actual.Pdf, Is.EqualTo(expected.ToArray()));
            Assert.That(Encoding.ASCII.GetString(actual.Pdf), Does.Contain("/MediaBox [0 0 432 648]"));
        }
        finally { folder.Delete(recursive: true); }
    }

    [Test]
    public void MalformedConflictingOrUnplaceableDimensionsProtectCustomerPdf()
    {
        string[][] invalid =
        [
            ["--page-width"],
            ["--page-height"],
            ["--page-width", "148mm"],
            ["--page-height", "210mm"],
            ["--page-width", "--margin", "10mm", "--page-height", "210mm"],
            ["--page-width", "0", "--page-height", "210mm"],
            ["--page-width", "0pt", "--page-height", "210mm"],
            ["--page-width", "71.999pt", "--page-height", "210mm"],
            ["--page-width", "200.01in", "--page-height", "210mm"],
            ["--page-width", "NaNpt", "--page-height", "210mm"],
            ["--page-width", "Infinitypt", "--page-height", "210mm"],
            ["--page-width", "2e2pt", "--page-height", "210mm"],
            ["--page-width", "12,5mm", "--page-height", "210mm"],
            ["--page-width", "50%", "--page-height", "210mm"],
            ["--page-width", "calc(200pt)", "--page-height", "210mm"],
            ["--page-width", "-1pt", "--page-height", "210mm"],
            ["--page-width", "148mm", "--page-width", "149mm", "--page-height", "210mm"],
            ["--page-width", "148mm", "--page-height", "210mm", "--page-height", "211mm"],
            ["--page-width", "148mm", "--page-height", "210mm", "--page-size", "a4"],
            ["--page-width", "148mm", "--page-height", "210mm", "--landscape"],
            ["--page-width", "72pt", "--page-height", "210mm"],
            ["--page-width", "100pt", "--page-height", "210mm", "--margin", "51pt"]
        ];
        var folder = Directory.CreateTempSubdirectory("factspdf-custom-paper-invalid-");
        try
        {
            var target = Path.Combine(folder.FullName, "approved.pdf");
            var sentinel = Encoding.ASCII.GetBytes("previously approved customer PDF");
            foreach (var flags in invalid)
            {
                File.WriteAllBytes(target, sentinel);
                var result = Run(["-", target, "--overwrite", .. flags]);
                Assert.That(result.Exit, Is.EqualTo(2), string.Join(" ", flags) + ": " + result.Stderr);
                Assert.That(result.Pdf, Is.Empty);
                Assert.That(File.ReadAllBytes(target), Is.EqualTo(sentinel));
                Assert.That(Directory.GetFiles(folder.FullName, ".factspdf-*.tmp"), Is.Empty);
            }
            var recovered = Run(["-", target, "--overwrite", "--page-width", "148mm",
                "--page-height", "210mm", "--margin", "12mm"]);
            Assert.That(recovered.Exit, Is.Zero, recovered.Stderr);
            Assert.That(Encoding.ASCII.GetString(File.ReadAllBytes(target), 0, 8),
                Is.EqualTo("%PDF-1.7"));
        }
        finally { folder.Delete(recursive: true); }
    }

    [Test]
    public void HelpDocumentsExactCustomPageContractWithoutReadingStdin()
    {
        using var input = new MemoryStream([0xff]);
        using var output = new MemoryStream();
        using var error = new StringWriter();
        Assert.That(CliApplication.Run(["--help"], input, output, error), Is.Zero);
        var help = Encoding.UTF8.GetString(output.ToArray());
        Assert.That(help, Does.Contain("--page-width length --page-height length"));
        Assert.That(help, Does.Contain("72-14400pt"));
        Assert.That(help, Does.Contain("do not combine with --page-size or --landscape"));
        Assert.That(error.ToString(), Is.Empty);
    }
}
