using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>Scoped parent-relative typography must change real PDF glyph metrics.</summary>
[TestFixture]
public sealed class RelativeFontSizingTests
{
    private static List<Paragraph> Read(string rules, string markup)
        => HtmlDocumentReader.Read(
            "<html><head><style>" + rules + "</style></head><body>" + markup + "</body></html>",
            new PdfOptions(), CancellationToken.None);

    private static byte[] Pdf(string html)
    {
        using var stream = new MemoryStream();
        PdfConverter.Convert(html, stream, new PdfOptions { PageWidth = 300, PageHeight = 400, Margin = 20 });
        return stream.ToArray();
    }

    private static void Reject(string html)
    {
        using var stream = new MemoryStream();
        stream.Write("keep-original"u8);
        stream.Position = 3;
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, stream, new PdfOptions()));
        Assert.That(error!.Code, Is.EqualTo("FPDF1202"));
        Assert.That(stream.ToArray(), Is.EqualTo("keep-original"u8.ToArray()));
        Assert.That(stream.Position, Is.EqualTo(3));
    }

    [Test]
    public void PercentAndEmUseComputedParentNotHeadingMultiplier()
    {
        var paragraphs = Read(
            "body{font-size:16pt}h1{font-size:125%}p{font-size:1.5em}",
            "<h1>Report</h1><p>Base <span style='font-size:125%'>Bigger</span> After</p>");
        Assert.That(paragraphs, Has.Count.EqualTo(2));
        Assert.That(paragraphs[0].Style.FontSize, Is.EqualTo(20).Within(0.000001));
        Assert.That(paragraphs[1].Style.FontSize, Is.EqualTo(24).Within(0.000001));
        Assert.That(paragraphs[1].Runs.Single(r => r.Text == "Bigger").Style.FontSize,
            Is.EqualTo(30).Within(0.000001));
        Assert.That(paragraphs[1].Runs.Last().Style.FontSize, Is.EqualTo(24).Within(0.000001));
    }

    [Test]
    public void NestedRelativeTypographyIsByteIdenticalToExplicitPointSizes()
    {
        var body = "<body><h1>Service statement</h1><p>Report <span>Important</span> details</p></body>";
        var relative = "<style>body{font-size:16pt}h1{font-size:125%}"
            + "p{font-size:1.5em}p span{font-size:125%}</style>";
        var absolute = "<style>body{font-size:16pt}h1{font-size:20pt}"
            + "p{font-size:24pt}p span{font-size:30pt}</style>";
        var result = Pdf(relative + body);
        Assert.That(result, Is.EqualTo(Pdf(absolute + body)));
        Assert.That(result, Is.Not.EqualTo(Pdf(body)), "Relative sizes must affect actual PDF paint/spacing.");
    }

    [Test]
    public void InlinePercentAndEmFollowCascadeAndImportant()
    {
        var paragraphs = Read("body{font-size:20pt}p{font-size:125%!important}",
            "<p style='font-size:2em'>Protected</p><p>Sibling</p>");
        Assert.That(paragraphs[0].Style.FontSize, Is.EqualTo(25).Within(0.000001));
        Assert.That(paragraphs[1].Style.FontSize, Is.EqualTo(25).Within(0.000001));
    }

    [Test]
    public void CssWideKeywordsStillResolveToRootOrParent()
    {
        var paragraphs = Read("body{font-size:20pt}p{font-size:1em}p.reset{font-size:initial}",
            "<p>Parent</p><p class=reset>Initial</p>");
        Assert.That(paragraphs[0].Style.FontSize, Is.EqualTo(20));
        Assert.That(paragraphs[1].Style.FontSize, Is.EqualTo(12));
    }

    [TestCase("0%")]
    [TestCase("-10%")]
    [TestCase("0em")]
    [TestCase("-1em")]
    [TestCase("15000%")]
    [TestCase("200em")]
    [TestCase("1rem")]
    [TestCase("1vh")]
    [TestCase("calc(2em)")]
    [TestCase("bogus")]
    public void BadRelativeSizeFailsBeforeOutputIsChanged(string value)
        => Reject("<p style='font-size:" + value + "'>Protected</p>");

    [Test]
    public void InvalidUnmatchedRuleStillFailsClosed()
        => Reject("<style>#missing{font-size:-5em}</style><p>Protected</p>");

    [TestCase("body{font-size:144pt}p{font-size:125%}")]
    [TestCase("body{font-size:1pt}p{font-size:50%}")]
    public void ComputedSizeOutsideOneTo144PointsFailsWithoutOutput(string css)
        => Reject("<style>" + css + "</style><body><p>Protected</p></body>");

    [Test]
    public void UnstyledAndExistingAbsoluteDocumentStayByteStable()
    {
        var original = Pdf("<p>Original output</p>");
        Assert.That(Pdf("<p style='font-size:12pt'>Original output</p>"), Is.EqualTo(original));
    }
}
