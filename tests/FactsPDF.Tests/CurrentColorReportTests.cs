using System.Text;
using FactsPDF.CommandLine;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>Computed currentColor: real box fill and text from one CSS ink.</summary>
[TestFixture]
public sealed class CurrentColorReportTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 595, PageHeight = 250, Margin = 18 };
    private static byte[] Pdf(string html, PdfOptions? options = null)
    {
        using var result = new MemoryStream();
        PdfConverter.Convert(html, result, options ?? Page);
        return result.ToArray();
    }

    private static void Reject(string css)
    {
        using var saved = new MemoryStream();
        saved.Write("retain customer PDF"u8);
        saved.Position = 4;
        var failure = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(
            "<style>.never{" + css + "}</style><p style='" + css + "'>Text</p>",
            saved, Page));
        Assert.That(failure!.Code, Is.EqualTo("FPDF1202"));
        Assert.That(Encoding.ASCII.GetString(saved.ToArray()), Is.EqualTo("retain customer PDF"));
        Assert.That(saved.Position, Is.EqualTo(4));
    }

    [Test]
    public void BackgroundAndColourFollowWinningImportantDeclarationAndActualPdfInk()
    {
        const string actual = "<style>.panel{color:blue;background:currentColor;border:2pt solid currentColor}" +
            ".panel{color:#315f82!important}</style>" +
            "<div class=panel style='color:red;background-color:currentColor'><p style='color:currentColor'>Audit</p></div>";
        const string hex = "<div style='color:#315f82;background-color:#315f82;border:2pt solid #315f82'>" +
            "<p>Audit</p></div>";
        Assert.That(Pdf(actual), Is.EqualTo(Pdf(hex)));
        var box = (BlockNode)HtmlDocumentReader.ReadTree(actual, Page, default).Children.Single();
        Assert.That(box.BoxStyle.BackgroundColor, Is.EqualTo(box.Style.Color));
        Assert.That(box.BoxStyle.BorderTop.Color, Is.Null,
            "Existing border currentColor must keep its final-text-ink fallback.");
    }

    [Test]
    public void BackgroundCurrentColorIsComputedPerElementNotInheritedFromParent()
    {
        const string actual = "<div style='color:#315f82;background:currentColor'>" +
            "<div style='color:#c25224;background-color:currentColor'>" +
            "<p style='color:currentColor'>Child invoice note</p></div></div>";
        const string hex = "<div style='color:#315f82;background:#315f82'>" +
            "<div style='color:#c25224;background-color:#c25224'>" +
            "<p>Child invoice note</p></div></div>";
        Assert.That(Pdf(actual), Is.EqualTo(Pdf(hex)));
    }

    [Test]
    public void InheritedCurrentColorBackgroundUsesReceivingElementInkThroughNestedPanels()
    {
        const string actual = "<div style='color:#315f82;background:currentColor'>" +
            "<div style='color:#c25224;background:inherit'>" +
            "<div style='color:#214d28;background:inherit'><p>Inherited swatch</p></div></div></div>";
        const string hex = "<div style='color:#315f82;background:#315f82'>" +
            "<div style='color:#c25224;background:#c25224'>" +
            "<div style='color:#214d28;background:#214d28'><p>Inherited swatch</p></div></div></div>";
        Assert.That(Pdf(actual), Is.EqualTo(Pdf(hex)));
        var root = (BlockNode)HtmlDocumentReader.ReadTree(actual, Page, default).Children.Single();
        var child = (BlockNode)root.Children.Single();
        var grandchild = (BlockNode)child.Children.Single();
        Assert.That(root.BoxStyle.BackgroundUsesCurrentColor, Is.True);
        Assert.That(child.BoxStyle.BackgroundUsesCurrentColor, Is.True);
        Assert.That(grandchild.BoxStyle.BackgroundUsesCurrentColor, Is.True);
        Assert.That(child.BoxStyle.BackgroundColor, Is.EqualTo(child.Style.Color));
        Assert.That(grandchild.BoxStyle.BackgroundColor, Is.EqualTo(grandchild.Style.Color));
    }

    [Test]
    public void InheritedLiteralRgbBackgroundKeepsParentInkInsteadOfChildInk()
    {
        const string actual = "<div style='color:#315f82;background:#315f82'>" +
            "<div style='color:#c25224;background:inherit'><p>Inherited swatch</p></div></div>";
        const string hex = "<div style='color:#315f82;background:#315f82'>" +
            "<div style='color:#c25224;background:#315f82'><p>Inherited swatch</p></div></div>";
        Assert.That(Pdf(actual), Is.EqualTo(Pdf(hex)));
        var parent = (BlockNode)HtmlDocumentReader.ReadTree(actual, Page, default).Children.Single();
        var child = (BlockNode)parent.Children.Single();
        Assert.That(parent.BoxStyle.BackgroundUsesCurrentColor, Is.False);
        Assert.That(child.BoxStyle.BackgroundUsesCurrentColor, Is.False);
        Assert.That(child.BoxStyle.BackgroundColor, Is.Not.EqualTo(child.Style.Color));
    }

    [Test]
    public void TransparentAndCssWideResetDoNotAccidentallyPaint()
    {
        const string actual = "<div style='color:#315f82;background:currentColor'>" +
            "<div style='background:initial'><p>Fresh section</p></div>" +
            "<div style='background:transparent'><p>Clear section</p></div></div>";
        const string explicitHex = "<div style='color:#315f82;background:#315f82'>" +
            "<div><p>Fresh section</p></div><div><p>Clear section</p></div></div>";
        Assert.That(Pdf(actual), Is.EqualTo(Pdf(explicitHex)));
    }

    [Test]
    public void MultiPageReportKeepsTextPositionsAndPdfBytesAgainstIndependentHexOracle()
    {
        var source = File.ReadAllText(Path.Combine(FindRoot(), "examples", "report-current-colour.html"));
        var control = File.ReadAllText(Path.Combine(FindRoot(), "examples", "report-current-colour-oracle.html"));
        var actual = Pdf(source);
        Assert.That(actual, Is.EqualTo(Pdf(control)),
            "PDF fill, text and decoration bytes must match a separately authored fixed-RGB control.");
        var pages = TextLayout.Layout(HtmlDocumentReader.ReadTree(source, Page, default), Page, default);
        Assert.That(pages.Count, Is.GreaterThan(1));
        Assert.That(pages.SelectMany(p => p.Runs).Count(x => x.Text.StartsWith("INK-", StringComparison.Ordinal)),
            Is.EqualTo(48));
        Assert.That(pages.SelectMany(p => p.Runs).Last().Text, Is.EqualTo("REPORT COMPLETE"));
        Assert.That(pages.SelectMany(p => p.PaintBoxes).Any(x => x.Style.BackgroundColor.HasValue), Is.True);
    }

    [Test]
    public void ExplicitSuppliedFontAndManagedCliRenderSameActualInk()
    {
        var font = PdfFont.LoadTrueType(FontFixture.Create());
        var options = Page with { Fonts = [font], BoldFonts = [font] };
        const string actual = "<div style='color:#315f82;background:currentColor;padding:4pt'>" +
            "<p style='font-size:12pt;text-decoration:underline dotted currentColor'>AB</p></div>";
        const string fixedInk = "<div style='color:#315f82;background:#315f82;padding:4pt'>" +
            "<p style='font-size:12pt;text-decoration:underline dotted #315f82'>AB</p></div>";
        Assert.That(Pdf(actual, options), Is.EqualTo(Pdf(fixedInk, options)));
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(actual));
        using var output = new MemoryStream();
        using var diagnostic = new StringWriter();
        Assert.That(CliApplication.Run(["-", "-"], input, output, diagnostic), Is.Zero,
            diagnostic.ToString());
        Assert.That(output.ToArray(), Is.EqualTo(Pdf(actual, new PdfOptions())));
    }

    [TestCase("color:currentcolor(1)")]
    [TestCase("color:transparent")]
    [TestCase("background:currentcolor no-repeat")]
    [TestCase("background-color:currentcolor red")]
    [TestCase("background:linear-gradient(currentcolor,blue)")]
    [TestCase("background:rgba(1,2,3,0.5)")]
    public void UnsupportedColourSyntaxFailsBeforeChangingPdf(string css) => Reject(css);

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "FactsPDF.slnx"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Repository root not found.");
    }
}
