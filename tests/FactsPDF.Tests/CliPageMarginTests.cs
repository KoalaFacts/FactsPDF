using System.Text;
using FactsPDF.CommandLine;
using NUnit.Framework;

namespace FactsPDF.Tests;

[TestFixture]
public sealed class CliPageMarginTests
{
    private static string Report => "<h1>Monthly invoice summary</h1>" +
        string.Concat(Enumerable.Range(1, 100).Select(i =>
            $"<p>Invoice {i:D3} covers services, billing and payment details.</p>"));

    private static (int Exit, byte[] Pdf, string Stderr) Render(
        IEnumerable<string> arguments, string? html = null)
    {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(html ?? Report));
        using var output = new MemoryStream();
        using var error = new StringWriter();
        var exit = CliApplication.Run(arguments.ToArray(), input, output, error);
        Assert.That(input.CanRead, Is.True, "CLI must leave caller stdin open.");
        Assert.That(output.CanWrite, Is.True, "CLI must leave caller stdout open.");
        return (exit, output.ToArray(), error.ToString());
    }

    [TestCase("20pt", "a4", false, 20d)]
    [TestCase("24PX", "letter", false, 18d)]
    [TestCase("0.75in", "letter", true, 54d)]
    [TestCase("10mm", "a4", false, 10d * (72d / 25.4d))]
    [TestCase("2cm", "letter", true, 2d * (72d / 2.54d))]
    public void PhysicalMarginAndPaperSettingsProduceTrueMultipagePdf(
        string margin, string paper, bool landscape, double points)
    {
        var args = new List<string> { "-", "-", "--margin", margin, "--page-size", paper, "--page-numbers" };
        if (landscape) args.Add("--landscape");
        var cli = Render(args);
        Assert.That(cli.Exit, Is.Zero, cli.Stderr);
        Assert.That(cli.Stderr, Does.Contain("Wrote"));
        var (width, height) = paper == "letter" ? (612d, 792d) : (595.28d, 841.89d);
        if (landscape) (width, height) = (height, width);
        using var expected = new MemoryStream();
        var result = PdfConverter.Convert(Report, expected, new PdfOptions
        {
            PageWidth = width, PageHeight = height, Margin = points, PageNumbers = true
        });
        Assert.That(result.PageCount, Is.GreaterThan(1), "Report must truly paginate.");
        Assert.That(cli.Pdf, Is.EqualTo(expected.ToArray()),
            "The CLI margin must change actual glyph placement, wrapping and footer positions.");
        Assert.That(Encoding.ASCII.GetString(cli.Pdf, 0, 8), Is.EqualTo("%PDF-1.7"));
    }

    [Test]
    public void ExistingDefaultAndExplicit36PtRemainByteForByteIdentical()
    {
        var baseline = Render(["-", "-"]);
        var explicitPoints = Render(["-", "-", "--margin", "36pt"]);
        var equivalentInches = Render(["-", "-", "--margin", "0.5in"]);
        Assert.That(baseline.Exit, Is.Zero, baseline.Stderr);
        Assert.That(explicitPoints.Exit, Is.Zero, explicitPoints.Stderr);
        Assert.That(equivalentInches.Exit, Is.Zero, equivalentInches.Stderr);
        Assert.That(explicitPoints.Pdf, Is.EqualTo(baseline.Pdf));
        Assert.That(equivalentInches.Pdf, Is.EqualTo(baseline.Pdf));
        var inset = Render(["-", "-", "--margin", "20pt"]);
        Assert.That(inset.Exit, Is.Zero, inset.Stderr);
        Assert.That(inset.Pdf, Is.Not.EqualTo(baseline.Pdf),
            "A different physical margin must visibly alter real PDF bytes.");
    }

    [Test]
    public void ZeroMarginIsExplicitAndMatchesThePublicPdfApi()
    {
        const string html = "<p>Edge-aligned accounting reference 0099.</p>";
        var actual = Render(["-", "-", "--margin", "0"], html);
        Assert.That(actual.Exit, Is.Zero, actual.Stderr);
        using var expected = new MemoryStream();
        PdfConverter.Convert(html, expected, new PdfOptions { Margin = 0 });
        Assert.That(actual.Pdf, Is.EqualTo(expected.ToArray()));
    }

    [Test]
    public void InvalidOrRepeatedMarginNeverOverwritesTheExistingCustomerPdf()
    {
        string[][] invalid =
        [
            ["--margin"],
            ["--margin", "--landscape"],
            ["--margin", "15"],
            ["--margin", "-1pt"],
            ["--margin", "NaNpt"],
            ["--margin", "Infinitypt"],
            ["--margin", "2e2pt"],
            ["--margin", "12,5mm"],
            ["--margin", "20%"],
            ["--margin", "calc(12mm)"],
            ["--margin", "1em"],
            ["--margin", "300pt"],
            ["--margin", "15mm", "--margin", "20mm"]
        ];
        var folder = Directory.CreateTempSubdirectory("factspdf-cli-margin-");
        try
        {
            var destination = Path.Combine(folder.FullName, "approved.pdf");
            var original = Encoding.ASCII.GetBytes("original customer report");
            foreach (var flags in invalid)
            {
                File.WriteAllBytes(destination, original);
                var bad = Render(new[] { "-", destination, "--overwrite" }.Concat(flags));
                Assert.That(bad.Exit, Is.EqualTo(2), string.Join(" ", flags) + ": " + bad.Stderr);
                Assert.That(bad.Pdf, Is.Empty);
                Assert.That(File.ReadAllBytes(destination), Is.EqualTo(original));
                Assert.That(Directory.GetFiles(folder.FullName, ".factspdf-*.tmp"), Is.Empty);
            }
            var recovered = Render(["-", destination, "--overwrite", "--margin", "20pt"]);
            Assert.That(recovered.Exit, Is.Zero, recovered.Stderr);
            Assert.That(Encoding.ASCII.GetString(File.ReadAllBytes(destination), 0, 8),
                Is.EqualTo("%PDF-1.7"));
        }
        finally { folder.Delete(recursive: true); }
    }

    [Test]
    public void HelpExplainsPhysicalMarginWithoutConsumingInvalidStdin()
    {
        using var input = new MemoryStream([0xff]);
        using var output = new MemoryStream();
        using var error = new StringWriter();
        Assert.That(CliApplication.Run(["--help"], input, output, error), Is.Zero);
        var help = Encoding.UTF8.GetString(output.ToArray());
        Assert.That(help, Does.Contain("--margin 0|length"));
        Assert.That(help, Does.Contain("default 36pt"));
        Assert.That(help, Does.Contain("pt/px/mm/cm/in"));
        Assert.That(error.ToString(), Is.Empty);
    }
}
