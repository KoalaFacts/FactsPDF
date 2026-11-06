using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

[TestFixture]
public sealed class PdfAsciiTests
{
    [TestCase("")]
    [TestCase("Hello, World 0~")]
    [TestCase("\u0000\u001f\u007f")]
    [TestCase("café 中")]
    [TestCase("pair 😀 end")]
    public void AppendHexMatchesHexOfAsciiBytes(string text)
    {
        var output = new StringBuilder("<");
        PdfAscii.AppendHex(output, text);
        Assert.That(output.ToString(), Is.EqualTo("<" + Convert.ToHexString(Encoding.ASCII.GetBytes(text))));
    }

    [Test]
    public void WritesEveryChunkOfALargeBuilderAndCountsTheSameBytes()
    {
        var text = new StringBuilder();
        for (var i = 0; i < 3000; i++) text.Append("q 0.5 0.25 0 rg ").Append(i).Append(" re f Q\n");
        text.Append('\u00e9'); // a non-ASCII character becomes '?' as Encoding.ASCII does
        var chunks = 0;
        foreach (var _ in text.GetChunks()) chunks++;
        Assert.That(chunks, Is.GreaterThan(1));
        using var output = new MemoryStream();
        PdfAscii.Write(output, text, CancellationToken.None);
        var expected = Encoding.ASCII.GetBytes(text.ToString());
        Assert.That(output.ToArray(), Is.EqualTo(expected));
        Assert.That(PdfAscii.ByteCount(text), Is.EqualTo(expected.Length));
    }

    [Test]
    public void WriteStopsWhenCancelled()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        using var output = new MemoryStream();
        Assert.That(() => PdfAscii.Write(output, new StringBuilder("BT ET"), cancelled.Token),
            Throws.InstanceOf<OperationCanceledException>());
        Assert.That(output.Length, Is.Zero);
    }

    [Test]
    public void ContentStreamLengthMatchesTheWrittenBytesOnLargePages()
    {
        var html = new StringBuilder();
        for (var i = 0; i < 400; i++)
            html.Append("<p style=\"border:1pt solid #123;padding:2pt;word-spacing:2pt\">AB ").Append(i).Append(" Wi AB Wi</p>");
        foreach (var writer in new[] { Pdf.Write(), Pdf.Write().WithFonts(f => f.Regular(PdfFont.FromBytes(FontFixture.Create(listDigits: true)))) })
        {
            var pdf = Encoding.Latin1.GetString(writer.ToBytes(html.ToString()));
            var streams = 0;
            for (var at = pdf.IndexOf("/Length ", StringComparison.Ordinal); at >= 0; at = pdf.IndexOf("/Length ", at + 1, StringComparison.Ordinal))
            {
                var end = pdf.IndexOfAny([' ', '/', '>'], at + 8);
                var length = int.Parse(pdf.AsSpan(at + 8, end - at - 8), System.Globalization.CultureInfo.InvariantCulture);
                var start = pdf.IndexOf("stream\n", at, StringComparison.Ordinal) + 7;
                Assert.That(pdf.AsSpan(start + length, 10).ToString(), Is.EqualTo("\nendstream"));
                streams++;
            }
            Assert.That(streams, Is.GreaterThan(2));
        }
    }

    [Test]
    public void ContentStreamsStillRespectMaxOutputBytes()
    {
        var html = string.Concat(Enumerable.Repeat("<p style=\"border:1pt solid #123\">Bounded content</p>", 200));
        var error = Assert.Throws<FactsPdfException>(() => Pdf.Write().WithLimits(l => l.MaxOutputBytes(4096)).ToBytes(html));
        Assert.That(error!.Code, Is.EqualTo("FPDF1401"));
    }
}
