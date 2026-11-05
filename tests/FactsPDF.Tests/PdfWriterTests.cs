using System.Buffers;
using NUnit.Framework;

namespace FactsPDF.Tests;

[TestFixture]
public sealed class PdfWriterTests
{
    private const string Html = "<h1>AB</h1><p>AB Wi 中文</p><p><b>AB</b> <i>Wi</i></p><img src=\"logo\">";
    private static readonly byte[] FontBytes = FontFixture.Create(listDigits: true);
    private static readonly PdfFont Font = PdfFont.FromBytes(FontBytes);
    private static readonly PdfImage Logo = PdfImage.FromBytes(PngFixture.Image([0, 10, 20, 30, 128], 1));

    private string directory = null!;

    [SetUp]
    public void SetUp() => directory = Directory.CreateTempSubdirectory("factspdf-writer-").FullName;

    [TearDown]
    public void TearDown() => Directory.Delete(directory, true);

    private static PdfWriter Configured() => Pdf.Write()
        .WithFonts(f => f.Regular(Font).Bold(Font).Italic(Font).BoldItalic(Font).Size(11).Subset())
        .WithImage("logo", Logo)
        .WithPage(p => p.Letter().Landscape().Margin(0.5, PdfUnit.Inch))
        .WithMetadata(m => m.Title("AB").Author("Koala Facts Trading Pty Ltd").Subject("S").Keywords("K"))
        .WithPageFurniture(f => f.HeaderRight("{page}.{pages}", t => t.Size(9).Bold().Color(1, 2, 3)).FooterLeft("AB"))
        .WithLimits(l => l.MaxPages(50).MaxElements(500));

    private static readonly PdfOptions Equivalent = new()
    {
        Fonts = [Font], BoldFonts = [Font], ItalicFonts = [Font], BoldItalicFonts = [Font], FontSize = 11, SubsetFonts = true,
        Images = new Dictionary<string, PdfImage> { ["logo"] = Logo },
        PageWidth = 792, PageHeight = 612, Margin = 36,
        Title = "AB", Author = "Koala Facts Trading Pty Ltd", Subject = "S", Keywords = "K",
        PageFurniture = new PdfPageFurniture
        {
            Default = new PdfPageFurnitureRegions
            {
                HeaderRight = new PdfPageText { Template = "{page}.{pages}", FontSize = 9, Bold = true, Color = new PdfRgbColor(1, 2, 3) },
                FooterLeft = new PdfPageText { Template = "AB" },
            },
        },
        MaxPages = 50, MaxElements = 500,
    };

    private static byte[] Engine(string html, PdfOptions options)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, options);
        return output.ToArray();
    }

    [Test]
    public void BuilderProducesTheSameBytesAsTheEquivalentEngineOptions()
        => Assert.That(Configured().ToBytes(Html), Is.EqualTo(Engine(Html, Equivalent)));

    [Test]
    public void DefaultWriterMatchesDefaultEngineOptions()
        => Assert.That(Pdf.Write().ToBytes("<p>Report</p>"), Is.EqualTo(Engine("<p>Report</p>", new PdfOptions())));

    [Test]
    public void LegacyMarginTextMatchesTheEngine()
    {
        var bytes = Pdf.Write().WithHeaderText("Service report").WithFooterText("Internal").WithPageNumbers().ToBytes("<p>Report</p>");
        Assert.That(bytes, Is.EqualTo(Engine("<p>Report</p>", new PdfOptions { HeaderText = "Service report", FooterText = "Internal", PageNumbers = true })));
    }

    [Test]
    public void WithMethodsReturnNewWritersAndNeverChangeTheOriginal()
    {
        var plain = Pdf.Write();
        var numbered = plain.WithPageNumbers();
        Assert.That(numbered, Is.Not.SameAs(plain));
        Assert.That(plain.ToBytes("<p>x</p>"), Is.EqualTo(Pdf.Write().ToBytes("<p>x</p>")));
        Assert.That(numbered.ToBytes("<p>x</p>"), Is.Not.EqualTo(plain.ToBytes("<p>x</p>")));
        Assert.That(Pdf.Write(), Is.SameAs(plain), "Pdf.Write() returns the shared immutable defaults.");
    }

    [Test]
    public async Task EveryOutputWritesIdenticalBytesAndReportsThem()
    {
        var writer = Configured();
        var expected = writer.ToBytes(Html);

        using var stream = new MemoryStream();
        var streamed = writer.ToStream(Html, stream);
        using var asyncStream = new AsyncOnlyStream();
        var streamedAsync = await writer.ToStreamAsync(Html, asyncStream);
        var buffer = new ArrayBufferWriter<byte>();
        var buffered = writer.ToBufferWriter(Html, buffer);
        var file = Path.Combine(directory, "report.pdf");
        var saved = writer.ToFile(Html, file);
        var asyncFile = Path.Combine(directory, "async.pdf");
        var savedAsync = await writer.ToFileAsync(Html, asyncFile);

        Assert.That(stream.ToArray(), Is.EqualTo(expected));
        Assert.That(asyncStream.ToArray(), Is.EqualTo(expected));
        Assert.That(buffer.WrittenSpan.ToArray(), Is.EqualTo(expected));
        Assert.That(File.ReadAllBytes(file), Is.EqualTo(expected));
        Assert.That(File.ReadAllBytes(asyncFile), Is.EqualTo(expected));
        foreach (var result in new[] { streamed, streamedAsync, buffered, saved, savedAsync })
        {
            Assert.That(result.BytesWritten, Is.EqualTo(expected.Length));
            Assert.That(result.PageCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void FailedFileWriteKeepsTheExistingFileAndLeavesNoTemporaryFile()
    {
        var file = Path.Combine(directory, "report.pdf");
        byte[] sentinel = [1, 2, 3];
        File.WriteAllBytes(file, sentinel);
        Assert.Throws<FactsPdfException>(() => Pdf.Write().ToFile("<script>x</script>", file));
        Assert.ThrowsAsync<FactsPdfException>(() => Pdf.Write().ToFileAsync("<script>x</script>", file));
        Assert.That(File.ReadAllBytes(file), Is.EqualTo(sentinel));
        Assert.That(Directory.GetFiles(directory), Is.EqualTo(new[] { file }));
    }

    [Test]
    public void SuccessfulFileWriteReplacesTheExistingFile()
    {
        var file = Path.Combine(directory, "report.pdf");
        File.WriteAllBytes(file, [1, 2, 3]);
        Pdf.Write().ToFile("<p>x</p>", file);
        Assert.That(File.ReadAllBytes(file), Is.EqualTo(Pdf.Write().ToBytes("<p>x</p>")));
        Assert.That(Directory.GetFiles(directory), Has.Length.EqualTo(1));
    }

    [Test]
    public void FailedStreamWritesNothing()
    {
        using var stream = new MemoryStream();
        Assert.Throws<FactsPdfException>(() => Pdf.Write().ToStream("<script>x</script>", stream));
        var buffer = new ArrayBufferWriter<byte>();
        Assert.Throws<FactsPdfException>(() => Pdf.Write().ToBufferWriter("<script>x</script>", buffer));
        Assert.That(stream.Length, Is.Zero);
        Assert.That(buffer.WrittenCount, Is.Zero);
    }

    [Test]
    public void LandscapeAndPortraitDoNotDependOnCallOrder()
    {
        string html = "<p>x</p>";
        var expected = Engine(html, new PdfOptions { PageWidth = 792, PageHeight = 612 });
        Assert.That(Pdf.Write().WithPage(p => p.Letter().Landscape()).ToBytes(html), Is.EqualTo(expected));
        Assert.That(Pdf.Write().WithPage(p => p.Landscape().Letter()).ToBytes(html), Is.EqualTo(expected));
        Assert.That(Pdf.Write().WithPage(p => p.Size(792, 612).Portrait()).ToBytes(html),
            Is.EqualTo(Engine(html, new PdfOptions { PageWidth = 612, PageHeight = 792 })));
    }

    [TestCase(210, 297, PdfUnit.Millimetre, 595.2755905511812, 841.8897637795276)]
    [TestCase(21, 29.7, PdfUnit.Centimetre, 595.2755905511812, 841.8897637795275)]
    [TestCase(8.5, 11, PdfUnit.Inch, 612, 792)]
    [TestCase(816, 1056, PdfUnit.Pixel, 612, 792)]
    public void PageUnitsConvertToPoints(double width, double height, PdfUnit unit, double points, double pointsHigh)
    {
        var options = Pdf.Write().WithPage(p => p.Size(width, height, unit).Margin(10, PdfUnit.Millimetre)).Options;
        Assert.That(options.PageWidth, Is.EqualTo(points).Within(1e-9));
        Assert.That(options.PageHeight, Is.EqualTo(pointsHigh).Within(1e-9));
        Assert.That(options.Margin, Is.EqualTo(10 * 72 / 25.4).Within(1e-9));
    }

    [Test]
    public void InvalidSettingsFailAtConfigurationWithTheParameterName()
    {
        Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => Pdf.Write().WithPage(p => p.Size(-1, 100)))!.ParamName, Is.EqualTo("width"));
        Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => Pdf.Write().WithPage(p => p.Size(100, 20_000)))!.ParamName, Is.EqualTo("height"));
        Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => Pdf.Write().WithPage(p => p.Margin(double.NaN)))!.ParamName, Is.EqualTo("value"));
        Assert.Throws<ArgumentOutOfRangeException>(() => Pdf.Write().WithPage(p => p.A4().Margin(300)));
        Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => Pdf.Write().WithFonts(f => f.Size(0)))!.ParamName, Is.EqualTo("points"));
        Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => Pdf.Write().WithLimits(l => l.MaxPages(0)))!.ParamName, Is.EqualTo("value"));
        Assert.That(Assert.Throws<ArgumentException>(() => Pdf.Write().WithImage("bad name", Logo))!.ParamName, Is.EqualTo("name"));
        Assert.Throws<ArgumentNullException>(() => Pdf.Write().WithFonts(f => f.Regular(null!)));
        Assert.Throws<ArgumentNullException>(() => Pdf.Write().WithFonts(f => f.Regular(Font, null!)));
    }

    [Test]
    public void SameImageNameReplacesTheEarlierImage()
    {
        var other = PdfImage.FromBytes(PngFixture.Image([0, 1, 2, 3, 4, 0, 5, 6, 7, 8], 1, 2));
        var writer = Pdf.Write().WithImage("logo", Logo).WithImage("logo", other);
        Assert.That(writer.Options.Images, Has.Count.EqualTo(1));
        Assert.That(writer.Options.Images["logo"], Is.SameAs(other));
    }

    [Test]
    public void FontChainsAreCopiedSoLaterCallerChangesHaveNoEffect()
    {
        PdfFont[] chain = [Font];
        var writer = Pdf.Write().WithFonts(f => f.Regular(chain));
        var expected = writer.ToBytes("<p>AB</p>");
        chain[0] = null!;
        Assert.That(writer.ToBytes("<p>AB</p>"), Is.EqualTo(expected));
    }

    [Test]
    public void FontsAndImagesLoadFromFiles()
    {
        var fontPath = Path.Combine(directory, "font.ttf");
        var imagePath = Path.Combine(directory, "logo.png");
        File.WriteAllBytes(fontPath, FontBytes);
        File.WriteAllBytes(imagePath, PngFixture.Image([0, 10, 20, 30, 128], 1));
        var fromFiles = Pdf.Write().WithFonts(f => f.Regular(PdfFont.FromFile(fontPath))).WithImage("logo", PdfImage.FromFile(imagePath));
        var fromBytes = Pdf.Write().WithFonts(f => f.Regular(Font)).WithImage("logo", Logo);
        const string html = "<p>AB 中文</p><img src=\"logo\">";
        Assert.That(fromFiles.ToBytes(html), Is.EqualTo(fromBytes.ToBytes(html)));
        Assert.Throws<FileNotFoundException>(() => PdfFont.FromFile(Path.Combine(directory, "missing.ttf")));
    }

    [Test]
    public void FirstPageFurnitureReplacesTheDefaultRegionsOnPageOne()
    {
        var furniture = Pdf.Write().WithPageFurniture(f => f.FooterCenter("{page}").FirstPage(p => p.HeaderCenter("Cover"))).Options.PageFurniture!;
        Assert.That(furniture.Default.FooterCenter!.Template, Is.EqualTo("{page}"));
        Assert.That(furniture.FirstPage!.HeaderCenter!.Template, Is.EqualTo("Cover"));
        Assert.That(furniture.FirstPage.FooterCenter, Is.Null);
    }

    [Test]
    public void OneSharedWriterRendersConcurrentlyWithIdenticalOutput()
    {
        var writer = Configured();
        var expected = writer.ToBytes(Html);
        var outputs = new byte[32][];
        Parallel.For(0, outputs.Length, new ParallelOptions { MaxDegreeOfParallelism = 8 }, i => outputs[i] = writer.ToBytes(Html));
        Assert.That(outputs, Has.All.EqualTo(expected));
    }

    // Like an ASP.NET Core response body with synchronous I/O disabled.
    private sealed class AsyncOnlyStream : Stream
    {
        private readonly MemoryStream inner = new();
        public byte[] ToArray() => inner.ToArray();
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new InvalidOperationException("Synchronous writes are disallowed.");
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            inner.Write(buffer.Span);
            return ValueTask.CompletedTask;
        }
        protected override void Dispose(bool disposing) { if (disposing) inner.Dispose(); base.Dispose(disposing); }
    }
}
