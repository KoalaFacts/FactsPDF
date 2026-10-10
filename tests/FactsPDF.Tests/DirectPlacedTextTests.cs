using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

[TestFixture]
public sealed class DirectPlacedTextTests
{
    private static readonly PdfOptions Page = new()
    {
        PageWidth = 300, PageHeight = 500, Margin = 20,
        MaxPages = 10000, MaxDisplayCommands = 20000
    };
    private static readonly TextStyle Style = new(12, 1.2, new Rgb(0, 0, 0), TextAlignment.Left);

    private static Paragraph Paragraph(string text)
    {
        var p = new Paragraph(Style);
        p.Runs.Add(new TextRun(text, Style));
        return p;
    }

    [Test]
    public void LongParagraphAllocatesFinalStringsWithoutPerRunBuilders()
    {
        var input = new List<Paragraph> { Paragraph(string.Concat(Enumerable.Repeat("A ", 100000))) };
        TextLayout.Layout([Paragraph("A")], Page, default);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var pages = TextLayout.Layout(input, Page, default);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.That(pages.Sum(p => p.Runs.Sum(r => r.Text.Count(c => c == 'A'))), Is.EqualTo(100000));
        Assert.That(pages.Count, Is.GreaterThan(1));
        TestContext.WriteLine($"M12 long-paragraph managed allocation: {allocated} bytes");
        Assert.That(allocated, Is.LessThan(2L * 1024 * 1024),
            "Build only the independent final string for each placed run, without intermediate char buffers.");
    }

    [TestCase("A中𠀀文B")]
    [TestCase("𠀀𠀀A𠀀B𠀀")]
    [TestCase("A\u00A0B")]
    [TestCase("（中）文，A。")]
    public void MixedBmpAndSupplementaryScalarsRemainExact(string text)
    {
        var options = Page with { Fonts = [PdfFont.LoadTrueType(FontFixture.Create())] };
        var runs = TextLayout.Layout([Paragraph(text)], options, default).SelectMany(p => p.Runs).ToArray();
        Assert.That(string.Concat(runs.Select(r => r.Text)), Is.EqualTo(text));
        Assert.That(runs.All(r => !r.Text.Contains('\0') && !r.Text.Contains('\uFFFD')), Is.True);
        Assert.That(string.Concat(runs.Select(r => r.Text)).EnumerateRunes().Count(),
            Is.EqualTo(text.EnumerateRunes().Count()));
    }

    [Test]
    public void StylesAndFontsRemainSeparateWhileSurrogatePairsRemainWhole()
    {
        var latin = PdfFont.LoadTrueType(FontFixture.Create(latinOnly: true));
        var unicode = PdfFont.LoadTrueType(FontFixture.Create());
        var red = Style with { Color = new Rgb(1, 0, 0) };
        var p = new Paragraph(Style);
        p.Runs.Add(new TextRun("A𠀀", Style));
        p.Runs.Add(new TextRun("中B", red));
        var runs = TextLayout.Layout([p], Page with { Fonts = [latin, unicode] }, default)[0].Runs;
        Assert.That(runs.Select(r => r.Text), Is.EqualTo(new[] { "A", "𠀀", "中", "B" }));
        Assert.That(runs.Select(r => r.Font), Is.EqualTo(new[] { latin, unicode, unicode, latin }));
        Assert.That(runs.Select(r => r.Style.Color), Is.EqualTo(new[] { Style.Color, Style.Color, red.Color, red.Color }));
        Assert.That(runs[1].X, Is.EqualTo(runs[0].X + 7.2).Within(0.00001));
        Assert.That(runs[2].X, Is.EqualTo(runs[1].X + 12).Within(0.00001));
    }

    [Test]
    public void EarlierPlacedStringsSurviveLaterLineBufferReuse()
    {
        const string text = "𠀀中𠀀文𠀀A𠀀B𠀀中文𠀀";
        var options = Page with { PageWidth = 65, Fonts = [PdfFont.LoadTrueType(FontFixture.Create())] };
        var runs = TextLayout.Layout([Paragraph(text)], options, default).SelectMany(p => p.Runs).ToArray();
        Assert.That(runs.Length, Is.GreaterThan(3));
        Assert.That(string.Concat(runs.Select(r => r.Text)), Is.EqualTo(text));
        Assert.That(runs.All(r => r.Text.EnumerateRunes().All(rune => rune.Value != 0xFFFD)), Is.True);
    }

    [Test]
    public void ActualUnicodePdfPreservesSupplementaryToUnicodeMapping()
    {
        using var output = new MemoryStream();
        var result = PdfConverter.Convert("<p>A中𠀀B</p>", output,
            Page with { Fonts = [PdfFont.LoadTrueType(FontFixture.Create())] });
        var pdf = Encoding.Latin1.GetString(output.ToArray());
        Assert.That(result.PageCount, Is.EqualTo(1));
        Assert.That(pdf, Does.Contain("/ToUnicode"));
        Assert.That(pdf, Does.Contain("D840DC00"));
    }

    [TestCase(0xD800)]
    [TestCase(0xDC00)]
    public void InvalidInputStillFailsBeforeWritingAnyCallerBytes(int surrogate)
    {
        using var output = new MemoryStream();
        output.Write("keep"u8);
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(
            "<p>A" + (char)surrogate + "B</p>", output, Page));
        Assert.That(error!.Code, Is.EqualTo("FPDF1304"));
        Assert.That(Encoding.ASCII.GetString(output.ToArray()), Is.EqualTo("keep"));
    }
}
