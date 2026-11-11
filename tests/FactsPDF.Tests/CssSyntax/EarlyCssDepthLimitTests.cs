using System.Text;
using FactsPDF.CssSyntax;
using NUnit.Framework;

namespace FactsPDF.Tests.CssSyntax;

// R1c2: MaxCssSyntaxDepth is enforced while a stylesheet is tokenized, so a nesting flood stops at
// the first opener past the limit instead of after the whole style element is tokenized. The
// tokenizer tracks the closers the parser waits for (only the matching closer leaves a block), so
// it fails exactly the documents the parser fails, at the same opener.
[TestFixture]
[NonParallelizable]
public sealed class EarlyCssDepthLimitTests
{
    private static string Repeat(string unit, int count) => new StringBuilder(unit.Length * count).Insert(0, unit, count).ToString();

    private static PdfWriter Writer(int maxDepth) => Pdf.Write().WithLimits(l => l.MaxInputCharacters(8_000_000)
        .MaxCssCharacters(8_000_000).MaxCssSyntaxNodes(8_000_000).MaxCssSyntaxDepth(maxDepth));

    [Test]
    public void NestingFloodStopsAtTheFirstOpenerPastTheLimit()
    {
        const string prefix = "<style>p{color:";
        var html = prefix + Repeat("f(", 100_000) + "}</style><p>x</p>";
        try { Writer(16).ToBytes(html); } catch (FactsPdfException) { } // warm up
        var before = GC.GetAllocatedBytesForCurrentThread();
        var error = Assert.Throws<FactsPdfException>(() => Writer(16).ToBytes(html))!;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        TestContext.Out.WriteLine($"100,000 nested functions over MaxCssSyntaxDepth 16: {allocated / 1024} KiB");
        Assert.That(error.Code, Is.EqualTo("FPDF1205"));
        Assert.That(error.Message, Does.Contain("MaxDepth"));
        // The rule's block is depth 1, so the 16th function is the 17th opener.
        Assert.That(error.SourceOffset, Is.EqualTo(prefix.Length + 15 * "f(".Length));
        // Before R1c2 every token of the style element was created first (17,679 KiB on main 5b2b06a).
        Assert.That(allocated, Is.LessThan(2_000_000));
    }

    private static CssSyntaxLimits Limits(int depth) => new(MaxCharacters: 1_000_000, MaxNodes: 1_000_000, MaxDepth: depth);

    private static void Parse(string css, bool declarations, int depth)
    {
        var limits = Limits(depth);
        var source = CssSourceText.Create(css, 0, limits);
        _ = declarations ? CssSyntaxParser.ParseDeclarations(source, limits) : CssSyntaxParser.ParseStylesheet(source, limits);
    }

    private static IEnumerable<TestCaseData> Nestings()
    {
        // css, declarations only, deepest nesting, offset of the opener that reaches it.
        yield return new TestCaseData("p{color:rgb(1,2,3)}", false, 2, 8).SetName("{m}(function in a rule)");
        yield return new TestCaseData("@media print{p{color:f(g())}}", false, 4, 23).SetName("{m}(at-rule, rule, two functions)");
        yield return new TestCaseData("a{b:f(]]]g()}}", false, 3, 9).SetName("{m}(stray square closers stay inside a paren)");
        yield return new TestCaseData("a{b:f(}x)c(d)}", false, 2, 4).SetName("{m}(a brace closer does not close a paren)");
        yield return new TestCaseData("a{b:[(])}", false, 3, 5).SetName("{m}(unclosed blocks at the end)");
        yield return new TestCaseData("}}a{b:(c)}", false, 2, 6).SetName("{m}(stray top-level closers)");
        yield return new TestCaseData("a{b{c:d}e{f:g(h)}}", false, 3, 12).SetName("{m}(nested rules)");
        yield return new TestCaseData("color:f(g(h()))", true, 3, 10).SetName("{m}(style attribute functions)");
        yield return new TestCaseData("}color:f([x]);)width:g()", true, 2, 9).SetName("{m}(style attribute stray closers)");
        yield return new TestCaseData("color:red;@x{y{z:w}}", true, 2, 14).SetName("{m}(at-rule in a style attribute)");
    }

    [TestCaseSource(nameof(Nestings))]
    public void ParsesAtItsDepthAndFailsAtTheSameOpenerOneBelow(string css, bool declarations, int depth, int offset)
    {
        Assert.DoesNotThrow(() => Parse(css, declarations, depth));
        var error = Assert.Throws<CssSyntaxLimitException>(() => Parse(css, declarations, depth - 1))!;
        Assert.That(error.Message, Is.EqualTo("CSS syntax nesting exceeds MaxDepth."));
        Assert.That(error.Span.Start, Is.EqualTo(offset));
    }

    [Test]
    public void ATokenizeAfterADepthFailureStartsAtDepthZero()
    {
        // The closer list is pooled per thread; a failed tokenize must not leave its openers behind.
        Assert.Throws<CssSyntaxLimitException>(() => Parse("a{b:" + Repeat("f(", 10), false, 4));
        Assert.DoesNotThrow(() => Parse("a{b:f(g(h()))}", false, 4));
    }
}
