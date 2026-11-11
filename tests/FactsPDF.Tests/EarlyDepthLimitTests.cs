using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

// R1c1: MaxDepth (FPDF1003) is enforced while the collecting pass tokenizes the page, so a nesting
// flood stops at the first element past the limit instead of after the whole page is tokenized.
// The early check replays the reader's open elements; it must never fail a document the reader
// renders, and must leave the reader's own error codes alone where it cannot mirror the reader.
[TestFixture]
[NonParallelizable]
public sealed class EarlyDepthLimitTests
{
    private static string Repeat(string unit, int count) => new StringBuilder(unit.Length * count).Insert(0, unit, count).ToString();

    private static PdfWriter Writer(int maxDepth) => Pdf.Write().WithLimits(l => l.MaxInputCharacters(8_000_000)
        .MaxElements(400_000).MaxDepth(maxDepth));

    private static FactsPdfException Fails(string html, int maxDepth) =>
        Assert.Throws<FactsPdfException>(() => Writer(maxDepth).ToBytes(html))!;

    [Test]
    public void NestingFloodStopsAtTheFirstElementPastTheLimit()
    {
        var html = Repeat("<div>", 100_000) + "x";
        try { Writer(16).ToBytes(html); } catch (FactsPdfException) { } // warm up
        var before = GC.GetAllocatedBytesForCurrentThread();
        var error = Assert.Throws<FactsPdfException>(() => Writer(16).ToBytes(html))!;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        TestContext.Out.WriteLine($"100,000 nested divs over MaxDepth 16: {allocated / 1024} KiB");
        Assert.That(error.Code, Is.EqualTo("FPDF1003"));
        Assert.That(error.SourceOffset, Is.EqualTo(16 * "<div>".Length), "the 17th div");
        // Before R1c1 the whole page was tokenized first: 3,921 KiB on main 7aa46f3. What remains is the token
        // list sized up front from the page (one 8-byte slot per tag).
        Assert.That(allocated, Is.LessThan(1_200_000));
    }

    private static IEnumerable<TestCaseData> AtTheLimit()
    {
        // Each document nests exactly `depth` elements at its deepest point; the reader renders it
        // at MaxDepth = depth and fails at depth - 1, so the early check must agree on both sides.
        yield return new TestCaseData("<div><div><p>x</p></div></div>", 3).SetName("{m}(plain nesting)");
        yield return new TestCaseData("<div><p>a<div><p>b</p></div></div>", 3).SetName("{m}(block closes an open p)");
        yield return new TestCaseData("<div><p>a</div><div><div><p>b</p></div></div>", 3).SetName("{m}(container end closes an open p)");
        yield return new TestCaseData("<ul><li>a<li>b<li><span>c</span></ul>", 3).SetName("{m}(li closes the previous li)");
        yield return new TestCaseData("<ul><li>a</ul><div><div><p>b</p></div></div>", 3).SetName("{m}(list end closes an open li)");
        yield return new TestCaseData("<html><head><style>p{color:red}</style><title>t</title></head><body><p>x</p></body></html>", 3)
            .SetName("{m}(head, title and style)");
        yield return new TestCaseData("<div><p><span><span>x</span></span></p></div>", 4).SetName("{m}(inline nesting)");
        yield return new TestCaseData("<div><hr><br><p>x<br/></p></div>", 2).SetName("{m}(void and self-closed void tags)");
    }

    [TestCaseSource(nameof(AtTheLimit))]
    public void RendersAtExactlyMaxDepthAndFailsOneBelow(string html, int depth)
    {
        Assert.That(Writer(depth).ToBytes(html), Is.Not.Empty);
        Assert.That(Fails(html, depth - 1).Code, Is.EqualTo("FPDF1003"));
    }

    [Test]
    public void DeepNestingInsideATableIsStillCaughtByTheReader()
    {
        var html = "<table style='width:80pt'><colgroup><col style='width:80pt'></colgroup><tbody><tr><td>" + Repeat("<span>", 20) + "x" + Repeat("</span>", 20) + "</td></tr></tbody></table>";
        Assert.That(Writer(64).ToBytes(html), Is.Not.Empty);
        Assert.That(Fails(html, 8).Code, Is.EqualTo("FPDF1003"));
    }

    [Test]
    public void AnUnsupportedElementKeepsItsOwnCode()
    {
        var html = "<blink>" + Repeat("<div>", 40) + "x";
        Assert.That(Fails(html, 16).Code, Is.EqualTo("FPDF1102"));
    }

    [Test]
    public void AnUnmatchedEndTagKeepsItsOwnCode()
    {
        var html = "<div></span>" + Repeat("<div>", 40) + "x";
        var error = Fails(html, 16);
        Assert.That(error.Code, Is.EqualTo("FPDF1101"));
        Assert.That(error.SourceOffset, Is.EqualTo("<div>".Length));
    }

    [Test]
    public void ANonVoidSelfClosedTagKeepsItsOwnCode()
    {
        var html = "<div/>" + Repeat("<div>", 40) + "x";
        Assert.That(Fails(html, 16).Code, Is.EqualTo("FPDF1101"));
    }
}
