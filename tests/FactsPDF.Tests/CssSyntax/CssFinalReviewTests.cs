using FactsPDF.CssSyntax;
using NUnit.Framework;

namespace FactsPDF.Tests.CssSyntax;

[TestFixture]
public sealed class CssFinalReviewTests
{
    private static CssSyntaxResult ParseDeclarations(string css)
    {
        var limits = new CssSyntaxLimits();
        return CssSyntaxParser.ParseDeclarations(CssSourceText.Create(css, 0, limits), limits);
    }
    private static CssSyntaxResult ParseStylesheet(string css)
    {
        var limits = new CssSyntaxLimits();
        return CssSyntaxParser.ParseStylesheet(CssSourceText.Create(css, 0, limits), limits);
    }

    [Test]
    public void UnknownAtRuleRetainsParsedDeclarationsAndNestedRules()
    {
        var parsed = ParseStylesheet("@future { color:red; div{color:blue} }");
        Assert.That(parsed.Diagnostics, Is.Empty);
        var rule = (CssAtRuleNode)parsed.Rules.Single();
        Assert.That(rule.Contents, Is.Not.Null);
        Assert.That(rule.Contents!.OfType<CssDeclarationNode>().Select(x => x.Name), Is.EqualTo(new[] { "color" }));
        Assert.That(rule.Contents!.OfType<CssQualifiedRuleNode>().Count(), Is.EqualTo(1));
    }

    [Test]
    public void SoleBraceValueWithTrailingJunkIsRejectedButNextDeclarationSurvives()
    {
        var parsed = ParseStylesheet("p{x:{} junk;color:red}");
        var body = ((CssQualifiedRuleNode)parsed.Rules.Single()).Contents;
        Assert.That(body.OfType<CssDeclarationNode>().Select(x => x.Name),
            Is.EqualTo(new[] { "color" }));
        Assert.That(parsed.Diagnostics, Is.Not.Empty);
    }

    [Test]
    public void InlineCommentErrorOffsetMapsThroughHtmlEntity()
    {
        var html = "<p style='color:r&#101;d; /*unfinished'>A</p>";
        var exception = Assert.Throws<FactsPdfException>(() =>
        {
            using var pdf = new MemoryStream();
            PdfConverter.Convert(html, pdf);
        });
        Assert.That(exception!.Code, Is.EqualTo("FPDF1203"));
        Assert.That(exception.SourceOffset, Is.EqualTo(html.IndexOf("/*unfinished", StringComparison.Ordinal)));
    }

    [Test]
    public void InlineClosingBraceIsPreservedAsInvalidValueRatherThanTruncating()
    {
        var parsed = ParseDeclarations("--x:red}blue");
        Assert.That(parsed.Declarations.Select(x => x.Name), Is.EqualTo(new[] { "--x" }));
        Assert.That(parsed.Diagnostics, Is.Not.Empty);
        Assert.That(parsed.Declarations[0].Values.OfType<CssTokenComponent>()
            .Any(x => x.Token.Kind == CssSyntaxTokenKind.CloseBrace), Is.True);
    }

    [Test]
    public void LeadingWhitespaceAfterColonIsNotPartOfValue()
    {
        var parsed = ParseDeclarations("color:  red");
        Assert.That(parsed.Diagnostics, Is.Empty);
        Assert.That(parsed.Declarations.Single().Values[0],
            Is.TypeOf<CssTokenComponent>());
        var first = (CssTokenComponent)parsed.Declarations.Single().Values[0];
        Assert.That(first.Token.Kind, Is.EqualTo(CssSyntaxTokenKind.Ident));
        Assert.That(first.Token.Value, Is.EqualTo("red"));
    }

    [Test]
    public void EndOfFileAfterEscapedQuoteReportsUnclosedString()
    {
        var parsed = ParseDeclarations("x:\"foo\\\"");
        Assert.That(parsed.Diagnostics.Any(d => d.Message.Contains("Unclosed", StringComparison.OrdinalIgnoreCase)), Is.True);
    }

    [Test]
    public void EndOfFileAfterEscapedClosingParenReportsUnclosedUrl()
    {
        var parsed = ParseDeclarations("x:url(foo\\)");
        Assert.That(parsed.Diagnostics.Any(d => d.Message.Contains("Unclosed", StringComparison.OrdinalIgnoreCase)), Is.True);
    }

    [Test]
    public void UnicodeRangeDescriptorHasAContextualValueToken()
    {
        var parsed = ParseDeclarations("unicode-range:U+00A0-00FF");
        Assert.That(parsed.Diagnostics, Is.Empty);
        Assert.That(parsed.Declarations.Single().Values.OfType<CssTokenComponent>()
            .Any(x => x.Token.Kind.ToString() == "UnicodeRange"), Is.True);
    }
}
