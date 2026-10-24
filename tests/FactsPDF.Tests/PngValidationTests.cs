using NUnit.Framework;

namespace FactsPDF.Tests;

public sealed class PngValidationTests
{
    private static byte[] H(byte depth = 8, byte type = 6) => PngFixture.Header(1, 1, depth, type);
    private static byte[] Z() => PngFixture.Zlib([0, 10, 20, 30, 128]);
    private static byte[] File(params (string, byte[])[] chunks) => PngFixture.File(chunks);
    public static IEnumerable<TestCaseData> Malformed()
    {
        yield return new TestCaseData(PngFixture.Signature).SetName("PngSignatureOnlyIsMalformed");
        var crc = PngFixture.Image([0, 10, 20, 30, 128], 1); crc[29] ^= 1;
        yield return new TestCaseData(crc).SetName("PngBadCrc");
        foreach (var (name, chunks) in new (string, (string, byte[])[])[]
        {
            ("NoHeader", [("IDAT", Z()), ("IEND", [])]),
            ("DuplicateHeader", [("IHDR", H()), ("IHDR", H()), ("IDAT", Z()), ("IEND", [])]),
            ("NoIdat", [("IHDR", H()), ("IEND", [])]),
            ("NoEnd", [("IHDR", H()), ("IDAT", Z())]),
            ("NonemptyEnd", [("IHDR", H()), ("IDAT", Z()), ("IEND", [0])]),
            ("NonconsecutiveIdat", [("IHDR", H()), ("IDAT", Z()), ("tEXt", [65, 0, 66]), ("IDAT", []), ("IEND", [])]),
            ("ReservedTypeBit", [("IHDR", H()), ("abca", []), ("IDAT", Z()), ("IEND", [])]),
            ("NonletterType", [("IHDR", H()), ("tE1t", []), ("IDAT", Z()), ("IEND", [])]),
            ("PaletteMissing", [("IHDR", H(type: 3)), ("IDAT", PngFixture.Zlib([0, 0])), ("IEND", [])]),
            ("PaletteEmpty", [("IHDR", H(type: 3)), ("PLTE", []), ("IDAT", Z()), ("IEND", [])]),
            ("PaletteNotTriples", [("IHDR", H(type: 3)), ("PLTE", [1, 2]), ("IDAT", Z()), ("IEND", [])]),
            ("PaletteTooManyForDepth", [("IHDR", H(1, 3)), ("PLTE", new byte[9]), ("IDAT", Z()), ("IEND", [])]),
            ("PaletteOnGray", [("IHDR", H(type: 0)), ("PLTE", [1, 2, 3]), ("IDAT", Z()), ("IEND", [])]),
            ("PaletteAfterData", [("IHDR", H()), ("IDAT", Z()), ("PLTE", [1, 2, 3]), ("IEND", [])]),
            ("DuplicatePalette", [("IHDR", H(type: 3)), ("PLTE", [1, 2, 3]), ("PLTE", [1, 2, 3]), ("IDAT", Z()), ("IEND", [])]),
            ("IndexOutsidePalette", [("IHDR", H(type: 3)), ("PLTE", [1, 2, 3]), ("IDAT", PngFixture.Zlib([0, 1])), ("IEND", [])]),
            ("AlphaTrnsForbidden", [("IHDR", H()), ("tRNS", [0]), ("IDAT", Z()), ("IEND", [])]),
            ("GrayTrnsWrongLength", [("IHDR", H(type: 0)), ("tRNS", [0]), ("IDAT", Z()), ("IEND", [])]),
            ("GrayTrnsOutOfRange", [("IHDR", H(2, 0)), ("tRNS", [0, 4]), ("IDAT", Z()), ("IEND", [])]),
            ("RgbTrnsOutOfRange", [("IHDR", H(type: 2)), ("tRNS", [1, 0, 0, 0, 0, 0]), ("IDAT", Z()), ("IEND", [])]),
            ("PaletteTrnsBeforePalette", [("IHDR", H(type: 3)), ("tRNS", [0]), ("PLTE", [1, 2, 3]), ("IDAT", Z()), ("IEND", [])]),
            ("PaletteTrnsTooLong", [("IHDR", H(type: 3)), ("PLTE", [1, 2, 3]), ("tRNS", [0, 128]), ("IDAT", Z()), ("IEND", [])]),
            ("PaletteTrnsEmpty", [("IHDR", H(type: 3)), ("PLTE", [1, 2, 3]), ("tRNS", []), ("IDAT", Z()), ("IEND", [])]),
            ("DuplicateTrns", [("IHDR", H(type: 0)), ("tRNS", [0, 1]), ("tRNS", [0, 1]), ("IDAT", Z()), ("IEND", [])]),
            ("TrnsAfterIdat", [("IHDR", H(type: 0)), ("IDAT", PngFixture.Zlib([0, 0])), ("tRNS", [0, 0]), ("IEND", [])]),
            ("PaletteAfterTrns", [("IHDR", H(type: 2)), ("tRNS", [0, 0, 0, 0, 0, 0]), ("PLTE", [1, 2, 3]), ("IDAT", Z()), ("IEND", [])]),
            ("InvalidFilter", [("IHDR", H()), ("IDAT", PngFixture.Zlib([5, 10, 20, 30, 128])), ("IEND", [])]),
            ("ShortRows", [("IHDR", H()), ("IDAT", PngFixture.Zlib([0, 10, 20, 30])), ("IEND", [])]),
            ("ExcessRows", [("IHDR", H()), ("IDAT", PngFixture.Zlib([0, 10, 20, 30, 128, 0])), ("IEND", [])]),
            ("SrgbInvalidIntent", [("IHDR", H()), ("sRGB", [4]), ("IDAT", Z()), ("IEND", [])]),
            ("SrgbWrongLength", [("IHDR", H()), ("sRGB", []), ("IDAT", Z()), ("IEND", [])]),
            ("SrgbAfterPalette", [("IHDR", H(type: 3)), ("PLTE", [1, 2, 3]), ("sRGB", [0]), ("IDAT", Z()), ("IEND", [])]),
            ("DuplicateSrgb", [("IHDR", H()), ("sRGB", [0]), ("sRGB", [0]), ("IDAT", Z()), ("IEND", [])])
        }) yield return new TestCaseData(File(chunks)).SetName("Png" + name);
        yield return new TestCaseData(File(("IHDR", H()), ("IDAT", Z()), ("IEND", [])).Concat(new byte[] { 0 }).ToArray()).SetName("PngTrailingFileByte");
        var length = PngFixture.Image([0, 10, 20, 30, 128], 1); Array.Fill(length, (byte)255, 33, 4);
        yield return new TestCaseData(length).SetName("PngChunkLengthOverflow");
        var checksum = Z(); checksum[^1] ^= 1;
        yield return new TestCaseData(File(("IHDR", H()), ("IDAT", checksum), ("IEND", []))).SetName("PngAdlerMismatch");
        yield return new TestCaseData(File(("IHDR", H()), ("IDAT", new byte[] { 0x78, 0x9c, 0xff, 0xff, 0xff, 0xff }), ("IEND", []))).SetName("PngCorruptDeflate");
    }
    [TestCaseSource(nameof(Malformed))]
    public void MalformedDataHasStableDiagnostic(byte[] bytes)
        => Assert.That(Assert.Throws<FactsPdfException>(() => PdfImage.LoadImage(bytes))!.Code, Is.EqualTo("FPDF1601"));

    [TestCase("acTL")][TestCase("fcTL")][TestCase("fdAT")][TestCase("iCCP")]
    [TestCase("cICP")][TestCase("mDCV")][TestCase("cLLI")][TestCase("ABCD")]
    public void UnsupportedAnimationColorTransformsAndCriticalChunksAreExplicit(string type)
        => Assert.That(Assert.Throws<FactsPdfException>(() => PdfImage.LoadImage(File(("IHDR", H()), (type, []), ("IDAT", Z()), ("IEND", []))))!.Code, Is.EqualTo("FPDF1602"));
    [TestCase(16, 6, 0)][TestCase(8, 6, 1)]
    public void UnsupportedDepthAndAdam7AreExplicit(byte depth, byte type, byte interlace)
        => Assert.That(Assert.Throws<FactsPdfException>(() => PdfImage.LoadImage(File(("IHDR", PngFixture.Header(1, 1, depth, type, interlace)), ("IDAT", Z()), ("IEND", []))))!.Code, Is.EqualTo("FPDF1602"));
    [Test]
    public void EveryTruncatedZlibPrefixIsRejectedEvenWithFreshValidChunkCrc()
    {
        var compressed = Z();
        for (var end = 0; end < compressed.Length; end++)
            Assert.That(Assert.Throws<FactsPdfException>(() => PdfImage.LoadImage(File(("IHDR", H()), ("IDAT", compressed[..end]), ("IEND", []))))!.Code,
                Is.EqualTo("FPDF1601"), $"Zlib prefix {end}");
    }
    [Test]
    public void UnusedTailAfterZlibEndIsPermittedByPng()
    {
        var image = PdfImage.LoadImage(File(("IHDR", H()), ("IDAT", [.. Z(), 1, 2, 3, 4, 5]), ("IEND", [])));
        Assert.That(PngFixture.ImageObjects(PngFixture.Pdf(image))[0].Pixels, Is.EqualTo(new byte[] { 10, 20, 30 }));
    }
    [Test]
    public void LiteralStoredDeflateAndAdlerDecodeWithoutFixtureCompressor()
    {
        // RFC1950 header + final stored block (five bytes) + independently summed Adler32.
        byte[] zlib = [0x78, 0x01, 0x01, 0x05, 0x00, 0xfa, 0xff, 0, 10, 20, 30, 128, 0x01, 0x25, 0x00, 0xbd];
        var image = PdfImage.LoadImage(File(("IHDR", H()), ("IDAT", zlib), ("IEND", [])));
        Assert.That(PngFixture.ImageObjects(PngFixture.Pdf(image))[0].Pixels, Is.EqualTo(new byte[] { 10, 20, 30 }));
    }
    [Test]
    public void SecondZlibStreamIsUnusedTailAndDoesNotAddPixels()
    {
        var image = PdfImage.LoadImage(File(("IHDR", H()), ("IDAT", [.. Z(), .. PngFixture.Zlib([0, 1, 2, 3, 255])]), ("IEND", [])));
        Assert.That(PngFixture.ImageObjects(PngFixture.Pdf(image))[0].Pixels, Is.EqualTo(new byte[] { 10, 20, 30 }));
    }
    [TestCase(0, 0)][TestCase(-1, 1)][TestCase(1, -1)]
    public void InvalidDimensionsAreMalformed(int width, int height)
        => Assert.That(Assert.Throws<FactsPdfException>(() => PdfImage.LoadImage(File(("IHDR", PngFixture.Header(width, height)), ("IDAT", Z()), ("IEND", []))))!.Code, Is.EqualTo("FPDF1601"));
    [TestCase(1, 2)][TestCase(2, 4)][TestCase(16, 3)][TestCase(8, 1)]
    public void InvalidColorAndDepthCombinationsAreMalformed(byte depth, byte type)
        => Assert.That(Assert.Throws<FactsPdfException>(() => PdfImage.LoadImage(File(("IHDR", H(depth, type)), ("IDAT", Z()), ("IEND", []))))!.Code, Is.EqualTo("FPDF1601"));
    [Test]
    public void EveryPngChunkTruncationAfterSignatureFailsAsMalformed()
    {
        var bytes = PngFixture.Image([0, 10, 20, 30, 128], 1);
        for (var end = 8; end < bytes.Length; end++)
            Assert.That(Assert.Throws<FactsPdfException>(() => PdfImage.LoadImage(bytes[..end]))!.Code, Is.EqualTo("FPDF1601"), $"PNG prefix {end}");
    }
}
