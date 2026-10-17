using System.Text;
using FactsPDF.CommandLine;
using NUnit.Framework;

namespace FactsPDF.Tests;

[TestFixture]
public sealed class CliTests
{
    private string directory = null!;
    [SetUp] public void SetUp() => directory = Directory.CreateTempSubdirectory("factspdf-tests-").FullName;
    [TearDown] public void TearDown() => Directory.Delete(directory, true);
    private string FilePath(string name) => Path.Combine(directory, name);

    private static (int Exit, byte[] Output, string Error) Run(string[] args, byte[]? bytes = null)
    {
        using var input = new MemoryStream(bytes ?? Encoding.UTF8.GetBytes("<p>Hello CLI</p>"));
        using var output = new MemoryStream();
        using var error = new StringWriter();
        var exit = CliApplication.Run(args, input, output, error);
        Assert.That(input.CanRead, Is.True);
        Assert.That(output.CanWrite, Is.True);
        return (exit, output.ToArray(), error.ToString());
    }

    [Test]
    public void StdoutContainsPdfAndStatusGoesToStderr()
    {
        var result = Run(["-", "-"]);
        Assert.That(result.Exit, Is.Zero);
        Assert.That(Encoding.ASCII.GetString(result.Output), Does.StartWith("%PDF-1.7"));
        Assert.That(result.Error, Does.Contain("1 page"));
    }

    [Test]
    public void FileConversionCreatesPdfAndNoStdoutText()
    {
        File.WriteAllText(FilePath("in.html"), "<p>From a file</p>");
        var result = Run([FilePath("in.html"), FilePath("out.pdf")]);
        Assert.That(result.Exit, Is.Zero);
        Assert.That(result.Output, Is.Empty);
        Assert.That(File.Exists(FilePath("out.pdf")), Is.True);
        Assert.That(File.ReadAllText(FilePath("out.pdf")), Does.StartWith("%PDF-1.7"));
    }

    [Test]
    public void ExistingDestinationIsProtectedByDefault()
    {
        File.WriteAllText(FilePath("out.pdf"), "keep me");
        Assert.That(Run(["-", FilePath("out.pdf")]).Exit, Is.EqualTo(4));
        Assert.That(File.ReadAllText(FilePath("out.pdf")), Is.EqualTo("keep me"));
    }

    [Test]
    public void ExplicitOverwriteReplacesDestinationAfterSuccess()
    {
        File.WriteAllText(FilePath("out.pdf"), "old");
        Assert.That(Run(["-", FilePath("out.pdf"), "--overwrite"]).Exit, Is.Zero);
        Assert.That(File.ReadAllText(FilePath("out.pdf")), Does.StartWith("%PDF-1.7"));
        Assert.That(Directory.GetFiles(directory).Length, Is.EqualTo(1));
    }

    [Test]
    public void RenderFailurePreservesDestinationAndRemovesTemporaryFiles()
    {
        File.WriteAllText(FilePath("out.pdf"), "keep me");
        var result = Run(["-", FilePath("out.pdf"), "--overwrite"], Encoding.UTF8.GetBytes("<script>bad</script>"));
        Assert.That(result.Exit, Is.EqualTo(3));
        Assert.That(result.Error, Does.Contain("FPDF1102"));
        Assert.That(File.ReadAllText(FilePath("out.pdf")), Is.EqualTo("keep me"));
        Assert.That(Directory.GetFiles(directory).Length, Is.EqualTo(1));
    }

    [Test]
    public void SameInputAndOutputAreRejectedEvenWithOverwrite()
    {
        var path = FilePath("in.html");
        File.WriteAllText(path, "<p>keep me</p>");
        Assert.That(Run([path, path, "--overwrite"]).Exit, Is.EqualTo(2));
        Assert.That(File.ReadAllText(path), Is.EqualTo("<p>keep me</p>"));
    }

    [Test]
    public void InvalidUtf8IsRejectedWithoutOutput()
    {
        var result = Run(["-", "-"], [0xff, 0xfe, 0xff]);
        Assert.That(result.Exit, Is.EqualTo(3));
        Assert.That(result.Output, Is.Empty);
    }

    [Test]
    public void InputLimitAppliesBeforeRendering()
    {
        var result = Run(["-", "-"], Encoding.UTF8.GetBytes(new string('A', 1_000_001)));
        Assert.That(result.Exit, Is.EqualTo(3));
        Assert.That(result.Error, Does.Contain("FPDF1001"));
        Assert.That(result.Output, Is.Empty);
    }

    [Test]
    public void AcceptsUtf8Bom()
    {
        var result = Run(["-", "-"], [0xef, 0xbb, 0xbf, .. Encoding.UTF8.GetBytes("<p>UTF8 BOM</p>")]);
        Assert.That(result.Exit, Is.Zero);
        Assert.That(Encoding.ASCII.GetString(result.Output), Does.StartWith("%PDF-1.7"));
    }

    [Test]
    public void UsageErrorsHaveANonzeroExitCode()
    {
        var result = Run([]);
        Assert.That(result.Exit, Is.EqualTo(2));
        Assert.That(result.Error, Does.Contain("Usage:"));
        Assert.That(result.Output, Is.Empty);
    }

    [Test]
    public void HelpDoesNotAttemptConversion()
    {
        var result = Run(["--help"], [0xff]);
        Assert.That(result.Exit, Is.Zero);
        Assert.That(Encoding.UTF8.GetString(result.Output), Does.Contain("Usage:"));
        Assert.That(result.Error, Is.Empty);
    }

    [Test]
    public void CancellationReturns130WithNoOutput()
    {
        using var input = new MemoryStream();
        using var output = new MemoryStream();
        using var error = new StringWriter();
        Assert.That(CliApplication.Run(["-", "-"], input, output, error, new CancellationToken(true)), Is.EqualTo(130));
        Assert.That(output.Length, Is.Zero);
    }

    [Test]
    public void InterleavedStyleFontFlagsPreserveIndependentFallbackOrderAndMatchCore()
    {
        var latin = FontFixture.Create(latinOnly: true); var cjk = FontFixture.Create();
        File.WriteAllBytes(FilePath("latin.ttf"), latin); File.WriteAllBytes(FilePath("cjk.ttf"), cjk);
        const string html = "<p>A中<b>A中</b><i>A中</i><b><i>A中</i></b></p>";
        var args = new List<string> { "-", "-", "--subset-fonts" };
        foreach (var file in new[] { "latin.ttf", "cjk.ttf" })
            foreach (var flag in new[] { "--font-italic", "--font", "--font-bold-italic", "--font-bold" })
                args.AddRange([flag, FilePath(file)]);
        var result = Run(args.ToArray(), Encoding.UTF8.GetBytes(html));
        Assert.That(result.Exit, Is.Zero, result.Error);
        // Load distinct objects as the CLI does, even for identical paths across roles.
        PdfFont[] Chain() => [PdfFont.LoadTrueType(latin), PdfFont.LoadTrueType(cjk)];
        using var expected = new MemoryStream();
        PdfConverter.Convert(html, expected, new() { Fonts = Chain(), BoldFonts = Chain(),
            ItalicFonts = Chain(), BoldItalicFonts = Chain(), SubsetFonts = true });
        Assert.That(result.Output, Is.EqualTo(expected.ToArray()));
        args.AddRange(["--font", FilePath("latin.ttf")]);
        Assert.That(Run(args.ToArray()).Exit, Is.EqualTo(2));
    }

    [TestCase("--font-bold", "b")][TestCase("--font-italic", "i")]
    [TestCase("--font-bold-italic", "b><i")]
    public void EachRoleCanBeSuppliedAloneAndItsFileIsProtected(string flag, string tag)
    {
        var bytes = FontFixture.Create(); var path = FilePath("font.ttf"); File.WriteAllBytes(path, bytes);
        var html = tag == "b><i" ? "<p>ASCII <b><i>中文</i></b></p>" : $"<p>ASCII <{tag}>中文</{tag}></p>";
        Assert.That(Run(["-", "-", flag, path], Encoding.UTF8.GetBytes(html)).Exit, Is.Zero);
        var rejected = Run(["-", path, flag, path, "--overwrite"], Encoding.UTF8.GetBytes(html));
        Assert.That(rejected.Exit, Is.EqualTo(2));
        Assert.That(rejected.Output, Is.Empty);
        Assert.That(File.ReadAllBytes(path), Is.EqualTo(bytes));
        Assert.That(Run(["-", "-", flag]).Exit, Is.EqualTo(2));
        Assert.That(Run(["-", "-", flag, "--subset-fonts"]).Exit, Is.EqualTo(2));
        Assert.That(Run(["-", "-", flag, FilePath("missing.ttf")]).Exit, Is.EqualTo(4));
    }

    [TestCase("<p><i>A</i></p>", "FPDF1507", false)]
    [TestCase("<p><b>中</b></p>", "FPDF1504", false)]
    [TestCase("<p>ASCII</p>", "FPDF1501", true)]
    public void StyleFailuresPreserveFileAndStdoutAndRecover(string html, string code, bool corrupt)
    {
        var path = FilePath("bold.ttf");
        File.WriteAllBytes(path, corrupt ? [1, 2, 3] : FontFixture.Create(latinOnly: true));
        var existing = FilePath("existing.pdf"); File.WriteAllText(existing, "keep me");
        var input = Encoding.UTF8.GetBytes(html);
        foreach (var target in new[] { "-", existing, FilePath("new.pdf") })
        {
            var result = Run(["-", target, "--overwrite", "--font-bold", path], input);
            Assert.That(result.Exit, Is.EqualTo(3), result.Error);
            Assert.That(result.Error, Does.Contain(code)); Assert.That(result.Output, Is.Empty);
            Assert.That(File.ReadAllText(existing), Is.EqualTo("keep me"));
            Assert.That(File.Exists(FilePath("new.pdf")), Is.False);
            Assert.That(Directory.GetFiles(directory, ".factspdf-*.tmp"), Is.Empty);
        }
        File.WriteAllBytes(path, FontFixture.Create());
        var recovered = Run(["-", "-", "--font-bold", path], Encoding.UTF8.GetBytes("<p><b>中文</b></p>"));
        Assert.That(recovered.Exit, Is.Zero, recovered.Error);
        Assert.That(recovered.Output, Is.Not.Empty);
    }
}
