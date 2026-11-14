using System.Text;
using FactsPDF.CssSyntax;
using NUnit.Framework;

namespace FactsPDF.Tests;

// Reading a style attribute compiles its declarations without an OfType iterator, a boxed array
// enumerator per attribute and per declaration, or a growing term list; direct text in a table
// cell is checked for whitespace without a boxed string enumerator.
[TestFixture]
[NonParallelizable]
public sealed class StyleAttributeAllocationTests
{
    private static readonly PdfOptions Options = new() { MaxElements = 100_000 };

    private static double ReadBytesPerItem(Func<int, string> html)
    {
        long Read(string source)
        {
            for (var i = 0; i < 10; i++) HtmlDocumentReader.ReadTree(source, Options, default);
            // The least of three reads, so a tiering recompile mid-read does not count.
            var least = long.MaxValue;
            for (var i = 0; i < 3; i++)
            {
                var before = GC.GetAllocatedBytesForCurrentThread();
                HtmlDocumentReader.ReadTree(source, Options, default);
                least = Math.Min(least, GC.GetAllocatedBytesForCurrentThread() - before);
            }
            return least;
        }
        // 1,000 and 2,000 items stay under the reader's 8,192-token pooled list.
        var small = Read(html(1_000));
        var large = Read(html(2_000));
        return (large - small) / 1_000.0;
    }

    private static string CellRows(int count)
    {
        var html = new StringBuilder("<table style=\"width:200pt\"><colgroup><col style=\"width:200pt\"></colgroup><tbody>");
        for (var i = 0; i < count; i++) html.Append("<tr><td>AB</td></tr>");
        return html.Append("</tbody></table>").ToString();
    }

    // Parsing and compiling one style attribute, as the stylesheet collection does for every start
    // tag with a style attribute (a whole read is too noisy to show the saving per attribute).
    private static long CompileStyleAttribute(string css, PdfOptions options)
    {
        var limits = new CssSyntaxLimits(options.MaxCssCharacters, options.MaxCssSyntaxNodes, options.MaxCssSyntaxDepth);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var declarations = CssSyntaxParser.ParseDeclarations(CssSourceText.Create(css, 0, limits), limits).Declarations;
        var compiled = CssSyntaxAdapter.CompileKnownDeclarations(declarations, new CssBudget(options, default));
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.That(compiled, Has.Length.EqualTo(16));
        return allocated;
    }

    [Test]
    public void CompilingAStyleAttributeAllocatesNoIteratorsOrGrowingTermLists()
    {
        // padding and border expand to 4 + 12 declarations.
        const string css = "padding:3pt;border:0.5pt solid #789";
        for (var i = 0; i < 20; i++) CompileStyleAttribute(css, Options);
        var least = long.MaxValue;
        for (var i = 0; i < 5; i++) least = Math.Min(least, CompileStyleAttribute(css, Options));
        TestContext.Out.WriteLine($"compile one style attribute: {least} bytes");
        Assert.That(least, Is.LessThan(4_200)); // main: 4,232 or 4,376 (two modes 144 bytes apart, depending on which tests ran before)
    }

    [Test]
    public void ReadingTableCellTextAllocatesNoStringEnumerator()
    {
        var perRow = ReadBytesPerItem(CellRows);
        TestContext.Out.WriteLine($"read per one-cell row: {perRow:F1} bytes");
        Assert.That(perRow, Is.LessThan(665)); // 680.5 on main
    }

    [TestCase("", true)]
    [TestCase(" \t\r\n\f", true)]
    [TestCase(" a ", false)]
    [TestCase("\u00a0", false)]
    [TestCase("\v", false)]
    public void AllSpaceMatchesTheHtmlWhitespaceSet(string text, bool expected)
    {
        Assert.That(HtmlTokens.AllSpace(text), Is.EqualTo(expected));
        Assert.That(HtmlTokens.AllSpace(text), Is.EqualTo(text.All(HtmlTokens.Space)));
    }

    [Test]
    public void StyleAttributeDeclarationsKeepTheirOrderAndRejectAtRules()
    {
        var limits = new CssSyntaxLimits(1_000, 1_000, 32);
        var result = CssSyntaxParser.ParseDeclarations(CssSourceText.Create("color:red;@media print{};margin:0", 0, limits), limits);
        Assert.That(result.Declarations.Select(d => d.Name), Is.EqualTo(new[] { "color", "margin" }));
        Assert.That(result.Diagnostics.Select(d => d.Message), Has.Some.Contains("at-rule"));
        var empty = CssSyntaxParser.ParseDeclarations(CssSourceText.Create(" ; ", 0, limits), limits);
        Assert.That(empty.Declarations, Is.Empty);
    }
}
