using NUnit.Framework;

namespace FactsPDF.Tests;

public sealed class PngBoundaryTests
{
    [TestCase("bytes")][TestCase("dimension")][TestCase("pixels")][TestCase("decoded")]
    public void LimitsAcceptExactExpandedColorPlusAlphaAndRejectOneLess(string kind)
    {
        var bytes = PngFixture.Image([0, 10, 20, 30, 128, 40, 50, 60, 255]);
        PdfImageLimits Limits(int delta) => kind switch
        {
            "bytes" => new() { MaxBytes = bytes.Length + delta }, "dimension" => new() { MaxDimension = 2 + delta },
            "pixels" => new() { MaxPixels = 2 + delta }, _ => new() { MaxDecodedBytes = 8 + delta }
        };
        Assert.That(PdfImage.LoadImage(bytes, Limits(0)).ByteLength, Is.EqualTo(bytes.Length));
        Assert.That(Assert.Throws<FactsPdfException>(() => PdfImage.LoadImage(bytes, Limits(-1)))!.Code, Is.EqualTo("FPDF1603"));
    }
    [Test]
    public void FilterOverheadDoesNotConsumeExpandedPixelBudgetAndMcuLimitIsJpegOnly()
    {
        var bytes = PngFixture.Image([0, 10, 0, 20, 0, 30], 1, 3, type: 0);
        var image = PdfImage.LoadImage(bytes, new() { MaxDecodedBytes = 3, MaxMcuBlocks = 1 });
        Assert.That(PngFixture.ImageObjects(PngFixture.Pdf(image))[0].Pixels, Is.EqualTo(new byte[] { 10, 20, 30 }));
    }
    [Test]
    public void HugeDimensionsFailBeforeAllocationOrInflation()
    {
        foreach (var (width, height) in new[] { (int.MaxValue, int.MaxValue), (8193, 1), (8192, 8192) })
        {
            var bytes = PngFixture.File(("IHDR", PngFixture.Header(width, height)), ("IDAT", []), ("IEND", []));
            Assert.That(Assert.Throws<FactsPdfException>(() => PdfImage.LoadImage(bytes))!.Code, Is.EqualTo("FPDF1603"));
        }
    }
    [Test]
    public void AlphaPlaneCountsForPaletteAndTransparencyKeys()
    {
        foreach (var bytes in new[] { PngFixture.Image([0, 0], 1, type: 0, transparency: [0, 0]),
            PngFixture.Image([0, 0], 1, type: 3, palette: [1, 2, 3], transparency: [128]) })
            Assert.That(Assert.Throws<FactsPdfException>(() => PdfImage.LoadImage(bytes, new() { MaxDecodedBytes = 1 }))!.Code, Is.EqualTo("FPDF1603"));
    }
    [Test]
    public void CopiesInputAndReadsShortNonseekableStreamsWithoutClosingThem()
    {
        var bytes = PngFixture.Image([0, 10, 20, 30, 128, 40, 50, 60, 255]); var original = bytes.ToArray();
        var image = PdfImage.LoadImage(bytes); Array.Fill(bytes, (byte)0);
        using var stream = new ShortReads(original); var streamed = PdfImage.LoadImage(stream);
        Assert.That(PngFixture.Pdf(streamed), Is.EqualTo(PngFixture.Pdf(image))); Assert.That(stream.CanRead, Is.True);
        Assert.That(stream.ReadBytes, Is.EqualTo(original.Length));
        using var limited = new ShortReads(original);
        Assert.That(Assert.Throws<FactsPdfException>(() => PdfImage.LoadImage(limited, new() { MaxBytes = 25 }))!.Code, Is.EqualTo("FPDF1603"));
        Assert.That(limited.ReadBytes, Is.EqualTo(26)); Assert.That(limited.CanRead, Is.True);
    }
    [Test]
    public void AggregateOriginalBytesAndOutputMaskBudgetPreserveCallerOutput()
    {
        var bytes = PngFixture.Image([0, 10, 20, 30, 128, 40, 50, 60, 255]); var image = PdfImage.LoadImage(bytes);
        var options = new PdfOptions { Images = new Dictionary<string, PdfImage> { ["logo"] = image, ["alias"] = image } };
        var full = PngFixture.Pdf(image);
        foreach (var bad in new[] { options with { MaxTotalImageBytes = bytes.Length * 2 - 1 }, options with { MaxOutputBytes = full.Length - 1 } })
        {
            using var output = new MemoryStream(); output.Write([1, 2, 3]); output.Position = 1;
            var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert("<img src='logo'>", output, bad));
            Assert.That(error!.Code, Is.EqualTo(bad.MaxTotalImageBytes == bytes.Length * 2 - 1 ? "FPDF1603" : "FPDF1401"));
            Assert.That(output.ToArray(), Is.EqualTo(new byte[] { 1, 2, 3 })); Assert.That(output.Position, Is.EqualTo(1));
        }
        Assert.That(PngFixture.Pdf(image, new() { MaxOutputBytes = full.Length }), Is.EqualTo(full));
    }
    [Test]
    public void CancellationBeforeAndDuringSourceReadsKeepsStreamOpen()
    {
        var bytes = PngFixture.Image([0, 10, 20, 30, 128, 40, 50, 60, 255]);
        Assert.Throws<OperationCanceledException>(() => PdfImage.LoadImage(bytes, cancellationToken: new(true)));
        using var cts = new CancellationTokenSource(); using var stream = new ShortReads(bytes, cts);
        Assert.Throws<OperationCanceledException>(() => PdfImage.LoadImage(stream, cancellationToken: cts.Token));
        Assert.That(stream.CanRead, Is.True); Assert.That(stream.ReadBytes, Is.EqualTo(12));
    }
    [Test]
    public void ExpandedBudgetIsCheckedBeforeCollectingImageData()
    {
        // A valid first IDAT is enough to identify an over-budget expanded plane;
        // the missing IEND must not defer that bound until after IDAT buffering.
        var bytes = PngFixture.File(("IHDR", PngFixture.Header(8192, 8192)), ("IDAT", []));
        Assert.That(Assert.Throws<FactsPdfException>(() => PdfImage.LoadImage(bytes,
            new() { MaxPixels = 67_108_864 }))!.Code, Is.EqualTo("FPDF1603"));
    }
    [Test]
    public void CpuBoundPngWorkObservesCancellationBeyondEntryGuard()
    {
        var rows = new byte[(4096 * 4 + 1) * 2048];
        var bytes = PngFixture.Image(rows, 4096, 2048);
        for (var attempt = 0; attempt < 4; attempt++)
        {
            using var cts = new CancellationTokenSource(); using var ready = new ManualResetEventSlim(); using var start = new ManualResetEventSlim();
            var thread = new Thread(() => { ready.Set(); start.Wait(); Thread.Sleep(3); cts.Cancel(); });
            thread.Start(); ready.Wait(); start.Set();
            try
            {
                var error = Assert.Throws<OperationCanceledException>(() => PngReader.Read(bytes, new(), cts.Token));
                if (error!.StackTrace?.Contains("PngReader.Decode", StringComparison.Ordinal) == true
                    || error.StackTrace?.Contains("PngReader.Crc", StringComparison.Ordinal) == true
                    || error.StackTrace?.Contains("CompleteInput.Read", StringComparison.Ordinal) == true) return;
                // Retry only cancellation at the entry guard after a scheduler pause.
                Assert.That(error.StackTrace, Does.Contain("PngReader.Read"));
            }
            finally { thread.Join(); }
        }
        Assert.Fail("Cancellation did not reach the PNG CPU-work checkpoints.");
    }
    private sealed class ShortReads(byte[] bytes, CancellationTokenSource? cancellation = null) : MemoryStream(bytes)
    {
        internal int ReadBytes { get; private set; }
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count)
        { var n = base.Read(buffer, offset, Math.Min(3, count)); ReadBytes += n; if (ReadBytes >= 12) cancellation?.Cancel(); return n; }
    }
}
