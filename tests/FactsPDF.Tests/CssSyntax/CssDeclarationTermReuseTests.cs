using FactsPDF.CssSyntax;
using NUnit.Framework;

namespace FactsPDF.Tests.CssSyntax;

// Compiling declarations reuses a token's source text for a dimension, percentage or hash term
// when it has no escapes, and copies the values only when a colour function is lowered. Escaped
// terms and lowered colours must still compile to the same values as before.
[TestFixture]
public sealed class CssDeclarationTermReuseTests
{
    private static CssDeclaration[] Compile(string css)
    {
        var limits = new CssSyntaxLimits();
        var result = CssSyntaxParser.ParseDeclarations(CssSourceText.Create(css, 0, limits), limits);
        return CssSyntaxAdapter.CompileKnownDeclarations(result.Declarations, new CssBudget(new PdfOptions(), CancellationToken.None));
    }

    private static string Value(CssDeclaration[] declarations, CssProperty property) =>
        declarations.Single(d => d.Property == property).Value;

    [Test]
    public void EscapeFreeAndEscapedTermsCompileToTheSameValues()
    {
        var plain = Compile("padding-top:3PT;width:50%;color:#AbC");
        var escaped = Compile("padding-top:3\\50 T;width:50%;color:#A\\62 C");
        foreach (var declarations in new[] { plain, escaped })
        {
            Assert.That(Value(declarations, CssProperty.PaddingTop), Is.EqualTo("3pt"));
            Assert.That(Value(declarations, CssProperty.Width), Is.EqualTo("50%"));
            Assert.That(Value(declarations, CssProperty.Color), Is.EqualTo("#abc"));
        }
    }

    [Test]
    public void ColourFunctionsAreStillLoweredToHex()
    {
        var declarations = Compile("color:rgb(255, 0, 16);background-color:hsl(0 100% 50%)");
        Assert.That(Value(declarations, CssProperty.Color), Is.EqualTo("#ff0010"));
        Assert.That(Value(declarations, CssProperty.BackgroundColor), Is.EqualTo("#ff0000"));
    }

    [Test]
    public void ABraceBlockMustStillBeTheWholeValue()
    {
        var limits = new CssSyntaxLimits();
        var result = CssSyntaxParser.ParseDeclarations(CssSourceText.Create("width:{y} red; color:{x}", 0, limits), limits);
        Assert.That(result.Declarations.Select(d => d.Name), Is.EqualTo(new[] { "color" }));
        Assert.That(result.Diagnostics.Count(d => d.Message.StartsWith("A CSS brace block", StringComparison.Ordinal)), Is.EqualTo(1));
    }

    [TestCase("width:10px\\")]
    [TestCase("padding-top:3pt\\")]
    [TestCase("color:#abc\\")]
    public void ATermEndingInABackslashIsStillRejected(string css)
    {
        // A final backslash decodes to U+FFFD without shortening the text, so the raw text must
        // not stand in for the decoded term.
        var error = Assert.Throws<FactsPdfException>(() => Compile(css));
        Assert.That(error!.Code, Is.EqualTo("FPDF1202"));
    }
}
