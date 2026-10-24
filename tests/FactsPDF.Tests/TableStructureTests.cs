using NUnit.Framework;

namespace FactsPDF.Tests;

public sealed class TableStructureTests
{
    internal const string Columns = "<colgroup><col style='width:100%'></colgroup>";
    internal const string One = "<table>" + Columns + "<tbody><tr><td>A</td></tr></tbody></table>";
    private static DocumentRoot Read(string html, PdfOptions? options = null)
        => HtmlDocumentReader.ReadTree(html, options ?? new(), default);

    [Test]
    public void StrictTableRetainsHeaderBodyInlineAndEmptyCellOrder()
    {
        var tree = Read("<p>Before</p><section><table><colgroup><col style='width:80pt'><col style='width:140pt'></colgroup>"
            + "<thead><tr><th>ID</th><th style='font-weight:normal'>Label</th></tr></thead>"
            + "<tbody><tr><td>A <i>B</i><br>C</td><td></td></tr></tbody></table></section><p>After</p>");
        var paragraphs = BlockTreeTraversal.Paragraphs(tree, default).ToArray();
        Assert.That(paragraphs, Has.Length.EqualTo(6));
        Assert.That(paragraphs.SelectMany(p => p.Runs).Select(r => r.Text),
            Is.EqualTo(new[] { "Before", "ID", "Label", "A ", "B", "", "C", "After" }));
        Assert.That(paragraphs[1].Style.Bold, Is.True);
        Assert.That(paragraphs[2].Style.Bold, Is.False);
        Assert.That(paragraphs[3].Runs[1].Style.Italic, Is.True);
        Assert.That(paragraphs[3].Runs[2].IsBreak, Is.True);
        Assert.That(paragraphs[4].Runs, Is.Empty);
    }

    [Test]
    public void TableSelectorsUseActualStructuralAncestryAndInheritedTextStyles()
    {
        var tree = Read("<style>table{font-size:10pt}table>tbody{font-weight:bold}tbody>tr{color:red}"
            + "tr>td{font-style:italic;text-align:right}td span{font-weight:normal}</style>"
            + One.Replace("<td>A", "<td><span>A</span>"));
        var cell = BlockTreeTraversal.Paragraphs(tree, default).Single();
        Assert.That(cell.Style.FontSize, Is.EqualTo(10));
        Assert.That(cell.Style.Bold && cell.Style.Italic, Is.True);
        Assert.That(cell.Style.Color, Is.EqualTo(new Rgb(1, 0, 0)));
        Assert.That(cell.Style.Alignment, Is.EqualTo(TextAlignment.Right));
        Assert.That(cell.Runs.Single().Style.Bold, Is.False);
    }

    [TestCase("<div>", "</div>")]
    [TestCase("<body>", "</body>")]
    [TestCase("<html><body>", "</body></html>")]
    public void TablesAreStandaloneBlocksInsideExistingContainers(string before, string after)
        => Assert.That(BlockTreeTraversal.Paragraphs(Read(before + One + after), default).Single().Runs.Single().Text, Is.EqualTo("A"));

    [Test]
    public void StructuralWhitespaceAndCommentsDoNotCreateCellText()
    {
        var html = "<table>\n<!-- c -->" + Columns + "\n<tbody>\n<tr> <td>A</td> </tr> </tbody> </table>";
        Assert.That(BlockTreeTraversal.Paragraphs(Read(html), default).Single().Runs.Single().Text, Is.EqualTo("A"));
    }

    [TestCase("<table></table>")]
    [TestCase("<table><tbody><tr><td>A</td></tr></tbody></table>")]
    [TestCase("<table><colgroup></colgroup><tbody><tr><td>A</td></tr></tbody></table>")]
    [TestCase("<table>" + Columns + "</table>")]
    [TestCase("<table>" + Columns + "<tbody></tbody></table>")]
    [TestCase("<table>" + Columns + "<tbody><tr></tr></tbody></table>")]
    [TestCase("<table>" + Columns + "<tbody><tr><td>A</td><td>B</td></tr></tbody></table>")]
    [TestCase("<table>" + Columns + "<tbody><tr><th>A</th></tr></tbody></table>")]
    [TestCase("<table>" + Columns + "<thead><tr><td>A</td></tr></thead><tbody><tr><td>B</td></tr></tbody></table>")]
    [TestCase("<table>" + Columns + "<thead></thead><tbody><tr><td>B</td></tr></tbody></table>")]
    [TestCase("<table>" + Columns + "<thead><tr><th>A</th></tr><tr><th>B</th></tr></thead><tbody><tr><td>C</td></tr></tbody></table>")]
    [TestCase("<table>" + Columns + Columns + "<tbody><tr><td>A</td></tr></tbody></table>")]
    [TestCase("<table>" + Columns + "<tbody><tr><td>A</td></tr></tbody><thead><tr><th>B</th></tr></thead></table>")]
    [TestCase("<table>" + Columns + "<tbody><tr><td>A</td></tr></tbody><tbody><tr><td>B</td></tr></tbody></table>")]
    [TestCase("<table>A" + Columns + "<tbody><tr><td>B</td></tr></tbody></table>")]
    [TestCase("<table>&#160;" + Columns + "<tbody><tr><td>B</td></tr></tbody></table>")]
    [TestCase("<table>" + Columns + "<tbody>A<tr><td>B</td></tr></tbody></table>")]
    [TestCase("<table>" + Columns + "<tbody><tr>A<td>B</td></tr></tbody></table>")]
    [TestCase("<table>" + Columns + "<style>td{color:red}</style><tbody><tr><td>A</td></tr></tbody></table>")]
    [TestCase("<table>" + Columns + "<tbody><tr><td><style>td{color:red}</style>A</td></tr></tbody></table>")]
    [TestCase("<table>" + Columns + "<tbody><tr><td>A</tbody></table>")]
    [TestCase("<table>" + Columns + "<tbody><tr><td>A")]
    [TestCase("<table/>")]
    [TestCase("<colgroup><col style='width:10pt'></colgroup>")]
    [TestCase("<col style='width:10pt'>")]
    [TestCase("<thead><tr><th>A</th></tr></thead>")]
    [TestCase("<tbody><tr><td>A</td></tr></tbody>")]
    [TestCase("<tr><td>A</td></tr>")]
    [TestCase("<td>A</td>")]
    [TestCase("<th>A</th>")]
    public void MalformedStructureFailsBeforeDestinationChanges(string html) => Fails(html, "FPDF1101");

    [TestCase("p")][TestCase("span")][TestCase("h1")]
    public void TableDoesNotRepairOrNestInsideTextElements(string tag)
        => Fails($"<{tag}>" + One + $"</{tag}>", "FPDF1101");

    [Test]
    public void TableCannotBeInsideAListItem() => Fails("<ol><li>" + One + "</li></ol>", "FPDF1101");

    [TestCase("<div>A</div>")][TestCase("<ul><li>A</li></ul>")]
    [TestCase(One)]
    public void CellsRejectDeferredBlockContent(string content)
        => Fails(One.Replace("<td>A", "<td>" + content), "FPDF1101");

    [Test]
    public void SeventeenthColumnFailsAtItsStart()
        => Fails("<table><colgroup>" + string.Concat(Enumerable.Repeat("<col style='width:10pt'>", 17)) + "</colgroup></table>", "FPDF1101");

    [TestCase("rowspan='1'")][TestCase("width='10'")]
    public void CellAttributesDoNotSilentlyExpandTheSubset(string attribute)
        => Fails(One.Replace("<td>", "<td " + attribute + ">"), "FPDF1103");

    [TestCase("col", "color:red")][TestCase("col", "padding:1pt")]
    [TestCase("colgroup", "width:100pt")][TestCase("tbody", "background:red")]
    [TestCase("tr", "border:1pt solid red")][TestCase("td", "width:100pt")]
    [TestCase("td", "margin-top:1pt")][TestCase("td", "break-before:page")]
    [TestCase("table", "margin-top:1pt")][TestCase("table", "table-layout:fixed")]
    [TestCase("table", "border-collapse:collapse")][TestCase("table", "border-spacing:0")]
    public void TableCssApplicabilityRejectsMatchedDeclarations(string selector, string declaration)
        => Fails($"<style>{selector}{{{declaration}}}</style>" + One, "FPDF1201");

    [Test]
    public void LosingInapplicableDeclarationStillFails()
        => Fails("<style>td{width:10pt}td{width:20pt!important}</style>" + One, "FPDF1201");

    [Test]
    public void CssPreflightStillPrecedesMalformedTableStructure()
        => Fails("<table><style>.never{not-a-property:x}</style></table>", "FPDF1201");

    [Test]
    public void TableTagsCountTowardsExistingElementAndDepthBudgets()
    {
        Assert.DoesNotThrow(() => Read(One, new() { MaxElements = 6, MaxDepth = 4 }));
        Fails(One, "FPDF1002", new() { MaxElements = 5 });
        Fails(One, "FPDF1003", new() { MaxDepth = 3 });
    }

    internal static void Fails(string html, string code, PdfOptions? options = null)
    {
        using var output = new MemoryStream(); output.Write("sentinel"u8); output.Position = 3;
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, options));
        Assert.That(error!.Code, Is.EqualTo(code));
        Assert.That(error.SourceOffset, Is.GreaterThanOrEqualTo(0));
        Assert.That(output.ToArray(), Is.EqualTo("sentinel"u8.ToArray()));
        Assert.That(output.Position, Is.EqualTo(3));
    }
}
