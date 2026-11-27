using System.Diagnostics;
using System.Globalization;
using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// Custom properties on hostile pages stay within FactsPDF's resource limits: an element pays only for
/// the custom properties it declares (never a copy of everything it inherits), the number of distinct
/// names is capped by <c>MaxCssCustomProperties</c>, and reference chains and the nesting of substituted
/// values are bounded by <c>MaxCssSyntaxDepth</c> however the chain is resolved (all FPDF1205).
/// </summary>
[TestFixture]
[NonParallelizable]
public sealed class CustomPropertyBudgetTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 400, Margin = 20 };

    private static byte[] Pdf(string html, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, options ?? Page);
        return output.ToArray();
    }

    private static FactsPdfException Fails(string html, PdfOptions options)
    {
        var error = Assert.Throws<FactsPdfException>(() => Pdf(html, options))!;
        Assert.That(error.Code, Is.EqualTo("FPDF1205"), error.Message);
        return error;
    }

    private static string Document(string css, string body) =>
        $"<!doctype html><html><head><style>{css}</style></head><body>{body}</body></html>";

    private static string RootTokens(int count)
    {
        var css = new StringBuilder(":root{");
        for (var i = 0; i < count; i++) css.Append(CultureInfo.InvariantCulture, $"--t{i}:{i % 9 + 1}pt;");
        return css.Append('}').ToString();
    }

    // 4,000 tokens on :root and 20,000 elements that each declare a custom property of their own.
    // Copying the inherited environment per declaring element allocated about 20,000 x 4,000 entries
    // (several GiB); a layered environment allocates for the element's own declaration only.
    private static string HostilePage()
    {
        var body = new StringBuilder("<div class='panel'><p>");
        for (var i = 0; i < 20_000; i++) body.Append("<b></b>");
        body.Append("</p></div>");
        return Document(RootTokens(4_000) + "b{--own:1pt}.panel{padding:var(--t3999) var(--t5)}", body.ToString());
    }

    [Test]
    public void ElementsPayOnlyForTheCustomPropertiesTheyDeclare()
    {
        var html = HostilePage();
        Pdf(html); // warm up
        var clock = Stopwatch.StartNew();
        var before = GC.GetAllocatedBytesForCurrentThread();
        var pdf = Pdf(html);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        TestContext.Out.WriteLine($"hostile page: {allocated / 1024} KiB in {clock.Elapsed.TotalMilliseconds:F0} ms");
        Assert.That(Encoding.ASCII.GetString(pdf, 0, 5), Is.EqualTo("%PDF-"));
        Assert.That(allocated, Is.LessThan(64L << 20), "per-element cost must not grow with the inherited custom properties");
        Assert.That(clock.Elapsed, Is.LessThan(TimeSpan.FromSeconds(10)));
    }

    [Test]
    public void InheritedValuesThroughManyDeclaringAncestorsResolveLikeCopies()
    {
        // Every level declares its own property and redefines --x from the level above, so a lookup
        // walks past many layers; initial hides an ancestor's value and inherit keeps it.
        var css = new StringBuilder(":root{--x:#111111;--keep:#2e7d32;--gone:#2e7d32}");
        var body = new StringBuilder();
        for (var i = 0; i < 60; i++)
        {
            css.Append(CultureInfo.InvariantCulture, $".l{i}{{--l{i}:{i}pt;--x:var(--x2, #2e7d32);--keep:inherit}}");
            body.Append(CultureInfo.InvariantCulture, $"<div class='l{i}'>");
        }
        css.Append(".l59{--gone:initial}p{color:var(--keep);background-color:var(--gone, #ffffff);margin-left:var(--l0)}");
        body.Append("<p>deep</p>").Append(new StringBuilder().Insert(0, "</div>", 60));
        var literal = Document("p{color:#2e7d32;background-color:#ffffff;margin-left:0pt}", body.ToString());
        Assert.That(Pdf(Document(css.ToString(), body.ToString())), Is.EqualTo(Pdf(literal)));
    }

    [Test]
    public void DistinctCustomPropertyNamesAreCapped()
    {
        var body = "<p>x</p>";
        Assert.That(Encoding.ASCII.GetString(Pdf(Document(RootTokens(4_096), body)), 0, 5), Is.EqualTo("%PDF-"));
        var clock = Stopwatch.StartNew();
        var error = Fails(Document(RootTokens(5_000), body), Page);
        Assert.That(error.Message, Does.Contain("MaxCssCustomProperties"));
        Assert.That(clock.Elapsed, Is.LessThan(TimeSpan.FromSeconds(5)));
        // Repeating a name, in rules and style attributes, does not count it again.
        var repeated = Document(":root{--a:1pt;--b:2pt}p{--a:3pt}", "<p style='--b:4pt;--a:5pt'>x</p>");
        Assert.That(Encoding.ASCII.GetString(Pdf(repeated, Page with { MaxCssCustomProperties = 2 }), 0, 5), Is.EqualTo("%PDF-"));
        Assert.That(Fails(repeated, Page with { MaxCssCustomProperties = 1 }).Message, Does.Contain("MaxCssCustomProperties"));
    }

    [Test]
    public void AReferenceChainResolvedBottomUpIsBoundedByTheSyntaxDepth()
    {
        // Names sort in chain order, so each link finds the one before it already resolved and the
        // resolver never recurses deeper than one link: only the chain length stops it.
        var css = new StringBuilder(":root{--c0000:#2e7d32");
        for (var i = 1; i < 10_000; i++) css.Append(CultureInfo.InvariantCulture, $";--c{i:D4}:var(--c{i - 1:D4})");
        css.Append("}p{color:var(--c9999)}");
        var options = Page with { MaxCssCustomProperties = 10_000 };
        var clock = Stopwatch.StartNew();
        Assert.That(Fails(Document(css.ToString(), "<p>x</p>"), options).Message, Does.Contain("MaxCssSyntaxDepth"));
        Assert.That(clock.Elapsed, Is.LessThan(TimeSpan.FromSeconds(5)));

        var shallow = new StringBuilder(":root{--c0000:#2e7d32");
        for (var i = 1; i <= 60; i++) shallow.Append(CultureInfo.InvariantCulture, $";--c{i:D4}:var(--c{i - 1:D4})");
        shallow.Append("}p{color:var(--c0060)}");
        Assert.That(Pdf(Document(shallow.ToString(), "<p>x</p>")), Is.EqualTo(Pdf(Document("p{color:#2e7d32}", "<p>x</p>"))));
    }

    [Test]
    public void SubstitutedValuesKeepTheirNestingDepth()
    {
        static string Nest(int depth, string inner) =>
            new StringBuilder().Insert(0, "f(", depth).Append(inner).Append(')', depth).ToString();
        var body = "<p>x</p>";
        // 40 + 20 functions deep after substitution: within MaxCssSyntaxDepth 64.
        var within = Document($":root{{--a:{Nest(40, "x")};--b:{Nest(20, "var(--a)")}}}", body);
        Assert.That(Pdf(within), Is.EqualTo(Pdf(Document("", body))));
        // 40 + 30: each value parses within the limit, but the substituted value nests 70 deep.
        var beyond = Document($":root{{--a:{Nest(40, "x")};--b:{Nest(30, "var(--a)")}}}", body);
        Assert.That(Fails(beyond, Page).Message, Does.Contain("nests functions and blocks deeper than MaxCssSyntaxDepth"));
        // A chain resolved bottom-up that wraps each link in a function grows one level per link.
        var css = new StringBuilder(":root{--n000:x");
        for (var i = 1; i < 200; i++) css.Append(CultureInfo.InvariantCulture, $";--n{i:D3}:f(var(--n{i - 1:D3}))");
        css.Append('}');
        Assert.That(Fails(Document(css.ToString(), body), Page).Message, Does.Contain("MaxCssSyntaxDepth"));
    }
}
