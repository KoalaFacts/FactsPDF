using NUnit.Framework;

namespace FactsPDF.Tests;

// #rgb is read by doubling each hex digit in place rather than through a #rrggbb string.
[TestFixture]
[NonParallelizable]
public sealed class ShortHexColorTests
{
    [TestCase("#789", "#778899")]
    [TestCase("#000", "#000000")]
    [TestCase("#fff", "#ffffff")]
    [TestCase("#AbC", "#aabbcc")]
    [TestCase("#f0a", "#ff00aa")]
    [TestCase("#123", "#112233")]
    [TestCase("#7899", "#77889999")]
    public void ShortFormEqualsItsLongForm(string shortForm, string longForm)
        => Assert.That(InlineCss.Color(shortForm, 0), Is.EqualTo(InlineCss.Color(longForm, 0)));

    [TestCase("#78")]
    [TestCase("#78990")]
    [TestCase("#78g")]
    [TestCase("#+78")]
    [TestCase("#7 8")]
    [TestCase(" #789")]
    [TestCase("#78 ")]
    [TestCase("789a")]
    public void MalformedShortFormsStillFailWithTheirOffset(string value)
    {
        var error = Assert.Throws<FactsPdfException>(() => InlineCss.Color(value, 12))!;
        Assert.That(error.Code, Is.EqualTo("FPDF1202"));
        Assert.That(error.SourceOffset, Is.EqualTo(12));
    }

    [AllocationBudget]
    [Test]
    public void ReadingAShortHexColorAllocatesNothing()
    {
        for (var i = 0; i < 100; i++) InlineCss.Color("#789", 0);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1_000; i++) InlineCss.Color("#789", 0);
        Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero);
    }

    [Test]
    public void ShortAndLongHexBordersRenderTheSameBytes()
    {
        static byte[] Render(string color) => Pdf.Write().ToBytes(
            "<table style=\"width:520pt\"><colgroup><col style=\"width:520pt\"></colgroup><tbody>"
            + $"<tr><td style=\"padding:3pt;border:0.5pt solid {color};color:{color}\">AB</td></tr></tbody></table>"
            + $"<p style=\"color:{color};background-color:{color}\">x</p>");
        Assert.That(Render("#789"), Is.EqualTo(Render("#778899")));
        Assert.That(Render("#F0a"), Is.EqualTo(Render("#ff00aa")));
    }
}
