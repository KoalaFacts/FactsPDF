using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>Computed CSS line heights must affect actual line-box/page geometry.</summary>
[TestFixture]
public sealed class LineHeightUnitsTests
{
    private static readonly PdfOptions Options = new()
    {
        PageWidth = 300, PageHeight = 400, Margin = 20
    };

    private static List<Paragraph> Read(string css, string content)
        => HtmlDocumentReader.Read(
            "<style>" + css + "</style><body>" + content + "</body>",
            Options, CancellationToken.None);

    private static byte[] Pdf(string html)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, Options);
        return output.ToArray();
    }

    private static void Reject(string css)
    {
        using var existing = new MemoryStream();
        existing.Write("existing-target"u8);
        existing.Position = 4;
        var failure = Assert.Throws<FactsPdfException>(
            () => PdfConverter.Convert(css, existing, Options));
        Assert.That(failure!.Code, Is.EqualTo("FPDF1202"));
        Assert.That(existing.ToArray(), Is.EqualTo("existing-target"u8.ToArray()));
        Assert.That(existing.Position, Is.EqualTo(4));
    }

    [Test]
    public void PercentAndEmResolveToElementComputedFontSizeThenInheritAbsoluteLeading()
    {
        var paragraphs = Read(
            "body{font-size:16pt;line-height:150%}" +
            "p{font-size:12pt}p.compact{line-height:1.5em}" +
            "p span{font-size:24pt}",
            "<p>First <span>Big</span></p><p class=compact>Second <span>Large</span></p>");
        Assert.That(paragraphs, Has.Count.EqualTo(2));
        Assert.That(paragraphs[0].Style.FontSize, Is.EqualTo(12));
        Assert.That(paragraphs[0].Style.AbsoluteLineHeight, Is.EqualTo(24).Within(0.000001));
        Assert.That(paragraphs[0].Runs.Single(r => r.Text == "Big").Style.AbsoluteLineHeight,
            Is.EqualTo(24).Within(0.000001));
        Assert.That(paragraphs[1].Style.AbsoluteLineHeight, Is.EqualTo(18).Within(0.000001));
        Assert.That(paragraphs[1].Runs.Single(r => r.Text == "Large").Style.AbsoluteLineHeight,
            Is.EqualTo(18).Within(0.000001));
    }

    [Test]
    public void UnitlessLeadingStillScalesWithDescendantsFontSizes()
    {
        var paragraphs = Read(
            "body{font-size:16pt;line-height:1.5}p{font-size:12pt}p span{font-size:24pt}",
            "<p>Body <span>Large</span></p>");
        Assert.That(paragraphs[0].Style.AbsoluteLineHeight, Is.Null);
        Assert.That(paragraphs[0].Style.LineHeight, Is.EqualTo(1.5));
        var large = paragraphs[0].Runs.Single(r => r.Text == "Large").Style;
        Assert.That(large.AbsoluteLineHeight, Is.Null);
        Assert.That(large.FontSize * large.LineHeight, Is.EqualTo(36));
    }

    [Test]
    public void PercentAndPhysicalPointLeadingProduceByteEqualPdfsButNotUnitlessInheritance()
    {
        const string body = "<body><p>First line<br>Second <span>Large font</span> and more</p>"
            + "<p>Follow-up</p></body>";
        const string prefix = "<style>body{font-size:12pt}p{font-size:12pt;"
            + "margin-bottom:0}p span{font-size:18pt}";
        var percent = Pdf(prefix + "p{line-height:150%}</style>" + body);
        var absolute = Pdf(prefix + "p{line-height:18pt}</style>" + body);
        var unitless = Pdf(prefix + "p{line-height:1.5}</style>" + body);
        Assert.That(percent, Is.EqualTo(absolute));
        Assert.That(percent, Is.Not.EqualTo(unitless),
            "Inherited absolute leading must not scale with a child font.");
    }

    [Test]
    public void ActualPdfBaselinesRespectInheritedPercentRatherThanUnitlessLineSpacing()
    {
        static double Distance(string declaration)
        {
            var paragraphs = Read("body{font-size:16pt;line-height:" + declaration + "}" +
                "p{font-size:12pt;margin-bottom:0}", "<p>First<br>Second</p>");
            var lines = TextLayout.Layout(paragraphs, Options, CancellationToken.None)[0].Runs;
            Assert.That(lines, Has.Count.EqualTo(2));
            return Math.Abs(lines[0].Baseline - lines[1].Baseline);
        }
        Assert.That(Distance("150%"), Is.EqualTo(24).Within(0.000001));
        Assert.That(Distance("1.5"), Is.EqualTo(18).Within(0.000001));
        Assert.That(Distance("24pt"), Is.EqualTo(24).Within(0.000001));
    }

    [Test]
    public void CssWideKeywordsCopyOrResetBothLeadingRepresentations()
    {
        var p = Read("body{font-size:12pt;line-height:24pt}p{font-size:24pt}" +
            "p.inherit{line-height:inherit}p.unset{line-height:unset}" +
            "p.initial{line-height:initial}p.normal{line-height:normal}",
            "<p class=inherit>Inherited</p><p class=unset>Unset</p>" +
            "<p class=initial>Initial</p><p class=normal>Normal</p>");
        Assert.That(p[0].Style.AbsoluteLineHeight, Is.EqualTo(24));
        Assert.That(p[1].Style.AbsoluteLineHeight, Is.EqualTo(24));
        Assert.That(p[2].Style.AbsoluteLineHeight, Is.Null);
        Assert.That(p[2].Style.LineHeight, Is.EqualTo(1.2));
        Assert.That(p[3].Style.AbsoluteLineHeight, Is.Null);
        Assert.That(p[3].Style.LineHeight, Is.EqualTo(1.2));
    }

    [TestCase("0%")]
    [TestCase("-20%")]
    [TestCase("1500%")]
    [TestCase("200em")]
    [TestCase("0pt")]
    [TestCase("-2mm")]
    [TestCase("999999pt")]
    [TestCase("0rem")]
    [TestCase("2vh")]
    [TestCase("calc(1em + 2pt)")]
    public void UnsupportedOrUnboundedLeadingCannotChangeExistingOutput(string value)
        => Reject("<p style='line-height:" + value + "'>Protected</p>");

    [Test]
    public void ValidSmallRelativeHeightUsesActualComputedFontSizeNotValidationStyle()
    {
        var paragraphs = Read(
            "body{font-size:30pt}p{line-height:0.005em}",
            "<p>Actual computed line height</p>");
        Assert.That(paragraphs, Has.Count.EqualTo(1));
        Assert.That(paragraphs[0].Style.AbsoluteLineHeight,
            Is.EqualTo(0.15).Within(0.000001));

        // The same winning value is invalid when the computed font shrinks.
        Reject("<style>body{font-size:12pt}p{line-height:0.005em}</style>"
            + "<body><p>Destination remains intact</p></body>");
    }

    [Test]
    public void InvalidUnmatchedStylesheetRuleStillFailsClosed()
        => Reject("<style>#never{line-height:-1em}</style><p>Protected</p>");

    [Test]
    public void LegacyDefaultAndExplicitUnitlessOutputStayByteStable()
    {
        var legacy = Pdf("<p>Unchanged<br>Output</p>");
        Assert.That(Pdf("<p style='line-height:1.2'>Unchanged<br>Output</p>"),
            Is.EqualTo(legacy));
        Assert.That(Pdf("<p style='line-height:normal'>Unchanged<br>Output</p>"),
            Is.EqualTo(legacy));
    }
}
