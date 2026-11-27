using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// The CSS-wide keywords revert and revert-layer (no cascade layers exist) roll the author styles back
/// for a property: the element renders as if no author declaration had been written for it, so the
/// user-agent defaults (heading sizes, paragraph gaps) or inheritance apply. Like Chrome 158, revert
/// also drops the presentational hints of that property (the list type attribute for list-style-type,
/// the hidden attribute for display), while revert-layer keeps them. Accepted in both modes; also as a
/// var() fallback or substituted value.
/// </summary>
[TestFixture]
public sealed class RevertKeywordTests
{
    private static readonly PdfWriter Writer = Pdf.Write().WithPage(p => p.Size(300, 400).Margin(20));

    private static string Document(string css, string body = "<h1>Invoice</h1><div class='panel'><p class='lead'>Paid in full</p></div><p>Thanks</p>") =>
        $"<!doctype html><html><head><style>{css}</style></head><body>{body}</body></html>";

    [TestCase("h1{font-size:30pt}h1{font-size:revert}", "")]
    [TestCase("p{margin-bottom:20pt}p{margin-bottom:revert}", "")]
    [TestCase("p{margin:20pt}p{margin:revert}", "")]
    [TestCase("p{margin:20pt}p{margin:revert-layer}", "")]
    [TestCase("body{color:#2e7d32}p{color:#c62828}p{color:revert}", "body{color:#2e7d32}")]
    [TestCase("p{color:#c62828}p{color:revert !important}p{color:#1565c0}", "")]
    [TestCase(".panel{border:1pt solid #333;padding:4pt}.panel{border:revert;padding:revert}", "")]
    [TestCase("p{color:#c62828}p{color:var(--missing, revert)}", "")]
    [TestCase(":root{--r:revert}p{color:#c62828}p{color:var(--r, revert)}", "")]
    public void RevertRendersAsIfTheAuthorDeclaredNothing(string css, string withoutIt)
    {
        var expected = Writer.ToBytes(Document(withoutIt));
        Assert.That(Writer.ToBytes(Document(css)), Is.EqualTo(expected), css);
        Assert.That(Writer.WithStrict(false).ToBytes(Document(css)), Is.EqualTo(expected), css);
    }

    [Test]
    public void RevertIsNotCheckedAgainstWhatTheElementAccepts()
    {
        // A margin on inline text fails, but revert asks for nothing to be applied.
        Assert.That(Writer.ToBytes(Document("span{margin-top:revert;padding:revert;display:revert}", "<p>a <span>b</span></p>")),
            Is.EqualTo(Writer.ToBytes(Document("", "<p>a <span>b</span></p>"))));
    }

    [Test]
    public void RevertDiscardsTheListTypeHint()
    {
        Assert.That(Writer.ToBytes(Document("ol{list-style-type:revert}", "<ol type='a'><li>x</li><li>y</li></ol>")),
            Is.EqualTo(Writer.ToBytes(Document("", "<ol><li>x</li><li>y</li></ol>"))));
    }

    // Chrome 158: getComputedStyle reports lower-alpha and none for these.
    [Test]
    public void RevertLayerKeepsThePresentationalHints()
    {
        Assert.That(Writer.ToBytes(Document("ol{list-style-type:revert-layer}", "<ol type='a'><li>x</li><li>y</li></ol>")),
            Is.EqualTo(Writer.ToBytes(Document("", "<ol type='a'><li>x</li><li>y</li></ol>"))));
        Assert.That(Writer.ToBytes(Document("div{display:revert-layer}", "<p>a</p><div hidden>gone</div>")),
            Is.EqualTo(Writer.ToBytes(Document("", "<p>a</p>"))));
    }

    [Test]
    public void RevertedDisplayShowsAHiddenBlock()
    {
        Assert.That(Writer.ToBytes(Document("div{display:revert}", "<div hidden>shown</div>")),
            Is.EqualTo(Writer.ToBytes(Document("", "<div>shown</div>"))));
    }

    [Test]
    public void RevertInAStyleAttributeAndOnInitialOnlyProperties()
    {
        Assert.That(Writer.ToBytes(Document("p{color:#c62828}", "<p style='color:revert;position:revert;cursor:revert-layer'>x</p>")),
            Is.EqualTo(Writer.ToBytes(Document("", "<p>x</p>"))));
    }
}
