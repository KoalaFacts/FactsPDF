using NUnit.Framework;

namespace FactsPDF.Tests;

// The cascade and stylesheet validation apply one declaration at a time through
// InlineCss.Apply(style, name, value, ...). It must behave exactly like the joined
// "name:value" form it replaces, including the error code, message and offset.
[TestFixture]
public sealed class InlineCssDeclarationTests
{
    private static TextStyle NewStyle() => new(12, 1.2, new(0, 0, 0), TextAlignment.Left);

    private static readonly object[] Declarations =
    [
        new object[] { "font-weight", "bold", true, false },
        new object[] { "font-weight", "450", true, false },
        new object[] { "font-style", "ITALIC", true, false },
        new object[] { " font-size ", " 14pt ", true, false },
        new object[] { "font-size", "larger", true, false },
        new object[] { "font-size", "200pt", true, false },
        new object[] { "color", "#ABC", true, false },
        new object[] { "color", "RebeccaPurple", false, true },
        new object[] { "color", "red !important", true, false },
        new object[] { "color", "", true, false },
        new object[] { "color", "   ", true, false },
        new object[] { "color", "red;font-weight:bold", true, false },
        new object[] { "color", "red;", true, false },
        new object[] { "color", "red;oops", true, false },
        new object[] { "font-weight;color", "red", true, false },
        new object[] { "a:b", "red", true, false },
        new object[] { "color", "a:b", true, false },
        new object[] { "", "red", true, false },
        new object[] { "  ", "red", true, false },
        new object[] { "line-height", "1.5em", true, false },
        new object[] { "line-height", "18PT", true, false },
        new object[] { "letter-spacing", "1pt", true, false },
        new object[] { "text-decoration", "underline overline", true, false },
        new object[] { "text-decoration", "underline underline", true, false },
        new object[] { "white-space", "pre", true, false },
        new object[] { "white-space", "pre", true, true },
        new object[] { "text-align", "justify", false, false },
        new object[] { "text-align", "center", false, true },
        new object[] { "margin-top", "6pt", true, false },
        new object[] { "margin-top", "6pt", false, false },
        new object[] { "break-after", "avoid", true, false },
        new object[] { "Unknown-Property", "x", true, false },
    ];

    [TestCaseSource(nameof(Declarations))]
    public void SeparatedDeclarationMatchesTheJoinedForm(string name, string value, bool paragraph, bool inline)
    {
        var joined = Outcome(() => InlineCss.Apply(NewStyle(), name + ":" + value, paragraph, inline, 17));
        var separated = Outcome(() => InlineCss.Apply(NewStyle(), name, value, paragraph, inline, 17));
        Assert.That(separated, Is.EqualTo(joined));
    }

    [Test]
    public void SeparatedLowerCaseDeclarationAllocatesNothingForAKeyword()
    {
        var style = NewStyle();
        for (var i = 0; i < 3; i++) InlineCss.Apply(style, "font-style", "italic", true, false, 0);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100; i++) InlineCss.Apply(style, "font-style", "italic", true, false, 0);
        Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero);
        Assert.That(style.Italic, Is.True);
    }

    private static string Outcome(Func<TextStyle> apply)
    {
        try
        {
            return "ok " + apply();
        }
        catch (FactsPdfException error)
        {
            return $"{error.Code} {error.Message} {error.SourceOffset}";
        }
    }
}
