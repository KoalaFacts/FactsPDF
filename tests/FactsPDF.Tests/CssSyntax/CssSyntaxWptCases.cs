using FactsPDF.CssSyntax;
using NUnit.Framework;

namespace FactsPDF.Tests.CssSyntax;

/// <summary>
/// Independent, selected observable WPT css/css-syntax checks.
/// Reference: web-platform-tests/wpt@521d168d63dbd206ea7fbe0b74ddd5760e6e668e.
/// These assertions are written for FactsPDF, not copied WPT source or W3C certification.
/// </summary>
[TestFixture]
public sealed class CssSyntaxWptCases
{
    public static IEnumerable<TestCaseData> Cases()
    {
        foreach (var (feature, name, source) in new[]
        {
            ("Preprocessing", "InputPreprocessing", "css/css-syntax/input-preprocessing.html"),
            ("Tokenizer", "DecimalPoints", "css/css-syntax/decimal-points-in-numbers.html"),
            ("Tokenizer", "EscapedEof", "css/css-syntax/escaped-eof.html"),
            ("Tokenizer", "NonAsciiCodepoints", "css/css-syntax/non-ascii-codepoints.html"),
            ("Tokenizer", "UrlWhitespace", "css/css-syntax/url-whitespace-consumption.html"),
            ("Tokenizer", "CommentBoundaries", "css/css-syntax/whitespace.html"),
            ("Parser", "AtRuleInDeclarationList", "css/css-syntax/at-rule-in-declaration-list.html"),
            ("Parser", "TrailingBraces", "css/css-syntax/trailing-braces.html"),
            ("Parser", "MissingSemicolon", "css/css-syntax/missing-semicolon.html"),
            ("Parser", "VarWithBlocks", "css/css-syntax/var-with-blocks.html"),
            ("Parser", "UnclosedConstructs", "css/css-syntax/unclosed-constructs.html"),
            ("Parser", "CustomPropertyAmbiguity", "css/css-syntax/custom-property-rule-ambiguity.html")
        })
            yield return new TestCaseData(name, source)
                .SetName($"CssSyntaxWpt__{feature}__{name}")
                .SetCategory("CssSyntaxConformance");
    }

    private static CssSyntaxLimits Limits => new();

    private static IReadOnlyList<CssSyntaxToken> Tokens(string css)
    {
        var limits = Limits;
        return CssSyntaxTokenizer.Tokenize(CssSourceText.Create(css, 0, limits), limits)
            .Where(t => t.Kind is not (CssSyntaxTokenKind.Whitespace or CssSyntaxTokenKind.Eof)).ToArray();
    }

    private static CssSyntaxResult Declarations(string css)
    {
        var limits = Limits;
        return CssSyntaxParser.ParseDeclarations(CssSourceText.Create(css, 0, limits), limits);
    }

    private static CssSyntaxResult Stylesheet(string css)
    {
        var limits = Limits;
        return CssSyntaxParser.ParseStylesheet(CssSourceText.Create(css, 0, limits), limits);
    }

    [TestCaseSource(nameof(Cases))]
    public void MatchesSelectedStandardsBehaviors(string name, string upstreamFixture)
    {
        Assert.That(upstreamFixture, Does.StartWith("css/css-syntax/"));
        switch (name)
        {
            case "InputPreprocessing":
                Assert.That(CssSourceText.Create("a\r\nb\fc\0", 0, Limits).Normalized,
                    Is.EqualTo("a\nb\nc\uFFFD"));
                break;
            case "DecimalPoints":
                var nums = Tokens(".5 2. +2.0");
                Assert.That(nums.Select(t => t.Kind), Is.EqualTo(new[]
                {
                    CssSyntaxTokenKind.Number, CssSyntaxTokenKind.Number,
                    CssSyntaxTokenKind.Delim, CssSyntaxTokenKind.Number
                }));
                Assert.That(nums[0].Value, Is.EqualTo(".5"));
                break;
            case "EscapedEof":
                Assert.That(Tokens("\\")[0].Value, Is.EqualTo("\uFFFD"));
                break;
            case "NonAsciiCodepoints":
                Assert.That(Tokens("café")[0].Value, Is.EqualTo("café"));
                break;
            case "UrlWhitespace":
                Assert.That(Tokens("url(a b)")[0].Kind, Is.EqualTo(CssSyntaxTokenKind.BadUrl));
                break;
            case "CommentBoundaries":
                Assert.That(Tokens("p/**/span").Select(t => t.Value), Is.EqualTo(new[] { "p", "span" }));
                break;
            case "AtRuleInDeclarationList":
                var inline = Declarations("@media print{p{color:red}} color:blue");
                Assert.That(inline.Diagnostics, Is.Not.Empty);
                Assert.That(inline.Declarations.Any(d => d.Name == "color"), Is.True);
                break;
            case "TrailingBraces":
                var rules = Stylesheet("p{color:red}}q{color:blue}");
                Assert.That(rules.Rules, Has.Count.EqualTo(2));
                Assert.That(rules.Diagnostics, Is.Not.Empty);
                break;
            case "MissingSemicolon":
                var declarations = Declarations("color:red background:blue");
                Assert.That(declarations.Diagnostics, Is.Empty);
                Assert.That(declarations.Declarations, Has.Count.EqualTo(1));
                break;
            case "VarWithBlocks":
                var varNode = Declarations("x:var(--a, {b:c})")
                    .Declarations.Single().Values.OfType<CssFunctionComponent>().Single();
                Assert.That(varNode.Values.OfType<CssBlockComponent>().Count(), Is.EqualTo(1));
                break;
            case "UnclosedConstructs":
                var unclosed = Stylesheet("p{color:red");
                Assert.That(unclosed.Rules, Has.Count.EqualTo(1));
                Assert.That(unclosed.Diagnostics, Is.Not.Empty);
                break;
            case "CustomPropertyAmbiguity":
                var custom = Declarations("--Theme:var(--a, red)");
                Assert.That(custom.Declarations.Single().Name, Is.EqualTo("--Theme"));
                Assert.That(custom.Diagnostics, Is.Empty);
                break;
            default: Assert.Fail("Unregistered conformance fixture: " + name); break;
        }
    }
}
