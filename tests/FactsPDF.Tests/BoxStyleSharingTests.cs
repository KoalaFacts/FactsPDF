using NUnit.Framework;

namespace FactsPDF.Tests;

// CSS computing assigns declarations into one private BoxStyle copy per element. Shared boxes —
// the initial box and every parent's computed box — must come out of a conversion unchanged.
[TestFixture]
[NonParallelizable]
public sealed class BoxStyleSharingTests
{
    [Test]
    public void ComputingStylesNeverAssignsTheSharedInitialBox()
    {
        const string Html = "<style>div{padding:4pt;border:1pt solid red;margin:2pt auto;width:50%}"
            + "td{vertical-align:middle;padding:inherit}</style><div><ul style=\"list-style:inside square\"><li>A</li></ul>"
            + "<table style=\"width:100pt\"><colgroup><col style=\"width:100pt\"></colgroup><tbody><tr><td>B</td></tr></tbody></table>"
            + "<div style=\"padding:initial;border-top:unset;background:#eee;outline:1pt solid blue\">C</div></div>";
        Pdf.Write().ToBytes(Html);
        Assert.That(BoxStyle.Initial, Is.EqualTo(new BoxStyle()));
    }

    [Test]
    public void ChildDeclarationsDoNotChangeTheParentsComputedBox()
    {
        var document = HtmlDocumentReader.ReadTree(
            "<div style=\"padding:4pt\"><div style=\"padding:inherit;padding-left:9pt;border:1pt solid red\">A</div></div>",
            new PdfOptions(), CancellationToken.None);
        var outer = (BlockNode)document.Children[0];
        var inner = (BlockNode)outer.Children[0];
        Assert.That(outer.BoxStyle.PaddingLeft.Value, Is.EqualTo(4));
        Assert.That(outer.BoxStyle.BorderTop.EffectiveWidth, Is.Zero);
        Assert.That(inner.BoxStyle.PaddingLeft.Value, Is.EqualTo(9));
        Assert.That(inner.BoxStyle.PaddingTop.Value, Is.EqualTo(4));
        Assert.That(inner.BoxStyle.BorderTop.EffectiveWidth, Is.EqualTo(1));
    }
}
