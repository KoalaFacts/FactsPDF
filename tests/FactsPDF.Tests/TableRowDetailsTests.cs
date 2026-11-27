using NUnit.Framework;

namespace FactsPDF.Tests;

// th cells in body and footer rows, td cells in the header row, and row / row group backgrounds.
public sealed class TableRowDetailsTests
{
    private const string Columns = "<colgroup><col style='width:60pt'><col style='width:60pt'></colgroup>";

    private static string Table(string style, string body, string head = "", string foot = "")
        => $"<style>{style}</style><table style='width:120pt;font-size:10pt'>{Columns}{head}<tbody>{body}</tbody>{foot}</table>";

    [Test]
    public void LabelValueRowsMixThAndTdCells()
    {
        var tree = HtmlDocumentReader.ReadTree(Table("", "<tr><th>Ref</th><td>A1</td></tr>",
            head: "<thead><tr><td>Key</td><th>Value</th></tr></thead>", foot: "<tfoot><tr><th>Sum</th><td>9</td></tr></tfoot>"), new(), default);
        var paragraphs = BlockTreeTraversal.Paragraphs(tree, default).ToArray();
        Assert.That(paragraphs.Select(p => p.Runs[0].Text), Is.EqualTo(new[] { "Key", "Value", "Ref", "A1", "Sum", "9" }));
        Assert.That(paragraphs.Select(p => p.Style.Bold), Is.EqualTo(new[] { false, true, true, false, true, false }));
    }

    [Test]
    public void BodyThNeedsABoldFontLikeAHeaderTh()
        => TableStructureTests.Fails(Table("", "<tr><th>Ref</th><td>A1</td></tr>"), "FPDF1507");

    [Test]
    public void BodyThRendersLikeABoldTd()
        => Assert.That(TableLayoutTests.Pdf(Table("th{font-weight:normal;text-align:left}", "<tr><th>Ref</th><td>A1</td></tr>")),
            Is.EqualTo(TableLayoutTests.Pdf(Table("", "<tr><td>Ref</td><td>A1</td></tr>"))));

    [TestCase("tbody{background-color:#eef3f7}", "<tr><td>A</td><td>B</td></tr><tr><td>C</td><td style='background-color:#c00'>D</td></tr>",
        "<tr><td style='background-color:#eef3f7'>A</td><td style='background-color:#eef3f7'>B</td></tr><tr><td style='background-color:#eef3f7'>C</td><td style='background-color:#c00'>D</td></tr>")]
    [TestCase("tbody{background:#eef3f7}tr.x{background-color:#0c0}", "<tr class=x><td>A</td><td>B</td></tr><tr><td>C</td><td>D</td></tr>",
        "<tr><td style='background-color:#0c0'>A</td><td style='background-color:#0c0'>B</td></tr><tr><td style='background-color:#eef3f7'>C</td><td style='background-color:#eef3f7'>D</td></tr>")]
    [TestCase("tr{background-color:#eef3f7}td{border:1pt solid #333;padding:2pt}", "<tr><td>A</td><td></td></tr>",
        "<tr><td style='background-color:#eef3f7'>A</td><td style='background-color:#eef3f7'></td></tr>")]
    public void RowAndGroupBackgroundsPaintUnderCellsWithoutTheirOwn(string style, string body, string expected)
        => Assert.That(TableLayoutTests.Pdf(Table(style, body)), Is.EqualTo(TableLayoutTests.Pdf(Table(style.Contains("td{", StringComparison.Ordinal)
            ? style[style.IndexOf("td{", StringComparison.Ordinal)..] : "", expected))));

    [Test]
    public void HeaderGroupBackgroundRepeatsWithTheHeader()
    {
        var rows = string.Concat(Enumerable.Range(0, 80).Select(i => $"<tr><td>R{i}</td><td>V</td></tr>"));
        var pages = TableLayoutTests.Layout(Table("thead{background-color:#eef3f7}th{font-weight:normal}", rows,
            head: "<thead><tr><th>Id</th><th>Value</th></tr></thead>"));
        Assert.That(pages, Has.Count.GreaterThan(1));
        foreach (var page in pages)
            Assert.That(page.PaintBoxes.Count(box => box.Style.BackgroundColor is { } color && color.G > 0.9 && color.B > 0.9 && color.R > 0.9), Is.EqualTo(2));
    }

    [TestCase("tr{padding:2pt}", "FPDF1201")]
    [TestCase("tr{border:1pt solid red}", "FPDF1201")]
    [TestCase("tbody{border-top:1pt solid red}", "FPDF1201")]
    [TestCase("tr{background-color:#eee}td{border-radius:3pt}", "FPDF1201")]
    public void OtherRowAndGroupBoxStylesStillFail(string style, string code)
        => TableStructureTests.Fails(Table(style, "<tr><td>A</td><td>B</td></tr>"), code);
}
