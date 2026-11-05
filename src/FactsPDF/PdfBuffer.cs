using System.Buffers;

namespace FactsPDF;

// A write-only, append-only stream over an ArrayPool array. The serializers build the whole PDF in one,
// so growing it returns the outgrown arrays to the pool instead of leaving them for the GC, and
// Dispose returns the final array. Every write checks cancellation and the byte limit (FPDF1401).
// Returned arrays are cleared, so no PDF content lingers in the shared pool.
// Read the result through WrittenSpan/WrittenMemory before disposing; never retain them after.
internal sealed class PdfBuffer : Stream
{
    private readonly int limit;
    private readonly CancellationToken cancellation;
    private byte[] buffer;
    private int length;

    internal PdfBuffer(int limit, CancellationToken cancellation)
    {
        this.limit = limit;
        this.cancellation = cancellation;
        buffer = ArrayPool<byte>.Shared.Rent(Math.Clamp(limit, 1, 4096));
    }

    internal ReadOnlySpan<byte> WrittenSpan => buffer.AsSpan(0, length);
    internal ReadOnlyMemory<byte> WrittenMemory => buffer.AsMemory(0, length);
    internal byte[] ToArray() => WrittenSpan.ToArray();

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => length;
    public override long Position { get => length; set => throw new NotSupportedException(); }

    public override void Write(ReadOnlySpan<byte> source)
    {
        cancellation.ThrowIfCancellationRequested();
        if ((long)length + source.Length > limit) throw new FactsPdfException("FPDF1401", "PDF exceeds MaxOutputBytes.");
        if (length + source.Length > buffer.Length) Grow(length + source.Length);
        source.CopyTo(buffer.AsSpan(length));
        length += source.Length;
    }

    public override void Write(byte[] source, int offset, int count) => Write(source.AsSpan(offset, count));
    public override void WriteByte(byte value) => Write([value]);

    private void Grow(int needed)
    {
        // Doubling keeps appends amortised O(1); the limit check above bounds needed.
        var size = (int)Math.Min(Math.Max(needed, 2L * buffer.Length), Math.Min(limit, Array.MaxLength));
        var larger = ArrayPool<byte>.Shared.Rent(size);
        buffer.AsSpan(0, length).CopyTo(larger);
        ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        buffer = larger;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && buffer.Length > 0)
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
            buffer = [];
            length = 0;
        }
        base.Dispose(disposing);
    }

    public override void Flush() { }
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
}
