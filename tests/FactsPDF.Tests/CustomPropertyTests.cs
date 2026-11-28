using System.Diagnostics;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// CSS custom properties (--name) and var() substitution. A page renders byte-identical to the same
/// page with every var() replaced by its value; custom properties cascade (specificity, !important,
/// style attributes) and inherit; var() falls back, nests and resolves custom properties that
/// reference others in dependency order. Where a browser would treat a declaration as invalid at
/// computed-value time (no value and no fallback, a cycle, an unsupported substituted value) the
/// renderer fails with FPDF1202 at that declaration instead of silently unsetting it, and doubling
/// or deep reference chains fail with FPDF1205.
/// </summary>
[TestFixture]
public sealed class CustomPropertyTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 400, Margin = 20 };

    private const string Content =
        "<h2>Invoice</h2><div class='panel'><p class='note'>Paid <span>in</span> full</p></div><p>Thanks</p>";

    private static byte[] Pdf(string html, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, options ?? Page);
        return output.ToArray();
    }

    // p{margin:0} comes first so the author rules still win: the user-agent 1em paragraph margins would
    // otherwise collapse with a panel margin of the same size and hide the effect the tests compare.
    private static string Document(string css, string body = Content) =>
        $"<!doctype html><html><head><style>h1,h2,h3,h4,h5,h6,th{{font-weight:normal}}p{{margin:0}}{css}</style></head><body>{body}</body></html>";

    private static FactsPdfException Fails(string html, string code)
    {
        var error = Assert.Throws<FactsPdfException>(() => Pdf(html))!;
        Assert.That(error.Code, Is.EqualTo(code), error.Message);
        return error;
    }

    private const string Tokens =
        ":root{--red-700:#b3261e;--brand:var(--red-700);--ink:#1d2a36;--font-3:13pt;--space-3:6pt;--space-6:12pt;--line:1.4;--rule:2px}";

    [TestCase("h2{color:var(--brand)}", "h2{color:#b3261e}")]
    [TestCase("h2{font-size:var(--font-3) !important}h2{font-size:20pt}", "h2{font-size:13pt !important}h2{font-size:20pt}")]
    [TestCase(".panel{padding:var(--space-3) var(--space-6)}", ".panel{padding:6pt 12pt}")]
    [TestCase(".panel{padding:0 var(--space-6)}", ".panel{padding:0 12pt}")]
    [TestCase(".panel{border-top:var(--rule) solid var(--red-700)}", ".panel{border-top:2px solid #b3261e}")]
    [TestCase(".panel{border:1px solid var(--brand)}", ".panel{border:1px solid #b3261e}")]
    [TestCase(".panel{margin:var(--space-6) 0}", ".panel{margin:12pt 0}")]
    [TestCase("p{line-height:var(--line)}", "p{line-height:1.4}")]
    [TestCase(".panel{background-color:var(--ink);color:white}", ".panel{background-color:#1d2a36;color:white}")]
    [TestCase("p{color:rgb(var(--r, 10), 20, 30)}", "p{color:rgb(10, 20, 30)}")]
    [TestCase("span{text-decoration:underline var(--brand)}", "span{text-decoration:underline #b3261e}")]
    [TestCase("h2{letter-spacing:var(--ls, 1pt);text-transform:var(--t, uppercase)}", "h2{letter-spacing:1pt;text-transform:uppercase}")]
    [TestCase("p{margin-inline-start:var(--space-3)}", "p{margin-inline-start:6pt}")]
    [TestCase(".panel{border-radius:var(--space-3);border:1pt solid var(--ink)}", ".panel{border-radius:6pt;border:1pt solid #1d2a36}")]
    public void VarRendersLikeTheLiteralValue(string usage, string literal)
    {
        var expected = Pdf(Document(literal));
        Assert.That(Pdf(Document(Tokens + usage)), Is.EqualTo(expected), usage);
        Assert.That(expected, Is.Not.EqualTo(Pdf(Document(""))), "the literal must change the page");
    }

    [Test]
    public void DesignTokenExampleRendersLikeItsLiteralOracle()
    {
        var examples = Path.Combine(FindRoot(), "examples");
        var variable = File.ReadAllText(Path.Combine(examples, "report-custom-properties.html"));
        var weightPin = "<style>h1,h2,h3,h4,h5,h6,th{font-weight:normal}</style>";
        var oracle = File.ReadAllText(Path.Combine(examples, "report-custom-properties-oracle.html"));
        Assert.That(variable, Does.Contain("var(--ex-brand)").And.Contain("--ex-brand: var(--ex-red-700)"));
        Assert.That(oracle, Does.Not.Contain("var("));
        var tokens = System.Text.RegularExpressions.Regex.Count(variable, @"^\s+--ex-[\w-]+:", System.Text.RegularExpressions.RegexOptions.Multiline);
        Assert.That(tokens, Is.GreaterThanOrEqualTo(70));
        var options = new PdfOptions();
        variable = weightPin + variable;
        oracle = weightPin + oracle;
        var pdf = Pdf(variable, options);
        Assert.That(pdf, Is.EqualTo(Pdf(oracle, options)));
        var a5 = new PdfOptions { PageWidth = 420, PageHeight = 595, Margin = 30 };
        Assert.That(Pdf(variable, a5), Is.EqualTo(Pdf(oracle, a5)));
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "FactsPDF.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    [Test]
    public void DroppedPropertiesAreValidatedAfterSubstitution()
    {
        Assert.That(Pdf(Document("p{cursor:var(--c, pointer);position:var(--p, static)}")), Is.EqualTo(Pdf(Document(""))));
        Assert.That(Fails(Document("p{position:var(--p, absolute)}"), "FPDF1202").Message, Does.Contain("after var() substitution"));
    }

    [Test]
    public void StyleAttributeUsesAndDeclaresCustomProperties()
    {
        var body = "<div class='panel' style='padding: 0 var(--space-6)'><p style='--ink:#123456;color:var(--ink)'>Paid</p></div>";
        var literal = "<div class='panel' style='padding: 0 12pt'><p style='color:#123456'>Paid</p></div>";
        Assert.That(Pdf(Document(Tokens, body)), Is.EqualTo(Pdf(Document("", literal))));
    }

    [Test]
    public void RepeatedStyleAttributesResolveAgainstTheirOwnElement()
    {
        // The same style text is compiled once and replayed; each element substitutes its own values.
        var body = "<div style='--c:#aa0000'><p style='color:var(--c)'>A</p></div><div style='--c:#0000aa'><p style='color:var(--c)'>B</p></div>";
        var literal = "<div><p style='color:#aa0000'>A</p></div><div><p style='color:#0000aa'>B</p></div>";
        Assert.That(Pdf(Document("", body)), Is.EqualTo(Pdf(Document("", literal))));
    }

    [Test]
    public void CustomPropertiesInheritAndChildrenOverride()
    {
        var css = ".panel{--accent:#2e7d32}p{color:var(--accent, #000000)}.note{--accent:#6a1b9a}";
        var literal = ".panel p{color:#2e7d32}.panel .note{color:#6a1b9a}";
        var body = "<div class='panel'><p>One</p><p class='note'>Two</p></div><p>Three</p>";
        Assert.That(Pdf(Document(css, body)), Is.EqualTo(Pdf(Document(literal + "p{color:#000000}", body))));
    }

    [Test]
    public void CustomPropertiesCascadeBySpecificityImportanceAndOrder()
    {
        string Page(string css) => Document(css + "p{color:var(--c)}");
        var green = Pdf(Document("p{color:#2e7d32}"));
        Assert.That(Pdf(Page(".note{--c:#2e7d32}p{--c:#c62828}")), Is.Not.EqualTo(green), "control: other paragraphs differ");
        Assert.That(Pdf(Document("p{--c:#c62828}.note{--c:#2e7d32}p{color:var(--c)}", "<p class='note'>x</p>")),
            Is.EqualTo(Pdf(Document("p{color:#2e7d32}", "<p class='note'>x</p>"))), "class beats type");
        Assert.That(Pdf(Document("p{--c:#2e7d32 !important}.note{--c:#c62828}p{color:var(--c)}", "<p class='note'>x</p>")),
            Is.EqualTo(Pdf(Document("p{color:#2e7d32}", "<p class='note'>x</p>"))), "!important beats specificity");
        Assert.That(Pdf(Document("p{--c:#c62828}p{--c:#2e7d32}p{color:var(--c)}", "<p>x</p>")),
            Is.EqualTo(Pdf(Document("p{color:#2e7d32}", "<p>x</p>"))), "later source order wins");
        Assert.That(Pdf(Document("p{--c:#c62828}p{color:var(--c)}", "<p style='--c:#2e7d32'>x</p>")),
            Is.EqualTo(Pdf(Document("p{color:#2e7d32}", "<p>x</p>"))), "style attribute beats the stylesheet");
    }

    [Test]
    public void NamesAreCaseSensitive()
    {
        var error = Fails(Document(":root{--Brand:#b3261e}p{color:var(--brand)}"), "FPDF1202");
        Assert.That(error.Message, Does.Contain("var(--brand)"));
        Assert.That(Pdf(Document(":root{--Brand:#b3261e;--brand:#2e7d32}p{color:var(--brand)}")),
            Is.EqualTo(Pdf(Document("p{color:#2e7d32}"))));
    }

    [Test]
    public void FallbacksApplyWhenTheReferenceHasNoValue()
    {
        var expected = Pdf(Document("p{color:#2e7d32}"));
        Assert.That(Pdf(Document("p{color:var(--missing, #2e7d32)}")), Is.EqualTo(expected));
        Assert.That(Pdf(Document("p{color:var(--missing, var(--also-missing, #2e7d32))}")), Is.EqualTo(expected));
        Assert.That(Pdf(Document(":root{--c:#2e7d32}p{color:var(--missing, var(--c))}")), Is.EqualTo(expected));
        Assert.That(Pdf(Document(":root{--c:#2e7d32}p{color:VAR(--c, #c62828)}")), Is.EqualTo(expected), "a present value wins over its fallback");
    }

    [Test]
    public void CssWideKeywordsOnCustomProperties()
    {
        var green = Pdf(Document("p{color:#2e7d32}"));
        // initial gives the guaranteed-invalid value, so the fallback applies.
        Assert.That(Pdf(Document(":root{--c:#c62828}p{--c:initial;color:var(--c, #2e7d32)}")), Is.EqualTo(green));
        // inherit and unset take the parent's computed value (custom properties are inherited).
        Assert.That(Pdf(Document(":root{--c:#2e7d32}p{--c:inherit;color:var(--c)}")), Is.EqualTo(green));
        Assert.That(Pdf(Document(":root{--c:#2e7d32}p{--c:UNSET;color:var(--c)}")), Is.EqualTo(green));
        // revert rolls the author origin back; custom properties inherit, so it acts as unset (Chrome 158).
        Assert.That(Pdf(Document(":root{--c:#2e7d32}p{--c:revert;color:var(--c)}")), Is.EqualTo(green));
        Assert.That(Pdf(Document(":root{--c:#2e7d32}p{--c:revert-layer;color:var(--c)}")), Is.EqualTo(green));
    }

    [Test]
    public void CustomPropertiesReferringToOthersResolveInDependencyOrder()
    {
        // Declared before their dependencies, and overridden on a descendant at the dependency only:
        // a descendant inherits the computed value, so --brand stays the root's colour.
        var css = ":root{--brand:var(--accent);--accent:var(--base);--base:#2e7d32}.panel{--base:#c62828}p{color:var(--brand)}";
        Assert.That(Pdf(Document(css)), Is.EqualTo(Pdf(Document("p{color:#2e7d32}"))));
    }

    [Test]
    public void EmptyCustomPropertySubstitutesNothing()
    {
        Assert.That(Pdf(Document(":root{--none:;}.panel{padding:4pt var(--none)}")), Is.EqualTo(Pdf(Document(".panel{padding:4pt}"))));
    }

    [Test]
    public void AnUnusedCycleChangesNothingAndAUsedCycleFails()
    {
        Assert.That(Pdf(Document(":root{--a:var(--b);--b:var(--a);--c:#2e7d32}p{color:var(--c)}")),
            Is.EqualTo(Pdf(Document("p{color:#2e7d32}"))));
        Assert.That(Pdf(Document(":root{--a:var(--b);--b:var(--a)}p{color:var(--a, #2e7d32)}")),
            Is.EqualTo(Pdf(Document("p{color:#2e7d32}"))), "a cycle member falls back like an undefined one");
        Assert.That(Pdf(Document(":root{--a:var(--a)}p{color:var(--a, #2e7d32)}")),
            Is.EqualTo(Pdf(Document("p{color:#2e7d32}"))), "a self reference is a cycle");
        var error = Fails(Document(":root{--a:#c62828 var(--b);--b:var(--a)}p{color:var(--b)}"), "FPDF1202");
        Assert.That(error.Message, Does.Contain("var(--b)").And.Contain("cycle"));
    }

    [Test]
    public void ACustomPropertyUsingAnInvalidReferenceIsItselfInvalid()
    {
        // --b has no value, so --a is guaranteed-invalid and the fallback applies, not the parent's --a.
        Assert.That(Pdf(Document(":root{--a:#c62828}p{--a:var(--b);color:var(--a, #2e7d32)}")),
            Is.EqualTo(Pdf(Document("p{color:#2e7d32}"))));
    }

    [Test]
    public void AReferenceWithoutValueOrFallbackFailsAtItsDeclaration()
    {
        var css = "p{padding:0;color:var(--missing)}";
        var html = Document(css);
        var error = Fails(html, "FPDF1202");
        Assert.That(error.Message, Does.Contain("var(--missing)").And.Contain("'color'"));
        Assert.That(error.SourceOffset, Is.EqualTo(html.IndexOf("color:var", StringComparison.Ordinal)));
    }

    [Test]
    public void AnUnsupportedValueAfterSubstitutionFailsLikeTheLiteral()
    {
        var error = Fails(Document(":root{--c:12pt}p{color:var(--c)}"), "FPDF1202");
        Assert.That(error.Message, Does.Contain("after var() substitution").And.Contain("color: 12pt"));
        Fails(Document(":root{--x:4pt}p{padding:var(--x)px}"), "FPDF1202");
        Fails(Document(":root{--x:4pt 4pt 4pt 4pt 4pt}.panel{padding:var(--x)}"), "FPDF1202");
        // The applicability rules of the substituted property still apply (padding on inline text).
        Assert.That(Fails(Document(":root{--x:4pt}span{padding:var(--x)}"), "FPDF1201").Message,
            Does.Contain("block elements"));
    }

    [Test]
    public void ACssWideKeywordFromSubstitutionAppliesAsThatKeyword()
    {
        // Chrome 158 applies a substituted CSS-wide keyword exactly like the literal keyword.
        foreach (var keyword in new[] { "inherit", "initial", "unset", "revert" })
            Assert.That(Pdf(Document($"body{{color:#2e7d32}}p{{color:#c62828}}p{{color:var(--missing, {keyword})}}")),
                Is.EqualTo(Pdf(Document($"body{{color:#2e7d32}}p{{color:#c62828}}p{{color:{keyword}}}"))), keyword);
        Assert.That(Pdf(Document(":root{--k:unset}body{color:#2e7d32}p{color:var(--k, #c62828)}")),
            Is.EqualTo(Pdf(Document("body{color:#2e7d32}p{color:#c62828}"))), "--k:unset on the root inherits nothing, so the fallback applies");
        // A custom property whose substitution is a CSS-wide keyword takes that keyword itself.
        Assert.That(Pdf(Document(":root{--c:#2e7d32}p{--c:var(--missing, inherit);color:var(--c, #c62828)}")),
            Is.EqualTo(Pdf(Document("p{color:#2e7d32}"))));
        Assert.That(Pdf(Document(":root{--c:#2e7d32}p{--c:var(--missing, initial);color:var(--c, #1565c0)}")),
            Is.EqualTo(Pdf(Document("p{color:#1565c0}"))));
        // A keyword with other components is invalid at computed-value time (strict mode fails).
        Fails(Document("p{color:var(--missing, initial) red}"), "FPDF1202");
        // A keyword the property cannot render still fails like the literal (display:initial is inline).
        Fails(Document("div{display:var(--missing, initial)}", "<div>x</div>"), "FPDF1202");
    }

    [Test]
    public void LosingAndUnmatchedDeclarations()
    {
        // Like every other matched declaration, a losing var() declaration is still checked.
        Fails(Document("p{color:var(--missing)}p{color:#2e7d32}"), "FPDF1202");
        // An unmatched rule is never substituted: its value can only be checked for an element.
        Assert.That(Pdf(Document(".absent{padding:var(--missing) bogus}p{color:#2e7d32}")),
            Is.EqualTo(Pdf(Document("p{color:#2e7d32}"))));
    }

    [TestCase("p{color:var(brand)}")]
    [TestCase("p{color:var(--a --b)}")]
    [TestCase("p{color:var()}")]
    [TestCase("p{color:var(--)}")]
    [TestCase(".absent{color:var(#fff)}")]
    [TestCase(":root{--a:var(1)}")]
    [TestCase("p{color:var(--a, var(x))}")]
    public void MalformedVarFailsAtParseTime(string css)
    {
        Assert.That(Fails(Document(css), "FPDF1202").Message, Does.Contain("var() takes a custom property name"));
    }

    [Test]
    public void UnsupportedPropertiesStillFailFirst()
    {
        Assert.That(Fails(Document("p{float:var(--x)}"), "FPDF1202").Message, Is.Not.Empty);
        Assert.That(Fails(Document("p{grid-template:var(--x)}"), "FPDF1201").Message, Does.Contain("grid-template"));
        Assert.That(Fails(Document("p{--:1}"), "FPDF1201").Message, Does.Contain("'--'"));
    }

    [Test]
    public void CustomPropertiesOnEveryElementIncludingVoidOnes()
    {
        var body = "<p>Line<br>break</p><hr>";
        Assert.That(Pdf(Document("*{--gap:4pt;--tw-ring:0 0 #0000}p{color:#2e7d32}", body)),
            Is.EqualTo(Pdf(Document("p{color:#2e7d32}", body))));
    }

    [Test]
    public void DoublingReferencesFailWithTheSyntaxNodeBudget()
    {
        var css = new System.Text.StringBuilder(":root{--l0:x x x x");
        for (var i = 1; i <= 40; i++) css.Append(System.Globalization.CultureInfo.InvariantCulture, $";--l{i}:var(--l{i - 1}) var(--l{i - 1})");
        css.Append("}p{color:var(--l40, #2e7d32)}");
        var before = GC.GetAllocatedBytesForCurrentThread();
        var clock = Stopwatch.StartNew();
        var error = Fails(Document(css.ToString()), "FPDF1205");
        Assert.That(error.Message, Does.Contain("MaxCssSyntaxNodes").Or.Contain("work budget"));
        Assert.That(clock.Elapsed, Is.LessThan(TimeSpan.FromSeconds(10)));
        Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.LessThan(64L << 20));
    }

    [Test]
    public void ASmallerSyntaxNodeBudgetBoundsEachSubstitutedValue()
    {
        // About 70 syntax nodes of stylesheet, and --l8 substitutes to 512 nodes when :root is styled.
        var css = new System.Text.StringBuilder(":root{--l0:x x");
        for (var i = 1; i <= 8; i++) css.Append(System.Globalization.CultureInfo.InvariantCulture, $";--l{i}:var(--l{i - 1}) var(--l{i - 1})");
        css.Append("}p{color:#2e7d32}");
        Assert.That(Pdf(Document(css.ToString())), Is.EqualTo(Pdf(Document("p{color:#2e7d32}"))));
        var error = Assert.Throws<FactsPdfException>(() => Pdf(Document(css.ToString()), Page with { MaxCssSyntaxNodes = 300 }))!;
        Assert.That(error.Code, Is.EqualTo("FPDF1205"));
        Assert.That(error.Message, Does.Contain("var() substitution exceeds MaxCssSyntaxNodes"));
    }

    [Test]
    public void DeepReferenceChainsFailWithTheSyntaxDepthBudget()
    {
        var css = new System.Text.StringBuilder(":root{--d0:#2e7d32");
        for (var i = 1; i <= 100; i++) css.Append(System.Globalization.CultureInfo.InvariantCulture, $";--d{i}:var(--d{i - 1})");
        css.Append("}p{color:var(--d100)}");
        Assert.That(Fails(Document(css.ToString()), "FPDF1205").Message, Does.Contain("MaxCssSyntaxDepth"));
        var shallow = new System.Text.StringBuilder(":root{--d0:#2e7d32");
        for (var i = 1; i <= 30; i++) shallow.Append(System.Globalization.CultureInfo.InvariantCulture, $";--d{i}:var(--d{i - 1})");
        shallow.Append("}p{color:var(--d30)}");
        Assert.That(Pdf(Document(shallow.ToString())), Is.EqualTo(Pdf(Document("p{color:#2e7d32}"))));
    }

    // A pseudo-element is a child of its originating element for custom properties: it sees the
    // element's, its own declarations never reach the element's children, and var() works in its
    // content and display (Chrome 158 prints each page like its literal twin).
    [TestCase("p{--c:#c00}p::before{content:'x ';color:var(--c)}", "p::before{content:'x ';color:#c00}")]
    [TestCase("p{--c:#c00}p::before{content:'x ';--c:#06c;color:var(--c)}", "p::before{content:'x ';color:#06c}")]
    [TestCase("p::before{--t:'>> ';content:var(--t)}", "p::before{content:'>> '}")]
    [TestCase("p{--t:'>> '}p::before{content:var(--t)}", "p::before{content:'>> '}")]
    [TestCase("p::before{content:var(--none,'>> ')}", "p::before{content:'>> '}")]
    [TestCase("p{--t:'<<'}p::after{content:var(--t)}", "p::after{content:'<<'}")]
    [TestCase("div{--d:block}div::before{content:'x';display:var(--d)}", "div::before{content:'x';display:block}")]
    public void VarInAPseudoElementResolvesAgainstItsElement(string css, string literal)
    {
        const string body = "<div><p>Aa</p></div>";
        Assert.That(Pdf(Document(css, body)), Is.EqualTo(Pdf(Document(literal, body))));
    }

    [Test]
    public void APseudoElementsCustomPropertyDoesNotReachTheElementsChildren()
    {
        const string body = "<p>Aa<span>Bb</span></p>";
        Assert.That(Pdf(Document("p{--c:#c00}span{color:var(--c)}p::before{content:'x ';--c:#06c}", body)),
            Is.EqualTo(Pdf(Document("span{color:#c00}p::before{content:'x '}", body))));
    }

    [Test]
    public void SubstitutionWorkIsChargedToTheMatchBudget()
    {
        // The smallest match budget the literal page needs is too small for the var() page, which
        // also pays for every component it substitutes.
        var body = string.Concat(Enumerable.Repeat("<div class='panel'><p>x</p></div>", 20));
        var literal = Document(".panel{padding:1pt 2pt 3pt 4pt}", body);
        var variable = Document(":root{--a:1pt 2pt 3pt 4pt}.panel{padding:var(--a)}", body);
        int low = 1, high = 1_000_000;
        while (low < high)
        {
            var mid = (low + high) / 2;
            try { Pdf(literal, Page with { MaxCssMatchOperations = mid }); high = mid; }
            catch (FactsPdfException error) when (error.Code == "FPDF1205") { low = mid + 1; }
        }
        Assert.That(Pdf(variable), Is.EqualTo(Pdf(literal)));
        Assert.That(Assert.Throws<FactsPdfException>(() => Pdf(variable, Page with { MaxCssMatchOperations = low }))!.Code,
            Is.EqualTo("FPDF1205"));
    }
}
