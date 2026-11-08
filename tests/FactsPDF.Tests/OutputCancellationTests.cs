using System.Buffers;
using System.Diagnostics;
using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

// R1: every public output path stops on cancellation, writes nothing to the caller's output when
// rendering is cancelled, and keeps an existing destination file intact.
[TestFixture]
public sealed class OutputCancellationTests
{
    private static readonly byte[] Sentinel = [1, 2, 3];
    private string directory = null!;

    [SetUp]
    public void SetUp() => directory = Directory.CreateTempSubdirectory("factspdf-cancel-").FullName;

    [TearDown]
    public void TearDown() => Directory.Delete(directory, true);

    // A long multi-page report: enough work that a short cancellation delay lands inside rendering.
    private static readonly string LargeHtml = BuildLargeHtml();

    private static string BuildLargeHtml()
    {
        var html = new StringBuilder("<h1>Report</h1>");
        for (var i = 0; i < 18_000; i++)
            html.Append("<p>Row ").Append(i).Append(" of a long report with enough words to wrap across the page width.</p>");
        return html.ToString();
    }

    private static PdfWriter LargeWriter() => Pdf.Write().WithLimits(l => l.MaxInputCharacters(4_000_000).MaxElements(40_000).MaxPages(2_000));

    public enum OutputPath { Bytes, Stream, StreamAsync, BufferWriter, File, FileAsync }

    private static readonly OutputPath[] AllPaths = Enum.GetValues<OutputPath>();

    private sealed class Outputs(string directory)
    {
        public MemoryStream Stream { get; } = new();
        public ArrayBufferWriter<byte> Buffer { get; } = new();
        public string File { get; } = Path.Combine(directory, "report.pdf");
    }

    private Outputs NewOutputs()
    {
        var outputs = new Outputs(directory);
        File.WriteAllBytes(outputs.File, Sentinel);
        return outputs;
    }

    // Synchronous paths run on the calling thread, as callers use them.
    private static async Task Run(PdfWriter writer, OutputPath path, string html, Outputs outputs, CancellationToken token)
    {
        switch (path)
        {
            case OutputPath.Bytes: writer.ToBytes(html, token); break;
            case OutputPath.Stream: writer.ToStream(html, outputs.Stream, token); break;
            case OutputPath.StreamAsync: await writer.ToStreamAsync(html, outputs.Stream, token); break;
            case OutputPath.BufferWriter: writer.ToBufferWriter(html, outputs.Buffer, token); break;
            case OutputPath.File: writer.ToFile(html, outputs.File, token); break;
            case OutputPath.FileAsync: await writer.ToFileAsync(html, outputs.File, token); break;
            default: throw new ArgumentOutOfRangeException(nameof(path));
        }
    }

    private void AssertNothingWritten(Outputs outputs)
    {
        Assert.That(outputs.Stream.Length, Is.Zero, "stream");
        Assert.That(outputs.Buffer.WrittenCount, Is.Zero, "buffer writer");
        Assert.That(File.ReadAllBytes(outputs.File), Is.EqualTo(Sentinel), "existing file");
        Assert.That(Directory.GetFiles(directory), Is.EqualTo(new[] { outputs.File }), "temporary files");
    }

    [TestCaseSource(nameof(AllPaths))]
    public void AlreadyCancelledTokenWritesNothingAndKeepsTheExistingFile(OutputPath path)
    {
        var outputs = NewOutputs();
        Assert.That(async () => await Run(Pdf.Write(), path, "<p>A</p>", outputs, new CancellationToken(true)),
            Throws.InstanceOf<OperationCanceledException>());
        AssertNothingWritten(outputs);
    }

    [TestCaseSource(nameof(AllPaths))]
    public void CancellationDuringRenderingStopsPromptlyAndWritesNothing(OutputPath path)
    {
        var writer = LargeWriter();
        writer.ToBytes(LargeHtml); // warm up JIT tiers
        // Both timings leave out garbage-collection pauses: one gen1/gen2 pause on this input can take
        // about half a full render on a busy two-core runner (on main as well), and no cancellation
        // check can run during it. What is measured is the engine's own work before it notices.
        var full = TimeSpan.MaxValue;
        for (var i = 0; i < 2; i++)
        {
            var pauses = GC.GetTotalPauseDuration();
            var run = Stopwatch.StartNew();
            writer.ToBytes(LargeHtml);
            var work = run.Elapsed - (GC.GetTotalPauseDuration() - pauses);
            if (work < full) full = work;
        }
        // On a machine fast enough to finish in under 100ms, "well below a full render" cannot be told
        // apart from scheduling noise, so the timing half of the test is inconclusive there.
        Assume.That(full, Is.GreaterThan(TimeSpan.FromMilliseconds(100)), $"a full render took only {full.TotalMilliseconds:F0}ms");

        var outputs = NewOutputs();
        using var cancellation = new CancellationTokenSource();
        // A dedicated thread, not a timer: a thread-pool timer can fire late on a busy two-core runner.
        var canceller = new Thread(() => { Thread.Sleep(5); cancellation.Cancel(); }) { IsBackground = true };
        var cancelPauses = GC.GetTotalPauseDuration();
        var elapsed = Stopwatch.StartNew();
        canceller.Start();
        Assert.That(async () => await Run(writer, path, LargeHtml, outputs, cancellation.Token),
            Throws.InstanceOf<OperationCanceledException>());
        elapsed.Stop();
        var gcPauses = GC.GetTotalPauseDuration() - cancelPauses;
        canceller.Join();

        // A path that dropped the token would still throw at a later check, but only after a full render.
        Assert.That(elapsed.Elapsed - gcPauses, Is.LessThan(full / 2),
            $"cancelled after {elapsed.Elapsed.TotalMilliseconds:F0}ms ({gcPauses.TotalMilliseconds:F0}ms of it in GC pauses); a full render takes {full.TotalMilliseconds:F0}ms of work");
        AssertNothingWritten(outputs);
    }

    [TestCase(OutputPath.File)]
    [TestCase(OutputPath.FileAsync)]
    public void FailedMoveIntoPlaceLeavesNoTemporaryFile(OutputPath path)
    {
        // The destination is a directory, so the final move fails after the temporary file is complete.
        var destination = Directory.CreateDirectory(Path.Combine(directory, "report.pdf")).FullName;
        var outputs = new Outputs(directory);
        Assert.That(async () => await Run(Pdf.Write(), path, "<p>A</p>", outputs, CancellationToken.None),
            Throws.InstanceOf<IOException>().Or.InstanceOf<UnauthorizedAccessException>());
        Assert.That(Directory.GetFiles(directory), Is.Empty, "temporary files");
        Assert.That(Directory.GetFileSystemEntries(destination), Is.Empty);
    }

    [Test]
    public void StreamCancelledBetweenChunksStopsBeforeTheNextChunk()
    {
        var writer = LargeWriter();
        var expected = writer.ToBytes(LargeHtml);
        Assert.That(expected.Length, Is.GreaterThan(2 * 16_384), "the PDF must span several 16 KiB chunks");

        using var cancellation = new CancellationTokenSource();
        using var output = new CancellingStream(cancellation);
        Assert.Throws<OperationCanceledException>(() => writer.ToStream(LargeHtml, output, cancellation.Token));
        Assert.That(output.Writes, Is.EqualTo(1));
        Assert.That(output.ToArray(), Is.EqualTo(expected.AsSpan(0, 16_384).ToArray()));
    }

    [Test]
    public void StreamAsyncPassesTheTokenToTheWrite()
    {
        using var cancellation = new CancellationTokenSource();
        using var output = new CancellingStream(cancellation);
        Assert.That(async () => await Pdf.Write().ToStreamAsync("<p>A</p>", output, cancellation.Token),
            Throws.InstanceOf<OperationCanceledException>());
        Assert.That(output.Writes, Is.EqualTo(1));
    }

    // Cancels its source when written to, as a caller abandoning a slow response would.
    private sealed class CancellingStream(CancellationTokenSource cancellation) : Stream
    {
        private readonly MemoryStream inner = new();
        public int Writes { get; private set; }
        public byte[] ToArray() => inner.ToArray();
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            Writes++;
            inner.Write(buffer);
            cancellation.Cancel();
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Writes++;
            cancellation.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            inner.Write(buffer.Span);
            return ValueTask.CompletedTask;
        }
    }
}
