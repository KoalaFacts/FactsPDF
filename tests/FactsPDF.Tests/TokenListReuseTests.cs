using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

// The document reader reuses one token list per thread. It must never keep a finished
// document's tokens alive, never pool a list whose array would sit on the large-object heap,
// and never change what a document renders to.
[TestFixture]
[NonParallelizable]
public sealed class TokenListReuseTests
{
    private const string Page = "<style>p{color:#036}</style><h1>T</h1><p class='a' style='margin:2pt'>A &amp; B</p><ul><li>x</li></ul>";

    private static string Paragraphs(int count)
    {
        var html = new StringBuilder();
        for (var i = 0; i < count; i++) html.Append("<p>A</p>");
        return html.ToString();
    }

    [Test]
    public void ListIsEmptyAndPooledAfterARead()
    {
        HtmlDocumentReader.ReadTree(Page, new PdfOptions(), default);
        var pooled = HtmlDocumentReader.PooledTokens;
        Assert.That(pooled, Is.Not.Null);
        Assert.That(pooled!.Value.Count, Is.Zero);
        Assert.That(pooled.Value.Capacity, Is.GreaterThan(0).And.LessThanOrEqualTo(HtmlDocumentReader.MaxPooledTokens));
    }

    [TestCase("<p>A</p><blink>B</blink>", "FPDF1102")]
    [TestCase("<p>A</p><style>p{color:banana}</style>", "FPDF1202")]
    [TestCase("<p>A &bogus; B</p>", "FPDF1104")]
    [TestCase("<p>A</p></div>", "FPDF1101")]
    public void ListIsEmptyAfterAFailedRead(string html, string code)
    {
        var error = Assert.Throws<FactsPdfException>(() => HtmlDocumentReader.ReadTree(html, new PdfOptions(), default));
        Assert.That(error!.Code, Is.EqualTo(code));
        Assert.That(HtmlDocumentReader.PooledTokens?.Count ?? 0, Is.Zero);
    }

    [Test]
    public void ListIsEmptyAfterACancelledRead()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        Assert.Throws<OperationCanceledException>(() => HtmlDocumentReader.ReadTree(Paragraphs(50), new PdfOptions(), cancelled.Token));
        Assert.That(HtmlDocumentReader.PooledTokens?.Count ?? 0, Is.Zero);
    }

    [Test]
    public void ListLargerThanTheLimitIsNotKept()
    {
        // 3 tokens per paragraph: far more than MaxPooledTokens references.
        HtmlDocumentReader.ReadTree(Paragraphs(HtmlDocumentReader.MaxPooledTokens), new PdfOptions { MaxElements = 100_000 }, default);
        Assert.That(HtmlDocumentReader.PooledTokens, Is.Null);
        HtmlDocumentReader.ReadTree(Page, new PdfOptions(), default);
        Assert.That(HtmlDocumentReader.PooledTokens!.Value.Capacity, Is.LessThanOrEqualTo(HtmlDocumentReader.MaxPooledTokens));
    }

    [Test]
    public void ReusedListRendersTheSameBytes()
    {
        var first = Pdf.Write().ToBytes(Page);
        Pdf.Write().ToBytes(Paragraphs(300)); // leaves a larger list in the slot
        Assert.That(Pdf.Write().ToBytes(Page), Is.EqualTo(first));
        Assert.That(Pdf.Write().ToBytes("<p>A</p>"), Is.EqualTo(Pdf.Write().ToBytes("<p>A</p>")));
    }

    [Test]
    public void ThreadsRenderTheSameBytesConcurrently()
    {
        var expected = Pdf.Write().ToBytes(Page);
        var results = new byte[8][];
        Parallel.For(0, results.Length, i =>
        {
            for (var round = 0; round < 5; round++) results[i] = Pdf.Write().ToBytes(round % 2 == 0 ? Page : Paragraphs(20 + i));
            results[i] = Pdf.Write().ToBytes(Page);
        });
        foreach (var result in results) Assert.That(result, Is.EqualTo(expected));
    }
}
