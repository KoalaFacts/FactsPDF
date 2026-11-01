using NUnit.Framework;

namespace FactsPDF.Tests;

public sealed class ImageStructureTests
{
    private static PdfImage Image() => PdfImage.LoadImage(JpegResourceTests.Bytes("rgb-420"));
    private static PdfOptions Options() => new() { Images = new Dictionary<string, PdfImage> { ["chart"] = Image() } };
    private static DocumentRoot Tree(string html, PdfOptions? options = null)
        => HtmlDocumentReader.ReadTree(html, ImageResources.Snapshot(options ?? Options(), default), default);

    [Test]
    public void MissingExplicitBindingHasALocatedResourceDiagnostic()
    {
        using var output = new MemoryStream();
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert("<div><img src='chart'></div>", output));
        Assert.That(error!.Code, Is.EqualTo("FPDF1604"));
        Assert.That(error.SourceOffset, Is.EqualTo(5)); Assert.That(output.Length, Is.Zero);
    }

    [Test]
    public void OptionsExposeTheExplicitNamedImageContract()
    {
        Assert.That(typeof(PdfOptions).GetProperty("Images")?.PropertyType,
            Is.EqualTo(typeof(IReadOnlyDictionary<string, PdfImage>)));
        Assert.That(typeof(PdfOptions).GetProperty("MaxImageResources"), Is.Not.Null);
        Assert.That(typeof(PdfOptions).GetProperty("MaxTotalImageBytes"), Is.Not.Null);
    }

    [Test]
    public void StandaloneVoidImagesRetainStylesResourceAndTextOrder()
    {
        const string html = "<section>Before<img src='chart' alt='Chart' class='pic'/>After</section>"
            + "<style>section > img.pic{width:50%;padding:2pt;border:1pt solid red;background-color:white}</style>";
        var root = Tree(html); var children = ((BlockNode)root.Children.Single()).Children;
        Assert.That(children, Has.Count.EqualTo(3));
        var image = (ImageNode)children[1];
        Assert.That(image.Image.PixelWidth, Is.EqualTo(320)); Assert.That(image.Alt, Is.EqualTo("Chart"));
        Assert.That(image.BoxStyle.Width!.Value.Resolve(200), Is.EqualTo(100));
        Assert.That(image.BoxStyle.PaddingTop.Resolve(200), Is.EqualTo(2));
        Assert.That(image.BoxStyle.BorderLeft.EffectiveWidth, Is.EqualTo(1));
        Assert.That(image.SourceOffset, Is.EqualTo(html.IndexOf("<img", StringComparison.Ordinal)));
        Assert.That(string.Concat(BlockTreeTraversal.Paragraphs(root, default).SelectMany(p => p.Runs).Select(r => r.Text)),
            Is.EqualTo("BeforeAfter"));
    }

    [Test]
    public void ImageAndListSiblingsKeepTheirSeparateStructure()
    {
        var root = Tree("<img src='chart'><ul><li>Item</li></ul><p>After</p>");
        Assert.That(root.HasLists, Is.True); Assert.That(root.Children[0], Is.TypeOf<ImageNode>());
        Assert.DoesNotThrow(() => ListLayout.Prepare(root, Options(), default));
        Assert.That(string.Concat(BlockTreeTraversal.Paragraphs(root, default).SelectMany(p => p.Runs).Select(r => r.Text)),
            Is.EqualTo("ItemAfter"));
    }

    [TestCase("<p>Before<img src='chart'></p>", "FPDF1101")]
    [TestCase("<span><img src='chart'></span>", "FPDF1101")]
    [TestCase("<ul><li><div><img src='chart'></div></li></ul>", "FPDF1101")]
    [TestCase("<head><img src='chart'></head>", "FPDF1101")]
    [TestCase("<img src='chart'></img>", "FPDF1101")]
    [TestCase("<img>", "FPDF1103")]
    [TestCase("<img src=''>", "FPDF1103")]
    [TestCase("<img src='https://example.invalid/chart.jpg'>", "FPDF1103")]
    [TestCase("<img src='data:image/jpeg;base64,AA=='>", "FPDF1103")]
    [TestCase("<img src='/tmp/chart.jpg'>", "FPDF1103")]
    [TestCase("<img src='chart' width='200'>", "FPDF1103")]
    [TestCase("<img src='chart' height='100'>", "FPDF1103")]
    [TestCase("<img src='chart' srcset='chart'>", "FPDF1103")]
    [TestCase("<img src='chart' style='height:100%'>", "FPDF1202")]
    [TestCase("<img src='chart' style='margin-top:1pt'>", "FPDF1201")]
    [TestCase("<img src='chart' style='break-before:page'>", "FPDF1201")]
    public void UnsupportedSyntaxAndPositionsFailExplicitly(string html, string code)
        => Assert.That(Assert.Throws<FactsPdfException>(() => Tree(html))!.Code, Is.EqualTo(code));

    [Test]
    public void DuplicateAttributePreservesTokenizerDiagnosticAndCssStillPreflights()
    {
        var error = Assert.Throws<FactsPdfException>(() => Tree("<img src=\"chart\" src=\"chart\">"));
        Assert.That(error!.Code, Is.EqualTo("FPDF1101")); Assert.That(error.SourceOffset, Is.EqualTo(17));
        Assert.That(Assert.Throws<FactsPdfException>(() => Tree("<img><style>.absent{max-height:10pt}</style>"))!.Code,
            Is.EqualTo("FPDF1201"));
    }

    [Test]
    public void ResourceMapSnapshotIsOrdinalAndIndependentOfCallerMutation()
    {
        var image = Image(); var map = new Dictionary<string, PdfImage>(StringComparer.OrdinalIgnoreCase) { ["Chart"] = image };
        var options = ImageResources.Snapshot(new() { Images = map }, default); map.Clear();
        Assert.That(options.Images["Chart"], Is.SameAs(image));
        Assert.That(options.Images.ContainsKey("chart"), Is.False);
        Assert.That(Assert.Throws<FactsPdfException>(() => HtmlDocumentReader.ReadTree("<img src='chart'>", options, default))!.Code,
            Is.EqualTo("FPDF1604"));
    }

    [Test]
    public void EveryAliasAndUnusedResourceCountsBeforeOutput()
    {
        var image = Image(); var map = new Dictionary<string, PdfImage> { ["a"] = image, ["b"] = image };
        var options = new PdfOptions { Images = map, MaxImageResources = 2, MaxTotalImageBytes = image.ByteLength * 2 };
        Assert.That(ImageResources.Snapshot(options, default).Images, Has.Count.EqualTo(2));
        foreach (var invalid in new[] { options with { MaxImageResources = 1 }, options with { MaxTotalImageBytes = image.ByteLength * 2 - 1 } })
        {
            using var output = new MemoryStream(); output.Write([1, 2, 3]); output.Position = 1;
            Assert.That(Assert.Throws<FactsPdfException>(() => PdfConverter.Convert("<p>ASCII</p>", output, invalid))!.Code,
                Is.EqualTo("FPDF1603"));
            Assert.That(output.ToArray(), Is.EqualTo(new byte[] { 1, 2, 3 })); Assert.That(output.Position, Is.EqualTo(1));
        }
    }

    [TestCase("")] [TestCase("/tmp/image.jpg")] [TestCase("图")]
    [TestCase("-chart")] [TestCase("a b")]
    public void InvalidBindingNamesAreApiMisuse(string key)
    {
        using var output = new MemoryStream();
        Assert.Throws<ArgumentException>(() => PdfConverter.Convert("A", output, new() { Images = new Dictionary<string, PdfImage> { [key] = Image() } }));
        Assert.That(output.Length, Is.Zero);
    }

    [Test]
    public void NullMapsValuesAndNonpositiveBoundsAreRejected()
    {
        foreach (var options in new[] { new PdfOptions { Images = null! }, new() { Images = new Dictionary<string, PdfImage> { ["a"] = null! } } })
        { using var output = new MemoryStream(); Assert.Throws<ArgumentException>(() => PdfConverter.Convert("A", output, options)); }
        foreach (var options in new[] { new PdfOptions { MaxImageResources = 0 }, new() { MaxTotalImageBytes = 0 } })
        { using var output = new MemoryStream(); Assert.Throws<ArgumentOutOfRangeException>(() => PdfConverter.Convert("A", output, options)); }
    }

    [Test]
    public void ImagesConsumeElementAndDepthBudgets()
    {
        Assert.That(Assert.Throws<FactsPdfException>(() => Tree("<img src='chart'><img src='chart'>",
            Options() with { MaxElements = 1 }))!.Code, Is.EqualTo("FPDF1002"));
        Assert.That(Assert.Throws<FactsPdfException>(() => Tree("<div><img src='chart'></div>",
            Options() with { MaxDepth = 1 }))!.Code, Is.EqualTo("FPDF1003"));
    }

    [Test]
    public void SnapshotStopsEnumerationAtResourceLimitPlusOne()
    {
        var source = new EndlessBindings(Image());
        Assert.That(Assert.Throws<FactsPdfException>(() => ImageResources.Snapshot(
            new() { Images = source, MaxImageResources = 2 }, default))!.Code, Is.EqualTo("FPDF1603"));
        Assert.That(source.Moves, Is.EqualTo(3));
    }

    [Test]
    public void NameLengthBoundaryAndSnapshotCancellationAreExplicit()
    {
        var image = Image();
        Assert.That(ImageResources.Snapshot(new() { Images = new Dictionary<string, PdfImage> { [new string('a', 128)] = image } }, default).Images,
            Has.Count.EqualTo(1));
        Assert.Throws<ArgumentException>(() => ImageResources.Snapshot(new() { Images = new Dictionary<string, PdfImage> { [new string('a', 129)] = image } }, default));
        Assert.Throws<OperationCanceledException>(() => ImageResources.Snapshot(Options(), new CancellationToken(true)));
    }

    private sealed class EndlessBindings(PdfImage image) : IReadOnlyDictionary<string, PdfImage>
    {
        public int Moves { get; private set; }
        public int Count => throw new InvalidOperationException("Do not trust or query arbitrary collection Count.");
        public IEnumerable<string> Keys => throw new NotSupportedException();
        public IEnumerable<PdfImage> Values => throw new NotSupportedException();
        public PdfImage this[string key] => throw new NotSupportedException();
        public bool ContainsKey(string key) => throw new NotSupportedException();
        public bool TryGetValue(string key, out PdfImage value) => throw new NotSupportedException();
        public IEnumerator<KeyValuePair<string, PdfImage>> GetEnumerator()
        { for (var i = 0; ; i++) { Moves++; yield return new(i.ToString(System.Globalization.CultureInfo.InvariantCulture), image); } }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
