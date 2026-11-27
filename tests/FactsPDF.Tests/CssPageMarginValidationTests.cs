using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>@page margins and sizes are validated like the writer's own page options.</summary>
[TestFixture]
public sealed class CssPageMarginValidationTests
{
    private const string Body = "<p>First line of the report</p>";

    private static FactsPdfException Fails(string css)
    {
        using var output = new MemoryStream();
        output.Write("KEEP"u8);
        var error = Assert.Throws<FactsPdfException>(
            () => PdfConverter.Convert("<style>" + css + "</style>" + Body, output, new PdfOptions()));
        Assert.That(error!.Code, Is.EqualTo("FPDF1202"), error.Message);
        Assert.That(Encoding.ASCII.GetString(output.ToArray()), Is.EqualTo("KEEP"));
        return error;
    }

    [TestCase("@page{margin-top:-10pt}")]
    [TestCase("@page{margin-right:-10pt}")]
    [TestCase("@page{margin-bottom:-10pt}")]
    [TestCase("@page{margin-left:-10pt}")]
    [TestCase("@page{margin:-1px}")]
    [TestCase("@page{margin:10pt -0.5mm}")]
    [TestCase("@page{margin:10pt 10pt 10pt -2em}")]
    [TestCase("@media print{@page{margin-left:-1rem}}")]
    public void NegativeMarginOnAnySideFails(string css)
        => Assert.That(Fails(css).Message, Does.Contain("nonnegative"));

    [TestCase("@page{margin:1e400pt}")]
    [TestCase("@page{margin-top:1e308in}")]
    [TestCase("@page{margin-left:-1e400pt}")]
    [TestCase("@page{size:1e400pt 200pt}")]
    [TestCase("@page{size:200pt 1e400pt}")]
    public void NonFiniteOrHugeMarginsAndSizesFail(string css) => Fails(css);

    [TestCase("@page{size:240pt 120pt;margin-top:60pt;margin-bottom:60pt}")]
    [TestCase("@page{size:240pt 120pt;margin-left:150pt;margin-right:90pt}")]
    [TestCase("@page{size:240pt 120pt;margin:60pt}")]
    [TestCase("@page{margin:100000pt}")]
    public void MarginsThatLeaveNoContentAreaFail(string css) => Fails(css);

    [TestCase("@page{size:240pt 120pt;margin:20em}")]
    [TestCase("@page{size:240pt 120pt;margin-top:5rem;margin-bottom:5rem}")]
    public void RelativeMarginsMustStillLeaveAPageArea(string css) => Fails(css);

    [Test]
    public void ZeroAndPositiveMarginsStillRender()
    {
        using var output = new MemoryStream();
        PdfConverter.Convert("<style>@page{size:240pt 120pt;margin:0 10pt 20pt 0}</style>" + Body, output, new PdfOptions());
        Assert.That(output.Length, Is.GreaterThan(0));
    }
}
