using System.Text;
using FactsPDF.CommandLine;
using NUnit.Framework;

namespace FactsPDF.Tests;

[TestFixture]
public sealed class CliResourceGuidanceTests
{
    private string directory = null!;

    [SetUp]
    public void SetUp() => directory = Directory.CreateTempSubdirectory("factspdf-guidance-").FullName;

    [TearDown]
    public void TearDown() => Directory.Delete(directory, true);

    private static (int Exit, byte[] Output, string Error) Run(string[] args, string html,
        CancellationToken cancellation = default)
    {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(html));
        using var output = new MemoryStream();
        using var error = new StringWriter();
        var exit = CliApplication.Run(args, input, output, error, cancellation);
        Assert.That(input.CanRead, Is.True);
        Assert.That(output.CanWrite, Is.True);
        return (exit, output.ToArray(), error.ToString());
    }

    [TestCase("<p>中文</p>", "FPDF1301", false, "--font")]
    [TestCase("<p>中文</p>", "FPDF1504", true, "fallback")]
    [TestCase("<p><b>A</b></p>", "FPDF1507", false, "--font-bold")]
    [TestCase("<img src='logo'>", "FPDF1604", false, "--image")]
    public void ResourceFailuresKeepOriginalDiagnosticAndAddOneActionableHint(
        string html, string code, bool supplyLatin, string remedy)
    {
        var bytes = FontFixture.Create(latinOnly: true);
        var path = Path.Combine(directory, "latin.ttf");
        File.WriteAllBytes(path, bytes);
        var options = supplyLatin ? new PdfOptions { Fonts = [PdfFont.LoadTrueType(bytes)] } : new PdfOptions();
        using var expectedOutput = new MemoryStream();
        var expected = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, expectedOutput, options));
        Assert.That(expected!.Code, Is.EqualTo(code));
        Assert.That(expectedOutput.Length, Is.Zero);

        var existing = Path.Combine(directory, "existing.pdf");
        var absent = Path.Combine(directory, "absent.pdf");
        byte[] sentinel = [0x00, 0xff, 0x41, 0x0a];
        File.WriteAllBytes(existing, sentinel);
        foreach (var target in new[] { "-", existing, absent })
        {
            var args = new List<string> { "-", target, "--overwrite" };
            if (supplyLatin) args.AddRange(["--font", path]);
            var result = Run(args.ToArray(), html);
            Assert.That(result.Exit, Is.EqualTo(3));
            Assert.That(result.Output, Is.Empty);
            using var lines = new StringReader(result.Error);
            Assert.That(lines.ReadLine(), Is.EqualTo(expected.Message));
            var hint = lines.ReadLine();
            Assert.That(hint, Does.StartWith("Hint: "));
            Assert.That(hint, Does.Contain(remedy));
            Assert.That(hint, Does.Contain("docs/first-use-troubleshooting.md"));
            Assert.That(hint!.Length, Is.LessThanOrEqualTo(512));
            Assert.That(lines.ReadLine(), Is.Null, "Exactly one static hint; no customer input or extra output.");
            Assert.That(File.ReadAllBytes(existing), Is.EqualTo(sentinel));
            Assert.That(File.Exists(absent), Is.False);
            Assert.That(Directory.GetFiles(directory, ".factspdf-*.tmp"), Is.Empty);
        }
    }

    [TestCase("<p>中文</p>", "--font")]
    [TestCase("<p><b>中文</b></p>", "--font-bold")]
    [TestCase("<p><i>中文</i></p>", "--font-italic")]
    [TestCase("<p><b><i>中文</i></b></p>", "--font-bold-italic")]
    public void FollowingFontGuidanceRecoversWithUnchangedPdfBytes(string html, string flag)
    {
        var failure = Run(["-", "-"], html);
        Assert.That(failure.Exit, Is.EqualTo(3));
        Assert.That(failure.Error, Does.Contain("Hint: "));
        Assert.That(failure.Error, Does.Contain(flag));
        var bytes = FontFixture.Create();
        var path = Path.Combine(directory, "explicit.ttf");
        File.WriteAllBytes(path, bytes);
        var font = PdfFont.LoadTrueType(bytes);
        var options = flag switch
        {
            "--font-bold" => new PdfOptions { BoldFonts = [font] },
            "--font-italic" => new PdfOptions { ItalicFonts = [font] },
            "--font-bold-italic" => new PdfOptions { BoldItalicFonts = [font] },
            _ => new PdfOptions { Fonts = [font] }
        };
        var recovered = Run(["-", "-", flag, path], html);
        Assert.That(recovered.Exit, Is.Zero, recovered.Error);
        Assert.That(recovered.Error, Does.Not.Contain("Hint:"));
        using var expected = new MemoryStream();
        PdfConverter.Convert(html, expected, options);
        Assert.That(recovered.Output, Is.EqualTo(expected.ToArray()));
    }

    [Test]
    public void UnsupportedInputIsNotMisdiagnosedAsMissingResources()
    {
        const string html = "<script>not supported</script>";
        using var expectedOutput = new MemoryStream();
        var expected = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, expectedOutput));
        var result = Run(["-", "-"], html);
        Assert.That(result.Exit, Is.EqualTo(3));
        Assert.That(result.Output, Is.Empty);
        Assert.That(result.Error, Is.EqualTo(expected!.Message + Environment.NewLine));
    }

    [Test]
    public void SuccessfulAsciiConversionHasNoGuidanceAndMatchesCore()
    {
        const string html = "<p>First report</p>";
        var result = Run(["-", "-"], html);
        using var expected = new MemoryStream();
        PdfConverter.Convert(html, expected);
        Assert.That(result.Exit, Is.Zero);
        Assert.That(result.Output, Is.EqualTo(expected.ToArray()));
        Assert.That(result.Error, Does.Not.Contain("Hint:"));
    }

    [Test]
    public void HelpAndCancellationAreNotResourceFailures()
    {
        var help = Run(["--help"], "");
        Assert.That(help.Exit, Is.Zero);
        Assert.That(help.Error, Is.Empty);
        var cancelled = Run(["-", "-"], "<p>中文</p>", new CancellationToken(true));
        Assert.That(cancelled.Exit, Is.EqualTo(130));
        Assert.That(cancelled.Output, Is.Empty);
        Assert.That(cancelled.Error, Is.EqualTo("Conversion cancelled." + Environment.NewLine));
    }
}
