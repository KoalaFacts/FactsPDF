using System.Text;
using FactsPDF.CommandLine;
using NUnit.Framework;

namespace FactsPDF.Tests;

// Encoding sniffing for bytes (the CLI) as Chrome does it (HTML Standard 13.2.3): a byte order
// mark first, then the meta prescan, then UTF-8. FactsPDF decodes UTF-8, UTF-16LE, UTF-16BE and
// windows-1252 (latin1, iso-8859-1, ascii); any other declared encoding fails FPDF1103 instead of
// decoding the bytes as something else.
[TestFixture]
public sealed class HtmlEncodingTests
{
    private static byte[] Latin1(string text) => Encoding.Latin1.GetBytes(text);

    [Test]
    public void ByteOrderMarksWinAndAreRemoved()
    {
        Assert.Multiple(() =>
        {
            Assert.That(HtmlEncoding.Decode([0xEF, 0xBB, 0xBF, .. "<p>A</p>"u8]), Is.EqualTo("<p>A</p>"));
            Assert.That(HtmlEncoding.Decode([0xFF, 0xFE, .. Encoding.Unicode.GetBytes("<p>A</p>")]), Is.EqualTo("<p>A</p>"));
            Assert.That(HtmlEncoding.Decode([0xFE, 0xFF, .. Encoding.BigEndianUnicode.GetBytes("<p>A</p>")]), Is.EqualTo("<p>A</p>"));
            // A BOM wins over a meta charset that names another encoding.
            Assert.That(HtmlEncoding.Decode([0xEF, 0xBB, 0xBF, .. Latin1("<meta charset=windows-1252><p>\u00E9</p>".Replace("\u00E9", "\u00C3\u00A9"))]),
                Is.EqualTo("<meta charset=windows-1252><p>\u00E9</p>"));
        });
    }

    [Test]
    public void AMetaCharsetSelectsWindows1252WithTheEncodingStandardIndex()
    {
        // 0x80 is the euro sign, 0x81 the C1 control U+0081 (the Encoding Standard index), 0xE9 e-acute.
        var bytes = Latin1("<!doctype html><meta charset=\"latin1\"><p>caf\u00E9 \u0080\u0081\u009f</p>");
        Assert.That(HtmlEncoding.Decode(bytes), Is.EqualTo("<!doctype html><meta charset=\"latin1\"><p>caf\u00E9 \u20AC\u0081\u0178</p>"));
    }

    [TestCase("<meta http-equiv=\"Content-Type\" content=\"text/html; charset=ISO-8859-1\">", "Windows1252")]
    [TestCase("<meta content='text/html;charset=\"windows-1252\"' http-equiv=content-type>", "Windows1252")]
    [TestCase("<META CHARSET=UTF-8>", "Utf8")]
    // A meta element cannot switch to UTF-16: the bytes it is written in are ASCII-compatible.
    [TestCase("<meta charset=utf-16le>", "Utf8")]
    // Comments and other tags are skipped; the first declaration wins.
    [TestCase("<!-- <meta charset=windows-1252> --><meta charset=utf-8><meta charset=latin1>", "Utf8")]
    [TestCase("<title x='<meta charset=latin1>'>T</title><meta charset=ascii>", "Windows1252")]
    public void ThePrescanFindsTheFirstDeclaration(string head, string expected)
        => Assert.That(HtmlEncoding.Prescan(Encoding.ASCII.GetBytes(head))?.ToString(), Is.EqualTo(expected));

    [TestCase("<p>A</p>")]
    // A content charset without http-equiv=content-type declares nothing.
    [TestCase("<meta content='text/html; charset=latin1'>")]
    [TestCase("<meta http-equiv=refresh content='text/html; charset=latin1'>")]
    public void WithoutADeclarationThePrescanFindsNothing(string head)
        => Assert.That(HtmlEncoding.Prescan(Encoding.ASCII.GetBytes(head)), Is.Null);

    [Test]
    public void PastTheFirstKilobyteThePrescanStopsAtBodyContent()
    {
        var padding = "<meta name=description content='" + new string('x', 1100) + "'>";
        Assert.Multiple(() =>
        {
            // Still in head: Chrome keeps looking, and so does FactsPDF.
            Assert.That(HtmlEncoding.Prescan(Encoding.ASCII.GetBytes(padding + "<meta charset=latin1>")), Is.EqualTo(HtmlEncodingKind.Windows1252));
            // After a body element starts, a later meta charset no longer decides the encoding.
            Assert.That(HtmlEncoding.Prescan(Encoding.ASCII.GetBytes(padding + "<p>A</p><meta charset=latin1>")), Is.Null);
        });
    }

    [TestCase("<meta charset=shift_jis>")]
    [TestCase("<meta charset=iso-8859-2>")]
    [TestCase("<meta http-equiv=content-type content='text/html; charset=gb18030'>")]
    [TestCase("<meta charset=bogus-label>")]
    public void AnEncodingFactsPdfDoesNotDecodeFails(string head)
    {
        var error = Assert.Throws<FactsPdfException>(() => HtmlEncoding.Decode(Encoding.ASCII.GetBytes(head + "<p>A</p>")))!;
        Assert.That(error.Code, Is.EqualTo("FPDF1103"));
    }

    [Test]
    public void MalformedBytesFailInsteadOfPrintingReplacementCharacters()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Assert.Throws<DecoderFallbackException>(() => HtmlEncoding.Decode([0xC3, 0x28]))!.Message, Is.EqualTo("Input must be valid UTF-8."));
            Assert.That(Assert.Throws<DecoderFallbackException>(() => HtmlEncoding.Decode([0xFF, 0xFE, 0x41]))!.Message, Is.EqualTo("Input must be valid UTF-16LE."));
        });
    }

    [TestCase("utf8", "Utf8")]
    [TestCase(" Unicode-1-1-UTF-8\t", "Utf8")]
    [TestCase("UTF-16", "Utf16LittleEndian")]
    [TestCase("unicodeFFFE", "Utf16BigEndian")]
    [TestCase("US-ASCII", "Windows1252")]
    [TestCase("iso_8859-1:1987", "Windows1252")]
    [TestCase("x-cp1252", "Windows1252")]
    [TestCase("koi8-r", null)]
    [TestCase("utf-7", null)]
    [TestCase("", null)]
    public void LabelsFollowTheEncodingStandard(string label, string? expected)
        => Assert.That(HtmlEncoding.Label(label)?.ToString(), Is.EqualTo(expected));

    private static (int Exit, byte[] Output, string Error) Cli(byte[] input)
    {
        using var stdin = new MemoryStream(input);
        using var stdout = new MemoryStream();
        using var stderr = new StringWriter();
        var exit = CliApplication.Run(["-", "-"], stdin, stdout, stderr);
        return (exit, stdout.ToArray(), stderr.ToString());
    }

    [Test]
    public void TheCliDecodesUtf16AndDeclaredWindows1252()
    {
        var utf8 = Cli("<p>Total</p>"u8.ToArray());
        var utf16 = Cli([0xFF, 0xFE, .. Encoding.Unicode.GetBytes("<p>Total</p>")]);
        var declared = Cli(Latin1("<meta charset=windows-1252><p>Total</p>"));
        var plain = Cli("<meta charset=utf-8><p>Total</p>"u8.ToArray());
        Assert.Multiple(() =>
        {
            Assert.That(utf8.Exit, Is.Zero);
            Assert.That(utf16.Output, Is.EqualTo(utf8.Output));
            Assert.That(declared.Output, Is.EqualTo(plain.Output));
        });
    }

    [Test]
    public void TheCliRejectsUndecodableInputWithoutOutput()
    {
        var malformed = Cli([0xC3, 0x28]);
        var unsupported = Cli("<meta charset=shift_jis><p>A</p>"u8.ToArray());
        Assert.Multiple(() =>
        {
            Assert.That(malformed.Exit, Is.EqualTo(3));
            Assert.That(malformed.Output, Is.Empty);
            Assert.That(malformed.Error, Does.Contain("Input must be valid UTF-8."));
            Assert.That(unsupported.Exit, Is.EqualTo(3));
            Assert.That(unsupported.Output, Is.Empty);
            Assert.That(unsupported.Error, Does.Contain("FPDF1103"));
        });
    }
}
