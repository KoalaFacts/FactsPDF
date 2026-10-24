using System.Text;
using FactsPDF.CommandLine;
using NUnit.Framework;

namespace FactsPDF.Tests;

[TestFixture]
public sealed class PageFurnitureJsonTests
{
    private string directory = null!;
    private const string Body = "<p>BODY ONE</p><p style=\"break-before:page\">BODY TWO</p>";
    [SetUp] public void SetUp() => directory = Directory.CreateTempSubdirectory("factspdf-furniture-").FullName;
    [TearDown] public void TearDown() => Directory.Delete(directory, true);
    private string PathFor(string name) => Path.Combine(directory, name);
    private string Config(byte[] bytes)
    {
        var path = PathFor("furniture.json"); File.WriteAllBytes(path, bytes); return path;
    }
    private string Config(string json) => Config(Encoding.UTF8.GetBytes(json));
    private static (int Exit, byte[] Pdf, string Error) Run(string[] args, string html = Body,
        CancellationToken cancellation = default)
    {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(html));
        using var output = new MemoryStream(); using var error = new StringWriter();
        var exit = CliApplication.Run(args, input, output, error, cancellation);
        return (exit, output.ToArray(), error.ToString());
    }
    private static byte[] Expected(PdfOptions options, string html = Body)
    {
        using var output = new MemoryStream(); PdfConverter.Convert(html, output, options); return output.ToArray();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void CliConfigurationMatchesIndependentlyConstructedApi(bool subset)
    {
        const string json = """{"default":{"headerLeft":{"template":"HL {page}","fontSize":9,"color":"#1234aB"},"headerCenter":{"template":"HC {pages}"},"headerRight":{"template":"{{key}}"},"footerLeft":{"template":"FL"},"footerCenter":{"template":"Page {page} of {pages}"},"footerRight":{"template":"FR"}},"firstPage":{"headerLeft":{"template":"Cover"},"footerRight":{"template":"{page}/{pages}"}}}""";
        var furniture = new PdfPageFurniture
        {
            Default = new()
            {
                HeaderLeft = new() { Template = "HL {page}", FontSize = 9, Color = new(0x12, 0x34, 0xab) },
                HeaderCenter = new() { Template = "HC {pages}" },
                HeaderRight = new() { Template = "{{key}}" },
                FooterLeft = new() { Template = "FL" },
                FooterCenter = new() { Template = "Page {page} of {pages}" },
                FooterRight = new() { Template = "FR" }
            },
            FirstPage = new() { HeaderLeft = new() { Template = "Cover" }, FooterRight = new() { Template = "{page}/{pages}" } }
        };
        var args = new List<string> { "-", "-", "--page-furniture", Config(json) };
        if (subset) args.Add("--subset-fonts");
        var result = Run(args.ToArray());
        Assert.That(result.Exit, Is.Zero, result.Error);
        Assert.That(result.Pdf, Is.EqualTo(Expected(new() { PageFurniture = furniture, SubsetFonts = subset })));
        Assert.That(result.Error, Does.Contain("2 page(s)"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void CliExplicitFurnitureStylesMatchTypedApi(bool subset)
    {
        var bytes = FontFixture.Create(); var fontPath = PathFor("font.ttf"); File.WriteAllBytes(fontPath, bytes);
        const string json = """{"default":{"headerLeft":{"template":"A","bold":true,"fontSize":9,"color":"#FF0000"},"headerCenter":{"template":"A","italic":true,"color":"#00ff00"},"headerRight":{"template":"A","bold":true,"italic":true,"color":"#0000FF"}}}""";
        var args = new List<string> { "-", "-", "--page-furniture", Config(json) };
        foreach (var flag in new[] { "--font", "--font-bold", "--font-italic", "--font-bold-italic" }) args.AddRange([flag, fontPath]);
        if (subset) args.Add("--subset-fonts");
        const string html = "<p>A</p><p style=\"break-before:page\">A</p>";
        var result = Run(args.ToArray(), html);
        Assert.That(result.Exit, Is.Zero, result.Error);
        PdfFont[] Chain() => [PdfFont.LoadTrueType(bytes)];
        Assert.That(result.Pdf, Is.EqualTo(Expected(new()
        {
            Fonts = Chain(),
            BoldFonts = Chain(),
            ItalicFonts = Chain(),
            BoldItalicFonts = Chain(),
            SubsetFonts = subset,
            PageFurniture = new()
            {
                Default = new()
                {
                    HeaderLeft = new() { Template = "A", Bold = true, FontSize = 9, Color = new(255, 0, 0) },
                    HeaderCenter = new() { Template = "A", Italic = true, Color = new(0, 255, 0) },
                    HeaderRight = new() { Template = "A", Bold = true, Italic = true, Color = new(0, 0, 255) }
                }
            }
        }, html)));
    }

    [TestCase("{}")]
    [TestCase("{\"default\":{}}")]
    [TestCase("{\"default\":{},\"firstPage\":null}")]
    [TestCase("{\"default\":{\"headerLeft\":null,\"footerRight\":{}}}")]
    public void OmittedAndEmptyConfigurationPreservesDefaultBytes(string json)
    {
        var result = Run(["-", "-", "--page-furniture", Config(json)]);
        Assert.That(result.Exit, Is.Zero, result.Error);
        Assert.That(result.Pdf, Is.EqualTo(Expected(new())));
    }

    [TestCase("null", false)]
    [TestCase("{}", true)]
    public void FirstPageNullInheritsButEmptyObjectSuppresses(string firstPage, bool suppress)
    {
        var result = Run(["-", "-", "--page-furniture", Config("{\"default\":{\"headerLeft\":{\"template\":\"Page {page}\"}},\"firstPage\":" + firstPage + "}")]);
        Assert.That(result.Exit, Is.Zero, result.Error);
        Assert.That(result.Pdf, Is.EqualTo(Expected(new()
        {
            PageFurniture = new()
            {
                Default = new() { HeaderLeft = new() { Template = "Page {page}" } },
                FirstPage = suppress ? new PdfPageFurnitureRegions() : null
            }
        })));
    }

    [TestCase("null")]
    [TestCase("[]")]
    [TestCase("1")]
    [TestCase("")]
    [TestCase("{\"default\":null}")]
    [TestCase("{\"Default\":{}}")]
    [TestCase("{\"default\":{},\"default\":{}}")]
    [TestCase("{\"default\":{},\"\\u0064efault\":{}}")]
    [TestCase("{\"firstPage\":{},\"firstPage\":null}")]
    [TestCase("{\"default\":{\"headerLeft\":{},\"headerLeft\":null}}")]
    [TestCase("{\"default\":{\"headerLeft\":{\"template\":\"A\",\"\\u0074emplate\":\"B\"}}}")]
    [TestCase("{\"default\":{\"headerLeft\":{\"unknown\":1}}}")]
    [TestCase("{\"font\":\"/must/not/load.ttf\"}")]
    [TestCase("{\"default\":{\"headerLeft\":\"A\"}}")]
    [TestCase("{\"default\":{\"headerLeft\":{\"template\":null}}}")]
    [TestCase("{\"default\":{\"headerLeft\":{\"bold\":1}}}")]
    [TestCase("{\"default\":{\"headerLeft\":{\"italic\":\"true\"}}}")]
    [TestCase("{\"default\":{\"headerLeft\":{\"fontSize\":\"10\"}}}")]
    [TestCase("{\"default\":{\"headerLeft\":{\"fontSize\":1e9999}}}")]
    [TestCase("{\"default\":{\"headerLeft\":{\"fontSize\":0.5}}}")]
    [TestCase("{\"default\":{\"headerLeft\":{\"fontSize\":145}}}")]
    [TestCase("{\"default\":{\"headerLeft\":{\"color\":\"#abc\"}}}")]
    [TestCase("{\"default\":{\"headerLeft\":{\"color\":\"#11223344\"}}}")]
    [TestCase("{\"default\":{\"headerLeft\":{\"color\":\"#gg1122\"}}}")]
    [TestCase("{\"default\":{\"headerLeft\":{\"color\":null}}}")]
    [TestCase("{/*comment*/}")]
    [TestCase("{\"default\":{},}")]
    [TestCase("{} {}")]
    [TestCase("{\"default\":{\"headerLeft\":{\"template\":\"\\uD800\"}}}")]
    [TestCase("{\"default\":{\"headerLeft\":{\"template\":\"\\uDC00\"}}}")]
    [TestCase("{\"default\":{\"headerLeft\":{\"template\":\"\\uD800\\u0041\"}}}")]
    [TestCase("{\"default\":{\"headerLeft\":{\"template\":\"\\uD800\\n\\uDC00\"}}}")]
    public void InvalidConfigurationIsAnArgumentErrorWithoutPdf(string json)
    {
        var result = Run(["-", "-", "--page-furniture", Config(json)]);
        Assert.That(result.Exit, Is.EqualTo(2), result.Error);
        Assert.That(result.Pdf, Is.Empty);
        Assert.That(result.Error, Does.Contain("PageFurniture"), "Must be a configuration diagnostic, not an unrecognised CLI flag.");
    }

    [Test]
    public void JsonUtf8BomAndExactByteBounds()
    {
        foreach (var bom in new[] { Array.Empty<byte>(), new byte[] { 0xef, 0xbb, 0xbf } })
        {
            var bytes = bom.Concat(Encoding.UTF8.GetBytes("{}" + new string(' ', 65_536 - bom.Length - 2))).ToArray();
            var result = Run(["-", "-", "--page-furniture", Config(bytes)]);
            Assert.That(result.Exit, Is.Zero, result.Error);
            Assert.That(result.Pdf, Is.EqualTo(Expected(new())));
            result = Run(["-", "-", "--page-furniture", Config([.. bytes, (byte)' '])]);
            Assert.That(result.Exit, Is.EqualTo(2), result.Error); Assert.That(result.Pdf, Is.Empty);
            Assert.That(result.Error, Does.Contain("65536"));
        }
        foreach (var bytes in new byte[][] { [0xff], [0xc0, 0xaf], [0xed, 0xa0, 0x80],
            [0xef, 0xbb, 0xbf, 0xef, 0xbb, 0xbf, (byte)'{', (byte)'}'] })
        {
            var result = Run(["-", "-", "--page-furniture", Config(bytes)]);
            Assert.That(result.Exit, Is.EqualTo(2), result.Error); Assert.That(result.Pdf, Is.Empty);
            Assert.That(result.Error, Does.Contain("PageFurniture"));
        }
    }

    [Test]
    public void CliConfigurationOptionAndDestinationConflicts()
    {
        var path = Config("{}");
        foreach (var tail in new string[][] { ["--page-furniture"], ["--page-furniture", ""],
            ["--page-furniture", "-"], ["--page-furniture", "--subset-fonts"],
            ["--page-furniture", path, "--page-furniture", path] })
            Assert.That(Run(["-", "-", .. tail]).Exit, Is.EqualTo(2));
        var result = Run(["-", Path.Combine(directory, ".", "furniture.json"), "--page-furniture", path, "--overwrite"]);
        Assert.That(result.Exit, Is.EqualTo(2)); Assert.That(result.Pdf, Is.Empty);
        Assert.That(File.ReadAllText(path), Is.EqualTo("{}"));
        Assert.That(result.Error, Does.Contain("Output must be different"));
        Assert.That(Run(["-", "-", "--page-furniture", PathFor("missing.json")]).Exit, Is.EqualTo(4));
        Assert.That(Run(["-", "-", "--page-furniture", directory]).Exit, Is.EqualTo(4));
    }

    [Test]
    public void CliMixedLegacyModeFailsEvenForEmptyLiteral()
    {
        var path = Config("{}");
        foreach (var tail in new string[][] { ["--page-numbers"], ["--header-text", ""], ["--footer-text", ""] })
        {
            var result = Run(["-", "-", "--page-furniture", path, .. tail]);
            Assert.That(result.Exit, Is.EqualTo(2)); Assert.That(result.Pdf, Is.Empty);
            Assert.That(result.Error, Does.Contain("PageFurniture"));
        }
    }

    [TestCase("{nope}", "FPDF1306")]
    [TestCase("line\\nnext", "FPDF1305")]
    public void CliLateFurnitureFailurePreservesExistingFileAndRecovers(string template, string code)
    {
        var path = Config("{\"default\":{\"headerLeft\":{\"template\":\"" + template + "\"}}}");
        var existing = PathFor("existing.pdf"); File.WriteAllText(existing, "unchanged sentinel");
        foreach (var target in new[] { "-", existing, PathFor("new.pdf") })
        {
            var result = Run(["-", target, "--page-furniture", path, "--overwrite"]);
            Assert.That(result.Exit, Is.EqualTo(3), result.Error); Assert.That(result.Error, Does.Contain(code));
            Assert.That(result.Pdf, Is.Empty); Assert.That(File.ReadAllText(existing), Is.EqualTo("unchanged sentinel"));
            Assert.That(File.Exists(PathFor("new.pdf")), Is.False);
            Assert.That(Directory.GetFiles(directory, ".factspdf-*.tmp"), Is.Empty);
        }
        var recovered = Run(["-", "-", "--page-furniture", Config("{}")]);
        Assert.That(recovered.Exit, Is.Zero, recovered.Error);
        Assert.That(recovered.Pdf, Is.EqualTo(Expected(new())));
    }

    [Test]
    public void TemplateLimitBelongsToCoreAndCancellationRetainsItsExitCode()
    {
        var path = Config("{\"default\":{\"headerLeft\":{\"template\":\"" + new string(' ', 1025) + "\"}}}");
        var result = Run(["-", "-", "--page-furniture", path]);
        Assert.That(result.Exit, Is.EqualTo(3), result.Error); Assert.That(result.Error, Does.Contain("FPDF1001"));
        Assert.That(result.Pdf, Is.Empty);
        result = Run(["-", "-", "--page-furniture", path], cancellation: new CancellationToken(true));
        Assert.That(result.Exit, Is.EqualTo(130)); Assert.That(result.Pdf, Is.Empty);
        var help = Run(["--help"]);
        Assert.That(help.Exit, Is.Zero); Assert.That(Encoding.UTF8.GetString(help.Pdf), Does.Contain("--page-furniture"));
    }
}
