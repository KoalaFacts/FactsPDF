using System.Globalization;
using NUnit.Framework;

namespace FactsPDF.Tests;

public sealed class TableWidthToleranceReviewTests
{
    [TestCase("99.999pt", 99.999)] [TestCase("100.001pt", 100.001)]
    [TestCase("99.999%", 99.999)] [TestCase("100.001%", 100.001)]
    public void ExactInclusiveToleranceDoesNotRejectValidDecimalTracks(string width, double expected)
    {
        var cell = TableLayoutTests.Layout(TableLayoutTests.Table(colStyle: "width:" + width, cellStyle: "background-color:red"))
            .Single().PaintBoxes.Single();
        Assert.That(cell.Width, Is.EqualTo(expected).Within(0.000000001));
    }

    [TestCase(99.998999999)] [TestCase(100.001000001)]
    public void RoundoffAllowanceDoesNotAcceptAnActualOutsideBoundary(double width)
        => TableStructureTests.Fails(TableLayoutTests.Table(colStyle: "width:" + width.ToString("R", CultureInfo.InvariantCulture) + "pt"), "FPDF1302");
}
