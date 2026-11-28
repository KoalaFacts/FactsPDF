using NUnit.Framework;

namespace FactsPDF.Tests;

public sealed class TableEmptyPlacementReviewTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 220, PageHeight = 100, Margin = 10 };

    [TestCase(1)] [TestCase(2)]
    public void TrailingInvisibleTableDoesNotCreateABlankPageOrSpendPageBudget(int maxPages)
    {
        const string before = "<p style='break-after:page'>A</p>";
        var options = Page with { MaxPages = maxPages };
        Assert.That(TableLayoutTests.NPdf(before + TableLayoutTests.Table(""), options),
            Is.EqualTo(TableLayoutTests.NPdf(before, options)));
    }

    [TestCase(false)] [TestCase(true)]
    public void InvisibleTableDoesNotConsumeNewAncestorsLeadingDecoration(bool painted)
    {
        var before = "<p style='font-size:10pt;line-height:2;margin-top:0;margin-bottom:0'>BEFORE</p>"
            + "<div style='padding-top:10pt" + (painted ? ";border:1pt solid red" : "") + "'>";
        var visible = TableLayoutTests.Table("A<br>B<br>C", cellStyle: "background-color:blue") + "</div>";
        var expected = TableLayoutTests.NPdf(before + visible, Page);
        var actual = TableLayoutTests.NPdf(before + TableLayoutTests.Table("") + visible, Page);
        Assert.That(actual, Is.EqualTo(expected), "An invisible zero-height table must not consume or strand ancestor decoration.");
        var pages = TableLayoutTests.NLayout(before + TableLayoutTests.Table("") + visible, Page);
        Assert.That(pages, Has.Count.EqualTo(2));
        Assert.That(pages[0].PaintBoxes, Is.Empty);
        Assert.That(pages[1].PaintBoxes.Single(b => b.Style.BackgroundColor.HasValue).Top, Is.EqualTo(painted ? 21 : 20));
    }

    [TestCase(false)] [TestCase(true)]
    public void InvisibleTableCannotHideRequiredLeadingDecorationByCreatingAPhantomPage(bool painted)
    {
        var html = "<div style='padding-top:1pt" + (painted ? ";background-color:red" : "") + "'>"
            + TableLayoutTests.Table("") + TableLayoutTests.Table("A<br>B<br>C<br>D") + "</div>";
        TableStructureTests.Fails(TableLayoutTests.Neutral + html, "FPDF1302", Page);
    }

    [Test]
    public void InvisibleTableDoesNotConsumeDeferredBreakAfter()
    {
        const string before = "<p style='break-after:page'>BEFORE</p><div style='padding-top:10pt'>";
        const string after = "</div><p>AFTER</p>";
        Assert.That(TableLayoutTests.NPdf(before + TableLayoutTests.Table("") + after, Page),
            Is.EqualTo(TableLayoutTests.NPdf(before + after, Page)));
    }
}
