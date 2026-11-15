using FactsPDF.CssSyntax;
using NUnit.Framework;

namespace FactsPDF.Tests;

// Compiling a declaration block collects its expanded declarations in one pooled scratch list
// per thread and returns an exact-size array, instead of a list sized to the nodes that grew by
// doubling as shorthands expanded.
[TestFixture]
[NonParallelizable]
public sealed class DeclarationScratchListTests
{
    private static readonly PdfOptions Options = new();

    private static IReadOnlyList<CssDeclarationNode> Parse(string css)
    {
        var limits = new CssSyntaxLimits(Options.MaxCssCharacters, Options.MaxCssSyntaxNodes, Options.MaxCssSyntaxDepth);
        return CssSyntaxParser.ParseDeclarations(CssSourceText.Create(css, 0, limits), limits).Declarations;
    }

    private static CssDeclaration[] Compile(IReadOnlyList<CssDeclarationNode> nodes) =>
        CssSyntaxAdapter.CompileKnownDeclarations(nodes, new CssBudget(Options, default));

    [Test]
    public void CompilingExpandedShorthandsAllocatesOnlyTheExactSizeResult()
    {
        // padding and border expand to 4 + 12 declarations.
        var nodes = Parse("padding:3pt;border:0.5pt solid #789");
        for (var i = 0; i < 20; i++) Compile(nodes);
        var least = long.MaxValue;
        for (var i = 0; i < 5; i++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            var compiled = Compile(nodes);
            least = Math.Min(least, GC.GetAllocatedBytesForCurrentThread() - before);
            Assert.That(compiled, Has.Length.EqualTo(16));
        }
        TestContext.Out.WriteLine($"compile 16 declarations: {least} bytes");
        Assert.That(least, Is.LessThan(1_300)); // main: 1,576 (list arrays of 2, 4, 8 and 16 plus the result); 1,208 here
    }

    [Test]
    public void EachCompileReturnsItsOwnArrayAndLeavesEarlierResultsIntact()
    {
        var first = Compile(Parse("padding:3pt;border:0.5pt solid #789"));
        var snapshot = first.ToArray();
        var second = Compile(Parse("color:red;margin:2pt"));
        Assert.That(second, Is.Not.SameAs(first));
        Assert.That(first, Is.EqualTo(snapshot));
        Assert.That(second.Select(d => d.Name), Is.EqualTo(new[] { "color", "margin-top", "margin-right", "margin-bottom", "margin-left" }));
    }

    [Test]
    public void AFailedCompileLeavesNoDeclarationsForTheNextOne()
    {
        // The second declaration fails after the first expanded into four.
        var error = Assert.Throws<FactsPdfException>(() => Compile(Parse("padding:3pt;color:#78g")))!;
        Assert.That(error.Code, Is.EqualTo("FPDF1202"));
        var next = Compile(Parse("color:red"));
        Assert.That(next.Select(d => d.Name), Is.EqualTo(new[] { "color" }));
    }
}
