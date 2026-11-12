using System.Text;
using FactsPDF.CssSyntax;
using NUnit.Framework;

namespace FactsPDF.Tests.CssSyntax;

// R1c3: MaxCssSyntaxNodes is checked while a stylesheet is tokenized against a lower bound of the
// parser's node count (the tokens the parser counts whenever it takes them), so a node flood stops
// soon after the limit instead of after the whole style element is tokenized. The bound never
// exceeds the parser's count, so every stylesheet keeps the exact node budget it needed before.
[TestFixture]
[NonParallelizable]
public sealed class EarlyCssNodeLimitTests
{
    private static string Repeat(string unit, int count) => new StringBuilder(unit.Length * count).Insert(0, unit, count).ToString();

    private static PdfWriter Writer(int maxNodes) => Pdf.Write().WithLimits(l => l.MaxInputCharacters(8_000_000)
        .MaxCssCharacters(8_000_000).MaxCssSelectors(400_000).MaxCssDeclarations(400_000).MaxCssSyntaxNodes(maxNodes));

    [Test]
    public void NodeFloodStopsSoonAfterTheLimit()
    {
        var html = "<style>" + Repeat("p{color:red}", 100_000) + "</style><p>x</p>";
        try { Writer(1_000).ToBytes(html); } catch (FactsPdfException) { } // warm up
        var before = GC.GetAllocatedBytesForCurrentThread();
        var error = Assert.Throws<FactsPdfException>(() => Writer(1_000).ToBytes(html))!;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        TestContext.Out.WriteLine($"100,000 rules over MaxCssSyntaxNodes 1,000: {allocated / 1024} KiB");
        Assert.That(error.Code, Is.EqualTo("FPDF1205"));
        Assert.That(error.Message, Does.Contain("MaxNodes"));
        // Before R1c3 every token of the style element was created first (114,108 KiB on main 65c99db).
        Assert.That(allocated, Is.LessThan(8_000_000));
    }

    private static CssSyntaxLimits Limits(int nodes) => new(MaxCharacters: 1_000_000, MaxNodes: nodes, MaxDepth: 64);

    private static void Parse(string css, bool declarations, int nodes)
    {
        var limits = Limits(nodes);
        var source = CssSourceText.Create(css, 0, limits);
        _ = declarations ? CssSyntaxParser.ParseDeclarations(source, limits) : CssSyntaxParser.ParseStylesheet(source, limits);
    }

    private static IEnumerable<TestCaseData> Budgets()
    {
        // css, declarations only, the node budget the parser needed on main 65c99db.
        yield return new TestCaseData("p{color:red}", false, 4).SetName("{m}(one rule)");
        yield return new TestCaseData("a,b c{x:1px 2%;y:#fff !important}", false, 15).SetName("{m}(selector list and important)");
        yield return new TestCaseData("a{b c d;e:f}", false, 7).SetName("{m}(declaration without a colon)");
        yield return new TestCaseData("a{b{c:d}e:f(g,h)}", false, 11).SetName("{m}(nested rule)");
        yield return new TestCaseData("@media print{p{q:r}}@import url(x.css);", false, 10).SetName("{m}(at-rules)");
        yield return new TestCaseData("<!--a{b:c}-->}d{e:f}", false, 9).SetName("{m}(cdo, cdc and a stray closer)");
        yield return new TestCaseData("a{--x:{b c}d;e:{f}g}", false, 11).SetName("{m}(brace blocks in values)");
        yield return new TestCaseData("@font-face{unicode-range:U+0-7F,u+4??}", false, 10).SetName("{m}(unicode range)");
        yield return new TestCaseData("color:red;margin:1px 2px", true, 6).SetName("{m}(style attribute)");
        yield return new TestCaseData("a b c;d:e;}f g;:h", true, 8).SetName("{m}(style attribute recovery)");
        yield return new TestCaseData("color:'x' \"y;width:url(z)", true, 4).SetName("{m}(strings and urls)");
        yield return new TestCaseData("@x y{z:w}color:[a](b)", true, 10).SetName("{m}(at-rule and blocks in a style attribute)");
    }

    [TestCaseSource(nameof(Budgets))]
    public void ParsesAtItsNodeBudgetAndFailsOneBelow(string css, bool declarations, int nodes)
    {
        Assert.DoesNotThrow(() => Parse(css, declarations, nodes));
        var error = Assert.Throws<CssSyntaxLimitException>(() => Parse(css, declarations, nodes - 1))!;
        Assert.That(error.Message, Is.EqualTo("CSS syntax node count exceeds MaxNodes."));
    }
}
