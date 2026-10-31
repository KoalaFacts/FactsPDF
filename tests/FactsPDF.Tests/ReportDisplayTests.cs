using System.Text;
using FactsPDF.CommandLine;
using NUnit.Framework;

namespace FactsPDF.Tests;

/// <summary>Full-tree PDF witness for scoped report display suppression.</summary>
[TestFixture]
public sealed class ReportDisplayTests
{
    private static readonly PdfOptions Page = new()
    {
        PageWidth = 300, PageHeight = 175, Margin = 20
    };

    private static byte[] Render(string html, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, options ?? Page);
        return output.ToArray();
    }

    private static void Reject(string html, string code)
    {
        using var output = new MemoryStream();
        output.Write("EXISTING PDF"u8);
        output.Position = 3;
        var error = Assert.Throws<FactsPdfException>(() =>
            PdfConverter.Convert(html, output, Page));
        Assert.That(error!.Code, Is.EqualTo(code));
        Assert.That(output.ToArray(), Is.EqualTo("EXISTING PDF"u8.ToArray()));
        Assert.That(output.Position, Is.EqualTo(3));
    }

    [Test]
    public void HiddenNestedReportSectionsAreAbsentFromActualPagesAndPdfBytes()
    {
        var rows = string.Concat(Enumerable.Range(1, 40)
            .Select(i => $"<p style='margin:0'>Reference {i:00}</p>"));
        var visible = "<section style='padding:4pt;border:1pt solid #345678'>" +
            "<h2 style='margin:0'>Invoice summary</h2>" + rows +
            "</section><p style='margin:0'>GRAND TOTAL</p>";
        var hidden = "<section style='display:none;padding:90pt;" +
            "background:#123456;border:8pt solid #111111'>" +
            "<h1 style='break-after:avoid'>PRIVATE HEADING</h1>" +
            "<div><p>PRIVATE ACCOUNT 010101</p></div>" +
            "<blockquote>PRIVATE COMMENT</blockquote></section>";
        var html = visible.Replace("<p style='margin:0'>GRAND TOTAL</p>",
            hidden + "<p style='margin:0'>GRAND TOTAL</p>", StringComparison.Ordinal);
        Assert.That(Render(html), Is.EqualTo(Render(visible)));
        var pages = TextLayout.Layout(HtmlDocumentReader.ReadTree(html, Page, default),
            Page, default);
        Assert.That(pages.Count, Is.GreaterThan(1));
        Assert.That(pages.SelectMany(x => x.Runs).Last().Text, Is.EqualTo("GRAND TOTAL"));
        Assert.That(pages.SelectMany(x => x.Runs).Any(x =>
            x.Text.Contains("PRIVATE", StringComparison.Ordinal)), Is.False);
        Assert.That(HtmlDocumentReader.Read(html, Page, default)
            .SelectMany(x => x.Runs).Any(x => x.Text.Contains("PRIVATE", StringComparison.Ordinal)),
            Is.False);
    }

    [TestCase("<div>FIRST<section style='display:none'><p>SECRET</p></section>SECOND</div>",
        "<div>FIRSTSECOND</div>")]
    [TestCase("<div><span>FIRST</span><blockquote style='display:none'>SECRET</blockquote><span>SECOND</span></div>",
        "<div><span>FIRST</span><span>SECOND</span></div>")]
    [TestCase("<div>FIRST<section style='display:none'>SECRET</section><p>SECOND</p></div>",
        "<div>FIRST<p>SECOND</p></div>")]
    [TestCase("<div>FIRST<section style='display:none'>SECRET</section><section style='display:none'>SECRET2</section>SECOND</div>",
        "<div>FIRSTSECOND</div>")]
    public void HiddenBlocksDoNotSplitSurroundingAnonymousText(string source, string oracle)
    {
        // Compare actual PDF glyphs and page/layout bytes, not merely a
        // tree or extracted text assertion. No invisible gap may survive.
        Assert.That(Render(source), Is.EqualTo(Render(oracle)));
        var text = HtmlDocumentReader.Read(source, Page, default);
        Assert.That(text.SelectMany(p => p.Runs).Any(r =>
            r.Text.Contains("SECRET", StringComparison.Ordinal)), Is.False);
    }

    [Test]
    public void HiddenParagraphAndQuoteDoNotLeaveBlankLineOrOutsideMargins()
    {
        const string visible = "<section><p style='margin:0'>FIRST</p>" +
            "<p style='margin:0'>SECOND</p></section>";
        const string hidden = "<section><p style='margin:0'>FIRST</p>" +
            "<p style='display:none;margin:90pt;padding:8pt'>HIDDEN P</p>" +
            "<blockquote style='display:none'>HIDDEN QUOTE</blockquote>" +
            "<p style='margin:0'>SECOND</p></section>";
        Assert.That(Render(hidden), Is.EqualTo(Render(visible)));
    }

    [Test]
    public void DisplayBlockRestoresAnOrdinaryReportBlockInSelectorCascade()
    {
        const string visible = "<section><p style='margin:0'>VISIBLE</p></section>";
        const string cascade = "<style>p{display:none}section p{display:block}</style>" +
            "<section><p style='margin:0'>VISIBLE</p></section>";
        Assert.That(Render(cascade), Is.EqualTo(Render(visible)));
        const string important = "<style>.secret{display:none!important}" +
            "section.secret{display:block}</style>" +
            "<section class=secret style='display:block'><p>HIDDEN</p></section>" +
            "<p style='margin:0'>VISIBLE</p>";
        Assert.That(Render(important),
            Is.EqualTo(Render("<p style='margin:0'>VISIBLE</p>")));
    }

    [Test]
    public void HiddenReportDoesNotConsumePaintBudgetButStillValidatesCss()
    {
        const string visible = "<p style='margin:0'>OK</p>";
        const string hidden = "<section style='display:none;background:#abcdef;" +
            "border:3pt solid #123456;padding:10pt'>" +
            "<p>ABSENT</p></section>";
        Assert.That(Render(hidden + visible,
            Page with { MaxDisplayCommands = 2 }),
            Is.EqualTo(Render(visible, Page with { MaxDisplayCommands = 2 })));
        Reject("<section style='display:none'><p style='padding:calc(1pt + 2pt)'>" +
            "NOT OUTPUT</p></section>", "FPDF1202");
        Reject("<section style='display:none'><img src=missing /></section>", "FPDF1604");
    }

    [TestCase("display:grid", "FPDF1202")]
    [TestCase("display:flex", "FPDF1202")]
    [TestCase("display:inline", "FPDF1202")]
    [TestCase("display:inherit", "FPDF1202")]
    [TestCase("display:initial", "FPDF1202")]
    [TestCase("display:unset", "FPDF1202")]
    [TestCase("display:None block", "FPDF1202")]
    public void UnsupportedDisplayValuesFailWithoutReplacingPdf(string css, string code)
        => Reject("<section style='" + css + "'><p>NO</p></section>", code);

    [Test]
    public void UnmatchedDisplayRuleStillRejectsUnsupportedValues()
        => Reject("<style>.missing{display:flex}</style><p>VALID</p>", "FPDF1202");

    [TestCase("<span style='display:none'>A</span>")]
    [TestCase("<body style='display:none'><p>A</p></body>")]
    [TestCase("<hr style='display:none'>")]
    [TestCase("<ul><li><p style='display:none'>A</p></li></ul>")]
    [TestCase("<table><tr><td><p style='display:none'>A</p></td></tr></table>")]
    public void UnsupportedElementContextsRejectDisplay(string html)
        => Reject(html, "FPDF1201");

    [Test]
    public void CliAndLibraryRenderIdenticalSuppressedReport()
    {
        const string html = "<style>.private{display:none}</style>" +
            "<section class=private><p>SECRET</p></section><p>PUBLIC</p>";
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(html));
        using var output = new MemoryStream();
        using var diagnostics = new StringWriter();
        Assert.That(CliApplication.Run(["-", "-"], input, output, diagnostics),
            Is.Zero, diagnostics.ToString());
        using var expected = new MemoryStream();
        PdfConverter.Convert(html, expected);
        Assert.That(output.ToArray(), Is.EqualTo(expected.ToArray()));
    }
}
