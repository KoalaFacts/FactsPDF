using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

[TestFixture]
public sealed class LayoutPaintBudgetReviewTests
{
    private static readonly PdfOptions Page = new()
    {
        PageWidth = 300, PageHeight = 140, Margin = 20, MaxDisplayCommands = 1
    };

    private static FactsPdfException Failure(string html, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        output.Write("keep"u8);
        var error = Assert.Throws<FactsPdfException>(() =>
            PdfConverter.Convert(html, output, options ?? Page));
        Assert.That(Encoding.ASCII.GetString(output.ToArray()), Is.EqualTo("keep"));
        return error!;
    }

    [TestCase("background-color:red;padding-bottom:200pt")]
    [TestCase("border-left:2pt solid red;padding-bottom:200pt")]
    [TestCase("border-top:2pt solid red;padding-bottom:200pt")]
    [TestCase("border-bottom:2pt solid red;padding-bottom:200pt")]
    public void ExhaustedBudgetWinsBeforeNonzeroPaintGeometryError(string css)
    {
        const string prefix = "<p>A</p>";
        var error = Failure(prefix + "<div style='" + css + "'></div>");
        Assert.That(error.Code, Is.EqualTo("FPDF1401"));
        Assert.That(error.SourceOffset, Is.EqualTo(prefix.Length));
    }

    [Test]
    public void RemainingAllowancePreservesPaintGeometryError()
    {
        var error = Failure("<p>A</p><div style='background-color:red;padding-bottom:200pt'></div>",
            Page with { MaxDisplayCommands = 2 });
        Assert.That(error.Code, Is.EqualTo("FPDF1302"));
    }

    [Test]
    public void ZeroWidthBackgroundDoesNotInventACommandToMaskGeometryError()
    {
        var error = Failure("<p>A</p><div style='width:0;background-color:red;padding-bottom:200pt'></div>");
        Assert.That(error.Code, Is.EqualTo("FPDF1302"));
    }

    [Test]
    public void ZeroHeightPaintRemainsFreeAfterExactBudget()
    {
        using var output = new MemoryStream();
        var result = PdfConverter.Convert("<p>A</p><div style='background-color:red'></div>", output, Page);
        Assert.That(result.PageCount, Is.EqualTo(1));
        Assert.That(output.Length, Is.GreaterThan(0));
    }

    [Test]
    public void ExhaustedPaintBudgetWinsBeforeLaterPaintOnlyGeometryError()
    {
        var error = Failure("<div style='background-color:blue;padding:2pt'></div>" +
            "<div style='background-color:red;padding-bottom:200pt'></div>");
        Assert.That(error.Code, Is.EqualTo("FPDF1401"));
    }
}
