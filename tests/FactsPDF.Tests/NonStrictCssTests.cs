using System.Text;
using FactsPDF.CommandLine;
using FactsPDF.CssSyntax;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// Strict and non-strict CSS (docs/css-stylesheets.md, "Strict and non-strict CSS"). Strict mode, the
/// default, is unchanged. Non-strict mode drops CSS that is invalid per the CSS specifications exactly
/// as a browser does (class A): the page renders byte-identical to the same page with the invalid
/// declaration, rule or at-rule removed (or, for a declaration invalid at computed-value time, with
/// its value replaced by unset), and each drop is reported as a warning carrying the code, message
/// and offset strict mode throws. Valid CSS the renderer does not implement (class B) and resource
/// limits (class C) fail in both modes with the same error.
/// </summary>
[TestFixture]
public sealed class NonStrictCssTests
{
    private static readonly PdfWriter Writer = Pdf.Write().WithPage(p => p.Size(300, 400).Margin(20));

    private static string Document(string css, string body = "<h1>Invoice</h1><div class='panel'><p class='lead'>Paid in full</p></div><p>Thanks</p>") =>
        $"<!doctype html><html><head><style>{css}</style></head><body>{body}</body></html>";

    private static (byte[] Pdf, PdfWarnings Warnings) NonStrict(string html, PdfWriter? writer = null)
    {
        PdfWarnings? warnings = null;
        var pdf = (writer ?? Writer).WithStrict(false).WithWarnings(report => warnings = report).ToBytes(html);
        Assert.That(warnings, Is.Not.Null, "The handler is called once for every successful conversion.");
        return (pdf, warnings!);
    }

    private static FactsPdfException Strict(string html) => Assert.Throws<FactsPdfException>(() => Writer.ToBytes(html))!;

    private static void AssertWarningIsTheStrictError(PdfWarning warning, FactsPdfException strict)
    {
        Assert.That(warning.Code, Is.EqualTo(strict.Code));
        Assert.That(warning.ToString(), Is.EqualTo(strict.Message));
        Assert.That(warning.SourceOffset, Is.EqualTo(strict.SourceOffset));
    }

    // Class A: invalid per the specifications, so Chrome drops it while parsing (each verdict was
    // checked against Chrome 158's CSSOM). The third column is the page with the dropped CSS removed.
    [TestCase("p{colr:red;color:navy}", "p{color:navy}", Description = "unknown property")]
    [TestCase("p{-moz-transform:rotate(5deg);color:navy}", "p{color:navy}", Description = "prefix Chrome does not know")]
    [TestCase("p{--:red;color:navy}", "p{color:navy}", Description = "-- alone is not a custom property")]
    [TestCase("p{color:bogus;font-size:14pt}", "p{font-size:14pt}", Description = "unknown colour keyword")]
    [TestCase("p{color:red blue;font-size:14pt}", "p{font-size:14pt}", Description = "two colours")]
    [TestCase("p{font-size:-4pt;color:navy}", "p{color:navy}", Description = "negative font size")]
    [TestCase(".panel{padding:-4pt;color:navy}", ".panel{color:navy}", Description = "negative padding")]
    [TestCase("p{margin:1pt 2pt 3pt 4pt 5pt;color:navy}", "p{color:navy}", Description = "five margin values")]
    [TestCase("p{margin-top:5foo;color:navy}", "p{color:navy}", Description = "unknown unit")]
    [TestCase(".panel{border:1pt solid red dashed}", ".panel{}", Description = "two border styles")]
    [TestCase("p{font-weight:1001}", "p{}", Description = "weight above 1000")]
    [TestCase("p{text-align:middle;color:navy}", "p{color:navy}", Description = "unknown keyword")]
    [TestCase("p{color:inherit red}", "p{}", Description = "CSS-wide keyword with another value")]
    [TestCase("p{color:;font-size:14pt}", "p{font-size:14pt}", Description = "empty value")]
    [TestCase("p{color:var(brand);font-size:14pt}", "p{font-size:14pt}", Description = "malformed var()")]
    [TestCase("p{float:lefty}", "p{}", Description = "unknown float keyword")]
    [TestCase("p..x{color:red}p{color:navy}", "p{color:navy}", Description = "selector: dot without a class")]
    [TestCase("p > {color:red}p{color:navy}", "p{color:navy}", Description = "selector: trailing combinator")]
    [TestCase("h1,,p{color:red}p{color:navy}", "p{color:navy}", Description = "selector: empty selector in the list")]
    [TestCase("p:frobnicate{color:red}p{color:navy}", "p{color:navy}", Description = "selector: unknown pseudo-class")]
    [TestCase("#1a{color:red}p{color:navy}", "p{color:navy}", Description = "selector: ID that is not an identifier")]
    [TestCase("[a b]{color:red}p{color:navy}", "p{color:navy}", Description = "selector: malformed attribute")]
    [TestCase("@frobnicate{p{color:red}}p{color:navy}", "p{color:navy}", Description = "unknown at-rule with a block")]
    [TestCase("@frobnicate;p{color:navy}", "p{color:navy}", Description = "unknown statement at-rule")]
    [TestCase("@media print;p{color:navy}", "p{color:navy}", Description = "@media without a block")]
    [TestCase("@media print{@frobnicate;p{color:navy}}", "@media print{p{color:navy}}", Description = "unknown at-rule inside @media")]
    [TestCase("p{color:navy;@frobnicate;font-size:14pt}", "p{color:navy;font-size:14pt}", Description = "unknown at-rule inside a style rule")]
    [TestCase("@keyframes none{from{color:red}}p{color:navy}", "p{color:navy}", Description = "keyframes named none")]
    public void InvalidCssIsDroppedLikeABrowserAndReportedWithTheStrictError(string css, string withoutIt)
    {
        var html = Document(css);
        var strict = Strict(html);
        var (pdf, warnings) = NonStrict(html);
        Assert.That(pdf, Is.EqualTo(Writer.ToBytes(Document(withoutIt))), css);
        Assert.That(warnings.Items, Has.Count.EqualTo(1), string.Join("\n", warnings.Items));
        Assert.That(warnings.TotalCount, Is.EqualTo(1));
        AssertWarningIsTheStrictError(warnings.Items[0], strict);
    }

    [TestCase("<p style='colour:blue;font-size:14pt'>x</p>", "<p style='font-size:14pt'>x</p>")]
    [TestCase("<p style='color:bogus'>x</p>", "<p style=''>x</p>")]
    [TestCase("<p style='padding-left:-1pt;color:navy'>x</p>", "<p style='color:navy'>x</p>")]
    public void InvalidStyleAttributeDeclarationsAreDropped(string body, string withoutIt)
    {
        var html = Document("", body);
        var strict = Strict(html);
        var (pdf, warnings) = NonStrict(html);
        Assert.That(pdf, Is.EqualTo(Writer.ToBytes(Document("", withoutIt))));
        Assert.That(warnings.Items, Has.Count.EqualTo(1));
        AssertWarningIsTheStrictError(warnings.Items[0], strict);
    }

    // CSS Syntax error recovery: the parser drops exactly what a browser drops and nothing else.
    [TestCase("p{color:navy} }", "p{color:navy}", 1, Description = "stray closing brace starts a rule that never gets a block (both errors share one location)")]
    [TestCase("p{color:'abc\n;font-size:14pt}h1{color:navy}", "p{font-size:14pt}h1{color:navy}", 1, Description = "bad string drops its declaration")]
    [TestCase("p{color:navy", "p{color:navy}", 1, Description = "unclosed block at the end is closed")]
    [TestCase("p{color:navy}/* unterminated", "p{color:navy}", 1, Description = "unterminated comment runs to the end")]
    public void SyntaxErrorsRecoverLikeABrowser(string css, string withoutIt, int count)
    {
        var html = Document(css);
        var strict = Strict(html);
        Assert.That(strict.Code, Is.EqualTo("FPDF1203"));
        var (pdf, warnings) = NonStrict(html);
        Assert.That(pdf, Is.EqualTo(Writer.ToBytes(Document(withoutIt))));
        Assert.That(warnings.Items.Select(w => w.Code), Is.All.EqualTo("FPDF1203"));
        Assert.That(warnings.TotalCount, Is.EqualTo(count));
        AssertWarningIsTheStrictError(warnings.Items[0], strict);
    }

    // Invalid at computed-value time: the declaration still wins the cascade and computes as unset.
    [TestCase("body{color:navy}.lead{color:var(--missing)}", "body{color:navy}.lead{color:unset}")]
    [TestCase(":root{--c:bogus}.lead{color:var(--c)}", ".lead{color:unset}")]
    [TestCase("p{margin-bottom:20pt}.lead{margin-bottom:var(--gap)}", "p{margin-bottom:20pt}.lead{margin-bottom:unset}")]
    [TestCase(":root{--w:-2pt}.panel{padding:var(--w) 4pt}", ".panel{padding:unset}")]
    [TestCase(":root{--a:var(--b);--b:var(--a)}.lead{color:var(--a)}", ".lead{color:unset}")]
    public void InvalidSubstitutionsBehaveAsUnset(string css, string unset)
    {
        var html = Document(css);
        var strict = Strict(html);
        var (pdf, warnings) = NonStrict(html);
        Assert.That(pdf, Is.EqualTo(Writer.ToBytes(Document(unset))), css);
        Assert.That(warnings.Items, Has.Count.EqualTo(1));
        AssertWarningIsTheStrictError(warnings.Items[0], strict);
    }

    // Class B: valid CSS the renderer does not implement fails in both modes with the same error,
    // because dropping it would render something different from a browser.
    [TestCase("p{position:absolute}")]
    [TestCase("p{display:flex}")]
    [TestCase("p{font-style:oblique}")]
    [TestCase("p{transform:rotate(5deg)}")]
    [TestCase("p{grid-template-columns:1fr 1fr}")]
    [TestCase("p{background:url(x.png)}")]
    [TestCase("p{color:revert}")]
    [TestCase("p:hover{color:red}")]
    [TestCase("p + p{color:red}")]
    [TestCase("p::before{color:red}")]
    [TestCase("@page{margin:0}")]
    [TestCase("@import url(x.css);")]
    [TestCase("@media screen{p{color:red}}")]
    [TestCase("@media print{color:red}")]
    [TestCase("p{a{color:red}}")]
    [TestCase("p{display:var(--missing)}")]
    [TestCase(":root{--d:flex}p{display:var(--d)}")]
    [TestCase("p{color:var(--missing, initial)}")]
    [TestCase("span{margin-top:4pt}", "<p><span>x</span></p>")]
    public void ValidButUnsupportedCssFailsInBothModes(string css, string? body = null)
    {
        var html = body is null ? Document(css) : Document(css, body);
        var strict = Strict(html);
        var nonStrict = Assert.Throws<FactsPdfException>(() => Writer.WithStrict(false).ToBytes(html))!;
        Assert.That(nonStrict.Code, Is.EqualTo(strict.Code), css);
        if (!css.Contains("var(", StringComparison.Ordinal))
        {
            Assert.That(nonStrict.Message, Is.EqualTo(strict.Message));
            Assert.That(nonStrict.SourceOffset, Is.EqualTo(strict.SourceOffset));
        }
    }

    [Test]
    public void ResourceLimitsFailInBothModes()
    {
        var html = Document(string.Concat(Enumerable.Repeat("p{colr:red;color:navy}", 4)));
        var limited = Writer.WithLimits(l => l.MaxCssDeclarations(3));
        var strict = Assert.Throws<FactsPdfException>(() => limited.WithStrict(true).ToBytes(html.Replace("colr:red;", "", StringComparison.Ordinal)))!;
        Assert.That(strict.Code, Is.EqualTo("FPDF1205"));
        var nonStrict = Assert.Throws<FactsPdfException>(() => limited.WithStrict(false).ToBytes(html))!;
        Assert.That(nonStrict.Code, Is.EqualTo("FPDF1205"));
    }

    [Test]
    public void StrictIsTheDefaultAndUnchanged()
    {
        var invalid = Document("p{colr:red}");
        Assert.That(Assert.Throws<FactsPdfException>(() => Pdf.Write().ToBytes(invalid))!.Code, Is.EqualTo("FPDF1201"));
        Assert.That(Assert.Throws<FactsPdfException>(() => Pdf.Write().WithStrict(true).ToBytes(invalid))!.Code, Is.EqualTo("FPDF1201"));
        var valid = Document("p{color:navy}");
        Assert.That(Writer.WithStrict(true).ToBytes(valid), Is.EqualTo(Writer.ToBytes(valid)));
        Assert.That(Writer.WithStrict(false).ToBytes(valid), Is.EqualTo(Writer.ToBytes(valid)));
        PdfWarnings? warnings = null;
        _ = Writer.WithWarnings(report => warnings = report).ToBytes(valid);
        Assert.That(warnings!.Items, Is.Empty);
        Assert.That(warnings.TotalCount, Is.Zero);
    }

    [Test]
    public void WarningsAreInSourceOrderAndDoNotChangeTheBytes()
    {
        var css = ":root{--x:1}.lead{color:var(--missing)}p{colr:red}h1{color:bogus}@frobnicate;";
        var html = Document(css, "<h1>T</h1><p class='lead' style='colour:red'>a</p><p class='lead'>b</p>");
        var (pdf, warnings) = NonStrict(html);
        Assert.That(warnings.Items.Select(w => w.SourceOffset), Is.Ordered);
        Assert.That(warnings.Items.Select(w => w.Code), Is.EqualTo(new[] { "FPDF1202", "FPDF1201", "FPDF1202", "FPDF1204", "FPDF1201" }));
        Assert.That(warnings.TotalCount, Is.EqualTo(5), "The var() declaration that fails for two elements is reported once.");
        Assert.That(Writer.WithStrict(false).ToBytes(html), Is.EqualTo(pdf), "A handler does not change the output.");
        using var stream = new MemoryStream();
        PdfWarnings? streamed = null;
        Writer.WithStrict(false).WithWarnings(report => streamed = report).ToStream(html, stream);
        Assert.That(stream.ToArray(), Is.EqualTo(pdf));
        Assert.That(streamed!.Items, Is.EqualTo(warnings.Items));
        var path = Path.Combine(Path.GetTempPath(), $"factspdf-nonstrict-{Guid.NewGuid():N}.pdf");
        try
        {
            PdfWarnings? filed = null;
            Writer.WithStrict(false).WithWarnings(report => filed = report).ToFile(html, path);
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(pdf));
            Assert.That(filed!.Items, Is.EqualTo(warnings.Items));
        }
        finally { File.Delete(path); }
    }

    [Test]
    public void ARepeatedStyleAttributeIsReportedForEveryElement()
    {
        var html = Document("", "<p style='colour:red'>a</p><p style='colour:red'>b</p><p style='colour:red'>c</p>");
        var (pdf, warnings) = NonStrict(html);
        Assert.That(warnings.Items, Has.Count.EqualTo(3));
        Assert.That(warnings.Items.Select(w => w.SourceOffset).Distinct().Count(), Is.EqualTo(3));
        Assert.That(pdf, Is.EqualTo(Writer.ToBytes(Document("", "<p>a</p><p>b</p><p>c</p>"))));
    }

    [Test]
    public void WarningsBeyondTheLimitAreCountedNotStored()
    {
        var css = string.Concat(Enumerable.Range(0, 6).Select(i => $"p{{colr{i}:red}}"));
        var (pdf, warnings) = NonStrict(Document(css), Writer.WithLimits(l => l.MaxCssWarnings(2)));
        Assert.That(warnings.Items, Has.Count.EqualTo(2));
        Assert.That(warnings.TotalCount, Is.EqualTo(6));
        Assert.That(pdf, Is.EqualTo(Writer.ToBytes(Document(""))));
        Assert.Throws<ArgumentOutOfRangeException>(() => Pdf.Write().WithLimits(l => l.MaxCssWarnings(0)));
    }

    [Test]
    public void AHandlerExceptionFailsTheConversionAndWritesNothing()
    {
        using var stream = new MemoryStream();
        var writer = Writer.WithStrict(false).WithWarnings(_ => throw new InvalidOperationException("stop"));
        Assert.Throws<InvalidOperationException>(() => writer.ToStream(Document("p{colr:red}"), stream));
        Assert.That(stream.Length, Is.Zero);
    }

    // The verdicts behind class A. Each "invalid" row is rejected by Chrome 158 (CSS.supports); each
    // "not proven" row is valid CSS, or quirks-mode CSS, or a value the grammar does not examine.
    [TestCase("color", "bogus", true)]
    [TestCase("color", "abc", false, Description = "quirks-mode hashless colour")]
    [TestCase("color", "rgb(1 2 3 / 50%)", false)]
    [TestCase("color", "frob(1)", true, Description = "unknown function")]
    [TestCase("width", "100", false, Description = "quirks-mode unitless length")]
    [TestCase("width", "-1px", true)]
    [TestCase("margin", "auto auto auto auto auto", true)]
    [TestCase("border", "1px solid red,", false, Description = "Chrome accepts the trailing comma")]
    [TestCase("font-family", "inherit red", false, Description = "Chrome reads it as one family name")]
    [TestCase("display", "flex", false)]
    [TestCase("display", "flexy", true)]
    [TestCase("box-shadow", "frobnicate", false, Description = "no grammar: never proven invalid")]
    [TestCase("colr", "red", true)]
    [TestCase("-webkit-box-shadow", "none", false)]
    [TestCase("--brand", "{ a }", false)]
    public void DeclarationVerdicts(string name, string value, bool invalid)
    {
        var limits = new CssSyntaxLimits();
        var result = CssSyntaxParser.ParseDeclarations(CssSourceText.Create(name + ":" + value, 0, limits), limits);
        var node = result.Declarations.Single();
        Assert.That(CssSpecValidity.InvalidDeclaration(node.Name, CssSyntaxAdapter.CanonicalName(node.Name.ToLowerInvariant()), node.Values),
            Is.EqualTo(invalid), $"{name}: {value}");
    }

    [TestCase("p..a", true)]
    [TestCase("p > > a", true)]
    [TestCase("p:foo", true)]
    [TestCase("p::foo", true)]
    [TestCase("p::before span", true)]
    [TestCase("[a=b x]", true)]
    [TestCase("p:first", true, Description = "page pseudo-class outside @page")]
    [TestCase("p:hover", false)]
    [TestCase("p::-webkit-scrollbar", false)]
    [TestCase("ns|p", false, Description = "namespace prefixes are not examined")]
    [TestCase("p:is(:foo)", false, Description = "forgiving arguments are not examined")]
    [TestCase("[a=\"b\" s]", false)]
    public void SelectorVerdicts(string selector, bool invalid)
    {
        var limits = new CssSyntaxLimits();
        var result = CssSyntaxParser.ParseStylesheet(CssSourceText.Create(selector + "{}", 0, limits), limits);
        var rule = (CssQualifiedRuleNode)result.Rules.Single();
        Assert.That(CssSpecValidity.InvalidSelectorList(rule.Prelude), Is.EqualTo(invalid), selector);
    }

    private static (int Exit, string[] Lines, byte[] Output) Cli(string html, params string[] extra)
    {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(html));
        using var output = new MemoryStream();
        using var error = new StringWriter();
        var exit = CliApplication.Run(["-", "-", .. extra], input, output, error, CancellationToken.None);
        return (exit, error.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries), output.ToArray());
    }

    [TestCase("--strict=false")]
    [TestCase("--strict", "false")]
    [TestCase("--strict", "FALSE")]
    public void CliNonStrictPrintsEachWarningAndExitsZero(params string[] option)
    {
        var html = "<!doctype html><html><head><style>\np{colr:red}\nh1{color:bogus}\n</style></head><body><h1>T</h1><p>x</p></body></html>";
        var (exit, lines, output) = Cli(html, option);
        Assert.That(exit, Is.Zero);
        Assert.That(lines, Is.EqualTo(new[]
        {
            "warning FPDF1201: CSS property 'colr' is not supported. Location: line 2, column 3.",
            "warning FPDF1202: Use #rgb, #rrggbb or an opaque standard CSS named color. Location: line 3, column 10.",
            $"Wrote 1 page(s), {output.Length} bytes.",
        }));
        Assert.That(output, Is.EqualTo(Pdf.Write().ToBytes(html.Replace("p{colr:red}", "", StringComparison.Ordinal).Replace("h1{color:bogus}", "", StringComparison.Ordinal))));
    }

    [TestCase("--strict=true")]
    [TestCase("--strict", "true")]
    public void CliStrictTrueIsTheDefault(params string[] option)
    {
        var html = "<p style='colr:red'>x</p>";
        var (defaultExit, defaultLines, _) = Cli(html);
        var (exit, lines, output) = Cli(html, option);
        Assert.That(exit, Is.EqualTo(3));
        Assert.That(defaultExit, Is.EqualTo(3));
        Assert.That(lines, Is.EqualTo(defaultLines));
        Assert.That(output, Is.Empty);
    }

    [TestCase("--strict", "maybe")]
    [TestCase("--strict=")]
    [TestCase("--strict")]
    [TestCase("--strict=false", "--strict=false")]
    public void CliRejectsOtherStrictValues(params string[] option)
    {
        var (exit, lines, _) = Cli("<p>x</p>", option);
        Assert.That(exit, Is.EqualTo(2));
        Assert.That(lines, Has.Some.Contains("Usage:"));
    }
}
