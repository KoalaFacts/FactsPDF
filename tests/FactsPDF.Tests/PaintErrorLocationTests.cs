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
