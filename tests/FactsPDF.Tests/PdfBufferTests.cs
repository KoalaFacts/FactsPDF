using NUnit.Framework;

namespace FactsPDF.Tests;

[TestFixture]
public sealed class PdfBufferTests
{
    [Test]
    public void GrowsAcrossManyWritesAndKeepsEveryByteInOrder()
    {
        using var buffer = new PdfBuffer(1 << 20, CancellationToken.None);
        var expected = new List<byte>();
        var random = new Random(7);
        for (var i = 0; i < 400; i++)
        {
            var chunk = new byte[random.Next(0, 3000)];
            random.NextBytes(chunk);
            if (i % 3 == 0) buffer.Write(chunk, 0, chunk.Length); else buffer.Write(chunk);
            buffer.WriteByte((byte)i);
            expected.AddRange(chunk); expected.Add((byte)i);
        }
        Assert.That(buffer.Length, Is.EqualTo(expected.Count));
        Assert.That(buffer.Position, Is.EqualTo(expected.Count));
        Assert.That(buffer.WrittenSpan.ToArray(), Is.EqualTo(expected));
        Assert.That(buffer.ToArray(), Is.EqualTo(expected));
    }

    [Test]
    public void WritingPastTheLimitFailsWithMaxOutputBytesAndKeepsEarlierBytes()
    {
        using var buffer = new PdfBuffer(10, CancellationToken.None);
        buffer.Write(new byte[10]);
        var error = Assert.Throws<FactsPdfException>(() => buffer.WriteByte(1));
        Assert.That(error!.Code, Is.EqualTo("FPDF1401"));
        Assert.That(buffer.Length, Is.EqualTo(10));
    }

    [Test]
    public void WritesObserveCancellation()
    {
        using var source = new CancellationTokenSource();
        using var buffer = new PdfBuffer(100, source.Token);
        buffer.Write([1, 2]);
        source.Cancel();
        Assert.Throws<OperationCanceledException>(() => buffer.Write([3]));
    }

    [Test]
    public void IsWriteOnlyAndDisposeIsIdempotent()
    {
        var buffer = new PdfBuffer(100, CancellationToken.None);
        Assert.That((buffer.CanRead, buffer.CanSeek, buffer.CanWrite), Is.EqualTo((false, false, true)));
        Assert.Throws<NotSupportedException>(() => buffer.Position = 0);
        Assert.Throws<NotSupportedException>(() => buffer.Seek(0, SeekOrigin.Begin));
        buffer.Write([1, 2, 3]);
        buffer.Dispose();
        buffer.Dispose();
        Assert.That(buffer.Length, Is.Zero);
    }

    [Test]
    public void EveryOutputPathWritesTheSameBytes()
    {
        var writer = Pdf.Write().WithPageNumbers();
        const string Html = "<h1>Title</h1><p>Body text that spans a little.</p>";
        var expected = writer.ToBytes(Html);
        using var stream = new MemoryStream();
        writer.ToStream(Html, stream);
        var bufferWriter = new System.Buffers.ArrayBufferWriter<byte>();
        writer.ToBufferWriter(Html, bufferWriter);
        Assert.That(stream.ToArray(), Is.EqualTo(expected));
        Assert.That(bufferWriter.WrittenSpan.ToArray(), Is.EqualTo(expected));
    }
}
