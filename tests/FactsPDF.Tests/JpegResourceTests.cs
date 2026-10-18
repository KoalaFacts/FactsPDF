using NUnit.Framework;
using System.Text.Json;

namespace FactsPDF.Tests;

public sealed class JpegResourceTests
{
    internal static byte[] Bytes(string name) => File.ReadAllBytes(Path.Combine(
        TestContext.CurrentContext.TestDirectory, "jpeg", name + ".jpg"));

    public static IEnumerable<TestCaseData> Corpus()
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            TestContext.CurrentContext.TestDirectory, "jpeg", "manifest.json")));
        foreach (var item in manifest.RootElement.GetProperty("cases").EnumerateArray())
            yield return new TestCaseData(item.GetProperty("file").GetString()![..^4],
                item.GetProperty("expected_load").GetString()!,
                item.TryGetProperty("width", out var width) ? width.GetInt32() : 0,
                item.TryGetProperty("height", out var height) ? height.GetInt32() : 0);
    }

    [TestCaseSource(nameof(Corpus))]
    public void ActualEncodedResourceMatchesItsAuthoredContract(string name, string expected, int width, int height)
    {
        var bytes = Bytes(name);
        if (expected != "accept")
        {
            Assert.That(Assert.Throws<FactsPdfException>(() => PdfImage.LoadImage(bytes))!.Code, Is.EqualTo(expected));
            return;
        }
        var image = PdfImage.LoadImage(bytes);
        Assert.Multiple(() =>
        {
            Assert.That(image.PixelWidth, Is.EqualTo(width));
            Assert.That(image.PixelHeight, Is.EqualTo(height));
            Assert.That(image.ByteLength, Is.EqualTo(bytes.Length));
            Assert.That(image.Data.ToArray(), Is.EqualTo(bytes));
        });
    }

    [TestCase("89504e470d0a1a0a")]
    [TestCase("474946383961")]
    [TestCase("524946460000000057454250")]
    [TestCase("ffd8ffe000104a46494600010100000100010000ffc2000b080008000801011100")]
    [TestCase("ff4fff51")]
    [TestCase("6e6f7420616e20696d616765")]
    public void UnsupportedContentHasExplicitDiagnostic(string hex)
        => Assert.That(Assert.Throws<FactsPdfException>(() => PdfImage.LoadImage(Convert.FromHexString(hex)))!.Code,
            Is.EqualTo("FPDF1602"));

    [TestCase("")] [TestCase("ff")] [TestCase("ffd8")]
    public void EmptyOrTruncatedJpegIsInvalid(string hex)
        => Assert.That(Assert.Throws<FactsPdfException>(() => PdfImage.LoadImage(Convert.FromHexString(hex)))!.Code,
            Is.EqualTo("FPDF1601"));

    [Test]
    public void LoadedResourceOwnsItsBytes()
    {
        var input = Bytes("rgb-420"); var expected = input.ToArray();
        var image = PdfImage.LoadImage(input); Array.Fill(input, (byte)0);
        Assert.That(image.Data.ToArray(), Is.EqualTo(expected));
    }

    [Test]
    public void PublicImageResourceLoadsContentThroughLoadImage()
    {
        // This first red test exercises the requested public entry without a
        // compile failure when the new resource class has not been introduced.
        var type = typeof(PdfConverter).Assembly.GetType("FactsPDF.PdfImage");
        Assert.That(type, Is.Not.Null, "The explicit immutable image resource is missing.");
        var loader = type!.GetMethods().SingleOrDefault(m => m.Name == "LoadImage"
            && m.GetParameters()[0].ParameterType == typeof(Stream));
        Assert.That(loader, Is.Not.Null, "LoadImage must accept explicit stream bytes.");
        using var input = File.OpenRead(Path.Combine(TestContext.CurrentContext.TestDirectory,
            "jpeg", "rgb-420.jpg"));
        var image = loader!.Invoke(null, [input, null, CancellationToken.None]);
        Assert.That(type.GetProperty("PixelWidth")!.GetValue(image), Is.EqualTo(320));
        Assert.That(type.GetProperty("PixelHeight")!.GetValue(image), Is.EqualTo(160));
    }
}
