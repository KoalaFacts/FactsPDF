using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// Media Queries 4 evaluated as Chrome evaluates them when it prints (R5x-resources-links-media): the
/// medium is print and the viewport is the page area. The page here is 310pt x 195pt with 18pt margins,
/// so the page area is 274pt x 159pt (365.33px x 212px, landscape, aspect ratio 1.72).
/// </summary>
[TestFixture]
public sealed class MediaQueryTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 310, PageHeight = 195, Margin = 18 };
    private const string Body = "<p>Quarterly revenue</p>";

    private static byte[] Pdf(string html, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, options ?? Page);
        return output.ToArray();
    }

    private static readonly byte[] Applied = Pdf("<style>p{color:red}</style>" + Body);
    private static readonly byte[] Ignored = Pdf(Body);

    private static void Applies(string media, bool applied)
        => Assert.That(Pdf("<style>@media " + media + "{p{color:red}}</style>" + Body), Is.EqualTo(applied ? Applied : Ignored), media);

    [TestCase("print", true)]
    [TestCase("all", true)]
    [TestCase("PRINT", true)]
    [TestCase("screen", false)]
    [TestCase("speech", false)]
    [TestCase("tv, print", true)]
    [TestCase("tv, projection", false)]
    [TestCase("not screen", true)]
    [TestCase("not print", false)]
    [TestCase("only print", true)]
    [TestCase("only screen", false)]
    [TestCase("not all and (monochrome)", true)]
    [TestCase("screen and (color)", false)]
    [TestCase("print and (color)", true)]
    public void MediaTypesAndModifiersMatchAsWhenPrinting(string media, bool applied) => Applies(media, applied);

    // Boundaries pinned on both sides: the page area is 274pt (365.33px) wide and 159pt (212px) high.
    [TestCase("(min-width:365px)", true)]
    [TestCase("(min-width:366px)", false)]
    [TestCase("(max-width:274pt)", true)]
    [TestCase("(max-width:273.9pt)", false)]
    [TestCase("(width:274pt)", true)]
    [TestCase("(min-width:20em)", true)]
    [TestCase("(min-width:23em)", false)]
    [TestCase("(max-height:212px)", true)]
    [TestCase("(max-height:211px)", false)]
    [TestCase("(width >= 274pt)", true)]
    [TestCase("(width > 274pt)", false)]
    [TestCase("(274pt < width)", false)]
    [TestCase("(274pt <= width)", true)]
    [TestCase("(200px < width <= 274pt)", true)]
    [TestCase("(400px > width > 366px)", false)]
    [TestCase("(height < 159pt)", false)]
    [TestCase("(width)", true)]
    [TestCase("(min-width:0)", true)]
    [TestCase("(max-width:0)", false)]
    public void WidthAndHeightReadThePageArea(string media, bool applied) => Applies(media, applied);

    [TestCase("(orientation:landscape)", true)]
    [TestCase("(orientation:portrait)", false)]
    [TestCase("(min-aspect-ratio:3/2)", true)]
    [TestCase("(min-aspect-ratio:16/9)", false)]
    [TestCase("(max-aspect-ratio:2)", true)]
    [TestCase("(aspect-ratio > 1)", true)]
    [TestCase("(aspect-ratio:274/159)", true)]
    public void OrientationAndAspectRatioReadThePageArea(string media, bool applied) => Applies(media, applied);

    [TestCase("(color)", true)]
    [TestCase("not (color)", false)]
    [TestCase("(monochrome)", false)]
    [TestCase("(monochrome:0)", true)]
    [TestCase("(max-monochrome:0)", true)]
    [TestCase("(min-monochrome:1)", false)]
    [TestCase("(grid)", false)]
    [TestCase("(grid:0)", true)]
    [TestCase("(color-index)", false)]
    [TestCase("(color-index:0)", true)]
    [TestCase("(color) and (monochrome)", false)]
    [TestCase("(monochrome) or (color)", true)]
    [TestCase("((monochrome) or (color)) and (orientation:landscape)", true)]
    [TestCase("not ((monochrome) or (grid))", true)]
    public void FixedPrintFeaturesAndConditions(string media, bool applied) => Applies(media, applied);

    [Test]
    public void ANonMatchingBlockIsIgnoredWholeEvenWhenItsContentIsUnsupported()
    {
        // Chrome never applies the block, so its declarations are not checked; a matching block still is.
        Assert.That(Pdf("<style>@media screen{p{display:inline-flex}@page{margin:1cm}}</style>" + Body), Is.EqualTo(Ignored));
        Assert.That(Assert.Throws<FactsPdfException>(() => Pdf("<style>@media print{p{display:inline-flex}}</style>" + Body))!.Code,
            Is.EqualTo("FPDF1202"));
    }

    [Test]
    public void MediaQueriesNestInStyleRulesAndInOneAnother()
    {
        Assert.That(Pdf("<style>p{@media (orientation:landscape){color:red}}</style>" + Body), Is.EqualTo(Applied));
        Assert.That(Pdf("<style>@media print{@media (max-width:100px){p{color:red}}}</style>" + Body), Is.EqualTo(Ignored));
    }

    [TestCase("screen", false)]
    [TestCase("print and (orientation:landscape)", true)]
    [TestCase("(max-width:100px)", false)]
    [TestCase(" all ", true)]
    public void StyleMediaAttributeUsesTheSameEvaluator(string media, bool applied)
        => Assert.That(Pdf("<style media='" + media + "'>p{color:red}</style>" + Body), Is.EqualTo(applied ? Applied : Ignored));

    [TestCase("(hover:hover)")]
    [TestCase("(pointer:fine)")]
    [TestCase("(any-hover)")]
    [TestCase("(resolution:1dppx)")]
    [TestCase("(prefers-color-scheme:light)")]
    [TestCase("(prefers-reduced-motion)")]
    [TestCase("(scripting:enabled)")]
    [TestCase("(update:none)")]
    [TestCase("(device-width:100px)")]
    [TestCase("(color:8)")]
    [TestCase("(min-color:1)")]
    [TestCase("(width:10vw)")]
    [TestCase("(width:calc(1px + 1px))")]
    [TestCase("(min-width:-1px)")]
    [TestCase("(min-width)")]
    [TestCase("(widht:100px)")]
    [TestCase("(orientation:sideways)")]
    [TestCase("(grid:2)")]
    [TestCase("(aspect-ratio:0/1)")]
    [TestCase("(orientation > 1)")]
    [TestCase("(100px < width = 300px)")]
    [TestCase("foo(bar)")]
    [TestCase("()")]
    [TestCase("print, (hover)")]
    public void UnmodelledFeaturesFailInBothModes(string media)
    {
        var html = "<style>@media " + media + "{p{color:red}}</style>" + Body;
        Assert.That(Assert.Throws<FactsPdfException>(() => Pdf(html))!.Code, Is.EqualTo("FPDF1204"), media);
        Assert.That(Assert.Throws<FactsPdfException>(() => Pdf(html, Page with { Strict = false }))!.Code, Is.EqualTo("FPDF1204"), media);
    }

    [TestCase("screen or print", false)]
    [TestCase("only (color)", false)]
    [TestCase("print and", false)]
    [TestCase("(color) and (grid) or (monochrome)", false)]
    [TestCase("and print", false)]
    [TestCase("screen or print, print", true)]
    public void MalformedQueriesFailStrictAndAreNotAllWhenNonStrict(string media, bool applied)
    {
        var html = "<style>@media " + media + "{p{color:red}}</style>" + Body;
        Assert.That(Assert.Throws<FactsPdfException>(() => Pdf(html))!.Code, Is.EqualTo("FPDF1204"), media);
        PdfWarnings? warnings = null;
        var pdf = Pdf(html, Page with { Strict = false, WarningHandler = w => warnings = w });
        var expected = applied ? Pdf("<style>p{color:red}</style>" + Body, Page with { Strict = false })
            : Pdf(Body, Page with { Strict = false });
        Assert.That(pdf, Is.EqualTo(expected), media);
        Assert.That(warnings!.Items.Select(x => x.Code), Is.EqualTo(new[] { "FPDF1204" }), media);
    }

    [Test]
    public void PageAreaFeaturesCannotBeCombinedWithAnAtPageSizeOrMargin()
    {
        Assert.That(Assert.Throws<FactsPdfException>(() => Pdf("<style>@page{size:400pt 300pt}@media (min-width:1px){p{color:red}}</style>" + Body))!.Code,
            Is.EqualTo("FPDF1204"));
        Assert.That(Assert.Throws<FactsPdfException>(() => Pdf("<style>@media (orientation:landscape){p{color:red}}@page{margin:10pt}</style>" + Body))!.Code,
            Is.EqualTo("FPDF1204"));
        // Over-correction: a media type or a fixed feature never reads the page area.
        Assert.That(Pdf("<style>@page{size:310pt 195pt;margin:18pt}@media print and (color){p{color:red}}</style>" + Body),
            Is.EqualTo(Pdf("<style>@page{size:310pt 195pt;margin:18pt}p{color:red}</style>" + Body)));
    }

    [Test]
    public void PageAreaFeaturesFailWhenContentWiderThanThePagePrintsScaledDown()
    {
        const string wide = "<div style='width:400pt'>Wide</div>";
        Assert.That(Assert.Throws<FactsPdfException>(() => Pdf("<style>@media (min-width:1px){p{color:red}}</style>" + wide))!.Code,
            Is.EqualTo("FPDF1204"));
        Assert.DoesNotThrow(() => Pdf("<style>@media (color){p{color:red}}</style>" + wide));
    }
}
