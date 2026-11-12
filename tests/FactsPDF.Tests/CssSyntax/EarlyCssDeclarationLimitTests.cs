using System.Text;
using FactsPDF.CssSyntax;
using NUnit.Framework;

namespace FactsPDF.Tests.CssSyntax;

// R1c5: every declaration of a top-level qualified rule (a non-custom name, a colon and a value,
// ended by `;` or the block's `}`) compiles at least one declaration counted against the
// declaration budget, or fails with its own error. The tokenizer counts those declarations against
// the declarations still left (MaxCssDeclarations minus what earlier style elements and attributes
// used), so a declaration flood stops at the first declaration past the budget instead of after the
// whole style element is tokenized and parsed. The count never exceeds the compiled declarations,
// so every document keeps the exact declaration budget it needed before.
[TestFixture]
[NonParallelizable]
public sealed class EarlyCssDeclarationLimitTests
{
    private static string Repeat(string unit, int count) => new StringBuilder(unit.Length * count).Insert(0, unit, count).ToString();

    private static PdfWriter Writer(int maxDeclarations) => Pdf.Write().WithLimits(l => l.MaxInputCharacters(8_000_000)
        .MaxCssCharacters(8_000_000).MaxCssSelectors(400_000).MaxCssDeclarations(maxDeclarations)
        .MaxCssSyntaxNodes(8_000_000));

    [Test]
    public void DeclarationFloodStopsAtTheFirstDeclarationPastTheBudget()
    {
        var html = "<style>p{" + Repeat("color:red;", 100_000) + "}</style><p>x</p>";
        try { Writer(100).ToBytes(html); } catch (FactsPdfException) { } // warm up
        var before = GC.GetAllocatedBytesForCurrentThread();
        var error = Assert.Throws<FactsPdfException>(() => Writer(100).ToBytes(html))!;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        TestContext.Out.WriteLine($"100,000 declarations over MaxCssDeclarations 100: {allocated / 1024} KiB");
        Assert.That(error.Code, Is.EqualTo("FPDF1205"));
        Assert.That(error.Message, Does.Contain("CSS declaration budget exceeded."));
        // The value of the 101st declaration; for a single-value property the compiled check reports it
        // there too (a shorthand can fail earlier, at the declaration where its emitted count crossed).
        Assert.That(error.SourceOffset, Is.EqualTo("<style>p{".Length + 100 * "color:red;".Length + "color:".Length));
        // Before R1c5 every token of the style element was created and parsed first.
        Assert.That(allocated, Is.LessThan(4_000_000));
    }

    private static IEnumerable<TestCaseData> Budgets()
    {
        // html, the declaration budget it needs, and the source offset of the declaration that
        // fails one below it with the compiled budget check (main before R1c5).
        const string two = "<style>a{color:red}b{color:red}</style><style>p{color:red}</style><p>x</p>";
        yield return new TestCaseData(two, 3, two.LastIndexOf("red", StringComparison.Ordinal))
            .SetName("{m}(two style elements)");
        const string shorthand = "<style>p{margin:1pt;color:red}</style><p>x</p>";
        yield return new TestCaseData(shorthand, 5, shorthand.IndexOf("red", StringComparison.Ordinal))
            .SetName("{m}(shorthand emits four)");
        const string spaced = "<style>\n p { color : red !important ; margin-top : 1pt }\n</style><p>x</p>";
        yield return new TestCaseData(spaced, 2, spaced.IndexOf("1pt", StringComparison.Ordinal))
            .SetName("{m}(whitespace and important)");
        const string media = "<style>@media print{p{color:red}}p{margin-top:1pt}</style><p>x</p>";
        yield return new TestCaseData(media, 2, media.IndexOf("1pt", StringComparison.Ordinal))
            .SetName("{m}(media block)");
        const string function = "<style>p{color:rgb(1,2,3);margin-top:1pt}</style><p>x</p>";
        yield return new TestCaseData(function, 2, function.IndexOf("1pt", StringComparison.Ordinal))
            .SetName("{m}(function value)");
        const string list = "<style>a,p{color:red}#x{margin-top:1pt}</style><p id=\"x\">x</p>";
        yield return new TestCaseData(list, 2, list.IndexOf("1pt", StringComparison.Ordinal))
            .SetName("{m}(selector list and id rule)");
    }

    [TestCaseSource(nameof(Budgets))]
    public void RendersAtItsDeclarationBudgetAndFailsOneBelow(string html, int declarations, int failingOffset)
    {
        Assert.DoesNotThrow(() => Writer(declarations).ToBytes(html));
        var error = Assert.Throws<FactsPdfException>(() => Writer(declarations - 1).ToBytes(html))!;
        Assert.That(error.Code, Is.EqualTo("FPDF1205"));
        Assert.That(error.Message, Does.Contain("CSS declaration budget exceeded."));
        Assert.That(error.SourceOffset, Is.EqualTo(failingOffset));
    }

    [Test]
    public void StyleAttributesCountAgainstTheStylesheetsThatFollow()
    {
        // Attributes are not counted early themselves, but the budget they used is not left to a later style element.
        const string html = "<p style=\"color:blue;margin-top:1pt\">x</p><style>p{color:red}</style>";
        Assert.DoesNotThrow(() => Writer(3).ToBytes(html));
        var error = Assert.Throws<FactsPdfException>(() => Writer(2).ToBytes(html))!;
        Assert.That(error.Code, Is.EqualTo("FPDF1205"));
    }

    private static CssSyntaxLimits Limits(int declarations) => new(MaxCharacters: 1_000_000, MaxNodes: 1_000_000, MaxDepth: 64,
        MaxDeclarations: declarations);

    private static void Parse(string css, int declarations)
    {
        var limits = Limits(declarations);
        _ = CssSyntaxParser.ParseStylesheet(CssSourceText.Create(css, 0, limits), limits);
    }

    private static IEnumerable<TestCaseData> DeclarationCounts()
    {
        // css, the declarations the tokenizer counts, and whether that is every declaration the
        // parser returns in top-level qualified rules (otherwise the count is lower).
        yield return new TestCaseData("p{a:b;c:d}", 2, true).SetName("{m}(two declarations)");
        yield return new TestCaseData("p{a:b;;c:d;}q{e:f}", 3, true).SetName("{m}(empty items and rules)");
        yield return new TestCaseData("p{a:f(;)x;b:[}]}", 2, true).SetName("{m}(terminators inside functions and blocks)");
        yield return new TestCaseData("@media print{p{a:b}}@x c;q{d:e}", 1, true).SetName("{m}(at-rules)");
        yield return new TestCaseData("p{--x:1;a:b}", 1, false).SetName("{m}(custom property)");
        yield return new TestCaseData("p{a:{b}}q{c:d}", 1, false).SetName("{m}(brace block value)");
        yield return new TestCaseData("p{a b{c:d}e:f}", 1, true).SetName("{m}(nested rule)");
        yield return new TestCaseData("p{a;b:c}", 1, true).SetName("{m}(bad declaration)");
        yield return new TestCaseData("p{#a:b;c:d}", 1, true).SetName("{m}(hash item)");
    }

    [TestCaseSource(nameof(DeclarationCounts))]
    public void CountsAtMostTheParsersDeclarations(string css, int counted, bool exact)
    {
        var parsed = CssSyntaxParser.ParseStylesheet(CssSourceText.Create(css, 0, Limits(int.MaxValue)), Limits(int.MaxValue));
        var declarations = parsed.Rules.OfType<CssQualifiedRuleNode>().Sum(r => r.Contents.OfType<CssDeclarationNode>().Count());
        if (exact) Assert.That(declarations, Is.EqualTo(counted), "the parser's own count");
        else Assert.That(declarations, Is.GreaterThan(counted), "the parser's own count");
        Assert.DoesNotThrow(() => Parse(css, counted));
        var error = Assert.Throws<CssSyntaxLimitException>(() => Parse(css, counted - 1))!;
        Assert.That(error.Message, Is.EqualTo("CSS declaration budget exceeded."));
    }
}
