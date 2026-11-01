using System.Globalization;
using System.Text;
using FactsPDF.CommandLine;
using NUnit.Framework;

namespace FactsPDF.Tests;

[TestFixture]
public sealed class CliPageGeometryTests
{
    private static string Html => "<h1>Quarterly report</h1>" +
        string.Concat(Enumerable.Range(1, 100).Select(i =>
            $"<p>Invoice {i:D3} - itemised charges for the accounting period.</p>"));

    private static (int Exit, byte[] Pdf, string Stderr) Render(
        string[] args, string? html = null)
    {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(html ?? Html));
        using var output = new MemoryStream();
        using var error = new StringWriter();
        var exit = CliApplication.Run(args, input, output, error);
        Assert.That(input.CanRead, Is.True);
        Assert.That(output.CanWrite, Is.True);
        return (exit, output.ToArray(), error.ToString());
    }

    [TestCase("a4", false, 595.28d, 841.89d)]
    [TestCase("A4", true, 841.89d, 595.28d)]
    [TestCase("letter", false, 612d, 792d)]
    [TestCase("LETTER", true, 792d, 612d)]
    public void ExplicitPaperAndOrientationChangeTruePdfMediaBoxAndPagination(
        string paper, bool landscape, double width, double height)
    {
        var args = new List<string> { "-", "-", "--page-size", paper, "--page-numbers" };
        if (landscape) args.Add("--landscape");
        var actual = Render(args.ToArray());
        Assert.That(actual.Exit, Is.Zero, actual.Stderr);
        Assert.That(actual.Stderr, Does.Contain("Wrote"));
        using var expected = new MemoryStream();
        var result = PdfConverter.Convert(Html, expected,
            new PdfOptions { PageWidth = width, PageHeight = height, PageNumbers = true });
        Assert.That(result.PageCount, Is.GreaterThan(1), "The witness must exercise actual pagination.");
        Assert.That(actual.Pdf, Is.EqualTo(expected.ToArray()),
            "The CLI must render the same full PDF as the source API page geometry.");
        var media = $"/MediaBox [0 0 {width.ToString("0.###", CultureInfo.InvariantCulture)} " +
                    $"{height.ToString("0.###", CultureInfo.InvariantCulture)}]";
        var pdf = Encoding.ASCII.GetString(actual.Pdf);
        Assert.That(pdf, Does.Contain(media));
        Assert.That(pdf.Split(media).Length - 1, Is.EqualTo(result.PageCount),
            "Every physical PDF page needs the selected MediaBox.");
    }

    [Test]
    public void DefaultA4AndExplicitA4RemainByteIdenticalAndOutputIsBinaryOnly()
    {
        var baseline = Render(["-", "-"]);
        var explicitA4 = Render(["-", "-", "--page-size", "a4"]);
        Assert.That(baseline.Exit, Is.Zero, baseline.Stderr);
        Assert.That(explicitA4.Exit, Is.Zero, explicitA4.Stderr);
        Assert.That(explicitA4.Pdf, Is.EqualTo(baseline.Pdf));
        Assert.That(explicitA4.Pdf, Is.Not.Empty);
        Assert.That(Encoding.ASCII.GetString(explicitA4.Pdf, 0, 8), Is.EqualTo("%PDF-1.7"));
    }

    [Test]
    public void LandscapeLetterWorksWithSuppliedUnicodeFontAndSourcePdfApi()
    {
        var font = FontFixture.Create();
        var path = Path.Combine(Path.GetTempPath(), "factspdf-page-geometry-" + Guid.NewGuid().ToString("N") + ".ttf");
        try
        {
            File.WriteAllBytes(path, font);
            const string html = "<p>Client summary: 中文。</p>";
            var actual = Render(["-", "-", "--page-size", "letter", "--landscape", "--font", path], html);
            Assert.That(actual.Exit, Is.Zero, actual.Stderr);
            using var expected = new MemoryStream();
            PdfConverter.Convert(html, expected, new PdfOptions
            {
                PageWidth = 792, PageHeight = 612, Fonts = [PdfFont.LoadTrueType(font)]
            });
            Assert.That(actual.Pdf, Is.EqualTo(expected.ToArray()));
            Assert.That(Encoding.ASCII.GetString(actual.Pdf), Does.Contain("/MediaBox [0 0 792 612]"));
        }
        finally { File.Delete(path); }
    }

    [Test]
    public void InvalidOrRepeatedPageOptionsDoNotReplaceAnExistingPdf()
    {
        var invalidFlags = new[]
        {
            new[] { "--page-size" },
            ["--page-size", "tabloid"],
            ["--page-size", "0"],
            ["--page-size", "--overwrite"],
            ["--page-size", "a4", "--page-size", "letter"],
            ["--landscape", "--landscape"]
        };
        var folder = Directory.CreateTempSubdirectory("factspdf-page-geometry-");
        try
        {
            var dest = Path.Combine(folder.FullName, "existing.pdf");
            var sentinel = Encoding.ASCII.GetBytes("original invoice bytes");
            foreach (var flags in invalidFlags)
            {
                File.WriteAllBytes(dest, sentinel);
                var args = new[] { "-", dest, "--overwrite" }.Concat(flags).ToArray();
                var bad = Render(args);
                Assert.That(bad.Exit, Is.EqualTo(2), bad.Stderr);
                Assert.That(bad.Pdf, Is.Empty);
                Assert.That(File.ReadAllBytes(dest), Is.EqualTo(sentinel));
                Assert.That(Directory.GetFiles(folder.FullName, ".factspdf-*.tmp"), Is.Empty);
            }
            var fixedInput = Render(["-", dest, "--overwrite", "--page-size", "letter", "--landscape"]);
            Assert.That(fixedInput.Exit, Is.Zero, fixedInput.Stderr);
            Assert.That(Encoding.ASCII.GetString(File.ReadAllBytes(dest), 0, 8), Is.EqualTo("%PDF-1.7"));
        }
        finally { folder.Delete(recursive: true); }
    }

    [Test]
    public void HelpDocumentsPageGeometryWithoutReadingStdin()
    {
        using var input = new MemoryStream([0xff]);
        using var output = new MemoryStream();
        using var error = new StringWriter();
        Assert.That(CliApplication.Run(["--help"], input, output, error), Is.Zero);
        var help = Encoding.UTF8.GetString(output.ToArray());
        Assert.That(help, Does.Contain("--page-size a4|letter"));
        Assert.That(help, Does.Contain("--landscape"));
        Assert.That(help, Does.Contain("595.28 x 841.89pt"));
        Assert.That(error.ToString(), Is.Empty);
    }
}
