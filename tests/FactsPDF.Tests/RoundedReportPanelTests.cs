using System.Text;
using FactsPDF.CommandLine;
using NUnit.Framework;

namespace FactsPDF.Tests;

[TestFixture]
public sealed class RoundedReportPanelTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 240, Margin = 20 };
    private static byte[] Pdf(string html, PdfOptions? page = null)
    {
        using var stream = new MemoryStream();
        PdfConverter.Convert(html, stream, page ?? Page);
        return stream.ToArray();
    }
    private static LayoutPage[] Layout(string html, PdfOptions? page = null)
    {
        var options = page ?? Page;
        return TextLayout.Layout(HtmlDocumentReader.ReadTree(html, options, default), options, default).ToArray();
    }
    private static void FailsProtected(string html, string expected)
    {
        using var target = new MemoryStream();
        target.Write("ORIGINAL-CUSTOMER-PDF"u8);
        target.Position = 7;
        var failure = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, target, Page));
        Assert.That(failure!.Code, Is.EqualTo(expected));
        Assert.That(target.ToArray(), Is.EqualTo("ORIGINAL-CUSTOMER-PDF"u8.ToArray()));
        Assert.That(target.Position, Is.EqualTo(7));
        Assert.That(Pdf("<p>Recovery</p>").Length, Is.GreaterThan(100));
    }

    [Test]
    public void RoundedCardPaintsRealVectorCornersAndHollowUniformBorder()
    {
        const string source = "<section style='width:160pt;background:#edf5fa;padding:8pt;" +
            "border:2pt solid #23446b;border-radius:12pt'>Invoice 41</section>";
        var page = Layout(source).Single();
        var paint = page.PaintBoxes.Single();
        Assert.That(paint.CommandCount, Is.EqualTo(2), "One actual background path and one actual even-odd border ring.");
        Assert.That(paint.Width, Is.EqualTo(180).Within(0.00001));
        Assert.That(page.Runs.Single().X, Is.EqualTo(30).Within(0.00001));
        var pdf = Encoding.ASCII.GetString(Pdf(source));
        Assert.That(pdf.Split(" c ").Length - 1, Is.GreaterThanOrEqualTo(8), "Outer and inner real cubic curves.");
        Assert.That(pdf, Does.Contain(" f Q\n").And.Contain("f* Q\n"));
        Assert.That(pdf, Does.Contain("0.137 0.267 0.42 rg"));
        Assert.That(pdf, Does.Contain("Tm <496E766F696365203431> Tj"));
    }

    [Test]
    public void EquivalentPhysicalLengthAndSourceSeparateOracleProduceIdenticalPdfBytes()
    {
        const string physical = "<div style='background:#e5f1f6;border:2pt solid blue;" +
            "border-radius:12pt;padding:5pt'>Card 12</div>";
        const string equivalent = "<div style='background:#e5f1f6;border:2pt solid blue;" +
            "border-radius:16px;padding:5pt'>Card 12</div>";
        Assert.That(Pdf(physical), Is.EqualTo(Pdf(equivalent)));
        Assert.That(Pdf(physical), Is.Not.EqualTo(Pdf(physical.Replace("12pt", "0"))));
    }

    [Test]
    public void CornerRadiusDoesNotChangeMeasuredTextPositionsOrLegacyZeroRadiusPdf()
    {
        const string before = "<p style='background:red;border:2pt solid blue;padding:6pt'>Marked report</p>";
        var rounded = before.Replace("padding:6pt", "border-radius:9pt;padding:6pt");
        Assert.That(Layout(rounded).Single().Runs.Select(x => (x.X, x.Baseline)),
            Is.EqualTo(Layout(before).Single().Runs.Select(x => (x.X, x.Baseline))));
        Assert.That(Pdf(before.Replace("padding:6pt", "border-radius:0;padding:6pt")),
            Is.EqualTo(Pdf(before)), "A zero-radius declaration must retain original PDF bytes.");
    }

    [Test]
    public void FragmentationRoundsFirstTopAndFinalBottomButDoesNotRepeatPageCaps()
    {
        var notes = string.Concat(Enumerable.Range(1, 45).Select(i => $"<p style='margin:0'>Audit note {i:00}<br></p>"));
        var html = "<section style='background:#edf5fa;border:2pt solid blue;" +
            "border-radius:12pt;padding:5pt'>" + notes + "</section>";
        var pages = Layout(html);
        Assert.That(pages.Length, Is.GreaterThanOrEqualTo(3));
        var painted = pages.Select(p => p.PaintBoxes.Single()).ToArray();
        Assert.That(painted[0].IsFirstFragment, Is.True);
        Assert.That(painted[0].IsLastFragment, Is.False);
        Assert.That(painted[^1].IsFirstFragment, Is.False);
        Assert.That(painted[^1].IsLastFragment, Is.True);
        Assert.That(painted.Skip(1).SkipLast(1).All(p => !p.IsFirstFragment && !p.IsLastFragment), Is.True);
        string Drawing(PaintedBox box)
        {
            var builder = new StringBuilder();
            PdfPaintSerializer.Append(builder, [box], Page.PageHeight, 20000, default);
            return builder.ToString();
        }
        Assert.That(Drawing(painted[0]), Does.Contain(" c "));
        Assert.That(Drawing(painted[^1]), Does.Contain(" c "));
        Assert.That(Drawing(painted[1]), Does.Not.Contain(" c "), "Continuation sides are straight.");
        Assert.That(pages.SelectMany(p => p.Runs).Count(), Is.EqualTo(45));
        var pdf = Encoding.ASCII.GetString(Pdf(html));
        Assert.That(pdf, Does.Contain("Tm <4175646974206E6F7465203031> Tj"));
        Assert.That(pdf, Does.Contain("Tm <4175646974206E6F7465203435> Tj"));
    }

    [Test]
    public void ImportantCascadeAndExplicitNonInheritanceAffectActualPdfPaths()
    {
        const string html = "<style>.card{border-radius:8pt!important}" +
            ".card{border-radius:3pt}</style><div class=card style='border-radius:2pt;" +
            "background:blue'><p>Top</p><div style='background:blue'>" +
            "Inner</div><div style='background:blue;border-radius:inherit'>Inherited</div>" +
            "<div style='background:blue;border-radius:initial'>Initial</div></div>";
        var outer = (BlockNode)HtmlDocumentReader.ReadTree(html, Page, default).Children.Single();
        Assert.That(outer.BoxStyle.CornerRadius, Is.EqualTo(8));
        var boxes = outer.Children.OfType<BlockNode>().ToArray();
        Assert.That(boxes[1].BoxStyle.CornerRadius, Is.EqualTo(8));
        Assert.That(boxes[0].BoxStyle.CornerRadius, Is.Zero);
        Assert.That(boxes[2].BoxStyle.CornerRadius, Is.Zero);
        Assert.That(Encoding.ASCII.GetString(Pdf(html)), Does.Contain(" c "));
    }

    [TestCase("<section style='border-radius:10%'>Invalid</section>", "FPDF1202")]
    [TestCase("<section style='border-radius:8pt 9pt'>Invalid</section>", "FPDF1202")]
    [TestCase("<section style='border-radius:3'>Invalid</section>", "FPDF1202")]
    [TestCase("<section style='border-radius:0.1pt'>Invalid</section>", "FPDF1202")]
    [TestCase("<section style='border-radius:73pt'>Invalid</section>", "FPDF1202")]
    [TestCase("<section style='border-radius:1e-999pt'>Invalid</section>", "FPDF1202")]
    [TestCase("<span style='border-radius:5pt'>Inline</span>", "FPDF1201")]
    [TestCase("<img src=logo style='border-radius:5pt'>", "FPDF1201")]
    [TestCase("<ul style='border-radius:5pt'><li>One</li></ul>", "FPDF1201")]
    [TestCase("<section style='border-radius:5pt;border:2pt dashed red'>Unsafe</section>", "FPDF1201")]
    [TestCase("<section style='border-radius:5pt;border-left:2pt solid red'>Unsafe</section>", "FPDF1201")]
    [TestCase("<section style='border-radius:5pt;border:2pt solid red;border-top-color:blue'>Unsafe</section>", "FPDF1201")]
    [TestCase("<style>.unused{border-radius:10%}</style><p>Safe</p>", "FPDF1202")]
    public void UnsupportedRadiusAndIncompatiblePaintFailBeforeWriting(string html, string error)
        => FailsProtected(html, error);

    [Test]
    public void ManagedCliAndApiProduceMatchingRoundedReportBytes()
    {
        const string source = "<section style='border-radius:12pt;background:blue;" +
            "border:2pt solid white;padding:6pt'>Service card</section>";
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(source));
        using var output = new MemoryStream();
        using var errors = new StringWriter();
        var exit = CliApplication.Run(["-", "-"], input, output, errors);
        Assert.That(exit, Is.Zero, errors.ToString());
        using var direct = new MemoryStream();
        PdfConverter.Convert(source, direct);
        Assert.That(output.ToArray(), Is.EqualTo(direct.ToArray()));
    }
}
