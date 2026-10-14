using FactsPDF.WasmProbe;
using NUnit.Framework;

namespace FactsPDF.Tests;

public class WasmProbeTests
{
    [Test]
    public void AsciiBytesEqualTheExistingCore()
    {
        const string html = "<p>Node CLI probe 2026.</p>";
        var result = ProbeConversion.Run(html, [], [], false);
        using var expected = new MemoryStream();
        var converted = PdfConverter.Convert(html, expected);
        Assert.Multiple(() =>
        {
            Assert.That(result.ErrorCode, Is.Null);
            Assert.That(Convert.FromBase64String(result.PdfBase64!), Is.EqualTo(expected.ToArray()));
            Assert.That(result.PageCount, Is.EqualTo(converted.PageCount));
            Assert.That(result.ManagedHeapBeforeBytes, Is.GreaterThanOrEqualTo(0));
            Assert.That(result.ManagedHeapAfterBytes, Is.GreaterThanOrEqualTo(0));
        });
    }

    [TestCase("<p>报告</p>", "FPDF1301")]
    [TestCase("<img src=\"explicit.jpg\">", "FPDF1102")]
    public void CoreFailuresReturnDiagnosticWithoutPdf(string html, string code)
    {
        var result = ProbeConversion.Run(html, [], [], false);
        Assert.Multiple(() =>
        {
            Assert.That(result.ErrorCode, Is.EqualTo(code));
            Assert.That(result.PdfBase64, Is.Null);
            Assert.That(result.PageCount, Is.Zero);
            Assert.That(result.ErrorMessage, Does.Contain(code));
        });
    }

    [Test]
    public void CorruptExplicitFontDoesNotFallBackToAscii()
    {
        var result = ProbeConversion.Run("<p>ASCII</p>", [1, 2, 3], [], true);
        Assert.That(result.ErrorCode, Does.StartWith("FPDF15"));
        Assert.That(result.PdfBase64, Is.Null);
    }

    [Test]
    public void FailureDoesNotPoisonNextConversion()
    {
        var before = ProbeConversion.Run("<p>Independent.</p>", [], [], false);
        var failure = ProbeConversion.Run("<p>报告</p>", [], [], false);
        var after = ProbeConversion.Run("<p>Independent.</p>", [], [], false);
        Assert.That(failure.PdfBase64, Is.Null);
        Assert.That(after.ErrorCode, Is.Null);
        Assert.That(after.PdfBase64, Is.EqualTo(before.PdfBase64));
    }
}
