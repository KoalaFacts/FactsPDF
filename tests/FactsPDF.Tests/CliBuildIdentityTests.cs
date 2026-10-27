using System.Reflection;
using System.Text;
using FactsPDF.CommandLine;
using NUnit.Framework;

namespace FactsPDF.Tests;

[TestFixture]
public sealed class CliBuildIdentityTests
{
    private static (int Exit, string Output, string Error, long InputPosition) Run(
        string[] arguments, CancellationToken cancellation = default)
    {
        using var input = new MemoryStream([0xff, 0xfe, 0xff]);
        using var output = new MemoryStream();
        using var error = new StringWriter();
        var code = CliApplication.Run(arguments, input, output, error, cancellation);
        return (code, Encoding.UTF8.GetString(output.ToArray()), error.ToString(), input.Position);
    }

    [Test]
    public void VersionReportsTheExactBuiltCliInformationalVersionWithoutInputAccess()
    {
        var declared = typeof(CliApplication).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        Assert.That(declared, Is.Not.Null.And.Not.Empty);
        Assert.That(declared, Does.StartWith("0.1.0-preview.1"));

        var actual = Run(["--version"]);
        Assert.Multiple(() =>
        {
            Assert.That(actual.Exit, Is.Zero);
            Assert.That(actual.Output, Is.EqualTo($"FactsPDF.Cli {declared}\n"));
            Assert.That(actual.Error, Is.Empty);
            Assert.That(actual.InputPosition, Is.Zero, "Version lookup must not read HTML from stdin.");
        });
    }

    [Test]
    public void VersionIsAStandaloneOperationAndCannotBeMixedWithConversionArguments()
    {
        using var directory = new TemporaryDirectory();
        var target = Path.Combine(directory.Path, "existing.pdf");
        File.WriteAllText(target, "untouched PDF", Encoding.UTF8);

        foreach (var arguments in new[]
        {
            new[] { "--version", "-", "-" },
            new[] { "--version", target },
            new[] { "--version", "--overwrite" }
        })
        {
            var actual = Run(arguments);
            Assert.That(actual.Exit, Is.EqualTo(2));
            Assert.That(actual.Output, Is.Empty);
            Assert.That(actual.Error, Does.Contain("Usage:"));
            Assert.That(actual.InputPosition, Is.Zero);
            Assert.That(File.ReadAllText(target, Encoding.UTF8), Is.EqualTo("untouched PDF"));
        }
    }

    [Test]
    public void PreCancelledVersionDoesNotPrintAnApparentlyValidBuild()
    {
        var actual = Run(["--version"], new CancellationToken(canceled: true));
        Assert.That(actual.Exit, Is.EqualTo(130));
        Assert.That(actual.Output, Is.Empty);
        Assert.That(actual.Error, Is.EqualTo("Conversion cancelled." + Environment.NewLine));
        Assert.That(actual.InputPosition, Is.Zero);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory("factspdf-build-id-").FullName;
        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
