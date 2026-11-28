using System.Globalization;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>Inherited white-space controls real report text without rewriting every child paragraph.</summary>
[TestFixture]
public sealed class InheritedWhiteSpaceReportTests
{
    private static readonly PdfOptions Page = new()
    {
        PageWidth = 240, PageHeight = 180, Margin = 16
    };

    private static byte[] Render(string html)
    {
        using var destination = new MemoryStream();
        PdfConverter.Convert(html, destination, Page);
        return destination.ToArray();
    }

    private static List<LayoutPage> Layout(string html)
        => TextLayout.Layout(HtmlDocumentReader.ReadTree(html, Page, default), Page, default);

    [TestCase("normal")]
    [TestCase("nowrap")]
    [TestCase("pre-line")]
    [TestCase("pre-wrap")]
    [TestCase("pre")]
    public void PanelWhiteSpaceUsesTheSamePDFTextAndGeometryAsExplicitChildDeclarations(string mode)
    {
        const string first = "ACCOUNT   OPEN\n  REVIEW";
        const string second = "APPROVED  BY\n  TEAM";
        var actual = "<style>.report{white-space:" + mode + "}</style>" +
            "<section class='report'><p>" + first +
            "</p><article><h3 style='font-weight:normal'>" + second + "</h3></article></section>";
        var oracle = "<section class='report'><p style='white-space:" + mode + "'>" +
            first + "</p><article><h3 style='font-weight:normal;white-space:" + mode + "'>" +
            second + "</h3></article></section>";
        Assert.That(Render(actual), Is.EqualTo(Render(oracle)),
            "Container inheritance must affect actual PDF bytes, not merely parse declarations.");
        var styled = HtmlDocumentReader.Read(actual, Page, default);
        var direct = HtmlDocumentReader.Read(oracle, Page, default);
        Assert.That(styled.Select(p => (p.Style.NoWrap, p.Style.PreserveLineBreaks, p.Style.PreserveSpaces)),
            Is.EqualTo(direct.Select(p => (p.Style.NoWrap, p.Style.PreserveLineBreaks, p.Style.PreserveSpaces))));
    }

    [TestCase("html")]
    [TestCase("body")]
    public void DocumentWhiteSpaceInheritsToNestedReportText(string target)
    {
        const string body = "<section><p>ITEM  01\n  PAID</p><div><p>TOTAL  02\n  DUE</p></div></section>";
        var html = "<html" + (target == "html" ? " style='white-space:pre-wrap'" : "") +
            "><body" + (target == "body" ? " style='white-space:pre-wrap'" : "") +
            ">" + body + "</body></html>";
        const string explicitBody = "<section><p style='white-space:pre-wrap'>ITEM  01\n  PAID</p>" +
            "<div><p style='white-space:pre-wrap'>TOTAL  02\n  DUE</p></div></section>";
        Assert.That(Render(html), Is.EqualTo(Render("<html><body>" + explicitBody + "</body></html>")));
    }

    [Test]
    public void ImportantPanelWinnerAndChildResetAreIndependentAcrossRealPages()
    {
        var details = string.Concat(Enumerable.Range(1, 48).Select(i =>
            "<p>ROW " + i.ToString("000", CultureInfo.InvariantCulture) + "   PAID\n  CHECK</p>"));
        var oracleDetails = string.Concat(Enumerable.Range(1, 48).Select(i =>
            "<p style='white-space:pre-line'>ROW " +
            i.ToString("000", CultureInfo.InvariantCulture) + "   PAID\n  CHECK</p>"));
        const string final = "<p style='white-space:normal'>FINAL   CHECK\n  DONE</p>";
        var actual = "<style>section.audit{white-space:pre-line!important}</style>" +
            "<section class='audit' style='white-space:normal'>" + details + final + "</section>";
        var oracle = "<section class='audit'>" + oracleDetails + final + "</section>";
        Assert.That(Layout(actual).Count, Is.GreaterThan(2));
        Assert.That(Render(actual), Is.EqualTo(Render(oracle)));
    }

    [Test]
    public void WideKeywordsResetOnlyTheRequestedNestedContainer()
    {
        const string body = "<p>A   B\nC</p>";
        var actual = "<div style='white-space:pre-wrap'>" +
            "<section style='white-space:initial'>" + body + "</section>" +
            "<article style='white-space:inherit'>" + body + "</article>" +
            "<blockquote style='white-space:unset'>" + body + "</blockquote></div>";
        var oracle = "<div><section><p style='white-space:normal'>A   B\nC</p></section>" +
            "<article><p style='white-space:pre-wrap'>A   B\nC</p></article>" +
            "<blockquote><p style='white-space:pre-wrap'>A   B\nC</p></blockquote></div>";
        Assert.That(Render(actual), Is.EqualTo(Render(oracle)));
    }

    [TestCase("pre")]
    [TestCase("pre-wrap")]
    public void AnonymousLeadingSpacesRetainActualPdfTextAndPosition(string mode)
    {
        var actual = "<div style='white-space:" + mode + "'>  <span>AB</span></div>";
        var oracle = "<p style='margin:0;white-space:" + mode + "'>  <span>AB</span></p>";
        var text = Layout(actual).SelectMany(page => page.Runs).ToArray();
        Assert.That(string.Concat(text.Select(run => run.Text)), Is.EqualTo("  AB"));
        Assert.That(Render(actual), Is.EqualTo(Render(oracle)),
            "Anonymous inherited whitespace must use the same PDF glyph advances as explicit text.");
    }

}
