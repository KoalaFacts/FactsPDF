using System.Text;
using FactsPDF.CommandLine;
using NUnit.Framework;

namespace FactsPDF.Tests;

[TestFixture]
public sealed class CliTaggedPdfTests
{
    private const string Page = "<html lang=\"en\"><head><title>Quarterly report</title></head><body><p>Invoice A</p></body></html>";

    private static (int Exit, byte[] Pdf, string Stderr) Render(params string[] args)
    {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(Page));
        using var output = new MemoryStream();
        using var error = new StringWriter();
        var code = CliApplication.Run(args, input, output, error);
        return (code, output.ToArray(), error.ToString());
    }

    [Test]
    public void TheCliWritesTaggedPdfByDefault()
    {
        var result = Render("-", "-");
        Assert.That(result.Exit, Is.Zero, result.Stderr);
        var text = Encoding.Latin1.GetString(result.Pdf);
        Assert.That(text, Does.Contain("/StructTreeRoot").And.Contain("/MarkInfo").And.Contain("/Lang (en)"));
        Assert.That(text, Does.Contain("/Title <FEFF" + Convert.ToHexString(Encoding.BigEndianUnicode.GetBytes("Quarterly report")) + ">"));
        using var expected = new MemoryStream();
        PdfConverter.Convert(Page, expected, new PdfOptions());
        Assert.That(result.Pdf, Is.EqualTo(expected.ToArray()));
    }

    [Test]
    public void UntaggedWritesTheUntaggedPdfOfEarlierReleases()
    {
        var result = Render("-", "-", "--untagged");
        Assert.That(result.Exit, Is.Zero, result.Stderr);
        var text = Encoding.Latin1.GetString(result.Pdf);
        Assert.That(text, Does.Not.Contain("StructTreeRoot").And.Not.Contain("BDC").And.Not.Contain("/Info"));
        using var expected = new MemoryStream();
        PdfConverter.Convert(Page, expected, new PdfOptions { Tagged = false });
        Assert.That(result.Pdf, Is.EqualTo(expected.ToArray()));
    }

    [Test]
    public void UntaggedIsListedInTheUsageAndCannotRepeat()
    {
        Assert.That(Encoding.UTF8.GetString(Render("--help").Pdf), Does.Contain("[--untagged]").And.Contain("--untagged writes an untagged PDF"));
        var repeated = Render("-", "-", "--untagged", "--untagged");
        Assert.That(repeated.Exit, Is.EqualTo(2));
        Assert.That(repeated.Stderr, Does.Contain("Usage:"));
    }
}
