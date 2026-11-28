using System.Text;
using NUnit.Framework;

namespace FactsPDF.Tests;

// Structural pseudo-classes and :not(). Expected matches were checked against Chrome's
// Element.matches() and computed colours on the same markup.
[TestFixture]
public sealed class StructuralPseudoClassTests
{
    private static readonly Rgb Red = new(1, 0, 0), Blue = new(0, 0, 1), Black = new(0, 0, 0);

    // Seven paragraphs and headings inside one div: p p h2 p h2 p p.
    private const string Mixed = "<div><p>1</p><p>2</p><h2>3</h2><p>4</p><h2>5</h2><p>6</p><p>7</p></div>";

    private static Rgb[] Colors(string css, string body)
        => HtmlDocumentReader.Read("<html><head><style>" + css + "</style></head><body>" + body + "</body></html>",
                new PdfOptions(), CancellationToken.None)
            .Select(p => p.Runs[0].Style.Color).ToArray();

    private static int[] Matched(string css, string body = Mixed)
        => Colors(css, body).Select((color, i) => (color, i)).Where(x => x.color == Red).Select(x => x.i + 1).ToArray();

    private static FactsPdfException Fails(string html, string code, PdfOptions? options = null)
    {
        using var output = new MemoryStream();
        output.Write("keep"u8);
        var ex = Assert.Throws<FactsPdfException>(() => PdfConverter.Convert(html, output, options));
        Assert.That(ex!.Code, Is.EqualTo(code), ex.Message);
        Assert.That(Encoding.ASCII.GetString(output.ToArray()), Is.EqualTo("keep"));
        return ex;
    }

    [TestCase(":first-child", new[] { 1 })]
    [TestCase(":last-child", new[] { 7 })]
    [TestCase("p:first-child", new[] { 1 })]
    [TestCase("h2:first-child", new int[0])]
    [TestCase(":first-of-type", new[] { 1, 3 })]
    [TestCase(":last-of-type", new[] { 5, 7 })]
    [TestCase("h2:last-of-type", new[] { 5 })]
    [TestCase(":only-child", new int[0])]
    [TestCase(":only-of-type", new int[0])]
    [TestCase(":nth-child(odd)", new[] { 1, 3, 5, 7 })]
    [TestCase(":nth-child(even)", new[] { 2, 4, 6 })]
    [TestCase(":nth-child(3n+1)", new[] { 1, 4, 7 })]
    [TestCase(":nth-child(-n+3)", new[] { 1, 2, 3 })]
    [TestCase(":nth-child(n+5)", new[] { 5, 6, 7 })]
    [TestCase(":nth-child(n+3):nth-child(-n+5)", new[] { 3, 4, 5 })]
    [TestCase(":nth-child(0n+4)", new[] { 4 })]
    [TestCase(":nth-child(4)", new[] { 4 })]
    [TestCase(":nth-child(-2)", new int[0])]
    [TestCase(":nth-last-child(2)", new[] { 6 })]
    [TestCase(":nth-last-child(odd)", new[] { 1, 3, 5, 7 })]
    [TestCase("p:nth-of-type(2)", new[] { 2 })]
    [TestCase("p:nth-of-type(even)", new[] { 2, 6 })]
    [TestCase(":nth-of-type(2)", new[] { 2, 5 })]
    [TestCase("p:nth-last-of-type(1)", new[] { 7 })]
    [TestCase("p:nth-last-of-type(-n+2)", new[] { 6, 7 })]
    [TestCase(":not(:first-child)", new[] { 2, 3, 4, 5, 6, 7 })]
    [TestCase("p:not(:last-of-type)", new[] { 1, 2, 4, 6 })]
    [TestCase(":not(p)", new[] { 3, 5 })]
    [TestCase(":not(p, h2)", new int[0])]
    [TestCase(":not(:nth-child(odd), h2)", new[] { 2, 4, 6 })]
    [TestCase(":not(:not(:first-child))", new[] { 1 })]
    [TestCase("p:nth-child(2)", new[] { 2 })]
    [TestCase(":FIRST-CHILD", new[] { 1 })]
    [TestCase(":Nth-Child(EVEN)", new[] { 2, 4, 6 })]
    public void StructuralSelectorsMatchTheSiblingsChromeMatches(string selector, int[] expected)
        => Assert.That(Matched("div > " + selector + "{color:#f00}"), Is.EqualTo(expected));

    // An+B spellings with Chrome's matches over seven siblings (CSS Syntax 3 section 6).
    [TestCase("odd", new[] { 1, 3, 5, 7 })] [TestCase(" odd ", new[] { 1, 3, 5, 7 })]
    [TestCase("2n+1", new[] { 1, 3, 5, 7 })] [TestCase("2n + 1", new[] { 1, 3, 5, 7 })]
    [TestCase("2n+ 1", new[] { 1, 3, 5, 7 })] [TestCase("2n +1", new[] { 1, 3, 5, 7 })]
    [TestCase("2n- 1", new[] { 1, 3, 5, 7 })] [TestCase("2n -1", new[] { 1, 3, 5, 7 })]
    [TestCase("2n-1", new[] { 1, 3, 5, 7 })] [TestCase("+2n+1", new[] { 1, 3, 5, 7 })]
    [TestCase("2N+1", new[] { 1, 3, 5, 7 })] [TestCase("2n+0001", new[] { 1, 3, 5, 7 })]
    [TestCase("-n+3", new[] { 1, 2, 3 })] [TestCase("-N+3", new[] { 1, 2, 3 })] [TestCase("-1n+3", new[] { 1, 2, 3 })]
    [TestCase("+n", new[] { 1, 2, 3, 4, 5, 6, 7 })] [TestCase("n", new[] { 1, 2, 3, 4, 5, 6, 7 })]
    [TestCase("n-1", new[] { 1, 2, 3, 4, 5, 6, 7 })] [TestCase("+n-1", new[] { 1, 2, 3, 4, 5, 6, 7 })]
    [TestCase("n- 1", new[] { 1, 2, 3, 4, 5, 6, 7 })] [TestCase("n - 1", new[] { 1, 2, 3, 4, 5, 6, 7 })]
    [TestCase("-n-1", new int[0])] [TestCase("-n- 1", new int[0])] [TestCase("0n-3", new int[0])]
    [TestCase("+5", new[] { 5 })] [TestCase("5", new[] { 5 })] [TestCase("-0n+3", new[] { 3 })]
    [TestCase("1000000000n+1", new[] { 1 })] [TestCase("-1000000000n+5", new[] { 5 })]
    public void AnPlusBSyntaxFollowsCssSyntax(string argument, int[] expected)
        => Assert.That(Matched("div > :nth-child(" + argument + "){color:#f00}"), Is.EqualTo(expected));

    [TestCase("p:nth-child(){color:red}")]
    [TestCase("p:nth-child( ){color:red}")]
    [TestCase("p:nth-child(- n+3){color:red}")]
    [TestCase("p:nth-child(+ n){color:red}")]
    [TestCase("p:nth-child(+ 5){color:red}")]
    [TestCase("p:nth-child(2n+-1){color:red}")]
    [TestCase("p:nth-child(2n+ +1){color:red}")]
    [TestCase("p:nth-child(3n+){color:red}")]
    [TestCase("p:nth-child(n-+1){color:red}")]
    [TestCase("p:nth-child(2 n){color:red}")]
    [TestCase("p:nth-child(1.5n){color:red}")]
    [TestCase("p:nth-child(5.0){color:red}")]
    [TestCase("p:nth-child(1e1){color:red}")]
    [TestCase("p:nth-child(+-1){color:red}")]
    [TestCase("p:nth-child(odd 1){color:red}")]
    [TestCase("p:nth-child(2n + 1 + 1){color:red}")]
    [TestCase("p:nth-child(x){color:red}")]
    [TestCase("p:nth-child([1]){color:red}")]
    [TestCase("p:nth-child(1000000001){color:red}")]
    [TestCase("p:nth-child(-1000000001n+1){color:red}")]
    [TestCase("p:nth-child(n-1000000001){color:red}")]
    [TestCase("p:nth-child{color:red}")]
    [TestCase("p:first-child(){color:red}")]
    [TestCase("p:empty(){color:red}")]
    [TestCase("p : first-child{color:red}")]
    [TestCase("p: first-child{color:red}")]
    [TestCase("p::first-line{color:red}")]
    [TestCase("p:first-child::first-line{color:red}")]
    [TestCase("p:first-line{color:red}")]
    [TestCase("p:root(){color:red}")]
    [TestCase("p:not(){color:red}")]
    [TestCase("p:not( ){color:red}")]
    [TestCase("p:not(.a,){color:red}")]
    [TestCase("p:not(,.a){color:red}")]
    [TestCase("p:not(.a,,.b){color:red}")]
    [TestCase("p:not(::before){color:red}")]
    [TestCase("p:not(.a)x{color:red}")]
    [TestCase("p:nth-child(2)p{color:red}")]
    [TestCase(":{color:red}")]
    public void InvalidOrUnsupportedPseudoClassSyntaxFails(string css)
        => Fails("<style>" + css + "</style><div><p>A</p></div>", "FPDF1203");

    [Test]
    public void PseudoClassesCountAsClassesAndNotTakesItsMostSpecificArgument()
    {
        // (0,1,1) beats (0,0,1) whatever the order.
        Assert.That(Matched("p:first-child{color:#f00}p{color:#00f}"), Is.EqualTo(new[] { 1 }));
        // Equal specificity (0,1,1): the later rule wins.
        Assert.That(Colors("p:first-child{color:#f00}p.x{color:#00f}", "<div><p class=x>A</p></div>"), Is.EqualTo(new[] { Blue }));
        Assert.That(Colors("p.x{color:#00f}p:first-child{color:#f00}", "<div><p class=x>A</p></div>"), Is.EqualTo(new[] { Red }));
        // :not(#x) adds (1,0,0), beating three classes.
        Assert.That(Colors(".a:not(#x){color:#f00}.a.a.a{color:#00f}", "<div><p class=a>A</p></div>"), Is.EqualTo(new[] { Red }));
        // :not(.x, #y) takes its most specific argument, (1,0,0), even though .x is what fails to match.
        Assert.That(Colors("p:not(.x, #y){color:#f00}p.a.a.a{color:#00f}", "<div><p class=a>A</p></div>"), Is.EqualTo(new[] { Red }));
        // :not(.x) is (0,1,0): p:not(.x) (0,1,1) loses to p.a.a (0,2,1).
        Assert.That(Colors("p:not(.x){color:#f00}p.a.a{color:#00f}", "<div><p class=a>A</p></div>"), Is.EqualTo(new[] { Blue }));
        // :not(p) adds a type (0,0,1); :nth-child() adds a class like any other pseudo-class.
        Assert.That(Colors(".a:not(div){color:#f00}p.a{color:#00f}", "<div><p class=a>A</p></div>"), Is.EqualTo(new[] { Blue }));
        Assert.That(Colors("p.a{color:#00f}.a:not(div){color:#f00}", "<div><p class=a>A</p></div>"), Is.EqualTo(new[] { Red }));
        Assert.That(Colors(".a:nth-child(1){color:#f00}.a.a{color:#00f}", "<div><p class=a>A</p></div>"), Is.EqualTo(new[] { Blue }));
        // Inline style and !important still outrank any selector.
        Assert.That(Colors("p:first-child:not(#x){color:#f00!important}", "<div><p style='color:#00f'>A</p></div>"), Is.EqualTo(new[] { Red }));
        Assert.That(Colors("p:first-child:not(#x){color:#f00}", "<div><p style='color:#00f'>A</p></div>"), Is.EqualTo(new[] { Blue }));
    }

    [Test]
    public void ZebraAndFirstLastRowRulesRenderLikeTheirExplicitlyStyledEquivalent()
    {
        static string Table(Func<int, string> cellStyle)
        {
            var html = new StringBuilder("<table class='table' style='width:200pt'><colgroup><col style='width:200pt'></colgroup><tbody>");
            for (var row = 1; row <= 5; row++) html.Append("<tr><td").Append(cellStyle(row)).Append(">Row ").Append(row).Append("</td></tr>");
            return html.Append("</tbody></table>").ToString();
        }
        var actual = "<style>.table tbody tr:nth-of-type(even) td{color:#336699}" +
            ".table tbody tr:last-of-type td{font-size:9pt}.table tbody tr:first-of-type > td{color:#ae0f1a}</style>" + Table(_ => "");
        var control = Table(row => row switch
        {
            1 => " style='color:#ae0f1a'",
            5 => " style='font-size:9pt'",
            _ when row % 2 == 0 => " style='color:#336699'",
            _ => ""
        });
        Assert.That(TableLayoutTests.Pdf(actual), Is.EqualTo(TableLayoutTests.Pdf(control)));
    }

    [Test]
    public void ImpliedParagraphAndListItemEndTagsCloseSiblings()
    {
        Assert.That(Matched("li:last-child{color:#f00}", "<ul><li>a<li>b<li>c</ul>"), Is.EqualTo(new[] { 3 }));
        Assert.That(Matched("p:nth-of-type(2){color:#f00}", "<section><p>A<p>B<div>C</div><p>D</section>"), Is.EqualTo(new[] { 2 }));
        Assert.That(Matched(":nth-child(3){color:#f00}", "<section><p>A<p>B<div>C</div><p>D</section>"), Is.EqualTo(new[] { 3 }));
    }

    [Test]
    public void EveryElementCountsAsASiblingIncludingDroppedScriptsAndVoidElements()
    {
        // The script and template are dropped before rendering but are siblings in the DOM.
        Assert.That(Matched("p:nth-child(3){color:#f00}", "<div><p>A</p><script>var x;</script><p>B</p></div>"), Is.EqualTo(new[] { 2 }));
        Assert.That(Matched("p:nth-child(3){color:#f00}", "<div><p>A</p><template><p>T</p></template><p>B</p></div>"), Is.EqualTo(new[] { 2 }));
        Assert.That(Matched("p:first-of-type{color:#f00}", "<div><script></script><p>A</p></div>"), Is.EqualTo(new[] { 1 }));
        Assert.That(Matched("p:first-child{color:#f00}", "<div><script></script><p>A</p></div>"), Is.EqualTo(Array.Empty<int>()));
        Assert.That(Matched("p:nth-child(2){color:#f00}", "<div><style>i{color:#000}</style><p>A</p></div>"), Is.EqualTo(new[] { 1 }));
        Assert.That(Matched("p:nth-child(3){color:#f00}", "<div><p>A</p><hr><p>B</p></div>"), Is.EqualTo(new[] { 2 }));
        var spans = Layout("<style>span:last-child{color:#f00}</style><p><span>A</span><br><span>B</span><br></p>");
        Assert.That(spans.Where(r => r.Text.Length > 0).Select(r => r.Style.Color), Is.EqualTo(new[] { Black, Black }));
    }

    [Test]
    public void DocumentLevelContentIsCountedWhereTheHtmlParserPutsIt()
    {
        // A fragment's style element goes into the implied head, its content into the implied body.
        Assert.That(Runs("<style>p:first-child{color:#f00}</style><p>A</p><p>B</p>"), Is.EqualTo(new[] { Red, Black }));
        // A style element inside body is a sibling like any other.
        Assert.That(Runs("<html><head></head><body><p>A</p><style>p:nth-child(3){color:#f00}</style><p>B</p></body></html>"),
            Is.EqualTo(new[] { Black, Red }));
        // Content after </body> or </html> is appended to body.
        Assert.That(Runs("<html><head><style>p:last-child{color:#f00}</style></head><body><p>A</p></body></html><p>B</p>"),
            Is.EqualTo(new[] { Black, Red }));
        // A style element after </head> still goes into head, so the first p stays body's first child.
        Assert.That(Runs("<html><head></head><style>p:first-child{color:#f00}</style><body><p>A</p><p>B</p></body></html>"),
            Is.EqualTo(new[] { Red, Black }));
        // Text before the first element starts the body, so a later style element is in body.
        Assert.That(Runs("Intro<style>p:nth-child(2){color:#f00}</style><p>A</p>").Skip(1), Is.EqualTo(new[] { Red }));
        // html is the only element of the document; body is html's second child after the implied head.
        Assert.That(Runs("<html><head><style>html:only-child body:nth-child(2):last-child p{color:#f00}</style></head><body><p>A</p></body></html>"),
            Is.EqualTo(new[] { Red }));
        Assert.That(Runs("<style>body:first-child p{color:#f00}</style><body><p>A</p></body>"), Is.EqualTo(new[] { Black }));
    }

    // The HTML parser ignores a repeated or misplaced html/head/body tag without attributes, so its
    // content stays where it is (Chrome: B is A's next sibling in body).
    [Test]
    public void IgnoredDocumentTagsDoNotMoveSiblings()
    {
        Assert.That(Runs("<style>p:first-child{color:#f00}p:last-child{color:#00f}</style><p>A</p><body><p>B</p></body>"),
            Is.EqualTo(new[] { Red, Blue }));
        Assert.That(Runs("<html><body><p>A</p></body></html><body><p>B</p></body><style>p:nth-child(2){color:#f00}</style>"),
            Is.EqualTo(new[] { Black, Red }));
        Assert.That(Runs("<style>div > p:only-child{color:#f00}</style><div><body><p>A</p></body></div>"), Is.EqualTo(new[] { Red }));
        Assert.That(Runs("<style>div > p:only-child{color:#f00}</style><div><html><head><p>A</p></head></html></div>"), Is.EqualTo(new[] { Red }));
    }

    [TestCase("<p>A</p><body class=x><p>B</p></body>")]
    [TestCase("<html><head></head><body><div><html lang=en><p>A</p></html></div></body></html>")]
    [TestCase("<p>A</p><head id=h></head>")]
    public void IgnoredDocumentTagsWithAttributesFail(string html)
        => Fails("<style>p:first-child{color:red}</style>" + html, "FPDF1101");

    [Test]
    public void EmptyMatchesNoChildElementsOrTextIncludingWhitespaceLikeChrome()
    {
        // Chrome: whitespace-only text is content; a comment is not.
        static byte[] Pdf(string body) => TableLayoutTests.Pdf("<html><head><style>div:empty{border-top:2pt solid #f00}" +
            "p:not(:empty){color:#f00}</style></head><body>" + body + "</body></html>");
        // Distinct paddings place each div's border at its own height.
        var actual = Pdf("<div style='padding-top:4pt'> </div><div style='padding-top:8pt'><!-- note --></div>" +
            "<div style='padding-top:16pt'></div><p>A</p>");
        Assert.That(actual, Is.EqualTo(Pdf("<div style='padding-top:4pt'> </div>" +
            "<div style='padding-top:8pt;border-top:2pt solid #f00'></div>" +
            "<div style='padding-top:16pt;border-top:2pt solid #f00'></div><p style='color:#f00'>A</p>")));
        Assert.That(actual, Is.Not.EqualTo(Pdf("<div style='padding-top:4pt;border-top:2pt solid #f00'> </div>" +
            "<div style='padding-top:8pt;border-top:2pt solid #f00'></div>" +
            "<div style='padding-top:16pt'></div><p style='color:#f00'>A</p>")));
        Assert.That(Matched("div:not(:empty) p{color:#f00}", "<div><p>A</p></div>"), Is.EqualTo(new[] { 1 }));
    }

    // Chrome: whitespace and character references are text; comments are not; the HTML parser
    // drops one line feed after a pre start tag; implied end tags leave an element empty.
    [TestCase("<div id=e></div>", true)]
    [TestCase("<div id=e><!-- note --></div>", true)]
    [TestCase("<div id=e><!-- a --><!-- b --></div>", true)]
    [TestCase("<div id=e> </div>", false)]
    [TestCase("<div id=e>\n</div>", false)]
    [TestCase("<div id=e>&nbsp;</div>", false)]
    [TestCase("<div id=e><span></span></div>", false)]
    [TestCase("<div id=e><br></div>", false)]
    [TestCase("<div id=e><script></script></div>", false)]
    [TestCase("<div id=e><style></style></div>", false)]
    [TestCase("<pre id=e>\n</pre>", true)]
    [TestCase("<pre id=e>\r\n</pre>", true)]
    [TestCase("<pre id=e>\n\n</pre>", false)]
    [TestCase("<pre id=e> \n</pre>", false)]
    [TestCase("<p id=e><p>A</p>", true)]
    [TestCase("<ul><li id=e><li>A</ul>", true)]
    [TestCase("<table style='width:9pt'><colgroup><col style='width:9pt'></colgroup><tbody><tr><td id=e></td></tr></tbody></table>", true)]
    public void EmptyFollowsChromeForWhitespaceCommentsAndPre(string body, bool empty)
    {
        var html = "<html><head><style>:empty{color:red}</style></head><body>" + body + "</body></html>";
        var tokens = new List<HtmlToken>();
        var sheets = CssStylesheets.Collect(html, new PdfOptions(), tokens, default);
        sheets.TrackPositions(tokens, default);
        var element = tokens.FindIndex(t => t.Kind == HtmlTokenKind.Start && t.Attributes!.TryGetValue("id", out var id) && id == "e");
        var position = sheets.Positions![element];
        Assert.That(position.Empty, Is.EqualTo(empty));
    }

    [Test]
    public void EmptyOnBodyCountsWhitespaceAfterTheBodyEndTag()
    {
        // Chrome appends the line feed after </body> to body, so body is not empty.
        Assert.That(Runs("<html><head><style>body:not(:empty) p{color:#f00}</style></head><body><p>A</p></body>\n</html>"),
            Is.EqualTo(new[] { Red }));
        static bool BodyEmpty(string html)
        {
            var tokens = new List<HtmlToken>();
            var sheets = CssStylesheets.Collect(html, new PdfOptions(), tokens, default);
            sheets.TrackPositions(tokens, default);
            return sheets.Positions![tokens.FindIndex(t => t.Kind == HtmlTokenKind.Start && t.Value == "body")].Empty;
        }
        Assert.That(BodyEmpty("<style>:empty{color:red}</style><body></body>"), Is.True);
        Assert.That(BodyEmpty("<style>:empty{color:red}</style><body></body>\n"), Is.False);
        Assert.That(BodyEmpty("<style>:empty{color:red}</style>"), Is.True);
    }

    [Test]
    public void SelectorSupportDoesNotHideUnsupportedDeclarations()
    {
        var table = "<table class='table' style='width:100pt'><colgroup><col style='width:100pt'></colgroup><tbody>" +
            "<tr><td>A</td></tr><tr><td>B</td></tr></tbody></table>";
        Fails("<style>.table tbody tr:nth-of-type(even){padding:2pt}</style>" + table, "FPDF1201");
        // A row border is validated and dropped in the separated model, where Chrome paints none.
        Assert.DoesNotThrow(() => HtmlDocumentReader.ReadTree(
            "<style>.table tbody tr:last-of-type{border-bottom:2px solid #ae0f1a}</style>" + table, new PdfOptions(), default));
        Fails("<style>.divider:not(:empty){gap:1}</style><div class=divider>A</div>", "FPDF1202");
        // Validated even when no element matches.
        Fails("<style>.missing:first-child{gap:1}</style><div>A</div>", "FPDF1202");
    }

    [Test]
    public void PositionsAreBuiltOnlyWhenASelectorNeedsThem()
    {
        static HtmlElementPositions? Positions(string html)
        {
            var tokens = new List<HtmlToken>();
            var sheets = CssStylesheets.Collect(html, new PdfOptions(), tokens, default);
            sheets.TrackPositions(tokens, default);
            return sheets.Positions;
        }
        Assert.That(Positions("<style>p:not(.a){color:red}</style><p>A</p>"), Is.Null);
        Assert.That(Positions("<style>p{color:red}</style><p>A</p>"), Is.Null);
        Assert.That(Positions("<style>p:not(:last-child){color:red}</style><p>A</p>"), Is.Not.Null);
    }

    [Test]
    public void StructuralMatchingIsChargedToTheCssWorkBudget()
    {
        var css = new StringBuilder("<style>");
        for (var i = 0; i < 40; i++) css.Append("div:not(.x").Append(i).Append("):nth-child(2n+1):not(:empty) p{color:red}");
        var body = string.Concat(Enumerable.Repeat("<div><div><p>A</p></div></div>", 40));
        Fails(css + "</style>" + body, "FPDF1205", new PdfOptions { MaxCssMatchOperations = 2_000 });
    }

    [Test]
    public void NotNestingAndArgumentCountAreBounded()
    {
        var deep = string.Concat(Enumerable.Repeat(":not(", 33)) + ".a" + new string(')', 33);
        Fails("<style>p" + deep + "{color:red}</style><p>A</p>", "FPDF1205");
        var wide = string.Join(",", Enumerable.Range(0, 33).Select(i => ".a" + i));
        Fails("<style>p:not(" + wide + "){color:red}</style><p>A</p>", "FPDF1205");
        var nested = string.Concat(Enumerable.Repeat(":not(", 10)) + ".a" + new string(')', 10);
        Assert.That(Matched("p" + nested + "{color:#f00}", "<div><p class=a>A</p><p>B</p></div>"), Is.EqualTo(new[] { 1 }));
    }

    [Test]
    public void ErrorsPointAtTheSelector()
    {
        var html = "<style>p{color:red} li:nth-child(2n+ +1){color:red}</style><p>A</p>";
        var ex = Fails(html, "FPDF1203");
        Assert.That(ex.SourceOffset, Is.GreaterThanOrEqualTo(html.IndexOf("li:", StringComparison.Ordinal)));
        Assert.That(ex.SourceOffset, Is.LessThan(html.IndexOf('{', html.IndexOf("li:", StringComparison.Ordinal))));
    }

    private static List<PlacedText> Layout(string html)
        => TableLayoutTests.Layout(html).SelectMany(page => page.Runs).ToList();

    private static Rgb[] Runs(string html)
        => Layout(html).Where(run => run.Text.Trim().Length > 0).Select(run => run.Style.Color).ToArray();
}
