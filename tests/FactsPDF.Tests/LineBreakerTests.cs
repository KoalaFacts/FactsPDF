using System.Globalization;
using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

// UAX #14 line breaking. The boundary cases pin the rules text layout relies on; setting
// FACTSPDF_LINE_BREAK_TEST to a Unicode LineBreakTest.txt runs the whole conformance file.
[TestFixture]
public sealed class LineBreakerTests
{
    private static string Mark(string text, bool strict = true)
    {
        var scalars = text.EnumerateRunes().Select(rune => rune.Value).ToArray();
        var breaks = new LineBreakAnalyzer().Analyze(scalars, strict);
        var result = new StringBuilder();
        for (var i = 0; i < scalars.Length; i++)
        {
            if (i > 0 && breaks[i] != LineBreakAnalyzer.No) result.Append('|');
            result.Append(char.ConvertFromUtf32(scalars[i]));
        }
        return result.ToString();
    }

    [TestCase("ab cd", "ab |cd")]
    [TestCase("well-known", "well-|known")]
    [TestCase("a\u00a0b c", "a\u00a0b |c")]
    [TestCase("a\u200bb", "a\u200b|b")]
    [TestCase("a\u2060b", "a\u2060b")]
    [TestCase("foo(bar)baz", "foo(bar)baz")]
    [TestCase("(a b)", "(a |b)")]
    [TestCase("( a)", "( |a)")]
    [TestCase("$12.50 US", "$12.50 |US")]
    [TestCase("1,234.5", "1,234.5")]
    [TestCase("日本語", "日|本|語")]
    [TestCase("日本。語", "日|本。|語")]
    [TestCase("日「本」語", "日|「本」|語")]
    [TestCase("\U0001F1E6\U0001F1E7\U0001F1E8", "\U0001F1E6\U0001F1E7|\U0001F1E8")]
    public void FollowsTheDefaultRules(string text, string expected) => Assert.That(Mark(text), Is.EqualTo(expected));

    [Test]
    public void SmallKanaIsAStarterOnlyOutsideStrictMode()
    {
        Assert.That(Mark("日ぁ", strict: false), Is.EqualTo("日|ぁ"));
        Assert.That(Mark("日ぁ", strict: true), Is.EqualTo("日ぁ"));
    }

    [Test]
    public void AnInlineAtomBreaksOnBothSides()
    {
        var breaks = new LineBreakAnalyzer().Analyze([(int)'a', -2, (int)'b'], true);
        Assert.That(breaks.ToArray(), Is.EqualTo(new byte[] { 0, 1, 1 }));
    }

    [Test]
    public void MatchesTheUnicodeConformanceFile()
    {
        var path = Environment.GetEnvironmentVariable("FACTSPDF_LINE_BREAK_TEST");
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) Assert.Ignore("Set FACTSPDF_LINE_BREAK_TEST to LineBreakTest.txt to run this.");
        var failures = new List<string>();
        var analyzer = new LineBreakAnalyzer();
        foreach (var line in File.ReadLines(path))
        {
            var content = line.Split('#')[0].Trim();
            if (content.Length == 0) continue;
            var scalars = new List<int>();
            var expected = new List<bool>();
            foreach (var token in content.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (token is "÷" or "×") { expected.Add(token == "÷"); continue; }
                scalars.Add(int.Parse(token, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
            }
            var breaks = analyzer.Analyze(scalars.ToArray(), strictNonStarters: true);
            for (var i = 1; i < scalars.Count; i++)
                if ((breaks[i] != LineBreakAnalyzer.No) != expected[i])
                {
                    failures.Add(line.Split('#')[0].Trim() + " at " + i + "  " + line.Split('#').ElementAtOrDefault(1)?.Trim());
                    break;
                }
        }
        Assert.That(failures.Take(15), Is.Empty, failures.Count + " conformance failures");
    }
}
