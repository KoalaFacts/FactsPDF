using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// calc(), min(), max() and clamp() fold to the value Chrome computes, at the same stage as the plain
/// value: em against the element's font size (font-size itself against the parent's), rem against the
/// root, viewport units against the page area, and a percentage plus a length against the containing
/// block. Each case is byte-identical to the page written with the folded value. Invalid expressions
/// fail like invalid CSS; valid ones that cannot be computed before layout fail explicitly.
/// </summary>
[TestFixture]
public sealed class CssMathTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 400, Margin = 20 };

    private static byte[] Pdf(string html, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, options ?? Page);
        return output.ToArray();
    }

    private static string Box(string style) => $"<div style='background:#cde;{style}'>A</div>";

    [TestCase("width:calc(10pt + 2 * 3pt)", "width:16pt")]
    [TestCase("width:calc(2 * (10pt + 5pt))", "width:30pt")]
    [TestCase("width:calc(100pt / 4)", "width:25pt")]
    [TestCase("width:calc(1in - 12pt)", "width:60pt")]
    [TestCase("width:calc(10px + 10pt)", "width:17.5pt")]
    [TestCase("width:calc(1cm + 5mm)", "width:42.5196850394pt")]
    [TestCase("width:calc(30pt + -4pt)", "width:26pt")]
    [TestCase("width:calc(((10pt)))", "width:10pt")]
    [TestCase("width:CALC(10pt * 2)", "width:20pt")]
    [TestCase("width:calc( 10pt  +  5pt )", "width:15pt")]
    [TestCase("width:min(10pt, 20pt, 30pt)", "width:10pt")]
    [TestCase("width:max(10pt, 20pt)", "width:20pt")]
    [TestCase("width:max(10pt, 2 * 8pt, calc(1pt + 2pt))", "width:16pt")]
    [TestCase("width:clamp(5pt, 50pt, 20pt)", "width:20pt")]
    [TestCase("width:clamp(30pt, 1pt, 40pt)", "width:30pt")]
    [TestCase("width:clamp(5pt, 25pt, 40pt)", "width:25pt")]
    [TestCase("width:clamp(none, 25pt, 20pt)", "width:20pt")]
    [TestCase("width:clamp(30pt, 25pt, none)", "width:30pt")]
    [TestCase("width:min(50%, 70%)", "width:50%")]
    [TestCase("width:calc(2 * 25%)", "width:50%")]
    [TestCase("width:calc(10pt * pi)", "width:31.4159265pt")]
    [TestCase("padding:calc(1pt + 1pt) calc(3pt * 2)", "padding:2pt 6pt")]
    [TestCase("border:calc(1pt + 1pt) solid red", "border:2pt solid red")]
    [TestCase("margin:calc(2pt * 2) calc(5pt - 2pt)", "margin:4pt 3pt")]
    public void FoldsToTheValueChromeComputes(string written, string folded)
        => Assert.That(Pdf(Box(written)), Is.EqualTo(Pdf(Box(folded))));

    [TestCase("width:calc(100% - 40pt)", "width:220pt")]
    [TestCase("width:calc(50% + 10pt)", "width:140pt")]
    [TestCase("width:calc(10% + 2 * 5pt)", "width:36pt")]
    [TestCase("width:calc(100% - 2em)", "width:240pt")]
    [TestCase("padding-left:calc(10% + 4pt)", "padding-left:30pt")]
    [TestCase("margin-left:calc(5% - 2pt)", "margin-left:11pt")]
    [TestCase("max-width:calc(50% + 10pt)", "max-width:140pt")]
    [TestCase("min-width:calc(100% - 200pt)", "min-width:60pt")]
    public void APercentageAndALengthResolveAgainstTheContainingBlock(string written, string folded)
        => Assert.That(Pdf($"<div style='font-size:10pt'>{Box(written)}</div>"), Is.EqualTo(Pdf($"<div style='font-size:10pt'>{Box(folded)}</div>")));

    [Test]
    public void ThePercentageBaseIsTheNearestContainingBlockNotThePage()
        => Assert.That(Pdf("<div style='width:200pt'><div style='background:#cde;width:calc(50% + 10pt)'>A</div></div>"),
            Is.EqualTo(Pdf("<div style='width:200pt'><div style='background:#cde;width:110pt'>A</div></div>")));

    [Test]
    public void EmIsTheElementsFontSizeAndRemTheRoots()
    {
        Assert.That(Pdf("<div style='font-size:10pt'>" + Box("width:calc(2em + 1pt)") + "</div>"),
            Is.EqualTo(Pdf("<div style='font-size:10pt'>" + Box("width:21pt") + "</div>")));
        // The element's own font-size counts, not the parent's.
        Assert.That(Pdf("<div style='font-size:10pt'>" + Box("font-size:20pt;width:calc(2em + 1pt)") + "</div>"),
            Is.EqualTo(Pdf("<div style='font-size:10pt'>" + Box("font-size:20pt;width:41pt") + "</div>")));
        Assert.That(Pdf("<style>html{font-size:20pt}</style>" + Box("font-size:10pt;width:calc(2rem + 1pt)")),
            Is.EqualTo(Pdf("<style>html{font-size:20pt}</style>" + Box("font-size:10pt;width:41pt"))));
    }

    [Test]
    public void FontSizeAndLineHeightResolvePercentagesAndEmAgainstTheirOwnBases()
    {
        // font-size: em and % refer to the parent's size; line-height: to the element's own.
        Assert.That(Pdf("<div style='font-size:10pt'><p style='font-size:calc(10pt + 50%)'>A</p></div>"),
            Is.EqualTo(Pdf("<div style='font-size:10pt'><p style='font-size:15pt'>A</p></div>")));
        Assert.That(Pdf("<div style='font-size:10pt'><p style='font-size:calc(1em + 4pt)'>A</p></div>"),
            Is.EqualTo(Pdf("<div style='font-size:10pt'><p style='font-size:14pt'>A</p></div>")));
        Assert.That(Pdf("<div style='font-size:10pt'><p style='font-size:max(12pt, 1em)'>A</p></div>"),
            Is.EqualTo(Pdf("<div style='font-size:10pt'><p style='font-size:12pt'>A</p></div>")));
        Assert.That(Pdf("<p style='font-size:20pt;line-height:calc(10pt + 50%)'>A<br>A</p>"),
            Is.EqualTo(Pdf("<p style='font-size:20pt;line-height:20pt'>A<br>A</p>")));
        Assert.That(Pdf("<p style='font-size:10pt;line-height:calc(1.5)'>A<br>A</p>"),
            Is.EqualTo(Pdf("<p style='font-size:10pt;line-height:1.5'>A<br>A</p>")));
    }

    [Test]
    public void AMathFontSizeMayRelyOnAVarSubstitution()
        => Assert.That(Pdf("<style>:root{--w:20pt}</style>" + Box("width:calc(var(--w) * 3)")), Is.EqualTo(Pdf(Box("width:60pt"))));

    [Test]
    public void ANegativeResultIsClampedWhereThePropertyRangeIsNonNegative()
    {
        Assert.That(Pdf("<div style='background:#cde;width:calc(10pt - 30pt)'></div>"), Is.EqualTo(Pdf("<div style='background:#cde;width:0pt'></div>")));
        Assert.That(Pdf(Box("padding:calc(10pt - 30pt)")), Is.EqualTo(Pdf(Box("padding:0pt"))));
        Assert.That(Pdf(Box("border:calc(1pt - 3pt) solid red")), Is.EqualTo(Pdf(Box("border:0pt solid red"))));
    }

    [Test]
    public void AuthorCssStillCascadesAroundMathDeclarations()
    {
        Assert.That(Pdf("<style>div{width:calc(50% - 5pt)!important}</style>" + Box("width:100pt")),
            Is.EqualTo(Pdf("<style>div{width:125pt!important}</style>" + Box("width:100pt"))));
        Assert.That(Pdf("<style>div{width:calc(10pt * 5)}</style>" + Box("width:100pt")), Is.EqualTo(Pdf(Box("width:100pt"))));
        Assert.That(Pdf("<style>div{width:calc(10pt * 5)}div{width:60pt}</style>" + "<div style='background:#cde'>A</div>"),
            Is.EqualTo(Pdf("<div style='background:#cde;width:60pt'>A</div>")));
    }

    // Viewport units are percentages of the page area: the page size less the margins.
    [TestCase("width:50vw;height:25vh", "width:130pt;height:90pt")]
    [TestCase("width:50vmin;height:25vmax", "width:130pt;height:90pt")]
    [TestCase("width:100vw", "width:260pt")]
    [TestCase("width:calc(100vw - 20pt)", "width:240pt")]
    [TestCase("width:50svw;height:50dvh", "width:130pt;height:180pt")]
    [TestCase("width:clamp(50pt, 10vw, 200pt)", "width:50pt")]
    [TestCase("width:min(100vw, 150pt)", "width:150pt")]
    public void ViewportUnitsAreRelativeToThePageArea(string written, string folded)
        => Assert.That(Pdf(Box(written)), Is.EqualTo(Pdf(Box(folded))));

    [Test]
    public void ThePageAreaFollowsTheAtPageSizeAndMargins()
    {
        const string page = "@page{size:400pt 300pt;margin:10pt 30pt}";
        // 340pt x 280pt of page area.
        Assert.That(Pdf($"<style>{page}</style>" + Box("width:50vw;height:25vh"), new()),
            Is.EqualTo(Pdf($"<style>{page}</style>" + Box("width:170pt;height:70pt"), new())));
        Assert.That(Pdf($"<style>{page}</style>" + Box("width:50vmin;height:25vmax"), new()),
            Is.EqualTo(Pdf($"<style>{page}</style>" + Box("width:140pt;height:85pt"), new())));
    }

    [Test]
    public void ViewportLengthsWorkInFontSizeAndTextIndent()
    {
        Assert.That(Pdf("<p style='font-size:5vw'>A</p>"), Is.EqualTo(Pdf("<p style='font-size:13pt'>A</p>")));
        Assert.That(Pdf("<p style='font-size:clamp(8pt, 4vw, 20pt)'>A</p>"), Is.EqualTo(Pdf("<p style='font-size:10.4pt'>A</p>")));
    }

    [TestCase("width:calc(10pt+5pt)")]
    [TestCase("width:calc(10pt +5pt)")]
    [TestCase("width:calc(10pt + 2)")]
    [TestCase("width:calc(2 * 3pt * 4pt)")]
    [TestCase("width:calc(10pt / 2pt)")]
    [TestCase("width:calc()")]
    [TestCase("width:calc(10pt,5pt)")]
    [TestCase("width:min()")]
    [TestCase("width:clamp(1pt, 2pt)")]
    [TestCase("width:calc(2 * 3)")]
    [TestCase("width:calc(10pt + auto)")]
    public void InvalidExpressionsFail(string declaration)
    {
        var html = "<!doctype html><html><head><style>div{background:#cde}</style></head><body><div style='" + declaration + "'>A</div></body></html>";
        Assert.That(Assert.Throws<FactsPdfException>(() => Pdf(html))!.Code, Is.EqualTo("FPDF1202"));
        // Non-strict mode drops a declaration that is invalid CSS, as a browser does.
        var dropped = FactsPDF.Pdf.Write().WithPage(p => p.Size(300, 400).Margin(20)).WithStrict(false).ToBytes(html);
        Assert.That(dropped, Is.EqualTo(FactsPDF.Pdf.Write().WithPage(p => p.Size(300, 400).Margin(20)).ToBytes(html.Replace(declaration, ""))));
    }

    [TestCase("width:calc(10pt / 0)")]
    [TestCase("width:calc(10pt / (1 - 1))")]
    [TestCase("width:min(10%, 30pt)")]
    [TestCase("width:max(10%, 30pt)")]
    [TestCase("width:clamp(10%, 50pt, 90%)")]
    [TestCase("width:calc(10ex)")]
    [TestCase("width:calc(10ch + 1pt)")]
    [TestCase("margin-left:calc(-5pt)")]
    public void ValidExpressionsThatCannotBeComputedFailExplicitlyInBothModes(string declaration)
    {
        var html = "<!doctype html><html><head><style>div{background:#cde}</style></head><body><div style='" + declaration + "'>A</div></body></html>";
        Assert.That(Assert.Throws<FactsPdfException>(() => Pdf(html))!.Code, Is.EqualTo("FPDF1202"));
        Assert.That(Assert.Throws<FactsPdfException>(() => FactsPDF.Pdf.Write().WithStrict(false).ToBytes(html))!.Code, Is.EqualTo("FPDF1202"));
    }

    [Test]
    public void MathInsideTheFontShorthandFailsExplicitly()
        => Assert.That(Assert.Throws<FactsPdfException>(() => Pdf("<p style='font:calc(10pt + 2pt) serif'>A</p>"))!.Code, Is.EqualTo("FPDF1202"));
}
