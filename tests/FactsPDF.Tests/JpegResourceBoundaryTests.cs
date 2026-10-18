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
        // 2,097,152 gray blocks with two coded zero bits per block; no full
        // image buffer. A real cancellation interrupts the CPU-bound validation.
        var bytes = JpegFixture.Gray(new byte[524288], width: 4096, height: 32768);
        var limits = new PdfImageLimits { MaxDimension = 32768, MaxPixels = 134217728,
            MaxDecodedBytes = 134217728, MaxMcuBlocks = 2097152 };
        CancelDuring(token => PdfImage.LoadImage(bytes, limits, token));
    }

    [Test]
    public void CancellationActuallyReachesTheLongMarkerFillByteLoop()
    {
        // Call the reader on already-owned bytes, avoiding a cancellation during
        // a loader copy. The incomplete header would report1601 without its
        // in-loop cancellation check. No wall-clock performance claim is made.
        var bytes = new byte[32 * 1024 * 1024]; Array.Fill(bytes, (byte)255);
        bytes[0] = 255; bytes[1] = 216; bytes[^1] = 217;
        var limits = new PdfImageLimits { MaxBytes = bytes.Length };
        JpegReader.Read(JpegFixture.Gray([0x3f]), new(), default); // warm reader/JIT
        for (var attempt = 0; attempt < 4; attempt++)
        {
            var error = CancelDuring(token => JpegReader.Read(bytes, limits, token));
            if (error.StackTrace?.Contains("JpegReader.Byte", StringComparison.Ordinal) == true
                || error.StackTrace?.Contains("JpegReader.Marker", StringComparison.Ordinal) == true) return;
            // A descheduled thread can see cancellation at the initial guard.
            // Retry only that case; it cannot establish fill-loop coverage.
            Assert.That(error.StackTrace, Does.Contain("JpegReader.Read"));
        }
        Assert.Fail("Cancellation never reached the marker-fill byte checkpoint; entry-guard cancellation is insufficient.");
    }

    private static OperationCanceledException CancelDuring(Action<CancellationToken> read)
    {
        // CancelAfter uses a thread-pool timer, which can remain queued until
        // after a CPU loop finishes on a busy runner. Both validation tests use
        // a ready dedicated thread to keep cancellation independent of that pool.
        using var cts = new CancellationTokenSource();
        using var ready = new ManualResetEventSlim();
        using var start = new ManualResetEventSlim();
        var cancelThread = new Thread(() => { ready.Set(); start.Wait(); Thread.Sleep(1); cts.Cancel(); });
        cancelThread.Start(); ready.Wait(); start.Set();
        try { return Assert.Throws<OperationCanceledException>(() => read(cts.Token))!; }
        finally { cancelThread.Join(); }
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
