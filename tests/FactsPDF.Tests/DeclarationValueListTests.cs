using FactsPDF.CssSyntax;
using NUnit.Framework;

namespace FactsPDF.Tests;

// The CSS parser collects each declaration's component values in one pooled list per thread and
// copies them into an exact-size array, instead of a new list per declaration that grew by doubling.
[TestFixture]
[NonParallelizable]
public sealed class DeclarationValueListTests
{
    private static readonly CssSyntaxLimits Limits = new(10_000, 1_000, 32);

    private static CssSyntaxResult Declarations(string css, CssSyntaxLimits? limits = null) =>
        CssSyntaxParser.ParseDeclarations(CssSourceText.Create(css, 0, limits ?? Limits), limits ?? Limits);

    private static string Values(CssDeclarationNode d) => string.Join(",", d.Values.Select(v => v switch
    {
        CssTokenComponent t => t.Token.Raw,
        CssFunctionComponent f => f.Name + "()",
        CssBlockComponent b => b.Opening.ToString(),
        _ => v.GetType().Name,
    })) + (d.Important ? "!" : "");

    [Test]
    public void ParsingADeclarationAllocatesOnlyItsExactSizeValueArray()
    {
        // `border` has five values (three terms and two spaces): the list grew 4 → 8 before its copy.
        const string css = "border:0.5pt solid #789";
        long Parse()
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            var result = Declarations(css);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(result.Declarations[0].Values, Has.Count.EqualTo(5));
            return allocated;
        }
        for (var i = 0; i < 20; i++) Parse();
        var least = long.MaxValue;
        for (var i = 0; i < 5; i++) least = Math.Min(least, Parse());
        TestContext.Out.WriteLine($"parse one five-value declaration: {least} bytes");
        Assert.That(least, Is.LessThan(1_070)); // before Z4h: 1,160 (a list object and arrays of 4 and 8 before the copy); 984 here
    }

    [Test]
    public void EachDeclarationKeepsItsOwnValues()
    {
        var result = Declarations("margin:1pt 2pt 3pt 4pt !important;color:red;width:calc(1pt + 2%);x:;unicode-range:U+0-7F,U+100");
        var first = result.Declarations.Select(Values).ToArray();
        Declarations("padding:9pt 8pt 7pt 6pt 5pt 4pt 3pt;color:#000");
        Assert.That(result.Declarations.Select(Values), Is.EqualTo(first));
        Assert.That(first, Is.EqualTo(new[] { "1pt, ,2pt, ,3pt, ,4pt!", "red", "calc()", "", "U+0-7F,,,U+100" }));
        Assert.That(result.Declarations[3].Values, Is.Empty);
    }

    [Test]
    public void ARejectedOrFailedDeclarationLeavesNoValuesForTheNextOne()
    {
        // A brace block that is not the whole value is rejected after its values were collected.
        var rejected = Declarations("a:b {c};color:red");
        Assert.That(rejected.Declarations.Select(Values), Is.EqualTo(new[] { "red" }));
        // Fails on the node budget in the middle of a value.
        Assert.Throws<CssSyntaxLimitException>(() => Declarations("a:b c d e f g h", new CssSyntaxLimits(10_000, 5, 32)));
        Assert.That(Declarations("color:blue").Declarations.Select(Values), Is.EqualTo(new[] { "blue" }));
    }
}
