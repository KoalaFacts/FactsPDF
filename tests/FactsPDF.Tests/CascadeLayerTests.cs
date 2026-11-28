using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>
/// @layer: named, anonymous and statement forms order the cascade as Chrome orders it. Unlayered normal
/// declarations beat every layer whatever their specificity; later layers beat earlier ones; sublayers
/// rank below their parent's own declarations; important declarations reverse all of that; the style
/// attribute beats layers; revert-layer rolls back to the layers below the declaration's own. Each case
/// renders byte-identical to the page holding only the winning declaration.
/// </summary>
[TestFixture]
public sealed class CascadeLayerTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 400, Margin = 20 };

    private static byte[] Pdf(string html)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, Page);
        return output.ToArray();
    }

    private const string Red = "#c00", Blue = "#00c", Green = "#080";

    // The page with the text in the winning colour and nothing else.
    private static string Winner(string color) => $"<style>p{{color:{color}}}</style><p class=s>T</p>";

    private static string Doc(string css, string attributes = "") => $"<style>{css}</style><p class=s id=s {attributes}>T</p>";

    [TestCase($"@layer a{{#s{{color:{Red}}}}} p{{color:{Blue}}}", Blue, Description = "unlayered wins over a layer's id selector")]
    [TestCase($"p{{color:{Blue}}} @layer a{{#s{{color:{Red}}}}}", Blue, Description = "whatever the order")]
    [TestCase($"@layer a{{.s{{color:{Red}}}}} @layer b{{p{{color:{Blue}}}}}", Blue, Description = "a later layer wins whatever the specificity: b is later")]
    [TestCase($"@layer a, b; @layer b{{.s{{color:{Red}}}}} @layer a{{.s{{color:{Blue}}}}}", Red, Description = "a statement fixes the order before the blocks")]
    [TestCase($"@layer b{{.s{{color:{Red}}}}} @layer a{{.s{{color:{Blue}}}}}", Blue, Description = "without a statement the first appearance fixes the order")]
    [TestCase($"@layer a{{.s{{color:{Red}}}}} @layer b{{.s{{color:{Blue}}}}} @layer a{{.s{{color:{Green}}}}}", Blue, Description = "reopening a layer keeps its place")]
    [TestCase($"@layer a{{@layer x{{.s{{color:{Red}}}}} .s{{color:{Blue}}}}}", Blue, Description = "a layer's own declarations win over its sublayers")]
    [TestCase($"@layer a{{@layer x{{.s{{color:{Red}}}}}}} @layer b{{.s{{color:{Blue}}}}}", Blue, Description = "sublayers rank with their top-level layer")]
    [TestCase($"@layer a.b{{.s{{color:{Red}}}}} @layer a{{.s{{color:{Blue}}}}}", Blue, Description = "a dotted name names a sublayer")]
    [TestCase($"@layer a{{.s{{color:{Red}}}}} @layer A{{.s{{color:{Blue}}}}}", Blue, Description = "layer names are case-sensitive")]
    [TestCase($"@layer{{.s{{color:{Red}}}}} @layer{{.s{{color:{Blue}}}}}", Blue, Description = "anonymous layers are distinct, in order")]
    [TestCase($"@layer a{{.s{{color:{Red}}}}} @media print{{@layer b{{.s{{color:{Blue}}}}}}}", Blue, Description = "layers inside @media print")]
    [TestCase($"@layer a{{@media print{{.s{{color:{Blue}}}}}}} @layer a{{p{{color:{Red}}}}}", Blue, Description = "@media print inside a layer; the more specific class wins within it")]
    [TestCase($"@layer a{{.s{{color:{Red}}}}} .s{{color:{Blue}}}", Blue)]
    [TestCase($"@layer a;@layer b;@layer a{{.s{{color:{Red}}}}}@layer b{{.s{{color:{Blue}}}}}", Blue)]
    public void NormalDeclarationsFollowTheLayerOrder(string css, string winner)
        => Assert.That(Pdf(Doc(css)), Is.EqualTo(Pdf(Winner(winner))));

    [TestCase($"@layer a, b; @layer a{{.s{{color:{Red}!important}}}} @layer b{{.s{{color:{Blue}!important}}}}", Red, Description = "the earliest layer wins for important declarations")]
    [TestCase($"@layer a{{.s{{color:{Red}!important}}}} .s{{color:{Blue}!important}}", Red, Description = "a layered important beats an unlayered important")]
    [TestCase($"@layer a{{.s{{color:{Red}!important}}}} .s{{color:{Blue}}}", Red, Description = "important beats normal")]
    [TestCase($"@layer a{{@layer x{{.s{{color:{Red}!important}}}} .s{{color:{Blue}!important}}}}", Red, Description = "sublayers win for important declarations")]
    [TestCase($"@layer a{{.s{{color:{Red}}}}} @layer b{{.s{{color:{Blue}!important}}}} .s{{color:{Green}}}", Blue)]
    public void ImportantDeclarationsReverseTheLayerOrder(string css, string winner)
        => Assert.That(Pdf(Doc(css)), Is.EqualTo(Pdf(Winner(winner))));

    [Test]
    public void TheStyleAttributeBeatsLayersButNotAnImportantLayerDeclaration()
    {
        Assert.That(Pdf(Doc($"@layer a{{.s{{color:{Red}}}}}", $"style='color:{Green}'")), Is.EqualTo(Pdf(Winner(Green))));
        Assert.That(Pdf(Doc($".s{{color:{Red}}}", $"style='color:{Green}'")), Is.EqualTo(Pdf(Winner(Green))));
        Assert.That(Pdf(Doc($"@layer a{{.s{{color:{Red}!important}}}}", $"style='color:{Green}'")), Is.EqualTo(Pdf(Winner(Red))));
        Assert.That(Pdf(Doc($"@layer a{{.s{{color:{Red}}}}}", $"style='color:{Green}!important'")), Is.EqualTo(Pdf(Winner(Green))));
    }

    [TestCase($"@layer a{{.s{{color:{Red}}}}} @layer b{{.s{{color:{Blue}}}}} @layer c{{.s{{color:revert-layer}}}}", Blue, Description = "rolls back to the layer below")]
    [TestCase($"@layer a{{.s{{color:{Red}}}}} @layer b{{.s{{color:revert-layer}}}} @layer c{{.s{{color:revert-layer}}}}", Red, Description = "chains down")]
    [TestCase($"@layer a{{.s{{color:{Red}}}}} .s{{color:revert-layer}}", Red, Description = "an unlayered declaration rolls back to the last layer")]
    [TestCase($"@layer a{{.s{{color:{Red}}}}} @layer b{{.s{{color:{Blue}}}}} .s{{color:revert-layer}}", Blue)]
    [TestCase($"@layer a{{.s{{color:{Red}}}}} @layer b{{.s{{color:{Blue}}}}} @layer c{{.s{{color:{Green}}} .s.z{{color:revert-layer}}}}", Green, Description = "other elements are untouched")]
    public void RevertLayerRollsBackToTheLayersBelow(string css, string winner)
        => Assert.That(Pdf(Doc(css)), Is.EqualTo(Pdf(Winner(winner))));

    [TestCase("@layer a{.s{color:revert-layer}}")]
    [TestCase(".s{color:revert-layer}")]
    public void RevertLayerWithNothingBelowComputesLikeTheUserAgentStyle(string css)
        => Assert.That(Pdf(Doc(css)), Is.EqualTo(Pdf("<p class=s>T</p>")));

    [Test]
    public void RevertLayerFromTheStyleAttributeRollsBackThroughTheLayers()
    {
        // The style attribute sorts with the unlayered styles, so it rolls back to the last layer.
        Assert.That(Pdf(Doc($"@layer a{{.s{{color:{Red}}}}} @layer b{{.s{{color:{Blue}}}}}", "style='color:revert-layer'")), Is.EqualTo(Pdf(Winner(Blue))));
    }

    [Test]
    public void ARevertedShorthandRollsEveryLonghandBack()
        => Assert.That(Pdf($"<style>@layer a{{div{{padding:6pt;background:#cde}}}} div{{padding:revert-layer}}</style><div>T</div>"),
            Is.EqualTo(Pdf("<style>div{padding:6pt;background:#cde}</style><div>T</div>")));

    [Test]
    public void LayersApplyToTheNestedAndSiblingSelectorsToo()
        => Assert.That(Pdf($"<style>@layer a{{div + p{{color:{Red}}}}} p{{color:{Blue}}}</style><div>D</div><p>T</p>"),
            Is.EqualTo(Pdf($"<style>p{{color:{Blue}}}</style><div>D</div><p>T</p>")));

    [Test]
    public void ALayerStatementMayFollowTheRulesItOrders()
    {
        // The layer order is fixed when the layer first appears anywhere, in document order across style elements:
        // b exists before the statement, so the statement only adds a after it and a wins.
        Assert.That(Pdf($"<style>@layer b{{.s{{color:{Red}}}}}</style><style>@layer a, b; @layer a{{.s{{color:{Blue}}}}}</style><p class=s>T</p>"),
            Is.EqualTo(Pdf(Winner(Blue))));
    }

    [TestCase("@layer a, b {p{color:red}}")]
    [TestCase("@layer ;")]
    [TestCase("@layer a b;")]
    [TestCase("@layer a..b{p{color:red}}")]
    [TestCase("@layer 1a{p{color:red}}")]
    public void MalformedLayerRulesFailLikeInvalidCssAndAreDroppedWhenNonStrict(string css)
    {
        var html = $"<style>{css} p{{color:{Blue}}}</style><p>T</p>";
        Assert.That(Assert.Throws<FactsPdfException>(() => Pdf(html))!.Code, Is.EqualTo("FPDF1204"));
        Assert.That(FactsPDF.Pdf.Write().WithPage(p => p.Size(300, 400).Margin(20)).WithStrict(false).ToBytes(html),
            Is.EqualTo(FactsPDF.Pdf.Write().WithPage(p => p.Size(300, 400).Margin(20)).ToBytes($"<style>p{{color:{Blue}}}</style><p>T</p>")));
    }

    [Test]
    public void ADocumentWithoutLayersCascadesAsBefore()
    {
        Assert.That(Pdf($"<style>p{{color:{Red}}}.s{{color:{Blue}}}</style><p class=s>T</p>"), Is.EqualTo(Pdf(Winner(Blue))));
        Assert.That(Pdf($"<style>.s{{color:{Blue}}}p{{color:{Red}}}</style><p class=s>T</p>"), Is.EqualTo(Pdf(Winner(Blue))));
        Assert.That(Pdf($"<style>p{{color:{Red}}}p{{color:{Blue}}}</style><p>T</p>"), Is.EqualTo(Pdf($"<style>p{{color:{Blue}}}</style><p>T</p>")));
    }
}
