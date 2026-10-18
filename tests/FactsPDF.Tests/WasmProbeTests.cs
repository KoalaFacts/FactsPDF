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
    [TestCase("<img src=\"explicit.jpg\">", "FPDF1604")]
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

    // Reflection keeps the first test executable before the private adapter exists.
    private static ProbeResult Styles(string html, byte[] packed, int[] lengths, int[] roles, bool subset = true)
    {
        var method = typeof(ProbeConversion).GetMethod("RunStyles", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, "Private four-role adapter is required.");
        return (ProbeResult)method!.Invoke(null, [html, packed, lengths, roles, subset])!;
    }

    [Test]
    public void ExplicitStyleBytesMatchCoreAndRecoverAfterMissingRole()
    {
        var font = FontFixture.Create();
        var packed = Enumerable.Range(0, 4).SelectMany(_ => font).ToArray();
        const string html = "<p>A<b>中</b><em>A</em><strong><em>文</em></strong></p>";
        var actual = Styles(html, packed, Enumerable.Repeat(font.Length, 4).ToArray(), [0, 1, 2, 3]);
        using var expected = new MemoryStream();
        PdfFont Load() => PdfFont.LoadTrueType(font);
        PdfConverter.Convert(html, expected, new() { Fonts = [Load()], BoldFonts = [Load()], ItalicFonts = [Load()], BoldItalicFonts = [Load()], SubsetFonts = true });
        Assert.That(actual.ErrorCode, Is.Null);
        Assert.That(Convert.FromBase64String(actual.PdfBase64!), Is.EqualTo(expected.ToArray()));
        var missing = Styles(html, font, [font.Length], [1]);
        Assert.That(missing.ErrorCode, Is.EqualTo("FPDF1507"));
        Assert.That(missing.PdfBase64, Is.Null);
        var recovered = Styles(html, packed, Enumerable.Repeat(font.Length, 4).ToArray(), [0, 1, 2, 3]);
        Assert.That(recovered.PdfBase64, Is.EqualTo(actual.PdfBase64));
    }

    [Test]
    public void PackedStyleProtocolRejectsInvalidConfigurationWithoutPdf()
    {
        foreach (var (data, lengths, roles) in new (byte[], int[], int[])[] {
            ([], [1], [1]), ([1], [], []), ([1], [1], [4]), ([1], [1], []),
            ([1], [-1, 2], [1, 2]), ([1], [int.MaxValue, int.MaxValue], [1, 2]),
            (new byte[9], Enumerable.Repeat(1, 9).ToArray(), new int[9]) })
        {
            var result = Styles("A", data, lengths, roles);
            Assert.That(result.ErrorCode, Is.EqualTo("B2_INPUT_FONTS"));
            Assert.That(result.PdfBase64, Is.Null);
        }
        var corrupt = Styles("ASCII", [1, 2, 3], [3], [3]);
        Assert.That(corrupt.ErrorCode, Is.EqualTo("FPDF1501"));
        Assert.That(corrupt.PdfBase64, Is.Null);
        Assert.That(Styles("ASCII", [], [], []).ErrorCode, Is.Null);
    }

    [Test]
    public void StyledMissingGlyphReturnsOriginalCoreDiagnostic()
    {
        var font = FontFixture.Create(latinOnly: true);
        var result = Styles("<p><i>中</i></p>", font, [font.Length], [2]);
        Assert.That(result.ErrorCode, Is.EqualTo("FPDF1504"));
        Assert.That(result.ErrorMessage, Is.EqualTo("FPDF1504: No supplied italic font contains U+4E2D."));
        Assert.That(result.PdfBase64, Is.Null);
    }
}
