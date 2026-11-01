using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

[TestFixture]
public sealed class RoundedCornerLonghandTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 240, Margin = 20 };

    private static byte[] Pdf(string html)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, Page);
        return output.ToArray();
    }

    private static BlockNode Panel(string html) =>
        (BlockNode)HtmlDocumentReader.ReadTree(html, Page, default).Children.Single();

    private static (double TL, double TR, double BR, double BL) Corners(BlockNode node) =>
        (node.BoxStyle.CornerRadius, node.BoxStyle.CornerTopRightRadius,
         node.BoxStyle.CornerBottomRightRadius, node.BoxStyle.CornerBottomLeftRadius);

    [Test]
    public void FourLonghandsGenerateExactlyTheSamePaintedPdfAsFourCornerShorthand()
    {
        const string start = "<section style='width:146pt;box-sizing:border-box;padding:5pt;" +
            "background:#e8f2f8;border:2pt solid #224466;";
        const string end = "'><p>Paid invoice 25</p></section>";
        var shorthand = start + "border-radius:12pt 9pt 6pt 3pt" + end;
        var longhands = start + "border-top-left-radius:12pt;border-top-right-radius:9pt;" +
            "border-bottom-right-radius:6pt;border-bottom-left-radius:3pt" + end;
        Assert.That(Corners(Panel(longhands)), Is.EqualTo((12d,9d,6d,3d)));
        Assert.That(Pdf(longhands), Is.EqualTo(Pdf(shorthand)), "The four sides must affect native PDF painting.");
        Assert.That(Pdf(longhands), Is.Not.EqualTo(Pdf(start + "border-radius:0" + end)));
        Assert.That(Encoding.ASCII.GetString(Pdf(longhands)), Does.Contain(" c ").And.Contain("f* Q"));
    }

    [Test]
    public void EachCornerFollowsShorthandOrderingAndImportantIndependently()
    {
        const string prefix = "<section style='background:blue;";
        Assert.That(Corners(Panel(prefix + "border-top-left-radius:10pt;border-radius:6pt'>A</section>")),
            Is.EqualTo((6d,6d,6d,6d)));
        Assert.That(Corners(Panel(prefix + "border-radius:6pt;border-top-left-radius:10pt'>A</section>")),
            Is.EqualTo((10d,6d,6d,6d)));
        Assert.That(Corners(Panel(prefix + "border-top-left-radius:10pt!important;border-radius:6pt'>A</section>")),
            Is.EqualTo((10d,6d,6d,6d)));
        Assert.That(Corners(Panel(prefix + "border-top-left-radius:10pt;border-radius:6pt!important'>A</section>")),
            Is.EqualTo((6d,6d,6d,6d)));
        const string styled = "<style>.card{border-radius:12pt 9pt 6pt 3pt}" +
            ".card{border-top-right-radius:5pt!important}</style>" +
            "<section class=card style='border-radius:2pt 4pt 6pt 8pt;" +
            "border-bottom-left-radius:10pt'>A</section>";
        Assert.That(Corners(Panel(styled)), Is.EqualTo((2d,5d,6d,10d)));
    }

    [Test]
    public void IndividualCssWideValuesInheritOnlyTheSelectedCorner()
    {
        var outer = Panel("<section style='border-radius:12pt 9pt 6pt 3pt'>" +
            "<div style='border-top-right-radius:inherit'>A</div>" +
            "<div style='border-top-right-radius:initial'>B</div>" +
            "<div style='border-top-right-radius:unset'>C</div>" +
            "<div style='border-radius:inherit;border-top-right-radius:initial'>D</div>" +
            "<div style='border-radius:initial;border-bottom-left-radius:inherit'>E</div></section>");
        var children = outer.Children.OfType<BlockNode>().ToArray();
        Assert.That(children, Has.Length.EqualTo(5));
        Assert.That(Corners(children[0]), Is.EqualTo((0d,9d,0d,0d)));
        Assert.That(Corners(children[1]), Is.EqualTo((0d,0d,0d,0d)));
        Assert.That(Corners(children[2]), Is.EqualTo((0d,0d,0d,0d)));
        Assert.That(Corners(children[3]), Is.EqualTo((12d,0d,6d,3d)));
        Assert.That(Corners(children[4]), Is.EqualTo((0d,0d,0d,3d)));
    }

    [Test]
    public void PaginatedLonghandCardsPreserveSearchableTextAndRealFragmentCurves()
    {
        var notes = string.Concat(Enumerable.Range(1, 50)
            .Select(i => $"<p style='margin:0'>Corner entry {i:00}</p>"));
        const string leading = "<article style='background:#e8f2f8;border:2pt solid #224466;padding:5pt;";
        var source = leading + "border-radius:0;border-top-left-radius:12pt;" +
            "border-top-right-radius:9pt;border-bottom-right-radius:6pt;" +
            "border-bottom-left-radius:3pt'>" + notes + "</article>";
        var oracle = leading + "border-radius:12pt 9pt 6pt 3pt'>" + notes + "</article>";
        var pages = TextLayout.Layout(HtmlDocumentReader.ReadTree(source, Page, default), Page, default).ToArray();
        Assert.That(pages.Length, Is.GreaterThan(2));
        Assert.That(pages.SelectMany(x => x.Runs).Count(), Is.EqualTo(50));
        Assert.That(pages[0].PaintBoxes.Single().IsFirstFragment, Is.True);
        Assert.That(pages[^1].PaintBoxes.Single().IsLastFragment, Is.True);
        Assert.That(Pdf(source), Is.EqualTo(Pdf(oracle)));
        var bytes = Encoding.ASCII.GetString(Pdf(source));
        Assert.That(bytes, Does.Contain("Tm <436F726E657220656E747279203031> Tj"));
        Assert.That(bytes, Does.Contain("Tm <436F726E657220656E747279203530> Tj"));
    }

    [Test]
    public void ZeroAndEquivalentPrintUnitsDoNotInventNewPdfPaint()
    {
        const string begin = "<div style='background:blue;";
        const string end = "'>Card</div>";
        Assert.That(Pdf(begin + "border-top-left-radius:0" + end),
            Is.EqualTo(Pdf(begin + "border-radius:0" + end)));
        Assert.That(Pdf(begin + "border-top-left-radius:12pt" + end),
            Is.EqualTo(Pdf(begin + "border-top-left-radius:16px" + end)));
    }

    private static void FailsProtected(string html, string code)
    {
        using var target = new MemoryStream();
        target.Write("KEEP-EXISTING-CUSTOMER-PDF"u8);
        target.Position = 4;
        var ex = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, target, Page));
        Assert.That(ex!.Code, Is.EqualTo(code));
        Assert.That(target.ToArray(), Is.EqualTo("KEEP-EXISTING-CUSTOMER-PDF"u8.ToArray()));
        Assert.That(target.Position, Is.EqualTo(4));
        Assert.That(Pdf("<p>Recovery succeeded</p>").Length, Is.GreaterThan(100));
    }

    [TestCase("<section style='border-top-left-radius:10%'>X</section>", "FPDF1202")]
    [TestCase("<section style='border-top-right-radius:73pt'>X</section>", "FPDF1202")]
    [TestCase("<section style='border-bottom-right-radius:0.1pt'>X</section>", "FPDF1202")]
    [TestCase("<section style='border-bottom-left-radius:2pt 3pt'>X</section>", "FPDF1202")]
    [TestCase("<section style='border-top-left-radius:4pt / 3pt'>X</section>", "FPDF1202")]
    [TestCase("<section style='border-top-left-radius:1e-999pt'>X</section>", "FPDF1202")]
    [TestCase("<style>.unused{border-top-left-radius:10%}</style><p>X</p>", "FPDF1202")]
    [TestCase("<span style='border-top-left-radius:5pt'>X</span>", "FPDF1201")]
    [TestCase("<ul style='border-bottom-left-radius:5pt'><li>X</li></ul>", "FPDF1201")]
    [TestCase("<section style='border-top-left-radius:5pt;border:2pt dashed red'>X</section>", "FPDF1201")]
    public void UnsupportedLonghandScopeAndValuesPreserveExistingOutput(string html, string error)
        => FailsProtected(html, error);
}
