using System.IO.Compression;
using NUnit.Framework;

namespace FactsPDF.Tests;

// Full (non-subset) embedding compresses the font program once per PdfFont and reuses it.
[TestFixture]
public sealed class FontProgramCacheTests
{
    [Test]
    public void CompressedProgramIsComputedOnceAndRoundTrips()
    {
        var bytes = FontFixture.Create(listDigits: true);
        var font = PdfFont.FromBytes(bytes);
        var first = font.CompressedData(CancellationToken.None);
        var second = font.CompressedData(CancellationToken.None);
        Assert.That(second.Overlaps(first) && second.Length == first.Length, Is.True, "The second call must return the cached bytes.");
        using var inflated = new MemoryStream();
        using (var zlib = new ZLibStream(new MemoryStream(first.ToArray()), CompressionMode.Decompress)) zlib.CopyTo(inflated);
        Assert.That(inflated.ToArray(), Is.EqualTo(bytes));
    }

    [Test]
    public void CachedProgramMatchesFreshCompressionInTheDocument()
    {
        var bytes = FontFixture.Create(listDigits: true);
        const string Html = "<p>AB Wi</p>";
        var warm = PdfFont.FromBytes(bytes);
        var writer = Pdf.Write().WithFonts(f => f.Regular(warm));
        var cold = writer.ToBytes(Html);
        Assert.That(writer.ToBytes(Html), Is.EqualTo(cold));
        Assert.That(Pdf.Write().WithFonts(f => f.Regular(PdfFont.FromBytes(bytes))).ToBytes(Html), Is.EqualTo(cold));
    }

    [Test]
    public void CachedProgramStillHonoursMaxOutputBytes()
    {
        var font = PdfFont.FromBytes(FontFixture.Create(listDigits: true));
        var limit = font.CompressedData(CancellationToken.None).Length - 1;
        var error = Assert.Throws<FactsPdfException>(() =>
            Pdf.Write().WithFonts(f => f.Regular(font)).WithLimits(l => l.MaxOutputBytes(limit)).ToBytes("<p>AB</p>"));
        Assert.That(error!.Code, Is.EqualTo("FPDF1401"));
    }

    [Test]
    public void CancelledCompressionLeavesNoCachedResult()
    {
        var font = PdfFont.FromBytes(FontFixture.Create(listDigits: true));
        Assert.Throws<OperationCanceledException>(() => font.CompressedData(new CancellationToken(true)));
        Assert.That(font.CompressedData(CancellationToken.None).Length, Is.GreaterThan(0));
    }
}
