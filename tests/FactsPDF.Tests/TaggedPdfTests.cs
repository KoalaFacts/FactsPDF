using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace FactsPDF.Tests;

// Tagged PDF output: the structure tree the HTML elements become, the marked content that ties page content
// to it, the document language and title, and the PDF/UA-1 identification that only a document which can
// meet the standard may claim. The structure is read back from the written PDF, not from the engine.
[TestFixture]
public sealed partial class TaggedPdfTests
{
    private static readonly PdfFont Font = PdfFont.FromBytes(FontFixture.Create(listDigits: true, quotationMarks: true));
    private static readonly PdfWriter Supplied = Pdf.Write().WithFonts(f => f.Regular(Font).Bold(Font).Italic(Font).BoldItalic(Font));
    private static readonly PdfWriter Untagged = Supplied.WithTaggedPdf(false);

    private const string Lang = "<html lang=\"en\"><head><title>AB report</title></head><body>";

    private static string Html(string body) => Lang + body + "</body></html>";

    private static PdfObjects Tagged(string html, PdfWriter? writer = null) => new((writer ?? Supplied).ToBytes(html));

    // ---------------------------------------------------------------- structure types

    [TestCase("<h1>AB</h1><p>WiB</p>", "Document(H1(#),P(#))")]
    [TestCase("<h1>AB</h1><h2>AB</h2><h3>AB</h3><h4>AB</h4><h5>AB</h5><h6>AB</h6>", "Document(H1(#),H2(#),H3(#),H4(#),H5(#),H6(#))")]
    [TestCase("<div><p>AB</p></div>", "Document(Div(P(#)))")]
    [TestCase("<section><p>AB</p></section><article><p>AB</p></article>", "Document(Sect(P(#)),Art(P(#)))")]
    [TestCase("<header>AB</header><main><nav>AB</nav><aside>AB</aside></main><footer>AB</footer>",
        "Document(Div(P(#)),Div(Div(P(#)),Aside(P(#))),Div(P(#)))")]
    [TestCase("<blockquote><p>AB</p></blockquote>", "Document(BlockQuote(P(#)))")]
    [TestCase("<p>AB <strong>AB</strong> <em>AB</em> <code>AB</code> <q>AB</q> <a href=\"https://example.com/x\">AB</a></p>",
        "Document(P(#,Strong(#),#,Em(#),#,Code(#),#,Quote(#),#,Link(#,link)))")]
    [TestCase("<ul><li>AB</li><li>WiB</li></ul>", "Document(L(LI(Lbl(#),LBody(#)),LI(Lbl(#),LBody(#))))")]
    [TestCase("<ol><li>AB<ul><li>WiB</li></ul></li></ol>", "Document(L(LI(Lbl(#),LBody(#,L(LI(Lbl(#),LBody(#)))))))")]
    [TestCase("<dl><dt>AB</dt><dd>WiB</dd></dl>", "Document(L(LI(Lbl(#)),LI(LBody(#))))")]
    [TestCase("<figure><figcaption>AB</figcaption></figure>", "Document(Div(Caption(#)))")]
    [TestCase("<table><caption>AB</caption><thead><tr><th>A</th><th>B</th></tr></thead><tbody><tr><td>A</td><td>B</td></tr></tbody></table>",
        "Document(Table(Caption(#),THead(TR(TH(#),TH(#))),TBody(TR(TD(#),TD(#)))))")]
    [TestCase("<table><tr><td>A</td></tr></table>", "Document(Table(TR(TD(#))))")]
    public void ElementsBecomeTheirStructureTypes(string body, string expected)
        => Assert.That(Tagged(Html(body)).Tree(), Is.EqualTo(expected));

    [Test]
    public void TextDirectlyInAGroupingElementGoesIntoAnImpliedParagraph()
        => Assert.That(Tagged(Html("<div>AB<p>WiB</p>AB</div>")).Tree(), Is.EqualTo("Document(Div(P(#),P(#),P(#)))"));

    [Test]
    public void TransparentInlineElementsAddNoStructure()
        => Assert.That(Tagged(Html("<p>A<span>B</span><b>A</b><i>B</i><small>A</small></p>")).Tree(), Is.EqualTo("Document(P(#))"));

    [Test]
    public void StrongAndEmAreMappedToSpanThroughTheRoleMapOnlyWhenUsed()
    {
        var used = Tagged(Html("<p><strong>A</strong><em>B</em></p>"));
        Assert.That(used.Text, Does.Contain("/RoleMap << /Em /Span /Strong /Span >>"));
        Assert.That(Tagged(Html("<aside><p>AB</p></aside>")).Text, Does.Contain("/RoleMap << /Aside /Sect >>"));
        Assert.That(Tagged(Html("<p>AB</p>")).Text, Does.Not.Contain("/RoleMap"));
    }

    [Test]
    public void AnInlineElementWithItsOwnLanguageCarriesThatLanguage()
    {
        var pdf = Tagged(Html("<p>AB <span lang=\"fr-CA\">WiB</span></p>"));
        Assert.That(pdf.Tree(), Is.EqualTo("Document(P(#,Span(#)))"));
        Assert.That(pdf.Text, Does.Contain("/S /Span").And.Contain("/Lang (fr-CA)"));
    }

    [Test]
    public void AnAbbreviationWithATitleCarriesItsExpansion()
    {
        var pdf = Tagged(Html("<p><abbr title=\"Wi Wi\">AB</abbr></p>"));
        Assert.That(pdf.Text, Does.Contain("/E <FEFF00570069002000570069>"));
    }

    [Test]
    public void ThAndTdCarryScopeAndColumnSpans()
    {
        var pdf = Tagged(Html("<table><thead><tr><th>A</th><th colspan=\"2\">B</th></tr></thead>" +
            "<tbody><tr><th>A</th><td colspan=\"2\">B</td></tr><tr><th>B</th><td>A</td><td>A</td></tr></tbody></table>"));
        Assert.That(pdf.Text, Does.Contain("/A << /O /Table /Scope /Column >>"));
        Assert.That(pdf.Text, Does.Contain("/A << /O /Table /Scope /Column /ColSpan 2 >>"));
        Assert.That(pdf.Text, Does.Contain("/A << /O /Table /Scope /Row >>"));
        Assert.That(pdf.Text, Does.Contain("/A << /O /Table /ColSpan 2 >>"));
    }

    [Test]
    public void AnEmptyCellOfARowWithContentStaysSoTheTableIsRegular()
        => Assert.That(Tagged(Html("<table><tr><td>A</td><td></td><td>B</td></tr></table>")).Tree(),
            Is.EqualTo("Document(Table(TR(TD(#),TD(),TD(#))))"));

    [Test]
    public void HiddenAndEmptyElementsLeaveNoStructure()
        => Assert.That(Tagged(Html("<p>AB</p><div hidden><p>WiB</p></div><div></div><p style=\"display:none\">AB</p>")).Tree(),
            Is.EqualTo("Document(P(#))"));

    // ---------------------------------------------------------------- marked content

    [Test]
    public void EveryTextRunIsInsideItsStructureElementsMarkedContentAndNothingElseIsUntagged()
    {
        var pdf = Tagged(Html("<style>div{border:1pt solid red;background-color:#eee}</style><div><p>AB <u>WiB</u></p></div>"));
        var content = pdf.PageContent(0);
        // The border and background are one artifact; the text is paragraph content; the underline is an artifact.
        Assert.That(content, Does.StartWith("/Artifact BMC\n"));
        Assert.That(Regex.Count(content, @"/P <</MCID \d+>> BDC\nBT"), Is.EqualTo(1));
        Assert.That(MarkedContentIsBalanced(content), Is.True, content);
        Assert.That(UntaggedPainting(content), Is.Empty, content);
    }

    [Test]
    public void AdjacentRunsOfOneElementShareOneMarkedContentSequence()
    {
        var pdf = Tagged(Html("<p>AB WiB AB WiB AB WiB AB WiB AB WiB AB WiB AB WiB AB WiB AB WiB AB WiB AB WiB AB WiB AB WiB</p>"),
            Supplied.WithPage(p => p.Size(100, 400).Margin(10)));
        var content = pdf.PageContent(0);
        Assert.That(Regex.Count(content, "BDC"), Is.EqualTo(1));
        Assert.That(Regex.Count(content, "BT"), Is.GreaterThan(1));
    }

    [Test]
    public void PageFurnitureIsAPaginationArtifact()
    {
        var pdf = Tagged(Html("<p>AB</p>"), Supplied.WithHeaderText("AB").WithPageNumbers());
        var content = pdf.PageContent(0);
        Assert.That(content, Does.Contain("/Artifact <</Type /Pagination>> BDC"));
        Assert.That(pdf.Tree(), Is.EqualTo("Document(P(#))"));
        Assert.That(UntaggedPainting(content), Is.Empty, content);
    }

    [Test]
    public void TextDecorationsAndListMarkerShapesAreArtifactsOrLabels()
    {
        var pdf = Tagged(Html("<ul><li>AB</li></ul><p style=\"text-decoration:underline\">WiB</p>"));
        Assert.That(pdf.Tree(), Is.EqualTo("Document(L(LI(Lbl(#),LBody(#))),P(#))"));
        // The bullet is the item's label, with its text as ActualText.
        Assert.That(pdf.Text, Does.Contain("/S /Lbl").And.Contain("/ActualText <FEFF2022>"));
        Assert.That(UntaggedPainting(pdf.PageContent(0)), Is.Empty);
    }

    [Test]
    public void ADecimalMarkerIsTheLabelAndTheItemTextIsTheBody()
    {
        var pdf = Tagged(Html("<ol><li>AB</li></ol>"));
        Assert.That(pdf.Tree(), Is.EqualTo("Document(L(LI(Lbl(#),LBody(#))))"));
        Assert.That(pdf.Text, Does.Not.Contain("/ActualText"));
    }

    [Test]
    public void ReadingOrderFollowsTheLinesNotThePaintOrder()
    {
        // The marker and the image are painted before the text, but read in line order.
        var pdf = Tagged(Html("<p>AB</p><ul><li>WiB</li></ul><p>AB</p>"));
        Assert.That(pdf.Tree(), Is.EqualTo("Document(P(#),L(LI(Lbl(#),LBody(#))),P(#))"));
        Assert.That(pdf.TreeText(), Is.EqualTo("Document(P(AB),L(LI(Lbl(),LBody(WiB))),P(AB))"));
    }

    [Test]
    public void ATextRunSplitAcrossPagesContinuesTheSameStructureElement()
    {
        var text = string.Join(" ", Enumerable.Repeat("AB WiB", 40));
        var pdf = Tagged(Html($"<p>{text}</p>"), Supplied.WithPage(p => p.Size(120, 120).Margin(10)));
        Assert.That(pdf.PageCount, Is.GreaterThan(1));
        Assert.That(pdf.Tree(), Does.StartWith("Document(P(").And.EndWith("))"));
        Assert.That(Regex.Count(pdf.Tree(), "P\\("), Is.EqualTo(1));
        Assert.That(pdf.Text, Does.Contain("/Type /MCR"));
        for (var page = 0; page < pdf.PageCount; page++) Assert.That(pdf.PageContent(page), Does.Contain("BDC"));
    }

    [Test]
    public void EveryMarkedContentIdIsInTheParentTreeOfItsPage()
    {
        var pdf = Tagged(Html("<h1>AB</h1><p>WiB <a href=\"https://example.com/\">AB</a></p><table><tr><td>A</td></tr></table>"));
        foreach (var page in Enumerable.Range(0, pdf.PageCount))
        {
            var mcids = Regex.Matches(pdf.PageContent(page), @"<</MCID (\d+)>> BDC").Select(m => int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            Assert.That(mcids, Is.EqualTo(Enumerable.Range(0, mcids.Length)));
            Assert.That(pdf.ParentTreeLength(page), Is.EqualTo(mcids.Length));
        }
    }

    // ---------------------------------------------------------------- links

    [Test]
    public void ALinkIsALinkElementWithAnObjectReferenceToItsAnnotation()
    {
        var pdf = Tagged(Html("<p><a href=\"https://example.com/a\">AB WiB</a></p>"));
        Assert.That(pdf.Tree(), Is.EqualTo("Document(P(Link(#,link)))"));
        var annotation = pdf.Annotations().Single();
        Assert.That(annotation, Does.Contain("/Subtype /Link").And.Contain("/StructParent 1").And.Contain("/Contents <FEFF"));
        Assert.That(pdf.Text, Does.Contain("/Tabs /S"));
        Assert.That(pdf.Text, Does.Contain("/ParentTreeNextKey 2"));
    }

    [Test]
    public void AWrappedLinkKeepsOneLinkElementWithOneReferencePerAnnotation()
    {
        var pdf = Tagged(Html("<p><a href=\"https://example.com/a\">AB WiB AB WiB AB WiB AB WiB AB WiB</a></p>"),
            Supplied.WithPage(p => p.Size(100, 300).Margin(10)));
        Assert.That(pdf.Annotations().Count, Is.GreaterThan(1));
        Assert.That(Regex.Count(pdf.Tree(), "Link\\("), Is.EqualTo(1));
        Assert.That(Regex.Count(pdf.Tree(), "link"), Is.EqualTo(pdf.Annotations().Count));
    }

    // ---------------------------------------------------------------- figures

    private static PdfWriter WithImage() => Supplied.WithImage("chart", PdfImage.FromBytes(JpegResourceTests.Bytes("rgb-420")));

    [Test]
    public void AnImageIsAFigureWithItsAltText()
    {
        var pdf = Tagged(Html("<p><img src=\"chart\" alt=\"Revenue by quarter\" style=\"width:60pt\"></p>"), WithImage());
        Assert.That(pdf.Tree(), Is.EqualTo("Document(P(Figure(#)))"));
        Assert.That(pdf.Text, Does.Contain("/S /Figure").And.Contain("/Alt <FEFF"));
        Assert.That(pdf.PageContent(0), Does.Contain("/Figure <</MCID 0>> BDC\nq "));
    }

    [Test]
    public void ADecorativeImageIsAnArtifactWithNoFigure()
    {
        var pdf = Tagged(Html("<p>AB</p><img src=\"chart\" alt=\"\" style=\"width:60pt;display:block\">"), WithImage());
        Assert.That(pdf.Tree(), Is.EqualTo("Document(P(#))"));
        Assert.That(pdf.PageContent(0), Does.Contain("/Artifact BMC\nq ").And.Contain("Do Q"));
        Assert.That(UntaggedPainting(pdf.PageContent(0)), Is.Empty);
    }

    [Test]
    public void AnSvgMarkedPresentationalOrHiddenIsDecorativeToo()
        => Assert.That(Tagged(Html("<p>AB<svg width=\"20\" height=\"10\" role=\"presentation\"><rect width=\"10\" height=\"5\"/></svg>" +
            "<svg width=\"20\" height=\"10\" aria-hidden=\"true\"><rect width=\"10\" height=\"5\"/></svg></p>")).Tree(), Is.EqualTo("Document(P(#))"));

    [Test]
    public void AnImageWithoutAltIsAFigureWithoutDescriptionAndTheDocumentIsNotPdfUa()
    {
        var writer = WithImage();
        var pdf = Tagged(Html("<p><img src=\"chart\" style=\"width:60pt\"></p>"), writer);
        Assert.That(pdf.Tree(), Is.EqualTo("Document(P(Figure(#)))"));
        Assert.That(pdf.Text, Does.Not.Contain("/Alt").And.Not.Contain("pdfuaid:part"));
        using var output = new MemoryStream();
        Assert.That(writer.ToStream(Html("<p><img src=\"chart\" style=\"width:60pt\"></p>"), output).PdfUa, Is.False);
    }

    [Test]
    public void ANonStrictConversionWarnsAboutAnImageWithoutAlt()
    {
        PdfWarnings? seen = null;
        WithImage().WithStrict(false).WithWarnings(w => seen = w)
            .ToBytes(Html("<p><img src=\"chart\" style=\"width:60pt\"><img src=\"chart\" alt=\"A\" style=\"width:60pt\"></p>"));
        Assert.That(seen!.Items.Select(w => w.Code), Is.EqualTo(new[] { "FPDF1105" }));
    }

    [Test]
    public void AnSvgIsAFigureWhoseAltIsItsAriaLabelOrTitle()
    {
        var pdf = Tagged(Html("<p><svg width=\"20\" height=\"10\" aria-label=\"A mark\"><rect width=\"10\" height=\"5\"/></svg>" +
            "<svg width=\"20\" height=\"10\"><title>Another mark</title><rect width=\"10\" height=\"5\"/></svg></p>"));
        Assert.That(pdf.Tree(), Is.EqualTo("Document(P(Figure(#),Figure(#)))"));
        Assert.That(Regex.Count(pdf.Text, "/Alt <FEFF"), Is.EqualTo(2));
    }

    // ---------------------------------------------------------------- document level

    [Test]
    public void TheCatalogDeclaresTheTaggedDocument()
    {
        var pdf = Tagged(Html("<p>AB</p>"));
        Assert.That(pdf.Catalog, Does.Contain("/MarkInfo << /Marked true >>").And.Contain("/StructTreeRoot ")
            .And.Contain("/Lang (en)").And.Contain("/ViewerPreferences << /DisplayDocTitle true >>").And.Contain("/Metadata "));
        Assert.That(pdf.Text, Does.Contain("/Type /StructTreeRoot").And.Contain("/ParentTreeNextKey 1").And.Contain("/StructParents 0"));
    }

    [Test]
    public void TheTitleElementIsTheTitleInInfoAndXmpAndAnExplicitTitleWins()
    {
        var fromHtml = Tagged(Html("<p>AB</p>"));
        Assert.That(fromHtml.Text, Does.Contain("/Info "));
        Assert.That(fromHtml.Text, Does.Contain("/Title <FEFF" + Convert.ToHexString(Encoding.BigEndianUnicode.GetBytes("AB report"))));
        Assert.That(fromHtml.Xmp, Does.Contain("<rdf:li xml:lang=\"x-default\">AB report</rdf:li>").And.Contain("<pdfuaid:part>1</pdfuaid:part>"));
        var explicitTitle = Tagged(Html("<p>AB</p>"), Supplied.WithMetadata(m => m.Title("Explicit")));
        Assert.That(explicitTitle.Xmp, Does.Contain(">Explicit</rdf:li>"));
        Assert.That(explicitTitle.Text, Does.Contain("/Title <FEFF" + Convert.ToHexString(Encoding.BigEndianUnicode.GetBytes("Explicit"))));
    }

    [Test]
    public void TheTitleIsNormalisedAndEscapedInTheXmpPacket()
    {
        var pdf = Tagged("<html lang=\"en\"><head><title>  A &amp; B\n  &lt;x&gt; 中 </title></head><p>AB</p></html>");
        Assert.That(pdf.Xmp, Does.Contain(">A &#x26; B &#x3C;x&#x3E; &#x4E2D;</rdf:li>"));
    }

    [Test]
    public void AMalformedLanguageIsNotWritten()
    {
        var pdf = Tagged("<html lang=\"en us\"><head><title>T</title></head><p>AB</p></html>");
        Assert.That(pdf.Catalog, Does.Not.Contain("/Lang"));
        Assert.That(pdf.Xmp, Does.Not.Contain("pdfuaid"));
    }

    [Test]
    public void OnlyADocumentThatCanMeetPdfUaIdentifiesItself()
    {
        string Xmp(string html, PdfWriter? writer = null) => Tagged(html, writer).Xmp;
        Assert.That(Xmp(Html("<h1>AB</h1><h2>AB</h2><h2>AB</h2><h1>AB</h1>")), Does.Contain("pdfuaid:part"));
        // The built-in Courier is not embedded.
        Assert.That(Xmp(Html("<p>AB</p>"), Pdf.Write()), Does.Not.Contain("pdfuaid"));
        // No language, no title, a skipped heading level or a missing first H1.
        Assert.That(Xmp("<head><title>T</title></head><p>AB</p>"), Does.Not.Contain("pdfuaid"));
        Assert.That(Tagged("<html lang=\"en\"><p>AB</p></html>").Catalog, Does.Not.Contain("/Metadata"));
        Assert.That(Xmp(Html("<h1>AB</h1><h3>AB</h3>")), Does.Not.Contain("pdfuaid"));
        Assert.That(Xmp(Html("<h2>AB</h2>")), Does.Not.Contain("pdfuaid"));
    }

    [Test]
    public void TheWriteResultReportsPdfUa()
    {
        using var output = new MemoryStream();
        Assert.That(Supplied.ToStream(Html("<p>AB</p>"), output).PdfUa, Is.True);
        Assert.That(Pdf.Write().ToStream(Html("<p>AB</p>"), output).PdfUa, Is.False);
        Assert.That(Untagged.ToStream(Html("<p>AB</p>"), output).PdfUa, Is.False);
    }

    [Test]
    public void TheBuiltInFontIsTaggedToo()
    {
        var pdf = Tagged(Html("<p>AB</p><p>WiB</p>"), Pdf.Write());
        Assert.That(pdf.Tree(), Is.EqualTo("Document(P(#),P(#))"));
        Assert.That(pdf.Text, Does.Not.Contain("pdfuaid"));
    }

    // ---------------------------------------------------------------- the switch and determinism

    [Test]
    public void UntaggedOutputHasNoStructureAndNoMarkedContent()
    {
        var pdf = new PdfObjects(Untagged.ToBytes(Html("<h1>AB</h1><p>WiB</p>")));
        Assert.That(pdf.Text, Does.Not.Contain("StructTreeRoot").And.Not.Contain("MarkInfo").And.Not.Contain("BDC")
            .And.Not.Contain("/Metadata").And.Not.Contain("/Info "));
    }

    [Test]
    public void TheOutputIsDeterministic()
    {
        var html = Html("<p>AB</p><ul><li>AB <a href=\"https://example.com/\">WiB</a></li></ul><table><tr><td>A</td><td></td></tr></table>");
        Assert.That(Supplied.ToBytes(html), Is.EqualTo(Supplied.ToBytes(html)));
        Assert.That(Pdf.Write().ToBytes(html), Is.EqualTo(Pdf.Write().ToBytes(html)));
    }

    [Test]
    public void TaggingOnlyAddsStructureAndMarkedContentToTheUntaggedPage()
    {
        // Removing the marked-content operators leaves the untagged page's painting: same text, same order.
        var html = Html("<style>p{border:1pt solid #333}</style><h1>AB</h1><p>WiB <b>AB</b> <a href=\"https://example.com/\">AB</a></p><ul><li>WiB</li></ul>");
        var tagged = new PdfObjects(Supplied.ToBytes(html)).PageContent(0);
        var plain = new PdfObjects(Untagged.ToBytes(html)).PageContent(0);
        Assert.That(Shown(tagged), Is.EqualTo(Shown(plain)));
        Assert.That(PaintOperators(tagged), Is.EqualTo(PaintOperators(plain)));
    }

    private static string Shown(string content) => string.Concat(Regex.Matches(content, "<([0-9A-F]+)>").Select(m => m.Groups[1].Value));

    private static string PaintOperators(string content)
        => string.Join(" ", Regex.Matches(content, @"\b(re|f|S|m|c|l|W|n|Tj|TJ|Do)\b").Select(m => m.Value));

    // BDC/BMC and EMC pair up and never nest.
    private static bool MarkedContentIsBalanced(string content)
    {
        var depth = 0;
        foreach (Match m in Regex.Matches(content, @"\b(BDC|BMC|EMC)\b"))
        {
            depth += m.Value == "EMC" ? -1 : 1;
            if (depth is < 0 or > 1) return false;
        }
        return depth == 0;
    }

    // Painting operators that are outside any marked-content sequence.
    private static List<string> UntaggedPainting(string content)
    {
        var untagged = new List<string>();
        var inside = false;
        foreach (var line in content.Split('\n'))
        {
            if (line.EndsWith("BDC", StringComparison.Ordinal) || line.EndsWith("BMC", StringComparison.Ordinal)) inside = true;
            else if (line == "EMC") inside = false;
            else if (!inside && Regex.IsMatch(line, @"\b(Tj|TJ|Do| f| S| re f|f Q|S Q)\b")) untagged.Add(line);
        }
        return untagged;
    }
}
