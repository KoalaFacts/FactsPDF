using NUnit.Framework;

namespace FactsPDF.Tests;

// CSS computing applies an element's text declarations to at most one private TextStyle copy and
// shares the parent's style when nothing changes. Shared styles — the root style, every parent's
// computed style and the stylesheet validation style — must come out of a conversion unchanged.
[TestFixture]
[NonParallelizable]
public sealed class TextStyleSharingTests
{
    private static DocumentRoot Read(string html) =>
        HtmlDocumentReader.ReadTree(html, new PdfOptions(), CancellationToken.None);

    private static IEnumerable<BlockChild> Descendants(List<BlockChild> children)
    {
        foreach (var child in children)
        {
            yield return child;
            if (child is BlockNode block)
                foreach (var nested in Descendants(block.Children)) yield return nested;
        }
    }

    private static TableNode Table(DocumentRoot document) => Descendants(document.Children).OfType<TableNode>().Single();

    private static List<Paragraph> Paragraphs(string html) =>
        HtmlDocumentReader.Read(html, new PdfOptions(), CancellationToken.None);

    [Test]
    public void ElementsWithoutTextChangesShareTheirParentsStyle()
    {
        var table = Table(Read("<table style=\"width:100pt\"><colgroup><col style=\"width:50pt\"><col style=\"width:50pt\"></colgroup>"
            + "<tbody><tr><td style=\"padding:3pt;border:0.5pt solid #789\">A</td><td>B</td></tr></tbody></table>"));
        var cells = table.Rows[0].Cells;
        Assert.That(cells[0].Style, Is.SameAs(table.Style));
        Assert.That(cells[1].Style, Is.SameAs(table.Style));
    }

    [Test]
    public void ElementsWithTextChangesGetTheirOwnStyleAndLeaveTheParentUnchanged()
    {
        var table = Table(Read("<table style=\"width:100pt;color:#123456\"><colgroup><col style=\"width:100pt\"></colgroup>"
            + "<thead><tr><th style=\"color:red;font-size:15pt;text-align:right;font-style:italic\">A</th></tr></thead>"
            + "<tbody><tr><td>B</td></tr></tbody></table>"));
        var cell = table.Rows[0].Cells[0];
        Assert.That(cell.Style, Is.Not.SameAs(table.Style));
        Assert.That(cell.Style.Bold, Is.True);
        Assert.That(cell.Style.Italic, Is.True);
        Assert.That(cell.Style.FontSize, Is.EqualTo(15));
        Assert.That(cell.Style.Alignment, Is.EqualTo(TextAlignment.Right));
        Assert.That(cell.Style.Color, Is.EqualTo(new Rgb(1, 0, 0)));
        Assert.That(table.Style.Bold, Is.False);
        Assert.That(table.Style.Italic, Is.False);
        Assert.That(table.Style.FontSize, Is.EqualTo(12));
        Assert.That(table.Style.Alignment, Is.EqualTo(TextAlignment.Left));
        Assert.That(table.Style.Color, Is.EqualTo(new Rgb(0x12 / 255d, 0x34 / 255d, 0x56 / 255d)));
    }

    [Test]
    public void TagDefaultsAndAuthorDeclarationsCombineOnOneCopy()
    {
        var runs = Paragraphs("<p>x<a href=\"https://example.com/\" style=\"color:green;text-decoration:none\">link</a>"
            + "<small style=\"font-weight:bold\">s</small></p>")[0].Runs;
        var link = runs.Single(r => r.Text == "link").Style;
        var small = runs.Single(r => r.Text == "s").Style;
        var paragraph = runs.Single(r => r.Text == "x").Style;
        Assert.That(link.LinkUri, Is.EqualTo("https://example.com/"));
        Assert.That(link.Color, Is.EqualTo(new Rgb(0, 128 / 255d, 0)));
        Assert.That(link.Decoration, Is.EqualTo(TextDecoration.None));
        Assert.That(small.FontSize, Is.EqualTo(10).Within(1e-9));
        Assert.That(small.Bold, Is.True);
        Assert.That(paragraph.LinkUri, Is.Null);
        Assert.That(paragraph.Bold, Is.False);
        Assert.That(paragraph.FontSize, Is.EqualTo(12));
        Assert.That(paragraph.Decoration, Is.EqualTo(TextDecoration.None));
    }

    [Test]
    public void InheritAndInitialReadTheSharedStylesWithoutChangingThem()
    {
        var document = Read("<style>div{color:blue;font-size:20pt} p{color:inherit;font-size:initial;line-height:2}</style>"
            + "<div><p>A</p></div><p>B</p>");
        var panel = Descendants(document.Children).OfType<BlockNode>().Single(b => b.Name == "div");
        var paragraphs = Descendants(document.Children).OfType<Paragraph>().ToList();
        var inner = paragraphs.Single(p => p.Runs[0].Text == "A").Style;
        var outer = paragraphs.Single(p => p.Runs[0].Text == "B").Style;
        Assert.That(panel.Style.FontSize, Is.EqualTo(20));
        Assert.That(panel.Style.LineHeight, Is.EqualTo(1.2));
        Assert.That(inner.Color, Is.EqualTo(new Rgb(0, 0, 1)));
        Assert.That(inner.FontSize, Is.EqualTo(12));
        Assert.That(inner.LineHeight, Is.EqualTo(2));
        Assert.That(outer.Color, Is.EqualTo(new Rgb(0, 0, 0)));
        Assert.That(outer.FontSize, Is.EqualTo(12));
    }

    [Test]
    public void UnmatchedRulesValidateWithoutChangingLaterDocuments()
    {
        // Every stylesheet declaration is validated, even one that matches nothing, against a
        // shared validation style. Validation must not leave its values behind.
        Read("<style>nothing{font-weight:bold;font-style:italic;font-size:30pt;color:red;text-align:center}</style><p>A</p>");
        var style = Paragraphs("<p>B</p>")[0].Style;
        Assert.That(style.Bold, Is.False);
        Assert.That(style.Italic, Is.False);
        Assert.That(style.FontSize, Is.EqualTo(12));
        Assert.That(style.Color, Is.EqualTo(new Rgb(0, 0, 0)));
        Assert.That(style.Alignment, Is.EqualTo(TextAlignment.Left));
    }

    [Test]
    public void SiblingsWithTheSameTagDefaultsShareOneStyle()
    {
        // Each p differs from body only by its tag defaults (an 8pt bottom margin), so plain sibling
        // paragraphs share one defaults copy instead of making one each.
        var paragraphs = Paragraphs("<p>A</p><p>B</p><div><p>C</p></div><p>D</p>");
        Assert.That(paragraphs[1].Style, Is.SameAs(paragraphs[0].Style));
        Assert.That(paragraphs[3].Style, Is.SameAs(paragraphs[0].Style));
        Assert.That(paragraphs[0].Style.MarginAfter, Is.EqualTo(8));
    }

    [Test]
    public void AStyledSiblingGetsItsOwnCopyAndLeavesTheSharedDefaultsUnchanged()
    {
        var paragraphs = Paragraphs("<style>.r{font-weight:bold}</style><p>A</p><p style=\"color:red;margin-bottom:2pt\">B</p>"
            + "<p class=\"r\">C</p><p>D</p><div style=\"color:blue\"><p>E</p></div>");
        var shared = paragraphs[0].Style;
        Assert.That(paragraphs[3].Style, Is.SameAs(shared));
        Assert.That(paragraphs[1].Style, Is.Not.SameAs(shared));
        Assert.That(paragraphs[1].Style.Color, Is.EqualTo(new Rgb(1, 0, 0)));
        Assert.That(paragraphs[2].Style, Is.Not.SameAs(shared));
        Assert.That(paragraphs[2].Style.Bold, Is.True);
        Assert.That(paragraphs[4].Style, Is.Not.SameAs(shared));
        Assert.That(paragraphs[4].Style.Color, Is.EqualTo(new Rgb(0, 0, 1)));
        Assert.That(shared.Color, Is.EqualTo(new Rgb(0, 0, 0)));
        Assert.That(shared.Bold, Is.False);
        Assert.That(shared.MarginAfter, Is.EqualTo(8));
    }
}
