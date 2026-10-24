using System.Text;
using FactsPDF.CommandLine;
using FactsPDF.Configuration;
using NUnit.Framework;

namespace FactsPDF.Tests;

[TestFixture]
public sealed class PageFurnitureColorRegressionTests
{
    [TestCase("#12345\\u0000")]
    [TestCase("#1234\\u0000\\u0000")]
    [TestCase("#1\\u0000\\u0000\\u0000\\u0000\\u0000")]
    [TestCase("#\\u000012345")]
    [TestCase("#12345 ")]
    [TestCase("#12345g")]
    [TestCase("#１２３４５６")]
    [TestCase("#12345\\t")]
    public void InvalidSixCharacterColorCannotBeRepairedByNumericParsing(string color)
    {
        var json = Encoding.UTF8.GetBytes("{\"default\":{\"headerLeft\":{\"template\":\"A\",\"color\":\"" + color + "\"}}}");
        var error = Assert.Throws<ArgumentException>(() => PageFurnitureJson.Parse(json));
        Assert.That(error!.Message, Does.Contain("PageFurniture"));
        Assert.That(error.Message, Does.Contain("#RRGGBB"));
    }

    [TestCase("#000000", 0, 0, 0)]
    [TestCase("#FFFFFF", 255, 255, 255)]
    [TestCase("#1aB2cD", 26, 178, 205)]
    public void ExactAsciiHexColorsRetainTheirChannels(string color, int red, int green, int blue)
    {
        var json = Encoding.UTF8.GetBytes("{\"default\":{\"headerLeft\":{\"color\":\"" + color + "\"}}}");
        Assert.That(PageFurnitureJson.Parse(json).Default.HeaderLeft!.Color,
            Is.EqualTo(new PdfRgbColor((byte)red, (byte)green, (byte)blue)));
    }

    [Test]
    public void RejectedColorPreservesStdoutExistingAndAbsentFilesThenRecovers()
    {
        var directory = Directory.CreateTempSubdirectory("factspdf-color-").FullName;
        try
        {
            var config = Path.Combine(directory, "config.json");
            var existing = Path.Combine(directory, "existing.pdf");
            var absent = Path.Combine(directory, "absent.pdf");
            File.WriteAllText(existing, "sentinel");
            File.WriteAllText(config, "{\"default\":{\"headerLeft\":{\"template\":\"A\",\"color\":\"#12345\\u0000\"}}}");
            foreach (var target in new[] { "-", existing, absent })
            {
                using var input = new MemoryStream("<p>A</p>"u8.ToArray());
                using var output = new MemoryStream();
                using var error = new StringWriter();
                Assert.That(CliApplication.Run(["-", target, "--overwrite", "--page-furniture", config], input, output, error),
                    Is.EqualTo(2), error.ToString());
                Assert.That(output.ToArray(), Is.Empty);
                Assert.That(File.ReadAllText(existing), Is.EqualTo("sentinel"));
                Assert.That(File.Exists(absent), Is.False);
                Assert.That(Directory.GetFiles(directory, ".factspdf-*.tmp"), Is.Empty);
            }
            File.WriteAllText(config, "{}");
            using var recoveredInput = new MemoryStream("<p>A</p>"u8.ToArray());
            using var recoveredOutput = new MemoryStream();
            using var recoveredError = new StringWriter();
            Assert.That(CliApplication.Run(["-", "-", "--page-furniture", config], recoveredInput, recoveredOutput, recoveredError),
                Is.Zero, recoveredError.ToString());
            using var expected = new MemoryStream();
            PdfConverter.Convert("<p>A</p>", expected);
            Assert.That(recoveredOutput.ToArray(), Is.EqualTo(expected.ToArray()));
        }
        finally { Directory.Delete(directory, true); }
    }
}
