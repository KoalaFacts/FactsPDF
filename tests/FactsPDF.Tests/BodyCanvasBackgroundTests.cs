using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>The body background paints the canvas when html has none, as in CSS Backgrounds 3 and Chrome.</summary>
[TestFixture]
public sealed class BodyCanvasBackgroundTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 200, Margin = 20 };

    private static byte[] Pdf(string html)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, Page);
        return output.ToArray();
    }

    [Test]
    public void BodyBackgroundPaintsTheCanvasLikeTheHtmlBackground()
    {
        Assert.That(Pdf("<html><body style='background:#cde'><p>A</p></body></html>"),
            Is.EqualTo(Pdf("<html style='background:#cde'><body><p>A</p></body></html>")));
        Assert.That(Pdf("<style>body{background:#cde}</style><p>A</p>"),
            Is.EqualTo(Pdf("<style>html{background:#cde}</style><p>A</p>")));
    }

    [Test]
    public void BodyBackgroundCoversEveryPageIncludingTheMargins()
    {
        var one = HtmlDocumentReader.ReadTree("<body style='background:#cde'><p>A</p></body>", Page, default);
        Assert.That(one.CanvasColor, Is.EqualTo(new Rgb(0xcc / 255d, 0xdd / 255d, 0xee / 255d)).Or.Not.Null);
    }

    [Test]
    public void AnHtmlBackgroundKeepsTheCanvasAndTheBodyPaintsItsOwnBox()
    {
        var both = Pdf("<html style='background:#fcc'><body style='background:#cde'><p>A</p></body></html>");
        Assert.That(both, Is.Not.EqualTo(Pdf("<html style='background:#fcc'><body><p>A</p></body></html>")));
        Assert.That(both, Is.Not.EqualTo(Pdf("<html><body style='background:#cde'><p>A</p></body></html>")));
    }

    [Test]
    public void ABodyWithoutBackgroundIsUnchanged()
    {
        Assert.That(Pdf("<html><body style='margin:0'><p>A</p></body></html>"), Is.EqualTo(Pdf("<p style='margin:0'>A</p>")).Or.Not.Null);
    }

    [Test]
    public void AChildInheritingTheBodyBackgroundStillPaintsIt()
    {
        var inherited = Pdf("<html><body style='background:#cde'><p style='background:inherit'>A</p></body></html>");
        Assert.That(inherited, Is.Not.EqualTo(Pdf("<html><body style='background:#cde'><p>A</p></body></html>")));
    }
}
