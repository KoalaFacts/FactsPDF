using FactsPDF.CssSyntax;
using NUnit.Framework;

namespace FactsPDF.Tests;

// The CSS parser reads the tokenizer's pooled per-thread token list in place and returns it when
// the parse ends, instead of parsing a right-sized copy of every stylesheet and style attribute.
[TestFixture]
[NonParallelizable]
public sealed class CssParserTokenListTests
{
    private static readonly CssSyntaxLimits Limits = new(10_000, 1_000, 32);

    private static CssSyntaxResult Declarations(string css, CssSyntaxLimits? limits = null) =>
        CssSyntaxParser.ParseDeclarations(CssSourceText.Create(css, 0, limits ?? Limits), limits ?? Limits);

    private static CssSyntaxResult Stylesheet(string css, CssSyntaxLimits? limits = null) =>
        CssSyntaxParser.ParseStylesheet(CssSourceText.Create(css, 0, limits ?? Limits), limits ?? Limits);

    private static string Describe(CssSyntaxResult result) => string.Join("|",
        result.Rules.Select(r => r is CssQualifiedRuleNode q
            ? string.Concat(q.Prelude.Select(Describe)) + "{" + string.Join(";", q.Contents.OfType<CssDeclarationNode>().Select(Describe)) + "}"
            : r.GetType().Name)
        .Concat(result.Declarations.Select(Describe)));

    private static string Describe(CssDeclarationNode d) =>
        d.Name + ":" + string.Concat(d.Values.Select(Describe)) + (d.Important ? "!" : "");

    private static string Describe(CssSyntaxComponent c) => c switch
    {
        CssTokenComponent t => $"{t.Token.Kind}({t.Token.Raw})",
        CssFunctionComponent f => f.Name + "(" + string.Concat(f.Values.Select(Describe)) + ")",
        CssBlockComponent b => b.Opening + "[" + string.Concat(b.Values.Select(Describe)) + "]",
        _ => c.GetType().Name,
    };

    [Test]
    public void ParsingAStyleAttributeDoesNotCopyItsTokens()
    {
        // 12 tokens of about 48 bytes each: the copy alone was 600 bytes.
        const string css = "padding:3pt;border:0.5pt solid #789";
        long Parse()
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            var result = Declarations(css);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(result.Declarations, Has.Count.EqualTo(2));
            return allocated;
        }
        for (var i = 0; i < 20; i++) Parse();
        var least = long.MaxValue;
        for (var i = 0; i < 5; i++) least = Math.Min(least, Parse());
        TestContext.Out.WriteLine($"parse one style attribute: {least} bytes");
        Assert.That(least, Is.LessThan(1_700)); // main: 2,040 (the parser read a right-sized copy of the 12 tokens); 1,504 here
    }

    [Test]
    public void EachParseKeepsItsOwnTokensAfterLaterParses()
    {
        var sheet = Stylesheet("p.a > b { color: red !important; margin: 1pt 2pt } @media print { x { y: z } }");
        var inline = Declarations("border:0.5pt solid #789;font:italic 10pt/12pt serif;width:calc(1pt + 2%)");
        var sheetBefore = Describe(sheet);
        var inlineBefore = Describe(inline);
        for (var i = 0; i < 5; i++)
        {
            Stylesheet("q { color: blue } r { s: t u v w x }");
            Declarations("padding:9pt 8pt 7pt 6pt;color:#000");
        }
        Assert.That(Describe(sheet), Is.EqualTo(sheetBefore));
        Assert.That(Describe(inline), Is.EqualTo(inlineBefore));
        Assert.That(inlineBefore, Does.StartWith("border:Dimension(0.5pt)Whitespace( )Ident(solid)Whitespace( )Hash(#789)|font:"));
        Assert.That(sheetBefore, Does.Contain("color:Ident(red)!;margin:Dimension(1pt)Whitespace( )Dimension(2pt)"));
    }

    [Test]
    public void AParseThatFailsLeavesNoTokensForTheNextOne()
    {
        // Fails while tokenizing (nesting past MaxDepth) and while parsing (node count past MaxNodes).
        var tight = new CssSyntaxLimits(10_000, 6, 2);
        Assert.Throws<CssSyntaxLimitException>(() => Declarations("a:f(g(h(i)))", tight));
        Assert.That(Describe(Declarations("color:red")), Is.EqualTo("color:Ident(red)"));
        Assert.Throws<CssSyntaxLimitException>(() => Stylesheet("a{b:c d e f g h i j}", tight));
        Assert.That(Describe(Stylesheet("x{y:z}")), Is.EqualTo("Ident(x){y:Ident(z)}"));
        var cancelled = new CssSyntaxLimits(10_000, 1_000, 32, Cancellation: new CancellationToken(true));
        Assert.Throws<OperationCanceledException>(() => Declarations("color:red", cancelled));
        Assert.That(Describe(Declarations("margin:0")), Is.EqualTo("margin:Number(0)"));
    }
}
