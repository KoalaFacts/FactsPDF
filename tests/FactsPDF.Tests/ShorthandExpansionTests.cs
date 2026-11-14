using NUnit.Framework;

namespace FactsPDF.Tests;

// The box shorthands (padding, border-width, border-style, border-color) and the border
// shorthands index shared side and edge arrays instead of building a copy of their terms, a
// four-side expansion and a property or edge array for every declaration they compile. Their
// allocation is guarded by the 200-row table budget in AllocationBudgetTests; these tests pin
// that the expansion, its bytes and its error locations are unchanged.
[TestFixture]
public sealed class ShorthandExpansionTests
{
    // Each shorthand renders exactly the bytes of its longhands, for every arity.
    [TestCase("padding:1pt", "padding-top:1pt;padding-right:1pt;padding-bottom:1pt;padding-left:1pt")]
    [TestCase("padding:1pt 2pt", "padding-top:1pt;padding-right:2pt;padding-bottom:1pt;padding-left:2pt")]
    [TestCase("padding:1pt 2pt 3pt", "padding-top:1pt;padding-right:2pt;padding-bottom:3pt;padding-left:2pt")]
    [TestCase("padding:1pt 2pt 3pt 4pt", "padding-top:1pt;padding-right:2pt;padding-bottom:3pt;padding-left:4pt")]
    [TestCase("border-style:solid;border-width:1pt 2pt 3pt;border-color:#f00 #0f0",
        "border-top-style:solid;border-right-style:solid;border-bottom-style:solid;border-left-style:solid;"
        + "border-top-width:1pt;border-right-width:2pt;border-bottom-width:3pt;border-left-width:2pt;"
        + "border-top-color:#f00;border-right-color:#0f0;border-bottom-color:#f00;border-left-color:#0f0")]
    [TestCase("border:1pt solid #00f", "border-top:1pt solid #00f;border-right:1pt solid #00f;border-bottom:1pt solid #00f;border-left:1pt solid #00f")]
    [TestCase("border-inline:2pt dashed #0f0", "border-left:2pt dashed #0f0;border-right:2pt dashed #0f0")]
    [TestCase("border-block:2pt dotted #0f0", "border-top:2pt dotted #0f0;border-bottom:2pt dotted #0f0")]
    [TestCase("border-inline-start:1pt solid", "border-left-width:1pt;border-left-style:solid;border-left-color:currentcolor")]
    [TestCase("border-block-end:1pt solid", "border-bottom-width:1pt;border-bottom-style:solid;border-bottom-color:currentcolor")]
    public void ShorthandsRenderLikeTheirLonghands(string shorthand, string longhands)
    {
        static byte[] Render(string style) => Pdf.Write().ToBytes($"<div style=\"{style}\">Box</div>");
        Assert.That(Render(shorthand), Is.EqualTo(Render(longhands)));
    }

    // The source location of a side's failure is still the term that side came from.
    [TestCase("padding:1pt -2pt", 24)]
    [TestCase("padding:1pt 2pt 3pt -4pt", 32)]
    [TestCase("border-width:1pt 2pt banana", 33)]
    [TestCase("border:1pt solid solid", 29)]
    public void SideFailuresPointAtTheirTerm(string style, int offset)
    {
        var error = Assert.Throws<FactsPdfException>(() => Pdf.Write().ToBytes($"<div style=\"{style}\">Box</div>"));
        Assert.That(error!.Code, Is.EqualTo("FPDF1202"));
        Assert.That(error.SourceOffset, Is.EqualTo(offset));
    }
}
