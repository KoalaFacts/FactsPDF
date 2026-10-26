using NUnit.Framework;

namespace FactsPDF.Tests;

public sealed class JpegResourceBoundaryTests
{
    [TestCase("bytes")] [TestCase("dimension")] [TestCase("pixels")]
    [TestCase("decoded")] [TestCase("blocks")]
    public void EachIndependentLimitAcceptsExactlyAndRejectsOneLess(string kind)
    {
        var bytes = JpegResourceTests.Bytes("rgb-420");
        PdfImageLimits Limits(int delta) => kind switch
        {
            "bytes" => new() { MaxBytes = bytes.Length + delta },
            "dimension" => new() { MaxDimension = 320 + delta },
            "pixels" => new() { MaxPixels = 51_200 + delta },
            "decoded" => new() { MaxDecodedBytes = 153_600 + delta },
            _ => new() { MaxMcuBlocks = 1_200 + delta }
        };
        Assert.That(PdfImage.LoadImage(bytes, Limits(0)).PixelWidth, Is.EqualTo(320));
        Assert.That(Assert.Throws<FactsPdfException>(() => PdfImage.LoadImage(bytes, Limits(-1)))!.Code, Is.EqualTo("FPDF1603"));
    }

    [Test]
    public void PartialEdgeMcusCountAllPaddedBlocks()
    {
        var bytes = JpegResourceTests.Bytes("odd-420");
        Assert.That(PdfImage.LoadImage(bytes, new() { MaxMcuBlocks = 12 }).PixelWidth, Is.EqualTo(17));
        Assert.That(Assert.Throws<FactsPdfException>(() => PdfImage.LoadImage(bytes,
            new() { MaxMcuBlocks = 11 }))!.Code, Is.EqualTo("FPDF1603"));
    }

    [Test]
    public void ShortNonSeekableStreamStopsAtLimitPlusOneAndStaysOpen()
    {
        using var source = new ShortReads(JpegResourceTests.Bytes("rgb-420"));
        Assert.That(Assert.Throws<FactsPdfException>(() => PdfImage.LoadImage(source,
            new() { MaxBytes = 40 }))!.Code, Is.EqualTo("FPDF1603"));
        Assert.That(source.BytesRead, Is.EqualTo(41));
        Assert.That(source.CanRead, Is.True);
    }

    [Test]
    public void ShortReadsLoadWithoutDependingOnLengthOrSeek()
    {
        var bytes = JpegResourceTests.Bytes("gray"); using var source = new ShortReads(bytes);
        Assert.That(PdfImage.LoadImage(source, new() { MaxBytes = bytes.Length }).Data.ToArray(), Is.EqualTo(bytes));
        Assert.That(source.BytesRead, Is.EqualTo(bytes.Length));
        Assert.That(source.CanRead, Is.True);
    }

    [Test]
    public void InvalidLimitsAreApiErrors()
    {
        var limits = new[] { new PdfImageLimits { MaxBytes = 0 }, new() { MaxDimension = -1 },
            new() { MaxPixels = 0 }, new() { MaxDecodedBytes = -1 }, new() { MaxMcuBlocks = 0 } };
        foreach (var limit in limits)
            Assert.Throws<ArgumentOutOfRangeException>(() => PdfImage.LoadImage(JpegResourceTests.Bytes("gray"), limit));
        Assert.Throws<ArgumentNullException>(() => PdfImage.LoadImage((Stream)null!));
        using var closed = new MemoryStream(); closed.Close();
        Assert.Throws<ArgumentException>(() => PdfImage.LoadImage(closed));
    }

    [Test]
    public void CancellationBeforeLoadingOrDuringStreamReadDoesNotCloseSource()
    {
        var bytes = JpegResourceTests.Bytes("rgb-420"); using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Assert.Throws<OperationCanceledException>(() => PdfImage.LoadImage(bytes, cancellationToken: cancelled.Token));
        using var cts = new CancellationTokenSource(); using var source = new ShortReads(bytes, cts);
        Assert.Throws<OperationCanceledException>(() => PdfImage.LoadImage(source, cancellationToken: cts.Token));
        Assert.That(source.BytesRead, Is.EqualTo(12)); Assert.That(source.CanRead, Is.True);
    }

    [Test]
    public void FailureDoesNotPoisonSubsequentResources()
    {
        var before = PdfImage.LoadImage(JpegResourceTests.Bytes("gray"));
        Assert.Throws<FactsPdfException>(() => PdfImage.LoadImage(JpegResourceTests.Bytes("truncated-entropy")));
        var after = PdfImage.LoadImage(JpegResourceTests.Bytes("gray"));
        Assert.That(after.Data.ToArray(), Is.EqualTo(before.Data.ToArray()));
    }

    [Test]
    public void EveryTruncatedPrefixOfARealJpegHasABoundedImageDiagnostic()
    {
        var bytes = JpegResourceTests.Bytes("rgb-420");
        for (var end = 0; end < bytes.Length; end++)
            Assert.That(Assert.Throws<FactsPdfException>(() => PdfImage.LoadImage(bytes.AsSpan(0, end)))!.Code,
                Is.EqualTo("FPDF1601"), $"Truncated length {end}");
    }

    [Test]
    public void ByteLimitIsCheckedBeforeUnsupportedFormatOrCompressedData()
    {
        Assert.That(Assert.Throws<FactsPdfException>(() => PdfImage.LoadImage(new byte[9],
            new() { MaxBytes = 8 }))!.Code, Is.EqualTo("FPDF1603"));
        var image = JpegFixture.Gray([0x3f], width: 65535, height: 65535);
        Assert.That(Assert.Throws<FactsPdfException>(() => PdfImage.LoadImage(image))!.Code, Is.EqualTo("FPDF1603"));
    }

    [Test]
    public void LargeScanValidationObservesCancellation()
    {
        // Cancel only after real MCU work reaches the final 256-block guard.
        // Entropy ends before the next 8192-byte boundary, so removal of the
        // loop's ThrowIfCancellationRequested cannot be masked by Byte().
        const int blocks = 2_096_640; // 4096 x 32760 / 64
        var bytes = JpegFixture.Gray(new byte[blocks / 4], width: 4096, height: 32760);
        var limits = new PdfImageLimits { MaxDimension = 32768, MaxPixels = 134217728,
            MaxDecodedBytes = 134217728, MaxMcuBlocks = blocks };
        var finalCheckpoint = 1 + (blocks - 1) / 256 * 256;
        Assert.That(finalCheckpoint, Is.EqualTo(blocks - 255));
        Assert.That((blocks / 4) % 8192, Is.Not.EqualTo(0));

        using var cancellation = new CancellationTokenSource();
        var reached = 0;
        Assert.Throws<OperationCanceledException>(() => JpegReader.Read(bytes, limits, cancellation.Token,
            onScanBlockCheckpoint: atBlock =>
            {
                reached = atBlock;
                if (atBlock == finalCheckpoint) cancellation.Cancel();
            }));
        Assert.That(reached, Is.EqualTo(finalCheckpoint));
        Assert.That(cancellation.IsCancellationRequested, Is.True);

        // An uncancelled public load still succeeds, and canceling after return
        // cannot retroactively invalidate an already loaded image.
        using var afterCompletion = new CancellationTokenSource();
        var image = PdfImage.LoadImage(bytes, limits, afterCompletion.Token);
        afterCompletion.Cancel();
        Assert.That(image.PixelWidth, Is.EqualTo(4096));
        Assert.That(image.PixelHeight, Is.EqualTo(32760));
    }

    [Test]
    public void CancellationActuallyReachesTheLongMarkerFillByteLoop()
    {
        // Cancel synchronously inside the 8192-byte header checkpoint, never at
        // entry. Without that guard, the malformed marker stream fails with a
        // JPEG diagnostic instead of OperationCanceledException.
        var bytes = new byte[3 * 8192 + 1];
        Array.Fill(bytes, (byte)255);
        bytes[0] = 255; bytes[1] = 216; bytes[^1] = 217;
        var limits = new PdfImageLimits { MaxBytes = bytes.Length };
        using var cancellation = new CancellationTokenSource();
        var reached = -1;
        Assert.Throws<OperationCanceledException>(() => JpegReader.Read(bytes, limits, cancellation.Token,
            onHeaderByteCheckpoint: position =>
            {
                reached = position;
                if (position == 8192) cancellation.Cancel();
            }));
        Assert.That(reached, Is.EqualTo(8192));
        Assert.That(cancellation.IsCancellationRequested, Is.True);
    }

    private sealed class ShortReads(byte[] bytes, CancellationTokenSource? cancellation = null) : MemoryStream(bytes)
    {
        public int BytesRead { get; private set; }
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = base.Read(buffer, offset, Math.Min(3, count)); BytesRead += read;
            if (BytesRead >= 12) cancellation?.Cancel();
            return read;
        }
    }
}
