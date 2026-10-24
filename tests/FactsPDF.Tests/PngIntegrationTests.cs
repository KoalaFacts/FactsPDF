using FactsPDF.CommandLine;
using FactsPDF.WasmProbe;
using NUnit.Framework;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace FactsPDF.Tests;

public sealed class PngIntegrationTests
{
    private static byte[] Bytes() => PngFixture.Image([0, 10, 20, 30, 0, 40, 50, 60, 128]);
    [TestCase(false)][TestCase(true)]
    public void MixedOpaqueTransparentJpegAndAliasesHaveUniqueCorrectObjectsAndXref(bool unicode)
    {
        var transparent = PdfImage.LoadImage(Bytes()); var jpeg = PdfImage.LoadImage(JpegResourceTests.Bytes("gray"));
        var opaque = PdfImage.LoadImage(PngFixture.Image([0, 80, 100], type: 0));
        using var output = new MemoryStream();
        PdfConverter.Convert("<p>" + (unicode ? "中文" : "A") + "</p><img src='t'><img src='j'><img src='t2'><img src='o'>", output,
            new() { Images = new Dictionary<string, PdfImage> { ["t"] = transparent, ["t2"] = transparent, ["j"] = jpeg, ["o"] = opaque },
                Fonts = unicode ? [PdfFont.LoadTrueType(FontFixture.Create())] : [] });
        var pdf = output.ToArray(); var text = Encoding.Latin1.GetString(pdf); var objects = PngFixture.ImageObjects(pdf);
        Assert.That(objects, Has.Count.EqualTo(4)); Assert.That(objects.Count(o => o.Dictionary.Contains("/SMask", StringComparison.Ordinal)), Is.EqualTo(1));
        var main = objects.Single(o => o.Dictionary.Contains("/SMask", StringComparison.Ordinal));
        var maskId = int.Parse(Regex.Match(main.Dictionary, @"/SMask (\d+) 0 R").Groups[1].Value, CultureInfo.InvariantCulture);
        Assert.That(objects.Single(o => o.Id == maskId).Pixels, Is.EqualTo(new byte[] { 0, 128 }));
        Assert.That(objects.Single(o => o.Dictionary.Contains("/DCTDecode", StringComparison.Ordinal)).Pixels, Is.EqualTo(JpegResourceTests.Bytes("gray")));
        Assert.That(Regex.Matches(text, @"/Im\d+ Do").Count, Is.EqualTo(4));
        var xref = int.Parse(Regex.Match(text, @"startxref\n(\d+)").Groups[1].Value, CultureInfo.InvariantCulture);
        Assert.That(text[xref..], Does.StartWith("xref\n"));
        var table = text[xref..].Split('\n'); var count = int.Parse(table[1].Split(' ')[1], CultureInfo.InvariantCulture);
        for (var id = 1; id < count; id++)
        {
            var offset = int.Parse(table[id + 2][..10], CultureInfo.InvariantCulture);
            Assert.That(text[offset..], Does.StartWith($"{id} 0 obj\n"));
        }
        if (unicode) Assert.That(text, Does.Contain("<4E2D>").And.Contain("<6587>"));
        using var preserved = new MemoryStream(); preserved.Write([9, 8, 7]); preserved.Position = 1;
        Assert.That(Assert.Throws<FactsPdfException>(() => PdfConverter.Convert("<img src='t'>", preserved,
            new() { Images = new Dictionary<string, PdfImage> { ["t"] = transparent }, MaxOutputBytes = 500 }))!.Code, Is.EqualTo("FPDF1401"));
        Assert.That(preserved.ToArray(), Is.EqualTo(new byte[] { 9, 8, 7 })); Assert.That(preserved.Position, Is.EqualTo(1));
    }
    [Test]
    public void ApiCliAndPrivateByteAdapterProduceIdenticalPngPdfAndRecoverAfterFailure()
    {
        var bytes = Bytes(); var expected = PngFixture.Pdf(PdfImage.LoadImage(bytes), new() { SubsetFonts = true });
        var directory = Directory.CreateTempSubdirectory("factspdf-png-");
        try
        {
            var imagePath = Path.Combine(directory.FullName, "logo = data.jpg"); File.WriteAllBytes(imagePath, bytes);
            using var input = new MemoryStream(Encoding.UTF8.GetBytes("<img src='logo'>")); using var output = new MemoryStream(); using var error = new StringWriter();
            Assert.That(CliApplication.Run(["-", "-", "--image", "logo=" + imagePath], input, output, error), Is.Zero, error.ToString());
            Assert.That(output.ToArray(), Is.EqualTo(expected));
            var method = typeof(ProbeConversion).GetMethod("RunImages", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
            ProbeResult Run(byte[] data) => (ProbeResult)method.Invoke(null, ["<img src='logo'>", Array.Empty<byte>(), Array.Empty<int>(), Array.Empty<int>(), data, new[] { data.Length }, new[] { "logo" }, true])!;
            var good = Run(bytes); Assert.That(good.ErrorCode, Is.Null); Assert.That(Convert.FromBase64String(good.PdfBase64!), Is.EqualTo(expected));
            Assert.That(Run(PngFixture.Signature).ErrorCode, Is.EqualTo("FPDF1601")); Assert.That(Run(bytes).PdfBase64, Is.EqualTo(good.PdfBase64));
            var target = Path.Combine(directory.FullName, "keep.pdf"); File.WriteAllText(target, "keep"); File.WriteAllBytes(imagePath, PngFixture.Signature);
            using var badInput = new MemoryStream(Encoding.UTF8.GetBytes("<img src='logo'>")); using var badOutput = new MemoryStream(); using var badError = new StringWriter();
            Assert.That(CliApplication.Run(["-", target, "--overwrite", "--image", "logo=" + imagePath], badInput, badOutput, badError), Is.EqualTo(3));
            Assert.That(badError.ToString(), Does.Contain("FPDF1601")); Assert.That(File.ReadAllText(target), Is.EqualTo("keep")); Assert.That(badOutput.Length, Is.Zero);
        }
        finally { directory.Delete(true); }
    }
}
