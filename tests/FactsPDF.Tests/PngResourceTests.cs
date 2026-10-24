using NUnit.Framework;

namespace FactsPDF.Tests;

public sealed class PngResourceTests
{
    public static IEnumerable<TestCaseData> Formats()
    {
        yield return new TestCaseData(3, (byte)1, (byte)0, new byte[] { 0, 0xa0 }, new byte[] { 255, 0, 255 });
        yield return new TestCaseData(3, (byte)2, (byte)0, new byte[] { 0, 0x1c }, new byte[] { 0, 85, 255 });
        yield return new TestCaseData(3, (byte)4, (byte)0, new byte[] { 0, 0x08, 0xf0 }, new byte[] { 0, 136, 255 });
        yield return new TestCaseData(3, (byte)8, (byte)0, new byte[] { 0, 0, 128, 255 }, new byte[] { 0, 128, 255 });
        yield return new TestCaseData(2, (byte)8, (byte)2, new byte[] { 0, 255, 0, 0, 0, 128, 255 }, new byte[] { 255, 0, 0, 0, 128, 255 });
    }
    [TestCaseSource(nameof(Formats))]
    public void SupportedGrayAndRgbExpandToLiteralEightBitPixels(int width, byte depth, byte type, byte[] rows, byte[] expected)
    {
        var bytes = PngFixture.Image(rows, width, depth: depth, type: type);
        var image = PdfImage.LoadImage(bytes); var objects = PngFixture.ImageObjects(PngFixture.Pdf(image));
        Assert.That(image.PixelWidth, Is.EqualTo(width)); Assert.That(image.PixelHeight, Is.EqualTo(1));
        Assert.That(image.ByteLength, Is.EqualTo(bytes.Length)); Assert.That(objects, Has.Count.EqualTo(1));
        Assert.That(objects[0].Pixels, Is.EqualTo(expected));
        Assert.That(objects[0].Dictionary, Does.Contain(type == 0 ? "/DeviceGray" : "/DeviceRGB"));
    }
    [TestCase(1, 0xa0, 3, new byte[] { 40, 50, 60, 10, 20, 30, 40, 50, 60 })]
    [TestCase(2, 0x18, 3, new byte[] { 10, 20, 30, 40, 50, 60, 70, 80, 90 })]
    [TestCase(4, 0x12, 2, new byte[] { 40, 50, 60, 70, 80, 90 })]
    [TestCase(8, 0x02, 1, new byte[] { 70, 80, 90 })]
    public void IndexedDepthsUsePaletteAndDefaultMissingAlphaToOpaque(byte depth, byte packed, int width, byte[] expected)
    {
        byte[] palette = depth == 1 ? [10, 20, 30, 40, 50, 60] : [10, 20, 30, 40, 50, 60, 70, 80, 90];
        byte[] rows = [0, packed];
        var image = PdfImage.LoadImage(PngFixture.Image(rows, width, depth: depth, type: 3, palette: palette, transparency: [0, 128]));
        var objects = PngFixture.ImageObjects(PngFixture.Pdf(image));
        Assert.That(objects, Has.Count.EqualTo(2)); Assert.That(objects[0].Pixels, Is.EqualTo(expected));
        Assert.That(objects[1].Pixels, Is.EqualTo(depth switch { 1 => new byte[] { 128, 0, 128 }, 2 => [0, 128, 255], 4 => [128, 255], _ => [255] }));
    }
    [TestCase(0, new byte[] { 0, 10, 20, 30, 40, 50, 60, 0, 70, 80, 90, 100, 110, 120 })]
    [TestCase(1, new byte[] { 1, 10, 20, 30, 30, 30, 30, 1, 70, 80, 90, 30, 30, 30 })]
    [TestCase(2, new byte[] { 2, 10, 20, 30, 40, 50, 60, 2, 60, 60, 60, 60, 60, 60 })]
    [TestCase(3, new byte[] { 3, 10, 20, 30, 35, 40, 45, 3, 65, 70, 75, 45, 45, 45 })]
    [TestCase(4, new byte[] { 4, 10, 20, 30, 30, 30, 30, 4, 60, 60, 60, 30, 30, 30 })]
    public void AllFiltersUseReconstructedLeftAndPreviousRows(int filter, byte[] rows)
    {
        var image = PdfImage.LoadImage(PngFixture.Image(rows, height: 2, type: 2));
        Assert.That(PngFixture.ImageObjects(PngFixture.Pdf(image))[0].Pixels,
            Is.EqualTo(new byte[] { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100, 110, 120 }), $"Filter {filter}");
    }
    [Test]
    public void PackedRowsApplyFiltersToBytesBeforeExpandingSamples()
    {
        var image = PdfImage.LoadImage(PngFixture.Image([1, 0x1b, 0x25, 2, 0xc9, 0x80], 5, 2, 2, 0));
        Assert.That(PngFixture.ImageObjects(PngFixture.Pdf(image))[0].Pixels,
            Is.EqualTo(new byte[] { 0, 85, 170, 255, 85, 255, 170, 85, 0, 255 }));
    }
    [TestCase(4, new byte[] { 0, 80, 0, 170, 128 }, new byte[] { 80, 170 }, new byte[] { 0, 128 })]
    [TestCase(6, new byte[] { 0, 255, 0, 80, 0, 40, 200, 90, 128 }, new byte[] { 255, 0, 80, 40, 200, 90 }, new byte[] { 0, 128 })]
    public void ExplicitAlphaPreservesUnassociatedHiddenColor(byte type, byte[] rows, byte[] color, byte[] alpha)
    {
        var objects = PngFixture.ImageObjects(PngFixture.Pdf(PdfImage.LoadImage(PngFixture.Image(rows, type: type))));
        Assert.That(objects[0].Pixels, Is.EqualTo(color)); Assert.That(objects[1].Pixels, Is.EqualTo(alpha));
        Assert.That(objects[0].Dictionary, Does.Contain($"/SMask {objects[1].Id} 0 R"));
        Assert.That(objects[1].Dictionary, Does.Contain("/DeviceGray")); Assert.That(objects[1].Dictionary, Does.Not.Contain("/SMask"));
    }
    [Test]
    public void GrayAndRgbTransparencyKeysCompareOriginalSamples()
    {
        var gray = PdfImage.LoadImage(PngFixture.Image([0, 0x1c], 3, depth: 2, type: 0, transparency: [0, 1]));
        Assert.That(PngFixture.ImageObjects(PngFixture.Pdf(gray))[1].Pixels, Is.EqualTo(new byte[] { 255, 0, 255 }));
        var rgb = PdfImage.LoadImage(PngFixture.Image([0, 1, 2, 3, 1, 2, 4], type: 2, transparency: [0, 1, 0, 2, 0, 3]));
        Assert.That(PngFixture.ImageObjects(PngFixture.Pdf(rgb))[1].Pixels, Is.EqualTo(new byte[] { 0, 255 }));
    }
    [Test]
    public void ConsecutiveSplitAndEmptyIdatWithAncillaryMetadataAreAccepted()
    {
        var z = PngFixture.Zlib([0, 80, 255]);
        var bytes = PngFixture.File(("IHDR", PngFixture.Header(1, 1, type: 4)), ("sRGB", [0]), ("gAMA", [0, 0, 177, 143]),
            ("cHRM", new byte[32]), ("pHYs", new byte[9]), ("tEXt", [65, 0, 66]),
            ("IDAT", z[..1]), ("IDAT", []), ("IDAT", z[1..]), ("IEND", []));
        Assert.That(PngFixture.ImageObjects(PngFixture.Pdf(PdfImage.LoadImage(bytes)))[0].Pixels, Is.EqualTo(new byte[] { 80 }));
    }
}
