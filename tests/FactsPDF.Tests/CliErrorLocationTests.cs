using System.Text;
using FactsPDF.CommandLine;
using NUnit.Framework;

namespace FactsPDF.Tests;

[TestFixture]
public sealed class CliErrorLocationTests
{
    private static (int Exit, string[] Lines) Run(string html, params string[] extra)
    {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(html));
        using var output = new MemoryStream();
        using var error = new StringWriter();
        var exit = CliApplication.Run(["-", "-", .. extra], input, output, error, CancellationToken.None);
        Assert.That(output.Length, Is.Zero, "A failed conversion writes no PDF bytes.");
        return (exit, error.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
    }

    private static FactsPdfException CoreError(string html)
        => Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, new MemoryStream()))!;

    [TestCase("<p style=\"display:flex\">x</p>", 1, 19)]
    [TestCase("<h1>Title</h1>\n<p>ok</p>\n  <p style=\"display:flex\">x</p>", 3, 21)]
    [TestCase("<h1>Title</h1>\r\n<p>ok</p>\r\n<p style=\"display:flex\">x</p>", 3, 19)]
    [TestCase("<p>a</p>\n<!-- 😀😀 --><p style=\"display:flex\">x</p>", 2, 30)]
    [TestCase("<h1>Title</h1>\r<p>ok</p>\r<p style=\"display:flex\">x</p>", 3, 19)]
    [TestCase("<h1>Title</h1>\r\n\n\r<p style=\"display:flex\">x</p>", 4, 19)]
    public void SourceDiagnosticsReportLineAndColumnOfTheCoreOffset(string html, int line, int column)
    {
        var core = CoreError(html);
        Assert.That(core.SourceOffset, Is.GreaterThanOrEqualTo(0));
        var (exit, lines) = Run(html);
        Assert.That(exit, Is.EqualTo(3));
        Assert.That(lines, Is.EqualTo(new[] { core.Message, $"Location: line {line}, column {column}." }));
    }

    [Test]
    public void LocationNeverEchoesInputText()
    {
        const string html = "<p>SECRET-CUSTOMER-NAME</p>\n<p style=\"display:flex\">SECRET-CUSTOMER-NAME</p>";
        var (_, lines) = Run(html);
        Assert.That(string.Join("\n", lines), Does.Not.Contain("SECRET"));
    }

    [Test]
    public void DiagnosticsWithoutSourceOffsetHaveNoLocationLine()
    {
        var path = Path.Combine(Path.GetTempPath(), $"factspdf-not-a-font-{Guid.NewGuid():N}.ttf");
        File.WriteAllBytes(path, [1, 2, 3, 4]);
        try
        {
            var (exit, lines) = Run("<p>x</p>", "--font", path);
            Assert.That(exit, Is.EqualTo(3));
            Assert.That(lines, Has.None.StartsWith("Location:"));
        }
        finally { File.Delete(path); }
    }
}
