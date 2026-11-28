using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// CSS counters (counter-reset, counter-increment, counter-set) printed by counter() and counters() in
/// ::before and ::after content, with the scoping rules Chrome prints: a counter is in scope for its
/// element, descendants and following siblings, and nests under an outer counter of the same name.
/// </summary>
[TestFixture]
public sealed partial class CssCounterTests
{
    private static readonly PdfOptions Options = new() { PageWidth = 400, PageHeight = 500, Margin = 20 };

    private static string Render(string html)
    {
        using var stream = new MemoryStream();
        PdfConverter.Convert(html, stream, Options);
        return Encoding.ASCII.GetString(stream.ToArray());
    }

    // The text of each printed line, in order: every show-text operator of one baseline joined.
    private static string[] Lines(string html)
    {
        var lines = new List<(string Y, StringBuilder Text)>();
        foreach (Match match in TextOperator().Matches(Render(html)))
        {
            var text = Encoding.ASCII.GetString(Convert.FromHexString(match.Groups[2].Value));
            if (lines.Count > 0 && lines[^1].Y == match.Groups[1].Value) lines[^1].Text.Append(text);
            else lines.Add((match.Groups[1].Value, new StringBuilder(text)));
        }
        return [.. lines.Select(line => line.Text.ToString())];
    }

    private static FactsPdfException Failure(string html)
    {
        using var stream = new MemoryStream();
        return Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, stream, Options))!;
    }

    [GeneratedRegex(@"1 0 0 1 [\d.-]+ ([\d.-]+) Tm <([0-9A-F]+)> Tj")]
    private static partial Regex TextOperator();

    private const string Base = "<style>h1,h2,p,div,ol,ul,li,section{margin:0;padding:0}h1,h2{font-weight:normal}";

    [Test]
    public void IncrementThenPrintNumbersHeadings() =>
        Assert.That(Lines(Base + "h1{counter-increment:sec}h1::before{content:counter(sec) '. '}</style><h1>Intro</h1><p>x</p><h1>Next</h1>"),
            Is.EqualTo(new[] { "1. Intro", "x", "2. Next" }));

    [Test]
    public void ResetStartsTheValueIncrementAddsAndSetOverrides()
    {
        Assert.That(Lines(Base + "body{counter-reset:chap 5}h2{counter-increment:chap 2}h2::before{content:'Ch ' counter(chap) ': '}</style><h2>A</h2><h2>B</h2>"),
            Is.EqualTo(new[] { "Ch 7: A", "Ch 9: B" }));
        Assert.That(Lines(Base + "h2{counter-set:x 10}h2::after{content:' [' counter(x) ']'}</style><h2>A</h2><h2>B</h2>"),
            Is.EqualTo(new[] { "A [10]", "B [10]" }));
        // On one element: reset, then increment, then set.
        Assert.That(Lines(Base + "h2{counter-reset:x 3;counter-increment:x 4;counter-set:x 20}h2::before{content:counter(x)}</style><h2>A</h2>"),
            Is.EqualTo(new[] { "20A" }));
        Assert.That(Lines(Base + "h2{counter-reset:x 3;counter-increment:x 4}h2::before{content:counter(x)}</style><h2>A</h2>"),
            Is.EqualTo(new[] { "7A" }));
    }

    [Test]
    public void AnIncrementWithoutACounterCreatesOne()
    {
        Assert.That(Lines(Base + "p{counter-increment:n}p::before{content:counter(n) ') '}</style><p>a</p><p>b</p>"),
            Is.EqualTo(new[] { "1) a", "2) b" }));
        Assert.That(Lines(Base + "p::before{content:counter(zz) '-' counters(zz,'.')}</style><p>a</p>"), Is.EqualTo(new[] { "0-0a" }));
    }

    [Test]
    public void AfterContentSeesTheCountersOfItsChildren() =>
        Assert.That(Lines(Base + "div::after{content:'n=' counter(c)}p{counter-increment:c}</style><div><p>a</p><p>b</p></div>"),
            Is.EqualTo(new[] { "a", "b", "n=2" }));

    [Test]
    public void ACounterIsInScopeForFollowingSiblingsAndASiblingResetReplacesIt()
    {
        Assert.That(Lines(Base + "div.r{counter-reset:n}p{counter-increment:n}p::before{content:counter(n) ') '}</style><div class='r'></div><p>a</p><p>b</p><div class='r'></div><p>c</p>"),
            Is.EqualTo(new[] { "1) a", "2) b", "1) c" }));
        // The div's reset replaces the counter the first p created at the same level, and its following sibling continues it.
        Assert.That(Lines(Base + "div.r{counter-reset:n}p{counter-increment:n}p::before{content:counter(n) ') '}</style><p>a</p><div class='r'><p>b</p><p>c</p></div><p>d</p>"),
            Is.EqualTo(new[] { "1) a", "1) b", "2) c", "3) d" }));
    }

    [Test]
    public void NestedResetsNestUnderTheOuterCounterAndLeaveScopeWithTheirElement()
    {
        // The inner section resets sub under the outer section's counter: counters() lists both, and a
        // heading after the inner section sees only the outer one again.
        Assert.That(Lines(Base + "section{counter-reset:sub}h2{counter-increment:sub}h2::before{content:counters(sub,'.') ' '}</style>"
                + "<section><h2>A</h2><section><h2>B</h2><h2>C</h2></section><h2>D</h2></section><section><h2>E</h2></section>"),
            Is.EqualTo(new[] { "1 A", "1.1 B", "1.2 C", "2 D", "1 E" }));
        Assert.That(Lines(Base + "div{counter-reset:c}p{counter-increment:c}p::before{content:counters(c,'/') ' '}</style>"
                + "<div><p>a</p><div><p>b</p><div><p>c</p></div><p>d</p></div><p>e</p></div><div><p>f</p></div>"),
            Is.EqualTo(new[] { "1 a", "1/1 b", "1/1/1 c", "1/2 d", "2 e", "1 f" }));
    }

    [Test]
    public void TheListItemCounterNumbersListItems()
    {
        // The marker (1., 2., a bullet) is printed first on each line.
        Assert.That(Lines(Base + "li::before{content:counter(list-item) '/'}</style><ol><li>a</li><li>b</li></ol><ul><li>c</li></ul>"),
            Has.Some.EndsWith("1/a").And.Some.EndsWith("2/b").And.Some.EndsWith("1/c"));
        var nested = Lines(Base + "li::before{content:counters(list-item,'.') ' '}</style><ol><li>a<ol><li>b</li><li>c</li></ol></li><li>d</li></ol>");
        Assert.That(string.Join("|", nested), Does.Contain("1.1 b").And.Contain("1.2 c").And.Contain("2 d"));
        // The ol start attribute starts the counter; a li value attribute changes the marker only.
        var start = string.Join("|", Lines(Base + "li::before{content:counter(list-item) '/'}</style><ol start='5'><li>a</li><li>b</li></ol>"));
        Assert.That(start, Does.Contain("5/a").And.Contain("6/b"));
    }

    [Test]
    public void ChangingTheListItemCounterFails()
    {
        Assert.That(Failure(Base + "li{counter-increment:list-item 2}</style><ol><li>a</li></ol>").Code, Is.EqualTo("FPDF1201"));
        Assert.That(Failure(Base + "ol{counter-reset:list-item 4}</style><ol><li>a</li></ol>").Code, Is.EqualTo("FPDF1201"));
        Assert.That(Failure(Base + "li::before{content:counter(list-item)}</style><ol reversed><li>a</li></ol>").Code, Is.EqualTo("FPDF1201"));
    }

    [Test]
    public void CounterStylesFormatTheValue()
    {
        Assert.That(Lines(Base + "p{counter-increment:n 7}p::before{content:counter(n,decimal-leading-zero) '|' counter(n,lower-roman) '|' counter(n,upper-alpha) '|' counter(n,lower-latin) '|' counter(n,none) '|' counter(n,upper-roman)}</style><p>a</p>"),
            Is.EqualTo(new[] { "07|vii|G|g||VIIa" }));
        // Out of range values for alphabetic and roman styles print in decimal; leading zeros pad 0 to 9 only.
        Assert.That(Lines(Base + "p{counter-increment:n -3}p::before{content:counter(n,decimal-leading-zero) '|' counter(n,lower-roman) '|' counter(n,upper-alpha) '|' counter(n)}</style><p>a</p>"),
            Is.EqualTo(new[] { "-3|-3|-3|-3a" }));
        Assert.That(Lines(Base + "p{counter-reset:n}p::before{content:counter(n,decimal-leading-zero) '|' counter(n,lower-roman) '|' counter(n,lower-alpha)}</style><p>a</p>"),
            Is.EqualTo(new[] { "00|0|0a" }));
        Assert.That(Lines(Base + "p{counter-reset:n 27}p::before{content:counter(n,lower-alpha) '|' counter(n,lower-roman)}</style><p>a</p>"),
            Is.EqualTo(new[] { "aa|xxviia" }));
    }

    [Test]
    public void CounterNamesAreCaseSensitive() =>
        Assert.That(Lines(Base + "p{counter-increment:Foo}p::before{content:counter(Foo) counter(foo)}</style><p>x</p>"),
            Is.EqualTo(new[] { "10x" }));

    [Test]
    public void DisplayNoneElementsCountNothing() =>
        Assert.That(Lines(Base + "p{counter-increment:c}p::before{content:counter(c) ' '}</style><p>a</p><p style='display:none'>h</p><p>b</p>"),
            Is.EqualTo(new[] { "1 a", "2 b" }));

    [Test]
    public void TableRowsAndInlineElementsCount()
    {
        Assert.That(Lines(Base + "tr{counter-increment:row}td:first-child::before{content:counter(row) '. '}td{padding:0}table{border-collapse:collapse}</style>"
                + "<table><tbody><tr><td>a</td><td>b</td></tr><tr><td>c</td><td>d</td></tr></tbody></table>"),
            Has.Some.Contains("1. a").And.Some.Contains("2. c"));
        Assert.That(Lines(Base + "span{counter-increment:s}span::before{content:'[' counter(s) ']'}</style><p>a <span>b</span> c <span>d</span></p>"),
            Is.EqualTo(new[] { "a [1]b c [2]d" }));
    }

    [Test]
    public void APseudoElementMayIncrementItsOwnCounter() =>
        // The counter ::before creates is a child of its p, so it is out of scope for the next p.
        Assert.That(Lines(Base + "p::before{counter-increment:q;content:counter(q) '. '}</style><p>a</p><p>b</p>"),
            Is.EqualTo(new[] { "1. a", "1. b" }));

    [Test]
    public void InheritTakesTheParentsComputedList() =>
        Assert.That(Lines(Base + "div{counter-reset:k 4}p{counter-increment:inherit}p::before{content:counter(k)}</style><div><p>a</p></div>"),
            Is.EqualTo(new[] { "4a" }));

    [Test]
    public void SeveralCountersAndNoneInOneDeclaration()
    {
        Assert.That(Lines(Base + "p{counter-increment:a b 2}p::before{content:counter(a) counter(b)}</style><p>x</p><p>y</p>"),
            Is.EqualTo(new[] { "12x", "24y" }));
        Assert.That(Lines(Base + "p{counter-increment:a}p.n{counter-increment:none}p::before{content:counter(a)}</style><p>x</p><p class='n'>y</p>"),
            Is.EqualTo(new[] { "1x", "1y" }));
    }

    [Test]
    public void ContentWithoutCountersIsUnchanged() =>
        Assert.That(Render(Base + "p::before{content:'a' 'b'}</style><p>x</p>"), Is.EqualTo(Render(Base + "p::before{content:'ab'}</style><p>x</p>")));

    [TestCase("counter-reset:none x")]
    [TestCase("counter-reset:x 1.5")]
    [TestCase("counter-increment:default")]
    [TestCase("counter-set:1 x")]
    [TestCase("counter-reset:x y(1)")]
    [TestCase("counter-increment:")]
    public void InvalidCounterListsFail(string css) =>
        Assert.That(Failure($"{Base}p{{{css}}}</style><p>x</p>").Code, Is.EqualTo("FPDF1202"));

    [TestCase("content:counter(n,disc)")]
    [TestCase("content:counter(n,lower-greek)")]
    [TestCase("content:counter()")]
    [TestCase("content:counters(n)")]
    [TestCase("content:counter(n,decimal,x)")]
    [TestCase("content:counters(n,1)")]
    [TestCase("content:counter(none)")]
    public void InvalidCounterFunctionsFail(string css) =>
        Assert.That(Failure($"{Base}p::before{{{css}}}</style><p>x</p>").Code, Is.EqualTo("FPDF1202"));

    [Test]
    public void CounterPropertiesAreAcceptedOnEveryElement()
    {
        const string body = "<ul><li>a</li></ul><table><tbody><tr><td>b</td></tr></tbody></table><p>c <span>d</span></p><hr><img>";
        var counted = "<style>*{counter-increment:all}</style>" + body.Replace("<img>", "");
        Assert.That(Render(counted), Is.EqualTo(Render(body.Replace("<img>", ""))));
    }
}
