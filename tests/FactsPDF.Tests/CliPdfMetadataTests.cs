using System.Text;
using FactsPDF.CommandLine;
using NUnit.Framework;

namespace FactsPDF.Tests;

[TestFixture]
public sealed class CliPdfMetadataTests
{
    private const string Report = "<h1>Quarterly report</h1><p>Invoice A</p>" +
        "<p style='break-before:page'>Invoice B</p>";
    private static (int Exit, byte[] Pdf, string Stderr) Render(string[] args, string? html = null)
    {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(html ?? Report));
        using var output = new MemoryStream();
        using var error = new StringWriter();
        var code = CliApplication.Run(args, input, output, error);
        Assert.That(input.CanRead, Is.True);
        Assert.That(output.CanWrite, Is.True);
        return (code, output.ToArray(), error.ToString());
    }

    private static string Hex(string value) =>
        "FEFF" + Convert.ToHexString(Encoding.BigEndianUnicode.GetBytes(value));

    [Test]
    public void PdfInfoActuallyLinksUnicodeMetadataAndPreservesMultipageOutput()
    {
        const string title = "Quarterly inspection — 澳新 🧾";
        const string author = "Koala Facts";
        const string subject = "Facilities (Q4)";
        const string keywords = "invoice, inspection, AU/NZ";
        var result = Render(["-", "-", "--page-numbers", "--title", title,
            "--author", author, "--subject", subject, "--keywords", keywords]);
        var baseline = Render(["-", "-", "--page-numbers"]);
        Assert.That(result.Exit, Is.Zero, result.Stderr);
        Assert.That(baseline.Exit, Is.Zero, baseline.Stderr);
        using var expected = new MemoryStream();
        var report = PdfConverter.Convert(Report, expected, new PdfOptions
        {
            PageNumbers = true, Title = title, Author = author,
            Subject = subject, Keywords = keywords
        });
        Assert.That(report.PageCount, Is.EqualTo(2));
        Assert.That(result.Pdf, Is.EqualTo(expected.ToArray()));
        Assert.That(result.Pdf, Is.Not.EqualTo(baseline.Pdf));
        var pdf = Encoding.ASCII.GetString(result.Pdf);
        Assert.That(pdf, Does.Contain("/Title <" + Hex(title) + ">"));
        Assert.That(pdf, Does.Contain("/Author <" + Hex(author) + ">"));
        Assert.That(pdf, Does.Contain("/Subject <" + Hex(subject) + ">"));
        Assert.That(pdf, Does.Contain("/Keywords <" + Hex(keywords) + ">"));
        Assert.That(pdf, Does.Match(@"trailer\n<< /Size \d+ /Root 1 0 R /Info \d+ 0 R >>"));
        Assert.That(Encoding.ASCII.GetString(baseline.Pdf), Does.Not.Contain("/Info "));
        Assert.That(pdf.Split("/MediaBox [").Length,
            Is.EqualTo(Encoding.ASCII.GetString(baseline.Pdf).Split("/MediaBox [").Length));
    }

    [Test]
    public void EmbeddedUnicodeFontAndExplicitPngKeepTheirPdfResources()
    {
        var folder = Directory.CreateTempSubdirectory("factspdf-metadata-");
        try
        {
            var font = FontFixture.Create(listDigits: true);
            var png = PngFixture.Image([0, 10, 20, 30, 0, 40, 50, 60, 128]);
            var fontPath = Path.Combine(folder.FullName, "font.ttf");
            var imagePath = Path.Combine(folder.FullName, "logo.png");
            File.WriteAllBytes(fontPath, font);
            File.WriteAllBytes(imagePath, png);
            const string html = "<p>中文 A</p><img src='logo'><p style='break-before:page'>中文 B</p>";
            var result = Render(["-", "-", "--font", fontPath, "--image", "logo=" + imagePath,
                "--title", "中文检查", "--author", "Koala Facts"], html);
            Assert.That(result.Exit, Is.Zero, result.Stderr);
            using var expected = new MemoryStream();
            var report = PdfConverter.Convert(html, expected, new PdfOptions
            {
                Fonts = [PdfFont.LoadTrueType(font)],
                Images = new Dictionary<string, PdfImage> { ["logo"] = PdfImage.LoadImage(png) },
                Title = "中文检查", Author = "Koala Facts"
            });
            Assert.That(report.PageCount, Is.EqualTo(2));
            Assert.That(result.Pdf, Is.EqualTo(expected.ToArray()));
            var body = Encoding.ASCII.GetString(result.Pdf);
            Assert.That(body, Does.Contain("/Title <" + Hex("中文检查") + ">"));
            Assert.That(body, Does.Contain("/ToUnicode"));
            Assert.That(body, Does.Contain("/Subtype /Image"));
        }
        finally { folder.Delete(recursive: true); }
    }

    [Test]
    public void InvalidMetadataAndRepeatedFlagsNeverReplacePriorReport()
    {
        string[][] invalid =
        [
            ["--title"], ["--author"], ["--subject"], ["--keywords"],
            ["--title", "--author"], ["--title", ""], ["--title", "   "],
            ["--subject", "line\nfeed"], ["--keywords", "x\0y"],
            ["--author", new string('A', 1025)], ["--title", "\uD800"],
            ["--subject", "\uDC00"], ["--title", "first", "--title", "second"]
        ];
        var folder = Directory.CreateTempSubdirectory("factspdf-metadata-invalid-");
        try
        {
            var destination = Path.Combine(folder.FullName, "approved.pdf");
            var sentinel = Encoding.ASCII.GetBytes("already approved report");
            foreach (var flags in invalid)
            {
                File.WriteAllBytes(destination, sentinel);
                var bad = Render(["-", destination, "--overwrite", .. flags]);
                Assert.That(bad.Exit, Is.EqualTo(2), string.Join(",", flags) + ": " + bad.Stderr);
                Assert.That(bad.Pdf, Is.Empty);
                Assert.That(File.ReadAllBytes(destination), Is.EqualTo(sentinel));
                Assert.That(Directory.GetFiles(folder.FullName, ".factspdf-*.tmp"), Is.Empty);
            }
            var retry = Render(["-", destination, "--overwrite", "--title", "Approved"]);
            Assert.That(retry.Exit, Is.Zero, retry.Stderr);
            Assert.That(Encoding.ASCII.GetString(File.ReadAllBytes(destination)),
                Does.Contain("/Title <" + Hex("Approved") + ">"));
        }
        finally { folder.Delete(recursive: true); }
    }

    [Test]
    public void DefaultRemainsByteIdenticalAndMetadataHonoursOutputBudget()
    {
        var baseline = Render(["-", "-"]);
        var titled = Render(["-", "-", "--title", "Report"]);
        var repeated = Render(["-", "-", "--title", "Report"]);
        Assert.That(baseline.Exit, Is.Zero, baseline.Stderr);
        Assert.That(titled.Exit, Is.Zero, titled.Stderr);
        Assert.That(titled.Pdf, Is.EqualTo(repeated.Pdf));
        Assert.That(Encoding.ASCII.GetString(baseline.Pdf), Does.Not.Contain("/Info "));
        Assert.That(Encoding.ASCII.GetString(titled.Pdf), Does.Not.Contain("/CreationDate"));
        using var sink = new MemoryStream();
        sink.WriteByte(42);
        var ex = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(
            Report, sink, new PdfOptions
            {
                Title = "Report", MaxOutputBytes = baseline.Pdf.Length + 1
            }));
        Assert.That(ex!.Code, Is.EqualTo("FPDF1401"));
        Assert.That(sink.ToArray(), Is.EqualTo(new byte[] { 42 }));
    }

    [Test]
    public void PublicApiRejectsMalformedUnicodeBeforeWriting()
    {
        using var output = new MemoryStream();
        output.WriteByte(42);
        Assert.Throws<ArgumentException>(() => PdfConverter.Convert(
            Report, output, new PdfOptions { Author = "\uD800" }));
        Assert.Throws<ArgumentException>(() => PdfConverter.Convert(
            Report, output, new PdfOptions { Subject = "first\rsecond" }));
        Assert.That(output.ToArray(), Is.EqualTo(new byte[] { 42 }));
    }

    [Test]
    public void HelpListsFullMetadataScope()
    {
        var result = Render(["--help"]);
        Assert.That(result.Exit, Is.Zero);
        var help = Encoding.UTF8.GetString(result.Pdf);
        foreach (var field in new[] { "title", "author", "subject", "keywords" })
            Assert.That(help, Does.Contain("--" + field + " text"));
        Assert.That(help, Does.Contain("never extracted from HTML"));
    }
}
