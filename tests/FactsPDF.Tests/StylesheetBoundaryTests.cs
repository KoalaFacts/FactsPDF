using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

[TestFixture]
public sealed class StylesheetBoundaryTests
{
    private static List<Paragraph> Read(string html, PdfOptions? options = null)
        => HtmlDocumentReader.Read(html, options ?? new(), CancellationToken.None);

    [Test]
    public void CandidateIndexSkipsThousandsOfUnrelatedClassRules()
    {
        var css = string.Concat(Enumerable.Range(0, 2000).Select(i => ".unrelated" + i + "{color:red}"));
        var p = Read("<style>" + css + ".selected{color:blue}</style><p class=selected>A</p>", new() { MaxCssMatchOperations = 10 });
        Assert.That(p[0].Style.Color, Is.EqualTo(new Rgb(0, 0, 1)));
    }
    [Test]
    public void WinningDeclarationsDoNotLeakIntoTheNextSibling()
    {
        var p = Read("<style>.a{color:red;font-size:18pt}</style><p class=a>A</p><p>B</p>");
        Assert.That(p[0].Style.Color, Is.EqualTo(new Rgb(1, 0, 0)));
        Assert.That(p[1].Style.Color, Is.EqualTo(new Rgb(0, 0, 0)));
        Assert.That(p[1].Style.FontSize, Is.EqualTo(12));
    }
    [Test]
    public void HtmlEndTagTerminatesStyleEvenInsideACssComment()
    {
        var error = Assert.Throws<FactsPdfException>(() => Read("<style>/* </style> */<p>A</p>"));
        Assert.That(error!.Code, Is.EqualTo("FPDF1203"));
    }
    [Test]
    public void EmptyStylesAndRulesAreValidAndDoNotCreateText()
    {
        using var output = new MemoryStream();
        var result = PdfConverter.Convert("<style> /* empty */ p{} </style>", output);
        Assert.That(result.PageCount, Is.EqualTo(1));
        Assert.That(Encoding.ASCII.GetString(output.ToArray()), Does.Not.Contain(" Tj"));
    }
    [TestCase("color:initial", "color")]
    [TestCase("line-height:initial", "line-height")]
    [TestCase("text-align:initial", "text-align")]
    public void InitialResetsOnlyTheWinningProperty(string declaration, string property)
    {
        var s = Read("<style>body{color:red;line-height:2;text-align:right}p{" + declaration + "}</style><body><p>A</p></body>")[0].Style;
        Assert.That(s.Color, Is.EqualTo(property == "color" ? new Rgb(0, 0, 0) : new Rgb(1, 0, 0)));
        Assert.That(s.LineHeight, Is.EqualTo(property == "line-height" ? 1.2 : 2));
        Assert.That(s.Alignment, Is.EqualTo(property == "text-align" ? TextAlignment.Left : TextAlignment.Right));
    }
    [Test]
    public void InlineInheritUsesParentValueRatherThanAWeakerStylesheetValue()
    {
        var s = Read("<style>body{color:red}p{color:blue}</style><body><p style='color:inherit'>A</p></body>")[0].Style;
        Assert.That(s.Color, Is.EqualTo(new Rgb(1, 0, 0)));
    }
    [Test]
    public void MultipleMatchingGroupArmsUseTheStrongestMatchingSpecificity()
    {
        var s = Read("<style>.x,#a{color:red}p.x{color:blue}</style><p id=a class=x>A</p>")[0].Style;
        Assert.That(s.Color, Is.EqualTo(new Rgb(1, 0, 0)));
    }
    [Test]
    public void StyleCommentsAreCountedInTheCharacterBudget()
    {
        var error = Assert.Throws<FactsPdfException>(() => Read("<style>/*" + new string('x', 100) + "*/</style>", new() { MaxCssCharacters = 50 }));
        Assert.That(error!.Code, Is.EqualTo("FPDF1205"));
    }
    [Test]
    public void GroupedEmptySelectorsStillConsumeTheSelectorBudget()
    {
        var error = Assert.Throws<FactsPdfException>(() => Read("<style>p,span{}</style>", new() { MaxCssSelectors = 1 }));
        Assert.That(error!.Code, Is.EqualTo("FPDF1205"));
    }
    [Test]
    public void RepeatedImportantMarkerIsRejected()
    {
        var error = Assert.Throws<FactsPdfException>(() => Read("<p style='color:red!important!important'>A</p>"));
        Assert.That(error!.Code, Is.EqualTo("FPDF1202"));
    }
    [Test]
    public void MatchedBrStyleIsExplicitlyUnsupported()
    {
        var error = Assert.Throws<FactsPdfException>(() => Read("<style>br{color:red}</style><p>A<br>B</p>"));
        Assert.That(error!.Code, Is.EqualTo("FPDF1103"));
    }
}
