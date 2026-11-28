using NUnit.Framework;

namespace FactsPDF.Tests;

// title and data-* print nothing in Chrome 158 (no tooltip, no text), so they are accepted on every
// element FactsPDF supports and render as the element without them; an attribute name is any run of
// characters HTML allows, so data-* names with dots or non-ASCII letters tokenize too.
[TestFixture]
public sealed class AttributeNameTests
{
    private static readonly PdfFont Face = PdfFont.FromBytes(FontFixture.Create(quotationMarks: true, listDigits: true));

    // One supplied face in every style role, so bold and italic elements render too.
    private static readonly PdfOptions Page = new()
    {
        PageWidth = 260, PageHeight = 300, Margin = 12,
        Fonts = [Face], BoldFonts = [Face], ItalicFonts = [Face], BoldItalicFonts = [Face]
    };

    private static byte[] Pdf(string html)
    {
        using var output = new MemoryStream();
        PdfConverter.Convert(html, output, Page);
        return output.ToArray();
    }

    // Each template names the element under test; {A} is where the attribute goes.
    private static readonly string[] Templates =
    [
        "<!doctype html><html {A}><head></head><body><p>A</p></body></html>",
        "<!doctype html><html><head {A}></head><body><p>A</p></body></html>",
        "<!doctype html><html><head></head><body {A}><p>A</p></body></html>",
        "<!doctype html><html><head><title {A}>A</title></head><body><p>A</p></body></html>",
        "<!doctype html><html><head><meta {A} name=x content=y></head><body><p>A</p></body></html>",
        "<!doctype html><html><head><link {A} rel=icon href=x.png></head><body><p>A</p></body></html>",
        "<!doctype html><html><head><base {A} href=https://example.com/></head><body><p>A</p></body></html>",
        "<!doctype html><html><head><style {A}>p{color:red}</style></head><body><p>A</p></body></html>",
        "<p>A</p><script {A}></script>",
        "<p>A</p><template {A}><p>B</p></template>",
        "<p>A<br {A}>B</p>", "<p>A</p><hr {A}>", "<svg {A} width=10 height=10><rect width=5 height=5 /></svg>",
        "<p {A}>A</p>", "<pre {A}>A</pre>", "<h1 {A}>A</h1>", "<h6 {A}>A</h6>", "<div {A}>A</div>", "<section {A}>A</section>",
        "<article {A}>A</article>", "<aside {A}>A</aside>", "<nav {A}>A</nav>", "<main {A}>A</main>", "<header {A}>A</header>",
        "<footer {A}>A</footer>", "<hgroup {A}>A</hgroup>", "<search {A}>A</search>", "<blockquote {A}>A</blockquote>",
        "<figure {A}>A</figure>", "<figcaption {A}>A</figcaption>",
        "<dl {A}><dt>A</dt></dl>",
        "<dl><dt {A}>A</dt></dl>", "<dl><dd {A}>A</dd></dl>",
        "<p><span {A}>A</span></p>", "<p><b {A}>A</b></p>", "<p><strong {A}>A</strong></p>", 
        "<p><i {A}>A</i></p>", "<p><em {A}>A</em></p>", "<p><var {A}>A</var></p>", "<p><cite {A}>A</cite></p>", "<p><dfn {A}>A</dfn></p>", "<address {A}>A</address>",
        "<p><small {A}>A</small></p>", "<p><u {A}>A</u></p>", "<p><s {A}>A</s></p>", "<p><del {A}>A</del></p>", "<p><ins {A}>A</ins></p>",
        "<p><a {A} href=https://example.com/>A</a></p>", "<p><code {A}>A</code></p>", "<p><kbd {A}>A</kbd></p>", "<p><samp {A}>A</samp></p>",
        "<p><time {A}>A</time></p>",
        "<p><data {A}>A</data></p>", "<p><abbr {A}>A</abbr></p>", "<p><q {A}>A</q></p>", "<p><bdi {A}>A</bdi></p>",
        "<ul {A}><li>A</li></ul>", "<ol {A}><li>A</li></ol>", "<menu {A}><li>A</li></menu>", "<ul><li {A}>A</li></ul>",
        "<table {A}><tr><td>A</td></tr></table>", "<table><caption {A}>A</caption><tr><td>A</td></tr></table>",
        "<table><colgroup {A}><col></colgroup><tr><td>A</td></tr></table>", "<table><colgroup><col {A}></colgroup><tr><td>A</td></tr></table>",
        "<table><thead {A}><tr><th>A</th></tr></thead><tbody><tr><td>B</td></tr></tbody></table>",
        "<table><tbody {A}><tr><td>A</td></tr></tbody></table>",
        "<table><tbody><tr><td>A</td></tr></tbody><tfoot {A}><tr><td>B</td></tr></tfoot></table>",
        "<table><tr {A}><td>A</td></tr></table>", "<table><tr><td {A}>A</td></tr></table>", "<table><tr><th {A}>A</th></tr></table>",
    ];

    [TestCase("title=\"A tip\"")]
    [TestCase("data-k=v")]
    [TestCase("data-a.b=1")]
    [TestCase("data-café='x'")]
    [TestCase("DATA-Upper=1")]
    [TestCase("data-=1")]
    [TestCase("data-k")]
    public void TitleAndDataAttributesRenderNothingOnEveryElement(string attribute)
    {
        foreach (var template in Templates)
        {
            // abbr[title] has a dotted underline in the HTML user-agent sheet, so it is the one element where title shows.
            if (attribute.StartsWith("title", StringComparison.Ordinal) && template.Contains("<abbr", StringComparison.Ordinal)) continue;
            var with = template.Replace("{A}", attribute, StringComparison.Ordinal);
            var without = template.Replace(" {A}", "", StringComparison.Ordinal);
            Assert.That(Pdf(with), Is.EqualTo(Pdf(without)), with);
        }
    }

    [Test]
    public void ADataAttributeWithADotOrNonAsciiLetterIsStillAnAttributeToSelectors()
    {
        const string body = "<p data-café='1'>A</p><p data-x='2'>B</p>";
        Assert.That(Pdf("<style>[data-x]{color:#c00}</style>" + body),
            Is.EqualTo(Pdf("<style>p+p{color:#c00}</style>".Replace("p+p", "p:nth-child(2)", StringComparison.Ordinal) + body)));
    }

    // The HTML parser keeps the first of a repeated attribute (Chrome 158 prints the first style).
    [TestCase("<p style='color:#c00' STYLE='color:#06c'>A</p>", "<p style='color:#c00'>A</p>")]
    [TestCase("<p title=a title=b data-k=1 data-k=2>A</p>", "<p title=a data-k=1>A</p>")]
    public void ARepeatedAttributeKeepsTheFirst(string with, string without)
        => Assert.That(Pdf(with), Is.EqualTo(Pdf(without)));

    [Test]
    public void AStyleElementTakesAStyleAttributeButNeverShowsItsSheet()
    {
        const string sheet = "<style>p{color:#c00}</style><p>A</p>";
        Assert.That(Pdf(sheet.Replace("<style>", "<style style='color:#06c;margin:4pt'>", StringComparison.Ordinal)), Is.EqualTo(Pdf(sheet)));
        var ex = Assert.Throws<FactsPdfException>(() => Pdf(sheet.Replace("<style>", "<style style='display:block'>", StringComparison.Ordinal)))!;
        Assert.That(ex.Code, Is.EqualTo("FPDF1103"));
    }

    [TestCase("onclick=x")]
    [TestCase("tabindex=0")]
    [TestCase("data-a\"b=1")]
    public void OtherAttributesStillFailExplicitly(string attribute)
    {
        var ex = Assert.Throws<FactsPdfException>(() => Pdf($"<p {attribute}>x</p>"))!;
        Assert.That(ex.Code, Is.AnyOf("FPDF1103", "FPDF1101"));
    }
}
