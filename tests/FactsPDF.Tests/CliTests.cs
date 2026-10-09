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
}
