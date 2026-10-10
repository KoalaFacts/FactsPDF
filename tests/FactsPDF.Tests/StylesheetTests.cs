using System.Globalization;
using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

[TestFixture]
public sealed class StylesheetTests
{
    private static readonly Rgb Red = new(1, 0, 0), Blue = new(0, 0, 1), Green = new(0, 128d / 255, 0), Black = new(0, 0, 0);
    private static List<Paragraph> Read(string css, string body = "<p>A</p>", PdfOptions? options = null)
        => HtmlDocumentReader.Read("<html><head><style>" + css + "</style></head><body>" + body + "</body></html>", options ?? new(), CancellationToken.None);
    private static byte[] Pdf(string html, PdfOptions? options = null)
    { using var output = new MemoryStream(); PdfConverter.Convert(html, output, options); return output.ToArray(); }
    private static FactsPdfException Fails(string html, string code, PdfOptions? options = null)
    {
        using var output = new MemoryStream(); output.Write("keep"u8);
        var ex = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, options));
        Assert.That(ex!.Code, Is.EqualTo(code)); Assert.That(Encoding.ASCII.GetString(output.ToArray()), Is.EqualTo("keep")); return ex;
    }

    [TestCase("p", "<p>A</p>")] [TestCase("P", "<p>A</p>")] [TestCase("*", "<p>A</p>")]
    [TestCase(".note", "<p class='other note'>A</p>")] [TestCase("#a", "<p id=a>A</p>")]
    [TestCase("p.note.active#a", "<p id=a class='note active'>A</p>")]
    [TestCase("*.note", "<p class=note>A</p>")]
    public void SupportedSimpleSelectorsApply(string selector, string body)
        => Assert.That(Read(selector + "{color:red}", body)[0].Runs[0].Style.Color, Is.EqualTo(Red));

    [TestCase(".note", "<p class=noteworthy>A</p>")]
    [TestCase(".Note", "<p class=note>A</p>")]
    [TestCase("#A", "<p id=a>A</p>")]
    [TestCase("p.note.active", "<p class=note>A</p>")]
    public void NonMatchingSelectorsDoNotStyleContent(string selector, string body)
        => Assert.That(Read(selector + "{color:red}", body)[0].Style.Color, Is.EqualTo(Black));

    [Test]
    public void CommaGroupsAndMultipleClassesMatchIndependently()
    {
        var p = Read("h1, p.x.y {color:blue}", "<h1>A</h1><p class='x y'>B</p><p class=x>C</p>");
        Assert.That(p.Select(x => x.Style.Color), Is.EqualTo(new[] { Blue, Blue, Black }));
    }
    [Test]
    public void UnmatchedIdInAGroupDoesNotRaiseMatchingClassSpecificity()
        => Assert.That(Read(".x,#missing {color:red} p.x {color:blue}", "<p class=x>A</p>")[0].Style.Color, Is.EqualTo(Blue));
    [Test]
    public void SpecificityIsLexicographicNotDecimalWeighted()
    {
        var selector = "p" + string.Concat(Enumerable.Repeat(".x", 20));
        Assert.That(Read("#a {color:red}" + selector + "{color:blue}", "<p id=a class=x>A</p>")[0].Style.Color, Is.EqualTo(Red));
    }
    [Test]
    public void RepeatedClassesIncreaseSpecificity()
        => Assert.That(Read(".x.x{color:red}.x{color:blue}", "<p class=x>A</p>")[0].Style.Color, Is.EqualTo(Red));
    [Test]
    public void EqualSpecificityUsesLastRuleAndLastDeclaration()
        => Assert.That(Read("p{color:red}p{color:green;color:blue}")[0].Style.Color, Is.EqualTo(Blue));
    [Test]
    public void CascadeChoosesEachPropertySeparately()
    {
        var s = Read("p{font-size:18pt;color:red}.x{color:blue}", "<p class=x>A</p>")[0].Style;
        Assert.That(s.FontSize, Is.EqualTo(18)); Assert.That(s.Color, Is.EqualTo(Blue));
    }
    [TestCase("p{color:red}", "color:blue", false)]
    [TestCase("#a{color:red !important}", "color:blue", true)]
    [TestCase("#a{color:red !important}", "color:blue !important", false)]
    public void InlineAndImportantUseTheCorrectOrder(string css, string inline, bool red)
        => Assert.That(Read(css, "<p id=a style='" + inline + "'>A</p>")[0].Style.Color, Is.EqualTo(red ? Red : Blue));
    [Test]
    public void ImportantIsCaseInsensitiveAndCanContainComments()
        => Assert.That(Read("p{color:red !/**/IMPORTANT; color:blue}")[0].Style.Color, Is.EqualTo(Red));
    [Test]
    public void InlineDuplicateDeclarationDoesNotOverrideAnEarlierImportant()
        => Assert.That(Read("", "<p style='color:red!important;color:blue'>A</p>")[0].Style.Color, Is.EqualTo(Red));
    [Test]
    public void DescendantAndChildCombinatorsDiffer()
    {
        var p = Read(".box p{font-size:18pt}.box>p{color:red}", "<div class=box><p>A</p><div><p>B</p></div></div>");
        Assert.That(p.Select(x => x.Style.FontSize), Is.All.EqualTo(18));
        Assert.That(p.Select(x => x.Style.Color), Is.EqualTo(new[] { Red, Black }));
    }
    [Test]
    public void MixedCombinatorsRetryAnEarlierMatchingAncestor()
    {
        const string body = "<div class=a><div class=b><div><div class=b><p class=c>A</p></div></div></div></div>";
        Assert.That(Read(".a > .b .c{color:red}", body)[0].Style.Color, Is.EqualTo(Red));
    }
    [Test]
    public void ChildSelectorsUseHtmlOmittedParagraphEndStructure()
    {
        var p = Read("body > p{color:red} p p{color:blue}", "<p>A<p>B");
        Assert.That(p.Select(x => x.Style.Color), Is.All.EqualTo(Red));
    }
    [Test]
    public void SyntheticRootDoesNotMatchUniversalParent()
        => Assert.That(HtmlDocumentReader.Read("<style>* > p{color:red}</style><p>A</p>", new(), default)[0].Style.Color, Is.EqualTo(Black));
    [Test]
    public void ClassTokensAreDecodedAndSplitOnHtmlWhitespace()
        => Assert.That(Read(".b{color:red}", "<p class='a&#32;b\tc'>A</p>")[0].Style.Color, Is.EqualTo(Red));
    [Test]
    public void LaterStyleBlocksAffectEarlierContentAndUseDocumentOrder()
    {
        const string html = "<p>A</p><style>p{color:red}</style><style>p{color:blue}</style>";
        Assert.That(HtmlDocumentReader.Read(html, new(), default)[0].Style.Color, Is.EqualTo(Blue));
    }
    [Test]
    public void CommentsDoNotCreateDescendantCombinators()
        => Assert.That(Read("p/**/.x{/* <fake> */color:/**/red/**/}", "<p class=x>A</p>")[0].Style.Color, Is.EqualTo(Red));
    [Test]
    public void RealWhitespaceAroundCommentsStillCreatesADescendant()
        => Assert.That(Read("div/**/ .x{color:red}", "<div><p class=x>A</p></div>")[0].Style.Color, Is.EqualTo(Red));
    [Test]
    public void RawStyleDoesNotDecodeCharacterReferencesOrFakeEndTags()
        => Assert.That(Read("/* &unknown; </stylex> <p> */ p{color:red}")[0].Style.Color, Is.EqualTo(Red));
    [Test]
    public void RawStyleEndTagIsCaseInsensitiveAndAllowsTrailingWhitespace()
        => Assert.That(HtmlDocumentReader.Read("<STYLE>p{color:red}</StYlE ><p>A</p>", new(), default)[0].Style.Color, Is.EqualTo(Red));
    [Test]
    public void MetadataAndCssNeverAppearAsPdfText()
    {
        var bytes = Pdf("<head><style type='text/css' media=print>p{color:red}</style><title>Invisible</title></head><p>A</p>");
        var text = Encoding.ASCII.GetString(bytes);
        Assert.That(text, Does.Contain("<41> Tj")); Assert.That(text, Does.Not.Contain("Invisible")); Assert.That(text, Does.Not.Contain("color:red"));
    }
    [Test]
    public void AuthorDeclarationsOverrideHeadingDefaults()
        => Assert.That(Read("h1{font-size:12pt}", "<h1>A</h1>")[0].Style.FontSize, Is.EqualTo(12));
    [Test]
    public void InheritedImportantDoesNotDefeatAChildDeclaration()
    {
        var p = Read("body{color:red!important;font-size:18pt;line-height:2;text-align:center} p{color:blue}")[0];
        Assert.That(p.Style.Color, Is.EqualTo(Blue)); Assert.That(p.Style.FontSize, Is.EqualTo(18));
        Assert.That(p.Style.LineHeight, Is.EqualTo(2)); Assert.That(p.Style.Alignment, Is.EqualTo(TextAlignment.Center));
    }
    [Test]
    public void InheritedSpanStylesDoNotCarryParagraphMarginsOrBreaks()
    {
        var p = Read("p{color:red;margin-bottom:20pt;break-after:page}", "<p><span>A</span></p>")[0];
        Assert.That(p.Runs[0].Style.Color, Is.EqualTo(Red)); Assert.That(p.Runs[0].Style.MarginAfter, Is.Zero); Assert.That(p.Runs[0].Style.BreakAfter, Is.False);
    }
    [TestCase("inherit", 18)] [TestCase("unset", 18)] [TestCase("initial", 12)]
    public void CssWideKeywordsResolveAgainstParentOrInitialNotHeadingDefaults(string keyword, int expected)
        => Assert.That(Read("body{font-size:18pt}h1{font-size:" + keyword + "}", "<h1>A</h1>")[0].Style.FontSize, Is.EqualTo(expected));
    [Test]
    public void UnsetNonInheritedPropertiesUseInitialRatherThanUaParagraphMargin()
    {
        var p = Read("p{margin-bottom:unset;break-before:initial}")[0];
        Assert.That(p.Style.MarginAfter, Is.Zero); Assert.That(p.Style.BreakBefore, Is.False);
    }
    [Test]
    public void StylesheetAndIndependentlyWrittenInlineVersionProduceIdenticalPdfBytes()
    {
        var sheet = "<style>body{color:blue}p{font-size:14pt;line-height:1.5}.next{break-before:page}p span{color:red}</style><body><p>A<span>B</span></p><p class=next>C</p></body>";
        var inline = "<body style='color:blue'><p style='font-size:14pt;line-height:1.5'>A<span style='color:red'>B</span></p><p style='font-size:14pt;line-height:1.5;break-before:page'>C</p></body>";
        Assert.That(Pdf(sheet), Is.EqualTo(Pdf(inline)));
    }

    [TestCase("p:hover{color:red}", "FPDF1203")] [TestCase("[id=x]{color:red}", "FPDF1203")]
    [TestCase("p+p{color:red}", "FPDF1203")] [TestCase("p~p{color:red}", "FPDF1203")]
    [TestCase(".\\78{color:red}", "FPDF1203")] [TestCase("p, {color:red}", "FPDF1203")]
    [TestCase("p/**/span{color:red}", "FPDF1203")] [TestCase("p > > span{color:red}", "FPDF1203")]
    [TestCase("p{color:re/**/d}", "FPDF1202")] [TestCase("p{font-size:12 pt}", "FPDF1202")]
    [TestCase("p{color:red !oops}", "FPDF1202")] [TestCase("p{color:red", "FPDF1203")]
    [TestCase("p{color:red}/*unfinished", "FPDF1203")]
    [TestCase("@import 'https://example.invalid/x.css';", "FPDF1204")]
    [TestCase("@media print{p{color:red}}", "FPDF1204")]
    [TestCase(".never{display:flex}", "FPDF1201")]
    public void UnsupportedOrMalformedCssFailsWithoutOutput(string css, string code)
        => Fails("<style>" + css + "</style><p>A</p>", code);
    [TestCase("<style media=screen>p{color:red}</style><p>A</p>", "FPDF1204")]
    [TestCase("<style type='text/less'>p{color:red}</style><p>A</p>", "FPDF1204")]
    [TestCase("<link rel=stylesheet href='https://example.invalid/a.css'><p>A</p>", "FPDF1102")]
    [TestCase("<p><style>p{color:red}</style>A</p>", "FPDF1101")]
    [TestCase("<style>p{color:red}", "FPDF1101")]
    [TestCase("<style/>p{color:red}", "FPDF1101")]
    public void UnsupportedStyleSourcesHaveExplicitBoundaries(string html, string code) => Fails(html, code);
    [Test]
    public void CssErrorsHaveSourceOffsetsInTheHtml()
    {
        var ex = Fails("<head><style>p{color:potato}</style></head><p>A</p>", "FPDF1202");
        Assert.That(ex.SourceOffset, Is.GreaterThanOrEqualTo(12)); Assert.That(ex.SourceOffset, Is.LessThan(30));
    }
    [Test]
    public void UnsupportedParagraphLayoutOnAContainerIsNotSilentlyIgnored()
        => Fails("<style>div{margin-top:12pt}</style><div><p>A</p></div>", "FPDF1201");
    [Test]
    public void AggregateCssCharacterBudgetIncludesInlineAndEmbeddedStyles()
        => Fails("<style>p{color:red}</style><p style='color:blue'>A</p>", "FPDF1205", new() { MaxCssCharacters = 18 });
    [Test]
    public void SelectorBudgetCountsCommaArms()
        => Fails("<style>p,h1,span{color:red}</style><p>A</p>", "FPDF1205", new() { MaxCssSelectors = 2 });
    [Test]
    public void DeclarationBudgetIncludesInlineDeclarations()
        => Fails("<style>p{color:red}</style><p style='font-size:14pt'>A</p>", "FPDF1205", new() { MaxCssDeclarations = 1 });
    [Test]
    public void MatchingWorkBudgetStopsBeforePdfOutput()
        => Fails("<style>p{color:red}p{font-size:14pt}</style><p>A</p>", "FPDF1205", new() { MaxCssMatchOperations = 1 });
    [Test]
    public void SelectorComplexityIsBounded()
        => Fails("<style>p" + string.Concat(Enumerable.Repeat(".x", 33)) + "{color:red}</style><p>A</p>", "FPDF1205");
    [Test]
    public void ZeroCssBudgetsAreRejectedEvenWhenThereIsNoCss()
        => Assert.Throws<ArgumentOutOfRangeException>(() => Pdf("<p>A</p>", new() { MaxCssCharacters = 0 }));
    [Test]
    public void PreCancelledStylesheetConversionPreservesOutput()
    {
        using var output = new MemoryStream(); output.Write("keep"u8);
        Assert.Throws<OperationCanceledException>(() => PdfConverter.Convert("<style>p{color:red}</style><p>A</p>", output, cancellationToken: new(true)));
        Assert.That(Encoding.ASCII.GetString(output.ToArray()), Is.EqualTo("keep"));
    }
    [Test]
    public void ConcurrentDocumentsDoNotLeakRulesAndAreCultureIndependent()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var a = Pdf("<style>p{font-size:10.5pt;color:red}</style><p>A</p>");
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            Parallel.For(0, 8, _ => Assert.That(Pdf("<style>p{font-size:10.5pt;color:red}</style><p>A</p>"), Is.EqualTo(a)));
            Assert.That(Read("p{color:blue}")[0].Style.Color, Is.EqualTo(Blue));
        }
        finally { CultureInfo.CurrentCulture = original; }
    }
}
