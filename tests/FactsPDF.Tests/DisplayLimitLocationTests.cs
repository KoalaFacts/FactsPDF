using NUnit.Framework;

namespace FactsPDF.Tests;

// When MaxDisplayCommands runs out, FPDF1401 points at the element whose text
// line, box paint, table border or list marker needed the command past the
// limit, so SourceOffset and the CLI's "Location: line L, column C." name it.
// Each document is rendered once at the exact command count it needs (which
// must succeed) and once at one command less (which must fail there).
public sealed class DisplayLimitLocationTests
{
    private const string Prefix = "<p>Fine text.</p>\n";

    private static int OffsetOf(string html, string marker) => html.IndexOf(marker, StringComparison.Ordinal);

    private static byte[] Render(string html, int commands)
        => Pdf.Write().WithLimits(limits => limits.MaxDisplayCommands(commands)).ToBytes(html);

    private static FactsPdfException FailsOneCommandShort(string html)
    {
        var needed = 1;
        while (true)
        {
            try
            {
                Render(html, needed);
                break;
            }
            catch (FactsPdfException error) when (error.Code == "FPDF1401")
            {
                needed++;
                Assert.That(needed, Is.LessThan(500), "the document should need only a few commands");
            }
        }
        Assert.That(needed, Is.GreaterThan(1), "the document needs at least two commands");
        var failure = Assert.Throws<FactsPdfException>(() => Render(html, needed - 1))!;
        Assert.That(failure.Code, Is.EqualTo("FPDF1401"));
        return failure;
    }

    [Test]
    public void ALaterLineOfAWrappedParagraphPointsAtItsParagraph()
    {
        var html = Prefix + "<p id='long'>" + string.Join(' ', Enumerable.Repeat("word", 400)) + "</p>";
        var error = FailsOneCommandShort(html);
        Assert.That(error.SourceOffset, Is.EqualTo(OffsetOf(html, "<p id='long'>")));
    }

    [Test]
    public void ABoxBackgroundPointsAtItsBox()
    {
        const string html = Prefix + "<div id='box' style='background-color:#eee;padding:4pt'></div>";
        var error = FailsOneCommandShort(html);
        Assert.That(error.SourceOffset, Is.EqualTo(OffsetOf(html, "<div id='box'")));
    }

    [Test]
    public void ABorderedBoxAroundTextPointsAtTheBoxWhenItsPaintIsLast()
    {
        const string html = Prefix + "<div id='box' style='border:1pt solid #000'><p id='inner'>Inside.</p></div>";
        var error = FailsOneCommandShort(html);
        Assert.That(error.SourceOffset, Is.EqualTo(OffsetOf(html, "<div id='box'"))
            .Or.EqualTo(OffsetOf(html, "<p id='inner'>")));
    }

    [Test]
    public void ATableCellPointsAtTheCellOrItsText()
    {
        const string html = Prefix +
            "<table style='width:200pt'><colgroup><col style='width:100pt'><col style='width:100pt'></colgroup>" +
            "<tbody><tr><td id='a' style='border:1pt solid #000'>A</td>" +
            "<td id='b' style='border:1pt solid #000'>B</td></tr></tbody></table>";
        var error = FailsOneCommandShort(html);
        Assert.That(error.SourceOffset, Is.EqualTo(OffsetOf(html, "<td id='b'")));
    }

    [Test]
    public void AListItemPointsAtItsItem()
    {
        const string html = Prefix + "<ul><li id='one'>One</li><li id='two'>Two</li></ul>";
        var error = FailsOneCommandShort(html);
        Assert.That(error.SourceOffset, Is.EqualTo(OffsetOf(html, "<li id='two'>")));
    }

    [Test]
    public void AHeadingPointsAtItsHeading()
    {
        const string html = Prefix + "<h2 id='title'>Title</h2>";
        var error = FailsOneCommandShort(html);
        Assert.That(error.SourceOffset, Is.EqualTo(OffsetOf(html, "<h2 id='title'>")));
    }
}

// When MaxOutputBytes runs out while a page's boxes are painted, FPDF1401
// points at the box whose background, border or outline crossed the limit.
public sealed class PaintByteLimitLocationTests
{
    private static int OffsetOf(string html, string marker) => html.IndexOf(marker, StringComparison.Ordinal);

    private static FactsPdfException Fails(string html, int maxOutputBytes, string message)
    {
        var error = Assert.Throws<FactsPdfException>(
            () => Pdf.Write().WithLimits(limits => limits.MaxOutputBytes(maxOutputBytes)).ToBytes(html))!;
        Assert.That(error.Code, Is.EqualTo("FPDF1401"));
        Assert.That(error.Message, Is.EqualTo("FPDF1401: " + message));
        return error;
    }

    // Forty boxes on one page paint well past the limit while the PDF header
    // (catalog, pages, font) still fits under it.
    private static string Boxes(string style)
        => string.Concat(Enumerable.Range(0, 40).Select(i => "<div id='b" + i + "' style='" + style + ";height:10pt'></div>"));

    private static int BoxIndexAt(string html, int offset)
    {
        for (var i = 0; i < 40; i++)
            if (OffsetOf(html, "<div id='b" + i + "'") == offset) return i;
        return -1;
    }

    [TestCase("background-color:#eee", "PDF paint content exceeds MaxOutputBytes.")]
    [TestCase("border:1pt solid #000", "PDF paint content exceeds MaxOutputBytes.")]
    [TestCase("border:1pt dashed #000", "PDF patterned border exceeds MaxOutputBytes.")]
    [TestCase("background-color:#eee;border-radius:4pt", "PDF rounded background exceeds MaxOutputBytes.")]
    [TestCase("border:1pt solid #000;border-radius:4pt", "PDF rounded border exceeds MaxOutputBytes.")]
    [TestCase("outline:1pt solid #000", "PDF outline content exceeds MaxOutputBytes.")]
    public void APaintedBoxPointsAtItsBox(string style, string message)
    {
        var html = Boxes(style);
        var error = Fails(html, 1000, message);
        Assert.That(BoxIndexAt(html, error.SourceOffset), Is.InRange(1, 39));
    }

    [Test]
    public void TheBoxThatCrossesTheLimitIsNamedNotAnEarlierOne()
    {
        var html = Boxes("background-color:#eee");
        var early = BoxIndexAt(html, Fails(html, 700, "PDF paint content exceeds MaxOutputBytes.").SourceOffset);
        var late = BoxIndexAt(html, Fails(html, 1400, "PDF paint content exceeds MaxOutputBytes.").SourceOffset);
        Assert.That(early, Is.InRange(1, 39));
        Assert.That(late, Is.GreaterThan(early).And.LessThanOrEqualTo(39));
    }
}

// When MaxOutputBytes runs out while a page's vector list markers (disc,
// circle, square) are painted, FPDF1401 points at the list item whose marker
// crossed the limit instead of reporting no location.
public sealed class ListMarkerByteLimitLocationTests
{
    private const int Items = 40;

    private static int OffsetOf(string html, string marker) => html.IndexOf(marker, StringComparison.Ordinal);

    private static string List(string type)
        => "<ul style='list-style-type:" + type + "'>"
            + string.Concat(Enumerable.Range(0, Items).Select(i => "<li id='i" + i + "'>x</li>")) + "</ul>";

    private static int ItemIndexAt(string html, int offset)
    {
        for (var i = 0; i < Items; i++)
            if (OffsetOf(html, "<li id='i" + i + "'>") == offset) return i;
        return -1;
    }

    private static FactsPdfException Fails(string html, int maxOutputBytes)
    {
        var error = Assert.Throws<FactsPdfException>(
            () => Pdf.Write().WithLimits(limits => limits.MaxOutputBytes(maxOutputBytes)).ToBytes(html))!;
        Assert.That(error.Code, Is.EqualTo("FPDF1401"));
        Assert.That(error.Message, Is.EqualTo("FPDF1401: PDF content exceeds MaxOutputBytes."));
        return error;
    }

    [TestCase("disc")]
    [TestCase("circle")]
    [TestCase("square")]
    public void AListMarkerPointsAtItsItem(string type)
    {
        var html = List(type);
        Assert.That(ItemIndexAt(html, Fails(html, 1500).SourceOffset), Is.InRange(1, Items - 1));
    }

    [Test]
    public void TheItemWhoseMarkerCrossesTheLimitIsNamedNotAnEarlierOne()
    {
        var html = List("disc");
        var early = ItemIndexAt(html, Fails(html, 1200).SourceOffset);
        var late = ItemIndexAt(html, Fails(html, 2400).SourceOffset);
        Assert.That(early, Is.InRange(1, Items - 1));
        Assert.That(late, Is.GreaterThan(early).And.LessThanOrEqualTo(Items - 1));
    }

    [Test]
    public void ADiscKeepsItsSizeWithTheItemOffset()
    {
        const int count = 10_000;
        var discs = new PlacedDisc[count];
        var color = new Rgb(0, 0, 0);
        discs[0] = new PlacedDisc(1, 2, 3, color, ListMarkerKind.Disc, 7);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < count; i++) discs[i] = new PlacedDisc(i, 2, 3, color, ListMarkerKind.Square, i);
        var perDisc = (GC.GetAllocatedBytesForCurrentThread() - before) / (double)count;
        Assert.That(perDisc, Is.EqualTo(72).Within(0.5));
        Assert.That(discs[^1].SourceOffset, Is.EqualTo(count - 1));
    }
}
