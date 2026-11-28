using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// @supports answers as Chrome's grammar answers (R5x-resources-links-media): a declaration the
/// specifications prove invalid is false, one FactsPDF compiles is true, and one FactsPDF can neither
/// prove invalid nor compile fails FPDF1204 instead of guessing.
/// </summary>
[TestFixture]
public sealed class SupportsRuleTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 200, Margin = 20 };
    private const string Body = "<p>Invoice <span>42</span></p>";

    private static byte[] Pdf(string html, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, options ?? Page);
        return output.ToArray();
    }

    private static readonly byte[] Applied = Pdf("<style>p{color:red}</style>" + Body);
    private static readonly byte[] Ignored = Pdf(Body);

    [TestCase("(display:grid)", true)]
    [TestCase("(display: flex)", true)]
    [TestCase("(color:red !important)", true)]
    [TestCase("(display:grid) and (color:red)", true)]
    [TestCase("(display:grid) and (display:bogus)", false)]
    [TestCase("(display:bogus) or (color:red)", true)]
    [TestCase("not (display:grid)", false)]
    [TestCase("(display:bogus)", false)]
    [TestCase("not (display:bogus)", true)]
    [TestCase("(colour:red)", false)]
    [TestCase("(--accent: anything at all)", true)]
    [TestCase("(color: var(--accent))", true)]
    [TestCase("((display:grid) or (display:bogus)) and (not (colour:red))", true)]
    [TestCase("selector(p > span)", true)]
    [TestCase("selector(p >)", false)]
    [TestCase("selector(p, span)", false)]
    [TestCase("not selector(p >)", true)]
    public void ConditionsAnswerAsChromeAnswers(string condition, bool applied)
        => Assert.That(Pdf("<style>@supports " + condition + "{p{color:red}}</style>" + Body), Is.EqualTo(applied ? Applied : Ignored), condition);

    [Test]
    public void SupportsNestsInMediaInStyleRulesAndInLayers()
    {
        Assert.That(Pdf("<style>@media print{@supports (display:grid){p{color:red}}}</style>" + Body), Is.EqualTo(Applied));
        Assert.That(Pdf("<style>p{@supports (display:grid){color:red}}</style>" + Body), Is.EqualTo(Applied));
        Assert.That(Pdf("<style>@layer base{@supports (display:grid){p{color:red}}}</style>" + Body), Is.EqualTo(Applied));
    }

    [Test]
    public void OnlyAMatchingBlockIsChecked()
    {
        Assert.That(Pdf("<style>@supports not (display:grid){p{display:inline-flex}}</style>" + Body), Is.EqualTo(Ignored));
        Assert.That(Assert.Throws<FactsPdfException>(() => Pdf("<style>@supports (display:grid){p{display:inline-flex}}</style>" + Body))!.Code,
            Is.EqualTo("FPDF1202"));
    }

    // Valid in Chrome's grammar but not implemented, or not decidable: guessing could apply a block Chrome
    // ignores or ignore one it applies.
    [TestCase("(display:ruby)")]
    [TestCase("(display)")]
    [TestCase("(foo bar)")]
    [TestCase("()")]
    [TestCase("font-format(woff2)")]
    [TestCase("font-tech(color-COLRv1)")]
    [TestCase("selector(p:hover)")]
    [TestCase("(color:red) or (display:ruby)")]
    public void UndecidableConditionsFailInBothModes(string condition)
    {
        var html = "<style>@supports " + condition + "{p{color:red}}</style>" + Body;
        Assert.That(Assert.Throws<FactsPdfException>(() => Pdf(html))!.Code, Is.EqualTo("FPDF1204"), condition);
        Assert.That(Assert.Throws<FactsPdfException>(() => Pdf(html, Page with { Strict = false }))!.Code, Is.EqualTo("FPDF1204"), condition);
    }

    [TestCase("display:grid")]
    [TestCase("(display:grid) and (color:red) or (color:blue)")]
    [TestCase("(display:grid) and")]
    [TestCase("not")]
    public void MalformedConditionsFailStrictAndDropTheRuleWhenNonStrict(string condition)
    {
        var html = "<style>@supports " + condition + "{p{color:red}}</style>" + Body;
        Assert.That(Assert.Throws<FactsPdfException>(() => Pdf(html))!.Code, Is.EqualTo("FPDF1204"), condition);
        PdfWarnings? warnings = null;
        var pdf = Pdf(html, Page with { Strict = false, WarningHandler = w => warnings = w });
        Assert.That(pdf, Is.EqualTo(Pdf(Body, Page with { Strict = false })), condition);
        Assert.That(warnings!.Items.Select(x => x.Code), Is.EqualTo(new[] { "FPDF1204" }), condition);
    }

    [Test]
    public void DeclarationTestsChargeTheDeclarationBudget()
    {
        var error = Assert.Throws<FactsPdfException>(() =>
            Pdf("<style>@supports (display:grid) and (color:red){p{color:red}}</style>" + Body, Page with { MaxCssDeclarations = 1 }))!;
        Assert.That(error.Code, Is.EqualTo("FPDF1205"));
    }
}
