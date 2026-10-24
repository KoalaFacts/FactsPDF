using System.Reflection;
using System.Text;
using FactsPDF.CommandLine;
using FactsPDF.Configuration;
using NUnit.Framework;

namespace FactsPDF.Tests;

[TestFixture]
public sealed class PageFurnitureDecoderTests
{
    private static PdfPageFurniture Parse(string json) => PageFurnitureJson.Parse(Encoding.UTF8.GetBytes(json));
    private static readonly Func<Stream, CancellationToken, PdfPageFurniture> Read = typeof(CliApplication)
        .GetMethod("ReadPageFurniture", BindingFlags.Static | BindingFlags.NonPublic)!
        .CreateDelegate<Func<Stream, CancellationToken, PdfPageFurniture>>();

    [TestCase("{\"default\":{\"headerLeft\":{\"template\":\"\\uD83D\\uDE80\"}}}", "\U0001F680")]
    [TestCase("{\"default\":{\"headerLeft\":{\"template\":\"\\\\uD800\"}}}", "\\uD800")]
    [TestCase("{\"default\":{\"headerLeft\":{\"template\":\"\\\\\\uD83D\\uDE80\"}}}", "\\\U0001F680")]
    public void DecoderPreservesPairedSurrogatesAndLiteralEscapes(string json, string expected) =>
        Assert.That(Parse(json).Default.HeaderLeft!.Template, Is.EqualTo(expected));

    [Test]
    public void EscapedPropertyNamesAreDecodedAndDefaultsMatchApi()
    {
        var result = Parse("{\"\\u0064efault\":{\"headerLeft\":{},\"headerRight\":null},\"firstPage\":{}}");
        Assert.That(result, Is.EqualTo(new PdfPageFurniture
        {
            Default = new() { HeaderLeft = new() }, FirstPage = new()
        }));
    }

    [Test]
    public void DecoderLeavesTemplateGrammarAndLengthToCore()
    {
        var result = Parse("{\"default\":{\"headerLeft\":{\"template\":\"{unknown}\\n\"}}}");
        Assert.That(result.Default.HeaderLeft!.Template, Is.EqualTo("{unknown}\n"));
        var template = new string('A', 1025);
        Assert.That(Parse("{\"default\":{\"headerLeft\":{\"template\":\"" + template + "\"}}}")
            .Default.HeaderLeft!.Template, Is.EqualTo(template));
    }

    [Test]
    public void DecoderCancellationWinsOverOversizeAndInvalidUtf8()
    {
        Assert.Throws<OperationCanceledException>(() => PageFurnitureJson.Parse(new byte[65_537], new CancellationToken(true)));
        Assert.Throws<OperationCanceledException>(() => PageFurnitureJson.Parse(new byte[] { 0xff }, new CancellationToken(true)));
    }

    [Test]
    public void DecoderBoundsComeBeforeUtf8AndShape()
    {
        var bytes = new byte[65_537]; bytes[0] = 0xff;
        Assert.That(Assert.Throws<ArgumentException>(() => PageFurnitureJson.Parse(bytes))!.Message, Does.Contain("65536"));
        Assert.That(Assert.Throws<ArgumentException>(() => PageFurnitureJson.Parse(new byte[] { 0xff }))!.Message, Does.Contain("UTF-8"));
    }

    [TestCase(1)][TestCase(13)][TestCase(4096)]
    public void BoundedReaderHandlesShortReadsWithoutSeekingOrClosing(int chunk)
    {
        using var input = new GuardedStream(Encoding.UTF8.GetBytes("{}" + new string(' ', 65_534)), chunk);
        Assert.That(Read(input, default), Is.EqualTo(new PdfPageFurniture()));
        Assert.That(input.Consumed, Is.EqualTo(65_536));
        Assert.That(input.Requests[^1], Is.EqualTo(1));
        Assert.That(input.Requests.Max(), Is.LessThanOrEqualTo(4096));
        Assert.That(input.Disposed, Is.False);
    }

    [Test]
    public void BoundedReaderConsumesOnlyOneOverflowSentinel()
    {
        using var input = new GuardedStream(Encoding.UTF8.GetBytes("{}" + new string(' ', 100_000)), int.MaxValue);
        Assert.That(Assert.Throws<ArgumentException>(() => Read(input, default))!.Message, Does.Contain("65536"));
        Assert.That(input.Consumed, Is.EqualTo(65_537));
        Assert.That(input.Requests[^1], Is.EqualTo(1));
        Assert.That(input.Requests.Max(), Is.LessThanOrEqualTo(4096));
        Assert.That(input.Disposed, Is.False);
    }

    [Test]
    public void BoundedReaderObservesCancellationBetweenActualReads()
    {
        using var cancellation = new CancellationTokenSource();
        using var input = new GuardedStream(Encoding.UTF8.GetBytes("{}     "), 2, cancellation);
        Assert.Throws<OperationCanceledException>(() => Read(input, cancellation.Token));
        Assert.That(input.Consumed, Is.EqualTo(2));
        Assert.That(input.Requests, Has.Count.EqualTo(1));
        Assert.That(input.Disposed, Is.False);
        using var untouched = new GuardedStream("{}"u8.ToArray(), 2);
        Assert.Throws<OperationCanceledException>(() => Read(untouched, cancellation.Token));
        Assert.That(untouched.Requests, Is.Empty);
    }

    private sealed class GuardedStream(byte[] bytes, int chunk, CancellationTokenSource? cancellation = null) : Stream
    {
        internal int Consumed { get; private set; }
        internal List<int> Requests { get; } = [];
        internal bool Disposed { get; private set; }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new InvalidOperationException("Length must not be inspected.");
        public override long Position { get => throw new InvalidOperationException("Position must not be inspected."); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
        public override int Read(Span<byte> buffer)
        {
            Requests.Add(buffer.Length);
            var count = Math.Min(Math.Min(buffer.Length, chunk), bytes.Length - Consumed);
            bytes.AsSpan(Consumed, count).CopyTo(buffer);
            Consumed += count;
            cancellation?.Cancel();
            return count;
        }
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
