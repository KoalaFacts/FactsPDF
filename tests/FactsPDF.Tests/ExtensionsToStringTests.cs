using FactsPDF.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

[TestFixture]
public sealed class ExtensionsToStringTests
{
    private static string[] Lines(string text) => text.SplitLines().Select(range => text[range]).ToArray();

    [TestCase("", new[] { "" })]
    [TestCase("a", new[] { "a" })]
    [TestCase("a\nb", new[] { "a", "b" })]
    [TestCase("a\r\nb", new[] { "a", "b" })]
    [TestCase("a\rb", new[] { "a", "b" })]
    [TestCase("a\r\n\n\rb", new[] { "a", "", "", "b" })]
    [TestCase("a\n\r\nb", new[] { "a", "", "b" })]
    [TestCase("a\r\r\nb", new[] { "a", "", "b" })]
    [TestCase("a\n", new[] { "a", "" })]
    [TestCase("a\r\n", new[] { "a", "" })]
    public void CrLfIsOneBreakAndLoneCrOrLfIsOneBreak(string text, string[] expected)
        => Assert.That(Lines(text), Is.EqualTo(expected));

    [TestCase("a\fb\u0085c\u2028d\u2029e")]
    public void OnlyCrAndLfEndLines(string text) => Assert.That(Lines(text), Is.EqualTo(new[] { text }));

    [TestCase("abc", 0, 1, 1)]
    [TestCase("abc", 3, 1, 4)]
    [TestCase("ab\r\ncd", 4, 2, 1)]
    [TestCase("ab\r\ncd", 2, 1, 3)]
    [TestCase("ab\rcd", 5, 2, 3)]
    [TestCase("😀x", 2, 1, 2)]
    [TestCase("a\n😀😀x", 6, 2, 3)]
    public void LineAndColumnAreOneBasedAndCountSurrogatePairsOnce(string text, int offset, int line, int column)
        => Assert.That(text.LineAndColumnAt(offset), Is.EqualTo((line, column)));

    [TestCase(-1)]
    [TestCase(4)]
    public void OffsetOutsideTheTextIsRejected(int offset)
        => Assert.Throws<ArgumentOutOfRangeException>(() => "abc".LineAndColumnAt(offset));
}
