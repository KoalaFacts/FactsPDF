using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

// Legal and contract templates number clauses with <ol type="1"> and sub-clauses with <ol type="a">.
// HTML maps type on ol, ul and li to list-style-type as a presentational hint, below every author
// declaration; before, the attribute failed FPDF1103.
[TestFixture]
public sealed class ListTypeAttributeTests
{
    private static readonly PdfOptions Page = new() { PageWidth = 300, PageHeight = 400, Margin = 20 };

    private static byte[] Pdf(string html)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, Page);
        return output.ToArray();
    }

    private const string Items = "<li>One</li><li>Two</li><li>Three</li><li>Four</li><li>Five</li>";

    [TestCase("1", "decimal")]
    [TestCase("a", "lower-alpha")]
    [TestCase("A", "upper-alpha")]
    [TestCase("i", "lower-roman")]
    [TestCase("I", "upper-roman")]
    public void OlTypeRendersLikeListStyleType(string type, string kind)
        => Assert.That(Pdf($"<ol type=\"{type}\">{Items}</ol>"),
            Is.EqualTo(Pdf($"<ol style=\"list-style-type:{kind}\">{Items}</ol>")));

    [Test]
    public void OlTypeOneRendersLikeNoAttribute()
        => Assert.That(Pdf($"<ol type=\"1\">{Items}</ol>"), Is.EqualTo(Pdf($"<ol>{Items}</ol>")));

    [Test]
    public void OlTypeChangesTheMarkers()
        => Assert.That(Pdf($"<ol type=\"a\">{Items}</ol>"), Is.Not.EqualTo(Pdf($"<ol>{Items}</ol>")));

    [Test]
    public void NestedClausesRenderLikeTheirListStyleTypes()
        => Assert.That(
            Pdf("<ol type=\"1\"><li>Clause<ol type=\"a\"><li>Sub-clause</li><li>Sub-clause</li></ol></li>"
                + "<li>Clause<ol type=\"I\" start=\"3\" reversed><li>Part</li><li>Part</li></ol></li></ol>"),
            Is.EqualTo(Pdf("<ol style=\"list-style-type:decimal\"><li>Clause<ol style=\"list-style-type:lower-alpha\"><li>Sub-clause</li><li>Sub-clause</li></ol></li>"
                + "<li>Clause<ol style=\"list-style-type:upper-roman\" start=\"3\" reversed><li>Part</li><li>Part</li></ol></li></ol>")));

    // A nested ol without type takes the ol default (decimal), not its parent's hint.
    [Test]
    public void NestedListWithoutTypeKeepsTheOlDefault()
        => Assert.That(Pdf("<ol type=\"A\"><li>Clause<ol><li>Sub</li><li>Sub</li></ol></li></ol>"),
            Is.EqualTo(Pdf("<ol style=\"list-style-type:upper-alpha\"><li>Clause<ol><li>Sub</li><li>Sub</li></ol></li></ol>")));

    [TestCase("<style>ol{list-style-type:upper-roman}</style>", "")]
    [TestCase("<style>ol{list-style:none}</style>", "")]
    [TestCase("<style>ol{list-style-type:decimal!important}</style>", "")]
    [TestCase("", " style=\"list-style-type:upper-alpha\"")]
    [TestCase("", " style=\"list-style:inside\"")]
    [TestCase("", " style=\"list-style-type:inherit\"")]
    [TestCase("", " style=\"list-style-type:initial\"")]
    public void AuthorCssOverridesTheHint(string sheet, string inline)
        => Assert.That(Pdf($"{sheet}<ol type=\"a\"{inline}>{Items}</ol>"), Is.EqualTo(Pdf($"{sheet}<ol{inline}>{Items}</ol>")));

    [TestCase("<ol><li>One</li><li type=\"i\">Two</li><li>Three</li></ol>", "<ol><li>One</li><li style=\"list-style-type:lower-roman\">Two</li><li>Three</li></ol>")]
    [TestCase("<ol type=\"a\"><li>One</li><li type=\"Disc\">Two</li></ol>", "<ol type=\"a\"><li>One</li><li style=\"list-style-type:disc\">Two</li></ol>")]
    [TestCase("<ul type=\"circle\"><li>One</li><li>Two</li></ul>", "<ul style=\"list-style-type:circle\"><li>One</li><li>Two</li></ul>")]
    [TestCase("<ul type=\"SQUARE\"><li>One</li><li type=none>Two</li></ul>", "<ul style=\"list-style-type:square\"><li>One</li><li style=\"list-style-type:none\">Two</li></ul>")]
    [TestCase("<ul type=\"disc\"><li>One</li></ul>", "<ul><li>One</li></ul>")]
    public void UlAndLiTypeRenderLikeListStyleType(string html, string css)
        => Assert.That(Pdf(html), Is.EqualTo(Pdf(css)));

    // The computed-box cache shares boxes between elements with the same declarations; the hint must
    // keep a hinted list and a plain list with the same box declarations apart, in either order.
    [Test]
    public void HintedAndPlainListsWithTheSameDeclarationsStayDistinct()
    {
        const string Sheet = "<style>ol{background-color:#eeeeee}</style>";
        const string Plain = "<ol><li>One</li><li>Two</li></ol>";
        const string Hinted = "<ol type=\"a\"><li>One</li><li>Two</li></ol>";
        const string Styled = "<ol style=\"list-style-type:lower-alpha\"><li>One</li><li>Two</li></ol>";
        Assert.Multiple(() =>
        {
            Assert.That(Pdf(Sheet + Hinted + Plain), Is.EqualTo(Pdf(Sheet + Styled + Plain)));
            Assert.That(Pdf(Sheet + Plain + Hinted), Is.EqualTo(Pdf(Sheet + Plain + Styled)));
        });
    }

    [Test]
    public void MarkersAreTheExpectedLabels()
    {
        var text = Encoding.ASCII.GetString(Pdf("<ol type=\"A\"><li>x</li><li>x</li></ol><ol type=\"i\"><li>x</li><li>x</li><li>x</li><li>x</li></ol>"));
        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("<412E> Tj"));
            Assert.That(text, Does.Contain("<422E> Tj"));
            Assert.That(text, Does.Contain("<69762E> Tj"));
        });
    }

    [TestCase("<ol type=\"b\"><li>A</li></ol>")]
    [TestCase("<ol type=\"\"><li>A</li></ol>")]
    [TestCase("<ol type=\"01\"><li>A</li></ol>")]
    [TestCase("<ol type=\" a\"><li>A</li></ol>")]
    [TestCase("<ol type=\"disc\"><li>A</li></ol>")]
    [TestCase("<ol type=\"lower-alpha\"><li>A</li></ol>")]
    [TestCase("<ul type=\"a\"><li>A</li></ul>")]
    [TestCase("<ul type=\"1\"><li>A</li></ul>")]
    [TestCase("<ol><li type=\"b\">A</li></ol>")]
    [TestCase("<ol><li type=\"decimal\">A</li></ol>")]
    [TestCase("<menu type=\"a\"><li>A</li></menu>")]
    [TestCase("<p type=\"a\">A</p>")]
    public void OtherValuesAndTargetsFail(string html)
    {
        using var output = new MemoryStream();
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, Page))!;
        Assert.Multiple(() =>
        {
            Assert.That(error.Code, Is.EqualTo("FPDF1103"));
            Assert.That(error.SourceOffset, Is.Zero.Or.GreaterThan(0));
            Assert.That(output.Length, Is.Zero);
        });
    }

    [Test]
    public void TheTwoLevelNestingLimitStillApplies()
    {
        using var output = new MemoryStream();
        var error = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(
            "<ol type=\"1\"><li>A<ol type=\"a\"><li>B<ol type=\"i\"><li>C</li></ol></li></ol></li></ol>", output, Page))!;
        Assert.That(error.Code, Is.EqualTo("FPDF1101"));
    }
}
