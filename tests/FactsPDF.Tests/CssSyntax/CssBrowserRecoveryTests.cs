using FactsPDF.CssSyntax;
using NUnit.Framework;

namespace FactsPDF.Tests.CssSyntax;

/// <summary>
/// Regression cases isolated by the independent Chrome CSSOM differential
/// harness (run 38019950271, Google Chrome 154.0.8037.97).
/// A malformed declaration must recover at the next top-level semicolon.
/// </summary>
[TestFixture]
public sealed class CssBrowserRecoveryTests
{
    [TestCase("!invalid; color:red")]
    [TestCase("*color:red; color:red")]
    [TestCase(":bad; color:red")]
    [TestCase("/bad; color:red")]
    [TestCase("broken(); color:red")]
    public void BadInlineDeclarationDoesNotHideLaterValidColor(string css)
    {
        var limits = new CssSyntaxLimits();
        var syntax = CssSyntaxParser.ParseDeclarations(CssSourceText.Create(css, 0, limits), limits);
        Assert.That(syntax.Declarations.Select(d => d.Name), Is.EqualTo(new[] { "color" }));
        Assert.That(syntax.Diagnostics, Is.Not.Empty);
        Assert.That(syntax.Diagnostics[0].Span.Start, Is.Zero);
    }

    [Test]
    public void BadNestedFunctionDeclarationDoesNotSwallowLaterSemicolon()
    {
        var limits = new CssSyntaxLimits();
        const string css = "oops(1, var(--x)); color:blue; font-size:12pt";
        var syntax = CssSyntaxParser.ParseDeclarations(CssSourceText.Create(css, 9, limits), limits);
        Assert.That(syntax.Declarations.Select(d => d.Name), Is.EqualTo(new[] { "color", "font-size" }));
        Assert.That(syntax.Diagnostics, Is.Not.Empty);
        Assert.That(syntax.Diagnostics[0].Span.Start, Is.EqualTo(9));
    }
}
