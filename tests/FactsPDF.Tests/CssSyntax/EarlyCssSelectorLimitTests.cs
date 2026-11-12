using System.Text;
using FactsPDF.CssSyntax;
using NUnit.Framework;

namespace FactsPDF.Tests.CssSyntax;

// R1c4: every top-level qualified rule compiles at least one selector, and the selector budget is
// counted before anything else about the rule is checked. The tokenizer counts those rules against
// the selectors still left (MaxCssSelectors minus the earlier style elements' selectors), so a rule
// flood stops at the first rule past the budget instead of after the whole style element is
// tokenized and parsed. The count never exceeds the compiled selectors, so every document keeps the
// exact selector budget it needed before.
[TestFixture]
[NonParallelizable]
public sealed class EarlyCssSelectorLimitTests
{
    private static string Repeat(string unit, int count) => new StringBuilder(unit.Length * count).Insert(0, unit, count).ToString();

    private static PdfWriter Writer(int maxSelectors) => Pdf.Write().WithLimits(l => l.MaxInputCharacters(8_000_000)
        .MaxCssCharacters(8_000_000).MaxCssSelectors(maxSelectors).MaxCssDeclarations(400_000)
        .MaxCssSyntaxNodes(8_000_000));

    [Test]
    public void RuleFloodStopsAtTheFirstRulePastTheBudget()
    {
        var html = "<style>" + Repeat("p{color:red}", 100_000) + "</style><p>x</p>";
        try { Writer(100).ToBytes(html); } catch (FactsPdfException) { } // warm up
        var before = GC.GetAllocatedBytesForCurrentThread();
        var error = Assert.Throws<FactsPdfException>(() => Writer(100).ToBytes(html))!;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        TestContext.Out.WriteLine($"100,000 rules over MaxCssSelectors 100: {allocated / 1024} KiB");
        Assert.That(error.Code, Is.EqualTo("FPDF1205"));
        Assert.That(error.Message, Does.Contain("CSS selector budget exceeded."));
        Assert.That(error.SourceOffset, Is.EqualTo("<style>".Length + 100 * "p{color:red}".Length)); // the 101st rule
        // Before R1c4 every token of the style element was created and parsed first.
        Assert.That(allocated, Is.LessThan(4_000_000));
    }

    private static IEnumerable<TestCaseData> Budgets()
    {
        // html, the selector budget it needs, and the source offset of the selector that fails one
        // below it on main 3c065a6 (the budget's own check, before R1c4).
        yield return new TestCaseData("<style>a{color:red}b{color:red}</style><style>p{color:red}</style><p>x</p>", 3, 46)
            .SetName("{m}(two style elements)");
        yield return new TestCaseData("<style>a,b{color:red}p{color:red}</style><p>x</p>", 3, 21)
            .SetName("{m}(selector list)");
        yield return new TestCaseData("<style>@media print{a{color:red}b{color:red}}p{color:red}</style><p>x</p>", 3, 45)
            .SetName("{m}(media block)");
        yield return new TestCaseData("<style>p{color:red}h1{color:red}</style><h1 style=\"color:red\">t</h1><p style=\"color:red\">x</p>", 2, 19)
            .SetName("{m}(style attribute)");
        yield return new TestCaseData("<style><!-- p{color:red} --> h1{color:red}</style><h1>t</h1><p>x</p>", 2, 29)
            .SetName("{m}(cdo and cdc)");
        yield return new TestCaseData("<style>p[title=\"{\"] , p[title=\"}\"]{color:red} p{color:red}</style><p>x</p>", 3, 46)
            .SetName("{m}(braces in attribute selector strings)");
        yield return new TestCaseData("<style>\n  p { color: red }\n  h1 { color: red }\n</style><h1>t</h1><p>x</p>", 2, 29)
            .SetName("{m}(whitespace between rules)");
        yield return new TestCaseData("<style>p{color:red}#x{color:red}</style><p id=\"x\">x</p>", 2, 19)
            .SetName("{m}(id selector rule)");
        yield return new TestCaseData("<style>#x{color:red} #y{color:red}</style><p id=\"x\">x</p>", 2, 21)
            .SetName("{m}(id selector rules after whitespace)");
    }

    [TestCaseSource(nameof(Budgets))]
    public void RendersAtItsSelectorBudgetAndFailsOneBelow(string html, int selectors, int failingOffset)
    {
        Assert.DoesNotThrow(() => Writer(selectors).ToBytes(html));
        var error = Assert.Throws<FactsPdfException>(() => Writer(selectors - 1).ToBytes(html))!;
        Assert.That(error.Code, Is.EqualTo("FPDF1205"));
        Assert.That(error.Message, Does.Contain("CSS selector budget exceeded."));
        Assert.That(error.SourceOffset, Is.EqualTo(failingOffset));
    }

    private static CssSyntaxLimits Limits(int rules) => new(MaxCharacters: 1_000_000, MaxNodes: 1_000_000, MaxDepth: 64,
        MaxQualifiedRules: rules);

    private static void Parse(string css, bool declarations, int rules)
    {
        var limits = Limits(rules);
        var source = CssSourceText.Create(css, 0, limits);
        _ = declarations ? CssSyntaxParser.ParseDeclarations(source, limits) : CssSyntaxParser.ParseStylesheet(source, limits);
    }

    private static IEnumerable<TestCaseData> RuleCounts()
    {
        // css, declarations only, the top-level qualified rules the parser returns.
        yield return new TestCaseData("p{color:red}", false, 1).SetName("{m}(one rule)");
        yield return new TestCaseData("@media print{p{q:r}s{t:u}}@import url(x.css);a{b:c}", false, 1).SetName("{m}(at-rules)");
        yield return new TestCaseData("@x a;b{c:d}", false, 1).SetName("{m}(at-rule ended by a semicolon)");
        yield return new TestCaseData("a;b{c:d}e{f:g}", false, 2).SetName("{m}(semicolon in a selector prelude)");
        yield return new TestCaseData("a{b:{c}d}e:f({}){g:h}", false, 2).SetName("{m}(braces inside blocks and functions)");
        yield return new TestCaseData("{a:b}c{d:e}", false, 2).SetName("{m}(empty prelude)");
        yield return new TestCaseData("<!--a{b:c}-->d{e:f}", false, 2).SetName("{m}(cdo and cdc)");
        yield return new TestCaseData("#a{b:c}#d{e:f}", false, 2).SetName("{m}(id selectors)");
    }

    [TestCaseSource(nameof(RuleCounts))]
    public void CountsExactlyTheTopLevelQualifiedRules(string css, bool declarations, int rules)
    {
        var parsed = CssSyntaxParser.ParseStylesheet(CssSourceText.Create(css, 0, Limits(int.MaxValue)), Limits(int.MaxValue));
        Assert.That(parsed.Rules.OfType<CssQualifiedRuleNode>().Count(), Is.EqualTo(rules), "the parser's own count");
        Assert.DoesNotThrow(() => Parse(css, declarations, rules));
        var error = Assert.Throws<CssSyntaxLimitException>(() => Parse(css, declarations, rules - 1))!;
        Assert.That(error.Message, Is.EqualTo("CSS selector budget exceeded."));
    }

    [Test]
    public void StyleAttributesAreNotCounted()
        => Assert.DoesNotThrow(() => Writer(1).ToBytes("<style>p{color:red}</style>"
            + Repeat("<p style=\"color:blue;margin:1pt\">x</p>", 20)));
}
