using NUnit.Framework;

namespace FactsPDF.Tests;

// The library compiles the same source on net8.0, net9.0 and net10.0. On net8.0 SplitAny and
// StartsWith(char) come from ExtensionsToReadOnlySpan and names are looked up through SpanStringSet's
// linear scan; on net9.0 and later the runtime's own members run. Every target must give these results.
[TestFixture]
public sealed class SpanCompatibilityTests
{
    private static string[] Split(string text, string separators)
    {
        var parts = new List<string>();
        foreach (var range in text.AsSpan().SplitAny(separators.AsSpan())) parts.Add(text[range]);
        return [.. parts];
    }

    [TestCase("", " ", new[] { "" })]
    [TestCase("a", " ", new[] { "a" })]
    [TestCase("a b", " ", new[] { "a", "b" })]
    [TestCase(" a", " ", new[] { "", "a" })]
    [TestCase("a ", " ", new[] { "a", "" })]
    [TestCase("a  b", " ", new[] { "a", "", "b" })]
    [TestCase(" ", " ", new[] { "", "" })]
    [TestCase("noopener\tnoreferrer\r\nnofollow\fugc", " \t\r\n\f", new[] { "noopener", "noreferrer", "", "nofollow", "ugc" })]
    [TestCase("a,b;c", ",;", new[] { "a", "b", "c" })]
    [TestCase("a b c\td", "", new[] { "a", "b", "c", "d" })]
    public void SplitAnyYieldsTheRuntimesRanges(string text, string separators, string[] expected) =>
        Assert.That(Split(text, separators), Is.EqualTo(expected));

    [TestCase("#x41", '#', true)]
    [TestCase("x#", '#', false)]
    [TestCase("", '#', false)]
    public void StartsWithCharChecksOnlyTheFirstCharacter(string text, char value, bool expected) =>
        Assert.That(text.AsSpan().StartsWith(value), Is.EqualTo(expected));

    [Test]
    public void SpanStringSetFindsTheStoredSpellingIgnoringAsciiCase()
    {
        var set = new SpanStringSet(["noopener", "privacy-policy"]);
        Assert.Multiple(() =>
        {
            Assert.That(set.TryGetValue("NoOpener".AsSpan(), out var found), Is.True);
            Assert.That(found, Is.SameAs("noopener"));
            Assert.That(set.Contains("PRIVACY-POLICY".AsSpan()), Is.True);
            Assert.That(set.Contains("noopene".AsSpan()), Is.False);
            Assert.That(set.TryGetValue("stylesheet".AsSpan(), out var missing), Is.False);
            Assert.That(missing, Is.Empty);
        });
    }
}
