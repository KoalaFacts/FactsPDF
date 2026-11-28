using NUnit.Framework;

namespace FactsPDF.Tests;

// R5x-html-tree-builder: the token list carries the tree the HTML parser builds (HTML Standard
// 13.2.6, tree construction). Omitted end tags are written out where the parser implies them, end
// tags with nothing to close are ignored, the end of the input closes every element, and a table's
// implied tbody, tr and colgroup are inserted; metadata elements are accepted in body and global
// attributes that print nothing are accepted everywhere. Each case prints exactly what the explicit
// markup Chrome builds for it prints. Formatting elements the parser would reopen (the adoption
// agency algorithm) and misplaced table content it would move (foster parenting) still fail.
[TestFixture]
public sealed class HtmlTreeBuilderTests
{
    private static byte[] Render(string html) => FactsPDF.Pdf.Write().ToBytes(html);

    private static FactsPdfException Fails(string html) => Assert.Throws<FactsPdfException>(() => Render(html))!;

    // Omitted end tags: each pair is the markup and the tree Chrome builds for it, written out.
    [TestCase("<p>A<div>B</div>", "<p>A</p><div>B</div>")]
    [TestCase("<p>A<p>B", "<p>A</p><p>B</p>")]
    [TestCase("<p>A<hr>B", "<p>A</p><hr>B")]
    [TestCase("<p>A<ul><li>B</ul>", "<p>A</p><ul><li>B</li></ul>")]
    [TestCase("<ul><li>A<li>B<li>C</ul>", "<ul><li>A</li><li>B</li><li>C</li></ul>")]
    [TestCase("<ol><li><p>A<li>B</ol>", "<ol><li><p>A</p></li><li>B</li></ol>")]
    // An li closes the open item past a div inside it, as the parser's li loop does.
    [TestCase("<ul><li><div>A<li>B</ul>", "<ul><li><div>A</div></li><li>B</li></ul>")]
    [TestCase("<dl><dt>T<dd>D<dt>U<dd>E</dl>", "<dl><dt>T</dt><dd>D</dd><dt>U</dt><dd>E</dd></dl>")]
    [TestCase("<h1>A<h2>B</h2>", "<h1>A</h1><h2>B</h2>")]
    [TestCase("<h2>A</h3><p>B</p>", "<h2>A</h2><p>B</p>")]
    [TestCase("<div><p>A</div><p>B</p>", "<div><p>A</p></div><p>B</p>")]
    // A span is no formatting element: a block's end tag closes it for good.
    [TestCase("<div><span>A</div>B", "<div><span>A</span></div>B")]
    [TestCase("<p><kbd>A</p>B", "<p><kbd>A</kbd></p>B")]
    [TestCase("<p>A <x-card>B</p>", "<p>A <x-card>B</x-card></p>")]
    public void OmittedEndTagsAreImplied(string omitted, string written)
        => Assert.That(Render(omitted), Is.EqualTo(Render(written)));

    [TestCase("<table><tr><td>A<td>B<tr><td>C<td>D</table>",
        "<table><tbody><tr><td>A</td><td>B</td></tr><tr><td>C</td><td>D</td></tr></tbody></table>")]
    [TestCase("<table><td>A</td></table>", "<table><tbody><tr><td>A</td></tr></tbody></table>")]
    [TestCase("<table><tbody><td>A</td></tbody></table>", "<table><tbody><tr><td>A</td></tr></tbody></table>")]
    [TestCase("<table><tr><td><p>A</td></tr></table>", "<table><tbody><tr><td><p>A</p></td></tr></tbody></table>")]
    [TestCase("<table><thead><tr><th>H<tbody><tr><td>A</table>",
        "<table><thead><tr><th>H</th></tr></thead><tbody><tr><td>A</td></tr></tbody></table>")]
    [TestCase("<table><caption>C<tr><td>A</table>", "<table><caption>C</caption><tbody><tr><td>A</td></tr></tbody></table>")]
    [TestCase("<table style='width:100pt'><col style='width:100pt'><tr><td>A</table>",
        "<table style='width:100pt'><colgroup><col style='width:100pt'></colgroup><tbody><tr><td>A</td></tr></tbody></table>")]
    [TestCase("<table style='width:100pt'><colgroup><col style='width:100pt'><tbody><tr><td>A</table>",
        "<table style='width:100pt'><colgroup><col style='width:100pt'></colgroup><tbody><tr><td>A</td></tr></tbody></table>")]
    public void TableStructureIsImplied(string omitted, string written)
        => Assert.That(Render(omitted), Is.EqualTo(Render(written)));

    // The parser's implied elements are elements: selectors see the implied tbody and tr.
    [Test]
    public void ImpliedTableElementsAreSelectable()
        => Assert.That(Render("<style>tbody > tr > td:last-child{color:#c00}</style><table><td>A<td>B</table>"),
            Is.EqualTo(Render("<style>.r{color:#c00}</style><table><tbody><tr><td>A</td><td class=r>B</td></tr></tbody></table>")));

    // End tags with no open element of their name are ignored, before body content as in it.
    [TestCase("<p>A</p></div></span><p>B</p>", "<p>A</p><p>B</p>")]
    [TestCase("<html></div><p>A</p>", "<p>A</p>")]
    [TestCase("<head><base href=/></base></head><p>A</p>", "<head><base href=/></head><p>A</p>")]
    [TestCase("<p>A<img src=x></img></p>", "<p>A<img src=x></p>")]
    [TestCase("<hr></hr><p>A</p>", "<hr><p>A</p>")]
    [TestCase("<figure>A</figcaption></figure>", "<figure>A</figure>")]
    public void StrayEndTagsAreIgnored(string stray, string clean)
    {
        // The img case needs its resource; the others do not use it.
        var writer = FactsPDF.Pdf.Write().WithImage("x", PdfImage.FromBytes(PngFixture.Image([0, 80, 100], type: 0)));
        Assert.That(writer.ToBytes(stray), Is.EqualTo(writer.ToBytes(clean)));
    }

    // The end of the input closes every open element.
    [TestCase("<div><p>A", "<div><p>A</p></div>")]
    [TestCase("<blockquote><p>A</p>", "<blockquote><p>A</p></blockquote>")]
    [TestCase("<ul><li>A<li>B", "<ul><li>A</li><li>B</li></ul>")]
    [TestCase("<p><b>A", "<p><b>A</b></p>")]
    [TestCase("<table><tr><td>A", "<table><tbody><tr><td>A</td></tr></tbody></table>")]
    [TestCase("<pre/>", "<pre></pre>")]
    public void TheEndOfTheInputClosesEveryElement(string open, string closed)
        => Assert.That(Render(open), Is.EqualTo(Render(closed)));

    // Over-correction guard: where the parser would close a formatting element and reopen it in
    // the next block (adoption agency, active formatting elements), the tags stay as written and the
    // reader rejects them; so does content the parser would move in front of a table, and an end
    // tag p without an open p (the parser inserts an empty paragraph).
    [TestCase("<p><b>A<div>B</div>")]
    [TestCase("<ul><li><b>A<li>B</ul>")]
    [TestCase("<div><i>A</div>B")]
    [TestCase("<p><code>A</p>B")]
    [TestCase("<div><span><b>A</span></div>")]
    [TestCase("<table><tr><td>A</td></tr><p>B</p></table>")]
    [TestCase("<table>x<tr><td>A</td></tr></table>")]
    [TestCase("<p>A</p></p>")]
    [TestCase("<p>A</br></p>")]
    public void MisnestingTheParserWouldRepairDifferentlyStillFails(string html)
        => Assert.That(Fails(html).Code, Is.EqualTo("FPDF1101"));

    // In body the parser inserts metadata where it is written; the user-agent sheet hides it.
    // An empty comment keeps the text in two runs, as the element between them does.
    [TestCase("<p>A<meta name=viewport content=x>B</p>", "<p>A<!---->B</p>")]
    [TestCase("<body><meta charset=utf-8><link rel=icon href=/a><p>A</p></body>", "<body><p>A</p></body>")]
    [TestCase("<p>A<title>T</title>B</p>", "<p>A<!---->B</p>")]
    [TestCase("<body><head><title>T</title></head><p>A</p></body>", "<body><p>A</p></body>")]
    [TestCase("<ul><meta name=x content=y><li>A</li></ul>", "<ul><li>A</li></ul>")]
    [TestCase("<table><tr><td>A<link rel=icon href=/a></td></tr></table>", "<table><tr><td>A</td></tr></table>")]
    [TestCase("<p>A<base href=/></p>", "<p>A</p>")]
    public void MetadataInBodyPrintsNothing(string html, string without)
        => Assert.That(Render(html), Is.EqualTo(Render(without)));

    [Test]
    public void MetadataStillCountsAsASibling()
        => Assert.That(Render("<style>p:first-child{color:#c00}</style><div><meta name=x content=y><p>A</p></div>"),
            Is.EqualTo(Render("<div><p>A</p></div>")));

    [TestCase("<style>title{display:block}</style><p>A<title>T</title></p>", "FPDF1201")]
    [TestCase("<table><tbody><meta name=x content=y><tr><td>A</td></tr></tbody></table>", "FPDF1101")]
    public void MetadataThatWouldShowOrMoveStillFails(string html, string code)
        => Assert.That(Fails(html).Code, Is.EqualTo(code));

    // Global attributes Chrome prints nothing for are accepted on every element and can be selected.
    [Test]
    public void GlobalAttributesThatPrintNothingAreAccepted()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Render("<html xmlns='http://www.w3.org/1999/xhtml'><p role=note aria-label='Total' aria-hidden=true tabindex=0 inert " +
                "translate=no spellcheck=false draggable=true accesskey=k autocapitalize=off autocorrect=off enterkeyhint=done " +
                "inputmode=none writingsuggestions=false itemscope itemtype='https://schema.org/Thing' itemprop=name itemid=x itemref=y " +
                "part=p exportparts=q slot=s nonce=n>A</p></html>"),
                Is.EqualTo(Render("<p>A</p>")));
            Assert.That(Render("<style>[role=note]{color:#c00} [aria-level='2']{color:#00c}</style><p role=note>A</p><p aria-level=2>B</p>"),
                Is.EqualTo(Render("<style>.n{color:#c00} .b{color:#00c}</style><p class=n>A</p><p class=b>B</p>")));
        });
    }

    // contenteditable changes line breaking, popover hides the element until shown and autofocus
    // may print a focus ring, so they still fail; xmlns is accepted only with the HTML namespace.
    [TestCase("<p contenteditable>A</p>")]
    [TestCase("<p popover>A</p>")]
    [TestCase("<p autofocus>A</p>")]
    [TestCase("<html xmlns='http://www.w3.org/2000/svg'><p>A</p></html>")]
    [TestCase("<p xmlns='http://www.w3.org/1999/xhtml'>A</p>")]
    public void AttributesThatChangeThePageStillFail(string html)
        => Assert.That(Fails(html).Code, Is.EqualTo("FPDF1103"));

    // :lang() matches the element's language, its own lang or the nearest ancestor's, as the range
    // itself or the range and a hyphen, ASCII case-insensitively; an empty lang is no language.
    [Test]
    public void LangDrivesTheLangPseudoClass()
        => Assert.That(Render("<style>p:lang(fr){color:#c00}</style><div lang=fr><p>A</p><p lang=fry>B</p><p lang=''>C</p><p lang=FR-ca>D</p></div><p>E</p>"),
            Is.EqualTo(Render("<style>.r{color:#c00}</style><div><p class=r>A</p><p>B</p><p>C</p><p class=r>D</p></div><p>E</p>")));

    [TestCase(":lang(en-US){color:red}")]
    [TestCase(":lang(en, fr){color:red}")]
    [TestCase(":lang(\"*-CH\"){color:red}")]
    [TestCase(":lang(){color:red}")]
    public void LanguageRangesBeyondAPrimarySubtagFail(string css)
        => Assert.That(Fails("<style>" + css + "</style><p>A</p>").Code, Is.EqualTo("FPDF1203"));

    // q prints the quotation marks of its language; FactsPDF has the English ones.
    [Test]
    public void EnglishQuotesFollowLang()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Render("<p lang=en-US><q>A</q></p>"), Is.EqualTo(Render("<p><q>A</q></p>")));
            Assert.That(Render("<div lang=''><p><q>A</q></p></div>"), Is.EqualTo(Render("<div><p><q>A</q></p></div>")));
            Assert.That(Fails("<html lang=de><p><q>A</q></p></html>").Code, Is.EqualTo("FPDF1103"));
            Assert.That(Fails("<p lang=fr>A <span><q>B</q></span></p>").Code, Is.EqualTo("FPDF1103"));
        });
    }

    // A string is already characters: a declared encoding FactsPDF can decode changes nothing.
    [TestCase("<meta charset=windows-1252>")]
    [TestCase("<meta charset=' ISO-8859-1 '>")]
    [TestCase("<meta charset=utf-16>")]
    [TestCase("<meta http-equiv=Content-Type content='text/html; charset=latin1'>")]
    [TestCase("<meta http-equiv=content-type content='text/html'>")]
    [TestCase("<meta http-equiv=X-UA-Compatible content='IE=edge'>")]
    public void DeclaredEncodingsFactsPdfDecodesAreAccepted(string meta)
        => Assert.That(Render("<head>" + meta + "</head><p>A</p>"), Is.EqualTo(Render("<p>A</p>")));
}
