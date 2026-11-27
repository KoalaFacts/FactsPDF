using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// border-color: transparent keeps the edge's width in layout and paints nothing; the
/// background, painted to the border box, shows through. Outlines still reject it.
/// </summary>
[TestFixture]
public sealed class TransparentBorderColorTests
{
    private static readonly PdfOptions Options = new() { PageWidth = 300, PageHeight = 500, Margin = 20 };

    private static BlockNode Block(string html)
        => (BlockNode)HtmlDocumentReader.ReadTree(html, Options, default).Children.Single();

    private static string Render(string html)
    {
        using var stream = new MemoryStream();
        PdfConverter.Convert(html, stream, Options);
        return Encoding.ASCII.GetString(stream.ToArray());
    }

    private static int Count(string text, string value)
    {
        var count = 0;
        for (var at = text.IndexOf(value, StringComparison.Ordinal); at >= 0;
             at = text.IndexOf(value, at + value.Length, StringComparison.Ordinal)) count++;
        return count;
    }

    private static void Fails(string html, string code)
    {
        using var stream = new MemoryStream();
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, stream, Options));
        Assert.That(error!.Code, Is.EqualTo(code));
        Assert.That(stream.Length, Is.Zero);
    }

    [Test]
    public void TransparentBorderTakesItsWidthAndPaintsLikePadding()
    {
        var transparent = Render("<div style='background:#abc;border:4pt solid transparent'>A</div>");
        var padded = Render("<div style='background:#abc;padding:4pt'>A</div>");
        Assert.That(transparent, Is.EqualTo(padded),
            "A transparent edge must shift content like padding and paint only the background.");
        var box = Block("<div style='border:4pt solid transparent'>A</div>").BoxStyle;
        Assert.That(box.BorderTop.EffectiveWidth, Is.EqualTo(4));
        Assert.That(box.BorderTop.Transparent, Is.True);
    }

    [Test]
    public void OneTransparentSideOmitsOnlyThatStroke()
    {
        var pdf = Render("<div style='border:4pt solid red;border-top-color:transparent'>A</div>");
        Assert.That(Count(pdf, "1 0 0 rg"), Is.EqualTo(3), "Right, bottom and left stay red; top paints nothing.");
        var all = Render("<div style='border:4pt solid red'>A</div>");
        Assert.That(Count(all, "1 0 0 rg"), Is.EqualTo(4));
    }

    [Test]
    public void BorderColorShorthandAndStylesheetsAcceptTransparent()
    {
        var box = Block("<div style='border:2pt solid;border-color:red transparent'>A</div>").BoxStyle;
        Assert.That(box.BorderTop.Transparent, Is.False);
        Assert.That(box.BorderTop.Color, Is.EqualTo(new Rgb(1, 0, 0)));
        Assert.That(box.BorderRight.Transparent, Is.True);
        Assert.That(box.BorderLeft.Transparent, Is.True);
        Assert.That(box.BorderBottom.Transparent, Is.False);

        var sheet = Render("<style>div{border:4pt dashed transparent;background:#abc}</style><div>A</div>");
        Assert.That(sheet, Is.EqualTo(Render("<div style='background:#abc;padding:4pt'>A</div>")),
            "Patterned transparent edges paint nothing either.");
    }

    [Test]
    public void LaterColourDeclarationClearsTransparency()
    {
        var box = Block("<div style='border:2pt solid transparent;border-color:blue'>A</div>").BoxStyle;
        Assert.That(box.BorderTop.Transparent, Is.False);
        Assert.That(box.BorderTop.Color, Is.EqualTo(new Rgb(0, 0, 1)));
        var reset = Block("<div style='border:2pt solid transparent;border:2pt solid'>A</div>").BoxStyle;
        Assert.That(reset.BorderTop.Transparent, Is.False, "The border shorthand resets colour to currentColor.");
    }

    [Test]
    public void InheritCopiesTheParentsTransparentEdge()
    {
        const string html = "<section style='border:2pt solid transparent'>" +
            "<div style='border-style:solid;border-width:2pt;border-color:inherit'>A</div></section>";
        var parent = Block(html);
        var child = (BlockNode)parent.Children.Single();
        Assert.That(child.BoxStyle.BorderTop.Transparent, Is.True);
        Assert.That(Render(html), Is.EqualTo(Render(
            "<section style='padding:2pt'><div style='padding:2pt'>A</div></section>")),
            "Neither box paints its edges.");
    }

    [Test]
    public void RoundedPanelWithTransparentRingPaintsOnlyItsBackground()
    {
        var pdf = Render("<div style='border-radius:6pt;border:2pt solid transparent;background:#abc'>A</div>");
        Assert.That(pdf, Is.EqualTo(Render("<div style='border-radius:6pt;padding:2pt;background:#abc'>A</div>")),
            "Only the rounded background is filled; no ring.");
        Fails("<div style='border-radius:6pt;border:2pt solid red;border-top-color:transparent'>A</div>", "FPDF1201");
    }

    [Test]
    public void TransparentOutlinesPaintNothing()
    {
        var plain = Render("<div>A</div>");
        Assert.That(Render("<div style='outline:1pt solid transparent'>A</div>"), Is.EqualTo(plain));
        Assert.That(Render("<div style='outline:1pt solid red;outline-color:transparent'>A</div>"), Is.EqualTo(plain));
        Assert.That(Render("<div style='outline:2pt dashed rgba(255,0,0,0)'>A</div>"), Is.EqualTo(plain));
    }
}
