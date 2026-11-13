using NUnit.Framework;

namespace FactsPDF.Tests;

// Paint-stage patterned-border failures point at the element whose border
// cannot be painted, so SourceOffset and the CLI's "Location: line L, column C."
// name it instead of leaving -1.
public sealed class PaintErrorLocationTests
{
    private const string Prefix = "<p>Fine text.</p>\n";
    private const string Thin = "Patterned border is too thin to paint.";
    private const string Short = "Patterned border edge is too short to paint.";

    private static FactsPdfException Fails(PdfWriter writer, string html, string message)
    {
        var error = Assert.Throws<FactsPdfException>(() => writer.ToBytes(html))!;
        Assert.That(error.Message, Is.EqualTo("FPDF1302: " + message));
        Assert.That(error.Code, Is.EqualTo("FPDF1302"));
        return error;
    }

    private static int OffsetOf(string html, string marker) => html.IndexOf(marker, StringComparison.Ordinal);

    [Test]
    public void TooThinBorderPointsAtItsBox()
    {
        const string html = Prefix + "<div id='thin' style='border:0.0005pt dashed black'>x</div>";
        var error = Fails(Pdf.Write(), html, Thin);
        Assert.That(error.SourceOffset, Is.EqualTo(OffsetOf(html, "<div id='thin'")));
    }

    [Test]
    public void TooThinBorderOnANestedBoxPointsAtTheInnerBox()
    {
        const string html = Prefix + "<div style='border:1pt solid black'><p>a</p><div id='inner' style='border-top:0.0005pt dotted red'>x</div></div>";
        var error = Fails(Pdf.Write(), html, Thin);
        Assert.That(error.SourceOffset, Is.EqualTo(OffsetOf(html, "<div id='inner'")));
    }

    [Test]
    public void TooShortBorderEdgePointsAtItsBox()
    {
        const string html = Prefix + "<div id='flat' style='border-left:2pt dashed black;height:0.0001pt;width:10pt'></div>";
        var error = Fails(Pdf.Write(), html, Short);
        Assert.That(error.SourceOffset, Is.EqualTo(OffsetOf(html, "<div id='flat'")));
    }

    [Test]
    public void TooThinTableCellBorderPointsAtTheCell()
    {
        const string html = Prefix + "<table style='width:160pt'><colgroup><col style='width:80pt'><col style='width:80pt'></colgroup><tbody><tr><td>a</td><td id='cell' style='border:0.0005pt dashed black'>b</td></tr></tbody></table>";
        var error = Fails(Pdf.Write(), html, Thin);
        Assert.That(error.SourceOffset, Is.EqualTo(OffsetOf(html, "<td id='cell'")));
    }

    [Test]
    public void TooThinBorderOnACellWhoseRowSplitsAcrossPagesPointsAtTheCell()
    {
        var paragraphs = string.Concat(Enumerable.Repeat("<p>Row text.</p>", 120));
        var html = Prefix + "<table style='width:160pt'><colgroup><col style='width:160pt'></colgroup><tbody><tr><td id='tall' style='border:0.0005pt dashed black'>"
            + paragraphs + "</td></tr></tbody></table>";
        var error = Fails(Pdf.Write(), html, Thin);
        Assert.That(error.SourceOffset, Is.EqualTo(OffsetOf(html, "<td id='tall'")));
    }

    [Test]
    public void TooThinTableBorderPointsAtTheTable()
    {
        const string html = Prefix + "<table id='grid' style='width:80pt;border:0.0005pt dashed black'><colgroup><col style='width:100%'></colgroup><tbody><tr><td>a</td></tr></tbody></table>";
        var error = Fails(Pdf.Write(), html, Thin);
        Assert.That(error.SourceOffset, Is.EqualTo(OffsetOf(html, "<table id='grid'")));
    }

    [Test]
    public void TooThinImageBorderPointsAtTheImage()
    {
        var image = PdfImage.FromBytes(JpegResourceTests.Bytes("rgb-420"));
        const string html = Prefix + "<img id='pic' src='a' style='border:0.0005pt dashed black'>";
        var error = Fails(Pdf.Write().WithImage("a", image), html, Thin);
        Assert.That(error.SourceOffset, Is.EqualTo(OffsetOf(html, "<img id='pic'")));
    }
}

// Paint-stage failures of decorated or linked text point at the paragraph whose
// run cannot be painted (R2c8). Plain runs keep no offset and stay 64 bytes.
public sealed class TextPaintErrorLocationTests
{
    private const int Paragraphs = 40;

    private static int OffsetOf(string html, string marker) => html.IndexOf(marker, StringComparison.Ordinal);

    private static string Underlined()
        => string.Concat(Enumerable.Range(0, Paragraphs).Select(i => "<p id='p" + i + "'><u>x</u></p>"));

    private static int ParagraphAt(string html, int offset)
    {
        for (var i = 0; i < Paragraphs; i++)
            if (OffsetOf(html, "<p id='p" + i + "'>") == offset) return i;
        return -1;
    }

    // The smallest limits at which FPDF1401 is raised by a text decoration.
    private static List<FactsPdfException> DecorationFailures(string html)
    {
        var failures = new List<FactsPdfException>();
        for (var limit = 600; limit < 6000 && failures.Count < 3; limit += 7)
        {
            try
            {
                Pdf.Write().WithLimits(limits => limits.MaxOutputBytes(limit)).ToBytes(html);
                break;
            }
            catch (FactsPdfException error) when (error.Message == "FPDF1401: PDF text decoration exceeds MaxOutputBytes.")
            {
                failures.Add(error);
            }
            catch (FactsPdfException error) when (error.Code == "FPDF1401")
            {
            }
        }
        return failures;
    }

    [Test]
    public void ATextDecorationThatCrossesMaxOutputBytesPointsAtItsParagraph()
    {
        var html = Underlined();
        var failures = DecorationFailures(html);
        Assert.That(failures, Is.Not.Empty);
        foreach (var error in failures)
            Assert.That(ParagraphAt(html, error.SourceOffset), Is.InRange(0, Paragraphs - 1));
    }

    [Test]
    public void AShortDashedDecorationPointsAtItsRun()
    {
        var style = new TextStyle(10, 12, new Rgb(0, 0, 0), TextAlignment.Left) { Decoration = TextDecoration.Underline, DecorationPattern = TextDecorationPattern.Dashed };
        var error = Assert.Throws<FactsPdfException>(() => PdfPaintSerializer.AppendTextDecoration(
            new System.Text.StringBuilder(), new LocatedText("x", 1, 2, style, null, 0.0005, 42), int.MaxValue))!;
        Assert.That(error.Message, Is.EqualTo("FPDF1302: Text decoration is too short to paint."));
        Assert.That(error.SourceOffset, Is.EqualTo(42));
    }

    [Test]
    public void DecoratedAndLinkedRunsKeepTheirParagraphAndPlainRunsDoNot()
    {
        const string html = "<p>plain</p><p id='u'><u>under</u></p><p id='a'><a href='https://example.com/'>link</a></p>";
        var options = new PdfOptions();
        var pages = TextLayout.Layout(HtmlDocumentReader.ReadTree(html, options, default), options, default);
        var runs = pages.SelectMany(page => page.Runs).ToList();
        Assert.That(runs.Single(run => run.Text == "plain").SourceOffset, Is.EqualTo(-1));
        Assert.That(runs.Single(run => run.Text == "plain").GetType(), Is.EqualTo(typeof(PlacedText)));
        Assert.That(runs.Single(run => run.Text == "under").SourceOffset, Is.EqualTo(OffsetOf(html, "<p id='u'>")));
        Assert.That(runs.Single(run => run.Text == "link").SourceOffset, Is.EqualTo(OffsetOf(html, "<p id='a'>")));
    }

    [Test]
    public void APlainRunStaysSixtyFourBytes()
    {
        const int count = 10_000;
        var runs = new PlacedText[count];
        var style = new TextStyle(10, 12, new Rgb(0, 0, 0), TextAlignment.Left);
        runs[0] = new PlacedText("x", 1, 2, style, null, 3);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 1; i < count; i++) runs[i] = new PlacedText("x", i, 2, style, null, 3);
        var perRun = (GC.GetAllocatedBytesForCurrentThread() - before) / (double)(count - 1);
        Assert.That(perRun, Is.EqualTo(64));
        GC.KeepAlive(runs);
    }
}
