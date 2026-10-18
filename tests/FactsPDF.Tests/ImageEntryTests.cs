using System.Text;
using FactsPDF.CommandLine;
using FactsPDF.WasmProbe;
using NUnit.Framework;

namespace FactsPDF.Tests;

public sealed class ImageEntryTests
{
    private string directory = null!;
    [SetUp] public void Setup() => directory = Directory.CreateTempSubdirectory("factspdf-image-").FullName;
    [TearDown] public void Cleanup() => Directory.Delete(directory, true);
    private string FilePath(string name) => Path.Combine(directory, name);
    private static (int Exit, byte[] Bytes, string Error) Cli(string[] args, string html = "<img src='chart'>", CancellationToken cancellation = default)
    {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(html)); using var output = new MemoryStream(); using var error = new StringWriter();
        var exit = CliApplication.Run(args, input, output, error, cancellation);
        return (exit, output.ToArray(), error.ToString());
    }
    private static ProbeResult Wasm(byte[] bytes, int[] lengths, string[] names, string html = "<img src='chart'>")
    {
        var method = typeof(ProbeConversion).GetMethod("RunImages", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.That(method, Is.Not.Null, "Private explicit image byte protocol is required.");
        return (ProbeResult)method!.Invoke(null, [html, Array.Empty<byte>(), Array.Empty<int>(), Array.Empty<int>(),
            bytes, lengths, names, true])!;
    }

    [Test]
    public void ExplicitImagePathWithSpacesAndEqualsProducesIdenticalFileStdoutAndCore()
    {
        var path = FilePath("some = bytes.png"); var bytes = JpegResourceTests.Bytes("rgb-420"); File.WriteAllBytes(path, bytes);
        var result = Cli(["-", "-", "--image", "chart=" + path]);
        Assert.That(result.Exit, Is.Zero, result.Error);
        using var expected = new MemoryStream(); PdfConverter.Convert("<img src='chart'>", expected,
            new() { Images = new Dictionary<string, PdfImage> { ["chart"] = PdfImage.LoadImage(bytes) } });
        Assert.That(result.Bytes, Is.EqualTo(expected.ToArray()));
        var output = FilePath("output.pdf"); Assert.That(Cli(["-", output, "--image", "chart=" + path]).Exit, Is.Zero);
        Assert.That(File.ReadAllBytes(output), Is.EqualTo(result.Bytes));
        Assert.That(Cli(["-", "-", "--image", "chart=" + path, "--image", "Chart=" + path]).Exit, Is.Zero);
    }

    [TestCase("chart")][TestCase("=file")][TestCase("chart=")][TestCase("chart=-")]
    [TestCase("../chart=file")][TestCase("中文=file")][TestCase("chart=--missing")]
    public void InvalidImageArgumentsAreParameterErrors(string argument)
    {
        var result = Cli(["-", "-", "--image", argument]);
        Assert.That(result.Exit, Is.EqualTo(2)); Assert.That(result.Bytes, Is.Empty);
    }

    [Test]
    public void DuplicateAndCountLimitsApplyBeforeReadingFiles()
    {
        Assert.That(Cli(["-", "-", "--image", "chart=missing", "--image", "chart=missing"]).Exit, Is.EqualTo(2));
        var args = new[] { "-", "-" }.Concat(Enumerable.Range(0, 17).SelectMany(i => new[] { "--image", "image" + i + "=missing" })).ToArray();
        Assert.That(Cli(args).Exit, Is.EqualTo(2));
        Assert.That(Cli(["-", "-", "--image"]).Exit, Is.EqualTo(2));
    }

    [TestCase("progressive", "FPDF1602")][TestCase("missing-eoi", "FPDF1601")]
    public void BadImagesPreserveOldOrAbsentTargetsAndStdout(string fixture, string code)
    {
        var path = FilePath("bad.jpg"); File.WriteAllBytes(path, JpegResourceTests.Bytes(fixture));
        foreach (var exists in new[] { false, true })
        {
            var target = FilePath("output.pdf"); if (exists) File.WriteAllText(target, "keep");
            var result = Cli(["-", target, "--overwrite", "--image", "chart=" + path]);
            Assert.That(result.Exit, Is.EqualTo(3)); Assert.That(result.Error, Does.Contain(code)); Assert.That(result.Bytes, Is.Empty);
            Assert.That(File.Exists(target), Is.EqualTo(exists)); if (exists) Assert.That(File.ReadAllText(target), Is.EqualTo("keep"));
        }
        Assert.That(Cli(["-", "-", "--image", "chart=" + path]).Bytes, Is.Empty);
        Assert.That(Directory.GetFiles(directory, ".factspdf-*.tmp"), Is.Empty);
    }

    [Test]
    public void InputImageAliasMissingFileOversizeAndCancellationKeepOutputUntouched()
    {
        var path = FilePath("image.jpg"); var bytes = JpegResourceTests.Bytes("rgb-420"); File.WriteAllBytes(path, bytes);
        Assert.That(Cli(["-", path, "--overwrite", "--image", "chart=" + path]).Exit, Is.EqualTo(2));
        Assert.That(File.ReadAllBytes(path), Is.EqualTo(bytes));
        Assert.That(Cli(["-", "-", "--image", "chart=" + FilePath("missing")]).Exit, Is.EqualTo(4));
        Assert.That(Cli(["-", path, "--overwrite", "--image", "chart=" + path], cancellation: new(true)).Exit, Is.EqualTo(130));
        using (var file = File.Create(path)) { file.Write(bytes); file.SetLength(8_388_609); }
        var large = Cli(["-", "-", "--image", "chart=" + path]);
        Assert.That(large.Exit, Is.EqualTo(3)); Assert.That(large.Error, Does.Contain("FPDF1603")); Assert.That(large.Bytes, Is.Empty);
    }

    [Test]
    public void WasmBytesMatchCoreAndRecoverAfterFailure()
    {
        var bytes = JpegResourceTests.Bytes("rgb-420"); var good = Wasm(bytes, [bytes.Length], ["chart"]);
        Assert.That(good.ErrorCode, Is.Null);
        using var expected = new MemoryStream(); PdfConverter.Convert("<img src='chart'>", expected,
            new() { Images = new Dictionary<string, PdfImage> { ["chart"] = PdfImage.LoadImage(bytes) }, SubsetFonts = true });
        Assert.That(Convert.FromBase64String(good.PdfBase64!), Is.EqualTo(expected.ToArray()));
        var bad = Wasm([1, 2, 3], [3], ["chart"]);
        Assert.That(bad.ErrorCode, Is.EqualTo("FPDF1602")); Assert.That(bad.PdfBase64, Is.Null);
        Assert.That(Wasm(bytes, [bytes.Length], ["chart"]).PdfBase64, Is.EqualTo(good.PdfBase64));
    }

    [Test]
    public void WasmShapeLimitsNamesAndLengthSumAreValidatedBeforeCopies()
    {
        foreach (var (lengths, names) in new[] { (new[] { 1 }, Array.Empty<string>()), (new[] { 0 }, new[] { "a" }),
            (new[] { 1 }, new[] { "" }), (new[] { 1 }, new[] { "../a" }), (new[] { 1 }, new[] { "中" }),
            (new[] { 1, 1 }, new[] { "a", "a" }), (new[] { int.MaxValue, int.MaxValue }, new[] { "a", "b" }),
            (Enumerable.Repeat(1, 17).ToArray(), Enumerable.Range(0, 17).Select(i => "i" + i).ToArray()) })
        {
            var result = Wasm([1, 2], lengths, names);
            Assert.That(result.ErrorCode, Is.EqualTo("B2_INPUT_IMAGES")); Assert.That(result.PdfBase64, Is.Null);
        }
        var over = new byte[8_388_609];
        Assert.That(Wasm(over, [over.Length], ["chart"]).ErrorCode, Is.EqualTo("FPDF1603"));
        var aggregate = new byte[33_554_433];
        Assert.That(Wasm(aggregate, [8_388_608, 8_388_608, 8_388_608, 8_388_608, 1], ["a", "b", "c", "d", "e"]).ErrorCode, Is.EqualTo("FPDF1603"));
    }

    [Test]
    public void CliCountsEveryUnusedAliasAgainstActualTotalCompressedBytes()
    {
        // 65,536 blocks, each DC0 +63 AC category10 values: 694 zero bits.
        // Exact byte alignment, no EOB needed, no decoder/library-generated payload.
        var bytes = JpegFixture.Gray(new byte[65_536 * 694 / 8], width: 2048, height: 2048, acValues: [10]);
        var path = FilePath("large-valid.jpg"); File.WriteAllBytes(path, bytes);
        Assert.That(bytes.Length * 5L, Is.LessThan(33_554_432)); Assert.That(bytes.Length * 6L, Is.GreaterThan(33_554_432));
        var five = new[] { "-", "-" }.Concat(Enumerable.Range(0, 5).SelectMany(i => new[] { "--image", $"i{i}={path}" })).ToArray();
        var good = Cli(five, "<p>ASCII</p>"); Assert.That(good.Exit, Is.Zero, good.Error);
        var bad = Cli([.. five, "--image", "sixth=" + path], "<p>ASCII</p>");
        Assert.That(bad.Exit, Is.EqualTo(3)); Assert.That(bad.Error, Does.Contain("FPDF1603")); Assert.That(bad.Bytes, Is.Empty);
    }
}
